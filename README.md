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

## Current status

Rovia is currently a Core and CLI prototype. The repository contains the initial
.NET 8 solution structure; routing functionality will be implemented incrementally.

## Non-goals

Rovia is not a VPN protocol, proxy server, complete GUI client, or replacement
for protocol implementations provided by sing-box or similar backends.
