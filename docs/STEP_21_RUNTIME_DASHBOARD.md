# 第21步｜运行中心与50群只读状态看板

## 开发交付

- `src/Features/RuntimeDashboard/RuntimeDashboardBuilder.cs`：由第17步持久化调度快照及第19步回执状态生成聚合统计、完成条数与百分比、下一条消息索引、待复核诊断原因、倒计时的时间锚点。每群独立映射，未确认的消息不会冒充完成。
- `RuntimeDashboardReader.cs`：本机 SQLite ReadOnly 事务读取已经存在的模拟记录、回执和隔离统计。既不调用真实 TDLib 发送，也不创建模拟任务。遇到结构损坏快照只跳过，不删除原始记录；真正隔离来自第20步安全启动流程。
- Windows 原生桌面严格 allowlist 增加 `runtime.snapshot` **无参数只读 IPC**，拒绝 `runtime.start`、`runtime.resume`、`telegram.send` 等可出站命令。无账号手机号、消息正文或敏感令牌。
- `apps/ui/src/components/RuntimeDashboardPanel.tsx` 替换“运行中心”空白页：暗色统一风格、5个统计项、异常告警、群ID/任务ID搜索、状态筛选、10条固定分页、单项进度条、发送前/输入/回执后等待阶段与倒计时、故障原因。
- 只在运行中心挂载**一个1秒时钟**，每5次秒刻度读取快照；隐藏即卸载清理定时器和监听。单页仅渲染10条，50个任务不会产生50个独立计时器。
- 浏览器预览无本地数据库时不注入假数据，显示明确空状态；真正桌面未连接正式 TDLib 实时授权，面板是**离线模拟调度元数据**，不是正在真实发送的聊天，也不会自动发起 Telegram 网络调用。

## 交互与性能验证

- `apps/ui/src/runtime-dashboard/model.test.tsx`：浏览器只读命令、SSR空状态、50群5页、搜索筛选、失败优先、倒计时防负数/暂停冻结、2000次分页计算。
- `tests/UI/Dashboard/Program.cs`：Windows/Linux .NET10模拟50群统计、投递确认、失败与隔离计数、只读SQLite与90轮刷新无状态变更。
- 旧调度/回执/恢复/账号权限/IPC与WPF/UI全量回归。Win10/11实际UI与真实群TDLib交互延期最后实机验收。

**第21步不接入真实任务开关、账号授权或Telegram消息发送。第22步处理系统提醒与紧急停止。**
