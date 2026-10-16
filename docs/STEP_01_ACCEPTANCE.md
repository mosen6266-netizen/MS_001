# 步骤 01 — 需求与许可范围冻结：验收记录

**阶段**：1/8；**对应计划**：`docs/MASTER_PLAN_32_STEPS.md` Step 01。
**执行日期**：2026-10-16。**验收对象**：文档，不含任何运行代码、账号授权或真实群发消息。

| 核对项 | 结果 | 证据 |
|---|---|---|
| GitHub唯一目标仓库、旧MS002不修改 | PASS（文档层） | `README.md`、`docs/DECISIONS.md` |
| P0 / P1 / P2 完整列出且可追踪 | PASS | `docs/REQUIREMENTS_MATRIX.md` |
| 已明确非目标、账号及群授权边界 | PASS | `docs/NON_GOALS.md` |
| 安全关键行为已定义 | PASS（设计基线） | `docs/EDITOR_AND_ROLES.md`、`docs/TEST_MATRIX.md` |
| 30s+5s 严格顺序 | PASS（规格，代码未运行） | `docs/REQUIREMENTS_MATRIX.md`、`docs/TEST_MATRIX.md` |
| Win10 生命周期与风险说明 | PASS | `docs/NON_GOALS.md`、`docs/PRODUCT_BASELINE.md` |
| 平台与商标风险显式记录 | PASS（评审规划） | `docs/NON_GOALS.md`、`docs/DECISIONS.md` |
| 工作名称与基线范围 | PASS（按用户“开始吧”同意此前方案继续） | `docs/DECISIONS.md`；商标权利留作正式发布门禁 |
| TDLib/Windows兼容及300/50容量已实机通过 | NOT_RUN（不属于本步骤） | Step 02、Step 30 执行 |

## 验收方式
1. 逐文件复查存在性、P0/P1/P2 三等级、需求 ID 唯一性、01–32 计划条目完整性。
2. 检查关键安全文本（许可、禁止绕过限流、未知消息不可自动重复发送、MS002隔离、Win10安全风险）。
3. 检查 `docs/TEST_MATRIX.md` 的时间测试 TIME-01，明确第30秒才“正在输入”、第35秒才调用发送。
4. 更新 `state/project_state.json` 和 `state/steps.json`，记录步骤 01 为已验收，下一步 02，完成进度1/32=3.125%。
5. 上述 PASS **仅表示需求文档合格**；尚未开展软件功能/Windows/TDLib测试，不能误称产品功能通过。

## 未封闭但不阻塞此步骤的发布前问题
- 公开发行前完成商标/图标合法使用审查；目前仅存用户提供图形的透明SVG转换文件。
- 仓库当前为public；私有化若需要须由仓库所有人确认操作，不能把将来的生产秘钥上传。
- 后续真实Telegram授权和群发送须在合法账号与测试群进行，不会在本步骤自动执行。
- Windows10兼容性须测试；官方常规支持2025-10-14已结束。
