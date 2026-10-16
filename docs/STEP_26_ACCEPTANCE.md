# 第26步开发验收｜云端授权安全性、恢复码和离线策略

日期：2026-10-17。唯一仓库 `mosen6266-netizen/MS_001`。**结论 done_dev，第27步待开始**。注意开发完成不代表已在Cloudflare上线或实际发送链路可用。

## 自动化实证
- [GitHub Actions Run 38976961308](https://github.com/mosen6266-netizen/MS_001/actions/runs/38976961308)：**completed / success，47/47项目业成功**（已检查所有47项，不是默认前30项），包含Windows win-x64 WPF构建与自包含发布、所有旧SQLite、50群模拟、回执、恢复、权限、历史、资源预算与桌面UI回归。
- `license/tests/license.test.mjs` + `security-scan.test.mjs`：**Node24 Windows 10/10、Linux 10/10**。真实内存SQLite通过受控D1兼容适配器模拟数据库原子操作，覆盖登录TOTP单次使用、管理员身份、恢复码、操作Origin/CSRF、15分钟会话、限流、HMAC轮换、签名keyId轮换以及旧行为回归与独立产品安全扫描。
- `tests/Licensing/Program.cs`：**Windows 54/54、Linux 54/54**断言通过。真实P-256公私钥签发/验证、最多两个发布方固定SPKI、公钥替换拒绝、失败清除本地许可、墙钟倒拨和离线最长1小时条件。
- 主源代码提交 [`1471fa77`](https://github.com/mosen6266-netizen/MS_001/commit/1471fa77e6d13e6a6d3fa91a0a60f785d2e278e9)，修复管理路由旧直通 [`dab8ef2a`](https://github.com/mosen6266-netizen/MS_001/commit/dab8ef2a55fa0b8fd5f3e94ac3a547f374d0a965)，最终D1兼容性修订 [`c875ff3c`](https://github.com/mosen6266-netizen/MS_001/commit/c875ff3ca997a43389dc13bded5868334c3a2a3e)。

## 安全能力
- 旧管理员Bearer令牌无法直接生成、禁用、续费卡密；必须先经主Token+TOTP或只用一次的恢复码建立15分钟随机session，再用独立CSRF令牌执行。可主动撤销当前或所有会话。
- D1对管理员登录、受保护操作及客户端验证执行安全限流并保存脱敏安全审计；失败固定响应，不包含密钥与堆栈。
- 已实现新旧HMAC Key轮换过渡、签名公钥双版本Pinning机制，以及签名离线许可内的单调时钟防篡改。
- 所有Secret由Worker环境配置，公开仓库不含正式私钥、管理员令牌或真实D1绑定；只操作 `MS_001` 的专用Telegram源文件。

## 仍须落实的正式验收（DEFERRED）
- 本步没有调用Cloudflare API创建或部署新D1/Worker，没有正式签名私钥配置，没有实际运行网上管理员登录/审计，只有Node24模拟D1与本地 .NET 签名验证。
- 15分钟session、TOTP错误次数限制、双公钥轮换及1小时离线撤销需要Cloudflare真实Worker/D1、证书、域名、WAF与Windows10/11安装包实测。
- 当前MS_001尚未连接真实TDLib在线消息发送服务，也没有把授权系统接入WPF正式启动和每次发送前许可门禁；不能宣称已实现授权上线的真实自动消息发送。
- Signal MS002卡密Worker、D1、账号与现有使用不受影响。

**开发进度26/32（81.25%），剩余6步。第27步：完整Windows安装与数据独立。**
