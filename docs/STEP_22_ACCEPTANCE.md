# 第22步开发阶段验收｜Windows 提醒、托盘、去重与紧急停止

日期：2026-10-16。唯一仓库 `mosen6266-netizen/MS_001`。结论 **done_dev**，下一步第23步。

## 经验证的 CI 结果

- [GitHub Actions Run 38966422700](https://github.com/mosen6266-netizen/MS_001/actions/runs/38966422700)：**completed / success，39/39 项作业成功**（查询了全部39项，而非只看默认前30项）。
- `tests/Notifications/Program.cs`：Windows **38/38 PASS**，Linux **38/38 PASS**；包括50模拟群原子停止、后台令牌即时关闭、重复停止幂等、持久闩锁重启阻断、权限过期不能发、磁盘写入失败仍阻断、200个并发同类异常去重、1000条记录容量限制、历史存档与文件损坏安全降级。
- Windows WPF 桌面 `dotnet build` 与 `win-x64 --self-contained` 发布通过；完整 UI、SQLite v9、安全IPC、群目录、剧本、50群模拟调度、恢复和回执回归绿色。
- 初轮测试出现C#多变量编译错误；之后WPF与WinForms托盘类型（Button、Brush、Orientation）冲突、旧文件对话框 SaveFileDialog/OpenFileDialog 名称冲突；全部修正，并用**最终完整39/39 CI**重新验收。
- 源码主提交 [`28fefa70`](https://github.com/mosen6266-netizen/MS_001/commit/28fefa70516e80c4dd75d3d0de8d55feb45ba3c3)；最终桌面兼容性修订 [`76270f9b`](https://github.com/mosen6266-netizen/MS_001/commit/76270f9b9be8a5619f298a896ff52a4341933b15)。

## 功能验收

- 通知中心：错误类别安全白名单、按安全记录ID去重、计数、确认与静默期、只保存脱敏用户本地500条历史；历史只显示类别和次数，无消息原文与手机号。
- Windows 窗口：右上顶部紧急停止和异常历史入口，托盘打开/历史/停止/退出，单个始终置顶告警窗可在主窗口最小化时显示；托盘多错误气泡批量节流，不叠几十个窗口。
- 紧急停止：确认左绿、取消右红；立刻 `OutboundCoordinator.Shutdown`，写本地永久闩锁，再停止模拟群任务，失败不会放行新的模拟许可。关闭主窗口会销毁托盘和计时器，**不会驻留后台**。
- 所有工作仅对离线模拟的本地任务有效；不增加真实 Telegram 发送 IPC，仍不允许未经验证的网络任务。

## 最终实机验收（DEFERRED）

- 本步没有实际打开 Win10/11 的真实托盘及系统锁屏/最小化窗口；WPF构建成功不等于真实用户桌面交互已验收。
- 正式 Telegram TDLib 发送适配器尚未接入，因此尚不能证明全球紧急停止能阻断实际正在发送的Telegram传输；未来对接必须在每条真实消息发送前检查相同安全闩锁，并与活跃调度器共享同一全局停止控制。
- 合法账号/群真实发送、50群高频任务、异常网络、用户 Windows 桌面72小时稳定性均留给正式版集中测试。
- Signal MS002和Cloudflare生产卡密后台均未修改。

**开发进度 22/32（68.75%），剩余10步，第23步待开始。**
