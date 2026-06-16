# Requirements

## Functional

| ID | Requirement | Phase |
|----|-------------|-------|
| F01 | User can configure Xbox Dev Mode connection (address, port, username, password) | 1 |
| F02 | Password is stored obfuscated (not plaintext) | 1 |
| F03 | Application tests the Xbox connection and reports status | 1 |
| F04 | Application lists installed packages from the Xbox | 1 |
| F05 | Application fetches the Emulation Revival catalog from 7 source pages | 2 |
| F06 | User can browse catalog items with search, category, and compatibility filters | 2 |
| F07 | User can view full details of a catalog item | 2 |
| F08 | User can download and install a package on the Xbox | 3 |
| F09 | Application analyzes downloaded packages for dependencies | 3 |
| F10 | User can uninstall installed packages | 3 |
| F11 | Application shows download and install progress | 3 |
| F12 | Application caches downloaded packages locally | 3 |

## Non-Functional

| ID | Requirement | Priority |
|----|-------------|----------|
| NF01 | Application targets .NET 8 | High |
| NF02 | UI uses Xbox 360 Blades theme colors | High |
| NF03 | Application uses MVVM pattern with CommunityToolkit.Mvvm | High |
| NF04 | Application obfuscates stored passwords (not cryptographic security) | Medium |
| NF05 | Release builds are self-contained, single-file ZIP | High |
| NF06 | CI runs on every push, release on tag | High |
