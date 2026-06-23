---
layout: default
title: Cross-Platform Porting
---

# Porting Log: Linux and macOS

## Status: ✅ Shipped in v0.8.6

Both Linux (x64) and macOS (x64 + arm64) release artifacts are built and published via CI. What follows is the original plan — kept for historical reference.

## Original Objective
- Enable building, publishing, and running XBVault on Linux and macOS for development and testing (does not cover final packaging like macOS notarization).

## Verified Assumptions
- Project uses .NET 8 + Avalonia (supports Linux/macOS).
- Currently has dependencies and scripts assuming Windows (WinExe, absolute dotnet path, PublishReadyToRun, BuiltInComInteropSupport).

## High-Level Checklist (Executable Order)

### 1. Quick Experiment (prove app launches)
- On a Linux machine with .NET 8 installed:
  ```bash
  dotnet publish XBVault -c Release -r linux-x64 --self-contained false -o out
  ./out/XBVault
  ```
- On macOS, replace `-r` with `osx-x64` or `osx-arm64` and run `./out/XBVault` or `open out/XBVault.app` if bundled.
- Install native libs if app fails (see "Native Dependencies" section).

### 2. Harden csproj (minimal changes)
- Add RuntimeIdentifiers: `win-x64;linux-x64;osx-x64;osx-arm64`.
- Conditionalize Windows-only props:
  - `OutputType` can be `Exe` instead of `WinExe` for cross-platform.
  - Move `BuiltInComInteropSupport` to `Condition="'$(RuntimeIdentifier)'=='win-*'"`.
- Don't remove Avalonia; just make props conditional.

### 3. Make scripts multi-OS / document alternatives
- Don't rely on `"C:\Program Files\dotnet\dotnet.exe"` in scripts. Options:
  - Update scripts to use `dotnet` from PATH (cross-OS).
  - Or clearly document how to run `dotnet publish` directly (see commands above).

### 4. Guard Windows-specific code
- Search for P/Invoke, COM, registry, or hard-coded paths:
  ```bash
  rg "DllImport|ComInterop|Registry|RegistryKey|PInvoke|BuiltInComInteropSupport|RuntimeInformation" -S
  ```
- Wrap/conditionalize with `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`.

### 5. Native Dependencies (install for dev/test)
- Linux (Debian/Ubuntu example):
  ```bash
  sudo apt install -y libgtk-3-0 libgdk-pixbuf2.0-0 libx11-6 libc6 libharfbuzz0b libfontconfig1
  ```
- macOS: install via Homebrew libs recommended by Avalonia (gtk3/homebrew formulas) and configure DISPLAY if using remote GUI.

### 6. Smoke Tests and Validation
- Simple CI job per OS: `dotnet publish` + try to run the binary (exit code 0) inside the runner.
- Manual tests: open UI and quickly navigate through critical screens (Settings, Browse, Installed).

### 7. CI
- Add matrix job (windows-latest, ubuntu-latest, macos-latest) that runs:
  - `dotnet --info`
  - `dotnet restore`
  - `dotnet publish XBVault -c Release -r <rid> --self-contained false -o out`
  - Run smoke executable (where possible)

### 8. Packaging and Distribution (separate)
- Linux: AppImage / deb / flatpak — needs to package native dependencies correctly.
- macOS: .app bundle, codesign and notarize for public distribution.

## Quick Estimate
- Local experimentation (prove publish + open): 1–2 hours.
- Remove/conditionalize Windows-only and adjust csproj/scripts: 2–6 hours.
- Configure multi-OS CI + smoke runs: 1–3 hours.
- Cross-platform packaging (prod): days → weeks (depends on target quality).

## Minimum PR Checklist
- csproj: RuntimeIdentifiers and conditionals applied.
- Build scripts: don't use absolute dotnet path or document alternatives.
- Code: runtime-guarded where necessary; no raw Windows P/Invoke without guards.
- Docs: update AGENTS.md and add quick instructions on how to test on Linux/macOS.
- CI: job matrix with publish + smoke run.

## Final Notes
- Prioritize quick experiment to know if problems are just native libs. If UI fails immediately, problem is env (dependencies) not .NET code.
- I can open a PR with conditional csproj and a sample CI workflow if you want — let me know which option you prefer.
