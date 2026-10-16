# Step10 开发阶段验收 — Telegram 登录与身份验证状态机

日期：2026-10-16；仓库：`mosen6266-netizen/MS_001`。
**结论：DEV PASS / Telegram原生DLL与真实用户登录验收 DEFERRED。**

## GitHub Actions 权威证据
- [CI Run 38935436769](https://github.com/mosen6266-netizen/MS_001/actions/runs/38935436769)：status=`completed`、conclusion=`success`，**15个job全部SUCCESS**。
- `authorization` Windows **94/94**，Linux **94/94** 断言全部PASS，使用模拟 `IAuthGateway` + Fake TDLib native transport，无真实手机号/验证码/登录/网络操作。
- 前端 7个Vitest文件、**26/26** UI 单元测试PASS（包含新增中文授权状态界面预览），共享设计CSS 16项静态检查PASS。
- Step09 TDLib模拟transport Windows/Linux各118项原有测试继续PASS；WPF Windows x64云端编译和自包含publish成功；SQLite迁移/用户设置/安全诊断/窗口几何与Chromium初始页截图回归全部绿。
- 实现期间发现新的测试文件 C# const缺少类型声明及字典初始化括号错误；修复后CI全绿。对取消与进程最终关闭的同一client重复`close`增加严格去重，并专门增补单元断言后复测成功。
- 此CI不构建/加载官方tdjson.dll，不能冒称真实扫码登录、邮箱验证、两步密码或Windows本机真实运行已通过。

## 实际交付
1. `src/Features/AccountAuth/AuthState.cs`：13种当前TDLib官方授权状态及未知状态的中文说明；QR 临时链接只驻留内存快照，不出现在对象调试字符串中。
2. `AuthSession.cs`：按clientId隔离登录状态、严格状态/输入校验、二维码只启动一次且以官方事件刷新、输入并发独占、取消优先、迟到授权不重新生效、状态Ready仅由官方update触发。
3. `src/Infrastructure/Telegram/TdlibAuthRequest.cs`：白名单TDLib登录指令序列化，包含`setTdlibParameters`加密key、`requestQrCodeAuthentication`、手机号、验证码、邮箱、邮箱验证码、两步密码和`registerUser`的正确官方JSON类型；严禁从UI直接透传任意TDLib命令。
4. `TdlibAuthGateway.cs`、`TdlibDispatcher.cs`：与原有单接收循环及 `@extra + @client_id` 关联服务对接，授权取消使用`close`而非`logOut`，所有关闭请求去重。
5. `apps/ui/src/components/AuthStatusPreview.tsx`：账号页面提供中文授权状态演示及官方Premium/未知状态说明；**登录、扫码、验证码实际输入/提交按钮仍禁用**，无真实 TDLib/WebView2 auth IPC。
6. `tests/AccountAuth/Program.cs`、`apps/ui/src/auth-preview.test.tsx` 与 `.github/workflows/ci.yml`：Windows/Linux双平台对13状态和未来兜底、重复QR/权限/超时/429/取消/500并发、TDLib 9种JSON指令、安全ToString与Close去重的完整回归。
7. 设计/安全交接 `docs/STEP_10_AUTH.md` 和 `docs/STEP_10_SECURITY_TEST_MATRIX.md`，供新对话第11步直接接续。

## 严格保留的正式发布阻断
- 用户本人在实际Windows10/11电脑执行：真实tdjson.dll（官方锁定来源1.8.67）加载、API id/hash获取与DPAPI安全注入、手机号短信或二维码真实授权、2FA/邮箱场景、登录失败/重新扫码、取消后官方确认关闭、重启无重复扫码。
- TDLib真实加密会话目录与数据升级仍要在第11步实现；本阶段仅临时内存模型，未保存真实登录会话、未向Telegram网络发送任何请求。
- 真实WebView2输入表单/QR图片/错误提示、不同DPI与系统账号权限仍未实机验证。
- 账号容量300与任务50只为未来目标；**不能把模拟客户端当作已经登录的账号**。Telegram平台限制、账号使用权限与频率要求必须遵守。
- 旧Signal MS002、其授权系统和数据完全不受本次提交影响。

**Step10 = `done_dev`；Step11 = `not_started`（账号注册表及会话生命周期）；开发进度 10/32 = 31.25%；剩余22步。**
