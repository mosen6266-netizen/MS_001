# Step02 — isolated and safe technical spikes

These artifacts are experimental. Nothing here is a production Telegram client.

## Synthetic 300 / 50 task scheduler
\`\`\`sh
python spikes/step02/capacity_simulation.py
\`\`\`
- Creates **300 synthetic account metadata records**, **50 independent bounded queues**, **10,000 synthetic events**.
- Checks no two group actors enter the same *simulated* account gate at once, and that every event is processed.
- Does **not** start 300 TDLib sessions, authorize even one account, access Telegram, or prove real-platform throughput.
- Local Linux run (2026-10-16): 300 records / 50 actors / 10,000 events / one simulated in-flight event per account. Elapsed ~0.16659 seconds, Python tracemalloc peak ~0.1878 MiB. This measurement **only** covers the Python simulation allocations, not per-TDLib session memory.

## Windows WPF/WebView2 probe
\`\`\`powershell
dotnet build spikes/step02/wpf/Step02.WpfSmoke.csproj -c Release
dotnet run --project spikes/step02/wpf/Step02.WpfSmoke.csproj
\`\`\`
- Requires Windows 10/11 x64 with .NET SDK 10 and the WebView2 Runtime.
- Builds a local WPF window with an offline HTML panel; never authenticates or sends Telegram messages.
- Optional manual "Probe tdjson DLL version" button uses **modern** \`td_create_client_id\` + \`td_send\` + \`td_receive\` ABI and will report missing DLL if TDLib is not installed. A true native proof requires compatible x64 tdjson.dll and its runtime dependencies.
- GitHub Actions includes a Windows compilation check. Windows GitHub runner does **not** replace Win10/11 end-user machine testing.
- C# / Windows builds and native probe are **not yet verified** from this Linux container; see \`docs/TECH_SPIKES.md\`.

## System constraints
No API ID or hash, no real Telegram account authorization, no Cloudflare mutations, no unsolicited messaging, no modifying Signal MS002. Source is committed to a **public** repository and must contain no secrets.
