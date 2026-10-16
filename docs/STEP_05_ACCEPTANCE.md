# Step 05 — Windows 容器与导航开发验收
日期：2026-10-16 | **DEV PASS / REAL-MACHINE VISUAL TEST DEFERRED**

## CI 证据
- [GitHub Actions Run 38924326676](https://github.com/mosen6266-netizen/MS_001/actions/runs/38924326676)：completed / **success**，全部6个job成功（UI、Window geometry Win+Linux、SQLite migration Win+Linux、Windows desktop编译/打包）。
- 前端 Vitest：2个测试文件，**8项测试通过**，涵盖路由、记忆滚动位置、Alt+1–6快捷键、HTML SSR检查、IPC消息限制。
- 窗口几何：Linux **10/10**、Windows **10/10** 通过；检测尺寸、越界、错误配置及辅助显示器负坐标。
- SQLite 回归及主程序 Windows WPF x64 自包含编译仍全部 success；未真实向 Telegram 登录或发送消息。

## 已提交源文件
`src/web/routes.ts`、`apps/ui/src/App.tsx`、`apps/ui/src/style.css`、`apps/ui/src/routes.test.ts`：固定侧边栏、单文档页面内切换、滚动位置保留、快捷键、响应式布局。
`src/Core/Shell/WindowPlacementPolicy.cs`、`tests/UI/Shell/Program.cs`：可独立测试的窗口几何算法。
`apps/desktop/MainWindow.xaml`、`apps/desktop/MainWindow.xaml.cs`、`apps/desktop/WindowStateStore.cs`：暗色加载界面、离线WebView2、保存窗口几何、初始化与关闭清理。
`.github/workflows/ci.yml`：多系统窗口几何自动化验证。
`docs/STEP_05_SHELL.md`：设计、风险与最终本机测试说明。

## 尚未实机验证
实际WebView2视觉闪烁、窗口打开/关闭后的句柄与进程释放、DPI125/150/200%、多屏拔插、滚轮体验及 Windows10/11 机器兼容测试，统一列入最终用户安装验收。CI与SSR不能代替真人视觉测试。

**Step05 `done_dev`，下一步 Step06 深色UI设计系统。开发进度 5/32 = 15.625%，剩余27步。**
