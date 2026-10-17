# MOSEN Telegram Scheduler — V1.0 候选 Release Notes

**状态：PRE-RELEASE PREPARATION ONLY / NOT PUBLISHED。日期2026-10-17。**

## 已完成并由开发CI验证的组件
- 深色Windows x64 WPF外壳，内嵌本地WebView2 UI、桌面/开始菜单快捷方式及MS002隔离的用户数据位置；编译独立自包含安装器（未签名，WebView2离线运行时随包）。
- SQLite分版本迁移与一致性备份、历史日志脱敏、审计、恶意备份迁移哈希验证和恢复候选。
- 账号备注与角色映射、群/话题成员权限预检、剧本编辑/导入导出、三段等待/正在输入/确认后等待、多群公平调度和离线合成回执去重、异常人工复核及全局停止。
- 独立Telegram许可证Worker与D1设计/离线测试、管理员会话/MFA策略、客户端签名票据验证、签名更新/断点续传/失败程序回滚逻辑（未部署真实生产密钥）。
- 第29步32个P0映射与跨模块Windows/Linux离线测试；第30步300元数据/50群及72小时加速虚拟时钟；第31步源码泄漏扫描和真实发布阻断机制。

## 重要风险与未完成项
- 本版本**没有经实机验收的原生TDLib真实账号授权/真实消息发送管线**，合成调度结果不能冒称用户群中的真实发送。
- 300代表SQLite元数据，不是300个同时在线真实Telegram会话；72小时为加速逻辑时间，非真实72小时稳定性。
- 目前无正式签名PFX/SPKI pin及正式签名安装包、无独立Cloudflare生产Worker/D1联调、无完成的Win10/11安装/更新/断电数据恢复证明。
- 本地安装目录内PowerShell更新辅助脚本完整性威胁模型仍未解除；第三方依赖许可、商标及品牌仍待人工审核。
- 13项release/readiness.json验收全未通过，正式Release不允许发布。**CI未签名包只适用于开发测试且不应导入真实账号。**

## 已有可复核证据
- 第29步全功能开发回归：[GitHub CI 39032907487](https://github.com/mosen6266-netizen/MS_001/actions/runs/39032907487)，55/55。
- 第30步合成容量：[GitHub CI 39033877609](https://github.com/mosen6266-netizen/MS_001/actions/runs/39033877609)，57/57。
- 第31步安全审计：[GitHub CI 39035133383](https://github.com/mosen6266-netizen/MS_001/actions/runs/39035133383)，58/58。
- 第32步最终文档/交付验证运行及真实安装工件哈希，应由第32步实际CI证据填写，不得使用旧包的哈希冒充新包。

使用和支持材料：[用户指南](../../docs/USER_GUIDE.md) · [故障](../../docs/TROUBLESHOOTING.md) · [恢复](../../docs/RECOVERY_GUIDE.md) · [正式验收](../../docs/FINAL_ACCEPTANCE.md)。
