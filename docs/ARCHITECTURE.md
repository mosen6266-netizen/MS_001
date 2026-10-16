# 总架构及可靠性原则
## 2026-10-16 Step02 关键更新：TDLib 原生 ABI
TDLib 最新 JSON C 接口为 \`td_create_client_id()\`、\`td_send(client_id, json)\`、\`td_receive(timeout)\`（全局接收队列）；此前 \`td_json_client_create/send/receive/destroy\` 已标为未来 TDLib 2.0 移除的旧接口。官方文档：https://core.telegram.org/tdlib/docs/td__json__client_8h.html 。
**强制：** 每个 TDLib native host 进程只有一个 \`td_receive\` consumer，所有收到的响应按到达顺序派发，再依据 \`@client_id\`、\`@extra\` 路由给对应账号与请求。返回 \`char*\` 的内容立即复制并解析，不得持有跨越下一次 receive 的裸指针。请求停止必须走授权状态 \`authorizationStateClosed\`，不能强杀仍持久化的TDLib客户端。
## 技术候选
Windows .NET 10 WPF + WebView2 宿主、React/TypeScript 单页 UI、Application Core 纯状态机、TDLib JSON 现代接口适配器、SQLite WAL 本地存储、独立的 Cloudflare Workers + D1 授权。版本只是候选，Step02 Windows 编译和实际 native DLL load 后才冻结。
## 资源隔离
- 300 是合法授权账号会话的注册/持久化容量，不代表300账号始终联网；按需连接与可配置预算，单账号独立 TDLib 状态、独立数据目录及隔离错误处理。
- 50 是同时独立运行的群任务目标，不能据此保证无速率限制；单账号在多个任务出现时由 per-account send gate 串行协调。群任务自身单独状态机和排队/取消控制。
- UI 不做网络操作；使用虚拟列表、增量状态推送、受限缓存、图片缩略图、有界队列、日志轮转和数据库 checkpoint，避免长时运行内存泄漏。
## 发送状态机
每条消息 \`planned -> waiting_before -> typing -> dispatched -> awaiting_confirmation -> confirmed/rejected/outcome_unknown -> waiting_after\`。
先持久化事务 ID 和剧本修订号，发送后关联平台消息 ID；若网络中断且发送结果不明，进入 \`outcome_unknown\` 并请求核查，**绝不能盲目重发同一条**。
定时使用单调时钟，暂停保留剩余等待/输入时长；睡眠恢复复核账号、权限、授权及任务状态。
**不可变计时顺序**：preSendDelayMs=30000 后才触发 typingDurationMs=5000，随后才调用 send。所有阶段串行，总35秒+平台提交/传输。
## 运行维护
- UI 卡住不影响本地核心状态一致性；异常精确分群、异常弹窗置顶、取消/停止需有序释放句柄并停止发送。
- Win10/Win11 x64 干净安装验证、数据分离、升级失败安全回滚、SQLite迁移前备份、DPAPI/等效机制保护会话数据库。
- Cloudflare 另建产品级 Worker/D1 测试与生产环境，绝不对旧 MS002 进行试验性数据库迁移。
- Windows 10 常规安全支持已结束，正式发布应说明风险与兼容范围。
## 平台底线
只用于自有/获授权真实账号与许可群组沟通；遵守 TDLib 与 Telegram 服务限制，受限账号不得靠其他账号轮换规避，不得伪造多人对话。
