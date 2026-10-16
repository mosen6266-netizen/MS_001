# Step11 开发阶段验收｜账号注册表及会话生命周期

日期：2026-10-16；GitHub `mosen6266-netizen/MS_001`。
**开发阶段 DEV PASS / 真实TDLib原生加载、登录与长期稳定性仍 DEFERRED。**

## 已验证的真实 CI
[GitHub Actions 38940132317](https://github.com/mosen6266-netizen/MS_001/actions/runs/38940132317) `completed/success`，**17 / 17 作业通过**。
- Windows **48 / 48** 项账号与会话生命周期模拟断言通过，Linux **48 / 48** 通过，包含300条账号资料、301号容量拒绝、中文备注和分组查询/分页、旧版本重启与备份、账号版本冲突、确认归档及恢复、已确认官方 user ID 唯一、18个独立密钥文件、Windows DPAPI保护、4槽有限资源池、关闭失败后需要检查等。
- 前端 **29 / 29** Vitest测试通过；主题与可访问性静态规则 **16 / 16** 通过。
- Windows x64 WPF 云端编译/自包含 publish 成功，Chromium桌面/窄窗口首页截图回归成功，SQLite迁移/备份、用户设置、安全诊断、窗口几何、TDLib mock适配器及认证 mock 状态机原有双系统矩阵全部通过。
- 注意：测试Windows运行器的DPAPI是真实Windows CurrentUser API，但会话 runtime 在本步是FAKE；Linux使用测试专用非加密key封套，绝不能用于真实客户端。**没有登录任何真实Telegram账号，也没有收发任何消息。**

## 本步真实交付
- `src/Infrastructure/Persistence/Migrations/0003_accounts.sql`、`AppDatabase.cs`：v3事务迁移、升级前备份、原数据保留，旧`ready`不再被误判为真在线。
- `AccountRegistry.cs`：300条有效元数据容量、版本化CRUD/搜索/中文备注、分组、官方认证后身份去重、SQLite保留的安全归档、独立查看及版本化恢复；被群/任务引用的账号归档阻断。
- `SessionVault.cs`：单账号独立路径、32字节随机密钥、Windows CurrentUser DPAPI封套、覆盖安装不变更已有密钥、失效/缺失密钥拒绝静默重建、Lease 清零与符号链接防护。
- `SessionSupervisor.cs`：300个登记账号与独立的最大活动16槽（可配置1–32），不会一次性打开300个TDLib实例；只有已认证的官方授权状态可从AwaitingAuthorization转为Ready，关闭无法确认标NeedsReview且暂不释放槽位。
- 严格 `accounts.list/create/edit/archive/restore` WebView2白名单：`IpcRequestGuard.cs`、`MainWindow.Accounts.cs` 和 `src/contracts/ipc.ts`；请求不包含电话号码、验证码、密钥或聊天数据，所有操作仅对本地账号元数据。
- `AccountRegistryPanel.tsx`：桌面实际 SQLite账号列表、备注/分组编辑、分页/过滤、已归档账号恢复及需要完全一致备注才能确认的单一绿色确认/红色取消弹窗；在不含Windows host的浏览器预览仍展示300条模拟数据，且清楚说明未真实登录。
- `tests/SessionLifecycle`、`apps/ui/src/account-registry.test.tsx` 及 Windows/Linux CI 覆盖所有上述条件。
- `docs/STEP_11_ACCOUNTS.md`、`docs/STEP_11_TEST_MATRIX.md` 为新对话交接与最终验收保留详细边界。

## 修复历史及边界
- 第一轮AccountRegistry的C#多行SQL字符串写法触发编译错误，已修正并重跑。
- 第一轮前端类型缩窄及Unicode控制字符正则校验导致UI类型/测试失败，已修正并重跑；归档弹窗增加真正禁用确认按钮且输入框位于弹窗内部，保存失败不丢编辑内容。
- 后续补充“已归档列表+恢复”及IPC严格布尔参数验证，重新运行全部17个作业成功。没有关闭lint、SQLite迁移、Security或Windows构建门槛。
- 注册账号元数据≠Telegram在线；假连接池不等于真实TDLib占用/CPU实测。原生tdjson.dll、真实API key配置、合法本人账号授权、实际重启后免扫、实际会话密钥解密、Windows10/11文件ACL/DPI、72小时长稳均 **DEFERRED**。
- 旧非规范GUID账号元数据可只读列出，但实际会话绑定/重命名需专门迁移流程，不能悄悄改变历史ID和脚本引用。
- 删除目前是可恢复归档，暂不提供不可逆文件粉碎；用户若最终需要永久删除须另设独立显式确认和备份审查。Signal MS002与其授权未修改。

**Step11 = done_dev。Step12（群组、话题与账号权限目录）= not_started。进度 11/32=34.375%，剩余21步。**
