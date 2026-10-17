# Step29 — P0全功能自动化回归测试矩阵

本步重点：独立单测覆盖、跨模块真实离线SQLite集成、完整模拟端到端流程、界面/可访问性既有测试、安全发布门禁。所有验证属于开发阶段；用户Windows10/11实机与真实Telegram账号/TDLib联调不计通过。

- [ ] 32个机器追踪的P0条目均对应实际可运行测试与CI作业，Windows/Linux P0目录合同测试全部通过。
- [x] 离线端到端：剧本从设备A导出 → B导入并绑定本地备注 → SQLite草稿/不可变版本发布 → 群权限预检 → 双群独立模拟 → 回执与发送后等待 → 一致性备份 → 失败重启隔离 → 全局停止。Windows/Linux均通过30个断言。
- [x] 负面安全场景：坏ZIP、缺失/重复备注、过期权限、活动群冲突、未提交不能确认、结果不明不能盲重发、重启不能自动恢复已停任务。
- [x] 现有 React UI Vitest 与可访问性/键盘互动回归、Chromium两种宽度快照、Windows WPF自包含编译与未签名离线Inno安装包编译成功。
- [x] 全部Windows/Linux原有模块单元矩阵继续通过，包括数据库迁移、TDLib假适配器、群/账号、隐私隔离、限流、回执去重、许可、安装与签名更新。
- [x] aggregate regression-gate验证29个上游CI作业均为success；缺失、跳过、失败、取消均触发拒绝。
- [x] 记录最新完整CI Run、实测代码Commit SHA，提交正式第29步开发验收报告。
- [ ] DEFERRED：Win10/11真实安装、人机可访问性、官方TDLib原生合法账号和群内发送、72小时实机长稳、独立Cloudflare授权生产及正式签名安装器，仍待最终验收。

数据源：tests/Unit/p0-regression-catalog.json、tests/E2E/Program.cs、tests/Integration/ci-contract.test.mjs、docs/TEST_MATRIX.md。

## 验收结果（2026-10-17）

- [GitHub Actions Run 39032907487](https://github.com/mosen6266-netizen/MS_001/actions/runs/39032907487)：completed/success，**55/55 Job成功**；测试基准commit 0567b58ab0d1d54f3ed03f9f18630a640218c584。
- Windows、Linux各30个跨模块E2E断言PASS及5个Node P0/集成测试PASS；29个上游CI全success并通过最终gate。
- 真实Windows/TDLib/账号/72小时/正式签名发布仍DEFERRED，不改变最后一条勾选状态。
