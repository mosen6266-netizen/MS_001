# Step29 — 全功能P0自动化回归开发验收（2026-10-17）

**结论：done_dev（仅开发阶段）**。唯一工作仓库为 MS_001 main，不触碰Signal MS002生产环境。

## 可信验证

- [GitHub Actions 39032907487](https://github.com/mosen6266-netizen/MS_001/actions/runs/39032907487)：completed / success，**55/55 Jobs SUCCESS**。受测源码提交 [0567b58ab0d1d54f3ed03f9f18630a640218c584](https://github.com/mosen6266-netizen/MS_001/commit/0567b58ab0d1d54f3ed03f9f18630a640218c584)。
- Windows/Linux两平台离线E2E：各 **30/30真实SQLite/跨模块断言PASS**。P0+集成Node测试：每平台 **5/5 PASS**。
- 回归汇总门禁：29个上游CI Job的结果全部success才通过，实际日志确认「Step29 aggregate P0 gate PASS: 29 required upstream jobs ALL success」。
- 全量旧测试、React UI、Chromium 1440/700px快照、Windows WPF x64编译及自包含发布、Inno Setup离线开发安装器重新构建均SUCCESS。
- 新离线开发包：MOSEN-Telegram-Scheduler-Setup-0.27.0-dev.exe，**269,074,484 字节**，SHA256 **EE2719B810B6EA2755F342CB76DE99670E7FC086F765803C6036DA951841C918**。仍为**未签名开发版本**，GitHub Artifact名ms001-step27-offline-installer-unsigned，约于2026-10-24过期。

## 分层回归交付

1. Unit：tests/Unit/p0-regression-catalog.json 32条P0条目映射至现存源码测试与实际CI工作流；p0-coverage.test.mjs验证ID、路径、测试断言和覆盖风险域。
2. Integration：tests/Integration/ci-contract.test.mjs 保护跨模块E2E的真实本地数据库/权限/备份路径；verify-ci-needs.mjs 严格要求29个上游作业均成功，跳过、取消、失败、缺失一律不放行。
3. E2E：tests/E2E/Program.cs 实际贯穿设备A剧本导出→设备B导入/备注角色匹配→SQLite草稿/不可变发布→群权限预检/重复备注人工选择→两群本地任务→30秒发送前等待+5秒输入→外部模拟提交/回执→12秒发送后等待→SQLite备份/独立恢复候选→断线重启审查隔离→全局停止持久化。
4. 负面场景：损坏压缩包、丢失/重复备注、不新鲜群权限、任务冲突、未提交的回执、未知结果不自动重发、重启后的运行禁止均有E2E覆盖。
5. UI：保留React Vitest导航、设置、编辑、动作锁和现有视觉快照；WebView2实际交互/DPI仍在最终验收。

首轮Linux曾因既有测试函数名称Assert/Verify及合并Node测试命令未被P0注册检查器正确识别而出现检查器误报；已修复并重新执行完整CI全绿。业务核心30项断言此前即PASS。

## 不可冒称完成的事项

**DEFERRED**：真实Win10/Win11安装/断电/回滚、TDLib原生库、合法账号真实登录及群内发送、50群真实容量、72小时物理长稳、真正Cloudflare Worker/D1生产对接、许可证公钥、正式签名EXE/Release和真实设备界面可访问性验收。这些均不是CI模拟PASS。

**开发进度29/32（90.625%），剩余3步，下一步30容量与长稳测试。**
