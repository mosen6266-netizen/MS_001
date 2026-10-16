# MOSEN Group Scheduler — Telegram 真实账号版（开发规划仓库资料）

> **当前状态：第24步（性能预算、资源监控与防泄漏）开发/CI验收完成；第25步独立卡密产品授权待开始。开发进度 24/32（75%）。真实TDLib账号登录与发送、正式Windows安装及用户实机测试仍未完成。**

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
- 用户已确认 MS_001 仓库并开始按32步开发；**一次一个步骤，测试、提交、更新两份进度文件后再继续**。
- 切换新对话时，带上仓库 URL 并发送 `templates/RESUME_PROMPT.md` 的内容。

## 已确认的严格计时顺序

每条消息严格按照 `发送前等待 preSendDelayMs → 正在输入 typingDurationMs → 发出消息 → 确认 → 发送后等待 postSendDelayMs` 执行。举例：发送前等待 30 秒，正在输入 5 秒，则在第 0–30 秒等待、第 30–35 秒呈现正在输入状态，在第 35 秒发出消息（实际到达时间受网络影响）。二者**不重叠**，绝不把正在输入的 5 秒包含在前面 30 秒里。

## 图标和品牌

用户已提供 400×400 图标原样素材，透明转换版已保存在 `assets/branding/icon.svg`；源图带棋盘格背景，正式安装图标要完成透明度校验，并在正式公开发布前审查第三方客户端使用 Telegram 标志的限制。

## 容量与平台说明

**300 个已保存授权会话 ≠ 300 个账号可以同时持续在线；50 群任务也不是无限并发发送承诺。** 容量必须经压测验收并遵守平台速率/群权限限制。禁止将程序用于虚假群聊互动、未经请求的广告群发、伪造人员身份或绕过 Telegram 限制。

参考链接：https://core.telegram.org/tdlib/getting-started 、https://core.telegram.org/api/terms。
## 图标源与生成

已保存透明 SVG：[`assets/branding/icon.svg`](assets/branding/icon.svg)，依据用户提供图形转换；通过 [`tools/branding/build_icons.py`](tools/branding/build_icons.py) 可生成 Windows 多尺寸 ICO 与 PNG。正式发布需要完成商标合规审查，并避免暗示软件是 Telegram 官方产品。

## 每一步的可视化进度汇报
完成某步骤的正式验收后必须向用户显示进度条、已完成步骤数 /32、百分比、剩余步骤、当前/下一步以及测试/commit证据；正在进行而未验收的步骤不计入完成。当前第25步待开始；已通过开发阶段验收的为 **24/32（75%）**，真实Windows/Telegram测试仍 DEFERRED。详细要求见 `docs/HANDOFF.md`。

## 最新需求冻结文件

- [P0/P1/P2需求与32步关联](docs/REQUIREMENTS_MATRIX.md)
- [非目标与平台安全边界](docs/NON_GOALS.md)
- [Step01文档验收证据](docs/STEP_01_ACCEPTANCE.md)

公开仓库提醒：当前MS_001为Public，切勿提交账号会话、令牌或个人信息。Win10常规支持已结束，兼容性将在后续实测。

