# V1.0 发布准备区 — RELEASE BLOCKED

这是MOSEN Telegram Scheduler在MS_001仓库的V1.0发布准备目录，**不是已经发行的正式版本**。当前版本签收状态：NOT RELEASE READY。release/readiness.json共13条真实验收要求全部DEFERRED且releaseAuthorized=false。

本目录包含：
- RELEASE_NOTES.md：拟议V1.0功能摘要、实际开发CI证据、未完成事项。
- ARTIFACTS.md：GitHub Actions临时未签名开发包/交接包的定位与SHA核验办法，不能当作正式下载页。
- RELEASE_SIGNOFF.md：待真实Win10/11、TDLib、Cloudflare、72小时等安全和合规审核通过后人工签收。
- docs/USER_GUIDE.md、docs/TROUBLESHOOTING.md、docs/RECOVERY_GUIDE.md、docs/FINAL_ACCEPTANCE.md位于仓库文档目录。
- release/readiness.json机器可读正式发布强制阻断清单；GitHub签名工作流必须先核对该文件并经受保护发布环境审批。

状态解释：
1. development CI pass / done_dev：源码、文档、模拟/云端构建验收通过，可以提交可追溯的**未签名开发构建Artifact**，不代表真实Telegram可用。
2. release blocked：任何一项真实验收没有可核验证据，不能开放正式版本下载，也不能改名成签名V1.0。
3. officially released：需要实际签名PFX及固定SPKI信任、官方tdjson/Cloudflare联调、真实设备/账号权限和资源报告、第三方许可审批、独立审核和最终人工签收；GitHub Actions当前只允许生成草稿Release，不能自动直接发布Latest。

不得将Signal MS002源代码、卡密或生产Cloudflare数据库迁移到本产品。
