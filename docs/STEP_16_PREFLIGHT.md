# 第16步｜不可变发布版本、预览与逐群启动预检

日期：2026-10-16，仓库 `MS_001`。

## 发布版本
- 本机 `SQLite` 第6版迁移创建 `published_script_snapshots`，关联既有 `script_versions`、`script_roles`、`script_steps`、`media_assets`。严格 revision 对比：只有当前、未归档、已保存草稿可发布；空剧本不能发布。
- 快照保存角色 UUID/备注、正文、独立毫秒计时、媒体引用和默认时间，计算 SHA-256 校验并拒绝被修改的发布快照。媒体完整性在写入事务前校验，完整版本在同一个 SQL 事务内保存，异常整体回滚。
- 版本号单调递增；后续草稿修改与发布版本独立。发布对象的 `scriptId/version/sha256` 和角色键、步骤按版本固定。
- `scripts.publish` 仅存本地已发布内容，**不是启动**。编辑器的发布、版本选择、只读时间线预览均不触发 Telegram 通讯。

## 时间、消息和链接预览
- `ScriptPreviewBuilder` 显示逐条发言备注、正文、媒体显示名、`preSendDelayMs`、`typingDurationMs`、`postSendDelayMs`，并解析文字中合法的 http/https 链接实体与 UTF-16 偏移。
- 每条最早进入发送阶段时间 = 前等待 + 正在输入；30秒+5秒=35秒，二者绝不重叠。
- 总时长显示**不含网络、回执、慢速模式的理论最小值**；实际后等待必须从发送确认起计。时间线不会猜测 Telegram 的网络耗时。

## 逐群/话题的预检矩阵
`src/Core/Preflight/PreflightPlanner.cs` 对每个目标分别检查：
1. 群是否来自可信 TDLib，同一群/话题是否重复；论坛话题必选且不可关闭/隐藏；正在运行/暂停/待审任务冲突。
2. 图片 SHA、MIME、尺寸及本地读取是否匹配；单张文件异常必须阻止运行，不得跳过该条。
3. 逐角色（只针对消息真正使用到的角色）使用 NFC + trim 后 **大小写敏感完整备注相等**；所有缺少备注分别报告 `ROLE_MISSING`。
4. 即时授权、成员身份、发送文字/图片权限、60秒内群权限核验；无可用账号报 `ACCOUNT_UNAVAILABLE`，同名多个可用账号报 `ROLE_AMBIGUOUS`。
5. 用户显式选人时，必须在该群/话题内对应的合格候选中匹配；不得使用失去授权的账号、绕过限流或随机轮换身份。

全部校验通过才构造 `FrozenTarget`，包含每个群/话题的 `FrozenBinding` 和独立 `FrozenStep`；如任一目标存在问题，整体 `Ready=false` 且不输出部分可执行计划。编辑草稿不能修改已冻结的步骤内容。第17步调度器需要进一步持久化并重新检查即时发送政策，而不是直接相信旧预检。

## 当前桌面实现的重要安全边界
- 发布、版本读取、时间线预览与 `scripts.preflight` 使用本地 WebView2 严格 IPC 白名单（无任意文件路径、秘密字段或 Telegram 发送指令）。
- UI 能选官方同步群、论坛话题、同名本机备注账号；“检查启动条件”会显示全部错误，但**不会创建任务**。
- **TDLib 原生授权尚未实机接入**；当前 Windows 处理器明确传入 `LiveAuthorized=false`、`MembershipsFreshForStart=false`，故真实界面必须 fail-closed，不能把 SQLite 的历史 `ready` 或缓存群成员当作实时验证。这部分将在官方登录/运行集成和最终实机验收中接线。
- Windows10/11 文件与窗口交互、真实 Telegram 授权/权限刷新、实际任务冻结持久化、50群压力/长稳仍未验收（DEFERRED）。
- MS002 Signal 项目及现有生产 Cloudflare 卡密数据不得修改。

下一步：第17步任务状态机与群任务调度；先在模拟环境验证，正式运行必须等实时授权与权限路径真正接通。
