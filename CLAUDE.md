# XBVault — Session Memory

> **Source of truth is `AGENTS.md`** at the repo root — commands, build env, CI, conventions live there. This file only keeps session-relative facts.

## Key Facts

- **Project**: .NET 10 + Avalonia 12 desktop + Android app at `F:\workspace\xb-homebrew-vault`
- **Dotnet**: use `dotnet` directly — NEVER prefix with `rtk` (it breaks dotnet resolution). Fallback: `& "C:\Program Files\dotnet\dotnet.exe"`
- **Version**: `2.0.4` — source in `Directory.Build.props`, not the csproj
- **Solution**: `XBVault.sln` (XBVault shared, XBVault.Desktop, XBVault.Android, tests)
- **Tests**: `tests/XBVault.Tests` (xUnit, 390+), `dotnet test ... -c Release`
- **Branching**: `feat/<name>`, `fix/<name>`, `chore/<name>` — branch off `main`, merge back. No commits directly on `main` for app code.
- **Icons**: personal set `F:\workspace\icons8-personal-set`, naming `{viewname}-{descriptor}-{size}.png`. See `.opencode/skills/assets-icons`.
- **Wiki**: `wiki/` is a git submodule → `https://github.com/marcelofrau/xb-homebrew-vault.wiki.git`. Edit inside `wiki/`, commit+push submodule, then bump pointer in `main`.
- **Docs site**: `docs/` is a Jekyll site deployed to Cloudflare Pages (`xbvault.pages.dev`). Keep structure; add pages via `docs/docs.md` + `_config.yml`. Use Mermaid for diagrams.
- **No git commit/push** unless explicitly asked and confirmed.

## Version-jump backdrop

- v1.3.1 was the last desktop-only release. v1.4.0 = .NET 10 + static-analysis cleanup. v2.0.0 = Android mobile app merged (mobile ports, sideload, overlays). v2.0.1-v2.0.4 = safe areas/status icons, URL resolver, version overrides, streaming uploads, matcher overhaul, abort button. Latest tag: `v2.0.4`.
- CHANGELOG.md now backfills v1.4.0..v2.0.4 (was stuck at v1.3.1).

## Known constraints (selected, full list in memory/skills)

- Android dev builds must use `dotnet publish -c Release` (AOT required for Avalonia JNI bridge). Always `adb uninstall` before `adb install`.
- Mobile views are pure-Avalonia `Mobile*` files in shared `XBVault/Views/` (no Android types) — App.axaml.cs is in the shared project.
- Connect window must never auto-connect on open.
- Android back: `OnBackInvokedCallback` (API 36+) / `OnBackPressed` fallback → overlay/tab-history stack.