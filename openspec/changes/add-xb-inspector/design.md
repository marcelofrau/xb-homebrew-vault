## Context

XBVault currently manages Xbox Dev Mode connection details, package catalog/install flows, installed packages, file explorer, tools, and local application logs. It already knows the configured Xbox host through `SettingsService.Current.XboxConnection` and `XboxDeviceService`, which makes a Vault-driven Inspector scan practical without Zeroconf or Xbox-side knowledge of the PC/Vault IP.

The idea comes from two exploration documents:

- `gemini-chat-2026-07-08T07-49-58-805Z.md` explored remote logs, REPL, state inspection, naming, protocol sketches, and library flavors.
- `xb-inspector-revisao-critica.md` refined the architecture and challenged optimistic assumptions around Xbox suspension, socket lifecycle, backpressure, thread origins for logs, security, and protocol versioning.

Core product framing:

```
XBVault = manager/workbench for Xbox Dev Mode homebrew
Inspector = runtime visibility/control for one inspector-enabled app
Tools = Xbox/device management
Logs = XBVault's own app logs
```

The Inspector should feel like a browser developer console adapted for Xbox homebrew: live logs in the output stream, and eventually a REPL input for runtime commands. The MVP deliberately stops at discovery + live logs to validate networking, lifecycle, and UI before introducing remote code execution.

## Goals / Non-Goals

**Goals:**

- Add an Inspector capability to XBVault for discovering inspector-enabled apps on the configured Xbox.
- Use a simple Vault-client / Xbox-server architecture: the Xbox agent listens passively; the Vault scans `9000`-`9010` on the known Xbox IP when the user clicks Scan.
- Define protocol v1 before implementation, including framing, versioning, handshake, log messages, errors, REPL messages, and future state inspection messages.
- Build the MVP around live log streaming, session status, disconnect handling, and multiple possible inspector endpoints.
- Document REPL semantics now so the protocol and UI do not paint us into a corner later.
- Require safe threading: logs can originate from any Xbox app thread; REPL/state mutations must execute on the Xbox app/main thread via a queue.
- Define a threat model that treats REPL as an explicit trusted-LAN developer-only remote-code-execution feature.

**Non-Goals:**

- MVP does not implement Lua execution, arbitrary REPL evaluation, or property editing.
- MVP does not ship all C++, C#, and Rust `xb-inspector` agents.
- MVP does not use mDNS/Zeroconf, UDP broadcast, or hash-derived per-app ports.
- MVP does not replace Visual Studio debugger features such as breakpoints, call stacks, or symbol-level debugging.
- MVP does not make the Xbox app depend on Vault availability; the app must keep running normally when the Vault is absent.

## Decisions

### Decision: Name the feature Inspector

Use **Inspector** for the XBVault UI and `xb-inspector` / `XbInspector` for companion libraries.

Alternatives considered:
- **DevTools**: familiar from browsers but conflicts with existing `Tools` section and sounds broader than runtime inspection.
- **Debugger**: creates false expectations around breakpoints/call stacks and feels heavier than the feature.
- **Live Console**: good for the console sub-view, too narrow for future state/property inspection.

### Decision: Xbox is passive server, Vault is active scanner/client

The Xbox-side agent opens a TCP listener on `9000`, falling back through `9010` if the port is unavailable. The app continues running whether or not Vault connects. XBVault scans the configured Xbox IP on demand.

```
Xbox app starts
  xb-inspector starts background listener
  bind 9000, else 9001...9010
  app/game loop continues

Vault Inspector Scan
  read configured Xbox IP
  connect attempts 9000..9010
  receive handshake(s)
  show session(s)
```

Alternatives considered:
- **Vault as TCP server, Xbox as client**: smoother auto-connect, but requires Xbox to know Vault IP or receive injected config.
- **mDNS/Zeroconf**: elegant and multi-device-friendly, but unnecessary because XBVault already knows the Xbox IP and mDNS adds multicast/platform/firewall complexity.
- **Hash-derived app ports**: avoids conflicts but requires matching hash logic in every agent flavor and Vault.
- **Bind random port + discovery**: requires discovery mechanism; rejected with mDNS for MVP.

### Decision: Use a small fixed scan range

The canonical range is `9000`-`9010` inclusive. It supports multiple concurrent or not-yet-released listeners without requiring broad scanning. Scan attempts should be asynchronous and bounded by short per-port timeouts.

Rationale:
- LAN closed-port failures generally return quickly.
- Xbox Dev Mode rarely keeps many UWP homebrew apps actively executing at once.
- The range is easy to document and mirror in each agent flavor.

### Decision: Protocol v1 uses newline-delimited JSON frames

Each message is one UTF-8 JSON object followed by `\n`. This keeps parsing simple across C++, C#, Rust, and XBVault, while avoiding ambiguous raw JSON stream boundaries.

Base envelope:

```json
{
  "event": "log",
  "protocol_version": 1,
  "id": "optional-correlation-id",
  "payload": {}
}
```

Rules:
- `protocol_version` MUST be present in `handshake` and SHOULD be present in every frame.
- Unknown `event` values are ignored or surfaced as warnings, not fatal.
- Unsupported protocol versions are rejected with a visible status message.
- `id` is required for request/response flows such as `repl_eval` / `repl_result`.

