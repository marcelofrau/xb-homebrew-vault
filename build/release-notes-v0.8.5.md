## v0.8.5 — Catalog Rewrite, UI Polish, Install Improvements

### Catalog Source Rewrite
- Replaced fragile main-website HTML crawling with `catalog.json` — refined data, fewer parsing errors.
- New `CatalogApi` model + `CatalogApiService` (JSON-based, cached 6h).
- Parse `UWP Port by:` field from item detail page, show in ItemDetailWindow after Developer.

### UI/UX
- Spinning CD placeholder while browse thumbnails load (no more empty whitespace).
- ItemDetailWindow overlay: description panel + install overlay.
- Browse flicker fix — Thumbnail made observable, removed RemoveAt/Insert grid shuffle.
- Settings redesign with screenshot live capture.
- Confirmation dialogs on Clear Cache / Restart / Reset.

### CustomInstall Wizard
- New dependency selection step.
- Fixed GoBack step logic.
- Extract path centralized via `CacheService`.

### Install Flow
- App cache cleared after successful install.
- "Not installed" → "Not detected" with name-mismatch hint.
- Install state properly resets when selecting a new item.

### Housekeeping
- Removed Border CornerRadius overlay (known issue #3).
- Removed `SlowThumbnails` debug flag.
- Extracted `ShowDescriptionPanel` / `ShowInstallOverlay` computed props.
- Social preview images for GitHub repo.
- OpenSpec proposals for future features: USB Permission Wizard, File Explorer (SSH/SFTP), First-Run Setup Wizard.
- Icon set path references synced to `F:\workspace\icons8-personal-set`.

### What's Next (proposals ready)
- `usb-permission-wizard` — NTFS permission grant for Xbox USB drives
- `file-explorer-ssh` — Native SFTP file browser via SSH.NET
- `first-run-setup-wizard` — Step-by-step Xbox connection config on first launch
