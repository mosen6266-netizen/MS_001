# V1.0资产定位与可信校验

当前**不存在已验收的正式V1.0签名安装包**。以下仅说明临时开发构建工件的定位，不提供伪造的正式下载URL。

- 进入 [MS_001 GitHub Actions](https://github.com/mosen6266-netizen/MS_001/actions)，打开最近一个完整通过的Main build and unit tests运行。
- 在Artifacts区域找到 ms001-step27-offline-installer-unsigned，下载后需解包查看开发EXE及同名.sha256校验文件。GitHub Actions工件会过期；不存在永久开发下载保证。
- 第32步将把发布准备文档压缩为独立档案 v1-development-handoff。这个交接档案**不包含私钥/卡密，也不替代安装程序签名**。
- 在本地PowerShell使用 Get-FileHash -Algorithm SHA256 -LiteralPath <EXE路径> 核对工件内的.sha256，切勿仅核对ZIP本身的体积。文件哈希只证明内容与清单一致，不证明来自可信发布者。正式EXE必须同时验证Authenticode发布者及被签名清单。
- 当前默认签名信任pin均为UNCONFIGURED，完整正式签名CI没有执行。不能将development Artifact链接当作GitHub Release链接，也不能声明“已经上架”。

每个Actions运行有其自己的测试源码SHA、可下载工件、过期日期和独立校验数据；正式发布前重新构建和记录准确结果。真实Win10/11实体测试与13项release/readiness签收仍然必需。
