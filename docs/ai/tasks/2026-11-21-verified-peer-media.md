# 2026-11-21 — Verified peer-assisted media

The SDK downloader accepts an optional LAN peer source. The trusted origin
supplies a piece table matching the media hash, size and piece size. A piece
may come from a sparse cache, one of the LAN peers or the original storage
URI. Every byte from a peer is verified with the origin digest before entering
a file or reaching the progressive consumer. The origin handles missing pieces.
An incomplete prefix is verified before resuming in peer mode. Lookahead can
prepare one independent piece while an earlier request is outstanding, with
time, memory and cancellation limits. Unsupported byte ranges fall back to a
continuous origin response. Existing callers without a configured peer source
keep the existing no-peer downloader path.

## LAN wire format

The platform discovery record advertises `media=1` and the server certificate
DER SHA-256 as `cert` (hex) on the normal Mizon Bonjour TCP endpoint. The client
connects over TLS pinned to that leaf. A single request is 44 bytes. Bytes
0–3 are ASCII `MZP1`. Bytes 4–35 are the binary SHA-256 of the full asset.
Bytes 36–39 and 40–43 are big-endian 32-bit piece size and zero-based piece
index. The response starts with a big-endian 32-bit length (0 if unavailable)
followed by exactly that number of bytes. There is no token, arbitrary path,
URL, file listing or peer-supplied manifest on this connection. Platform
upload handlers must only serve media explicitly published by the trusted
Mizon origin for sharing, and enforce their own bounded listener lifetime.

## Checks

The diagnostic harness exercises older downloader paths, sparse storage, two
real TLS peers plus origin ranges, corrupt-peer fallback, incomplete-prefix
verification, certificate pin rejection and generated API operations. The
prebuilt manifest is opt-in at the server and is independent of LAN reachability.
