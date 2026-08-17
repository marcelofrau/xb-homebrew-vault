# Glossary

Technical terms used in XB Homebrew Vault, explained in plain language.

---

## A

### Appx / Appxbundle
A file format used by Windows and Xbox to package apps. `.appx` is a single package; `.appxbundle` is a collection of packages (e.g., for multiple architectures). Think of them like `.apk` files on Android.

### Avalonia UI
The cross-platform UI framework that XB Homebrew Vault is built with. It allows the same app to run on Windows, macOS, and Linux with a native look.

---

## B

### Backend
The part of the app that does the work (connecting to Xbox, installing packages, etc.) — as opposed to the user interface you see and click on.

---

## C

### Catalog
The collection of homebrew apps available for installation. XB Homebrew Vault fetches the catalog from the Emulation Revival project, which curates and maintains the list.

### CLI
**Command-Line Interface** — running the app from a terminal with special options (like `--check` or `--reset-data`). See [CLI Reference](CLI-Reference.md).

### Console
The Xbox's Developer Mode environment. When you "enter Dev Mode," your Xbox switches from the normal retail interface to a development environment where you can run unsigned code.

### Crash Dump
A file created when an app crashes on the Xbox. It contains diagnostic information that can help developers understand what went wrong.

---

## D

### Dev Mode
**Developer Mode** — a special mode on Xbox that allows running homebrew apps, emulators, and development tools. You need to enable it through the Xbox Dev Mode app from the Microsoft Store.

### Device Portal
**Xbox Device Portal** — a web-based interface built into Xbox Dev Mode. It provides a browser-based way to manage packages, files, and settings. XB Homebrew Vault replaces much of this functionality with a desktop app.

---

## E

### Emulation Revival
A community project that curates and maintains a catalog of homebrew apps, emulators, and tools for Xbox Dev Mode. XB Homebrew Vault uses their catalog as its package source.

---

## F

### File Explorer (in-app)
The built-in file browser that connects to your Xbox via SSH/SFTP. Lets you browse, upload, download, and manage files on your Xbox's file system.

---

## H

### Homebrew
Software created by the community for platforms that don't officially support it. In this context, homebrew refers to apps, emulators, and games that run on Xbox in Developer Mode.

---

## I

### Inspector
A developer tool in XB Homebrew Vault that connects to XRay agents running inside homebrew apps. Provides live log streaming and a Lua REPL for remote diagnostics. See [Inspector](Inspector.md).

---

## J

### JSON
**JavaScript Object Notation** — a common format for storing and exchanging data. The catalog and Xbox Device Portal API both use JSON.

---

## L

### Loopback Exemption
A Windows/Xbox security setting that allows an app to access network services running on the same device. Some homebrew apps need this to communicate with the Xbox's own Device Portal API.

### Lua
A lightweight programming language. XB Homebrew Vault's Inspector uses Lua to let you send commands to running apps on your Xbox (the "REPL" feature).

---

## M

### MSIX / MSIXBundle
Modern versions of the Appx format. `.msix` is a single package; `.msixbundle` is a collection. Both can be installed with XB Homebrew Vault.

### MVVM
**Model-View-ViewModel** — the software architecture pattern used by XB Homebrew Vault. It separates the user interface (View) from the data and logic (ViewModel), making the code cleaner and easier to maintain.

---

## N

### NTFS
A file system used by Windows. USB drives for Xbox Dev Mode must be formatted as NTFS (not FAT32 or exFAT) for proper permission handling.

---

## P

### Package
An installable app or tool for Xbox. Packages come in `.appxbundle`, `.msixbundle`, `.appx`, or `.msix` formats.

### Package Manager
The Xbox system service that handles installing, removing, and managing packages. When XB Homebrew Vault installs something, it communicates with this service.

---

## R

### REM
**Read-Eval-Print Loop** — an interactive programming environment where you type commands and see results immediately. XB Homebrew Vault's Inspector includes a Lua REPL for sending commands to running apps.

### REST API
A way for apps to communicate over HTTP. The Xbox Device Portal uses a REST API, and XB Homebrew Vault's Portal Explorer uses it to browse app files.

---

## S

### SFTP
**SSH File Transfer Protocol** — a secure way to transfer files over a network. XB Homebrew Vault's File Explorer uses SFTP to browse and manage files on your Xbox.

### SSH
**Secure Shell** — a protocol for securely connecting to another computer over a network. Xbox Dev Mode uses SSH for file access and command execution.

### Splash Screen
The first screen you see when launching XB Homebrew Vault. It shows the app logo while the app initializes in the background.

---

## T

### TCP
**Transmission Control Protocol** — the underlying protocol for most internet and network communication. XB Homebrew Vault uses TCP to connect to the Xbox Device Portal and to XRay agents.

---

## U

### UWP
**Universal Windows Platform** — a Microsoft platform for building apps that run on Windows, Xbox, and other Microsoft devices. Most Xbox homebrew apps are built as UWP apps.

### USB Permission Wizard
A tool in XB Homebrew Vault that sets up NTFS file permissions on a USB drive so the Xbox can read and write to it. Windows-only. See [Dev Tools — USB Permission Wizard](Dev-Tools.md#usb-permission-wizard).

---

## V

### ViewModel
In the MVVM pattern, a ViewModel is the "middle layer" that holds the data and logic for a screen. It connects the user interface to the underlying services.

---

## W

### WebSocket
A protocol for persistent, two-way communication between apps. XB Homebrew Vault uses WebSockets to receive real-time performance data from the Xbox.

---

## X

### X-Files
A popular homebrew file explorer app for Xbox Dev Mode. XB Homebrew Vault includes a wizard to set it up with the proper loopback exemptions.

### Xbox Dev Mode
See **Dev Mode**.

### XRay
A lightweight diagnostics library for Xbox homebrew apps. When an app includes XRay, XB Homebrew Vault can connect to it for live log streaming and remote commands. See [Inspector](Inspector.md).
