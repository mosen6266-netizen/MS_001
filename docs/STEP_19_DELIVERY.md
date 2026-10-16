# Step19 — 持久发送回执、去重与未知结果保护

## 阶段目标与边界

- 本步新增 `src/Infrastructure/Delivery/DeliveryJournal.cs`、`DeliveryModels.cs`、SQLite v8 `delivery_attempts` / `delivery_events`、跨平台 `tests/Delivery`。
- **当前仍是可信客户端回执接口的离线模拟，不是实际 Telegram 发送。** 没有 TDLib 网络调用、WebView2 出站写 IPC、会话导出或后台发送线程。
- 早期 v2 `runs/run_steps/outbox` 不被误当成可恢复真实任务；Step19 新增独立的本地投递日志，不写入消息文本、照片、手机号、密码、账户会话或第三方密钥。

## 消息事务身份与校验

1. 第17步模拟作业必须确实处于 `Running / AwaitingSend`，且 Step18 的唯一 `OutboundPermit` 须与当前步骤键、账户GUID、群ID、话题ID以及运行ID全部严格匹配。
2. `Register` 在**任何未来网络提交前**，通过 SQLite 原子事务持久化 `Planned`，分配 `attempt_id`，并用原许可的 GUID 作为 `correlation_id`。数据库 `UNIQUE(run_id,step_key)` 禁止同一步多次尝试，换另一个许可也不得重新发送。
3. `BeforeSubmit` 原子写入 `Submitting` 后才能由未来可信传输适配器尝试一次送出；再次调用返回禁止重发。后续第18步策略锁须保持，直到明确官方完成。
4. `MarkPending` 仅代表传输侧已受理，**不代表 Telegram 已送达**。可信官方回执 `Confirm` 同时核对相关令牌、账户、群、话题、有效平台消息ID和可信来源标识，才写入 `Confirmed`。发布的消息ID按账户×群唯一；重复相同回执幂等，矛盾或挪用消息ID禁止。
5. 官方确认早于 `Pending` 时允许从 `Submitting` 直接确认；随后迟到的 `Pending` 不会回退 `Confirmed`。在 `Unknown` 后抵达且全部匹配的可信官方确认，允许安全解决待复核事务。
6. 经过明确定义且可信的“提交前本地拒绝/平台明确未接收”可以标为 `Failed`；网络断开、超时、未验证响应必需标为 `Unknown` 或 `Review`。状态不明绝不自动补发，亦不可凭推测标记确定失败。
7. 崩溃重启对 `Planned` 转 `Review`，对 `Submitting/Pending` 转 `Unknown`；原 `Unknown`、`Confirmed`、`Failed` 保持。不可恢复时静默重试实际发消息。
8. 可自动重试的只有纯读取型操作 `read-message-status` / `read-chat-permissions` / `read-auth-state`。消息或图片提交不在重试白名单。

## 数据库结构

- `delivery_attempts`：冻结的运行ID、步ID、账户ID、群、话题、唯一相关ID、状态、官方消息ID、脱敏诊断码、乐观修订版本、时间。
- 唯一约束：`UNIQUE(run_id,step_key)`、`UNIQUE(correlation_id)`、`UNIQUE(account_id,chat_id,platform_message_id)`（仅消息ID非空时）。
- `delivery_events`：按事务追加状态转换审计，仅固定英文诊断码，不存原始 Telegram 异常正文和消息文本。
- 单一事务提交状态 + 对应审计事件；异常或 SQLite 约束冲突不产生伪确认事件。
- 该日志是**离线投递历史与恢复门禁**，不允许被直接作为网络发送队列。下一步20处理意外退出与电源/网络恢复。

## 仍待集成 / DEFERRED

- 正式 TDLib 接口提供真正校验过的官方消息回执以及请求相关性，服务端限流与授权的实时刷新；目前 `SourceVerified` 是内部的可信端口占位，**绝不可从网页 IPC 或普通 UI 文本直接伪造为 true**。
- 第18步单账号锁持有/释放与本日志的事务原子集成、旧正式 `runs/outbox` 转生产级持久化、客户端 UI、Win10/11 安装/进程强退测试及真实合法群发送，仍属于后续集成和最后用户集中实测。
- Step19 开发测试只使用虚构 GUID、群号和平台消息ID，不涉及 MS002、Cloudflare 生产资源。
