# Step 08 — 本地安全、脱敏诊断与故障基础

目标：为将来的自有授权 Telegram 会话预留安全基础；**Step08 不登录账号、不收集验证信息、不操作 TDLib、不发送消息、不部署 Cloudflare**。源码可公开，绝不提交凭据、账户ID和会话文件。

## 保护边界

### Windows DPAPI（本地机密包装原语）
- `src/Core/Security/DpapiProtector.cs` 直接调用 Windows `CryptProtectData / CryptUnprotectData`，使用 **CurrentUser**、禁用交互提示及产品作用域 entropy（不是加密密钥）；没有使用 `CRYPTPROTECT_LOCAL_MACHINE`，不能跨用户登录解密。
- 返回值是带版本前缀 `MS001-DPAPI-USER-v1:` 的 Base64 外壳；包含 Windows DPAPI 密文，不放入日志、网络、剧本、用户可查看导出。
- 明文暂存字节在函数内使用完毕清零；`Unprotect` 返回的数据由未来调用者负责及时清零。当前软件**尚未实现将机密存入磁盘的安全会话仓库**；未来 TDLib 的数据库独立于 `app.db`，账号会话加密和生命周期的安全复核属于接入阶段。
- CI Windows runner 进行合成机密的 DPAPI roundtrip、篡改拒绝；Linux CI 应显示不支持并安全拒绝，不伪装加密成功。**DPAPI 不等于完整磁盘加密**；Windows 登录后的同一用户进程仍可能读取其可访问数据，需后续文件访问控制与威胁模型防御。

### WebView2 IPC
- 已有 WebView2 主窗口校验事件来源为内置的 `https://ms001.local/index.html`，禁止外站跳转及打开新窗口。
- 新增 `IpcRequestGuard.TryValidate`：最大全文 8192 字符、JSON 深度限制、root及嵌套对象不允许重复字段、白名单字段、固定协议版本、短ASCII requestId、4种既有本地命令 + 只读 `diagnostics.get`。
- `settings.save` 必须通过严格字段和值范围、非负 revision 检查；`diagnostics.get` 不可携带额外负载；所有未知命令、嵌套凭据字段、非法 JSON 原地拒绝，无远程请求/发送通道。
- 验证先于 JSON DTO 反序列化与动作派发，不允许跳过输入校验。

### 结构化本地诊断
- `src/Core/Telemetry/SafeDiagnostics.cs` 只接受 `DiagnosticEvent`、`FailureCategory` 枚举，**不允许传日志消息、Exception.ToString()、异常堆栈、号码、名字、聊天/地址或原始IPC JSON**。
- 写入 `%LOCALAPPDATA%\MOSEN\TelegramScheduler\diagnostics\events.jsonl`，最多保留当前与前一份，默认每份上限256KiB；用 WriteThrough 尽量减少突然断电时丢失近期事件（不保证硬件缓存完全安全）。
- 公开给设置页的诊断快照仅含进程正常/降级状态、启动后运行秒数、内存MiB、线程数和事件/警告/失败计数。错误详情分类显示固定代号，不提供原始异常。
- UI 初始化/异常/退出自动生成固定代号日志，独立于Signal MS002目录。
- `tests/Security` 进行越权IPC字段、重复键/恶意长度、1200个事件密集写入/轮转/文件大小上限、异常内容不泄漏、Windows DPAPI篡改测试。使用纯合成随机/固定内容。

## 明确未完成与最终集中验收
- 真实账号凭据生命周期、TDLib数据库文件和相关内容加密**尚未接入，不能宣称已安全存储 Telegram 会话**。
- 安全基线只防常见误日志/非法IPC；Windows上的恶意软件/管理员权限、执行时被入侵的 WebView2 内容、恶意修改安装目录/依赖及调试器攻击，仍需签名发布/访问权限/供应链治理。
- 真实 Win10/11 环境的磁盘权限与只读盘、磁盘满、杀软干扰、数据目录 ACL、Windows 转储/崩溃隐私和高负载实测 **DEFERRED** 至用户最终安装验收。
- 公共 GitHub 仓库内不得包含真实会话、手机号或令牌。提交、CI日志和测试数据均使用合成样本。
