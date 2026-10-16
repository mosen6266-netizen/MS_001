# Step 02 — critical technology spikes and test evidence
Date: 2026-10-16 | Target repo: \`mosen6266-netizen/MS_001\` | Status: **IN_PROGRESS — not yet accepted**

## Evidence summary
| Gate | Method | Outcome | Interpretation |
|---|---|---|---|
| Restore missing GitHub project docs | Compare trees of historical/main commits and fast-forward merge missing blobs | **PASS** — restoration commit \`c1561ca471a79546acdcbbf857e1b6039a336391\` | Commit helper must always include base_tree_sha |
| TDLib modern JSON ABI | Read [official td_json_client.h](https://core.telegram.org/tdlib/docs/td__json__client_8h.html) | **DOC CONFIRMED** | Prefer \`td_create_client_id / td_send / td_receive\` (old td_json_client_* deprecated ahead of TDLib 2.0); updates processed in receive order, never parallel calls to td_receive |
| TDLib initialization | Read [TDLib getting started](https://core.telegram.org/tdlib/getting-started) | **DOC CONFIRMED** | Auth state via updates; setTdlibParameters needs Telegram API ID, hash and writable per-account database directory; not available for actual login test |
| WPF + WebView2 on Windows | Review [Microsoft tutorial](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf) and [runtime distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution) | **WINDOWS CI BUILD PASS; runtime GUI NOT_RUN** | Probe under \`spikes/step02/wpf/\`; WebView2 runtime must be packaged or installed and validated on clean PCs |
| Account/queue capacity simulation | \`python spikes/step02/capacity_simulation.py\` | **PASS (synthetic only)** | Linux x86_64 / CPython 3.13.5: 300 synthetic metadata records, 50 group actors, 10,000 synthetic messages; 0 actual Telegram sessions or messages |
| Synthetic run metrics | Python \`time.perf_counter\` + \`tracemalloc\` | **PASS (local simulation)** | 0.16659 s elapsed, peak Python tracemalloc ~0.1878 MiB, per-account peak concurrency 1; NOT physical RAM/CPU cost of real TDLib clients |
| 300 actual TDLib sessions and restarts | Authorized real-user / native Windows test | **NOT_RUN** | Needs actual authorized sessions, native tdjson DLL, Windows host and hardware budgets |
| One owned account login + restart | TDLib test mode or controlled owned account | **NOT_RUN — BLOCKER for Step 02 acceptance** | Requires own developer API credentials, x64 native library, real Windows test environment and consent |
| Windows 10/11 WPF UI runtime test | Real machine / clean virtual machine | **NOT_RUN — BLOCKER** | Current environment is Linux, no dotnet CLI installed |
| 50 real Telegram group execution | Authorized test groups respecting platform constraints | **NOT_RUN** | This is Step 30 long-run/capacity acceptance, not an assumption made by this synthetic test |

## Operating system and packaging
- Linux x86_64 is not a valid WPF runtime target; local \`command -v dotnet\` returned no executable.
- WebView2 supports a number of Windows 10 builds and Windows 11, but Microsoft ended regular support for Windows 10 on October 14, 2025. Compatibility does not equal ongoing OS security support.
- WebView2 NuGet SDK version candidate pinned for compilation spike: \`1.0.4258.31\`, published 2026-09-28; production pin requires real Windows 10/11 GUI runtime checks and security review.
- TDLib is native C++, dynamic libraries and versions must be pinned. Avoid shipping unreviewed binaries.
- Verified CI run: [38919353228](https://github.com/mosen6266-netizen/MS_001/actions/runs/38919353228) **SUCCESS**, 2026-10-16, runs Python synthetic-capacity on Ubuntu and builds WPF .NET 10 project on Windows hosted runner. Compilation passed, **not** native tdjson load or interactive GUI.
- First CI run [38919305431](https://github.com/mosen6266-netizen/MS_001/actions/runs/38919305431) **FAILED** due to a P/Invoke attribute issue (`DllImport` incorrectly using `StringMarshalling`). Corrected to `[MarshalAs(UnmanagedType.LPUTF8Str)]` and reran successfully. Fixed commit: `7ec32cb9c63c8e334a10d633d84e863203228103`.
- The GitHub Actions job \`.github/workflows/step02-tech-spike.yml\` can verify compiling (if Actions is enabled) but does not prove execution on Win10/Win11 desktops.

## Step 02 exit requirements — do not mark done prematurely
1. Windows x64 WPF/WebView2 probe built and actually launched on Win10 & Win11, with runtime presence and absence cases documented.
2. Verified tdjson native x64 load and \`td_receive\` event stream using modern ABI; library/API version and SHA/loader dependencies recorded.
3. Single personally authorized Telegram account login and app restart test under controlled conditions; no extra accounts or group sends.
4. Preliminary CPU/RAM/handle/disk footprint recorded from actual Windows native TDLib host with session fully closed, plus hardware specs.
5. Security/architecture risks reviewed; acceptance details captured in this document with real logs and reproducible steps.
Only after these pass can \`state/steps.json\` step 02 become \`done\` and progress advance from 1/32 to 2/32.

## Observed limitation and next execution
Current Linux container cannot host WPF UI or perform Windows native TDLib login. Await controlled Windows x64 environment and valid **privately supplied** Telegram developer credentials through a secure channel; never request that secrets be posted in a public GitHub issue, commit, or chat if avoidable. Keep status \`in_progress\` / blocked on environment until evidence exists.

## Gate status after CI
- [x] Source code compiles on a hosted Windows runner (CI build PASS)
- [x] Synthetic 300 registry / 50 queues test passes locally and on GitHub Actions (NO Telegram traffic)
- [x] Historical Git tree omission corrected and current tree integrity checked (30 files after source upload)
- [ ] Real Win10 x64 GUI launch/WebView2 runtime detection
- [ ] Real Win11 x64 GUI launch/WebView2 runtime detection
- [ ] Matching tdjson native runtime loaded and event loop verified
- [ ] One personally authorized Telegram account logged in and restored after restart (consent + private credentials required)
- [ ] Native per-session CPU/RAM/handle/disk baseline under actual Windows

**Result: STEP 02 NOT COMPLETE.** Scope should remain 1/32 verified until these exit gates have evidence.