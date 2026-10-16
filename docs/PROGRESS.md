# 进度流水
日期：2026-10-16。仓库：`mosen6266-netizen/MS_001`。
总计 8 阶段 / 32 步；**本次只建立规划基线和开始步骤 01 的文档工作，没有开始功能代码编写。**
- 步骤 01：`in_progress`；用户已确认使用该 GitHub 仓库并已提供 icon WEBP。
- 完成开发步骤：0 / 32；未进行 Windows/TDLib 编译、登录、群发送、压力测试。
- 已确定 P0：严格先等待、再输入、然后发送；30秒+5秒=35秒（加网络耗时）。
- 已知后续阻断：步骤02 TDLib/Windows 实机验证需要开发/测试环境；生产使用需单独授权；用户 Logo 透明及 Telegram 商标合规检查；Cloudflare 独立资源确认。
## 更新模板
### Step XX
- 状态（not_started/in_progress/blocked/implemented_unverified/verified/done）：
- 代码/文档变更、文件路径：
- GitHub commit/PR：
- 执行测试命令与结果：
- 未运行的测试及原因：
- 原始日志/截图/性能报告：
- 缺陷与风险：
- 下一步交接条件：

## 2026-10-16｜初始规划与图标交付核查
- GitHub 初始化提交：`9018380a32193875417c38d562b5dc1f1cfcbd03`。
- 32 步主计划及 14 个规划文档基线：`381cdc24898ed34f7d8a25f7ad99cdb489d2dd20`；已远程读取核查计划包含全部32步。
- 用户提供图标转换为透明 SVG：`2f5f75a10096ae37b0e5af1696f030b3aa447fa4`，路径 `assets/branding/icon.svg`。
- 本地图标构建脚本：`7f70491032a97234f11afce6f364eac42398a0b9`，路径 `tools/branding/build_icons.py`。
- 计时顺序已核查：先等30秒，后输入5秒，再调用消息发送，不重叠。对应功能代码尚未编写，自动化测试尚未运行。
- 本次为步骤01文档工作，开发阶段完成数仍为 **0/32**，没有发 Telegram 消息，也没有操作 Cloudflare 或旧 MS002。

## 用户新增进度汇报要求（2026-10-16）
- 每完成一步，必须给用户展示进度条和 `已完成 X/32 | 百分比 X/32×100% | 当前/下一步 | 剩余 32-X`。
- 目前：`0/32` 已验收，`0%`，第 01 步进行中，剩余 32 步。
- 本次新增的是汇报规则，**不代表完成第 01 步或任何开发功能**。

## Step 01 — 需求与许可范围冻结（2026-10-16）
- 状态：`done`（完成的是文档步骤，**不是软件功能完成**）；下一步：`02 not_started`。
- 交付：`docs/REQUIREMENTS_MATRIX.md`（15项P0、7项P1、3项P2），`docs/NON_GOALS.md`、`docs/STEP_01_ACCEPTANCE.md`；同步更新产品基线、决策文件、进度状态与首页。
- 测试：静态文档检查（32步、ID唯一、P0/P1/P2齐全、关键时间顺序、Win10风险及MS002隔离）通过；真实登录/消息发送/Win构建/容量压力测试均 **NOT_RUN**。
- 实施：仅向MS_001写入文档和状态，无生产部署、无Telegram真实登录与发消息、无MS002修改。
- 关键留项：MS_001目前public；图标/产品名的商标合规审查属于正式发布阻断项；300/50实测属于后续技术验证与容量测试。
- 进度条：█░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ 1/32 = 3.125%，剩余31步；每完成下一步必须继续报告并更新GitHub。
- 代码提交：见本步骤 GitHub 当前提交历史；第二次提交将补充首个提交SHA以避免自引用。

- 步骤 01 基线提交 SHA：`15c60ce77bba52494004168081f62f4758292fbb`；2026-10-16 已从GitHub回读核对 `state/project_state.json`、`state/steps.json`、需求矩阵、非目标及验收文件，结果一致。

## Step 02 — 技术验证启动及仓库结构恢复（2026-10-16）
- 发现历史 Git 提交构造 tree 时未设置 base_tree_sha，导致新 main 分支意外缺少规格文件。已以 fast-forward 新提交恢复全部文档，保留 Step01 最新状态；修复提交：`c1561ca471a79546acdcbbf857e1b6039a336391`。
- 修复规则：今后若使用 Git Data API 创建 tree，必须提供 `base_tree_sha`；提交后必须重新读取 main 的完整文件树，检查规划文件与进度文件仍全部存在。否则不得推进步骤。
- Step02 当前标记 in_progress；尚未通过 Win10/11 单账号真实授权 / WPF构建 / TDLib native DLL 验收。

### Step 02 — isolated smoke tests & CI (2026-10-16)
- 修复仓库完整性：main 分支恢复历史提交中遗漏的文档，修复提交 `c1561ca471a79546acdcbbf857e1b6039a336391`；新提交全部基于完整 base tree，不再全量清空。
- 新增可复现模拟队列：300条虚拟账号资料 + 50独立任务队列 + 10,000条虚拟事件，Linux 本地测试成功：0.16659秒，Python tracemalloc峰值0.1878MiB；这不是TDLib内存基准。
- Windows WPF + WebView2 Smoke 工程源码、最新 TDLib JSON ABI 的 C# 声明和 GitHub Actions 已上传；CI成功: https://github.com/mosen6266-netizen/MS_001/actions/runs/38919353228，Windows runner `dotnet build` 成功且 Linux Python 测试通过。
- 最初 CI 失败 `https://github.com/mosen6266-netizen/MS_001/actions/runs/38919305431`；修复 DllImport UTF-8 参数声明后通过；记录了失败原因，不把初版失败隐藏。
- **状态 blocked（Step02未验收）**：尚缺 Windows 10/11 可交互实机验证、TDLib原生DLL加载、本人授权账号的单次登录/重启恢复、实际CPU/RAM/句柄数据。不能以云端编译或模拟300个对象替代。
- **进度维持：1/32（3.125%），剩余31步**；明确需要本人的Windows测试环境和对自有测试账号的安全授权，云端GitHub不保存任何凭据。

