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

The canonical version lives in **`XBVault/XBVault.csproj`**:

```xml
<Version>0.8.5</Version>
```

The release script `build/build-release.ps1` overrides it at publish time:

```powershell
.\build\build-release.ps1 -Version 0.9.0 -Arch x64
```

**Workflow:**
1. Before a release, update `<Version>` in `.csproj` to the target version.
2. The release script stamps that version into the compiled binary and ZIP name.

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

The tag triggers CI to produce the final build artifact (future improvement).

## Bumping the Version

### Before a release

1. Decide what changed since the last tag:
   - Breaking change? → bump MAJOR (or MINOR while pre-1.0)
   - New feature? → bump MINOR
   - Bug fix only? → bump PATCH

2. Update `.csproj`:
   ```xml
   <Version>0.9.0</Version>
   ```

3. Commit with message:
   ```
   chore: bump to 0.9.0
   ```

4. Tag and push.

### Between releases (development)

No version bumps needed during development. The `.csproj` version stays at the last release until the next release is ready.

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

Current CI (`.github/workflows/build.yml`) runs on every push:

```yaml
dotnet build XBVault/XBVault.csproj
```

**Planned future improvements:**
- Run on pull requests only
- Add `dotnet test` when tests exist
- Publish release artifacts on tag push

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
# 1. bump version in .csproj
# 2. commit
git add . && git commit -m "chore: bump to 0.9.0"
git tag -a v0.9.0 -m "Release v0.9.0"
git push && git push origin v0.9.0
# 3. build
.\build\build-release.ps1 -Version 0.9.0 -Arch x64
```
