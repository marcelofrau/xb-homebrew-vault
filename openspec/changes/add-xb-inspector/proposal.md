## Why

Xbox Dev Mode homebrew development is constrained by unreliable official tooling: Visual Studio remote debugging is fragile, Xbox Device Portal file/log access is inconsistent, and runtime state is hard to inspect without rebuild/deploy loops. XBVault already owns the developer's Xbox connection context, so adding an Inspector gives developers a first-party workflow for live logs now and safe runtime interaction later.

This proposal is grounded in two prior exploration notes from `C:\Users\fraumar\workspace\_non_work_\archive`:
- `gemini-chat-2026-07-08T07-49-58-805Z.md`: broad brainstorm/specification for logs, REPL, protocol, naming, and architecture.
- `xb-inspector-revisao-critica.md`: critical review refining the architecture and identifying suspension behavior, backpressure, thread-safety, protocol versioning, threat-model, and flavor-maintenance risks.

## What Changes

- Add a new top-level XBVault **Inspector** experience for homebrew developers.
- Introduce an Inspector scan flow that uses the configured Xbox IP and searches a small TCP port range (`9000`-`9010`) for active inspector agents.
- Define protocol v1 for framed JSON messages covering handshake, live logs, REPL commands/results, errors, and future state inspection.
- MVP implementation focuses on live log discovery and streaming only.
- Document REPL support up front as a future phase: opt-in, compile-time gated, and executed safely on the app/main thread through a command queue.
- Define expected behavior for multiple active inspector sessions, disconnect/reconnect, backpressure, and unsupported protocol versions.
- Define the companion Xbox-side `xb-inspector` agent concept and recommended C++, C#, and Rust implementation flavors without requiring all flavors in the MVP.
- Add a threat model explicitly limiting the feature to trusted LAN / Xbox Dev Mode developer workflows and preventing accidental inclusion in public release builds.

## Capabilities

### New Capabilities
- `xb-inspector`: Live Inspector workflow for discovering inspector-enabled homebrew apps, streaming logs, and defining the protocol and future REPL/state inspection behavior.

### Modified Capabilities
- None.

## Impact

- UI: new `Inspector` section in the main navigation, with scan/status, session selection, log feed, and future REPL input area.
- Networking: new TCP client scan/connect service in XBVault, using configured Xbox host and bounded async connection attempts.
- Protocol: new versioned NDJSON-style inspector protocol with explicit message schemas.
- Settings/state: optional remembered inspector UI preferences may be added later, but the MVP should not depend on persisted inspector state.
- Xbox-side ecosystem: requires a companion `xb-inspector` library or test agent for real end-to-end use; the Vault side should be testable against a local mock agent.
- Security: future REPL execution is a deliberate remote-code-execution surface and must be disabled unless the Xbox-side agent is compiled with an explicit inspector/debug flag.
