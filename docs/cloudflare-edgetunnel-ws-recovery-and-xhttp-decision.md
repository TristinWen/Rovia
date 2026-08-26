# Cloudflare EdgeTunnel WS Recovery and XHTTP Decision Record

Last reviewed: 2026-08-26

## Final decision

Keep the existing Cloudflare-only VLESS over WebSocket service unchanged. Do not deploy an XHTTP entry.

The existing EdgeTunnel Worker is a WebSocket application that parses VLESS and opens outbound TCP sockets. Standard XHTTP is an Xray-core transport with request correlation, streaming behavior, and several operating modes. Changing a share-link parameter, adding an ordinary HTTP handler, or assigning another UUID to the Worker would not create a standards-compatible XHTTP server.

A real XHTTP endpoint normally requires an Xray-core origin behind Cloudflare. That violates the Cloudflare-only requirement. No Worker source, route, DNS record, UUID, environment variable, custom domain, or Cloudflare deployment was changed as part of this review.

## Known-good WS connection

```text
vless://6805c634-c8eb-43e9-8920-699aba969a0b@tristinwen.ccwu.cc:443?encryption=none&security=tls&sni=tristinwen.ccwu.cc&fp=chrome&insecure=0&allowInsecure=0&type=ws&host=tristinwen.ccwu.cc&path=%2F#edgetunnel
```

Treat this UUID as a credential. Rotate it if the repository is public or the link has been disclosed beyond trusted users.

## How the current service works

```text
VLESS client
  -> TLS and WebSocket / on tristinwen.ccwu.cc:443
  -> Cloudflare edge terminates TLS
  -> EdgeTunnel Worker accepts the WebSocket upgrade
  -> Worker validates the VLESS UUID and target
  -> Worker opens an outbound TCP socket
  -> destination
```

The layers have separate jobs:

- VLESS carries authentication and destination information.
- WebSocket packages the VLESS byte stream as an HTTP upgrade that Workers can receive.
- TLS protects the client-to-Cloudflare connection.
- The Worker is the proxy endpoint; it is not an Xray-core server.

## Settings that must remain unchanged

| Item | Required value |
| --- | --- |
| Hostname | `tristinwen.ccwu.cc` |
| Port | `443` |
| UUID | `6805c634-c8eb-43e9-8920-699aba969a0b` |
| Security | TLS |
| SNI | `tristinwen.ccwu.cc` |
| Transport | WebSocket |
| WebSocket Host | `tristinwen.ccwu.cc` |
| WebSocket path | `/` |

Do not add a path branch to the production Worker merely to imitate XHTTP. Do not replace `type=ws` with `type=xhttp` or `type=http`; the client and server transport implementations must match.

## Verification procedure

1. Export or copy the known-good link before any Cloudflare maintenance.
2. Confirm DNS for `tristinwen.ccwu.cc` still resolves through Cloudflare.
3. Import the exact link into the existing client and connect.
4. Verify HTTPS access through the proxy with at least two independent sites.
5. If the client has transport diagnostics, confirm that the TLS handshake and WebSocket upgrade both succeed.
6. After any unrelated Worker deployment, repeat the connection test before closing the Cloudflare dashboard.

## Common failures

| Symptom | Check |
| --- | --- |
| TLS or certificate error | DNS proxy status, custom domain attachment, certificate issuance, SNI, and system clock. |
| WebSocket upgrade fails | Worker route/custom domain, `Upgrade` handling, WebSocket support, Host, and path `/`. |
| Authentication fails | Worker UUID must exactly match the link UUID. |
| Cloudflare error page | Worker deployment status, route conflicts, account/zone selection, and Worker logs. |
| Some sites fail | Worker outbound-socket restrictions, destination DNS, IPv4/IPv6 behavior, and Cloudflare-to-Cloudflare connection limitations. |
| Link with `type=xhttp` fails | Expected: no Cloudflare-only XHTTP endpoint was deployed. |

## Safe rollback

If a future Worker update breaks the WS service:

1. Stop making changes and keep a copy of the failed version for diagnosis.
2. Open the Worker deployment/version history in the correct Cloudflare account.
3. Roll back to the last version that served the known-good WS link.
4. Restore the previous custom-domain or route association if it was changed.
5. Restore the previous DNS record and orange-cloud proxy status if they were changed.
6. Test the exact known-good link again.

Rollback must restore code, bindings, routes, and DNS together when more than one of them changed. Do not rotate the UUID during an outage unless compromise is the cause, because doing so also invalidates every saved client link.

## Manual rebuild after complete loss of access

1. Sign in to the Cloudflare account that owns the zone and Worker. The Cloudflare plugin account inspected on 2026-08-26 contained no Workers or Worker custom domains, so it was not the live deployment account.
2. Obtain the exact EdgeTunnel source and configuration used by the working deployment. This Rovia repository does not contain the Worker source.
3. Create a new Worker only if the original Worker cannot be restored.
4. Configure the Worker with UUID `6805c634-c8eb-43e9-8920-699aba969a0b` and preserve VLESS over WebSocket on `/`.
5. Deploy the Worker without adding XHTTP, REALITY, or unrelated transport branches.
6. Attach `tristinwen.ccwu.cc` as the Worker custom domain or matching route.
7. Ensure the DNS record is proxied by Cloudflare and TLS is active.
8. Import the exact known-good WS link and test TLS, WebSocket upgrade, authentication, and proxied HTTPS egress.
9. Once service is restored, separately back up the deployed source, binding names (not secret values), route/custom-domain details, and deployment version identifier.

If the original source is unavailable, review any replacement EdgeTunnel fork before deployment. Public forks can differ in UUID handling, proxy-IP behavior, subscription pages, DNS behavior, and security assumptions.

## Why there is no new UUID or link

A reserved UUID or an XHTTP-looking VLESS URI would imply that a compatible server exists when it does not. This record intentionally provides no new connection. If the requirements later allow a VPS or container origin, XHTTP can be designed as a separate hostname without touching this WS Worker.

## Authoritative references

- Xray transport compatibility: https://xtls.github.io/en/config/transport.html
- XHTTP design and modes: https://github.com/XTLS/Xray-core/discussions/4113
- Cloudflare WebSocket support: https://developers.cloudflare.com/network/websockets/
- Cloudflare Workers TCP sockets: https://developers.cloudflare.com/workers/runtime-apis/tcp-sockets/
- sing-box transport list (Rovia backend): https://sing-box.sagernet.org/configuration/shared/v2ray-transport/

