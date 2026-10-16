# Step26 — Telegram 授权安全与独立性

## 安全边界

本步仅对 `MS_001` 的独立 Telegram 授权服务进行离线开发和 CI 验证，不会连接、覆写、导出或调用 Signal MS002 的 Worker、D1 或现存卡密。`license/wrangler.toml` 使用独立资源名称和占位ID，并禁止自动开启公开的 workers.dev 路由。**该项目尚未部署在 Cloudflare，也未配置真实密钥。**

## 管理员双重身份验证

- `ADMIN_TOKEN` 是独立于 Signal 的管理员主密钥，必须为随机强凭据且只存 Cloudflare Secret。旧版仅凭主密钥的发行/续费/禁用接口已经禁用。
- `POST /v1/admin/session` 必须同时提供 `Authorization: Bearer <ADMIN_TOKEN>` 和 JSON `{ "totp": "六位动态码" }` 或 `{ "recoveryCode": "一次性恢复码" }`。
- TOTP 使用 SHA-1/HOTP RFC6238 常见30秒/6位时间码，容许相邻一个步长的设备时差。通过 D1 单次条件更新，已使用的时间步不能重复签发另一会话；依赖管理员安全保管 TOTP Secret。
- 认证成功后只返回一次随机256位**管理员会话令牌**与独立256位**CSRF令牌**，D1仅存 SHA-256 摘要和到期/代际。会话有效期15分钟，HTTP使用 `Authorization: Bearer <sessionToken>` + `X-CSRF-Token: <csrf>`；不使用Cookie、localStorage保存秘密。会话过期、缺任一令牌、跨Origin、代际不符一律拒绝。
- `/v1/admin/logout` 注销单会话；`/v1/admin/sessions/revoke` 升级代际令所有旧会话失效；同样要求完整短时会话和CSRF。**主密钥不再是管理API的直接操作权**。
- `/v1/admin/recovery/rotate` 需要已认证会话；随机产生8个一次性恢复码，只在此次HTTPS回应展示，D1只存带独立 `LICENSE_PEPPER` 的HMAC-SHA256摘要。重新生成后旧恢复码作废；使用任何一个都会由单条原子DELETE消费，无法重放。恢复码登录也必须持有管理员主密钥；不要把恢复码和主密钥放在同一存储位置。
- 没有把TOTP密钥、管理员令牌或恢复码写到 GitHub；没有提供通过WebView2/前端IPC获取这些数据的入口。
- 审计记录仅包含固定动作类别、发生时间、成功布尔值，拒绝记录管理员密码、TOTP、手机号、IP原文、session或卡密。支持最多50条的游标式审计读取（需已登录会话）。

## 限流、CSRF 与报错

- 登录以可信 `CF-Connecting-IP` 标识，5分钟8次限制。操作会话每分钟90次，卡密验证每分钟120次。IP标识用 `LICENSE_PEPPER` 派生的 HMAC 摘要后保存，忽略可伪造的 `X-Forwarded-For`。
- 这里的限流是 D1 原子计数的固定窗口，不是 DDoS/WAF 替代品；公网部署必须另行配置 Cloudflare WAF/Rate Limiting，并验证反向代理不会让攻击者自行设置 `CF-Connecting-IP`。本模块未模拟全球多地区恶意Bot持续攻击。
- CSRF验证还检查外部传入的 `Origin`（若存在必须匹配请求源），所有管理变更使用严格 JSON schema、HTTPS，错误响应固定 `DENIED` 而不返回数据库/会话细节。**即使浏览器无Cookie仍要求独立CSRF密钥，作为额外纵深防御**。
- 禁用许可证在线立即拒绝（下一次客户端检查）；若签名离线许可已经发出，无法凭空撤销用户断网机器的本地离线票据，直到最多1小时到期。需秒级禁用必须在真实发送路径执行实时在线许可证核验（未来整合阶段）。
- 无网络时客户端绝不创建新签名许可、延长失效窗口、把服务失败当激活成功。带签名的最多1小时本进程许可使用单调计时器与已观察墙钟最大值抑制倒拨系统时间的延长攻击。系统电源关闭/重启后不会自动产生跨进程许可。
- 本步**没有真实TDLib发送链路**，授权内核并非已经接入任何生产消息发送。

## 签名及密钥轮换

- 许可证使用随机128位 TG 卡密，卡密服务端仅持有HMAC摘要。可在轮换时为 `LICENSE_PEPPER_PREVIOUS` 配置旧Secret，用当前Secret签发新卡、旧Secret验证尚未迁移的卡；必须在迁移/过期之后撤掉旧Secret。不能只改变Secret便假定旧卡仍正常。
- P-256签名私钥存独立Worker Secret `LICENSE_SIGNING_PKCS8`；响应携带 `keyId`。Windows客户端只允许开发者**预先内嵌**且校验过的最多两个SPKI公钥（例如 `tg-v1` 与 `tg-v2`）；绝不能把服务端返回的公钥当作可信。轮换流程是先发布双公钥客户端，再部署新Worker签名key+keyId，待旧1小时票据过期再发布只包含新公钥的客户端。
- 管理主密钥 `ADMIN_TOKEN`、TOTP `ADMIN_TOTP_SECRET` 与恢复码的轮换要和密钥管理员分开操作。轮换TOTP后新步长进入验证，旧码不可再使用；执行 `/v1/admin/sessions/revoke` 让旧会话立即失效。
- Worker签名密钥不可直接通过HTTP管理员接口修改；只能经独立Cloudflare Secret安全配置与人工发布审核。任何公钥不能在用户端远程任意替换。

## 测试和已知限制

- Node24在Windows/Linux用真实内存SQLite模拟D1 prepared statements，覆盖MFA重放、恢复码使用/再次拒绝、CSRF、会话过期、停用与代际注销、并发设备限制、续费幂等及Secret轮换。并行安全配置检查不含Signal Worker/D1实际资源。
- .NET在Windows/Linux用真实P-256密钥和时间序列测试双公钥签名、失效与倒拨时钟。
- 云端实际创建、真实Cloudflare公网MFA登录、CDN/WAF、管理员UI、签名公钥发布/Windows激活集成和 Win10/11 真机仍为 DEFERRED，当前不宣称生产安全验收。
