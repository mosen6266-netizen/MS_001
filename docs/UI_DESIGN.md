# MOSEN Group Scheduler — V1.0 深色 UI 设计系统

阶段 2/8，Step06，2026-10-16。本文是源码和设计基线，不代表 Win10/11 实机 UI 截图已经人工验收。

## 用户确认的交互原则
- 黑、白、灰为主要视觉；绿色操作成功/确认，红色取消及危险动作，必要时黄色警告；所有页面中文界面。
- **确认按钮位于左边且为绿色，取消按钮位于右边且为红色**；调用统一 `ConfirmDialog` 禁止每个页面自行发明按钮顺序。对不可恢复危险操作还需要具体说明后果、二次确认，不得因为绿色按钮让用户误解安全性。
- 左侧导航固定，不跟随内容滚动；页面切换 React 内部状态完成，WebView2 不整页重新加载；禁止因搜索、保存触发背景闪黑闪白。
- 重要提示保留手动关闭入口，不靠 2 秒自动消失；一次仅显示一个 Toast，避免多个弹窗叠加。
- 不依赖颜色作为唯一状态信号：文字标签与图标/语义配合；不要把“红色”当作已执行动作成功的依据。

## 权威样式文件
- `src/web/design/tokens.css`：主题色、全局字体、间距、圆角、控件高度、层级、动画速度。所有页面引用变量，禁止复制私有颜色规则。
- `src/web/design/primitives.css`：按钮、输入框、原生 select、多行输入、标记、弹窗和 Toast 的基础样式。
- `src/web/design/tokens.ts`：主题 RGB 色值及 WCAG 对比度计算，用于自动化验证。
- `apps/ui/src/components/DesignSystem.tsx`：`ActionButton`、`StatusPill`、`TextField`、`SelectField`、`ConfirmDialog`、`ToastViewport`。
- `apps/ui/src/components/DesignShowcase.tsx`：设置页面中的交互预览，只测试外观/交互，不读写真实数据。
- `apps/ui/src/style.css`：全站主题导入和主窗口布局，不维护第二套通用按钮。
- `apps/ui/src/design.test.tsx`：组件 SSR、对比度、弹窗按钮顺序、表单标签/辅助文本及 Toast 可访问性检查。
- `tests/UI/Visual/check-design.mjs`：CSS 主题、不重复硬编码、响应式及键盘焦点约束的静态验证。

## 主题标准
| 元素 | 使用变量 | 说明 |
|---|---|---|
| 页面最底层 | `--surface-root` | 一贯暗色，不允许加载时白底 |
| 侧栏 | `--surface-nav` | 固定、独立滚动 |
| 卡片 | `--surface-panel` | 深灰、可辨识边界 |
| 文字 | `--fg-primary`、`--fg-muted` | 主文案对比度确保可读 |
| 肯定/确认 | `--action-success` | 浅绿色底、深字，左侧 |
| 取消/危险 | `--action-danger` | 红色底、深字，右侧 |
| 键盘焦点 | `--action-focus` | 显著描边，Tab 可见 |
| 微动效 | `--motion-fast` | `prefers-reduced-motion` 下关闭 |
| 弹窗 | `--z-dialog` | 单例控制；对话框有 aria-modal 和焦点保持 |
| 提示气泡 | `--z-toast` | 单一消息可手动关闭，覆盖时替换 |

所有输入框必须有 `label` 与 `htmlFor` 关联；错误提示必须通过 `aria-describedby` 关联；优先使用原生 `select`，保障滚轮和键盘操作。常用控件至少 40px 高，窄屏不应出现横向溢出。

## Modal 生命周期约定
由父页面控制一个 `open` 状态；点击确认发出调用者自行校验的动作，取消不触发提交。打开时焦点移到确认按钮，Tab 在弹窗内循环，Esc 等同取消（busy 状态拒绝 Esc 退出），关闭后焦点回原触发控件。禁止叠加多个模态窗口。用户当前未发起危险操作，因此预览只弹出安全确认示例。

## 视觉回归的分层验证
1. 可自动化：React JSX/SSR 快速检验、对比度阈值、CSS 设计变量、窗口 10 项几何测试、CI Linux/Windows 前端构建。
2. 浏览器真实渲染：在后续 UI 集成测试阶段完善视觉截图与基线像素差异检测，特别检查 100%、125%、150%、200% DPI 和中文换行。
3. 用户最终实机集中验收：WebView2 Win10/11 暗色首帧、对话框 Tab/ESC、提示位置、多显示器、窗口缩放及不闪屏；在此之前不能宣称真人视觉测试已经通过。

## 版本纪律
本文件和 token 命名作为后续 07–24 步界面基础，新增通用控件必须复用设计组件。修改全局变量需要更新颜色可读性测试、回归测试与变更记录。禁止为了 CI 通过而关闭无障碍检查。
