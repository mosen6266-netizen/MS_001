# 第31步 — 安全审计、备份恢复及正式发布阻断测试矩阵

日期：2026-10-17。该矩阵区别开发测试成功与正式发行许可，绝不将CI合成/模拟结果当作Win10/11实机安全认证。

## 开发阶段自动化

- [x] 实际SQLite迁移备份即使PRAGMA integrity_check为ok，也要比对每项schema_migrations SHA256与内嵌官方迁移记录；篡改后PrepareRestore拒绝且不产生新候选、不改动现有live DB与合法恢复候选。Windows和Linux SQLite migration CI各32项PASS。
- [x] git受版本控制的源码文件高置信度令牌扫描；仅报告路径+类别，不泄漏密钥实际匹配内容。Step31 CI实际扫描339个文本文件，结果PASS（不涵盖Git历史或CloudflareSecrets）。
- [x] Release就绪清单固定13条真人/实机安全证据要求，初始全部DEFERRED且releaseAuthorized=false；任何缺条、重复、未审批、证据缺失都拒绝。
- [x] Signed Release仅能手动启动；正式签名之前检查readiness与密钥泄漏，draft发布前复核；发布作业需要独立的protected environment和仅该阶段写入权限。无真实签名证书不允许发布。
- [x] Step31 repository security audit及发布阻断新增8个Node用例全部PASS。
- [x] 最新完整GitHub Actions run全绿（在包含Step31改动的代码提交版本上）、最终聚合门禁验证所有上游任务，保存准确Run/Commit SHA。
- [x] 手工审计高危代码/权限的最后一轮结果与剩余风险表入docs/SECURITY_AUDIT.md。

## 严格禁止自动勾选的最终发行检查

- [ ] 真实Win10/11双系统干净安装、覆盖/卸载及数据备份回滚、物理断电/磁盘异常/锁定。
- [ ] 官方tdjson.dll签名或可复核源码编译、本人合法账号真实授权/登出、50群真实授权与限流场景。
- [ ] 300真实原生会话/72小时真实墙钟长稳与连续CPU/RAM/句柄/连接恢复指标、错误率数据。
- [ ] 独立Cloudflare生产D1与Worker/Secrets/TOTP/撤销/限流真实测试，绝不复用Signal MS002环境。
- [ ] Windows签名证书与更新公钥正式签发、第三方许可/商标合法合规人工审批、更新辅助脚本完整性威胁模型确认。
- [ ] 正式出版审批：release/readiness.json全部13条有真实证据和独立人员签名+GitHub环境受保护审批，只有签名Release才可对外提供。

完成开发阶段CI后可以记录Step31=done_dev；以上真实发布阻断项仍必须逐项核验，不应因开发测试通过而自动清空。

## 开发阶段验证结果

[GitHub Actions 39035133383](https://github.com/mosen6266-netizen/MS_001/actions/runs/39035133383) completed/success **58/58作业全部通过**；受测源码commit a40ec24b721b27c605205cc7ae268c6d22ebb71c。安全审计扫描339个git追踪文本文件，8/8新增Node审计测试PASS；Win/Linux SQLite迁移各32项PASS；最终汇总31个上游Job全success。详细见[STEP_31_ACCEPTANCE](STEP_31_ACCEPTANCE.md)。实机和签名/许可清单仍全部保持DEFERRED，不自动勾选。
