# Step09 TDLib 通信适配层：设计与验收范围

唯一仓库 `mosen6266-netizen/MS_001`。范围仅为多客户端 JSON 请求/响应接收调度基础，不实现实际账号认证、消息发送、可运行真实客户端或后台连接。真实 Windows + tdjson.dll 的 integration gate 统一延期，不能视为已经通过。

## 已实现模块
- `src/Infrastructure/Telegram/TdlibTransport.cs` 仅向 native TDLib 提供 `td_create_client_id`、`td_send`、`td_receive` 的官方 C ABI P/Invoke。实例只能在 Windows x64 使用；源代码在 GitHub 公共仓库可审计；没有登录凭证。
- `TdlibDispatcher.cs` 为进程单一接收器，拥有所有已注册的 TDLib 客户端 ID，避免把多账号每一个建立独立 `td_receive` 线程；接收**必须**严格单一且顺序处理。
- `SendControlAsync` 阶段仅允许读取TDLib版本（`getOption(version)`）与读取授权状态（`getAuthorizationState`），不允许向外输入任意 JSON 或触发真实消息发送。
- `@extra` 与 `@client_id` 双重比对，错误账号但相同关联ID的回执不能唤醒其他账号任务；超时/取消后删除待决项，迟到回执不当作新更新或自动重复请求。
- 有界 256 条更新 Channel，满后停止向上层推进而不是无界缓存或并发乱序；关闭取消等待并停止阻塞，进程仍要等真实 `authorizationStateClosed` 确认 native 客户端退出。
- `DisposeAsync` 对注册 client ID 发 `close`；限时等待关闭确认，超时只能报告“不确定是否真正关闭”，禁止声称 native 资源已完全释放；进程级长稳仍需验证。
- `tests/TelegramAdapter`：独立于 Telegram 网络且不需要 tdjson.dll 的伪造transport，验证逆序回复/交叉客户端隔离/100条更新排序/超时/取消/关闭/背压停止/非法命令拒绝。
- `native/tdlib/source-lock.json` 源码官方版本及完整SHA已固定；**没有原生二进制，也没有二进制SHA256，不能把 Windows build 编译当成 native load 验收**。

## 测试纪律
CI `tdlib-adapter` 使用Windows与Linux矩阵执行 mock 测试，并继续执行现有UI/数据/安全/桌面编译回归。未经实际 TDLib 运行不标注“原生通过”。无法在没有用户API凭证的情况下进行授权登录；按用户说明集中最后本机实测。

## 重要限制
- 即使注册了300个clientId，这不是300个真实授权账号，也不能推断并发/CPU/内存/联网容量。
- 关闭前未知请求响应不得用于自动补发，平台FLOOD_WAIT/Slow Mode不允许通过换账号绕过。此阶段没有任何发送消息API。
- 在第10步明确加入TDLib认证参数、隔离会话数据和凭据保护前，此 adapter 不在 GUI 中启动并创建真实客户端。
