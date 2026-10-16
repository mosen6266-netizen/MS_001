# 第 10 步｜Telegram 登录与身份验证状态机

日期：2026-10-16；唯一项目仓库 `mosen6266-netizen/MS_001`。此步骤只做**开发+模拟/云端编译验收**。TDLib 1.8.67 原生 DLL 尚未构建及接入程序；不应将 mock 测试称为真实账号登录完成。

## 官方状态到中文界面
映射源文件 `src/Features/AccountAuth/AuthState.cs`。每一个 TDLib `updateAuthorizationState` 事件均须经过 `@client_id` 过滤。13 个正式授权状态 + 未来未知状态统一覆盖：

| 官方 `authorization_state.@type` | 中文阶段 | 允许操作 |
|---|---|---|
| authorizationStateWaitTdlibParameters | 配置连接 | 配置受保护的 API 参数、独立会话数据库 |
| authorizationStateWaitPhoneNumber | 选择登录方式 | 自有手机号、手机扫码 |
| authorizationStateWaitOtherDeviceConfirmation | 等待手机确认 | 查看由 TDLib 更新的二维码链接、刷新状态、取消 |
| authorizationStateWaitCode | 输入登录验证码 | 提交官方验证码 |
| authorizationStateWaitEmailAddress | 验证电子邮箱 | 提交邮箱地址 |
| authorizationStateWaitEmailCode | 输入邮箱验证码 | 提交邮箱收到的验证码 |
| authorizationStateWaitPassword | 两步验证密码 | 提交本人两步密码 |
| authorizationStateWaitRegistration | 注册新账号 | 用户明确接受官方服务条款、填名字 |
| authorizationStateWaitPremiumPurchase | 需要官方处理 | 不绕过 Telegram 的Premium要求 |
| authorizationStateReady | 授权成功 | 不再要求验证码，后续需核实群权限 |
| authorizationStateLoggingOut | 正在退出 | 等待完成 |
| authorizationStateClosing | 正在关闭 | 等待完成 |
| authorizationStateClosed | 已关闭 | 旧客户端无法继续使用 |
| 其他未知官方状态 | 无法继续验证 | 停止未授权操作，提示更新客户端 |

中文说明在 `AuthStateReader`（状态机）中维护；前端的 `AuthStatusPreview.tsx` 是独立纯界面状态演示，不等于真实 TDLib UI 已接入。下一步账号管理注册表和真实 IPC 接入时应统一前后端枚举，避免维护两套正式状态真相。

## 安全的请求与回执路径
- `src/Infrastructure/Telegram/TdlibAuthRequest.cs`：只能构造下列官方请求，不开放任意 JSON：
  `setTdlibParameters`、`requestQrCodeAuthentication`、`getAuthorizationState`、`setAuthenticationPhoneNumber`、`checkAuthenticationCode`、`setAuthenticationEmailAddress`、`checkAuthenticationEmailCode`（使用 `emailAddressAuthenticationCode` 嵌套对象）、`checkAuthenticationPassword` 和 `registerUser`。取消使用 `close`，不是 `logOut`。
- `TdlibAuthGateway.cs` 将受限认证命令传给第09步的 TDLib 调度器；后者通过 `@extra + @client_id` 进行严格请求关联。成功 `ok` 只是命令已被 Telegram 接收，**只有官方 `authorizationStateReady` 才算登录成功**。
- `AuthSession.cs` 每个逻辑 TDLib clientID 最多一个受控授权尝试：二维码重复点击合并、QR链接只能依靠官方更新替换，不创建新的账户；请求进行中禁止第二次提交，但用户取消始终优先，未完成回执不能让已取消的客户端重新成为已登录。
- `RefreshQr` 只调用 `getAuthorizationState` 查当前官方状态，不重新请求第二份二维码或自动刷新登录会话。TDLib 官方 QR token 有时效且由官方更新；没有依据时不能向界面倒数“精确秒数”。
- `AuthSession.Accept` 拒绝其他 clientId 的状态更新；旧的取消流程只接受最终 `Closed`，拒绝迟到的 `Ready`。未来更换会话ID必须新建对象，不偷复用。
- Telegram返回错误使用受控分类：429 提示遵守平台等待、一般错误提示校验输入/官方界面；未知/超时结果不自动再次提交，避免反复请求。
- 对需要服务条款的新账号注册，必须本人明确同意，禁止默认勾选；Premium 限制状态不得自动绕过。

## 机密边界
- 手机号、验证码、邮箱、密码、密文密钥与 QR 深链只在当前本机用户主动发起的受控流程内使用，不进入 SQLite 元数据、Cloudflare、GitHub 或诊断日志；`AuthCommand.ToString()`、`AuthView.ToString()` 显式隐藏私密字段。
- `TdlibAuthConfiguration` 的 API hash 与 TDLib databaseKey 应由未来的 Windows 当前用户安全配置区提供，**不得从公开源码或远程日志读取**；目前没有真实配置仓库或机密数据落盘。测试只用假的 API id/hash、临时目录及全0合成key。
- C# string 无法保证像字节数组一样可清零；最终接入阶段必须审查其堆内生命周期、进程dump、WebView2传递与账号仓库加密。
- `setTdlibParameters` 明确使用完整受保护账号目录、不连接聊天内容发送，本阶段配置关闭本地消息数据库（`use_message_database=false`）。后续需要消息缓存时必须另行威胁建模并修改发布门槛。

## 自动化测试
`tests/AccountAuth/` 在 Windows/Linux .NET 10 使用本地 fake gateway/native transport 检查：
- 13种官方状态+未知状态中文呈现；
- 不同账号事件隔离、重复授权更新、QR token 内存链接轮换与日志脱敏；
- 无效手机号、错误状态验证码、注册条款拒绝、官方 Premium 阻断；
- 成功 `ok` 不虚报已登录，只有 updateReady 有效；
- 500 次重复提交只调用一次 gateway，取消可立即中断并拒绝迟到的 Ready；
- 429与超时用户提示不包含凭据；
- 9种 TDLib JSON 授权指令逐一检查类型、`@extra`、嵌套 email code 字段；
- `close` 不调用 `logOut`，关闭调度器之后资源释放。
- 其他历史 Step01-09 UI、安全、数据库、窗口、TDLib adapter 继续回归。

## 真机延期与后续步骤
官方 TDLib DLL 源码钉版本但 **win-x64 tdjson.dll 尚未构建/加载验证**；真实合法自有账号短信/扫码/邮箱/两步认证、重启后恢复、QR超时、多个账号互斥、敏感输入不被系统剪贴板/崩溃dump泄露等，按用户确认的规则留待最终 Win10/11 验收。
第11步接入账号注册表、独立会话目录和加密持久化，正式原生连接的测试门槛仍列入后续分发与最终验收，不得以mock冒充成功。

参考官方文档：
- https://core.telegram.org/tdlib/getting-started
- https://core.telegram.org/tdlib/docs/classtd_1_1td__api_1_1_authorization_state.html
- https://core.telegram.org/tdlib/docs/classtd_1_1td__api_1_1set_tdlib_parameters.html
- https://core.telegram.org/tdlib/docs/classtd_1_1td__api_1_1request_qr_code_authentication.html
