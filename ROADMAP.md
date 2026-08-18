# Rovia Roadmap

This document is the handoff source for continuing Rovia development on another
machine. Read it together with `README.md` before changing architecture.

## Product direction

Rovia is a reusable, backend-independent adaptive proxy routing engine. It is not
a new proxy protocol and must not duplicate transport implementations provided by
sing-box or Xray.

```text
Desktop / CLI / future mobile clients
                  |
             Rovia Core
                  |
      backend and platform abstractions
          |                    |
      sing-box          platform networking
```

Core owns models, health evidence, scoring, selection, hysteresis, and failover.
Adapters own backend JSON, processes, TUN integration, and operating-system APIs.

## Current implementation

Implemented and validated:

- .NET 8 solution with Core, Config, Backends, Runtime, Windows platform, CLI,
  Avalonia Desktop, and test projects.
- VLESS, Trojan, VMess, and SIP002 Shadowsocks link parsing.
- Plain-text and Base64 HTTP subscription import with stable deduplication.
- Persisted subscription definitions, manual atomic provider refresh, stable node
  replacement, and preservation of user-edited node labels.
- Cross-process node-repository reload and serialized writers so a detached host
  observes Desktop/CLI imports and provider refreshes without lost updates.
- Provider-isolated scheduled refresh and immediate route re-evaluation after
  network address or availability changes.
- Atomic JSON node persistence with current-user Windows DPAPI credential encryption
  and automatic plaintext migration.
- Deterministic sing-box configuration and managed binary provisioning.
- TCP health samples, rolling success/failure metrics, stability-first scoring,
  deterministic ranking, hysteresis, and explicit failover state.
- Real proxy egress checks, multi-target diagnostics, warmed median proxy latency,
  and bounded quick download throughput measurements.
- Cross-process runtime status, speed-test, diagnostics, and clean disconnect.
- Detached runtime ownership independent of the invoking CLI/Desktop, repeated
  backend-exit supervision, and scheduled due-provider refresh.
- Bounded atomic health history and route decisions explaining emergency failover,
  hysteresis holds, score improvements, and active switching thresholds.
- Windows system-proxy snapshot and restoration.
- Startup preflight for occupied proxy ports plus interrupted-session recovery for
  orphaned sing-box processes and pending system-proxy snapshots.
- Bounded structured JSONL runtime logs and redacted support ZIP export from CLI
  or Desktop.
- Opt-in sing-box TUN configuration with strict automatic routes.
- Desktop controls for links, subscriptions, nodes, traffic mode, diagnostics,
  speed testing, connection, and disconnection.
- Persisted backend-independent routing/DNS policy with deterministic sing-box
  domain, suffix, CIDR, process, direct, proxy, reject, and proxied-DNS mapping.
- Generated policy configuration validates against installed sing-box 1.12 with
  an explicit local bootstrap resolver and proxied remote application DNS.
- Self-contained Windows x64 publish output with a separate CLI runtime folder.

Last known validation baseline:

```text
dotnet build Rovia.sln --configuration Release
dotnet test Rovia.sln --configuration Release --no-build

47 tests passed (including opt-in installed sing-box configuration validation)
0 build warnings
0 build errors
```

The existing real VLESS node is stored outside Git under `%LOCALAPPDATA%\Rovia`.
Credentials and generated binaries must never be committed.

## Active handoff

Current focus: **P0 — Windows reliability**. Detached background-host ownership is
implemented; Windows startup registration and operating-system event recovery remain.

Recommended next development slice:

1. Register the detached host as an optional per-user Windows startup task.
2. Add explicit Windows sleep/resume notification handling; network-interface and
   availability changes already trigger immediate health re-evaluation.
3. Add integration coverage for duplicate start, clean stop, and unclean process
   recovery around a controllable fake backend.

After that slice, continue P0 with elevated real-TUN validation and cleanup, local
ACL hardening, and Desktop tray/startup behavior. Continue P1 with node groups and
pinned routes; subscription persistence, scheduling, routing/DNS mapping, and
explainable health history are implemented.

## Architecture constraints

- `Rovia.Core` must not reference Config, Backends, Runtime, Desktop, or platform APIs.
- Parsers convert external formats only into `ProxyNode`.
- Backend adapters convert Core models into backend configuration.
- TUN and system proxy behavior belong to platform/runtime layers, not Core.
- Desktop and CLI must control the same runtime contracts.
- Never log complete UUIDs, passwords, tokens, subscription contents, or credentials.
- Prefer deterministic, explainable routing over machine learning.
- Preserve stability weighting above small latency improvements.
- Keep thresholds in policy objects rather than magic numbers.
- Align related C# declarations and assignments where it improves readability.
- Add a concise XML summary to each class.

## Priority roadmap

### P0 — Windows reliability

Goal: make the current prototype safe for daily use.

- Detached background ownership is implemented; optional per-user startup
  registration remains instead of requiring an elevated system service.
- Crash recovery and immediate network-interface re-evaluation are implemented;
  explicit sleep/resume notification handling remains.
- Validate real TUN connection under elevation and implement explicit route cleanup.
- Detect elevation, stale TUN interfaces, and backend early exits. Port-conflict
  detection is implemented.
- Structured rotating logs and redacted diagnostic export are implemented.
- Windows DPAPI credential encryption and plaintext migration are implemented;
  explicit local ACL hardening remains.
- Add Desktop system tray, close behavior, startup option, and update notifications.

Definition of done:

