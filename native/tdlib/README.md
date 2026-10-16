# TDLib 原生依赖锁定与构建门槛

- 上游官方项目：https://github.com/tdlib/td ，Boost Software License 1.0。
- 已核实**官方 1.8.67 源代码提交**：`bc9c263e2bfee06aaab41e82db51a103376030bc`。该 commit 属于官方仓库并将项目版本更新为 1.8.67。用于本开发分支的来源锁定，存于 `source-lock.json`。
- 本阶段使用官方 TDLib 新 JSON ABI：`td_create_client_id`、`td_send`、`td_receive`。官方要求 process-global receive 不可由不同线程并行调用、按接收顺序处理更新。C# 适配层复制返回的 UTF-8 内容，因为 native pointer 仅保证在下次 receive/execute 前有效。
- **阶段09不把未经验证第三方 tdjson.dll 复制到公开仓库**。当前 mock 单元测试不加载 native DLL，也不访问 Telegram 网络。
- 原生二进制生产门槛：Windows CI 从锁定的官方 SHA 检出源代码；固定 CMake/编译器/依赖版本完成 win-x64 Release；验证 TDLib 导出函数、SHA256/文件大小与依赖清单，执行真实 native tdjson.dll 加载及 `getOption(version)` 无账号 smoke；审查安全公告后绑定安装程序。这个门槛将随第26–28步 Windows 分发及第30–31步验证验收。
- 源码能构建并不代表 300 个真实用户账号、50 个群任务、关闭后原生无句柄泄漏已验收；需要正式实机测试。
- 新对话助手不得把本文件的 source pin 误写为“DLL已编译并发布”。源码未下载到当前仓库，也没有二进制 hash，直到构建与审核完成。

官方 C 接口文档：https://core.telegram.org/tdlib/docs/td__json__client_8h.html
