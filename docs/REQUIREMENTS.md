# Requirements

## Functional

| ID | Requirement | Version |
|----|-------------|---------|
| F01 | User can configure Xbox Dev Mode connection (address, port, username, password) | v0.2 |
| F02 | Password is stored obfuscated (not plaintext) | v0.2 |
| F03 | Application tests the Xbox connection and reports status | v0.2 |
| F04 | Application lists installed packages from the Xbox | v0.2 |
| F05 | Application fetches the Emulation Revival catalog from 7 source pages | v0.3 |
| F06 | User can browse catalog items with search, category, and compatibility filters | v0.3 |
| F07 | User can view full details of a catalog item | v0.3 |
| F08 | User can download and install a package on the Xbox | v0.4 |
| F09 | Application analyzes downloaded packages for dependencies | v0.4 |
| F10 | User can uninstall installed packages | v0.4 |
| F11 | Application shows download and install progress | v0.4 |
| F12 | Application caches downloaded packages locally | v0.4 |
| F13 | User can launch, suspend, and terminate installed packages | v0.8 |
| F14 | Application shows which installed packages are currently running | v0.8 |
| F15 | User can view, enable/disable, and delete crash dumps | v0.8 |
| F16 | User can view network configuration (interfaces, IP, WiFi, link speed) | v0.8 |
| F17 | User can list and kill running processes on the Xbox | v0.8 |
| F18 | User can view system information (OS, console type, CPU, memory) | v0.8 |
| F19 | User can capture screenshots from the Xbox | v0.8 |
| F20 | User can view real-time performance metrics (CPU, memory, GPU, temperature) | v0.8 |
| F21 | User can restart or shutdown the Xbox from the app | v0.8 |
| F22 | User can browse Xbox file system | v0.8 |
| F23 | Application detects real network link speed instead of dial-up bps | v0.8.1 |

## Non-Functional

| ID | Requirement | Priority |
|----|-------------|----------|
| NF01 | Application targets .NET 8 | High |
| NF02 | UI uses Xbox 360 Blades theme colors | High |
| NF03 | Application uses MVVM pattern with CommunityToolkit.Mvvm | High |
| NF04 | Application obfuscates stored passwords (not cryptographic security) | Medium |
| NF05 | Release builds are self-contained, single-file ZIP | High |
| NF06 | CI runs on every push, release on tag | High |
| NF07 | All dialog windows follow a consistent template (green gradient title bar, black border, close button) | High |
| NF08 | Real-time performance data uses WebSocket instead of polling | Medium |
