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

## 2026-10-17 后续集成：0.29.0-dev，Windows TDLib + 登录 + 群组同步 + 手动发送

**最终受测GitHub CI**：[Run 39042347008](https://github.com/mosen6266-netizen/MS_001/actions/runs/39042347008) completed/success，**60/60 Jobs success**，受测SHA `42c662a941e1240161d530f2153150abb3d58681`。

**新的可下载 Windows 10/11 x64 安装包**： [GitHub artifact 12105005964](https://github.com/mosen6266-netizen/MS_001/actions/runs/39042347008/artifacts/12105005964)，文件名 `MOSEN-Telegram-Scheduler-Setup-0.29.0-dev.exe`，295,834,863字节；真实EXE SHA256：`D26451E6F7F03B136B93FA345E9BA22CDD2DB2CEF5B37C4FE7DAB871A2196525`。**仍是未签名开发包，不是正式签名V1.0**。

本次实际代码交付：Windows桌面NuGet包含tdlib.native.win-x64 1.8.67官方TDLib协议原生库，并添加真实Windows Native Smoke，成功加载tdjson.dll返回版本1.8.67；运行时发布目录断言必须存在tdjson.dll。离线安装包嵌入经过微软Authenticode校验的Microsoft Visual C++ x64运行库和WebView2。登录桌面界面连接真正的TDLib状态机，支持扫码二维码（本机离线生成临时PNG）、本人手机号、验证码、邮箱验证码和两步验证；单Windows用户DPAPI保存应用API参数及TDLib数据库密钥。群组页支持TDLib loadChats→getChats→getChat，只显示普通群和超级群，并检查can_send_basic_messages。人工确认后可向**一个已选且有当前权限的群**提交不超过2000字符的文字，等待TDLib updateMessageSendSucceeded/Failed，结果未知不自动重发，手动发送速率限制30秒；不接入多账号批量群发、脚本自动发送或绕过Telegram限制。

剩余验证与限制：**未使用用户真实登录凭据做过网络端到端测试，也没有实际发出真人Telegram消息的验收证据**；必须由用户安装自测确认实际官方授权、群列表、真实发出和回执。一次连接仅一个实际TDLib账号，不等于300真实并发账号。Cloudflare生产卡密、真实72小时物理长稳、正式Windows签名与V1.0发行门禁仍未满足；release/readiness.json仍BLOCKED。请后续在此受测源码SHA基础上增量调试，绝不重新写工程或改Signal MS002。

## 2026-10-17 — 0.29.1-dev 账号管理点击白屏修复

用户实际报告：Windows 0.29.0-dev进入“账号管理”后窗口内容呈整页深黑/空白，没有账号列表。根因未取得真实客户端原生/WebView2诊断堆栈，不能臆称已经完全定位；代码已在疑似React原生状态响应和WebView2进程异常路径增加保护。

本次修复：
- 登录组件不再未经验证把WebView2的任意返回值强制当作TelegramLiveStatus；严格验证布尔字段、群组数组、二维码和长度；缺失groups等异常数据不会触发未捕获的groups.filter渲染异常。
- TDLib状态刷新避免每2.5秒积累多笔尚未完成的查询。
- 账号管理和群组页两层PanelErrorBoundary，登录面板与账号数据面板各自独立恢复，确保单一React组件失败不能卸载整个Shell；App根也有显式恢复页面。
- Windows WebView2.ProcessFailed产生可见WPF诊断界面，避免原生渲染进程退出后仍保留全黑区域；不外泄错误堆栈、账号或API密钥。
- 增加SSR渲染Windows账号页面/群组页面与原生状态异常载荷的前端回归测试；保持原有安装器AppId与私有数据目录，支持覆盖安装设计。

**CI实证：** [Run 39044069673](https://github.com/mosen6266-netizen/MS_001/actions/runs/39044069673)，受测提交 `982ba896183b70a30d846f6e3019a32c61661feb`，60/60 Jobs全部成功。Windows桌面WPF构建、原生tdjson DLL版本测试、JS/React UI/新页面回归、离线WebView2与VC++ Inno安装器均通过。

**开发安装器：** [GitHub Actions artifact 12105490588](https://github.com/mosen6266-netizen/MS_001/actions/runs/39044069673/artifacts/12105490588)，`MOSEN-Telegram-Scheduler-Setup-0.29.1-dev.exe`，EXE实际大小 **295851009 bytes**，真实EXE SHA-256 **9B3CB02DE21B5970B38F44B24027F781699C235946CFCCF946F10D8240A225B1**，约2026-10-24过期。仍为未签名开发测试包，真实Win10/11现场点击和用户合法Telegram登录/发送尚待测试，不能保证所有故障都彻底消失。**不触碰Signal MS002。**
