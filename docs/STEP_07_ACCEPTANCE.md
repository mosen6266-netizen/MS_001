# Step 07 开发验收：设置、搜索与状态管理
日期：2026-10-16。仓库：`mosen6266-netizen/MS_001`。**结论：DEV PASS；最终本机真实交互验收 DEFERRED。**

## GitHub 测试
[GitHub Actions 38927977333](https://github.com/mosen6266-netizen/MS_001/actions/runs/38927977333)：completed/success，9 个 job 全部通过。
- React/TypeScript：**22 项 Vitest 单元测试**通过，包含原有 IPC/导航/设计组件，以及新增设置合同、300模拟记录过滤分页、1000次输入防抖、1000次重复保存防重锁。 
- 设计规范：16 项 static check PASS。
- SQLite 非敏感设置：Windows 16 项 / Linux 16 项 PASS，含默认值、变更保存、重启读取、revision冲突拒绝、非法输入拒绝、独立备份恢复。
- SQLite 原迁移测试：Windows 27 项、Linux 27 项 PASS；窗口几何：Windows/Linux 各10项 PASS。
- Windows WPF/WebView2：云端 dotnet build + 自包含发布成功；Chromium桌面1440px、窄窗口700px离线截图job PASS（不覆盖设置保存操作的浏览器端交互）。

## 实际开发内容
- `src/Infrastructure/Persistence/UserPreferences.cs`：SQLite元数据设置管理，严格四字段、事务保存、revision CAS 语义、跨进程数据冲突不覆盖及错误隔离。
- `src/contracts/ipc.ts` 和 `apps/desktop/MainWindow.Settings.cs`：只允许 `settings.get`/`settings.save` 离线本地配置操作；8KB 请求上限、字段白名单、主 UI 线程异步处理本地数据库；禁止 Telegram 消息 API、凭据或任意命令。
- `apps/ui/src/hooks/usePreferences.ts`：WebView2 设置数据加载、超时/冲突明确提示；浏览器预览不会谎称保存成功。
- `apps/ui/src/components/PreferencesPanel.tsx`：密度、搜索防抖150/200/250ms、每页10/20/50、桌面通知偏好（*通知实际动作未实现*）；只有宿主确认成功才显示“已保存”。
- `src/web/settings/search.ts` 与 `MockAccountIndex.tsx`：300 条**合成**账号资料检索；每页最多50 DOM 行，防抖与滚动，不登录实际Telegram。
- `src/web/settings/exclusive.ts`：防止单次异步请求执行中的重复提交。1000次重复异步操作单元测试只执行一次；失败恢复后允许再次主动提交。
- `apps/ui/src/App.tsx` 保留单页状态路由/固定侧栏；真实业务将在后续步骤按设计系统集成。

## 修复记录
第一版 UI 提交引入两个未使用 import，导致 `npm run lint` 失败（Run 38927675865）。修正后重跑 GitHub CI PASS；另外消除了重复点击被跳过却仍显示成功 Toast 的逻辑漏洞，并补充1000次并发回归。
**没有关闭 lint、NuGet安全门槛或任何测试来掩盖失败。**

## 待最终本机验收
- Win10/11 实际磁盘满、杀软、SQLite 文件锁和断电时数据一致性。
- WebView2 真实交互中的连续输入、焦点/滚轮/分页、显示响应与保存反馈；高 DPI、多显示器无闪白。
- 将合成 300 条数据替换为真实合法授权账号的列表与权限（步骤09–12）；性能及长稳由后续发布阶段检验。
- 不允许宣称真实300账号登录/50群发送/通知系统已实现。

开发步骤 **07 = done_dev**；下一步 **08 = not_started**；开发进度 **7/32=21.875%**，剩余25步。
