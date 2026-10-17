# Step27｜Windows x64 离线安装包与用户数据保护

## 安装包设计

- 仓库源码 `installer/Setup.iss` 为稳定标识的 Inno Setup 6 安装程序（MOSEN Telegram Scheduler），`build/Build-Installer.ps1` 仅在Windows打包机运行。使用 `dotnet publish --self-contained true -r win-x64` 的WPF运行文件；最终用户**不需要安装 .NET SDK、Python、Java或signal-cli**。
- 运行在当前用户权限（`PrivilegesRequired=lowest`），无需管理员或全局软件目录，安装目标固定在 `%LOCALAPPDATA%\Programs\MOSEN\TelegramScheduler`。同一个 `AppId` 配合 `UsePreviousAppDir=yes` 进行覆盖安装；禁止用户选择可能与个人数据冲突的安装目录。
- 主程序、开始菜单快捷方式、默认勾选的桌面快捷方式、安装/卸载控制面板图标均来自用户提供的 `assets/branding/icon.svg`，由构建环境生成多尺寸 `app.ico`；不在客户端运行 Python。
- 安装器携带 **Microsoft Edge WebView2 Evergreen Standalone Installer（x64）**，下载自微软官方渠道；CI先检查 Authenticode签名主体为Microsoft、证书有效，再将其完整打入离线安装包。安装时检查系统WebView2版本，如没有才运行 `/silent /install`。客户端不能假设用户预先安装过 WebView2。运行时将 WebView2 profile放在本机用户数据目录，避免安装目录写入失败。
- 安装包仍为**unsigned development build**，不能代表已具备正式发行软件的发布者签名/可信自动更新机制。第28步处理发行签名、二阶段安全升级和回滚。
- `AppMutex=Local\MOSEN.GroupScheduler.Desktop.Singleton`：当程序已运行时安装器会阻止直接覆盖运行中的DLL/EXE，需先退出应用；不允许安装时偷偷继续运行后台任务。

## 用户数据隔离和升级

| 内容 | 地址和策略 |
|---|---|
| 程序安装文件 | `%LOCALAPPDATA%\Programs\MOSEN\TelegramScheduler` |
| 数据库、账号备注、剧本、任务、授权状态 | `%LOCALAPPDATA%\MOSEN\TelegramScheduler\app.db` / 产品各自子路径 |
| 数据库迁移备份 | `%LOCALAPPDATA%\MOSEN\TelegramScheduler\backups` |
| 浏览器WebView2配置 | `%LOCALAPPDATA%\MOSEN\TelegramScheduler\webview2` |
| 安全诊断 | `%LOCALAPPDATA%\MOSEN\TelegramScheduler\diagnostics` |
| 历史通知和窗口位置 | 用户数据根目录中的独立JSON |

- C# `UserDataPaths` 保留已使用的 `MOSEN\TelegramScheduler` 相对路径，不触碰 Signal MS002，也不将数据放入程序安装目录。 `UserDataPaths.AssertNoInstallDirectoryOverlap` 拒绝运行目录与用户数据目录重合。
- SQLite既有迁移在升级前独立做备份，禁止跨版本覆盖数据库文件。任何清单更新只替换安装文件；没有 `[InstallDelete]`/`[UninstallDelete]`，不删除数据目录、卡密、账号备注、日志、剧本和TDLib会话。
- 默认卸载：仅移除程序与创建的快捷方式，用户账号、授权数据、数据库和WebView2配置**永久保留**，重新安装可恢复旧数据。
- **可选彻底删除**：仅在确认完整备份且已停止程序后，使用 `build/Remove-LocalUserData.ps1 -Purge -Confirmation '删除Telegram本地数据'`。不传 `-Purge` 只预览；脚本拒绝重解析点和目录联接，防止递归误删其它目标。运行此操作会永久删除全部TelegramScheduler的TDLib和数据库文件，并不会清空Signal数据。
- 本版本不会把用户数据保存在GitHub、安装包或 Cloudflare；不做明文会话迁移。

## 安装与更新检查

1. 已连接的GitHub Action自动编译UI和win-x64 WPF自包含发布；打包前先生成用户提供SVG对应的多尺寸ICO。
2. 检查离线WebView2 EXE 的微软Authenticode签名；签名无效或下载不完整则**拒绝打包**。构建机安装Inno Setup6（仅构建前置，最终用户不需要）。
3. 完整 `Build-Installer.ps1` 生成 `MOSEN-Telegram-Scheduler-Setup-0.27.0-dev.exe` 及旁边的 `.sha256`，GitHub Action 上传短期开发构建产物。
4. 安装时不需要自带 Python/Java。若安装运行时的权限/企业策略限制WebView2写入或安装，则安装过程可能失败，需要Windows实机验证；不能静默宣称已经解决了全部系统兼容问题。
5. 覆盖安装保留账号备注/数据库/桌面位置等历史资料；关闭页面直接退出；桌面图标和开始菜单图标指向相同的exe。
6. 要彻底卸载可运行上述脚本；默认不自动清除用户数据，避免误删。正式 Win10/11 虚拟机 clean install、真实覆盖安装、卸载和恢复属于**DEFERRED 实机验证**。

## 范围限制

- 本步的自动化测试验证程序集编译、安装器真实构建、静态安全配置和模拟安装数据保留，不代表已经在干净 Win10/11 VM 执行安装／卸载／重装全流程。
- Telegram真实TDLib、真实消息发送、云端生产卡密Worker均没有在本步接通。不要使用此dev安装器处理生产账号数据；待后续第28步签名和最终真实测试统一发布。
