# MOSEN Group Scheduler — Telegram 真实账号版（开发规划仓库资料）

> **当前状态：步骤 01（需求及许可范围冻结）已完成，步骤 02 待开始；已完成 1 / 32（3.125%）。尚无正式功能代码或已部署服务。**

用户要求：独立 Windows 桌面程序，可管理最多 300 条合法授权 Telegram 账号会话，管理 50 个群/话题任务；编辑剧本时按账号备注下拉选择发言人；导出导入支持按角色备注跨设备匹配；缺少备注在“开始运行”时提醒；每条消息有发送前等待、正在输入时间、发送后等待三项独立设置。

产品与 Signal Auto Scheduler MS002 **完全隔离**，不访问、不更改其 GitHub 仓库、安装目录、会话或 Cloudflare 生产授权数据。

## 最重要的入口（新 GPT 必读）

1. `state/project_state.json` — 机器可读的准确步骤与状态。
2. `docs/PROGRESS.md` — 进度流水、测试结果、提交 SHA、阻断事项。
3. `docs/MASTER_PLAN_32_STEPS.md` — **8 个阶段/32 个可独立完成的具体步骤**。
4. `docs/HANDOFF.md` — 上下文过长、新对话或中断时的严格接力规则。
5. `templates/RESUME_PROMPT.md` — 用户复制到新 GPT 对话的一段恢复指令。
6. `docs/EDITOR_AND_ROLES.md` — 剧本交互和“三段时间”的最新最终约定。
7. `docs/PRODUCT_BASELINE.md`、`docs/ARCHITECTURE.md`、`docs/TEST_MATRIX.md` — 产品/技术/质量基线。
8. `docs/DECISIONS.md` — 已决定事项以及尚待用户确认的问题。
9. `docs/RELEASE_CHECKLIST.md` — 正式发布唯一门禁。

## 如何用

- 规划资料提交到当前指定仓库：`https://github.com/mosen6266-netizen/MS_001`。
- 用户已提供图标并确认 MS_001 仓库；从步骤 01 按顺序实现；**一次一个步骤，测试、提交、更新两份进度文件后再继续**。
- 切换新对话时，带上仓库 URL 并发送 `templates/RESUME_PROMPT.md` 的内容。

## 已确认的严格计时顺序

每条消息严格按照 `发送前等待 preSendDelayMs → 正在输入 typingDurationMs → 发出消息 → 确认 → 发送后等待 postSendDelayMs` 执行。举例：发送前等待 30 秒，正在输入 5 秒，则在第 0–30 秒等待、第 30–35 秒呈现正在输入状态，在第 35 秒发出消息（实际到达时间受网络影响）。二者**不重叠**，绝不把正在输入的 5 秒包含在前面 30 秒里。

## 图标和品牌

用户已提供 400×400 WEBP 原始图标素材，待放入 `assets/branding/`；源图带棋盘格背景，正式安装图标要完成透明度校验，并在正式公开发布前审查第三方客户端使用 Telegram 标志的限制。

## 容量与平台说明

**300 个已保存授权会话 ≠ 300 个账号可以同时持续在线；50 群任务也不是无限并发发送承诺。** 容量必须经压测验收并遵守平台速率/群权限限制。禁止将程序用于虚假群聊互动、未经请求的广告群发、伪造人员身份或绕过 Telegram 限制。

参考链接：https://core.telegram.org/tdlib/getting-started 、https://core.telegram.org/api/terms。
## 图标源与生成

已保存透明 SVG：[`assets/branding/icon.svg`](assets/branding/icon.svg)，依据用户提供图形转换；通过 [`tools/branding/build_icons.py`](tools/branding/build_icons.py) 可生成 Windows 多尺寸 ICO 与 PNG。正式发布需要完成商标合规审查，并避免暗示软件是 Telegram 官方产品。

## 每一步的可视化进度汇报
完成某步骤的正式验收后必须向用户显示进度条、已完成步骤数 /32、百分比、剩余步骤、当前/下一步以及测试/commit证据；正在进行而未验收的步骤不计入完成。当前第02步待开始，**1/32（3.125%）**。详细要求见 `docs/HANDOFF.md`。

## 最新需求冻结文件

- [P0/P1/P2需求与32步关联](docs/REQUIREMENTS_MATRIX.md)
- [非目标与平台安全边界](docs/NON_GOALS.md)
- [Step01文档验收证据](docs/STEP_01_ACCEPTANCE.md)

公开仓库提醒：当前MS_001为Public，切勿提交账号会话、令牌或个人信息。Win10常规支持已结束，兼容性将在后续实测。
