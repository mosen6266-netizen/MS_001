# Step17 — 50 群独立任务状态机与公平调度（开发阶段）

## 交付边界
- `src/Core/StateMachine`：Ready / Running / Paused / Review / Completed / Stopped / Failed 任务状态；WaitingBefore → Typing → AwaitingSend → AwaitingReceipt → WaitingAfter 的严格三段时间轨迹。30 秒等待与 5 秒“正在输入”串行而非并行，模拟回执确认后才开始发送后等待。
- `src/Core/Scheduler`：显式 `Tick(virtualTime)` 每轮遍历每个运行中的目标最多推进一个时间阶段，轮次采用旋转首位而非固定群优先。没有后台线程、网络 API、TDLib 发送接口、自动重试或发送动作。
- `SchedulerCheckpointStore`：独立于真实 `runs/outbox` 的 `scheduler_simulation_jobs` v7 表保存模拟任务状态与冻结的角色对应账号 GUID/消息键/三段时间；正文及媒体位于第16步不可变已发布版本，不重复写入队列。
- 新建任务只能接收第16步成功、无问题、完整的 `PreflightResult`；单批1至50个目标，不允许重复群/话题。创建为原子事务，不会产生半批任务。
- Pause 在暂停瞬间扣减已经耗费的等待时间并保存精确剩余毫秒；Resume 以新的时间原点继续，不把暂停期间流逝时间算进去。
- Stop、全局 StopAll 都产生持久停机状态；StopAll 为原子批量更新并锁住当前实例，禁止停止后在本进程重新开始。
- 加载旧检查点时：所有 Ready、Running、Paused 一律转为 Review，必须人工重新预检，绝不自动继续。未知发送结果同样转入 Review，无盲目补发。每群故障通过错误通知隔离，其余群照常模拟推进。

## API/真实发送安全隔离
- 这里实现的是**任务调度的可测试离线内核**，不是开放给用户的真实 Telegram 发消息按钮；`SimulateSubmitted`/`SimulateConfirmed` 仅供程序内合成测试。
- WebView2 不注册新启动发送 IPC；第16步前端仍只有只读预检。不能依赖硬盘上的旧登录标志突破即时 TDLib 授权检查。
- 第18步再做平台合法的账号级串行发送限制、慢速模式等实时出站协调，第19步接官方回执及幂等去重，第20步以后整合任务操作界面。最终由用户在自有设备和授权群里实测。

## 状态与调度原则
| 事件 | 前状态 | 后状态 | 是否自动发送 |
|---|---|---|---|
| Start（显式）| Ready | Running / WaitingBefore | 否 |
| Tick 至前等待结束 | WaitingBefore | Typing | 否 |
| Tick 至输入时间结束 | Typing | AwaitingSend | 否 |
| 外部模拟提交 | AwaitingSend | AwaitingReceipt | 否 |
| 外部模拟确认回执 | AwaitingReceipt | WaitingAfter | 否 |
| 后等待结束 | WaitingAfter | 下一消息/Completed | 否 |
| Pause / Resume | Running / Paused | Paused / Running | 否 |
| 任意运行态退出或重启 | Ready / Running / Paused | Review | 否 |
| StopAll | 非终态 | Stopped | 否 |
| 单群故障 | 某群 Running | 该群 Review/Failed | 否 |

未合并的剩余事项：接入真实 TDLib 实时授权刷新、群/话题慢速模式与出站发送管线，任务从模拟表迁移至正式 `runs`，真实账号权限断开停机、实际 Windows 10/11 UI/多群持续压测。它们属于后续开发与最终实机验收，不能算本步已通过。
