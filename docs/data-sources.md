---
layout: default
title: Data Sources
description: How XB Homebrew Vault sources its catalog — Emulation Revival JSON API integration, catalog structure, and sync pipeline for Xbox homebrew apps.
---

# Data Sources

> **Single source of truth:** the app consumes a generated **JSON catalog API** (`CatalogApiService`). This is the same `catalog.json` that builds the Emulation Revival website, so the desktop app and the site never drift apart.

## Emulation Revival Catalog API

`CatalogApiService` fetches a single generated JSON document (HTTP GET, capped at 5 minutes per request so large catalogs never hit an arbitrary 30s wall):

```
https://emulationrevival.github.io/api/catalog.json
```

On success, items are parsed, classified, and written to disk cache (see [Catalog Cache](#catalog-cache)).

### catalog.json structure

Top-level envelope (`CatalogApiResponse`):

```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-06-20T08:00:00Z",
  "items": [ /* CatalogApiItem[] */ ]
}
```

Each item (`CatalogApiItem`):

| Field | Type | Notes |
|-------|------|-------|
| `id` | string | Stable identifier |
| `title` | string | Display name |
| `description` | string | Summary text |
| `category` / `categorySlug` | string | e.g. `Emulator` / `emulator` |
| `version` | string | Latest version |
| `releaseDate` | string? | Release date |
| `compatibility` | string | Console compatibility note |
| `isExperimental` | bool | Flags experimental apps |
| `imageUrl` / `pageUrl` | string? | Card image, source page |
| `downloadUrl` | string? | Fallback primary download |
| `sourceCodeUrl` / `setupGuideUrl` / `tutorialUrl` / `releaseNotesUrl` | string? | External links |
| `requirements` | string[] | Listed requirements |
| `features` | string[] | Listed features |
| `contributors` | object | Developers / porters / maintainers / mod authors / prebuilt-by |
| `downloads` | array | Download assets (see below) |

### Download classification

Each entry in `downloads[]` is `{ url, label, assetId }`. `CatalogApiService.ClassifyDownloads` tags each as **main**, **dependency**, or **external** so the installer knows what to upload to the console:

- **Dependency** — URL/label matches the dependency regex (e.g. `VCLibs`, framework packages).
- **External** — not an installable package (`.appx` / `.msix` / `.zip` / `.msixbundle` / `.appxbundle`); e.g. mod links, ModDB, or non-release GitHub pages.
- **Main** — the first remaining installable package.

The primary `downloadUrl` resolves to the first non-dependency asset, falling back to the item's `downloadUrl` field. See [Package Installation Flow](integration-package-installation-flow) for how these feed the installer.

## Catalog Cache

Parsed results are cached to disk so the app starts instantly and works offline:

```
%LOCALAPPDATA%\XBVault\cache\catalog-api.json
```

Cache envelope (`CatalogCache`): `{ fetchedAt, source, data }`, where `data` is the full `catalog.json` payload.

| Property | Value |
|----------|-------|
| TTL | **6 hours** (`CacheTtlHours = 6`) |
| Location | `%LOCALAPPDATA%\XBVault\cache\catalog-api.json` |
| Stale fallback | Used (TTL ignored) when the API is unreachable |
| Manual refresh | `CatalogApiService.ClearCache()` / force-refresh in the UI |

### Fetch flow

```mermaid
flowchart TD
    Start["FetchCatalogAsync()"] --> Force{forceRefresh?}
    Force -->|No| Cache{"Cache fresh?<br/>age ≤ 6h"}
    Cache -->|Yes| ReturnCache["Return cached items"]
    Cache -->|No| Fetch
    Force -->|Yes| Fetch["GET catalog.json"]
    Fetch --> Ok{"HTTP 200<br/>+ parsed?"}
    Ok -->|Yes| Save["Save to cache<br/>(fetchedAt = now)"]
    Save --> ReturnFresh["Return fresh items"]
    Ok -->|No| Stale{"Stale cache<br/>exists?"}
    Stale -->|Yes| ReturnStale["Return stale items<br/>(TTL ignored)"]
    Stale -->|No| Fail["Return empty<br/>+ error"]

    style Start fill:#447F3E,stroke:#9ACA3C,color:#fff
    style ReturnCache fill:#9ACA3C,stroke:#447F3E,color:#000
    style ReturnFresh fill:#9ACA3C,stroke:#447F3E,color:#000
    style ReturnStale fill:#FF9900,stroke:#447F3E,color:#000
    style Fail fill:#CC3333,stroke:#447F3E,color:#fff
    style Force fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style Cache fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style Fetch fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style Ok fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style Stale fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style Save fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
```

## Catalog Overrides

The Emulation Revival catalog is **externally maintained and read-only within the app** — matching accuracy is corrected app-side through three override layers:

| # | Source | File / location | Purpose | Priority |
|---|--------|-----------------|---------|----------|
| 1 | **Embedded** | `XBVault/Assets/package-overrides.json` | PFN / name → catalog-ID mappings shipped with the app (e.g. `Doom64EX-Classic` → its catalog ID) | High |
| 2 | **Remote version overrides** | GitHub raw `versionOverrides` merged over the embedded table | maps a `catalogVersion` to the real Xbox manifest version when upstream reports the wrong version (e.g. Sonic 2 SMS catalog `2.9.2` vs manifest `2.9.0.2`); remote wins on duplicate keys | Higher |
| 3 | **Local (user)** | `%APPDATA%\XBVault\local-overrides.json` via `LocalOverrideService` | UI-triggered remap of a catalog name to an installed package | Highest |

Key rules:

- `versionOverrides` entries are **gated on `catalogVersion`** — they only apply while the catalog reports that version, so a real upstream fix in a later catalog release is never permanently masked.
- Effective version resolution falls back `remote → embedded → catalog.Version` and is centralized in `VersionCheckerService`.
- The override layers reuse the same `PackageOverrideService` "remote over embedded" merge pattern as the embedded table, so version-override fixes ship **without an app release**.

```mermaid
flowchart LR
    subgraph versionOverrides["Remote (GitHub raw)"]
        R1["catalogVersion → real manifest version"]
    end
    subgraph embedded["Embedded (package-overrides.json)"]
        E1["PFN / name → catalog ID"] 
        E2["base versionOverrides"]
    end
    subgraph local["Local (local-overrides.json)"]
        L1["user remapped name → package"]
    end
    subgraph source["Install source"]
        S1["Installed package manifest"]
        S2["catalog.json"]
    end
    S1 --> C{"VersionCheckerService"}
    S2 --> C
    R1 --> C
    E1 --> M{"Matcher"}
    E2 --> C
    L1 --> M
    C --> D["Effective version → update decision"]
    M --> D

    style R1 fill:#447F3E,stroke:#9ACA3C,color:#fff
    style E1 fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style E2 fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style L1 fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style S1 fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style S2 fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style C fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style M fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style D fill:#9ACA3C,stroke:#447F3E,color:#000
```

## Package Cache

> Distinct from the [Catalog Cache](#catalog-cache) above (catalog metadata in `%APPDATA%`). This cache holds the **downloaded package files** in `%TEMP%`.

Downloaded packages are stored in `%TEMP%/XBVault/cache/`:

```mermaid
graph TD
    root["%TEMP%/XBVault/cache/"]
    dolphin["dolphin/"]
    retroarch["retroarch/"]
    pcsx2["pcsx2/"]
    root --> dolphin
    root --> retroarch
    root --> pcsx2

    dolphin --> d1["DolphinWinRT_1.1.9.0_x64.msix"]
    dolphin --> dm["manifest.json"]

    retroarch --> r1["RetroArch-SeriesConsoles.appx"]
    retroarch --> r2["Microsoft.VCLibs.x64.14.00.appx"]
    retroarch --> rm["manifest.json"]

    pcsx2 --> p1["pcsx2-v1.0.0-xbox.msix"]
    pcsx2 --> pm["manifest.json"]
    
    style root fill:#447F3E,stroke:#9ACA3C,color:#fff
    style dolphin fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style retroarch fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style pcsx2 fill:#2A2D33,stroke:#447F3E,color:#9ACA3C
    style d1 fill:#9ACA3C,stroke:#447F3E,color:#000
    style dm fill:#9ACA3C,stroke:#447F3E,color:#000
    style r1 fill:#9ACA3C,stroke:#447F3E,color:#000
    style r2 fill:#9ACA3C,stroke:#447F3E,color:#000
    style rm fill:#9ACA3C,stroke:#447F3E,color:#000
    style p1 fill:#9ACA3C,stroke:#447F3E,color:#000
    style pm fill:#9ACA3C,stroke:#447F3E,color:#000
```

`manifest.json` stores parsed metadata and dependency info so reinstalls don't need re-download:

```json
{
  "name": "RetroArch",
  "version": "1.16.0",
  "category": "Emulator",
  "packageFile": "RetroArch-SeriesConsoles.appx",
  "dependencies": ["Microsoft.VCLibs.x64.14.00.appx"],
  "sourceUrl": "https://emulationrevival.github.io/..."
}
```

---

**Related:**
- [Package Installation Flow](integration-package-installation-flow) — how cached files and dependencies feed the installer
- [API Reference](api) — Device Portal endpoints
- [Architecture](architecture) — where `CatalogApiService` sits in the service layer

---

[← API](api) · [Architecture →](architecture)
