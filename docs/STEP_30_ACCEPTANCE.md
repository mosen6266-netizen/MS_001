# 第30步：300账号元数据/50模拟群容量与加速长稳测试开发阶段验收

**日期：2026-10-17；结论：done_dev（仅离线模拟和短时CI基准）。** 本文件明确区分开发阶段与最终用户Windows/TDLib/72小时实体长稳。完整可信方法见 docs/BENCHMARKS.md、docs/CAPACITY_LIMITS.md、docs/STEP_30_TEST_MATRIX.md。

## 可追溯CI证据

- [GitHub Actions Run 39033877609](https://github.com/mosen6266-netizen/MS_001/actions/runs/39033877609) **completed/success，57/57 Jobs SUCCESS**，测试源码 [cc2043fa1460c5a6206036fb964149f17b857dfa](https://github.com/mosen6266-netizen/MS_001/commit/cc2043fa1460c5a6206036fb964149f17b857dfa)。
- Windows和Linux各 **20条容量断言PASS**，各自的 JSON报告合同校验 **1/1 PASS**，最终汇总门禁明确打印 **30 required upstream jobs ALL success**。
- 原有P0 E2E、发布签名模拟单元、SQLite及安全恢复、UI可访问性自动化/Chromium快照、WPF build/publish和未签名Inno Windows离线安装包构建继续通过。
- 官方CI两个源报告Artifact（不是人为造数）：
  - step30-synthetic-capacity-ubuntu-latest（GitHub Actions artifact ID 12101840842）
  - step30-synthetic-capacity-windows-latest（GitHub Actions artifact ID 12101726732）
  - 这些报告可从以上Run的Artifacts区域下载，保留期限约30天。

## 真实短时测量摘要

| 指标 | Linux | Windows |
|---|---:|---:|
| CI OS | Ubuntu 24.04.5 LTS | Microsoft Windows 10.0.26100 |
| .NET / CPUs | .NET 10.0.13 / 4 | .NET 10.0.13 / 4 |
| 真实经过时间 | 4.190秒 | 20.561秒 |
| 本短测取样进程工作集最高值 | 90MiB | 63MiB |
| 最后样本CPU百分比 | 23.85% | 11.82% |
| 最后样本线程数 | 9 | 10 |
| 最后样本句柄数 | 不可得（null） | 267 |
| SQLite主文件长度 | 524,288B | 524,288B |
| 合成故障注入 | 2 | 2 |
| 自动盲重试 | 0 | 0 |
| 终端断言 | 20/20 | 20/20 |

测试实现记录：300个SQLite账号元数据（真实在线账号0、tdjson连接0）+ 50群各6个模拟角色的任务预检和冻结（300角色映射）+ 72小时加速**逻辑时钟**（4320分钟预算Tick）+ 失联结果人工审查、SQLite重启隔离和全局停止持久化（无真实Telegram消息）。预算趋势仅保存最后480个逻辑分钟数据。机型CPU/RAM为短时测试进程数据，**不是72小时墙钟的长期连续采样**，也不是300个原生会话的资源消耗。

原始JSON的 assertionsPassed 字段在写报告之后再做最后一次文件存在性检查之前记录，因此为19；CI终端在最后独立检查通过后为20，两者语义不同，不是少跑一次测试。

## 仍未执行的真实测试（正式发布阻断）

- **LONG-SOAK / NATIVE-RESOURCE / FULL-CAPACITY 仍为DEFERRED**：真实Windows10/11计算机上连续72小时墙钟采集CPU/工作集/堆/句柄/数据库/WAL增长、错误率和断网重连；有合法授权的小规模真实TDLib登录与安全退出；随后才可能由可复核数据判断300账号/50群是否实际可运行。CI仅证明数据结构及本地离线状态机可执行。
- 真正长期TCP/TDLib重连延迟、真实官方Telegram限流、群权限变化、窗口交互和签名升级/断电回滚均未测量。不应将任何合成fixture当作真实成功率或真实稳定性指标。
- 未部署Cloudflare生产授权，也未修改Signal MS002。

**开发进度30/32 = 93.75%；剩余2步。下一步31安全审计、数据恢复和发布阻断。**
