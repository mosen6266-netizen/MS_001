# MOSEN Telegram Scheduler — 最终V1.0验收总表

日期：2026-10-17。唯一仓库 mosen6266-netizen/MS_001；与Signal MS002完全隔离。

## 两种不同验收（禁止混用）
- **开发步骤验收**：32步可由离线自动测试、云端构建、审计和可复现交付材料证明。可以记为done_dev，绝不能称真实用户工作流或正式发布通过。
- **正式发行签收**：需要真实设备、原生TDLib、用户许可、第三方许可及正式签名测试证据并经人工签发。当前**未通过、发布BLOCKED**，不创建正式GitHub Release、不发布V1.0 Latest链接。

## P0和CI证据
需求与单测映射：docs/REQUIREMENTS_MATRIX.md、docs/TEST_MATRIX.md、tests/Unit/p0-regression-catalog.json（32条P0追踪）、docs/PROGRESS.md。每条已完成开发步骤见docs/STEP_01_ACCEPTANCE.md至STEP_31_ACCEPTANCE.md以及本步STEP_32_ACCEPTANCE.md。

- 全功能离线E2E：Run 39032907487，55/55作业成功（Windows/Linux各30条模拟业务链断言）。
- 合成容量：Run 39033877609，57/57成功；300条SQLite元数据/50群本地模拟、72小时**加速逻辑**，不是300真实登录和72小时真人机器长稳。
- 安全审计：Run 39035133383，58/58成功；339个受版本控制文本扫描和Win/Linux各32条SQLite迁移断言。
- 第32步文档、交接Artifact和最终CI必须由本步骤运行结果**另行记录**，不能编造结果。

## 正式签收前需完成的实证门禁
系统读取 release/readiness.json，固定13条真实要求，当前13/13 status=DEFERRED、releaseAuthorized=false。无有效证据时，正式GitHub Actions发布作业拒绝执行。

1. Windows10真实干净安装/升级/卸载及数据库保留。
2. Windows11真实干净安装/升级/卸载及数据保留。
3. 原生官方tdjson.dll来源、版本、运行正确性与依赖可信验证。
4. 自有授权账号的真实QR/验证码/两步验证/重启/退出。
5. 真实Telegram群/Topic成员授权、禁言、限流及不确定回执处理。
6. 合法真实50群稳定性及停止行为。
7. 合法真实300个账号的连接上限、RAM/CPU及重连，不是metadata测试。
8. 连续72小时**墙钟**的实际CPU、内存、句柄、数据库和网络长稳取样。
9. 物理电源/磁盘/WAL异常、SQLite备份与签名更新失败回滚。
10. 正式Windows代码签名PFX、公钥指纹、安装器/EXE、可信清单和本地PowerShell更新helper完整性风险消除。
11. 独立Cloudflare Worker和D1、MFA、Secret、签名票据及客户端许可门禁联调。
12. 实际随包第三方代码/许可/署名/商标合规复核。
13. 正式用户/授权代表签收及独立审批。

每一项必须有核验人、UTC时间、真实可追溯证据；GitHub受保护环境审批和签名凭据检查仍为额外条件。模拟CI全绿不能自动给真实证据打勾。

## V1.0交付项状态
| 交付物 | 状态 |
|---|---|
| 用户手册、故障排查、恢复操作、候选Release Notes | 源码仓库已准备，待本步CI验收 |
| Windows x64未签名离线候选Setup | GitHub Actions构建可产出，工件有过期时间；不是正式发布 |
| 发布准备资料包及内容hash | 第32步CI构建和校验后登记 |
| Windows正式签名V1.0 EXE和Setup | NOT BUILT / BLOCKED |
| 官方TDLib及Cloudflare生产端到端 | NOT VERIFIED / BLOCKED |
| 真实Win10/11与72小时验收 | NOT VERIFIED / BLOCKED |
| 正式GitHub Releases v1.0.0（公开发布） | NOT PUBLISHED |
| 人工签字授权 | NOT APPROVED |

## 结论
**当前正式V1.0验收结论：BLOCKED。** 完成全部开发步骤之后，可记录“开发计划32/32完成”；但需保留发行状态BLOCKED直到所有真实门槛解决。切勿将开发测试结果当成正式产品可使用或已发布的证明。

人工签名：未完成；日期：未填写；二次安全审核：未完成。
参阅 releases/V1.0/RELEASE_SIGNOFF.md、docs/RELEASE_CHECKLIST.md、docs/SECURITY_AUDIT.md。

## Step32开发交付CI结果（非正式发布批准）

2026-10-17已核查 [Actions Run 39036960159](https://github.com/mosen6266-netizen/MS_001/actions/runs/39036960159) completed/success，**59/59作业全部通过**；受测代码SHA 1b17ba0c8e6170e5b64023fbe0fae5ad7a3d30e3。文档ZIP19文件SHA256 3FF4BF94C4BB9AC2DB843A61A2EC647884EF0D23E8BE721001285E828BBA844F，unsigned dev安装EXE 269,075,217 bytes/SHA256 85D04E5CC52FC33D81D062518603D38E1ADB7EC0A19CA6A5EE607236C8589C9A。见[Step32报告](STEP_32_ACCEPTANCE.md)。全部13项正式release/readiness仍DEFERRED；本文件仅承认开发交付验收，不代表V1.0正式上市。
