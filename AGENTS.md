High-signal notes for automated agents working on this repo.

Build env
- SDK installed via scoop at `C:\Program Files\dotnet\dotnet.exe` (v10.0, builds net8.0 targets fine).
- `rtk` wrapper breaks `dotnet` resolution. Run `dotnet` directly — do NOT prefix with `rtk`.
- If `dotnet` fails via PATH, use full path: `& "C:\Program Files\dotnet\dotnet.exe" build ...`

Run / build
- Dev run: `powershell -File build/run.ps1` (or `dotnet run --project XBVault`)
- Quick build (debug): `powershell -File build/build.ps1` (or `dotnet build XBVault/XBVault.csproj`)
- Release build (mandatory Version):
  - Windows: `powershell -File build/build-release.ps1 -Version 0.1.0 -Arch x64`
  - Linux/macOS: `bash build/build-release.sh 0.1.0 x64`
  - Optional Windows installer: add `-Installer` flag (requires Inno Setup)
  - Output ZIP: `build/dist/XBVault-v<Version>-<RID>.zip`
- No `.sln` file — single project. All commands target `XBVault/` directly.
- `.gitignore` excludes `AGENTS.md` — committing it is opt-in.

CI (`.github/workflows/build.yml`)
- `build` job: runs on push/PR to `main` (windows-latest + ubuntu-latest). `dotnet restore` + `dotnet build -c Release`.
- `release` job: on tag `v*`. Builds matrix: win-x64, win-arm64, linux-x64, osx-x64, osx-arm64. Self-contained ZIP per RID.
- `publish` job: creates GitHub release from tag with all ZIPs.

Environment
- Requires .NET 8 SDK. CI uses `dotnet-version: 8.0.x`.
- Windows scripts default to `"C:\Program Files\dotnet\dotnet.exe"`. Fallback: `dotnet` on PATH (build-release.ps1 checks both).
- `OutputType` is `WinExe` on Windows, `Exe` on other platforms (csproj lines 3-4).
- `PublishReadyToRun` enabled on Windows, disabled for arm64 cross-compile.
- `BuiltInComInteropSupport` is Windows-only (csproj line 9).
- USB detection via WMI (`System.Management`) — Windows only, macOS/Linux no-op.

Project / conventions
- Project folder name must remain `XBVault` (build scripts expect it). Do not rename.
- Settings stored at `%APPDATA%/XBVault/settings.json`. Credentials obfuscated via `CryptoService` (XOR + salt). Do not commit this file.
- `Assets/**` embedded as AvaloniaResource — referenced in AXAML via `avares://XBVault/...`.
- No test project exists. No test framework. Do not invent test runs.
- Tech stack: .NET 8, Avalonia 12, CommunityToolkit.Mvvm 8.4 (source generators), SSH.NET 2024.2.

OpenSpec workflow
- `openspec/` directory tracks spec-driven changes. Check `openspec/changes/` for active work.
- GitHub skills in `.github/skills/` reference prompts in `.github/prompts/`.

Assets / Icons
- UI icons: always PNG. Naming: `{viewname}-{descriptor}-{size}.png` (lowercase, hyphens).
- Each view/window gets folder under `XBVault/Assets/Views/`.
- App icon (`Assets/Icons/app.ico`) is the sole `.ico` — required by `ApplicationIcon` in csproj.
- Personal icon set: `F:\workspace\icons8-personal-set` (organized by size).
- Full reference: `docs/ASSETS-GUIDE.md`. Icon sync: `docs/ICON-SET-SYNC.md`.
- Do not commit personal/commercial-licensed icons without explicit permission.

Branching
- Feature branches: `feat/<name>`, `fix/<name>`, `chore/<name>`.
- Branch off `main`, merge back, delete branch. No commits directly on `main` for app code.

Docs
- Always use Mermaid (```mermaid) for diagrams — never ASCII art.
- Supported diagram types: flowchart, sequenceDiagram, classDiagram, stateDiagram, gantt.

When unsure
- Prefer executable sources (build scripts, csproj) over prose docs.
- If change touches build scripts, verify on Windows with PowerShell 7+ and .NET 8.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
