# 第 08 步开发验收 — 客户端本地安全及诊断基础

日期：2026-10-16；仓库：`mosen6266-netizen/MS_001`。
**结果：DEV PASS / 最终真实Win10/11和Telegram会话验收 DEFERRED。**

## 证据
- [GitHub Actions CI 38930950275](https://github.com/mosen6266-netizen/MS_001/actions/runs/38930950275)：`completed` + **`success`**，11个job全部成功。
- 安全/诊断：Windows **26/26** 个断言通过（含DPAPI CurrentUser加解密、篡改拒绝）；Linux **24/24** 通过（不支持Windows DPAPI时明确拒绝）。
- 前端 6个 Vitest文件，**24/24** 单测通过；CSS/可读性16个静态检查PASS。
- SQLite迁移Windows/Linux各27项、SQLite设置Windows/Linux各16项、窗口几何Windows/Linux各10项仍PASS；Windows WPF云端`dotnet build` + self-contained publish PASS；Chromium桌面1440/窄屏700初始页面截图检查PASS。
- 日志测试：写入1200次代码枚举事件，使用2KiB测试滚动阈值**确实触发**轮转并只保留最近两份；原始异常中诱饵密码/手机号内容没有进入JSONL；生产阈值仍256KiB/文件。

## 源码
- `src/Core/Security/DpapiProtector.cs`：Windows CurrentUser DPAPI，版本化密文封套；仅用于将来安全凭证接口，当前无真实凭据落盘。
- `src/Core/Security/IpcRequestGuard.cs`：严格大小/JSON结构/重复字段/协议/请求ID/五个允许命令和settings.save负载范围校验，先校验后执行。
- `src/Core/Telemetry/SafeDiagnostics.cs`：只有固定事件代号和分类的JSONL日志、当前进程内存/线程/句柄及启动时长计数、滚动留存；禁止异常消息/聊天数据/手机号作为字段。
- `apps/desktop/MainWindow.xaml.cs` 与 `App.xaml.cs`：WebView2来源校验、拒绝非法IPC、生命周期和崩溃记录固定类别/安全提示。
- `apps/ui/src/components/DiagnosticsPanel.tsx`：只读健康显示，浏览器预览不会假装有宿主数据。
- `tests/Security/Program.cs`、`apps/ui/src/diagnostics.test.tsx`：自动化安全与泄漏回归。
- `docs/STEP_08_SECURITY.md`、`docs/SECURITY_THREAT_MODEL.md`：防护与遗留边界。

## 已修复缺陷
- 初版WPF集成中引用诊断组件但组件尚未提交，导致UI构建失败；后续补齐。
- 前端IPC的`diagnostics.get` TypeScript联合类型已扩展，但执行验证白名单遗漏该命令，导致测试失败；修复后全量通过。
- 原始日志单测未实际触发滚动；改成可注入小容量测试阈值并验证两份留存，重新跑全套CI PASS。
- 不曾通过关闭测试、隐藏构建警告或绕开安全门槛推进状态。

## 尚未完成且不能冒称已通过
- **尚未把DPAPI接到真实TDLib会话数据库或账号登录**；目前只是加解密原语和安全本地日志结构，不代表300账号会话安全/可运行。
- 未验证真实Win10/11用户目录ACL、跨用户DPAPI解密拒绝、系统崩溃转储隐私、磁盘满/杀软、更新路径权限、TDLib原生异常、WebView2跨域攻击；这些列入最终用户实机验收及后续开发设计。
- 当前没有真实Telegram账号交互、消息发送或Cloudflare许可服务部署；旧Signal MS002未修改。

**开发Step08 = done_dev**，下一步Step09 = not_started（TDLib通信适配层）；总开发进度 **8 / 32 = 25%**，剩余24步。
