# 第17步开发阶段验收｜50群独立任务调度器

2026-10-16。仓库：`mosen6266-netizen/MS_001`。**done_dev**，不等于Telegram真实发消息和正式安装验收通过。

## 可核实证据
- [GitHub Actions 38957627884](https://github.com/mosen6266-netizen/MS_001/actions/runs/38957627884) completed/success：**29/29 Job**，包括原有UI、Chrome视觉截图、数据库迁移、账号安全/加密、Telegram模拟、Windows WPF自包含发布。
- 新增跨平台 `tests/Scheduler/Program.cs`：**Windows 37/37、Linux 37/37** 合成测试成功。没有登录Telegram，也没有发送任何实际消息。
- 源码提交 [`8bf4e7028aec53b0449e05f4134f1aefddeb176b`](https://github.com/mosen6266-netizen/MS_001/commit/8bf4e7028aec53b0449e05f4134f1aefddeb176b)。
- 状态机、轮转公平调度、持久化位于 `src/Core/StateMachine/`、`src/Core/Scheduler/`，数据库迁移 `0007_scheduler_simulations.sql`。

## 开发验收结果
- 50个模拟群每个三条消息；状态与计时彼此独立。每次Tick对每个运行群推进一个非零耗时阶段，轮流更换开始群，单个群故障不会拖住余下群。
- 30s纯等待→5s模拟正在输入→第35s仅达到允许提交阶段，提交和成功回执由测试外部显式驱动，无消息发送API；发送后12s从确认时刻启动。
- Pause在第14秒保留16秒，Resume不会将暂停期间的秒数计入剩余；恢复后的消息执行顺序不改变。
- 所有状态与已冻结角色/消息键及时间写入SQLite v7，批量创建/全局停止采用事务；重启把Ready、Running、Paused统一转Review，Tick不能自动唤醒、未知回执不得盲目补发。
- 退出与StopAll全部停止、当前实例锁定不能重启，重新载入仍处于终态。
- 冻结的真实消息内容不在模拟表复制；发送文本属于已发布版。此核心没有暴露任何WebView2真正运行IPC，故仍严格沿用第16步默认阻断的实时身份与群权限安全边界。

## 未进行的验收（DEFERRED）
- Windows 10/11人工UI、真正TDLib实时账号授权/群查询、多账号跨群官方速率限制、正式runs/outbox写入与消息执行、掉线恢复、50群和300账号压力及72小时稳定性、真实回执均未执行。需要第18/19步实现规则协调与消息交付，后续阶段进行集成，最后用户集中实机测试。
- 不涉及 Signal MS002、生产授权后台、真实账号密钥或Cloudflare资源。

**开发进度 17/32（53.125%），还剩15步，第18步待开始。**
