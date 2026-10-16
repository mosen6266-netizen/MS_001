# 第20步｜断网、崩溃、休眠与重启安全恢复

## 实现范围（开发阶段）
- `src/Core/Recovery/RecoveryCoordinator.cs`：集中处理离线启动恢复、网络断开、电脑休眠、进程正常关闭/异常退出之后的恢复报告。事件来自未来可信桌面生命周期入口，本步仍没有实际网路发送或 TDLib 绑定。
- `RecoveryQuarantineScanner.cs`：扫描第17步 `scheduler_simulation_jobs` 和第19步 `delivery_attempts` 的关键元数据，检查 GUID、群/话题、修订、状态、JSON 快照、冻结步骤和时间格式。损坏只写入 `recovery_quarantine`，**不删除原始记录也不改写原始数据**，由恢复枚举器过滤。旧记录仍可人工调查或备份。
- SQLite v9 新增 `recovery_quarantine(source,record_id,reason,discovered_utc)`，复合主键保证重复恢复不会重复插入；仓库旧迁移依序保留。损坏行与健康行可共存且损坏行永不可充当待发送任务。
- 第17步 `FairScheduler.HoldAllForReview(now)`：网络/休眠/关闭时在单事务中将所有非终态模拟运行写入 `Review`，并永久锁住当前调度器实例。不采用仅存于内存的“暂停标志”冒险续发。
- 第19步 `DeliveryJournal.LoadAndQuarantineInterrupted(now)`：`Planned→Review`，`Submitting/Pending→Unknown`，已知 `Confirmed`/明确 `Failed` 不被逆转。结果不明时只可接受后续**可信、完整关联的官方回执**，不可自动重试发送。
- 第18步 `OutboundCoordinator.Shutdown()` 首先执行，确保任何磁盘恢复失败仍关闭当前出站许可。重连和电脑唤醒不会自动开闸。纯本地备份/记录查阅不作为发送凭据。
- `RecoveryReport`：报告隔离记录和未确认任务数量，公开 `CanAutomaticallySend=false` 和 `RequiresManualReview=true`；为第21步运行中心准备只读数据。

## 安全恢复顺序
1. 未来桌面启动必须先进入禁止发送阶段；打开本机 SQLite，应用安全 v9 迁移，扫描损坏元数据并插入隔离索引。
2. 加载健康群任务并将 Ready/Running/Paused 转入 Review；处理健康投递事务的未知状态；隔离行既不可继续出站，也不覆盖历史审计。
3. 只读展示待人工复核的账号/群/事务/异常；用户明确处理后，仍需按第16与18步重新进行**实时授权和平台限制检查**。任何浏览器 UI 缓存数据不构成授权。
4. 网络断开、系统挂起、窗口退出优先关闭出站许可，再原子暂停群任务并将悬而未决发送归类为 Unknown/Review。即使恢复了互联网/唤醒，当前进程也不自发恢复。

## 模拟测试与仍未完成事项
- `tests/Recovery/Program.cs` 离线注入：网络断开、崩溃重建新恢复控制器、24小时休眠、损坏 JSON 和损坏回执 GUID、多次恢复、晚到可信模拟回执。
- 本步不能模拟真实 Windows 硬断电导致的磁盘写入丢失，不能替代 SQLite 文件损坏、WDAC/杀毒、实物机器睡眠/重启或 Telegram 网络状态验证。
- 第21步将在独立运行中心显示这些只读状态；真实 Windows 10/11、TDLib 服务端回执、群授权与72小时压力均按用户要求留到正式版安装后集中实测。
- 由于目前第16步仍默认真实授权为 false，本步依旧**没有真实 Telegram 发送、后台群发、UI 自动恢复开关**。绝不改动 MS002、Cloudflare 卡密生产资源。
