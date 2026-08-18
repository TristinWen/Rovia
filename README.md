# Rovia

Rovia is a reusable intelligent proxy routing engine that monitors, scores,
selects, and fails over between proxy routes while delegating protocol transport
to existing backends such as sing-box.

See [ROADMAP.md](ROADMAP.md) for architecture constraints, current limitations,
new-machine setup, prioritized milestones, and handoff requirements.

## Architecture

```text
Share link parser
        |
        v
    ProxyNode
        |
        v
   Rovia Core
        |
        v
Backend adapter
        |
        v
     sing-box
```

The dependency direction is intentionally constrained:

- `Rovia.Core` contains reusable domain and routing logic and has no project dependencies.
- `Rovia.Config` parses external configuration into Core models.
- `Rovia.Backends` adapts Core abstractions to external proxy backends.
- `Rovia.Cli` is a validation host for the reusable libraries.

## Features

- Backend-independent domain models and a reusable high-level routing engine.
- VLESS parsing for TLS, Reality, flow, WebSocket, HTTP, and gRPC parameters.
- Trojan, VMess, and SIP002 Shadowsocks parsing and sing-box mapping.
- Plain-text and Base64 subscription import with stable deduplication.
- Durable local JSON storage with unknown share-link parameters preserved.
- Bounded TCP health probes with rolling success, failure, latency, and jitter metrics.
- Deterministic stability-first scoring, hysteresis, and emergency failover.
- Deterministic sing-box configuration and managed process lifecycle.
- A CLI for importing, inspecting, probing, ranking, and connecting nodes.
- A controllable runtime host with automatic monitoring and route history.
- Safe Windows system proxy activation with previous-setting restoration.
- Optional transparent TUN capture for applications that ignore system proxies.
- A simple Avalonia desktop control panel shared with the same Core and runtime.

## Build and test

Rovia requires the .NET 8 SDK. A compatible `sing-box` executable is additionally
required for `connect` and `connect-auto`.

```text
dotnet build Rovia.sln --configuration Release
dotnet test Rovia.sln --configuration Release
```

## CLI

```text
rovia import "vless://..."
rovia list
rovia remove <node-id>
rovia subscription-add <name> <url>
rovia subscription-list
rovia subscription-refresh [subscription-id]
rovia subscription-remove <subscription-id>
rovia probe
rovia rank
rovia check-config <node-id>
rovia connect <node-id>
rovia connect-auto
rovia status
rovia speed-test
rovia diagnose
rovia export-diagnostics [output.zip]
rovia disconnect
```

`connect` runs as the owner of sing-box and exposes a mixed HTTP/SOCKS proxy at
`127.0.0.1:2080` by default. It verifies real HTTP egress before changing the
Windows system proxy. Use `disconnect` or press Ctrl+C for an orderly shutdown;
the exact previous system proxy configuration is then restored.
Set `ROVIA_SING_BOX` to a custom executable path, `ROVIA_LISTEN_PORT` to change
the endpoint, or `ROVIA_DATA_DIR` to relocate local state.

Set `ROVIA_MODE=tun` before connecting to enable transparent TUN capture. TUN
normally requires an elevated Windows process and does not modify the Windows
HTTP proxy. The default `system-proxy` mode remains safer for ordinary browsing.

`speed-test` warms the proxy connection, reports the median of three application
latency samples, and streams at most 512 KB for up to 3 seconds to estimate download throughput. It is
manual by design so periodic route monitoring does not consume significant data.

Rovia manages its own sing-box executable under `%LOCALAPPDATA%\Rovia\bin` on
Windows. If the binary is missing, the next connection downloads the latest
stable package from the official SagerNet GitHub release, verifies the SHA-256
digest published by GitHub, and installs it atomically. `ROVIA_SING_BOX` remains
available as an explicit override.

## Desktop

Build and launch the lightweight desktop shell after building the solution:

```text
dotnet run --project src/Rovia.Desktop --configuration Release
```

Subscription providers are persisted separately from nodes. Refresh atomically
replaces only the selected provider's nodes while preserving stable node labels.

The desktop application can import links, display persisted nodes, start automatic
routing, inspect runtime status, run speed tests and diagnostics, select system-
proxy or TUN mode, and disconnect. It also imports HTTP subscription URLs containing
VLESS, Trojan, VMess, and Shadowsocks entries. It delegates parsing and state to the
existing libraries and controls the same runtime host as the CLI.

## Current status

Rovia is a functional Core, runtime, CLI, and Desktop prototype. sing-box supports
the implemented VLESS, Trojan, VMess, and Shadowsocks models. Health checks measure TCP
reachability for ranking, while connection activation separately verifies HTTP
egress. The runtime monitors routes every 30 seconds and applies policy-controlled
switching; it is not yet installed as an operating-system service.

TUN configuration is opt-in and validated against sing-box, but requires an
elevated Windows process for real traffic capture. Mobile platform VPN bridges,
Xray backend support, Hysteria2/TUIC parsing, and subscription scheduling remain
future milestones.

Credentials are encrypted in the local node file with Windows DPAPI for the
current user. Rovia never includes them in node display strings, normal CLI output,
scoring diagnostics, or support ZIP exports.

## Non-goals

Rovia is not a VPN protocol, proxy server, complete GUI client, or replacement
for protocol implementations provided by sing-box or similar backends.
