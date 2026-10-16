# Step10 认证安全测试矩阵
| 场景 | 预期行为 | 自动测试 | 最终实机 |
|---|---|---|---|
| 等待TDLib初始化参数 | 显示配置指引，不能直接发消息 | mock ✓ | DEFERRED |
| 手机号方式 | 只接受国际格式和授权的操作 | mock ✓ | DEFERRED |
| QR刷新 | 使用TDLib最新临时链接，不开重复client | mock ✓ | DEFERRED |
| 验证码/邮箱验证码 | 提交仅在对应状态允许；不写日志 | mock ✓ | DEFERRED |
| 二步密码 | 掩盖日志与诊断，提交时不宣称Ready | mock ✓ | DEFERRED |
| 真实Telegram安全验证及RateLimit | 429阻止盲目重试，Premium要求不绕过 | 模拟429 ✓ | DEFERRED |
| 取消同时请求进行中 | 立即提交close，迟到结果忽略 | mock ✓ | DEFERRED |
| 新注册与官方条款 | 明确本人同意才可发送registerUser | mock ✓ | DEFERRED |
| 关闭后不重新启用会话 | 不从迟到updateReady恢复已取消授权 | mock ✓ | DEFERRED |
| 真机账号登录/重启恢复 | 不重扫的授权会话持久保留 | NOT_RUN | DEFERRED (Step11/最终) |
| TDLib native DLL SHA/ABI | 只加载签名/校验后的固定版原生包 | NOT_RUN | BLOCKER |
| 300会话真实负载 | CPU/RAM/句柄稳定且平台允许 | NOT_RUN | DEFERRED |
| Win10/11 WebView2输入敏感信息 | 无日志/崩溃转储/截屏泄露 | NOT_RUN | DEFERRED |

当前仅验证源码协议与合成流程。真实凭据从未提交 GitHub，测试也不会向 Telegram 发验证码。
