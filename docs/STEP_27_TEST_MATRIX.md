# 第27步安装器与独立用户数据验收矩阵

- [x] 用户图标 `icon.svg` → 多尺寸 `app.ico`，应用程序EXE及安装器均嵌入统一图标。
- [x] win-x64 WPF self-contained `dotnet publish`，不要求最终用户自行安装Python/Java/.NET SDK。
- [x] WebView2官方x64 Evergreen Standalone安装器，CI验证微软有效数字签名后随主安装器离线打包。
- [x] Inno Setup 6真正编译 `MOSEN-Telegram-Scheduler-Setup-0.27.0-dev.exe` 和 SHA256校验清单。
- [x] 当前用户无管理员安装、用户桌面与开始菜单快捷方式、稳定AppId、同版本原地覆盖安装。
- [x] 关闭程序后覆盖安装（mutex锁）；升级前SQLite自动备份；卸载默认保留用户数据、备注、剧本和TDLib。
- [x] WebView2用户配置目录独立，写入LocalAppData而非Program Files/安装目录。
- [x] 单独强确认的可选数据删除脚本，不含隐式或自动清理，拒绝符号链接和未知位置。
- [x] Windows/Linux单元及Inno合同断言，模拟覆盖/默认卸载证明用户数据与程序目录分离。
- [x] 全部旧CI和Windows桌面构建通过，仓库状态/验收报告/下一步27→28正确。
- [ ] DEFERRED：**干净Win10/11实体/VM**离线安装、无WebView2时自动安装、开始菜单/桌面图标视觉、覆盖旧用户真实账号、真实卸载回装及防病毒检查，最终版集中执行。
