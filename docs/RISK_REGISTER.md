# Step02 — risk register (updated 2026-10-16)
| Risk | Severity | Detection | Mitigation and verification | Status |
|---|---|---|---|---|
| Git tree commit accidentally erases files | **P0** | main tree contains only subset of expected files | Always create_tree with base_tree_sha and compare file-tree manifest after every commit; 2026-10-16 restoration \`c1561ca...\` | Incident resolved; regression guard needed |
| TDLib old C JSON API deprecation | **P0** | Old td_json_client_* functions disappear in 2.0 | New native interface \`td_create_client_id\`, \`td_send\`, \`td_receive\`; single owner receive loop, orderly close | Modern ABI documented; Windows CI compile PASS; native DLL load pending |
| Real TDLib per-account RAM/handles unexpectedly high | **P0** | 300 accounts + periodic reconnections degrade host | On-demand session supervisor; ramp 1/5/20/... accounts; isolate native process/shard; collect real Windows metrics | Actual measurements pending |
| Real platform limits on many accounts/tasks | **P0** | Telegram restrictions, FLOOD_WAIT, permissions | No bypass/rotation; per-account global gate and respect platform waits; operate only in authorized groups | Platform-dependent |
| Windows WPF/WebView2 runtime missing | **P1** | Startup fails on clean Win10 or LTSC | Packaging strategy with runtime prerequisite/detection and offline fallback installer | Windows test pending |
| Native tdjson DLL load / bitness / dependent DLL errors | **P0** | DllNotFound, BadImageFormat, entrypoint mismatch | Pin official build / SHA / license / dependencies; Win10+11 native smoke tests | Pending |
| Session storage and credential leakage | **P0** | Public repo, crash logs, backups expose credentials | No secrets in repo; encrypt local sessions and backups; redaction | No secrets used in spikes |
| Duplicate send on uncertain network outcome | **P0** | Request timed out but Telegram accepted message | Outbox result-unknown holds task until reconciled; never blind retry | Requires later implementation |
| Clock drift/sleep affects 30+5 sequence | **P0** | Task resumes early or sends in wrong order | Monotonic clock, separate stages, pause/resume tests | Steps 13–20 |
| Platform brand/logo confusion | **P1** | User-supplied paper plane closely resembles Telegram mark | Product branded MOSEN; transparent graphics; trademark approval gate | Pending release audit |
| Public repo exposes proprietary project | **P1** | MS_001 visible Public | User may set Private in GitHub; repo commits contain public-safe code/docs only | User decision |
| No Windows/macOS GUI runtime in current executor | **P0 for Step02 exit** | uname=Linux; dotnet command absent | Windows-runner build plus user-owned clean Windows runtime/test account | Blocked real-machine check |

Definitions: P0=blocks safe acceptance or may cause integrity harm; P1=important mitigation before production. All statuses refer only to documented evidence, not assumptions.

## Step02 CI 记录
- Windows runner WPF+WebView2 构建已通过（[Actions Run 38919353228](https://github.com/mosen6266-netizen/MS_001/actions/runs/38919353228)）；尚未在Win10/11实际启动、授权或执行 TDLib native DLL。
- Python 300/50 模拟任务在同一run通过，不能据此宣称300账号真实在线。
