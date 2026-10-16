# 第12步开发阶段验收 — 群组、话题及账号权限目录

日期：2026-10-16；唯一仓库 `mosen6266-netizen/MS_001`。
**结论：开发/模拟CI DEV PASS。TDLib原生库及实际群/话题自动同步、在线权限复查、账号真实发送仍为 DEFERRED。**

## 权威CI证据
- [GitHub Actions Run 38942859853](https://github.com/mosen6266-netizen/MS_001/actions/runs/38942859853)：工作流completed / **success**；**19/19**作业成功；失败/跳过的门槛为0。
- Windows GroupCatalog **47/47**，Linux **47/47** 断言通过，使用官方结构的合成TDLib JSON，未使用真实账号/手机号或调用Telegram服务器。
- React/TypeScript 前端9个Vitest文件，**32/32** 测试通过；设计/可访问性静态16项通过。
- 原有Windows WPF桌面主程序编译、自包含发布（尚非真正安装包）、Chromium 1440/700px 初始截图、SQLite迁移和备份、设置存储、DPAPI/本地IPC安全、窗口几何、TDLib适配器及认证状态机/300账户注册等全部回归成功。
- 测试开发中修正了 C# 比较表达式语法、React TopicPage 清空时类型不一致、React SSR初始loading状态断言；增补`chatMember.member_id.user_id`与已验证用户ID的严格比较以及错误成员不可绑定的安全反例测试后全量重跑通过。**未跳过/禁用CI任务。**

## 本次提交的真实功能
1. **SQLite v4** — `0004_groups.sql`，给群组引入官方`source`来源标签、是否论坛、话题关闭/隐藏状态、成员权限快照、文字/图片许可、慢速模式数值及更新时间。历史手工资料不被当作可信群组，历史`can_send`清零；迁移/备份保持其他账号和剧本数据。
2. **正式目录源码（测试用fake TDLib事件）** — `GroupModels.cs`、`TdlibGroupParser.cs`、`GroupCatalogRepository.cs`，只接受已验证官方来源的 `chat_id`、`message_thread_id` 和账户成员ID，群名变化不会改变旧剧本FK目标；后到达的旧状态不会覆盖更新的撤销/关闭权限；账号权限按account_id隔离；群组分页/中文搜索/话题/成员查询。
3. **发送前权限判定** — `GroupPermissionPolicy.Preflight` 禁止未确认成员、失效缓存、缺少图片/文字权限、封禁/退群、话题关闭/隐藏、频道冒充群组；必须由**本次启动的真实TDLib重新验证**提供fresh标志且记录不超过60秒。返回慢速模式信息供将来执行器遵守；它不是Telegram真正发送回执。
4. **中文桌面只读群组页** — `GroupCatalogPanel.tsx` / `App.tsx`，通过本地 `groups.list/groups.topics/groups.members` 命令显示可信来源群组、话题、每账户文字/图片权限与慢速模式；支持群名搜索和分页。没有官方真实同步时诚实展示空状态，不会伪造已经读取群组。
5. **IPC严格白名单** — `IpcRequestGuard.cs` / `src/contracts/ipc.ts` / `MainWindow.Groups.cs`。WebView2不允许任意发送/加群/改权/指定文件路径；前端只可读取本地目录。
6. 自动验证 `tests/GroupCatalog` 和 `apps/ui/src/group-catalog.test.tsx`，测试56群目录分页、绑定的群名重命名、私聊过滤、话题关闭、权限撤销与到期、不同账号权限隔离、官方userID核验、重启与数据库备份。

## 仍为正式版阻断，不能以DEV PASS冒充已上线
- **真实TDLib原生`tdjson.dll`当前未构建/加载/绑定账号；没有真实用户登录，也没有Telegram群组自动同步到应用的数据。** 真实事件到持久仓库的生产监听器需要后续原生客户端整合，且必须正确处理局部更新、群组迁移及多话题分页。
- 真实的群成员身份重查（`getChatMember`）、默认chatPermissions与slow mode每次任务开始新鲜验证，和不同账号的官方返回异常/FLOOD_WAIT等，都需要后续实际集成。当前从缓存无法启动真实发送。
- Telegram群组权限与任务能力只在真实账号本人已授权且群允许时使用；不得用多账号切换绕过平台的限流、封禁或慢速模式。
- Windows 10/11实机画面、账号授权、多个群话题真实变化、50独立群任务容量/长稳、真实API原生安全均按用户要求最终集中测试，列入DEFERRED。
- 原 Signal Auto Scheduler MS002 及其生产卡密/资料未修改。

**Step12=done_dev，Step13=not_started（可视化剧本编辑器）；开发阶段 12/32 = 37.5%，剩余20步。**
