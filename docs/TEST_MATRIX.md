# 强制验收测试矩阵
| ID | 场景 | 预期 |
|---|---|---|
| TIME-01 | 30秒发送前等待、5秒正在输入 | 在 t=30s 才触发 typing，t=35s 才提交 send；不得重叠 |
| TIME-02 | 前等待0/输入0 | 立即派发，不创建残留输入定时器 |
| TIME-03 | 等待/输入期间暂停、睡眠和恢复 | 剩余倒计时准确，无负数/重复发送 |
| TIME-04 | 单条时间配置导出、跨电脑导入 | 3项时长原值一致，剧本 revision 不变 |
| ROLE-01 | 本机300个账号，剧本仅需要3个备注 | 只检查3个必需备注 |
| ROLE-02 | 缺失客服B | 阻止启动并明确显示缺少的备注、群及消息 |
| ROLE-03 | 同一备注对应多个账号 | 根据群成员关系/权限收敛，不唯一时人工选 |
| ROLE-04 | 任务运行中修改账号备注或剧本 | 运行身份与已发布快照不变 |
| GROUP-01 | 50群任务中一群权限异常 | 仅相关任务暂停，其他状态正确 |
| GROUP-02 | 同账号参加多个群 | 发送串行协调，不冲突、不借轮换绕过限流 |
| SEND-01 | 发消息后响应超时但实际已送达 | outcome_unknown 不盲目重发 |
| SEND-02 | 重启后恢复未完成任务 | 不自动重发结果未知消息 |
| UI-01 | 300账号搜索、50任务实时卡片更新 | 无主线程长阻塞、全屏闪烁、重复保存 |
| SYS-01 | 程序关闭/异常重启/睡眠唤醒 | 退出后不再发送；重启任务状态准确 |
| DATA-01 | 数据迁移/备份恢复/升级失败 | 用户数据与会话安全，旧版本可恢复 |
| SECURITY-01 | 剧本导出、日志收集、Git提交 | 零手机号/API密钥/会话泄漏 |
| PERF-01 | 72小时长稳与资源统计 | 在实际硬件上形成可重复的CPU/内存/句柄/磁盘/错误率报告 |
| RELEASE-01 | Win10/Win11 x64 干净安装 | 完整安装、一键启动、合法依赖、更新可回滚 |
未执行的项目必须标记 not_run；不能写 pass。压测结果决定实际硬件门槛，不凭设计推测。


## Step29 P0 自动化覆盖追踪（2026-10-17）

既有 TIME/ROLE/GROUP/SEND/UI/SYS/DATA/SECURITY/PERF/RELEASE 验收项继续保留。本步新增机器可解析覆盖台账 tests/Unit/p0-regression-catalog.json，追踪**32个开发阶段P0条目**至实际测试文件与 GitHub CI Job，不能据此将真实设备 PERF-01/RELEASE-01 标记为通过。

- **Unit**：tests/Unit/p0-coverage.test.mjs 校验ID、路径、源码断言和必测领域；现有Windows/Linux单位矩阵保留。
- **Integration**：tests/Integration/ci-contract.test.mjs 约束模块串接和不使用真实网络；verify-ci-needs.mjs 只有全部29个依赖 Job 都 success 才放行，任何跳过、取消或失败均拒绝。
- **E2E**：tests/E2E/Program.cs 在本地SQLite贯穿跨设备剧本打包、备注映射、不可变发布、群权限预检、调度状态机、确认后等待、备份与重启隔离（30项断言；Windows/Linux）。
- **UI/视觉**：保留 React Vitest、Chromium 1440/700px 快照与 WPF 编译作业；真实Win10/11和WebView2体验只列 DEFERRED。
- **报告**：docs/STEP_29_TEST_MATRIX.md 跟踪本次CI完成度；仅在最新 Run completed/success 且全部Jobs success时签发开发阶段验收。

**边界声明**：模拟预检使用用户合成的已授权账户与群信息，没有真实Telegram身份授权、TDLib原生通信、真实发送、Cloudflare签发或操作Signal MS002。所有相关真实发布验证仍在最终验收阶段。
