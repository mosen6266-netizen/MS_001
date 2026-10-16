# Step 03 开发阶段验收 — 2026-10-16
项目唯一仓库：`mosen6266-netizen/MS_001`。
**验收结论：DEV PASS / ON-DEVICE DEFERRED**，仅标记基础工程可编译与自动化检查通过，不代表正式版可使用 Telegram。

## GitHub CI
- [GitHub Actions main build 38921641970](https://github.com/mosen6266-netizen/MS_001/actions/runs/38921641970)：status=`completed`, conclusion=`success`（通过API核查）。
- `ui` job：SUCCESS；`npm ci` 严格按已提交的 lockfile 安装，`npm run lint`（TypeScript + ESLint）、`npm test`（Vitest IPC tests）、`npm run build`（Vite）均完成。
- `windows` job：SUCCESS；GitHub Windows 运行器 `.NET 10 WPF/WebView2` 云端编译及 `dotnet publish --self-contained true -r win-x64` 已通过。该发布产物只是工程技术构建，不是可用于 Telegram 的正式安装包。
- 输出 artifact：`ms001-step03-desktop-build`（ID 12054891527）；`ms001-ui-dist`（ID 12054124228）。不会上传或内置真实账号、聊天内容或 API 凭据。
- 可复现依赖：`apps/ui/package.json` 直接依赖精确版本；`apps/ui/package-lock.json` lockfileVersion 3，238 个 package 条目；CI 使用 `npm ci` 而非随安装时解析最新依赖。
- 在仓库中删除了仅用于首次生成锁文件的 `contents:write` workflow，常规 CI 权限恢复为 `contents:read`。

## 交付文件
- `apps/desktop/`：Windows WPF 主程序，单实例 Mutex，离线 WebView2 显示，限制网页导航、新窗口与 IPC 命令；未启用 Telegram 通信。
- `apps/ui/`：React + TS 固定侧栏的暗色离线界面、页内路由和状态；未开发的功能标注未启用。
- `src/contracts/ipc.ts`：严格限制 `system.ping` / `app.info` 两个 IPC 命令及响应类型；
- `.github/workflows/ci.yml`：前端 lint/test/build + Windows build/publish；
- `Directory.Build.props`、`.gitignore`：确定性构建配置和敏感文件排除。
- `docs/STEP_03_ENGINEERING.md`：架构与范围界定。

## 保留的风险（最终本机验收而不是本阶段的阻断）
- Windows 10/11 真实 GUI 启动和 WebView2 Runtime 配套、单实例焦点提升及 DPI/兼容测试仍未实测，留待最终安装测试。
- TDLib 原生 DLL 加载、真实账号授权及网络消息发送尚未实施；这些属于后续 09–20 步，与最终真机测试。
- 本次只有骨架和最小 IPC 合同，不应被当成 300 账号或 50 群软件的完整版本。

## 步骤结论
**Step03 = `done_dev`**，Next Step04 = `not_started`。
开发进度 `3 / 32 = 9.375%`，真实运行/最终发布验收仍未完成。