- Repeated connect/disconnect and crash-recovery tests never leave a dead proxy,
  stale route, orphaned sing-box process, or broken system proxy.
- System-proxy and TUN modes pass real browser, UDP, DNS-leak, sleep/resume, and
  network-switch tests.

### P1 — Routing and subscriptions

Goal: make adaptive routing useful with real multi-node subscriptions.

- Persist subscription definitions separately from imported nodes. Implemented.
- Manual refresh with atomic provider replacement is implemented; invoke due
  refresh automatically from the durable host after its lifecycle is established.
- User-edited node labels are preserved across refreshes; explicit pinned-route
  selections remain planned with node groups below.
- Add Hysteria2 and TUIC parsers and sing-box mapping.
- Core `RoutingRule`/`DnsPolicy` are persisted and mapped into deterministic
  sing-box route and DNS configuration. Desktop rule editing remains optional UI.
- Add regional/node groups, pinned routes, exclusions, and application policies.
- Feed passive backend failures into node health instead of relying only on probes.
- Bounded health history, CLI inspection, and Desktop route-switch reasons are implemented.

Definition of done:

- Subscription refresh does not duplicate nodes or expose credentials.
- Offline routes fail over automatically; small score changes do not cause flapping.
- Direct/proxy/block and DNS behavior are covered by deterministic configuration tests.

### P2 — Xray backend

Goal: prove backend independence and support Xray-specific users.

- Implement `XrayConfigBuilder`, `XrayBackend`, and verified binary provisioning.
- Publish accurate `BackendCapabilities` and reject unsupported node features early.
- Add same-node sing-box/Xray benchmarks for latency, throughput, memory, and CPU.
- Select a backend explicitly; do not claim one is faster without measurements.

Definition of done:

- Core and Config require no changes to add Xray.
- Common VLESS/Trojan/VMess nodes pass equivalent adapter tests.

### P3 — Android

Goal: validate the first mobile platform without copying Windows process behavior.

- Create a native Kotlin app/service prototype using Android `VpnService`.
- Embed a supported sing-box/libbox Android library; do not launch a Windows-style
  executable.
- Define a versioned, language-neutral bridge contract for connect, disconnect,
  permission, status, errors, metrics, and route changes.
- Reuse Core policy/configuration concepts without rewriting scoring in Kotlin.
- Run as a foreground service with correct notification and lifecycle behavior.

Definition of done:

- Real device can grant VPN permission, connect, route TCP/UDP/DNS, survive the UI
  closing, and disconnect without leaving a VPN state.

### P4 — iOS

Goal: implement Apple networking after the Android bridge contract is stable.

- Create a SwiftUI host and `NEPacketTunnelProvider` extension.
- Obtain and configure the Network Extension entitlement and App Group.
- Embed the Apple sing-box/libbox framework in the extension.
- Exchange versioned configuration/status through the App Group and provider API.
- Respect extension memory, lifecycle, signing, and App Store constraints.

Definition of done:

- A physical iOS device can install a signed build, start the packet tunnel, route
  TCP/UDP/DNS, report status to the host app, and stop cleanly.

### P5 — Shared client UI

Decide only after native mobile tunnels work:

- Keep native Kotlin/SwiftUI, or
- use Unity/Avalonia/MAUI as a shared foreground UI with thin native VPN plugins.

Unity must never own `VpnService` or `NEPacketTunnelProvider` runtime behavior. The
native service/extension must continue when the Unity UI process is suspended.

## Known limitations

- Windows TUN configuration is generated and checked, but real elevated TUN traffic
  has not been accepted as a release-quality validated feature.
- Runtime is a detached host independent of CLI/Desktop, but is not registered for
  per-user Windows startup and does not yet consume OS resume/network events.
- Subscription providers, manual refresh, and background due refresh are persisted.
- Routing/DNS policy is configured through `routing.json`; Desktop does not yet
  provide a visual policy editor.
- Only sing-box is implemented as a backend.
- Hysteria2 and TUIC enum values exist but their parsers/adapters do not.
- Credentials are protected with current-user Windows DPAPI. Non-credential node
  metadata remains readable JSON, and explicit file ACL hardening remains planned.
- Desktop has no tray integration, installer, updater, localization, or accessibility
  validation.
- Android and iOS projects do not exist yet.

## New-machine setup

Requirements:

- Git
- .NET 8 SDK
- Windows x64 for the current Desktop publish target

```text
git clone https://github.com/TristinWen/Rovia.git
cd Rovia
dotnet restore Rovia.sln
dotnet build Rovia.sln --configuration Release
dotnet test Rovia.sln --configuration Release --no-build
```

Create a self-contained Windows package:

```text
dotnet publish src/Rovia.Desktop/Rovia.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true --output publish/win-x64
```

Run `publish/win-x64/Rovia.Desktop.exe`. Keep the entire publish directory; the
Desktop starts `runtime/rovia.exe`, which requires the files beside it. sing-box is
installed on first connection under `%LOCALAPPDATA%\Rovia\bin` from the official
release after SHA-256 verification.

## Handoff checklist

Before each commit:

1. Ensure no credentials, local node files, binaries, publish output, or logs are staged.
2. Run the narrowest relevant tests, then the full build/tests before handoff.
3. Confirm port 2080 is not unexpectedly listening after runtime tests.
4. Confirm the original Windows proxy was restored after system-proxy tests.
5. Use an English Conventional Commit with a useful bullet-list body.
6. Push the commit and leave a clean worktree.

When starting the next milestone, update this file in the same commit that changes
the stated implementation or validation status.
