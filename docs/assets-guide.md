# Assets Guide

## Directory structure

```
XBVault/Assets/
├── _Backup/        # Legacy/migration — do not add new files here
│   ├── Icons/
│   └── Images/
├── Fonts/
│   ├── Oxanium-*.ttf
│   ├── ProFontWindowsNerdFont-Regular.ttf
├── Icons/
│   └── app.ico     # Application icon only
├── Themes/
│   └── BladesTheme.axaml
└── Views/          # Per-view/window icons & images
    ├── AboutWindow/
    ├── BrowseView/
    ├── ConfirmWindow/
    ├── ConnectionWindow/
    ├── CustomInstallWindow/
    ├── ErrorDialog/
    ├── InstalledView/
    ├── ItemDetailWindow/
    ├── LogsView/
    ├── MainWindow/
    ├── NetworkInfoWindow/
    ├── ProcessesWindow/
    ├── RefreshWindow/
    ├── ScreenshotWindow/
    ├── SettingsView/
    ├── SplashWindow/
    ├── SystemInfoWindow/
    └── ToolsView/
```

Each view/window that needs icons gets its own subfolder under `Views/`.

## Naming convention

**Pattern:** `{viewname}-{descriptor}[-{size}].png`

| Part | Rule | Example |
|------|------|---------|
| viewname | Lowercase, no separator | `mainwindow`, `custominstall`, `errordialog` |
| descriptor | kebab-case, meaningful | `about`, `close`, `step1-disabled` |
| size (optional) | Pixel size for icons | `16`, `20`, `32`, `48`, `100` |

**Examples from codebase:**

- `mainwindow-about-32.png` — MainWindow's about button, 32px
- `custominstall-step1-disabled-20.png` — CustomInstall wizard step 1 disabled, 20px
- `connection-banner.png` — No size = full-width banner/background
- `logs-error-16.png` — Log severity icon, 16px
- `installed-polling-16.png` — Installed view polling indicator, 16px

**Rules:**
- Always lowercase
- Use hyphens as separator (never underscores or camelCase)
- Omit size only for full-width images, backgrounds, or logos that fill a container
- Matching `.axaml` references the file via `avares://XBVault/Assets/Views/{ViewName}/{filename}`

## Icon source: personal set

All PNG icons come from the developer's personal Icons8-derived collection at:

```
F:\workspace\icons8-personal-set
(Windows) or /mnt/f/workspace/icons8-personal-set (WSL)
```

The set is organized by pixel size:

```
icons8-personal-set/
├── 16x16/
├── 20x20/
├── 24x24/
├── 32x32/
├── 48x48/
├── 50x50/
├── 64x64/
├── 100x100/
├── 128x128/
├── 256x256/
├── ico/           # .ico variants — do not use unless explicitly asked
├── catalog/       # Metadata for browsing
└── download-*.py  # Fetch scripts
```

**Workflow to add a new icon:**

1. Identify the needed size from the context (see "Size selection" below)
2. Copy from `icons8-personal-set/{size}x{size}/{name}-{size}.png`
3. Rename following the convention: `{viewname}-{descriptor}-{size}.png`
4. Place in `XBVault/Assets/Views/{ViewName}/`
5. Reference in axaml as `avares://XBVault/Assets/Views/{ViewName}/{filename}`

## Format rules

- **Always use PNG.** Never use `.ico` files unless the user explicitly requests it.
  - The sole exception is `Assets/Icons/app.ico` (application/window icon), which must be `.ico`.
- Do not convert `.ico` files to `.png` — always source the PNG from the personal set.
- Do not use JPG, BMP, GIF, SVG, or WebP for icons.

## Size selection

Match the icon size to the UI context:

| Context | Size |
|---------|------|
| Inline with small text / status indicators | 16×16 |
| Toolbar buttons, compact actions | 20×20 |
| Tab / sidebar icons (alongside labels) | 32×32 |
| Standalone buttons, dialog body icons | 48×48 |
| Large indicators (success/failure, empty states) | 100×100 |
| Full-width backgrounds, banners | Variable (omit size in name) |

If in doubt between two sizes, prefer the larger one — it can always be scaled down in XAML with `Width`/`Height`.

## Attribution

All third-party icons used in this project must be attributed in `docs/ATTRIBUTIONS.md`.
See that file for current attributions and licenses.

## View-agnostic icons

If an icon is needed by multiple views and is not specific to any single one, place it in `Assets/Icons/` (not `Assets/Views/`). Currently this folder only contains `app.ico`.
