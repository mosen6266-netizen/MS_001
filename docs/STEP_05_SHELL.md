# Step 05 — Windows 容器与固定导航

**计划步骤：** 05/32 | **阶段：** 2/8 | **结果：** DEV PASS，GitHub CI 全部通过；真实 Windows 交互测试 DEFERRED。

## 工程内容
- `src/web/routes.ts` 定义六页白名单与 `ShellState`；导航调用本地 React 状态切换，不更改 URL、不调用 `window.location`，不会触发 WebView2 的整页 Navigate。当前页面内容区域保留独立滚动值。
- `apps/ui/src/App.tsx` 固定主导航 + 单一主内容区；Alt+1 至 Alt+6 页面快捷键；键盘编辑场景不拦截输入框/文本域/下拉框；bridge 事件绑定在卸载时解除。业务模块仍处未实现状态，不假装 Telegram 可用。
- `apps/ui/src/style.css` 顶层和 document 禁止额外滚动；侧边栏自身固定，内容区域独立滚动；小窗口断点适配，键盘 focus-visible，可减少动效。
- `src/Core/Shell/WindowPlacementPolicy.cs` 不依赖 Windows 平台的窗口安全坐标计算；保存的尺寸越界/NaN/不可用屏幕坐标自动纠正，保留最大化标志。纯计算工具 Linux / Windows 双平台可运行。
- `apps/desktop/MainWindow.xaml` 启动即显示一致的暗色遮罩，WebView2 初始化成功后再切换显示；错误时停留在中文诊断面板，而不是闪出白屏/黑屏。
- `apps/desktop/WindowStateStore.cs` 将非敏感的窗口几何保存在本地用户目录，写临时文件后原子替换，不接触 Telegram 会话。
- `apps/desktop/MainWindow.xaml.cs` 强化可信本地域名限制、阻止外部页面跳转和新窗口；CoreWebView2 初始化只执行一次，关闭窗口释放 WebView2 控件及事件，不保留发送后台。
- 既有 `App.xaml.cs` 单实例互斥锁继续使用，不允许双启动两个本地程序。

## 自动化验收
1. `apps/ui/src/routes.test.ts`：6 个路由唯一；切换保存滚动位置、同页面不会多次切换、非法滚动值处理；Alt+1…6 快捷键；React SSR 快速检查只有一个 `<main>`、一个 `<aside>` 和六个导航按钮、无页面整刷链接。
2. `tests/UI/Shell/Program.cs`：10 个窗口坐标/几何测试（默认居中、越屏纠正、最大化、缩小屏幕、损坏配置处理等）；GitHub Actions Windows + Linux。
3. GitHub Actions 其他回归：SQLite 迁移 27 项每系统、前端 TypeScript/ESLint/单测/构建、WPF Windows x64 编译和打包。

## 边界和未验证
- SSR/状态模型可以证明切换不依赖页面链接，但不能代替浏览器交互、实际窗口 DPI、多屏设备或录屏判断是否出现视觉闪烁。
- 最终 Windows 10/11 实机要验证加载遮罩、浏览器内视觉闪烁、导航滚动、125/150/200% DPI、断开 WebView2 Runtime、窗口关闭后台释放情况；按用户要求集中于最终验收。
- 此时没有连接真实账号、没有群组自动发送、没有 Cloudflare 授权部署，旧 Signal MS002 绝不修改。

## 自动化结果
[CI 38924326676](https://github.com/mosen6266-netizen/MS_001/actions/runs/38924326676)：6个 job success，UI Vitest 8项、Windows/Linux窗口几何各10项、数据库回归与WPF编译通过；最终实机GUI验收仍DEFERRED。
