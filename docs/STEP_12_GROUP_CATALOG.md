# 第12步：Telegram 群组、话题和账号发言权限目录

日期：2026-10-16。仓库 `mosen6266-netizen/MS_001`。第12步属于**源码开发/模拟与云端CI阶段**，不是实际Telegram同步验收。

## 稳定ID与数据可信来源

- 数据库新增 `src/Infrastructure/Persistence/Migrations/0004_groups.sql`，schema v4。旧 `chats` 和 `account_memberships` 资料默认 `source='unverified'` / `permission_source='unverified'`，旧 `can_send` 清零。只有已验证账号关联的 TDLib 数据才能入库为 `tdlib` 来源。UI只能读取，不能伪造`tdlib`来源。
- 群必须通过 Telegram `chat.id` 映射；群组名称只是显示字段，改名不能导致重新分配 `chat_id`，不能破坏 `group_bindings`。论坛话题由 `(chat_id, message_thread_id)` 共同确定，名字变更不丢失 ID。
- `GroupCatalogRepository.cs`：原子 UpsertChat/UpsertTopic/UpsertMembership、按时间戳拒绝迟到的旧状态、未验证群组/账号禁止赋予发言权限、仅列出group和supergroup而非私聊/频道、按中文群名查询及每页10/20/50条、按群列出话题和各账号权限。
- `TdlibGroupParser.cs`：从官方形状的`chat`、`updateNewChat`、`forumTopicInfo`、`chatMember`、`supergroupFullInfo` / `updateSupergroupFullInfo` 提取只读快照。TDLib 部分更新例如 `updateChatTitle` 不得当完整chat对象以免覆盖群权限；应在未来接入实际TDLib时从可信缓存取全量对象或使用独立的局部更新逻辑。
- 成员角色包括member/administrator/creator/restricted/left/banned/unknown。仅有member身份尚不足以获得发送能力；普通成员必须再检查群默认 `chatPermissions.can_send_basic_messages` 和对应图片权限，未知/缺字段默认不允许。频道不能当群组发送。
- 慢速模式 `slow_mode_delay` 是官方超级群信息的静态值；具体到期剩余、FloodWait及慢速模式实时变化，必须在任务真正执行前再次确认，绝不能通过切换账号绕过平台约束。

## 开始任务前必须执行的检查

`GroupPermissionPolicy.Preflight` 返回结构化 `PermissionDecision`：
1. 必须是 TDLib来源的普通群/超级群，且话题和chat ID匹配、未关闭未隐藏。
2. 账号成员必须是有效成员；禁止left/banned/unknown；权限不明、照片权限不够、群默认不允许文字发送时全部拒绝。
3. 必须明确由**本次启动的真实TDLib实时检查**来提供fresh标记，不允许仅凭 SQLite 缓存判断可以开始。权限记录超过60秒/时间异常即返回 `RECHECK_REQUIRED`。此fresh标记当前仅为供后续启动器调用的内部参数，**没有接入真实TDLib重新检查，所以目前不能启动任何发送**。
4. 同账号的群权限单独储存；A在群内可以发言绝不意味着B也可以；角色选择以后必须优先匹配目标群有权限账号，同备注歧义要求交互确认。
5. 预检查通过并不代表Telegram最终接受发送，还应遵守slow mode、FLOOD_WAIT及其他账号限制；未知结果不能盲重试。

## 前端/后端

- `apps/ui/src/components/GroupCatalogPanel.tsx`：中文群组页，搜索、分页、进入话题/成员权限，只读状态，空目录明确显示等待官方同步。
- `apps/desktop/MainWindow.Groups.cs` 与 `IpcRequestGuard.cs`、`src/contracts/ipc.ts`：仅 `groups.list`、`groups.topics`、`groups.members` 三种读取请求。参数白名单、输入范围、结果验签与最多300条成员展示；没有发送或写入Telegram权限的前端命令。
- 截至本步 **没有真正连接TDLib原生DLL，也没有注入真实API ID/Hash、登录任何账号或完成会话恢复**。UI不会制造模拟群组显示为真实群组。TDLib更新事件到受信任存储器的生产监听绑定仍需在正式原生客户端集成阶段完成。

## 验证

- `tests/GroupCatalog` Windows/Linux mock: 群组解析、私聊过滤、topic ID校验、成员发送权限默认为拒绝、慢速模式、频道/封禁/话题关闭拒绝、TTL、图片限制、SQLite v4、旧手工资料隔离、56个群分页、中文搜索、群改名保留 `group_bindings` 外键、账号权限相互独立、迟到的旧状态不可覆盖撤销与话题关闭、重启/备份留存。
- 历史Step01–11全部回归，不应因为群功能改变账号注册表、授权状态机、消息时间设置（30秒发送前等待→5秒正在输入→发送，共35秒）。
- 用户声明最后才集中安装实机测试：真实TG群同步、话题/权限更新事件、离群/禁言、无权限提示、SlowMode、300账号50任务限制等均标记`DEFERRED`，不能以模拟CI取代。

官方资料：https://core.telegram.org/tdlib/getting-started 、https://core.telegram.org/tdlib/docs/classtd_1_1td__api_1_1get_chat_member.html 、https://core.telegram.org/tdlib/docs/classtd_1_1td__api_1_1supergroup_full_info.html

**开发完成状态以 GitHub 最新全部CI作业通过后写入 `docs/STEP_12_ACCEPTANCE.md` 为准。**

## 最终CI开发验收
[完整GitHub Actions 38942859853](https://github.com/mosen6266-netizen/MS_001/actions/runs/38942859853) 所有19个Job通过；Windows/Linux各47个组/话题权限模拟断言，前端32个单测、16个UI静态检查。官方chatMember.member_id必须等于当前已认证账号本人userID，非本人返回丢弃。真实登录/群自动同步/实时权限/发送任务DEFERRED。详见 [STEP_12_ACCEPTANCE.md](STEP_12_ACCEPTANCE.md)。
