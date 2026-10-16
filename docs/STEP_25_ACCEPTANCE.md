# 第25步开发阶段验收｜Telegram独立卡密产品授权

日期：2026-10-17。唯一仓库 `mosen6266-netizen/MS_001`。结论 **done_dev**，下一步 **26**。开发通过不等于已开通云端生产卡密服务。

## 实际成功的证据
- [GitHub Actions Run 38974949818](https://github.com/mosen6266-netizen/MS_001/actions/runs/38974949818)：**completed/success，47/47项作业成功**（读取全部job，不是默认前30项），包含Windows WPF构建发布及所有既有回归。
- `license/tests/license.test.mjs`：Windows/Linux各4组情景（真实内存SQLite+D1-compatible prepared/batch适配）全部通过；覆盖独立产品HMAC卡密、首次激活、设备上限、15个并发绑定请求抢2个名额、重复/并发续费的唯一应用、禁用/启用、过期、跨产品隔离、错误Secret拒绝。
- `tests/Licensing/Program.cs`：Windows **39/39**、Linux **39/39**。模拟真实P-256签名和HTTPS协议，验证nonce防重放、其他产品/设备、签名篡改、非法时间、最多1小时离线、服务端明确拒绝、设备匿名种子稳定与损坏拒绝等。
- 第一批CI中Node24全局crypto只读与.NET毫秒舍入断言错误已修正。第二批发现真实续费秒/毫秒换算错误，已修正并再次在上述完整第三批全绿CI测试中验证。
- 源码初版 [`f394b334`](https://github.com/mosen6266-netizen/MS_001/commit/f394b334054881b171abb2c54c872b28c0b01c06)，业务修复 [`2f72fd45`](https://github.com/mosen6266-netizen/MS_001/commit/2f72fd45a07fdcbf915472072f7af2b45604b0f3)。

## 隔离验证
- 服务/数据库标识为独立的 `ms001-telegram-license-staging`、`MOSEN_TELEGRAM_SCHEDULER`，并明确要求专用D1和新的Secret，不接入Signal MS002任何现有卡密/Worker/D1 ID。
- 公共源码只存 `REPLACE_WITH_NEW_TELEGRAM_D1_DATABASE_ID` 配置占位符。**实际没有创建或部署Cloudflare Worker、D1，亦未保存生产管理员密钥或ECDSA私钥到仓库。**
- 离线令牌最长一小时且仅在本进程使用；管理员调用与设备授予有服务端权限判断。现有真实 Telegram 发送功能尚未启用，不能宣称已经有生产卡密控制网络投递。

## 未完成的正式发布事项（DEFERRED）
- 独立Cloudflare测试/正式数据库真实创建、迁移和Worker上线；单独Secret部署与备份、管理员会话/TOTP/恢复码/限流/CSRF、钥匙轮换（第26步）。
- Windows激活/续费UI、可靠厂商SPKI公钥内嵌、许可证门禁接入真实TDLib消息发送；现有只读离线模拟未连接云端许可，此类实际产品授权需要之后集成。
- Win10/11实体机器账号绑定、换机/磁盘恢复、Cloudflare公网实际HTTP、服务不可用/吊销时序及72小时稳定性仍需后续验收。

**项目开发进度 25/32（78.125%），剩余7步。第26步待开始。**