### Windows 测试包已生成（2026-10-16）
- GitHub Windows CI 成功打包可独立运行的测试程序，`MS001-Step02-Windows-x64-Smoke`，在 [CI run 38919854974](https://github.com/mosen6266-netizen/MS_001/actions/runs/38919854974) 的 Artifacts 区域可获取；GitHub Artifact ID `12053528530`，已核实存在。
- 注意：尚无人实际在 Win10/11 客户端启动该程序，且此包不包含任何 Telegram 登录、群消息发送功能。
- 在取得本机测试证据前进度不前进：`1/32`（3.125%），Step02 blocked。

## 2026-10-16 — 用户要求实机测试延后，转入第03步
- 用户明确表示不需要中途本机测试；待全部正式版开发完成后自己安装统一测试。
- Step02 云端 Windows 构建（Actions 38919353228 / 38919854974）、打包和 Python 300/50 模拟均通过，技术风险及限制已记录。
- Step02 标记 `done_dev`（**仅开发/CI结束**），Win10/11 GUI启动、tdjson native加载、真实单账号登录/重启及性能指标转移到最终统一验收，状态 `DEFERRED`，不是 PASS。
- Step03 基础工程与持续集成开始，进度 **2/32 = 6.25%**，剩余30步；仅统计开发完成比例。

## Step 03 — 基础工程及CI开发验收（2026-10-16）
- 实际新增19个工程/CI文件；WPF/WebView2 本地宿主、单实例保护、React/TypeScript业务页骨架、有限IPC、严格CSP以及CI管线；无Telegram实际发送。
- npm依赖锁文件已从GitHub构建产物生成并提交主仓库，lockfileVersion=3，238个packaged entries；常规CI使用npm ci，临时写入权限工作流已删除。
- [CI 38921641970](https://github.com/mosen6266-netizen/MS_001/actions/runs/38921641970)：UI job成功（TypeScript lint、Vitest IPC test、Vite build），Windows job成功（编译和win-x64自包含publish）。
- Windows原生UI启动/TDLib实机和性能验证依用户要求延后；文档 `docs/STEP_03_ACCEPTANCE.md` 说明，禁止把CI当成正式版验收。
- **Step03 done_dev，当前下一步04（数据合同与迁移框架），开发进度3/32 = 9.375%，剩余29步**。

## Step04 — 数据合同与事务迁移开发验收（2026-10-16）
- 完成：SQLite v1/v2 表结构、版本化剧本合同、迁移hash校验、WAL/FULL、事务与异常回滚、升级前备份、恢复候选数据库、自动化测试。
- 修复：首次NuGet源包含高危版本，被TreatWarningsAsErrors阻止；升级SQLite依赖至10.0.13后全套CI通过，未关闭安全检查。
- [CI 38922965210](https://github.com/mosen6266-netizen/MS_001/actions/runs/38922965210) **SUCCESS**：Linux/Windows各27项测试、UI lint/test/build与Windows WPF构建发布均成功。
- **真实物理断电/本机安装、会话数据库加密和Windows10/11硬件行为仍DEFERRED**，模拟测试不代替真实环境。
- 第04步状态 `done_dev`，下一步 `05 not_started`，进度 **4/32 = 12.5%**（剩余28步）。详细证据见 `docs/STEP_04_ACCEPTANCE.md`。

## Step05 — Windows 主窗口容器与导航框架（2026-10-16）
- 完成单文档状态路由、6个主导航页、Alt+1–6快捷键，固定侧栏/内容独立滚动；导航不触发整页刷新。
- Windows 主窗体实现启动暗色遮罩、离线资源可信域名导航保护、首次初始化单次执行、关闭WebView2清理与窗口几何原子保存；损坏窗口配置自动纠正。
- [CI 38924326676](https://github.com/mosen6266-netizen/MS_001/actions/runs/38924326676) **SUCCESS**；前端8项测试通过，几何Windows/Linux各10项，原数据库回归及Windows编译通过。对“无视觉闪烁”的结论只限设计与SSR，真机视觉测试留到用户统一验收。
- **状态 done_dev，下一步06待开始，开发进度 5/32 = 15.625%，剩余27步**；真实运行、DPI与多屏切换仍DEFERRED。

## Step06 — 深色 UI 设计系统（2026-10-16）
- 已交付可复用的 dark CSS tokens、Button/Field/Select/Status/Dialog/Toast 组件；标准按钮左绿确认/右红取消，Esc/Tab焦点控制，提示单一展示槽；设置页提供离线交互演示。
- 用户提供的图标 SVG 已接入软件导航区；品牌发布合规仍待正式审核。
- Chromium 截图测试第一次发现 Linux 缺中文字体出现方框，补齐 Noto CJK 字体重新生成宽1440/窄700截图，人工复查中文已正确显示；只是初始工作台，后续需完整浏览器/UI交互测试。
- [CI 38926182509](https://github.com/mosen6266-netizen/MS_001/actions/runs/38926182509) 7个作业**SUCCESS**；UI15项、静态设计16项、Windows/Linux各10项几何及27项数据库测试、Windows x64云端编译均通过。
- 结论：`done_dev`，真机 WebView2/DPI/模态键盘交互与正式发布审查 `DEFERRED`；下一步 **07**，开发进度 **6/32（18.75%），剩余26步**。详见 `docs/STEP_06_ACCEPTANCE.md`。

## Step07 — 设置、搜索与状态管理（2026-10-16）
- 提交 SQLite 非敏感设置持久化、revision 冲突保护、WPF白名单异步IPC、React设置编辑页与搜索防抖150–250ms、300条合成数据索引、每页不超过50条、1000次提交防重锁和测试。
- [CI 38927977333](https://github.com/mosen6266-netizen/MS_001/actions/runs/38927977333) 完整9项job SUCCESS；前端22项，Windows/Linux各16项设置测试，数据库/窗口历史回归均通过，Windows编译与Chromium初始页截图通过。
- 修复过首轮 UI 未使用导入引发 lint 失败、双击跳过后误报保存成功；全部有代码和复测记录。
- **未实测的真正Windows用户操作、群账号接入及磁盘故障标记DEFERRED**，不会冒充已真实登录/发送。
- Step07 `done_dev`，Step08待开始；进度 **7/32（21.875%）**，剩余25步。详见 `docs/STEP_07_ACCEPTANCE.md`。

## Step08 — 本地安全与脱敏诊断开发验收（2026-10-16）
- 完成Windows CurrentUser DPAPI保护原语、WebView2来源+严格IPC JSON白名单、固定结构诊断日志/轮转留存、健康快照、崩溃/初始化事件类别记录与设置页只读诊断。
- [GitHub Actions CI 38930950275](https://github.com/mosen6266-netizen/MS_001/actions/runs/38930950275) **11个job SUCCESS**；Windows安全26项、Linux安全24项；React24项单测、CSS16项、已有SQLite/设置/窗口回归及Windows桌面编译全部通过。
- 修复前端诊断IPC白名单遗漏/前期导入组件时序导致CI红灯，并将日志测试改为真正触发滚动的故障校验；逐次复测，没有关闭警告或测试。
- **安全界限**：尚未接入真实TDLib会话，DPAPI只是可信本地保护原语；Windows10/11多账户/ACL/磁盘故障/崩溃转储与账号/群操作均 DEFERRED。
- 完整记录 `docs/STEP_08_ACCEPTANCE.md`、`docs/STEP_08_SECURITY.md`、`docs/SECURITY_THREAT_MODEL.md`；**开发Step08 done_dev，Step09待开始；进度8/32=25%，剩余24步**。

## Step09 — TDLib JSON 适配器开发/CI验收（2026-10-16）
- 官方JSON ABI的Native P/Invoke接口、进程单一接收/按序更新、有界背压与异步请求 `@extra + @client_id` 双重匹配、超时取消与关闭限时等待机制提交完成。
- 固定官方1.8.67源代码提交 `bc9c263e2bfee06aaab41e82db51a103376030bc`；**尚未获得/构建原生 tdjson.dll，不存在二进制摘要或真实 DLL 加载测试**。原生构建/版本安全审查/安装集成仍是后续强制门槛。
- [CI 38933092843](https://github.com/mosen6266-netizen/MS_001/actions/runs/38933092843) 13个jobs **SUCCESS**；Windows/Linux模拟TDLib各118项断言通过，UI+数据库/诊断安全/窗口回归与Windows编译通过；测试用的是fake transport，不涉及真实账号/消息。
- 修复C#测试的raw字符串大括号与数组类型编译错误后复测，同时修正原生接收必须在首个请求发送后启动的生命周期要求。
- Step09 `done_dev`（仅源码与模拟验证）、Step10待开始，开发进度 **9/32（28.125%）**，剩余23步。详情见 `docs/STEP_09_ACCEPTANCE.md`。

## Step10 — 登录与身份验证状态机开发/CI验收（2026-10-16）
- 完成13种官方 TDLib 授权状态的中文说明/临时QR内存链接、严格按clientId隔离/按状态限制操作、验证码/邮箱/2FA/注册条款/Premium拦截、成功只接受Telegram Ready更新、取消防迟到授权恢复。
- 官方TDLib登录JSON白名单请求、二维码状态刷新、`close`操作去重；账号页面内提供清楚标记为模拟、**不支持实际登录**的中文状态预览。
- [CI Run 38935436769](https://github.com/mosen6266-netizen/MS_001/actions/runs/38935436769) **15个job全部成功**，Windows+Linux各94项认证模拟测试，前端26项、设计16项及第01–09步Windows编译/回归全部通过。
- 修复C#测试初始化语法错误，额外加入取消与全局关闭不会重复`close`的保护并复测；真实 TDLib win-x64 DLL尚未构建与载入，用户账号/本机测试按约定DEFERRED。
- 结论 **Step10 done_dev**；下一步 **11 — 账号注册表及会话生命周期**，开发进度 **10/32（31.25%）**，剩余22步。完整证据 `docs/STEP_10_ACCEPTANCE.md`。

## Step11 — 账号注册表与会话生命周期（2026-10-16）
- SQLite v3账号元数据注册表：300条容量、中文备注/分组CRUD、revision乐观冲突、verified user ID去重、归档及恢复；旧非GUID ID可只读显示但禁止拼接会话目录。
- Windows CurrentUser DPAPI独立随机TDLib DB key、每账号分离目录/不覆盖历史key、可清零密钥租约；SessionSupervisor默认16个按需活动槽位，授权未确认不称Ready，关闭不确定要人工审查。
- WebView2仅允许本地账号元数据 CRUD，账号管理页接入真实本地SQLite列表而非模拟账号，可搜索/分页、按备注二次确认归档和查看已归档恢复。无native TDLib账号登录/发消息功能。
- [GitHub Actions 38940132317](https://github.com/mosen6266-netizen/MS_001/actions/runs/38940132317) **17/17 SUCCESS**；Windows/Linux各48项注册表/会话模拟断言，前端29项、设计16项，SQLite迁移、安全、WPF打包和原有CI全部通过。
- 修复C# SQL多行字符串语法、TS类型收缩和Unicode控制字符验证、弹窗二次确认，并增补归档恢复后重跑完整CI。
- 真实Win10/11、TDLib原生DLL、账号密钥完整生命周期/免重扫/72小时压测延至最终本机验收；**Step11 done_dev，下一步12，开发进度11/32（34.375%），剩余21步**。详见 `docs/STEP_11_ACCEPTANCE.md`。

## Step12 — Telegram 群组、话题与账号权限目录（2026-10-16）
- SQLite v4加官方trusted来源、forum话题关闭状态及单账号成员权限快照；群名更新由`chat_id`定位，保留剧本目标绑定；权限撤销/话题关闭的时间戳单调处理、权限默认拒绝。
- 官方形状TDLib chat/topic/chatMember/supergroupFullInfo解析及本人userID核验；`GroupPermissionPolicy.Preflight`拒绝未知/过期/被封禁/无权限/话题关闭，同时要求本次开始时重新从Telegram检查并接受慢速模式调度。
- 只读中文群组界面，按名称搜索分页、查看话题/账号文字图片权限与slow mode，后端不接受伪造群组/任意发送IPC；没有真实原生会话时列表为空而非假装在线。
- [GitHub Actions 38942859853](https://github.com/mosen6266-netizen/MS_001/actions/runs/38942859853) **19/19 SUCCESS**；Windows/Linux各47个GroupCatalog模拟断言、UI32/32、CSS16/16，数据库迁移/备份、安全/账号生命周期与Windows编译发布回归全部通过。
- 解决初次C#测试语法、前端TopicPage状态类型、SSR初始加载测试不匹配并加入成员ID安全边界，最终全量CI全绿。
- `docs/STEP_12_ACCEPTANCE.md` 为详尽验收；真实TDLib DLL、账号登录、官方群/话题同步、实时权限刷新/50群实操仍**DEFERRED**。**Step12 done_dev；下一步Step13可视化剧本编辑器；开发进度12/32=37.5%，剩余20步。**

## Step13 — 可视化剧本编辑器（2026-10-16）
- v5 SQLite独立草稿表、版本CAS、保存/复制/确认归档/恢复；角色保存`roleKey/roleLabel`，不保存手机号/本机账号ID/API哈希/会话；中文英文emoji换行及草稿备份，目录元信息按需读取。
- 中文可视剧本编辑页：从已登记的备注下拉选发言角色、消息卡片/多选/复制/拖拽/上下移动/撤销重做/搜索/分页，0.7秒自动保存和手动保存，切换剧本前flush，当前文字输入不重算整份大型剧本。
- [完整CI 38946517290](https://github.com/mosen6266-netizen/MS_001/actions/runs/38946517290) **21/21 SUCCESS**：Windows/Linux各24项脚本草稿测试、UI40/40、安全Windows35/35及Linux33/33、WPF编译发布/SQLite迁移/账号和TDLib合成回归全绿。
- 修复SSR双侧栏、类型/编译和首屏异步测试不一致、无用C#变量和快捷键作用范围；全部复测。**UI页面未做Win10/11实机拖拽E2E或真正Telegram授权发送，DEFERRED。**
- 三段时间编辑及素材是Step14、便携导入导出是Step15、发布预检是Step16；但时间语义仍维持`pre30s + typing5s => 35s后发送`。验收记录`docs/STEP_13_ACCEPTANCE.md`。
- Step13 `done_dev`，下一步14，开发进度 **13/32（40.625%）**，剩余19步。

## Step14 — 时间及图片编辑（2026-10-16）
- 剧本默认/逐条/按字段批量修改三段等待；30s发送前等待、5s正在输入，累计35s到达发送阶段，后等待从收到模拟回执算起。只在预览中计算，不执行Telegram发送。
- PNG/JPEG/WebP本地5MiB每张，128MiB额度，SHA256地址去重、文件名消毒、预览哈希校验；支持图片添加、拖拽、预览、替换、移除与图文共存，剧本只记录hash等便携元数据。
- [完整CI 38949689786](https://github.com/mosen6266-netizen/MS_001/actions/runs/38949689786) 全23个作业通过：Windows/Linux TimingMedia各20、Editor各29、前端45/45、安全Win42/Linux40、WPF打包与原有CI回归成功。
- 修复旧毫秒时间、GUID匹配、默认时间漏记修改、媒体路径过滤与测试字符串。无绕过失败检查；真实TDLib、Telegram发送/typing及Win10/11实机验收仍DEFERRED。
- Step14 done_dev；第15步为角色备注移植与剧本格式。开发进度14/32（43.75%），剩余18步。详见docs/STEP_14_ACCEPTANCE.md。

## Step15 — 角色备注移植与 .mosentgscript ZIP（2026-10-16）
- **开发阶段状态 done_dev**；实际 Windows 用户交互与 TDLib 真实 Telegram 登录/发送仍为 DEFERRED。没有进行任何正式部署，MS002 及生产卡密系统未修改。
- 实现并提交内容：`src/Features/Scripts/Portable/PortableScriptPackage.cs` 和 `RoleRemarkResolver.cs`、Windows 原生打开/保存对话框 `apps/desktop/MainWindow.Portable.cs`、编辑器“导入/导出 .mosentgscript”按钮，严格IPC控制以及 `EditorDraftRepository.CreateDocument` 原子创建草稿。
- ZIP清单为严格白名单：`format/version/document`；仅存 UUID 角色键与自定义备注、正文、3段独立毫秒时间和受限图片元信息；PNG/JPEG/WebP 按SHA-256去重打包。无账号ID、手机号、会话、聊天令牌或设备专有路径。防目录穿越、同名ZIP条目、额外文件、图片篡改及超量解压。导入只创建新草稿，不覆盖原有草稿。
- 跨设备：A三角色、B多出一个账号不影响精确匹配；备注比较采用Unicode NFC+trim；所有缺失角色逐一提示，同名重复角色不得随机绑定，群内权限候选过滤和真正任务启动冻结留到Step16。
- 自动化测试：独立Portable测试 **Linux 18/18，Windows 18/18**；完整 [GitHub Actions 38954528584](https://github.com/mosen6266-netizen/MS_001/actions/runs/38954528584) **25/25 SUCCESS**；UI测试与设计、所有SQLite/安全/TDLib mock、Windows x64 WPF构建发布、Chromium截图回归均通过。
- 源码功能提交：[`2ad3feeb`](https://github.com/mosen6266-netizen/MS_001/commit/2ad3feebba6b5e08bfe412575a6269ffb4e1fd6a)，SSR bug修复提交：[`6ba635d7`](https://github.com/mosen6266-netizen/MS_001/commit/6ba635d7e5285963f9000905305689d56d940c23)；本验收/进度提交在后续git历史中。
- 开发进度：**15/32=46.875%，剩余17步**。下一步 **Step16 发布快照、预览和启动预检**。Windows10/11原生对话框、跨电脑真实用户及群权限真实测试由用户最终统一验收。

## Step16 — 发布快照、预览和启动前检查（2026-10-16）
- **开发阶段验收 done_dev**：SQLite v6新增 `published_script_snapshots` 事务表，发布版本保留 SHA256 和源草稿修订，保存角色UUID、三段时间、媒体引用及正文；修改草稿、复制草稿不会污染已发布版本，旧版本仍可读取与校验。
- 新增 `src/Features/Scripts/Preview`：逐条角色、内容、媒体与HTTP(S)链接实体、UTF16偏移、独立三段时间，30秒前等待+5秒正在输入=35秒最早进入发送阶段；网络耗时不虚报为0。
- `src/Core/Preflight`：逐群/话题判断已验证群、关闭/未选话题、重复任务、图片校验、缺失所有角色备注、即时授权及群成员权限、重复备注人工指派；全部无误才生成按群冻结的绑定与步骤。任何问题阻止运行。
- 本地桌面端发布/版本列表/预览/预检IPC与编辑器 UI 均已接入，缺失权限会逐项显示。**真实 TDLib 授权尚未接入，客户端强制 `LiveAuthorized=false`、`MembershipsFreshForStart=false`，因此不能以缓存数据开始发送，当前不提供真实“启动任务”指令**。
- 初次 CI 发现历史数据库 schema v5 固定断言导致6个旧模块作业失败，已升级3套旧回归测试到v6；前端TypeScript `unknown` 列表类型问题也已修复。新C#模拟预检30断言 Linux/Windows 各通过，旧模块全量回归通过。
- [CI 38956311705](https://github.com/mosen6266-netizen/MS_001/actions/runs/38956311705) **27/27 SUCCESS**，包含前端lint/test/Vite、Chromium截图、Linux/Windows全部安全/数据库/角色/群模拟测试、Windows WPF编译及win-x64自包含发布；源码最后修复提交 [`b6d6296`](https://github.com/mosen6266-netizen/MS_001/commit/b6d6296896ee6fff95331cbfb65a97b413c0adc8)；验收文档见 `docs/STEP_16_ACCEPTANCE.md`。
- **仍DEFERRED**：Windows10/11真正可交互界面、合法Telegram账号的TDLib原生实时登录/群权限刷新、多群真实任务启动、资源压测。MS002与生产Cloudflare授权无任何修改。
- 当前进度 **16/32=50%，剩余16步，下一步17“任务状态机与50群调度”**。

## Step17 — 50群独立任务状态机与虚拟时钟调度（2026-10-16）
- **开发完成 done_dev**，支持每群独立 Ready/Running/Paused/Review/Completed/Stopped/Failed 状态；`src/Core/StateMachine` 对每条消息严格实现发送前等待→正在输入→等待外部提交→等待外部回执→发送后等待。30+5=35秒不重叠，12秒后等待由确认后开始；零耗时阶段安全转换。
- `src/Core/Scheduler/FairScheduler.cs` 使用轮转首群，每次手动 Tick 最多对每个活动群推进一个非零耗时阶段；50群模拟都公平取得执行机会。暂停保留精确剩余毫秒，恢复不会计入暂停时的墙上时间。某群失败/进入人工复核，不影响其他群。
- `scheduler_simulation_jobs` 是 SQLite v7 单独模拟表，所有任务与账号/步骤关系存检查点（正文仍位于发布版本），原子批量创建、乐观版本控制、全局原子停止。重启后 **Ready/Running/Paused 强制 Review**，没有任何自动重试、后台定时器或真实Telegram出站命令。
- 回归旧数据库 v6→v7 后，Windows/Linux `tests/Scheduler/Program.cs` **各 37/37 PASS**；完整 [CI 38957627884](https://github.com/mosen6266-netizen/MS_001/actions/runs/38957627884) **29/29 SUCCESS**，包含原有安全/授权/SQLite/媒体与可视界面、Windows x64 自包含桌面编译发布。
- 源码提交 [`8bf4e702`](https://github.com/mosen6266-netizen/MS_001/commit/8bf4e7028aec53b0449e05f4134f1aefddeb176b)，设计与证据见 `docs/STEP_17_SCHEDULER.md`、`docs/STEP_17_TEST_MATRIX.md`、`docs/STEP_17_ACCEPTANCE.md`。
- **仍 DEFERRED**：真实 Telegram 原生账号授权/群实时权限、正式 `runs/outbox` 持久化与实际发送、Windows10/11控制界面、50 群/300会话持续压测，预计按用户指示最终集中实机测试。此步骤没有启动自动群发功能，不能等同可用正式版。
- 进度 **17/32 = 53.125%**；剩余 **15步**，下一步 **18 账号与群发送规则协调**。

## Step18 — 账号级出站规则协调与频率预算（2026-10-16）
- **开发阶段 done_dev**：新增 `src/Core/OutboundPolicy/OutboundCoordinator.cs`，可在第17步 `Running/AwaitingSend` 状态为冻结的账号、群和话题签发仅供模拟的单次许可；没有真实Telegram网络发送/TDLib连接或后台出站线程。
- 安全约束：单账号全群串行（包括50个并发群）、30秒内的即时授权验证、每次发送权限刷新、官方群/话题和对应消息文字/照片权限、不可重绑定角色。未知回执持有账号锁等待人工复核，不能当做失败盲目重试。
- 保守预算：默认每账号出站许可间隔3s、群级间隔3s、账号一分钟最多6次，不得降低安全底限；按官方群速模式和服务端账号 FloodWait 延长阻断期限，保护受影响群防止通过更换账号绕过限制，不能用短等待覆盖更长等待。
- 首次CI发现“未知回执阻止重发”的合成测试复用了过期授权而被错误的安全门先拦截，已修正合成授权时间。修正后 Windows/Linux `tests/Policy/Program.cs` 各 **62/62 PASS**；完整 [GitHub Actions 38958656142](https://github.com/mosen6266-netizen/MS_001/actions/runs/38958656142) **30/30 SUCCESS**，覆盖原有群/会话、预检、调度、SQLite、UI和安全回归。
- 源码主要提交 [`bf20488a`](https://github.com/mosen6266-netizen/MS_001/commit/bf20488ab799e1cf129ad2ec112a37c4440e5e4a)；测试修正 [`b1ac1e75`](https://github.com/mosen6266-netizen/MS_001/commit/b1ac1e7525c57f31b87a29672af7b3166156df3e)。规则与验收记录：`docs/STEP_18_OUTBOUND_POLICY.md`、`docs/STEP_18_TEST_MATRIX.md`、`docs/STEP_18_ACCEPTANCE.md`。
- **DEFERRED**：实时TDLib授权/权限事实提供者、官方服务器返回错误码与冻结后的动态权限对接、跨重启正式发送速率预算、真实 outbox 与回执状态、Windows10/11正式客户端、合法群实际发送/50群长稳。不得声称正式发送功能可用；MS002及生产Cloudflare服务未修改。
- 开发进度 **18/32（56.25%），剩余14步**；下一步 **19：发送回执和去重保护**。

## Step19 — 持久消息回执与事务去重（2026-10-16）
- **开发阶段完成 done_dev**：SQLite v8 新增 `delivery_attempts` / `delivery_events`。每条冻结的运行+消息步骤只允许一笔投递事务，许可与冻结账号/群/话题/步键严格关联；先记录 Planned，再登记 Submitting，传输确认 Pending 不等于 Telegram 确认。
- **官方可信回执模型**：检查相关令牌、账号、群、话题、来源真实性及平台消息 ID，精确匹配才写 Confirmed。重复回执幂等，矛盾回执、跨账户消息复用或迟到 Pending 不会冲掉已确认结果；确认先于 Pending 到达也可安全关联。
- **错误分类与恢复**：超时、断线、未经证实的响应为 Unknown/Review；仅平台可信明示未送达可 Failed；同一步永久禁止盲目重新发送。重启 Planned→Review、Submitting/Pending→Unknown，真正 Confirmed 保留。纯读取状态/权限可安全重试，消息、照片提交没有自动重试入口。
- 首轮新增测试因同一 `var` 语句声明多个变量触发编译错误，真实修复并重新跑全部测试。Windows/Linux `tests/Delivery/Program.cs` **各64/64通过**，完整 [CI 38960129236](https://github.com/mosen6266-netizen/MS_001/actions/runs/38960129236) **30/30 SUCCESS**，包含旧数据库 v8、预检、50群调度、出站规则、账号安全和 UI 回归。
- 源码提交 [`dcb242c1`](https://github.com/mosen6266-netizen/MS_001/commit/dcb242c1e1d67699ee649c394542d5031a5b5515)；测试编译修正 [`a4f44951`](https://github.com/mosen6266-netizen/MS_001/commit/a4f44951461ccb56ca211c229d354093c0023196)。设计、测试矩阵和验收见 `docs/STEP_19_DELIVERY.md`、`docs/STEP_19_TEST_MATRIX.md`、`docs/STEP_19_ACCEPTANCE.md`。
- **DEFERRED**：真实可信TDLib回执供应者、实时授权/群级权限及账号锁释放的集成、正式生产outbox、Windows10/11实机断网/崩溃及合法群发送、50群长稳；模块目前纯离线，不会真实向Telegram发消息，MS002保持不动。
- **开发进度 19/32 = 59.375%，剩余13步**；下一步 **20：断网、崩溃与重启恢复**。

## Step20 — 开发执行中（CI 未验收）
- 代码已提交：`src/Core/Recovery/`、SQLite v9 损坏记录非破坏隔离、`tests/Recovery/Program.cs`、断网/睡眠/重启的只读恢复报告；当前源码提交 `ba4a0bdc`。
- GitHub CI [38961738009](https://github.com/mosen6266-netizen/MS_001/actions/runs/38961738009) 等待完整执行（运行器队列）。**没有全部通过前不得标为 done_dev，进度仍19/32**。
- 真实 Win10/11 系统网络断开/休眠强退、官方 TDLib 会话与实际发送集成均 DEFERRED，不代表实机验收。

## Step20 — 断网、崩溃、休眠与重启安全恢复（2026-10-16，开发阶段验收）
- **done_dev**：`src/Core/Recovery` 新增安全启动扫描与故障协调。SQLite v9 `recovery_quarantine` 按来源/主键记录损坏任务与投递记录，保留原始数据供人工修复且从可执行/待发送枚举排除。未损坏的其余任务照常读取并进入人工复核。
- 网络断开、电脑休眠与进程退出时先关闭第18步模拟出站许可，再原子将未结束群任务置 `Review`；第19步 `Planned→Review`、`Submitting/Pending→Unknown`，`Confirmed` 不变。恢复网络及电脑唤醒后仍不自动继续执行，必须重新授权与复核。
- 通过原子隔离与完整审计保留防止损坏一行拖垮整个数据库的恢复读取；跨重启重复隔离不重写原始记录，不产生重复任务。迟到但可信且完整关联的模拟官方回执仍可以确认 `Unknown`。
- CI [38961738009](https://github.com/mosen6266-netizen/MS_001/actions/runs/38961738009) **全部成功**；新增 `tests/Recovery/Program.cs` 在 Windows 与 Linux **各47/47项通过**，同时通过所有既有数据库、群权限、50群调度、发送策略、回执保护、WPF和 UI 回归。
- 开发源码提交 [`ba4a0bdc`](https://github.com/mosen6266-netizen/MS_001/commit/ba4a0bdc231162e1fb733571992c6e427e7b021a)，代码、验证矩阵及已知限制见 `docs/STEP_20_RECOVERY.md`、`docs/STEP_20_TEST_MATRIX.md`、`docs/STEP_20_ACCEPTANCE.md`。
- **DEFERRED**：Win10/11 原生生命周期真实接线、真断网/休眠/强退、TDLib账号实时状态、50群/72小时实机长稳。当前为离线演练，无实际 Telegram 出站或后台发送功能，Signal MS002 不受影响。
- **开发进度20/32（62.5%），剩余12步，下一步21运行中心与实时状态。**

## Step21 — 运行中心与50群实时状态展示（2026-10-16）
- **开发阶段 done_dev**。`src/Features/RuntimeDashboard/` 基于第17步模拟任务快照、第19步投递状态及第20步损坏隔离统计形成**真实本地状态只读**模型：完成率、下一条、时间锚点、模拟运行、暂停、失败和未知结果显式分类。既不自动创建任务，也不读取/显示 Telegram 私密消息正文。
- Windows WebView2 IPC 新增严格白名单 **`runtime.snapshot`**（无参数、只读，任何发送/恢复命令仍被拒绝），读取SQLite快照时不调用会改变任务状态的启动恢复方法。
- `apps/ui/src/components/RuntimeDashboardPanel.tsx` 已替换运行中心占位页，提供50任务10条/页、6类状态筛选、群/任务ID搜索、优先显示异常、进度条、单条倒计时、已读回执/人工复核/隔离记录提醒。**仅一个共享1秒计时器，每5秒同步一次，只在此页面挂载**；浏览器无桌面环境时显式显示预览空状态，绝不伪装真实群发中。
- 自动化：`tests/UI/Dashboard/Program.cs` Windows/Linux 各 **23/23 PASS**，包含90轮本地SQLite只读刷新；React/Vitest `runtime-dashboard/model.test.tsx` 7项测试覆盖50群分页、搜索、异常、倒计时及2000次纯分页运算。最终 [CI 38963263363](https://github.com/mosen6266-netizen/MS_001/actions/runs/38963263363) **37/37 成功**，含WPF、账号/群/剧本/恢复/调度全量回归。
- 初始前端 SSR 预览空态在 CI 发现文案预期错误，修复后重新运行全量 CI 通过。主源码提交 [`4b1580b1`](https://github.com/mosen6266-netizen/MS_001/commit/4b1580b1e534846b3f17cd491dceda3460c05412)，测试预览修正 [`e330b85f`](https://github.com/mosen6266-netizen/MS_001/commit/e330b85f3f8ea4eb3fccb5329e695be9046f7e27)；交付与限制见 `docs/STEP_21_RUNTIME_DASHBOARD.md`、`docs/STEP_21_TEST_MATRIX.md`、`docs/STEP_21_ACCEPTANCE.md`。
- **DEFERRED**：真实 TDLib 发送/官方回执数据源、Windows10/11实机交互与真实50群高频更新、72小时长稳测试。当前数据为离线模拟记录，并非真实 Telegram 运行证明。MS002、现有卡密后台无更改。
- 开发进度 **21/32（65.625%），剩余11步**；下一步 **22：Windows提醒与紧急停止**。

## Step22 — Windows异常提醒、托盘与全局紧急停止（2026-10-16）
- **开发阶段 done_dev**：`src/Desktop/Notifications/` 实现脱敏告警历史、时间窗口内同键去重（带发生次数、最多500条）、原子本地历史JSON保存；不记录 Telegram 消息文本、手机号、登录凭据或原始异常堆栈。
- 原生 Windows WPF 顶部增加紧急停止与历史查看；系统托盘可打开主窗体、查看异常历史、停止及退出；严重异常复用一个始终置顶的独立提醒窗口，主窗体最小化仍能看到，托盘气泡20秒批量节流避免多错误刷屏。**确认按钮在左（绿）、取消在右（红）**。关闭程序销毁托盘、窗口与计时器，不留后台继续执行。
- **安全停止顺序**：立即关闭本地出站许可 → 原子记录 `emergency-stop.latch` → 50个模拟任务经SQLite单事务停止（含重启识别闩锁）；磁盘失败明确显示并保持当前进程许可封锁，禁止将部分失败误认作全面成功。当前无正式 TDLib 消息发送入口，不宣称已控制真实官方发送。
- 自动化：跨平台 `tests/Notifications/Program.cs` Windows/Linux **各38项断言通过**，并检查200并发错误合并、1000错误容量、持久历史、50群停止、重复触发与磁盘失败。Windows完整 WPF x64 实际构建及发布通过。完整 [CI 38966422700](https://github.com/mosen6266-netizen/MS_001/actions/runs/38966422700) **39/39 SUCCESS**，原有安全、恢复、运行中心、预检、剧本和SQLite 回归未受影响。
- 开发期间首次测试C#变量声明错误，随后WPF/WinForms类型重名，均修复并重新全量CI验证。源码主要提交 [`28fefa70`](https://github.com/mosen6266-netizen/MS_001/commit/28fefa70516e80c4dd75d3d0de8d55feb45ba3c3)，最终构建修复 [`76270f9b`](https://github.com/mosen6266-netizen/MS_001/commit/76270f9b9be8a5619f298a896ff52a4341933b15)；详细见 `docs/STEP_22_NOTIFICATIONS.md`、`docs/STEP_22_TEST_MATRIX.md`、`docs/STEP_22_ACCEPTANCE.md`。
- **DEFERRED**：真实Win10/11托盘/休眠/锁屏置顶表现、TDLib官方发送前统一停止闩锁核对、正式账号与群交互、50群压力和72小时运行。Signal MS002、Cloudflare生产授权后台未修改。
- **进度22/32（68.75%），剩余10步，下一步23：历史、审计与安全导出**。

## Step23 — 历史、审计与安全导出（2026-10-16）
- **开发阶段 done_dev**：SQLite v10 独立派生历史表、来源事件去重、固定审计类别和级别、索引游标分页，保留原始任务与投递回执。
- Windows“任务历史”入口支持10条分页、异常筛选、脱敏诊断ZIP和保留7/30/90/180/365天或强确认清空历史；纯派生数据清理后留存审计标记，绝不删除任务、原始回执、账号与紧急停止闩锁。
- 支持ZIP固定3文件、最多200条类型事件、最大256KiB，无群号/账号/任务/投递GUID、消息、数据库、路径。固定名称诊断日志保留接口防误删，独立运维文档 `docs/OPERATIONS.md`。
- Windows/Linux `tests/History/Program.cs` **各48项断言通过**，真实注入100,000行，100页索引查询、严格导出内容检查、清理原始数据完整性；Linux插入约3231毫秒/100页125毫秒；Windows插入约4436毫秒/100页196毫秒（CI环境仅供基线参考）。
- 完整 [CI 38971446337](https://github.com/mosen6266-netizen/MS_001/actions/runs/38971446337) **41/41 SUCCESS**；WPF win-x64完整构建发布，历史迁移与既有安全/调度/恢复回归全部通过。
- 源码提交 [`b2a363a6`](https://github.com/mosen6266-netizen/MS_001/commit/b2a363a6086b33ba77dd27c36f277996667fd445)。设计、测试、验收见 `docs/STEP_23_HISTORY.md`、`docs/STEP_23_TEST_MATRIX.md`、`docs/STEP_23_ACCEPTANCE.md`。
- **DEFERRED**：Win10/11实机历史/文件对话框、正式TDLib在线任务与官方回执、长时50群性能；目前是离线模拟持久数据。Signal MS002与Cloudflare生产资源未更改。
- **开发进度23/32（71.875%），剩余9步；下一步24性能剖析与资源防泄漏。**
