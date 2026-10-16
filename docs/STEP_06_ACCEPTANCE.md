# 第 06 步：深色 UI 设计系统 — 开发阶段验收

日期：2026-10-16；唯一仓库：`mosen6266-netizen/MS_001`。
**结果：DEV PASS；实际 Windows 10/11 WebView2 交互验收 DEFERRED。**

## GitHub 自动化证据
- [Actions Run 38926182509](https://github.com/mosen6266-netizen/MS_001/actions/runs/38926182509)：**completed / success**，7 个 job 全部成功。
- 前端：3 个 Vitest 文件、**15 项单元测试通过**（既有路由/IPC 以及色彩对比度、确认弹窗左绿右红、输入关联、Toast单例）。
- `tests/UI/Visual/check-design.mjs`：**16 项设计和 CSS 静态检查通过**。
- 几何：Windows/Linux 各 **10 项测试通过**；数据库迁移：Windows/Linux 各 **27 项回归测试通过**。
- Windows x64 WPF 云端编译与发布：success（不是用户本机安装测试）。
- Chromium 无界面截图：桌面 1440px、窄窗口 700px 两幅 PNG 已生成并手工查看布局；自动截图环境缺少中文字体引发方框字，已通过安装 Noto CJK 字体修复并复查中文正确渲染。截图 artifact：`ms001-step06-visual-snapshots`，ID `12057250812`，GitHub 临时产物会到期。
- 这两幅截图只覆盖初始工作台；完整设置预览弹窗/按钮的浏览器交互及视觉像素差异基线目前未自动校验，保留给后续 UI 集成/最后真实机器验收。

## 本步提交
- `src/web/design/tokens.css` 与 `primitives.css`：共享深色设计令牌、字体/间距/圆角/焦点/模态/提示。
- `src/web/design/tokens.ts`：对比度计算与自动阈值测试。
- `apps/ui/src/components/DesignSystem.tsx`：ActionButton、TextField、SelectField、StatusPill、ConfirmDialog、ToastViewport 等复用组件。
- `apps/ui/src/components/DesignShowcase.tsx`：设置页面的离线 UI 演示区，不保存任何账号或群数据。
- `apps/ui/src/App.tsx` 与 `style.css`：统一主题接入、原有固定侧栏/内容滚动不重载、品牌区接入用户上传图形的 SVG 转换版。
- `apps/ui/src/design.test.tsx`、`tests/UI/Visual/check-design.mjs`、`.github/workflows/ci.yml`：自动化测试和 Linux Chromium 截图。
- `docs/UI_DESIGN.md`：面向后续页面可复用的交互与无障碍规范。

## 强制行为
- 确认在左且绿色、取消在右且红色；Dialog 支持 Tab 限制焦点与 Esc 取消；忙碌状态禁止重复点击。
- 成功/错误/警告有语义文本与颜色；Toast 只有一个展示槽，手动关闭，不叠加。
- 原生 select 支持滚轮/键盘；文字输入有显式 label；暗色首帧和 40px 最小可用控件高度。
- 真实 Telegram 登录、群组发送、Cloudflare 许可均未在本步运行；Signal MS002 不受影响。

## 发布前仍须验收（未通过不能宣称正式版验收）
- Windows10/11 实机 DPI 100/125/150/200%、屏幕阅读器/键盘焦点、窗口切换时视觉闪白/闪黑。
- 实际完整弹窗、Toast、滚动下拉框在 WebView2 的交互效果与高分辨率截图基线。
- 用户图标/Telegram 相似标记需在对外正式发布前检查相关使用规则与许可，不暗示软件来自 Telegram 官方。

**结论：Step06 = done_dev；Step07 = not_started；开发进度 6/32 = 18.75%，剩余 26 步。**
