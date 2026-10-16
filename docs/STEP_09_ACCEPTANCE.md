# Step09 开发阶段验收：TDLib JSON 通信适配层

日期：2026-10-16；仓库：`mosen6266-netizen/MS_001`。
**结果：DEV PASS（模拟与编译验证）／官方 TDLib 原生 DLL 构建、真实加载与账号登录验收 DEFERRED。**

## GitHub 真实证据
- [GitHub Actions Run 38933092843](https://github.com/mosen6266-netizen/MS_001/actions/runs/38933092843)：`completed` / **`success`**；全部**13个 job 成功**。
- TDLib Fake Transport 适配器：**Windows 118 / 118、Linux 118 / 118** 个断言通过；没有加载 tdjson.dll、没有 Telegram 网络活动、没有真实账号/消息操作。
- 前端 UI/样式静态检查、Windows WPF 编译与 self-contained publish、Windows/Linux SQLite迁移、设置持久化、DPAPI/诊断与窗口几何历史回归全绿。
- 初版测试源存在 C# 内插 raw 字符串闭括号冲突（CS9007）、对模拟数组误用 Count 属性（CS0019），分别修复、重新运行完整 CI 通过。后续补强确保 `td_receive` 仅在首个请求已发出后执行；成功 CI 对应最终源码更改后的完整流水线。

## 代码及实现
- `src/Infrastructure/Telegram/TdlibTransport.cs`：官方现代 TDLib C JSON ABI 的 P/Invoke 声明（td_create_client_id、td_send、td_receive），Windows x64、进程仅一个 native receiver；复制UTF-8返回指针内容，不跨下次 receive 保留 native pointer。
- `src/Infrastructure/Telegram/TdlibDispatcher.cs`：支持多个逻辑 client ID 的**单一接收循环**；`@extra` + `@client_id` 同时匹配响应；收到事件顺序写入有界256条队列，满后背压而非无限堆积；未知客户端/无效JSON隔离；读取超时/取消清理待决项，迟到回执不当作更新、绝不自动重发。
- 本阶段只公开 `getOption(version)` / `getAuthorizationState` 两种只读控制请求，`DisposeAsync` 发送 `close` 并限时等待 `authorizationStateClosed`，不能将超过宽限期的客户端冒称正常关闭。
- `tests/TelegramAdapter/Program.cs`：两个合成客户端的逆序响应、跨账号ID注入隔离、100条严格顺序更新、垃圾/未知客户端丢弃、请求超时/取消、关闭每个 client、一千毫秒内退避、满队列背压停止、未知命令拒绝等。测试纯模拟，不要推断真实TDLib联网或多账号登录性能。
- `native/tdlib/source-lock.json` 锁定官方源 commit `bc9c263e2bfee06aaab41e82db51a103376030bc`（1.8.67），官方页面已核实确实是更新至 v1.8.67 的提交。**没有声称生成过 win-x64 tdjson.dll，更没有DLL SHA256或实际二进制。**
- `docs/STEP_09_TDLIB.md` 和 `native/tdlib/README.md` 明确约束下阶段认证、原生加载、DLL签名/依赖与发布检查。

## 明确延期且不可误标为 PASS
1. Windows原生 `tdjson.dll` 构建与固定SHA256、官方仓库固定版本构建依赖审查、DLL导出函数真实加载、UI完整打包内置。计划结合第26–28步分发阶段补齐，属于真正正式版阻断项。
2. 自有账户授权和实际 TDLib 网络事件：下阶段10–12实现用户授权状态机与会话存储；实际使用自有账号实机验证按用户要求集中最后测试。
3. 真实 300 个会话资源、50群任务并发、崩溃恢复、CPU/RAM/句柄/网络表现：仍无数据可宣称通过，属于后续/终版门禁。
4. 此适配器尚未接入 WPF GUI的真实登录入口，更不会调用发送消息API。没有真实 Telegram 会话/卡密配置写入 public 仓库，旧 Signal MS002 未修改。

**Step09 `done_dev`；Step10 `not_started`。32步开发完成 `9/32 = 28.125%`，剩余23步。**
