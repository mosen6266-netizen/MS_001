# 第27步开发阶段验收｜Windows完整离线安装包和数据独立

日期：2026-10-17。唯一仓库 `mosen6266-netizen/MS_001`。**结论：done_dev，第28步待开始**，并非实际Win10/11干净机器安装或正式发行签名验收。

## GitHub CI真实证据

- [GitHub Actions Run 39026535003](https://github.com/mosen6266-netizen/MS_001/actions/runs/39026535003)：**completed / success，50/50全部通过**。
- Windows/Inno Setup 6.7.1 **真实编译得到** `MOSEN-Telegram-Scheduler-Setup-0.27.0-dev.exe`，**269,048,726字节**。SHA-256: `B89BA706946CC4EE13F100B6917090A487132464EA556DEF67F2D42A09EC1153`。
- Windows GitHub Actions artifact `ms001-step27-offline-installer-unsigned`（大小268,624,546字节，为平台对上传产物的打包大小，含EXE和SHA清单，非安装EXE自身精确大小）。CI已确认该artifact和应用WPF发布、用户图标、中文安装界面均已上传，保留期7天，不作为最终发布链接。
- 下载的是 Microsoft Edge WebView2 Evergreen Standalone x64（官方 `https://go.microsoft.com/fwlink/?linkid=2124701`），运行时文件大小检查且微软 Authenticode 数字签名有效；与安装器一起封装以便未联网环境安装。
- Windows / Linux各通过 `tests/Installer/Program.cs` **22/22**安装路径和升级/卸载模拟断言、`contract.test.mjs` **4/4**配置安全测试；原有48项CI回归以及 Windows UI构建全部成功。

## 交付范围

- 自包含Windows x64 WPF编译产物，无需最终用户安装Python/Java/.NET SDK；统一图标及桌面/开始菜单快捷方式，stable AppId，安装到当前用户LocalAppData\Programs。
- `AppMutex` 阻止运行期间覆盖安装；持久数据仍位于旧的 `%LOCALAPPDATA%\MOSEN\TelegramScheduler`，包括账号、备注、数据库、剧本、备份、通知和WebView2配置，绝不写进安装目录。
- 卸载默认保留数据；独立 `Remove-LocalUserData.ps1` 只有主动调用并使用精确中文确认词才删除用户数据，不会影响Signal MS002。
- 简体中文安装向导语言为MIT开源翻译，并保留许可文件；English可选。
- 此阶段发现和修复官方WebView2小型在线bootstrapper链接与Inno缺少中文语言文件两处问题，保留离线包与签名校验要求。

## DEFERRED / 不应过度宣称

- 未曾在干净Windows10/11实体或VM中实际双击安装器，也未跑真实WebView2缺失场景、用户数据覆盖/卸载后的自动验证和安全软件测试；这些工作在最终完整正式版本阶段统一完成。
- **安装包未进行出版社Authenticode签名**；第28步实现签名自动更新、可信发布、断网和损坏包回滚策略。这一开发包不应发给用户用于正式生产。
- Telegram TDLib实际授权登录和真实发送、云端Cloudflare许可证部署还没有在此步骤集成，不能将离线安装模拟视为最终产品可用。
- Signal MS002生产仓库、安装、账号和Cloudflare卡密均未修改。

**开发进度27/32（84.375%），剩余5步；下一步28。**
