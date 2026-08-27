# Rovia for Android

Rovia Desktop cannot be republished directly as an Android application. Windows
system-proxy APIs, detached executables, and child-process ownership do not map to
Android. The Android client should keep Rovia's routing concepts while using the
platform VPN lifecycle.

## Recommended architecture

```text
Jetpack Compose UI
        |
Android route controller
        |
Foreground VpnService
        |
versioned mobile bridge
        |
sing-box/libbox Android library
        |
Android TUN file descriptor
```

- Build the host application in Kotlin with Jetpack Compose.
- Implement the tunnel as a foreground service derived from `VpnService` and
  protect it with `android.permission.BIND_VPN_SERVICE`.
- Embed a supported Android sing-box/libbox library. Do not launch the Windows
  `sing-box.exe`, `xray.exe`, or `rovia.exe` binaries.
- Keep UI and tunnel ownership separate. Closing or recreating an Activity must
  not terminate an active tunnel.
- Use Android Keystore-backed encrypted storage for node credentials.
- Keep transient traffic logs in a bounded memory buffer, matching Desktop.

The first Android release should support the transports that the embedded mobile
core supports reliably. VLESS XHTTP requires a mobile-compatible Xray/libXray path
or confirmed support in the selected library; it must not be advertised merely
because Desktop Xray supports it.

## Shared contract

Define a versioned JSON contract before building the UI:

- connect and disconnect requests;
- selected node and routing policy;
- permission-required, connecting, connected, reconnecting, and stopped states;
- active core, local addresses, latency, traffic totals, and route-switch reason;
- structured, redacted errors and incremental live-log sequence numbers.

The contract should reuse Rovia model names and semantics, but contain no .NET or
Windows-specific types. Golden JSON fixtures can then validate both C# and Kotlin
implementations.

## Delivery slices

### 1. Device prototype

Create an `android/` Gradle project with one Compose screen, VPN permission flow,
foreground notification, service binding, and clean start/stop. Prove a direct
TUN loop on a physical phone before adding subscriptions or adaptive routing.

### 2. One real transport

Integrate the mobile proxy library and validate one VLESS TLS/WebSocket node over
Wi-Fi and cellular. Verify DNS, IPv4, IPv6, reconnect after network changes,
screen-off survival, battery use, and cleanup after force-stop.

### 3. Rovia routing

Add encrypted node import, health evidence, stability-first selection, hysteresis,
and bounded failover. Port behavior through the shared contract and golden tests;
do not copy Windows process-management code.

### 4. Release hardening

Add always-on/per-app options only after basic lifecycle tests pass. Validate OEM
battery restrictions, crashes, device reboot, VPN revocation, captive portals,
metered networks, and competing VPN applications.

## Publishing

1. Reserve a permanent package name such as `com.rovia.client` before the first
   public release.
2. Create adaptive launcher assets from the Rovia icon, including foreground and
   monochrome layers.
3. Generate an upload keystore outside Git and keep passwords out of Gradle files.
4. Build a signed release Android App Bundle (`.aab`). New Google Play apps use
   Android App Bundles and Play App Signing.
5. Complete Android developer verification, Play data-safety declarations, VPN
   disclosure/privacy materials, content rating, screenshots, and store listing.
6. Release through internal testing, then closed testing, then staged production.
7. Keep a separately signed APK only for direct distribution or non-Play stores.

Official references:

- [Android VPN developer guide](https://developer.android.com/develop/connectivity/vpn)
- [Prepare an Android release](https://developer.android.com/studio/publish/preparing)
- [Android app signing](https://developer.android.com/studio/publish/app-signing)
- [Android App Bundle testing](https://developer.android.com/guide/app-bundle/test)

## Release gate

Do not publish until a signed release build passes on at least one physical phone
and one tablet-sized target, across Wi-Fi/cellular switching and screen-off. The
test must prove that disconnect and crash recovery leave no active VPN, DNS route,
or foreground notification behind.
