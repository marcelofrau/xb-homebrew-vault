High-signal notes for automated agents working on this repo.

Run / build
- Dev run (Windows PowerShell):
  - powershell -File build/run.ps1
  - Equivalent: dotnet run --project XBVault
- Release build (Windows PowerShell, mandatory Version):
  - powershell -File build/build-release.ps1 -Version 0.1.0 -Arch x64
  - Output ZIP: build/dist/XBVault-v<Version>-win-<Arch>.zip

Environment
- Requires .NET 8 SDK and Windows (project is WinExe, RID use win-<arch>). Do not attempt publish as linux/macos without changing csproj.
- Scripts call dotnet at "C:\Program Files\dotnet\dotnet.exe". If dotnet is elsewhere on host, either run dotnet directly (see "Equivalent" above) or update the script before committing.

Project / conventions agents must not break
- Project folder name must remain XBVault (build scripts expect it). Do not rename top-level project folder without updating scripts.
- Settings are stored in %APPDATA%/XBVault/settings.json. Credentials are obfuscated (CryptoService). Do not commit this file.

Packaging / publish
- build/build-release.ps1 does a self-contained publish for Windows (-r win-<arch>) and zips publish output. It also sets PublishReadyToRun=true.
- Version passed to build-release is required and injected via -p:Version. Bump Version there for release artifacts.

Tests / CI
- There are no test suites or CI workflows in repo root to run. Do not invent test runs.

Docs / source of truth
- Prefer executable sources: build/*.ps1 and XBVault/*.csproj. README.md and docs/*.md are high-value reference but scripts are authoritative for build/publish behavior.

Other
- Avalonia UI entrypoint: XBVault/Program.cs (Start with dotnet run --project XBVault). Use this when you need to run the app headless for smoke checks.

Docs quick links
- Cross-platform porting plan: docs/CROSS-PLATFORM-PORTING.md
- Versioning & branch strategy: docs/PLAN.md
- Icon attributions & personal set: docs/ATTRIBUTIONS.md

Icons
- Personal icon set lives outside repo by default. If you need to sync it into this repo, follow docs/ICON-SET-SYNC.md. Do not commit personal/commercial-licensed icons without explicit permission.

When unsure
- If a change touches build scripts, verify on Windows with PowerShell 7+ and .NET 8 before committing artifacts or CI changes.
