# USB NTFS Preparation for Xbox Ecosystem (Cross-Platform Investigation)

**Status**: Investigation | **Last Updated**: 2026-06-26
**Goal**: Enable external NTFS drives (HDDs/SSDs) for Xbox Series S/X Dev Mode on all three host platforms — Windows, Linux, macOS.

---

## 1. Overview & Objectives

XB Homebrew Vault currently prepares USB drives for Xbox via Windows-only code paths (Win32 security API). This investigation covers the technical constraints and implementation approaches for supporting Linux and macOS hosts.

The core requirement: the Xbox UWP sandbox must be able to read and write data (emulator ROMs, media, screenshots) directly on an NTFS volume without triggering `ACCESS DENIED` errors. This demands explicit ACL entries in the NTFS `$MFT` — standard POSIX permission models cannot express Xbox-specific SIDs.

---

## 2. Technical Context & Security Constraints

Xbox Series S/X runs a hypervisor-based multi-OS architecture. Third-party UWP applications (RetroArch, media players, dev tools) execute inside a restricted partition with tightly isolated UWP security.

### 2.1 Critical Security Identifiers (SIDs)

For a UWP application to access an external filesystem without interactive `FilePicker` mediation, the NTFS volume ACLs must include:

| SID | Value | Role |
|-----|-------|------|
| `ALL APPLICATION PACKAGES` | `S-1-15-2-1` | Logical group covering all installed universal app packages. **Privilege-minimum, recommended.** |
| `EVERYONE` | `S-1-1-0` | Global group granting full control to any execution entity, including anonymous UWP subsystem users. Broader, useful for debugging. |

On Windows these are set via `icacls.exe` or the Security Properties GUI. On Unix hosts these SIDs are not natively expressible — see scenarios below.

### 2.2 Layer Abstraction Limitations (Wine / Proton)

Evaluated running `icacls.exe` under Wine on Linux. **Classified as inviable.** Wine intercepts Windows security API calls and translates them to local POSIX permissions. It cannot inject raw Security Descriptor metadata into the NTFS `$MFT`. The required SID entries never reach the disk.

---

## 3. Solution Architectures

### Scenario A: Native Windows API (Host Windows)

**Status**: Implemented (current approach). Standard NT permission inheritance.

1. Mount volume on Windows.
2. Properties → Security → Advanced.
3. Add `Everyone` (or `ALL APPLICATION PACKAGES`).
4. Grant **Full Control**.
5. Enable: *"Replace all child object permission entries with inheritable permission entries from this object"*.
6. Apply — ACLs propagate recursively through the `$MFT`.

**Strengths**: Native, reliable, well-understood.
**Weakness**: Windows-only.

---

### Scenario B: Low-Level via `ntfs-3g` Driver (Host Linux)

**Status**: Pending validation (POC required).

The open-source `ntfs-3g` driver can map POSIX permissions to Windows SIDs via a structured mapping file at `.NTFS-3G/UserMapping` on the volume root.

**Steps:**

```bash
# 1. Mount read-write, create mapping structure
mkdir -p /mnt/target_drive/.NTFS-3G
echo "::S-1-15-2-1" > /mnt/target_drive/.NTFS-3G/UserMapping

# 2. Unmount to avoid filesystem contention
sudo umount /dev/sdX1

# 3. Apply octal mask recursively via security auditor
sudo ntfssecaudit -r /dev/sdX1 777 /
```

**Key mechanism**: `ntfssecaudit` reads the `UserMapping` file, resolves the SID, and writes the ACL entries directly into the NTFS `$MFT` at the block level — bypassing FUSE or VFS permission translation.

**Risks**:
- `ntfssecaudit` may not be packaged on all distros. Source: `ntfs-3g` tarball provides it; some distros split it into a separate `ntfsprogs-ntfssecaudit` package.
- Requires `root`. Must be script-friendly for headless environments.

---

### Scenario C: Runtime Interception via FUSE (Host macOS)

**Status**: Pending validation (POC required).

macOS does not ship `ntfssecaudit`. The approach relies on FUSE-based ntfs-3g with dynamic `UserMapping` behavior.

**Steps:**

```bash
# 1. Install dependencies
brew install --cask macfuse
brew install gromgit/fuse/ntfs-3g-mac

# 2. Mount, write SID mapping
mkdir /Volumes/XboxDrive/.NTFS-3G
echo "::S-1-15-2-1" > /Volumes/XboxDrive/.NTFS-3G/UserMapping

# 3. Unmount the native Apple driver
diskutil unmount /Volumes/XboxDrive

# 4. Remount with ntfs-3g FUSE driver (local permissions + full access)
sudo mkdir /Volumes/XboxVFS
sudo /opt/homebrew/sbin/ntfs-3g /dev/diskXsX /Volumes/XboxVFS \
  -o local,allow_other,permissions

# 5. Apply recursive POSIX permissions — FUSE translates to NTFS SIDs
sudo chmod -R 777 /Volumes/XboxVFS

# 6. Unmount and flush
sudo diskutil unmount /Volumes/XboxVFS
```

**Key mechanism**: The FUSE driver intercepts `chmod` syscalls and translates them to NTFS ACL modifications using the `UserMapping` as a SID lookup table. The `-o local,allow_other,permissions` flags are required for non-root UWP-like access.

**Risks**:
- macOS SIP (System Integrity Protection) may interfere with third-party kernel extensions. `macFUSE` requires explicit approval in System Settings → Privacy & Security.
- `ntfs-3g-mac` from `gromgit/fuse` is a community tap. No official Homebrew formula exists.
- Performance overhead from FUSE context-switching vs native Apple `NTFS` driver.

---

## 4. Next Steps for POC

| # | Step | Details | Platform |
|---|------|---------|----------|
| 1 | **Saves write test** | Verify RetroArch in Dev Mode can create `Saves/` subfolders autonomously on the prepared drive | Linux, macOS |
| 2 | **Performance benchmark** | Measure read/write throughput of `UserMapping`-provisioned NTFS vs natively-provisioned (Windows) | Linux |
| 3 | **Script automation** | Develop a unified shell script (Bash) based on Scenario B to automate provisioning of new test media | Linux |
| 4 | **macOS FUSE validation** | Confirm `macFUSE` + `ntfs-3g-mac` works on macOS Sequoia+ with SIP enabled | macOS |
| 5 | **Edge: non-root environments** | Test whether `sudo`-less setups (containers, CI runners) can delegate the permission step | Linux |

## 5. Open Questions

- Does `ALL APPLICATION PACKAGES` (`S-1-15-2-1`) suffice, or is `EVERYONE` strictly required for some UWP apps?
- Does `ntfssecaudit` support incremental updates (single-file ACL change) or only full-recursive rewrites?
- Is there a performance regression from FUSE translation on macOS compared to the Windows-native path?
- Can the XBVault app invoke the USB preparation workflow via a bundled shell script (escaping the UWP sandbox via Dev Mode elevation)?

## References

- [ntfs-3g UserMapping documentation](https://manpages.ubuntu.com/manpages/jammy/man8/ntfs-3g.8.html)
- [Xbox Dev Mode security model](https://learn.microsoft.com/en-us/windows/uwp/xbox-apps/)
- [macFUSE](https://macfuse.io/)
