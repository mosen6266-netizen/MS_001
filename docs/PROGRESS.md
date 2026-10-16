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
