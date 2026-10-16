# Step02 Windows real-machine smoke test (no Telegram login)

**Purpose:** Verify that the experimental WPF shell opens on a real Windows 10/11 x64 computer and WebView2 renders an offline local page. **It is not a finished app** and does not send Telegram messages.

## Download/build
1. Open [GitHub Actions — Step02 technical spike](https://github.com/mosen6266-netizen/MS_001/actions/workflows/step02-tech-spike.yml).
2. Open the newest successful run of the `Step02 technical spike` workflow.
3. Under **Artifacts**, download `MS001-Step02-Windows-x64-Smoke`; extract it to a temporary folder.
4. Run `MS001.Step02.WpfSmoke.exe` from the extracted folder. The build is intended to be self-contained for .NET 10, but still needs WebView2 Runtime installed (or a compatible runtime deployment package).
5. Verify the dark local page loads, runtime version is shown, UI stays responsive, and the window closes cleanly. Click the native TDLib probe only if you intentionally supply a trusted, compatible x64 tdjson.dll; without it, a “DLL not supplied (NOT RUN)” result is expected.

## Never upload secrets
Do not upload phone numbers, Telegram API hashes, verification codes, screenshots of authentication messages, or session files. The provided smoke probe does not ask for these.

## Safe result template
- Windows edition/build: __
- Architecture: x64 / other
- WebView2 version displayed: __
- Offline page renders: yes / no
- Launch/close: pass / fail
- Did you provide optional native tdjson DLL? yes / no
- Native probe result (no tokens or account details): __
- Baseline CPU/RAM/handles during idle (optional): __
- Reproduction steps for failure: __

**Limits:** A successful GUI smoke run does not prove Telegram authorization, TDLib native message reception, 300 simultaneous sessions, 50 real groups, or long-run stability. Step02 requires separate controlled self-owned Telegram account test with locally entered developer credentials and restart evidence before acceptance.
