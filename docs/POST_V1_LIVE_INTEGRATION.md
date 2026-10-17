# MS_001 后续原生连接改进（2026-10-17）

最新开发测试构建：0.28.0-dev
CI: https://github.com/mosen6266-netizen/MS_001/actions/runs/39039528817
结果：completed/success，59/59 作业通过。
受测提交：842769ae219ca199a921256f8ebcd88e117e8569。

Windows 安装工件：https://github.com/mosen6266-netizen/MS_001/actions/runs/39039528817/artifacts/12104145069
EXE：MOSEN-Telegram-Scheduler-Setup-0.28.0-dev.exe
字节数：269069612
SHA256：F6E517CD3C6E29F2B4EA16FCE08AF4D7C9DDA0C0BFCC7E9133689E9F6ADDAEA4
仍为未签名开发安装包。

本次实际完成：
- Windows 用户个人的 Telegram 应用 API ID、API Hash 配置入口。
- API Hash 保存时使用本机 DPAPI CurrentUser 加密，不回传给 WebView 或诊断。
- 支持读取是否配置、显式清除；IPC 强制只允许预期字段/长度。
- 同步修改前端请求合同、设置UI、Windows本地接线、安全测试，升级安装包版本号以便与0.27区分。
- Windows/Linux整体CI与现有离线 Inno 安装器构建全绿。

真实能力边界：
- 新功能仅配置 Telegram 应用 API 参数，**并不构成Telegram账号已登录**。
- tdjson.dll 尚未打包、官方TDLib授权状态尚未接通 WPF、真实群消息发送没有实现。
- 尚无生产许可、真实Win10/11部署与V1.0签名验收。
- 不允许宣称0.28开发构建为正式全功能版本，也不要以合成回执代替真实投递。
- 后续继续从此main分支增量维护；不要重写MS_001或改动Signal MS002。
