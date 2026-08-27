# XHTTP stability guide

## Root cause found in Rovia

Rovia previously emitted an `xhttp` transport inside a sing-box configuration. The
installed sing-box 1.13.16 rejects that configuration with `unknown transport type:
xhttp`. Rovia now routes VLESS XHTTP nodes through Xray and keeps all existing WS,
HTTP, gRPC, VMess, Trojan, and Shadowsocks nodes on sing-box.

The second likely source of Codex reconnects is stale HTTP/2 connection reuse. Rovia
now reads the standard Xray `extra.xmux` object and offers a LongStream profile that
periodically retires reusable connections:

```json
{
  "maxConcurrency": 6,
  "cMaxReuseTimes": 24,
  "hMaxRequestTimes": 150,
  "hMaxReusableSecs": 90,
  "hKeepAlivePeriod": 15
}
```

These are test defaults, not universal optimum values. `hKeepAlivePeriod` sends an
HTTP/2 connection keepalive understood by Xray. Rovia never injects bytes into the
proxied VLESS/TLS stream. An H2 keepalive does not guarantee that Cloudflare will
retain an HTTP response whose body has no application data.

## Profiles and rollback

Import the server-provided XHTTP link, find its node ID with `rovia list`, then run:

```text
rovia xhttp-profiles <node-id>
```

- `XHTTP-Default` omits XMUX overrides and follows Xray defaults.
- `XHTTP-LongStream` uses `stream-one` plus the bounded reuse values above.
- `WS-Fallback` uses the same endpoint, host, and path. Import it only when that path
  is actually enabled for WS on the server.

Rollback is immediate: disconnect, select the original Default or WS node, and
reconnect. Set `ROVIA_XRAY` to an already verified Xray executable to bypass managed
provisioning. XHTTP currently supports Rovia system-proxy mode; TUN remains on
sing-box and is rejected explicitly for XHTTP.

## Cloudflare and protocol boundaries

Cloudflare Workers permit a streamed HTTP response to continue while the client is
connected, but outbound TCP sockets to Cloudflare IP ranges are blocked. A Worker
cannot reliably classify a close as a client reset, edge reset, or target reset from
the client-side Rovia process alone. Worker-side correlation IDs, close-reason
aggregation, upstream proxy fallback, Durable Object session coordination, and
`stream-up` or `packet-up` support must be implemented and tested in the EdgeTunnel
Worker repository. That source is not present in this repository.

Rovia deliberately does not generate `stream-up` or `packet-up` links and does not
claim protocol-level downstream padding support. Current Xray protocol behavior is
documented in the upstream XHTTP discussion; Cloudflare socket restrictions and
Worker limits are documented by Cloudflare:

- https://github.com/XTLS/Xray-core/discussions/4113
- https://developers.cloudflare.com/workers/runtime-apis/tcp-sockets/
- https://developers.cloudflare.com/workers/platform/limits/

## Validation record

Automated coverage checks standard `extra.xmux` parsing, deterministic Xray config,
sing-box rejection, and three-profile round trips. The generated configuration was
also accepted by official Xray 26.3.27 after its GitHub SHA-256 asset digest was
verified.

Real-network acceptance still requires the user's endpoint and workload:

1. Run Default and LongStream separately for two hours of Codex use.
2. Record reconnects, interrupted generations, and `-1` latency samples.
3. Test WS only if the Worker exposes the generated host/path combination.
4. Test a Cloudflare-hosted destination separately from Google/GitHub; a failure may
   be the documented Worker TCP restriction rather than an XHTTP failure.
5. Keep the old production Worker and link unchanged until the new test Worker meets
   the requested success criteria.

Cloudflare Free/Paid consumption cannot be measured from Rovia. XMUX connection
rotation increases connection establishment modestly but does not add Worker-side
storage or Durable Object charges. Any Worker-side diagnostic store or split-session
implementation needs a separate cost measurement against the selected Cloudflare plan.
