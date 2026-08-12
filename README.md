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
- A controllable runtime host with automatic monitoring and route history.
- Safe Windows system proxy activation with previous-setting restoration.
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
rovia probe
rovia rank
rovia check-config <node-id>
rovia connect <node-id>
rovia connect-auto
rovia status
rovia speed-test
rovia disconnect
```

`connect` runs as the owner of sing-box and exposes a mixed HTTP/SOCKS proxy at
`127.0.0.1:2080` by default. It verifies real HTTP egress before changing the
Windows system proxy. Use `disconnect` or press Ctrl+C for an orderly shutdown;
the exact previous system proxy configuration is then restored.
Set `ROVIA_SING_BOX` to a custom executable path, `ROVIA_LISTEN_PORT` to change
the endpoint, or `ROVIA_DATA_DIR` to relocate local state.

`speed-test` warms the proxy connection, reports the median of three application
latency samples, and streams at most 5 MB to estimate download throughput. It is
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

The desktop application can import VLESS links, display persisted nodes, start
automatic routing, inspect runtime status, and disconnect. It delegates parsing
and state to the existing libraries and controls the same runtime host as the CLI.

## Current status

Rovia is a functional Core, runtime, CLI, and Desktop prototype. VLESS through sing-box is the only
implemented protocol/backend combination. Health checks currently measure TCP
reachability for ranking, while connection activation separately verifies HTTP
egress. The runtime monitors routes every 30 seconds and applies policy-controlled
switching; it is not yet installed as an operating-system service.

Credentials are stored in the local node file because sing-box requires them, but
Rovia never includes them in node display strings, normal CLI output, or scoring
diagnostics. Protect the local state directory as sensitive data.

## Non-goals

Rovia is not a VPN protocol, proxy server, complete GUI client, or replacement
for protocol implementations provided by sing-box or similar backends.
