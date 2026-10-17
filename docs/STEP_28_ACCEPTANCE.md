# 第28步开发阶段验收｜可信签名更新、下载与安全回滚

日期：2026-10-17。唯一目标仓库：[MS_001](https://github.com/mosen6266-netizen/MS_001)。
结论：**done_dev（开发阶段验收通过）**，不等于正式代码签名或真实 Windows 安装/升级/回滚验收。

## CI 验证

- [GitHub Actions 39031497799](https://github.com/mosen6266-netizen/MS_001/actions/runs/39031497799) **completed / success；52/52 Jobs SUCCESS**；对应源码提交 [716f22ad](https://github.com/mosen6266-netizen/MS_001/commit/716f22ad3a4c707b5b4574cad34b738f2d1c70ff)。
- Windows/Linux 更新安全测试各**22条断言 PASS**：P-256签名、未知公钥、产品/URL/版本约束、SHA256、完整下载、HTTP Range断点、Range回退、哈希错误删除、断网保持原安装。
- Windows/Linux 发布安全合同测试各**4条 PASS**：最小权限手动草稿发布、内嵌公钥、确认交互、回滚脚本合同。
- Windows/Linux ReleaseSigner和ReleaseVerifier的独立.NET10构建通过；Windows PowerShell静态解析3个安装/签名/回滚脚本均通过。
- WPF Windows x64编译、自包含发布及历史全模块回归通过。
- CI在Windows实际编译Inno离线安装包：**MOSEN-Telegram-Scheduler-Setup-0.27.0-dev.exe**，**269,072,288 字节**；SHA-256 **E81CA3181840A67603677EBBBD83090B94F85D2269290FD3FEDA747CFE76FFBF**。该包仍**未签名**，暂存Artifact为 ms001-step27-offline-installer-unsigned，保存约至2026-10-24，非正式发售安装包。

## 核心交付

1. src/Core/Updater：P-256公钥固定签名清单、固定本产品和GitHub HTTPS Release路径、整包大小/版本/SHA256校验、断点续传；拒绝符号链接缓存。
2. apps/desktop/MainWindow.Updates.cs：Windows原生“检查更新”，内嵌发布者公钥，未配置时拒绝自动更新，用户确认后下载；确认绿色在左、取消红色在右。
3. updater/Apply-Update.ps1：原程序退出后再次校验安装包 SHA256及Windows Authenticode；保留两代 previous-program 和 previous-program.prior 备份；pending备份拒绝覆盖；成功安装后核对新程序的签名及版本，错误尽力恢复旧程序并保留故障记录；不清理私有数据库、账号、角色备注、剧本或WebView2用户配置。
4. updater/ReleaseSigner、updater/ReleaseVerifier、.github/workflows/release.yml：签名与独立验签工具，构建阶段只读仓库，单独最小写权限作业只生成草稿Release，密钥/证书缺失即拒绝发布。

## 发现并修复

- Windows CI解析发现一个回滚脚本版本参数字符串截断与旧脚本重复附加，修复后新CI通过。
- Windows WPF与WinForms同名类型(Color、Brushes、Button、Application等)引发编译失败，且进程启动语句有C#表达式错误；显式限定WPF类型、修正语句后重新构建发布通过。
- 旧备份目录原本阻止连续升级，改成两代滚动备份和中断临时备份阻断。

## 正式发布验收延期（DEFERRED）

- 目前公开信任值 release-v1.spki.base64 与 publisher-thumbprint.txt 为 UNCONFIGURED；尚无有效发行者签名PFX及独立P-256私钥，**没有执行真实签名工作流、没有公开正式Release、开发版默认禁用自动更新**。切勿误称已发布真实签名EXE。
- 真正Win10/11实机断电/断网/磁盘故障/杀软、连续真实安装恢复、SQLite跨版本迁移与回退、TDLib原生合法账号、50群负载、Cloudflare独立许可生产对接，全部依最终发布门禁单独验收，尚非PASS。
- Signal MS002和其Cloudflare生产系统未修改。

**开发进度28/32（87.5%），剩余4步；下一步29全功能自动化回归。**
