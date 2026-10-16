# 第25步｜Telegram独立卡密和签名授权（开发环境）

## 范围和与Signal生产业务的隔离
- Telegram产品标识固定为 `MOSEN_TELEGRAM_SCHEDULER`，只有 `MS_001/license/` 的 Worker 与专用 D1 数据库使用该标识。
- Worker（`ms001-telegram-license-staging`）与D1（`ms001-telegram-license-staging`）为**全新资源名称**，不是原 `signal-scheduler-license` Worker，也没有任何MS002 D1 ID、API URL或卡密导入流程。
- 仓库内的 `license/wrangler.toml` 使用未填写的专用数据库ID占位符，`workers_dev=false`；不得将该文件的占位符改成MS002正式数据库，**本步不自动部署Cloudflare资源，也不迁移任何Signal生产数据**。
- `ADMIN_TOKEN`、`LICENSE_PEPPER`、`LICENSE_SIGNING_PKCS8` 必须来自新Worker单独配置的Secret，长度检查不通过时服务拒绝。不得写入GitHub、卡密生成日志、公开前端或安装包。
- 只有专用 Telegram 管理接口可以发行/续费/禁用卡密。Worker 拒绝来自 Signal 产品的运行配置，也不接受未关联专用产品的卡密或签名。

## 服务端安全设计
- `license/migrations/0001_license.sql` 包含许可证、设备绑定、续费幂等记录与管理员操作审计四类表。完整卡密只在首次发行HTTP返回一次；数据库只保存新Secret参与的HMAC-SHA256，**不存卡密明文**。
- `POST /v1/admin/cards`（独立Bearer管理员密钥）：`{durationDays:1..1825,maxDevices:1..5}`；随机128位卡密与UUID生成，首次激活才开始计时，发行时不启动计时。
- `POST /v1/license/activate`：`{card,deviceHash,nonce}`，原子首次生效并按D1单条条件INSERT限制可用设备数；同设备重复激活幂等，不超过 `max_devices`。设备ID为本地随机安装种子派生的SHA-256，不是手机号或真实硬件序列号。
- `POST /v1/license/check`：只校验已绑定设备，不能悄悄激活新卡或新机器。
- `POST /v1/admin/renew`：`{licenseId,operationId,durationDays}`，D1原子batch中唯一操作ID加状态位，重复调用只增加一次有效期；若未激活先叠加可用卡密天数，已激活则从**当前到期或当前时刻中较晚时间**叠加。
- `POST /v1/admin/disable`：`{licenseId,disabled:true/false}`，只改变独立产品卡密；线上查询立即拒绝已禁用卡密。重新启用不隐式创建设备或绕过过期判定。
- 完整响应只采用固定状态码，不返回数据库错误、原始卡密、管理员Token和内部异常堆栈。管理端HTTP不可缓存，格式与长度受限；管理员使用固定长度散列比较。无后台管理网站，后续正式版阶段再完成受控UI/密钥恢复。
- 服务端状态由专用 D1 负责，客户端任何本地字段不可改写服务端到期时间或增加已绑定设备配额。

## 客户端与离线窗口
- `src/Core/Licensing` 包含HTTPS专用POST访问层、ECDSA P-256离线签名验证、产品ID/设备摘要/随机nonce匹配，以及最长**3600秒**授权窗口。签名规范是 claims 按固定字段顺序以UTF-8及换行符拼接，服务端 `crypto.subtle.sign(ECDSA SHA-256)`，Windows .NET 采用 `ECDsa.VerifyData` P-1363 验证。
- 每次在线请求使用新随机128位nonce防旧响应重放；签名未通过、其它产品ID、其它设备、到期、倒退时钟、超1小时窗口、未配置公钥或无卡密都失败关闭。
- 客户端安装标识为本地32字节随机种子派生的SHA-256（不读取用户手机号、用户名、MAC、磁盘序列号）。同一安装恢复种子后身份不变；损坏时不偷偷生成另一设备。**随机种子可被复制，因此这不是抗克隆的硬件证明**，正式版可在Windows账户保护存储并完善换机政策。
- Worker接收的是匿名SHA-256设备标识，并只以此关联名额；不读取任何 Telegram session。
- 离线许可**只在当前已成功验证的进程内**缓存，不会跨进程自动续期；无网络或无有效签名时不能制造新的许可。服务端明确拒绝/禁用会使客户端清空现有内存许可。服务端禁用生效具有最长一小时已签离线窗口的理论延迟（客户端在线检查立即清空）；若要求秒级撤销，正式发送前必须执行在线校验且拒绝纯离线发送。

## 操作与未来配置
1. 创建**全新**Telegram专用D1数据库，并使用独立Worker与独立Secrets。不要使用MS002的数据库ID/密钥。
2. 将新D1 ID填入仅Telegram Worker的 `license/wrangler.toml`；先在隔离测试环境运行 migration + Cloudflare Wrangler的dry-run。这里**没有自动填任何真实ID**。
3. 从独立安全管理环境生成P-256签名密钥。私钥只传到新Worker Secret；把对应SPKI公钥固定在Windows正式安装包构建配置，绝不能由卡密用户自行替换。开发仓库不含任何生产签名密钥。
4. 正式客户端连接同一固定HTTPS Worker地址后再把 `LicenseHttpClient` 和 `SignedLicenseVerifier` 接到程序启动/每次真实发送安全门禁。**第25步没有创建这个真实Cloudflare部署，也不把未通过官方Telegram验证的离线调度当作已正式授权发送**。

## 测试与已知限制
- `license/tests/license.test.mjs` 采用Node24真实内存SQLite引擎模拟D1兼容的 `prepare/bind/first/run/batch` 接口，测试并发15台设备抢2个名额、并发重复续费、禁用、过期、跨Tenant数据库隔离、HMAC摘要、ECC签名。
- `tests/Licensing/Program.cs` Windows/Linux使用真实ECDSA密钥对验证合法/篡改/错误产品/错误设备/nonce/时间回退以及无网状态。两边均不访问Cloudflare生产Worker。
- **DEFERRED**：新Cloudflare生产/测试资源的实际创建和公网部署、正式管理员UI与密码恢复、证书/公钥固定发行签名、WPF激活入口、整机账户绑定与真实网络撤销时序。当前MS_001正式Telegram消息发送本来也尚未启用，不得据此宣称已完成上线授权。最后发布前这些要进行闭环实测。
