# Rovia

Rovia is a reusable intelligent proxy routing engine that monitors, scores,
selects, and fails over between proxy routes while delegating protocol transport
to existing backends such as sing-box.

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
- Durable local JSON storage with unknown share-link parameters preserved.
- Bounded TCP health probes with rolling success, failure, latency, and jitter metrics.
- Deterministic stability-first scoring, hysteresis, and emergency failover.
- Deterministic sing-box configuration and managed process lifecycle.
- A CLI for importing, inspecting, probing, ranking, and connecting nodes.

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
rovia probe
rovia rank
rovia check-config <node-id>
rovia connect <node-id>
rovia connect-auto
```

`connect` runs as the foreground owner of sing-box and exposes a mixed HTTP/SOCKS
proxy at `127.0.0.1:2080` by default. Press Ctrl+C for an orderly disconnect.
Set `ROVIA_SING_BOX` to a custom executable path, `ROVIA_LISTEN_PORT` to change
the endpoint, or `ROVIA_DATA_DIR` to relocate local state.

## Current status

Rovia is a functional Core and CLI prototype. VLESS through sing-box is the only
implemented protocol/backend combination. Health checks currently measure TCP
reachability rather than end-to-end proxy throughput, and automatic background
monitoring is not yet hosted as a long-running service.

Credentials are stored in the local node file because sing-box requires them, but
Rovia never includes them in node display strings, normal CLI output, or scoring
diagnostics. Protect the local state directory as sensitive data.

## Non-goals

Rovia is not a VPN protocol, proxy server, complete GUI client, or replacement
for protocol implementations provided by sing-box or similar backends.
