# Step03 — project foundation engineering

**Purpose:** Build a real React/TypeScript UI and .NET Windows WPF/WebView2 host without Telegram access.

## Implemented
- WPF singleton Mutex prevents second process from opening a competing DB or task engine.
- Local UI dist is embedded as desktop content and mapped to a trusted virtual host; all external navigation/new windows blocked. CSP is strict and frontend has no network access.
- WebView2 bridge accepts only `system.ping` / `app.info`, no arbitrary method or string evaluation. User-facing pages display honest unimplemented labels.
- React state-based sidebar avoids full-page refresh and keeps navigation local to the UI.
- IPC DTO/type guard has unit tests; CI pipeline runs frontend lint/type check/unit/build and compiles WPF on Windows hosted runner.

## Explicit deferred items
- Windows 10/11 native launch, packaging with WebView2 runtime, TDLib and account login are final user acceptance items.
- `apps/ui/package-lock.json` is generated via a temporary one-time CI workflow, committed and verified; CI now installs with npm ci. Now committed it becomes canonical for `npm ci`; before that, transitive dependencies are **not fully pinned** and Step03 DEV PASS, native Windows testing deferred.
- GitHub Actions builds are not equivalent to a full 72h stability run or a complete release installer.

## Security constraints
This skeleton sends **no Telegram messages** and contains **no real credentials**, no network API, no Telegram user sessions. Never commit *.session, API hash, password, or any .env file.

## Accepted CI evidence
- [GitHub Actions 38921641970](https://github.com/mosen6266-netizen/MS_001/actions/runs/38921641970) UI lint/test/build and Windows WPF build + self-contained publish: SUCCESS.
- Lockfile verified on main, 238 package entries; CI is read-only after removal of one-time lock writer.
- Full results, boundaries and artifact identifiers: [STEP_03_ACCEPTANCE.md](STEP_03_ACCEPTANCE.md).
