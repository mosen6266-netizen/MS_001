# V1.0资产定位与可信校验

当前**不存在已验收的正式V1.0签名安装包**。以下仅说明临时开发构建工件的定位，不提供伪造的正式下载URL。

- 进入 [MS_001 GitHub Actions](https://github.com/mosen6266-netizen/MS_001/actions)，打开最近一个完整通过的Main build and unit tests运行。
- 在Artifacts区域找到 ms001-step27-offline-installer-unsigned，下载后需解包查看开发EXE及同名.sha256校验文件。GitHub Actions工件会过期；不存在永久开发下载保证。
- 第32步将把发布准备文档压缩为独立档案 v1-development-handoff。这个交接档案**不包含私钥/卡密，也不替代安装程序签名**。
- 在本地PowerShell使用 Get-FileHash -Algorithm SHA256 -LiteralPath <EXE路径> 核对工件内的.sha256，切勿仅核对ZIP本身的体积。文件哈希只证明内容与清单一致，不证明来自可信发布者。正式EXE必须同时验证Authenticode发布者及被签名清单。
- 当前默认签名信任pin均为UNCONFIGURED，完整正式签名CI没有执行。不能将development Artifact链接当作GitHub Release链接，也不能声明“已经上架”。

每个Actions运行有其自己的测试源码SHA、可下载工件、过期日期和独立校验数据；正式发布前重新构建和记录准确结果。真实Win10/11实体测试与13项release/readiness签收仍然必需。

## 2026-10-17已核实的交付工件

- [开发文档交接包 v1-development-handoff](https://github.com/mosen6266-netizen/MS_001/actions/runs/39036960159/artifacts/12102174301)（19份文件，内层v1-development-handoff.zip SHA256 3FF4BF94C4BB9AC2DB843A61A2EC647884EF0D23E8BE721001285E828BBA844F，预计过期2026-11-16）。
- [未签名的 Windows dev安装包](https://github.com/mosen6266-netizen/MS_001/actions/runs/39036960159/artifacts/12103105698)（MOSEN-Telegram-Scheduler-Setup-0.27.0-dev.exe，269,075,217字节，EXE SHA256 85D04E5CC52FC33D81D062518603D38E1ADB7EC0A19CA6A5EE607236C8589C9A，预计过期2026-10-24）。
- CI [39036960159](https://github.com/mosen6266-netizen/MS_001/actions/runs/39036960159) completed/success 59/59，受测代码 SHA 1b17ba0c8e6170e5b64023fbe0fae5ad7a3d30e3。GitHub artifact外层zip文件sha与原始内部文件的SHA不是相同对象。
- 不存在正式V1.0的签名EXE/正式更新签名清单/公开GitHub Releases v1.0.0；不得把开发工件当作正式下载链接。