- [Step04 SQLite 与故障恢复验收](docs/STEP_04_ACCEPTANCE.md) · [自动化测试](https://github.com/mosen6266-netizen/MS_001/actions/runs/38922965210)

- [Step05 Windows容器与导航验收](docs/STEP_05_ACCEPTANCE.md) · [CI自动化结果](https://github.com/mosen6266-netizen/MS_001/actions/runs/38924326676)

- [Step06 深色UI设计规范](docs/UI_DESIGN.md) · [开发阶段验收](docs/STEP_06_ACCEPTANCE.md) · [CI测试及截图](https://github.com/mosen6266-netizen/MS_001/actions/runs/38926182509)

- [Step07 设置与搜索规范](docs/STEP_07_SETTINGS.md) · [Step07 开发验收](docs/STEP_07_ACCEPTANCE.md) · [9项CI作业](https://github.com/mosen6266-netizen/MS_001/actions/runs/38927977333)

- [Step08 本地安全设计](docs/STEP_08_SECURITY.md) · [威胁模型](docs/SECURITY_THREAT_MODEL.md) · [开发验收](docs/STEP_08_ACCEPTANCE.md) · [CI安全检查](https://github.com/mosen6266-netizen/MS_001/actions/runs/38930950275)

- [Step09 TDLib适配器规范](docs/STEP_09_TDLIB.md) · [官方原生来源锁](native/tdlib/source-lock.json) · [第09步开发验收](docs/STEP_09_ACCEPTANCE.md) · [13项CI作业](https://github.com/mosen6266-netizen/MS_001/actions/runs/38933092843)

- [Step10 认证状态与安全交互](docs/STEP_10_AUTH.md) · [认证测试矩阵](docs/STEP_10_SECURITY_TEST_MATRIX.md) · [Step10开发验收](docs/STEP_10_ACCEPTANCE.md) · [CI全部15项作业](https://github.com/mosen6266-netizen/MS_001/actions/runs/38935436769)

- [Step11 账号注册表设计](docs/STEP_11_ACCOUNTS.md) · [测试矩阵](docs/STEP_11_TEST_MATRIX.md) · [开发验收](docs/STEP_11_ACCEPTANCE.md) · [CI全绿](https://github.com/mosen6266-netizen/MS_001/actions/runs/38940132317)

- [Step12 群/话题和权限规范](docs/STEP_12_GROUP_CATALOG.md) · [权限测试矩阵](docs/STEP_12_TEST_MATRIX.md) · [开发验收](docs/STEP_12_ACCEPTANCE.md) · [19项CI作业](https://github.com/mosen6266-netizen/MS_001/actions/runs/38942859853)

- [Step13 可视剧本编辑器](docs/STEP_13_SCRIPT_EDITOR.md) · [草稿测试矩阵](docs/STEP_13_TEST_MATRIX.md) · [第13步开发验收](docs/STEP_13_ACCEPTANCE.md) · [全部CI作业](https://github.com/mosen6266-netizen/MS_001/actions/runs/38946517290)

- [Step14 三段时间及图片编辑](docs/STEP_14_TIMING_AND_MEDIA.md) · [测试矩阵](docs/STEP_14_TEST_MATRIX.md) · [开发验收](docs/STEP_14_ACCEPTANCE.md) · [全部23项CI](https://github.com/mosen6266-netizen/MS_001/actions/runs/38949689786)

- [Step15 便携剧本ZIP格式](docs/SCRIPT_FORMAT.md) · [测试矩阵](docs/STEP_15_TEST_MATRIX.md) · [开发验收](docs/STEP_15_ACCEPTANCE.md) · [CI 25/25全绿](https://github.com/mosen6266-netizen/MS_001/actions/runs/38954528584)

- [Step16 发布快照与启动预检设计](docs/STEP_16_PREFLIGHT.md) · [测试矩阵](docs/STEP_16_TEST_MATRIX.md) · [开发验收](docs/STEP_16_ACCEPTANCE.md) · [CI 27/27](https://github.com/mosen6266-netizen/MS_001/actions/runs/38956311705)

- [Step17 调度状态机与50群模拟](docs/STEP_17_SCHEDULER.md) · [测试矩阵](docs/STEP_17_TEST_MATRIX.md) · [开发验收](docs/STEP_17_ACCEPTANCE.md) · [CI 29/29](https://github.com/mosen6266-netizen/MS_001/actions/runs/38957627884)

- [Step18 账号出站规则与限流](docs/STEP_18_OUTBOUND_POLICY.md) · [测试矩阵](docs/STEP_18_TEST_MATRIX.md) · [开发验收](docs/STEP_18_ACCEPTANCE.md) · [CI 30/30](https://github.com/mosen6266-netizen/MS_001/actions/runs/38958656142)

- [Step19 投递回执与去重](docs/STEP_19_DELIVERY.md) · [测试矩阵](docs/STEP_19_TEST_MATRIX.md) · [开发验收](docs/STEP_19_ACCEPTANCE.md) · [CI 30/30](https://github.com/mosen6266-netizen/MS_001/actions/runs/38960129236)

- [Step20 断网/崩溃/休眠恢复](docs/STEP_20_RECOVERY.md) · [测试矩阵](docs/STEP_20_TEST_MATRIX.md) · [开发验收](docs/STEP_20_ACCEPTANCE.md) · [CI](https://github.com/mosen6266-netizen/MS_001/actions/runs/38961738009)

- [Step21 运行中心50群看板](docs/STEP_21_RUNTIME_DASHBOARD.md) · [测试矩阵](docs/STEP_21_TEST_MATRIX.md) · [开发验收](docs/STEP_21_ACCEPTANCE.md) · [CI 37/37](https://github.com/mosen6266-netizen/MS_001/actions/runs/38963263363)

- [Step22 Windows异常提醒与紧急停止](docs/STEP_22_NOTIFICATIONS.md) · [测试矩阵](docs/STEP_22_TEST_MATRIX.md) · [开发验收](docs/STEP_22_ACCEPTANCE.md) · [CI 39/39](https://github.com/mosen6266-netizen/MS_001/actions/runs/38966422700)

- [Step23 历史、审计与安全导出](docs/STEP_23_HISTORY.md) · [测试矩阵](docs/STEP_23_TEST_MATRIX.md) · [运维说明](docs/OPERATIONS.md) · [开发验收](docs/STEP_23_ACCEPTANCE.md) · [CI 41/41](https://github.com/mosen6266-netizen/MS_001/actions/runs/38971446337)

- [Step24 性能预算与8小时加速模拟](docs/PERFORMANCE.md) · [测试矩阵](docs/STEP_24_TEST_MATRIX.md) · [开发验收](docs/STEP_24_ACCEPTANCE.md) · [CI 43/43](https://github.com/mosen6266-netizen/MS_001/actions/runs/38973014639)
