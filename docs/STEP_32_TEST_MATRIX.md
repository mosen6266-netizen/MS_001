# Step32 — 最终交付和V1.0签收矩阵

开发CI能完成的阶段：
- [x] V1.0候选使用说明、故障处理、数据库和升级恢复文档全部形成仓库文件。
- [x] releases/V1.0/含README、Release Notes、版本工件说明及空白人工签收表；清晰告知V1.0未正式发布。
- [x] docs/FINAL_ACCEPTANCE.md按照release/readiness.json逐项保留13个真实世界DEFERRED，不把模拟任务或未签名CI安装器当作生产程序。
- [x] GitHub CI新增handoff-docs作业：生成确定性的ZIP、分文件SHA256 manifest、ZIP SHA256；只打包文档/状态，没有EXE或密钥。
- [x] Windows和Linux既有全量CI、未签名开发Inno Setup独立编译、文档合同测试和handoff-docs真实执行通过、最终aggregate全部成功、记录运行SHA及工件链接。
- [x] docs/STEP_32_ACCEPTANCE.md和docs/PROGRESS.md更新最终可复核开发阶段验收结论与真实阻断情况。

**不是可自动通过的发行签收（保持BLOCKED）：**
- [ ] Windows10真实干净安装、升级、卸载、回滚及数据保存。
- [ ] Windows11真实干净安装、升级、卸载、回滚及数据保存。
- [ ] 官方TDLib原生动态库和本人合法授权账号的真实登录/群权限/发送与退出测试。
- [ ] 50真实群/300真实在线账号容量，以及72小时真实墙钟资源长稳。
- [ ] 正式Cloudflare独立授权Worker、真实D1、MFA、签名密钥及客户端校验链路。
- [ ] 第三方依赖与版权商标审核、PowerShell本地更新helper完整性风险关闭。
- [ ] 两种独立签名私钥和真实Windows签名EXE/Setup、可信更新清单、独立人员人工验收与GitHub受保护环境发布审批。
- [ ] 正式GitHub Release v1.0.0签发并对外提供可核验下载URL。

**规则：** 第32步可以在全部开发文档/云端构建/ZIP核验完成后记done_dev，开发进度为32/32；但正式V1.0始终要单独标记BLOCKED，直到以上真实门槛全部提供证据并通过发布审批。

## 实际验收：2026-10-17

[Run 39036960159](https://github.com/mosen6266-netizen/MS_001/actions/runs/39036960159) completed/success，59/59 Jobs全部成功，源码 SHA 1b17ba0c8e6170e5b64023fbe0fae5ad7a3d30e3；总门禁32上游Job全成功。资料ZIP内含19份源文件，SHA256 3FF4BF94C4BB9AC2DB843A61A2EC647884EF0D23E8BE721001285E828BBA844F，GitHub [资料artifact](https://github.com/mosen6266-netizen/MS_001/actions/runs/39036960159/artifacts/12102174301)。未签名开发Setup 269,075,217字节，SHA256 85D04E5CC52FC33D81D062518603D38E1ADB7EC0A19CA6A5EE607236C8589C9A，GitHub [EXE artifact](https://github.com/mosen6266-netizen/MS_001/actions/runs/39036960159/artifacts/12103105698)。**正式V1.0仍全部BLOCKED，不勾选真实签发项。**
