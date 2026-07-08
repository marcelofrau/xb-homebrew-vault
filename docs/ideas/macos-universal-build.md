# macOS Universal Build

**Impact:** Low | **Effort:** Medium | **Suggested priority:** Phase 2

## Problem

Mac users have to choose between `osx-x64` and `osx-arm64` when downloading. Users who don't know their architecture (Intel vs Apple Silicon) download the wrong one and the app doesn't run.

## Current State

- CI generates 2 separate ZIPs: `XBVault-v*-osx-arm64.zip` and `XBVault-v*-osx-x64.zip`
- No user confusion reported yet, but it's a friction point
- GitHub Actions `macos-latest` is now ARM

## Solution: extra package (additive, not replacement)

**Strategy:** keep the 2 existing separate ZIPs + add a third universal one. Users who know what they're doing download the specific one; users who want simplicity grab the universal.

### CI Flow

```
macos-latest → build osx-arm64.zip  (existing, unchanged)
macos-latest → build osx-x64.zip    (existing, unchanged)

new job: osx-universal (macos-latest, needs: [release-osx-arm64, release-osx-x64])
  → download artifact release-osx-arm64
  → download artifact release-osx-x64
  → extract both
  → lipo Mach-O binaries
  → copy DLLs and config (identical, either one works)
  → package XBVault-v*-osx-universal.zip
  → upload as release-osx-universal
```

### What to merge

| Type | Action | Examples |
|------|--------|----------|
| Mach-O executable | `lipo` | `XBVault` |
| Native dylibs | `lipo` | `libSkiaSharp.dylib`, `libHarfBuzzSharp.dylib`, `coreclr.dylib`, `hostfxr.dylib`, `hostpolicy.dylib` |
| Managed DLLs | Copy (identical) | `*.dll`, `*.pdb` |
| Config | Copy (identical) | `*.json`, scripts |

### Script: `build/build-release-universal.sh`

Receives 2 directories (arm64 + x64 already built), merges and packages.

```bash
#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:?Usage: $0 <version>}"
ARM_DIR="${2:?path to arm64 publish dir}"
X64_DIR="${3:?path to x64 publish dir}"
OUTPUT_DIR="${4:-dist}"

DIST_DIR="$ROOT/$OUTPUT_DIR"
ZIP_NAME="XBVault-v$VERSION-osx-universal.zip"
ZIP_PATH="$DIST_DIR/$ZIP_NAME"
UNI_DIR="$DIST_DIR/publish-universal"

mkdir -p "$UNI_DIR"

echo "Merging binaries with lipo..."
for file in "$ARM_DIR"/*; do
  base=$(basename "$file")
  x64_file="$X64_DIR/$base"

  if [ ! -f "$x64_file" ]; then
    echo "  SKIP (x64 missing): $base"
    cp "$file" "$UNI_DIR/"
    continue
  fi

  if file "$file" | grep -q "Mach-O"; then
    lipo -create "$file" "$x64_file" -output "$UNI_DIR/$base"
    echo "  LIPO: $base"
  else
    cp "$file" "$UNI_DIR/$base"
    echo "  COPY: $base"
  fi
done

echo "Generating helper scripts..."
bash "$ROOT/build/gen-helper-scripts.sh" "$UNI_DIR"

echo "Packaging $ZIP_NAME..."
cd "$UNI_DIR" && zip -r "$ZIP_PATH" . && cd "$ROOT"

echo "Universal release created: $ZIP_PATH"
rm -rf "$UNI_DIR"
```

### CI integration (build.yml)

Keep existing entries + add `osx-universal` job:

```yaml
# Existing — unchanged
- os: macos-latest
  rid: osx-arm64
  script: bash build/build-release.sh $VERSION arm64

- os: macos-latest
  rid: osx-x64
  script: bash build/build-release.sh $VERSION x64

# New — merge only, no build
- os: macos-latest
  rid: osx-universal
  script: |
    # Download both artifacts (via needs or separate step)
    # build/build-release-universal.sh $VERSION ./arm64-publish ./x64-publish
```

**Detail:** the `osx-universal` job needs `needs: [release-osx-arm64, release-osx-x64]` in build.yml, downloads artifacts from previous jobs, extracts, and calls the script.

### Risk: R2R cross-compile on ARM runner

CI `macos-latest` is ARM. `dotnet publish -r osx-x64` with `PublishReadyToRun=true` needs the x64 cross-compiler. Expected behavior (.NET 8 SDK supports this), but needs validation. If it fails:
- Build arm64 on `macos-latest`
- Build x64 on `macos-13` (Intel, still available as runner)
- Universal job stays the same — only the x64 artifact source changes

Since separate ZIPs still exist, this risk **doesn't block** — the universal just won't ship for x64 until validated.

### Estimated size

- ZIP osx-arm64: ~80MB (existing)
- ZIP osx-x64: ~80MB (existing)
- ZIP universal: ~130MB (managed DLLs + JSONs don't duplicate)
- **Total in release:** ~290MB (vs ~160MB today)

### Pros
- Zero risk of breaking existing flow
- Advanced users choose separate, beginners grab universal
- Additive CI — doesn't touch existing jobs
- If cross-compile fails, only universal is affected; separate ZIPs still ship

### Cons
- 3 macOS ZIPs in release (more visual clutter)
- Extra CI job
- Needs R2R cross-compile validation on ARM runner
- Extra script to maintain

## References
- [man lipo](https://www.unix.com/man-page/osx/1/lipo/)
- [.NET RID catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
- [Apple Universal Binaries](https://developer.apple.com/documentation/apple-silicon/building-a-universal-macos-binary)
