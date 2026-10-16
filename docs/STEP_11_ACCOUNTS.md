# Step11 — 账号注册表、独立会话目录与生命周期
日期：2026-10-16。项目 MOSEN Group Scheduler，仓库 `mosen6266-netizen/MS_001`。本步只开发及模拟/CI；没有真实Telegram授权与消息发送。

## 范围
- 复用 Step04 SQLite `accounts` 表，增加 schema v3：`group_tag`（分组），`verified_telegram_user_id`（官方认证后唯一身份），`revision`（乐观冲突校验），`deleted_utc`（可恢复的归档）。
- 兼容旧版本 `app.db` 数据结构：原账号 ID 保留原值，若并非规范 GUID 仍可在列表中**只读显示**，不得把任意遗留字符串拼成TDLib会话目录。未来引入专门升级迁移器后再允许编辑或连接该类旧条目。
- 支持每个真实本地槽位新建、按分组筛选、搜索、修改备注、归档、查看归档、恢复；别名可以重复：剧本角色绑定优先使用目标群成员资格并对同备注歧义要求手动选择。已由官方授权确认的 Telegram userID 应唯一（`ux_accounts_verified_user_id`），但此阶段尚未接入真实 `getMe`。
- 可登记最多 **300 条正常账号元数据**，并不等于 300 条已登录会话；旧归档记录作为 tombstone 保留，不清除密钥或文件。正常账号数每次创建与恢复由 SQLite 事务检查上限，避免交错超限。
- `account_id` 是随机 GUID，与用户手机号和 TG ID 无关，作为独立目录名称。不将手机号、API hash、验证码、私密聊天、会话加密密钥或二维码保存在公共SQLite表。

## 文件及组件
- `src/Infrastructure/Persistence/Migrations/0003_accounts.sql` + `AppDatabase.cs`：带SHA的 v3 自动迁移、升级前备份、事务回滚。升级时将旧 `ready/connecting` 元数据改成 `auth_required`，**绝不凭旧数据库声称当前仍在线**。
- `src/Infrastructure/Persistence/AccountRegistry.cs`：SQL参数化的CRUD、备份一致性、revision冲突、同 Telegram verified ID 去重、对群绑定/任务引用的归档阻断以及可恢复软删除。
- `src/Core/Sessions/SessionVault.cs`：每个账号 `%LOCALAPPDATA%\\MOSEN\\TelegramScheduler\\tdlib-sessions\\<GUID>\\`，独立数据库与媒体目录，单独的 `db-key.dpapi`；实际Windows DPAPI CurrentUser加密封套，32字节随机密钥；重启/覆盖安装不重生成密钥；拒绝已识别的符号链接/重解析点；密钥只在需要时以可清零字节数组Lease打开。
- `src/Core/Sessions/SessionSupervisor.cs`：最多300个槽位登记；默认最多16个并行打开的运行时槽位（可配置1–32），按需打开而非一次性实例化300个TDLib连接；区分 `Dormant / Opening / AwaitingAuthorization / Ready / Closing / NeedsReview / Archived`。只有可信TDLib的 `authorizationStateReady` 更新才能视为 Ready。原生关闭结果不确定时保持连接槽位并要求人工恢复，防止同一账号双开。
- `src/Core/Sessions/IAccountRuntime`：实际TDLib客户端在下一阶段/原生构建集成后接入，此阶段为测试 Fake runtime；**未连接真实 tdjson.dll**。
- `src/Core/Security/IpcRequestGuard.cs`、`src/contracts/ipc.ts`：本地仅允许 accounts.list/create/edit/archive/restore 五种操作，严格校验字段与长度/页大小/GUID/版本。没有任意文件路径或TDLib消息发送接口。
- `apps/desktop/MainWindow.Accounts.cs`：Windows宿主通过已验证 WebView2 origin 接收元数据操作，异步SQLite；错误不将SQL/账号秘密暴露给前端。
- `apps/ui/src/components/AccountRegistryPanel.tsx`：在Windows宿主中显示真实本地账号元数据、备注/分组编辑、10/20/50条分页、正常/归档切换、精确备注二次确认、恢复功能。只展示“未连接”，不虚报已授权。纯浏览器没有宿主时继续使用Step07 的300条合成列表作为演示。
- `tests/SessionLifecycle` + CI Windows/Linux矩阵：300条注册/查询分页/分组/重启、备注修订冲突、verifiedID唯一索引、归档撤销、备份、18个独立key、Windows DPAPI、连接池4槽容量及失败关闭占用恢复。Linux使用测试用明文封套**仅合成临时数据**；正式Windows库只能使用DPAPI。

## 操作与资源定义
- 账号登记 ≠ 已连接 ≠ 授权成功 ≠ 有权向群发言。三个层次分别计数，不允许把300个目录变成300个常驻网络会话。
- 启动时持久化记录不得自动标 Ready；实际授权结果须等官方 TDLib 状态更新。在原生库尚未构建、凭据注入与保护体系未接上之前，正式登录按钮继续不可用。
- 关闭或暂停前需等待本机 runtime 确认关闭；收到超时、异常或不可验证的完成状态要标记`NeedsReview`并保留预算位，不盲目释放再开。
- 归档只是**可逆的安全移除**，需要当前备注和版本双重确认；任何被运行任务或群绑定引用的账号不允许归档。**不会自动删除TDLib会话文件**，永久删除须独立高级危险操作设计并验收。
- 只有当前Windows用户能使用DPAPI密钥：换设备/丢Windows个人资料密钥意味着无法直接重用原加密会话。跨电脑恢复须后续单独设计加密备份与用户授权，不得自动上传到云端。
- 用户明确要求真实Windows 10/11 + 合法自有 Telegram 账号测试最后集中进行。因此本步的DPAPI和会话Fake验证不构成可运行300个真实账号的证明。

## 风险留存
1. 未做TDLib真实 native DLL 加载、登录与会话重启验证；账户注册表之外的真实认证接口在第10步模拟实现，尚未在WPF启用。
2. 需要正式Windows ACL、多账号/多用户和杀软/磁盘满下的密钥文件保护评估；只依赖当前用户DPAPI不能抵御同一用户进程内的恶意代码。
3. 若关闭时native挂起必须限制等待时长并明确记录无法确认；不能将未知关闭结果标成成功。
4. 旧版非GUID ID目前只读兼容；后续添加安全无损的标识迁移设计，不得在未完成FK协调前直接改变ID。
5. 300条合成账户测试不代表300条真实Telegram会话的CPU/内存/网络容量。50群任务仍待后续步骤实现。
