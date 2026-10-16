# GPT 跨对话接力协议
仓库唯一真相来源：https://github.com/mosen6266-netizen/MS_001 (main)。
新对话首先读取：`state/project_state.json`、`state/steps.json`、`docs/PROGRESS.md`、`docs/DECISIONS.md`、`docs/MASTER_PLAN_32_STEPS.md` 当前步骤、相关规范与 GitHub 最新提交。不可只依赖旧聊天上下文。
## 执行顺序
1. 确认当前是 MS_001 而非 MS002 / MS007 / MS_Telegram_bot。
2. 读取最早未验收步骤并核查上一步交付、提交 SHA、测试原始证据与遗留问题。
3. 当前状态若为 `in_progress`，继续检查未完成项，不重新发明架构；若 `blocked`，解决阻断；若 `not_started`，用户同意后开发；若 `verified`，确认后推进下一步。
4. 单次只做当前步骤；先检查现有实现，再进行最小相关修改；先本地/CI 验证再提交代码；不能实测的项目写 `not_run`。
5. 每次 GitHub 提交后更新 `state/project_state.json`、`state/steps.json`、`docs/PROGRESS.md` 并记录真实提交 SHA 和本步测试证据。自引用 commit SHA 无法嵌入自身提交时，应记录前一提交并在下一提交更新或说明。
6. 给用户报告步骤、已交付内容、真实提交链接、通过/未通过及下一步。中断后不能后台自动工作，靠仓库恢复。
## 禁止
不得触碰 Signal MS002 生产环境；不得把 API hash、Session、验证码、Token、账号手机号、密码、卡密放进仓库/日志；不得冒称正式版测试通过；不得绕过 Telegram 平台规则。
