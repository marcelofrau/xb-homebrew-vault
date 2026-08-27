---
layout: default
title: Branching & Versioning
---

# Branching & Versioning

## Semantic Versioning (SEMVER)

This project follows **[Semantic Versioning 2.0.0](https://semver.org/)**.

Given a version number `MAJOR.MINOR.PATCH`:

| Bump | When | Example |
|------|------|---------|
| **MAJOR** | Breaking API or behavioral change. Existing users must take action to keep working. | `1.3.0` → `2.0.0` |
| **MINOR** | New feature added without breaking existing functionality. Also: non-breaking deprecations. | `1.3.0` → `1.4.0` |
| **PATCH** | Bug fix, performance improvement, refactor, docs. No new features, no breaking changes. | `1.3.0` → `1.3.1` |

**Pre-1.0 (0.x.y):** Anything may change at any time. Treat MINOR as MAJOR (a new feature might break things), PATCH as MINOR (a fix may change behavior). Once `1.0.0` is released, strict SEMVER applies.

### Pre-release labels

Use dots to append labels for intermediate builds:

| Label | Meaning | Example |
|-------|---------|---------|
| `-alpha.N` | Very early, may not work | `0.9.0-alpha.1` |
| `-beta.N` | Feature-complete, testing | `0.9.0-beta.2` |
| `-rc.N` | Release candidate | `0.9.0-rc.1` |

## Version Source of Truth

The canonical version lives in **`Directory.Build.props`** at the repo root (applied to every project):

```xml
<PropertyGroup>
  <Version>2.0.4</Version>
</PropertyGroup>
```

The release scripts override or stamp it at publish time:

```powershell
# Desktop releases (self-contained ZIPs per RID)
.\build\build-release.ps1 -Version 2.0.4 -Arch x64

# Android release (signed APK, requires Android SDK + JDK 21)
.\build\build-release-android.ps1 -Version 2.0.4
```

**Workflow:**
1. Before a release, update `<Version>` in `Directory.Build.props` to the target version.
2. The release script stamps that version into the compiled binary, ZIP name, and APK.
3. For Android, the version is also mapped to a monotonic integer `versionCode` (`-p:ApplicationVersion = MAJOR*1000000 + MINOR*1000 + PATCH`) via `build-release-android.ps1`.

## Branch Strategy

```
main  ─────●──────────●──────────●──────────●────
            \        / \        / \        /
             \      /   \      /   \      /
              ●────●     ●────●     ●────●
            feature/   feature/   feature/

```

### `main`

- **Always releasable.** Every commit on `main` has passed CI (`dotnet build`).
- Direct commits are allowed for: urgent fixes, docs, CI config, version bumps.
- For any feature or change that touches application code, use a feature branch.

Other branches are short-lived and deleted after merge.

### Feature branches: `feat/<name>`

Used for every OpenSpec change, new feature, or non-trivial fix.

| Branch prefix | Purpose |
|---------------|---------|
| `feat/<name>` | New feature or OpenSpec change |
| `fix/<name>` | Bug fix |
| `chore/<name>` | Tooling, CI, refactors, tech debt |
| `docs/<name>` | Documentation-only changes |

**Naming:** Use the OpenSpec change name when one exists, e.g.:

```
feat/first-run-setup-wizard
fix/connection-timeout-handling
chore/split-xboxdeviceservice
```

**Lifecycle:**

```
1. Branch off main        git switch -c feat/my-thing
2. Implement              OpenSpec tasks, multiple commits
3. Push, CI validates    dotnet build must pass
4. Merge back to main    git switch main && git merge feat/my-thing
5. Delete branch         git branch -d feat/my-thing
```

### Release branches (optional)

Only needed when `main` needs to keep moving while a release is stabilized:

```
main    ──●────●────●────●────────────●────●────
              \          / (bugfix)   /
               ●────────●────────────●
               release/0.9.x
```

For a solo/small-team project, releases can go straight through `main` with a tag.

## Git Tags

Every release gets an **annotated tag** matching the version:

```powershell
git tag -a v0.9.0 -m "Release v0.9.0"
git push origin v0.9.0
```

The tag triggers the `release` GitHub Actions job, which builds the full artifact matrix (6 desktop ZIPs + the signed Android APK), runs VirusTotal scans, and publishes the GitHub Release with notes from `release-notes/v{version}.md`.

## Bumping the Version

### Before a release

1. Decide what changed since the last tag:
   - Breaking change? → bump MAJOR (or MINOR while pre-1.0)
   - New feature? → bump MINOR
   - Bug fix only? → bump PATCH

2. Update `Directory.Build.props`:
   ```xml
   <Version>2.0.4</Version>
   ```

3. Add a `release-notes/v{version}.md` (templates live under `release-notes/`) and backfill the `CHANGELOG.md` entry.

4. Commit with message:
   ```
   chore: bump to 2.0.4
   ```

5. Tag and push.

### Android versionCode

`versionCode` must be monotonic and is derived from the semantic version:

```
versionCode = MAJOR * 1_000_000 + MINOR * 1_000 + PATCH
```

Passed to the Android publish as `-p:ApplicationVersion` (the `-p:ApplicationVersionCode` MSBuild property does **not** exist and is silently ignored).

### Between releases (development)

No version bumps needed during development. `Directory.Build.props` stays at the last release until the next release is ready.

## Commit Messages

Use **Conventional Commits** for consistent changelog generation:

| Prefix | Scope |
|--------|-------|
| `feat:` | New feature |
| `fix:` | Bug fix |
| `chore:` | Tooling, deps, CI, refactors |
| `docs:` | Documentation |
| `perf:` | Performance improvement |
| `style:` | Formatting (no code change) |

Examples:

```
feat: add first-run setup wizard with 3-step onboarding

fix: handle null reference in network config parser

chore: bump CommunityToolkit.Mvvm to 8.4.0

docs: add branching and versioning strategy
```

## CI

CI runs via GitHub Actions (`.github/workflows/build.yml`) on **push and PR** to `main`:

| Job | When | Runs |
|-----|------|------|
| `build` | push/PR | `dotnet restore` + `dotnet build -c Release` on windows-latest and ubuntu-latest |
| `build-android` | push/PR | publish android-arm64 Release APK (debug key on CI) |
| `release` | tag `v*` | full artifact matrix (win/linux/osx × x64/arm64 + android-arm64 APK) → ZIP → SHA256 + VirusTotal → GitHub Release with notes |
| `deploy-docs` | main push | Jekyll site → Cloudflare Pages |

Tests run in the `build` job via `dotnet test` (the suite currently has **390+ tests**; the list must stay green before merging).

## Quick Reference

```powershell
# Start a feature
git switch main && git pull
git switch -c feat/first-run-setup-wizard

# Commit during development
git add . && git commit -m "feat: add SetupWizardViewModel with 3-step navigation"

# Merge when done
git switch main
git merge feat/first-run-setup-wizard
git branch -d feat/first-run-setup-wizard

# Release
# 1. bump version in Directory.Build.props
# 2. commit
git add . && git commit -m "chore: bump to 2.0.4"
git tag -a v2.0.4 -m "Release v2.0.4"
git push && git push origin v2.0.4
# 3. build
.\build\build-release.ps1 -Version 2.0.4 -Arch x64
```