### Decision: MVP supports logs, protocol reserves REPL and state inspection

The first implementation should parse and display:
- `handshake`
- `log`
- `agent_error`
- socket disconnect/error state

The protocol and UI model should reserve later support for:
- `repl_eval`
- `repl_result`
- `state_snapshot`
- `state_update`
- `command_list`
- `command_invoke`

This prevents a future REPL phase from requiring an incompatible protocol rewrite.

### Decision: REPL is future, opt-in, compile-time gated

REPL allows the Vault to send script/code to execute inside the Xbox app. That is remote code execution by design. It is acceptable only as a developer tool in trusted LAN / Dev Mode contexts.

Xbox-side requirements for any REPL-capable agent:
- Feature is compiled only when an explicit build flag is enabled, such as `XB_INSPECTOR_ENABLED` plus a REPL-specific flag like `XB_INSPECTOR_REPL_ENABLED`.
- REPL is off by default in public/community release builds.
- Network thread MUST NOT mutate game/app state directly.
- Network thread enqueues commands; the app/main thread consumes and executes at a safe point.
- The agent reports whether REPL is supported in `handshake.payload.capabilities`.

### Decision: Separate queues for logs and commands on Xbox

Logs and commands have different threading shapes:

```
Logs:
  many app threads ──> MPSC bounded queue ──> network thread ──> Vault

Commands / REPL:
  network thread ──> command queue ──> app/main thread ──> result queue ──> network thread
```

MVP Vault does not implement the Xbox agent, but the design documents this requirement so companion libraries do not adopt unsafe shortcuts.

### Decision: Backpressure is explicit

Xbox-side log queues must be bounded. When the outbound queue is full, agents should drop oldest queued log events and send a synthetic warning when possible:

```json
{
  "event": "log",
  "protocol_version": 1,
  "payload": {
    "level": "WARN",
    "tag": "xb-inspector",
    "message": "128 log events dropped due to backpressure"
  }
}
```

Vault-side UI must also bound in-memory log history per session to avoid rendering or memory problems.

### Decision: Agent flavors are tracked, not promised in one change

Recommended flavors:

| Flavor | Network | Script/bind | Notes |
|---|---|---|---|
| C++ | Winsock2 non-blocking or background thread | Sol2 + Lua C API | Best target for many native homebrews; low overhead; mature Lua binding. |
| C# | `System.Net.Sockets.TcpListener` / async tasks | MoonSharp | Easy managed prototype; MoonSharp is pure C# and suitable for UWP-style sandbox constraints. |
| Rust | `std::net` thread or async runtime if allowed | mlua | Strong safety but more binding boilerplate. |

The critical review notes that these flavors are not equal in maintenance cost. The first real agent should be selected based on validation goal: C# for fastest prototype, C++ for most representative homebrew adoption.

## Risks / Trade-offs

- **Xbox suspension/socket lifecycle assumptions are wrong** → Do not rely on the OS freeing ports instantly. Keep port fallback, short scan range, and explicit reconnect behavior. Add empirical suspension tests before claiming multi-app guarantees.
- **REPL creates unauthenticated remote code execution** → Treat REPL as future opt-in, compile-time gated, trusted-LAN only, and document it prominently.
- **Log floods overload the Xbox or Vault UI** → Require bounded queues, drop-oldest behavior, synthetic backpressure warnings, and bounded UI history.
- **TCP framing bugs corrupt message parsing** → Use newline-delimited JSON rather than concatenated raw JSON.
- **Protocol drift between Vault and embedded agents** → Include `protocol_version` from day one and reject unsupported major versions cleanly.
- **Different agent flavors diverge** → Keep protocol capability-based. Do not require every flavor to support REPL/state watch at the same time.
- **Scan reports false positives on unrelated services** → Require valid `handshake` within a short timeout before surfacing a session.
- **UI scope gets too big** → Deliver MVP as Console/log stream first; leave State/Profiler/REPL as documented future phases.

## Migration Plan

No existing user data migration is required for MVP.

Rollout sequence:
1. Add the Inspector UI and Vault-side network/protocol services behind normal UI entry points.
2. Include a local/mock inspector agent for manual validation or test harness use.
3. Validate against one real Xbox-side agent flavor before documenting community usage.
4. Introduce REPL only in a later change after protocol/log stream are stable.

Rollback:
- The Inspector tab can be removed without affecting existing Browse/Installed/File Explorer/Tools/Settings/Logs behavior.
- Protocol changes are isolated to new services and models.

## Open Questions

- Should the first real agent flavor be C# for speed or C++ for community relevance?
- Should the Vault auto-connect after scan when exactly one valid session is found, or require explicit selection?
- Should log history persist to disk per Inspector session, or remain in-memory only for MVP?
- What exact Xbox Dev Mode behavior occurs when two inspector-enabled UWP apps are alternated quickly: suspended socket held, process terminated, or bind immediately released?
- Should REPL be Lua-only initially, or should registered commands be introduced as an intermediate safer phase before arbitrary Lua?
