# Step31 — Third-party component and licensing review (provisional)

This is an audit inventory and a publication blocker, NOT legal clearance or an assertion that all licenses are approved.

## Known component families and review needed

| Component family | Source in repository | Obligation or unfinished check | Status |
|---|---|---|---|
| React and React DOM | apps/ui/package.json and package-lock.json | Verify exact resolved versions, authoritative upstream license notices and packaged notices | REVIEW_REQUIRED |
| Vite, Vitest, TypeScript, ESLint (build and test only) | apps/ui/package-lock.json | Confirm whether any build-time assets/code are redistributed and check dependency advisories | REVIEW_REQUIRED |
| Microsoft .NET runtime and desktop libraries | apps/desktop/*.csproj; NuGet PackageReference across src/ | Verify Microsoft redistributed .NET components and NuGet package notices, pin/hash dependency inventory | REVIEW_REQUIRED |
| Microsoft WebView2 Evergreen offline runtime | build/Build-Installer.ps1; installer/Setup.iss | Official distribution terms, signature, hashes and use of latest supported redistribution method | REVIEW_REQUIRED |
| Inno Setup compiler and bundled language translations | installer/Setup.iss; installer/lang/LICENSE | Confirm Inno Setup license requirements and translation attribution for shipped installer | REVIEW_REQUIRED |
| Telegram TDLib / native tdjson | related TDLib adapter and deferred native bundle | Official upstream source/version, third-party licenses, reproducible official native build, binary signatures/hashes, native dependency inventory | NOT_YET_BUNDLED / RELEASE_BLOCKER |
| Cloudflare Worker and D1 | license/ | Service compliance, API/IP privacy handling, infrastructure ownership and independent environment verification | STAGING_ONLY / RELEASE_BLOCKER |
| Brand names, user-facing graphics and platform affiliation | assets/branding, program icon, documentation | Human trademark and presentation review, no false implication of Telegram or Microsoft affiliation | HUMAN_REVIEW_REQUIRED |

## Controls and exclusions

The current codebase is an unsigned development build. A release cannot be approved until a human has reviewed the exact third-party binaries and versions **as actually redistributed**, attached notice/attribution files, and recorded the release acceptance evidence. The git-tracked high-confidence scanner does not replace a dependency-vulnerability scan, a software bill of materials, or legal license verification. Current React package versions in package.json do not establish that all transitive dependencies are acceptable.

Required release-readiness item: third_party_license_trademark_review. It remains DEFERRED in release/readiness.json. All released products must have a separate protected-environment approver and a signed installer; no legal conclusion is asserted by this document.
