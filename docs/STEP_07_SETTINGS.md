# Step 07 — 设置、搜索与状态管理

日期：2026-10-16。**仅开发与云端验证；真实 Win10/11 操作延后到最终验收。**

## 源码结构
- `src/Infrastructure/Persistence/UserPreferences.cs`：本地非敏感 UI 设置的类型化存储，依托 Step04 的 SQLite `settings` 表；事务读写、旧 revision 冲突拒绝、损坏 JSON 拒绝、重启恢复和备份恢复。
- `src/contracts/ipc.ts`：只允许 `system.ping` / `app.info` / `settings.get` / `settings.save` 四种 IPC 命令；严格校验对象键和数据范围。**没有任何 Telegram 发送、会话读取或任意 SQL 命令。**
- `apps/desktop/MainWindow.Settings.cs`：WPF 宿主源验证、8KB 请求上限、离线 SQLite 异步任务和结果/冲突状态码；UI 线程不等待磁盘 I/O。
- `apps/ui/src/hooks/usePreferences.ts`、`PreferencesPanel.tsx`：加载/保存/版本冲突/超时提示。Web 浏览器预览不能保存，不会虚报已写入本地 DB。
- `src/web/settings/search.ts`：Unicode NFC + 备注模糊筛选、150–250ms trailing debounce、分页每页10/20/50，列表最大只渲染50行。
- `MockAccountIndex.tsx`：300条**模拟**本地账号资料，实际真实账号由第09–12步接入；无手机号、API键或登录会话。用于搜索输入时的交互/性能回归。
- `src/web/settings/exclusive.ts` 和 `useActionLock.ts`：异步保存独占锁；丢弃正在处理的重复点击，失败后释放锁；不将第二次点击误报为保存成功。
- 原有导航`routes.ts`与页面滚动记忆保留，不在搜索或保存后重建全应用。

## 设置数据边界
`UserPreferences`字段：density=`compact|comfortable`，searchDebounceMs=150..250，pageSize=10/20/50，desktopNotifications=boolean。通知目前仅为**偏好开关**，真实 Windows 提醒须后续步骤完成。
- 不在此表存手机号码、Telegram会话数据、Cookie、密钥或用户消息内容。
- 前端不能把设置直接保存到 localStorage 并声称 SQLite 已落盘；桌面宿主必须返回成功的修订号。
- 保存冲突不自动覆盖更新后的设置，也不在超时后自动重发可能已成功的保存请求。

## 开发阶段自动化验证
1. Windows/Linux：默认设置、读取持久化、v1/v2 revision 自增、同 revision 重复保存冲突、非法密度/延迟/页大小拒绝、备份恢复。
2. 前端 300 条模拟数据搜索、分页末页、无结果与 NFC 规范化、1000次连续输入防抖合并与取消。
3. 前端1000次重复异步保存仅执行一次；失败后手动重试允许；不显示虚假成功。
4. 既有 UI 组件、路由记忆、视觉规范检查，WPF Windows CI 编译，以及Chromium离线截图回归。
5. 真实 Win10/11 SQLite 文件锁、实际 WebView2 搜索输入观感、磁盘满/权限受限/杀软和持续运行全部 **DEFERRED** 至用户最后集中验收。

开发源代码保存后必须取得 GitHub Actions 全绿结果，再标记 `done_dev`。云端编译绝不算真实 Telegram 功能完成。
