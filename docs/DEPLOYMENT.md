# Step26 — Telegram-only Cloudflare 部署与密钥轮换手册

**本文件仅为未来操作指引。到第26步为止没有执行Cloudflare公网部署，没有创建实际D1和签名生产Secret。Signal MS002必须继续保持完全独立。**

## 1. 独立资源创建前的检查

使用拥有最小权限的Telegram-only Cloudflare账号/项目，核对现有Signal MS002 Worker `signal-scheduler-license` 不在以下新服务名称里。为Telegram单独准备以下资源：

- Worker：`ms001-telegram-license-staging`（正式环境请另取不冲突名称）
- D1：`ms001-telegram-license-staging`（正式库需不同于staging、绝不是MS002数据库）
- 对应独立Worker Secrets：`ADMIN_TOKEN`、`ADMIN_TOTP_SECRET`、`LICENSE_PEPPER`、`LICENSE_SIGNING_PKCS8`。密钥需要随机生成、分开存储和安全备份。
- 视阶段设置新签名密钥ID `LICENSE_SIGNING_KID`（例如 `tg-v1`）。轮换时仅新Worker可添加 `LICENSE_PEPPER_PREVIOUS`，绝不导入Signal任何生产Secret。
- 只在已经明确核对新D1真实ID后替换 `license/wrangler.toml` 中的 `REPLACE_WITH_NEW_TELEGRAM_D1_DATABASE_ID`。禁止使用旧的Signal D1 ID。

## 2. 手动数据库迁移与发布检查

- 进入 `license/`，确保 Wrangler 绑定只指向Telegram数据库，先在本地运行 `wrangler d1 migrations apply <new-Telegram-D1> --local`。检查表结构与迁移路径：`0001_license.sql` 和 `0002_admin_security.sql`。
- 新Worker发布前先验证 `node --test tests/*.test.mjs`（Node24）及 `npm run check` 和CI绿灯。只在新隔离Cloudflare账户中迁移/发布，**不能把这些迁移命令作用到MS002的生产库**。
- 配置全部独立Secret后，先用不带授权的错误请求确认服务拒绝，随后使用**新密钥**和TOTP在隔离网络验证 `/v1/admin/session`。通过后为测试许可证执行发行/激活/检查/禁用并观察审计元数据。
- 密钥恢复码仅在管理员已通过MFA后的 `/v1/admin/recovery/rotate` 生成8个，用户需要立即离线保存；不存在“忘记密码后自动发回主密钥”接口。恢复管理员设备时必须同时拥有管理员主密钥与未使用恢复码。
- 客户端部署前必须固定独立HTTPS Worker可信地址与一组发行方公钥；签名公钥缺失将拒绝激活。Win10/11正式EXE、卡密激活UI和消息发送门禁不在第26步自动执行。

## 3. 许可证签名与Secret轮换

1. 使用安全密钥管理环境生成新的P-256签名密钥对。私钥只配置到Cloudflare `LICENSE_SIGNING_PKCS8`；SPKI公钥由发行方验明后固定进客户端代码。
2. 先发布允许 **两个** keyId（旧+新）的客户端并实测双签名验证，再把仅新Worker的 `LICENSE_SIGNING_PKCS8` 更新成新私钥，同时指定新 `LICENSE_SIGNING_KID`。
3. 等待至少1小时旧离线签名票据到期后，才可以把旧公钥从后续客户端构建剔除。客户端旧版若只固定旧公钥必须升级，否则会被新Worker拒绝，不能在服务端绕过签名校验。
4. 卡密 HMAC Secret轮换应在仅Telegram环境配置 `LICENSE_PEPPER_PREVIOUS` 为旧Secret，新 `LICENSE_PEPPER` 发行未来卡密；确认旧卡到期或通过受控迁移重算摘要后才能移除旧Secret。
5. 管理员Token/TOTP更换时，应先核实另一个可用恢复流程，再通过已经认证会话调用 `/v1/admin/sessions/revoke` 吊销全部旧会话。恢复码轮换会使全部旧恢复码失效。
6. 尽可能限制 Worker 管理域名来源并在公网独立设置 Cloudflare WAF/防暴力登录规则；审计定期检查 `login_denied`、`rate_limited`、`sessions_revoked`。

## 4. 正式验收前的阻断项目

- 云端资源当前**完全未部署**；必须对真实D1原子限流/数据库并发、独立Worker Secrets、签名公钥版本、Cloudflare异常返回和真实IP防护做联调。
- Win10/11安装、用户可见激活与续费界面、厂商固定公钥、设备标识用户数据加密、正式 TDLib 发送前许可证检查、1小时断网后的停发行为均需后续项目阶段完工。
- Signal MS002已有生产卡密系统不需要迁移，亦不能使用它的D1、Secret、域名来替代Telegram独立授权系统。
