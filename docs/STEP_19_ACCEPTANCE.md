# 第19步开发阶段验收｜持久事务、官方回执关联与去重

日期：2026-10-16，唯一仓库 `mosen6266-netizen/MS_001`。**结论：done_dev，下一步 Step20。** 不是正式版真实消息投递通过。

## 可核实证据

- [GitHub Actions Run 38960129236](https://github.com/mosen6266-netizen/MS_001/actions/runs/38960129236)：**completed / success，30/30 项作业成功**，全部旧回归测试绿色。
- `tests/Delivery/Program.cs` 在 **Windows 64/64、Linux 64/64**：包含新事务唯一约束、关联令牌、提交前持久化、可信账号群回执匹配、重复/矛盾回执、官方确认与Pending乱序、两笔消息复用平台ID阻断、意外断网与超时、后到可信确认、重启未知隔离、只允许读取类自动重试与旧 SQLite 一致性。
- 初次 CI 发现测试脚本六处 `var` 多变量编译错误（CS0819），并非验证成功；已将它们分离声明，完整重跑后通过。
- [核心提交 `dcb242c1`](https://github.com/mosen6266-netizen/MS_001/commit/dcb242c1e1d67699ee649c394542d5031a5b5515)；[修复提交 `a4f44951`](https://github.com/mosen6266-netizen/MS_001/commit/a4f44951461ccb56ca211c229d354093c0023196)。

## 已开发模块

- SQLite v8 `delivery_attempts` 与 `delivery_events`，每个运行/消息步唯一事务、每个 correlation id 唯一，账号×群×平台消息ID 唯一，带修订版本与安全事件码；无消息正文、密钥、手机号和登录会话。
- 严格状态 Planned → Submitting → Pending → Confirmed；未知结果 Unknown，明确拒绝 Failed，重启未提交意图 Review；官方 Confirmed 可先于 Pending 关联，迟到 Pending 不得回退状态。
- 任意一次消息不能因同一步二次注册、超时、重复回执、无依据失败而盲目重发；未知事务只能结合人工复核和准确可信的后到回执解决。
- 安全重试白名单只允许读取型查询，消息/照片实际提交不在白名单。

## 尚未执行的真实验收（DEFERRED）

- 现在的 `OfficialDeliveryReceipt.SourceVerified` 是后续**仅由可信TDLib适配器提供**的契约，不可把WebView/UI任意请求标记成已证实。
- 真正TDLib网络出站、服务端回执关联、Step18账号安全锁集成、正式任务outbox、可控断网/崩溃/休眠、Windows10/11安装体验、合法群50路任务和72小时长稳，都未由本次自动化测试验证。第20步接下来完成断网/重启恢复开发，最终集中真实机器测试。
- MS002生产应用、现有Cloudflare卡密及任何真实密钥均未修改。

**进度 19/32（59.375%），剩余13步，下一步20。**
