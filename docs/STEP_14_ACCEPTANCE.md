# 第14步开发阶段验收：三段时间与图片草稿

日期：2026-10-16。仓库：mosen6266-netizen/MS_001。
结论：开发和云端自动化验证通过。没有实际登录 Telegram 账号，没有实际发送；Windows10/11真人本机验收延后集中进行。

## 权威证据
- 完整 GitHub Actions：https://github.com/mosen6266-netizen/MS_001/actions/runs/38949689786，completed/success，23/23 Job全部通过。
- 新增的时间和本地图片断言：Windows 20/20，Linux 20/20。
- 剧本草稿保存与版本兼容：Windows 29/29，Linux 29/29。
- React 前端：12个测试文件，45/45测试通过；设计静态16/16。
- Windows安全/IPC 42/42，Linux安全/IPC 40/40。
- SQLite历史迁移和备份、300模拟账号/DPAPI、TDLib模拟授权、群组/话题权限、WPF编译、win-x64自包含发布及Chromium首页截图回归全部通过。

## 源码交付
1. 三段时间设定：发送前等待→正在输入→进入发送阶段→平台确认后等待。30秒发送前等待 + 5秒输入，最早第35秒进入发送阶段；真实网络耗时另计。剧本默认5/5/0秒、单条覆盖、按勾选字段批量更改；快捷0/3/5/10/15/30秒以及自定义值。兼容老草稿已有的毫秒字段，不强制取整。
2. 时间线：src/Features/Scripts/Timing/TimingPreview.cs 是独立纯计算模块，无真实网络、系统计时器或消息发送；tests/Scripts/Timing/Program.cs 验证阶段顺序、零延迟和后等待从平台模拟回执后才开始。
3. 便携素材引用：EditorDocument.cs 的MediaReference只含sha256 hash、MIME、长度、尺寸及显示名。没有图片Base64、机器路径、手机号、账号ID及会话密钥。复制消息、撤销、保存和重启保持时间与媒体引用。
4. 内容寻址本地仓库：MediaAssetStore.cs 支持PNG/JPEG/WebP，5MiB单图上限及128MiB本地额度，签名/尺寸/MIME检查、SHA256去重、原子写入、预览完整性复验。移除只移除当前消息引用，不盲删被其他草稿共享的素材。
5. Windows/前端安全接口：MainWindow.Media.cs、IpcRequestGuard.cs和src/contracts/ipc.ts将导入和预览限制在media.import/media.preview，禁止路径注入、超大载荷及不受支持的可执行SVG；其他IPC权限不扩大。
6. 中文编辑器：ScriptEditorPanel.tsx、TimingField.tsx、MediaAttachment.tsx及state.ts实现默认值、逐条设置、批量修改、图片拖入/选择/预览/替换/移除，支持图片与正文共存；保留撤销重做和草稿版本防冲突。

## 本步发现并修复
- 补全Timing与Media在C# Editor项目中的编译包含。
- 旧脚本毫秒值兼容：1500ms等历史时间不被强制四舍五入。
- 修复前端GUID校验漏掉一个四位段，导致旧脚本无法读取。
- 修复默认时间未参与编辑器变更及撤销历史比较。
- 修复新增安全测试C#字符串与图片展示名路径分隔符过滤；完整CI后续全部全绿。

## 实际发布尚未验证
- 真实 Telegram 正在输入状态、上传和发送回执/暂停恢复，待后续任务执行器和TDLib原生集成。
- 真实Windows10/11图片拖拽/系统文件选取、JPEG/WebP桌面实际解码、多屏DPI、断电磁盘满和长期负载需要最终实机验收；当前PNG和类型测试是合成测试。
- 图像与角色跨电脑 ZIP 导入导出、ZIP资源完整性及路径穿越防护属于第15步。发布冻结/启动前权限与素材预检属于第16步。
- Signal MS002及现有生产Cloudflare卡密后端未修改。

Step14=done_dev，Step15=not_started；进度14/32=43.75%，剩余18步。
