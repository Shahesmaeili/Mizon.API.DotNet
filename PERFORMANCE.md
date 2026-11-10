# SDK performance and NativeAOT verification

Verified on 2026-11-08 for `Mizon.API` and `Mizon.API.Client`, targeting .NET 10.

## Changes

- All 40 built-in operations use source-generated JSON metadata. GET operations build typed, escaped query strings; no reflection is needed by their request/response paths.
- Normal responses deserialize directly from the HTTP body. NDJSON search uses `DeserializeAsyncEnumerable` with `topLevelValues: true`, cancellation, and disposal when enumeration stops early. It does not wait for the complete response.
- POST data is serialized once to UTF-8, reused for the cache key and compression, and sent without an intermediate UTF-16 JSON string or extra compressed-buffer copy.
- Response caches are partitioned by SDK instance, session, device metadata, authorization requirement and response type. Errors and nonpositive TTLs are not cached. Cache hits return a separate envelope instead of modifying an earlier response's `IsFromCache` property. DTO content remains shared within the same cache entry and should be treated as read-only.
- Logout always reaches the server. A late login cannot restore a token after logout or replace a newer session.
- Caller cancellation and request timeouts are distinguished. `Dispose` cancels outstanding work; externally supplied `HttpClient`/`IMemoryCache` remain caller-owned. The DI-created API owns its named `HttpClient`.
- `AddMizonApi` now resolves the configured named HTTP client. Connection lifetime is bounded for DNS refresh, and the singleton token/session is preserved.
- `DownloadToCacheAsync` uses a bounded buffer instead of materializing each media chunk. `DownloadAsync` still supplies independently owned chunk bytes for existing progressive consumers. Small image buffers adapt to file size.
- Download fixes cover incomplete cache files, unaligned resume, short bodies, malformed ranges, servers that ignore Range, final-path publication, cancellation and body timeouts. Cache path layout is unchanged. Validation uses size/HTTP range information, not an unconditional full-file hash.
- `DownloadSession.Dispose` cancels active/paused downloads and releases their cache lease. SignalR disposal releases subscriptions, callbacks, pending work and the connection. Reconnect restores registration and membership using the current token; JWT identity is parsed once per token.
- Both libraries declare `IsAotCompatible`; application-only publish/globalization flags were removed from library projects. Existing Microsoft SDK dependencies were aligned to 10.0.14. The six corresponding direct references in the Windows app were aligned to prevent NuGet downgrade errors. No UI code was changed.
- Client `GlobalUsings` now includes the shared serialization namespace; unnecessary local imports were removed. Standard framework usings remain implicit.

## Verification

`Tools/SdkDiagnostics` contains self-contained tests; mutation operations use a fake handler and never call the database. Checks include:

- All 40 operations, nested DTOs, optional values, query encoding, request compression and response casing with JSON reflection disabled.
- Cache/session isolation, authentication errors, null/malformed JSON, cancellation before cache hits, request timeout, late login, DI configuration and ownership.
- Streaming first-result delivery before response completion, early disposal, empty results and cancellation of buffered results.
- 60 media assertions, including concurrent writers, Range fallback, truncated transfers, resume, small images, and 100 cancellation/disposal races.
- Real loopback SignalR negotiation/WebSockets, source-generated payloads, concurrent lifecycle calls, navigation, reconnect, token changes and disposal.

The complete runner was published and executed as Windows x64 NativeAOT with .NET runtime/ILCompiler **10.0.14**. It reports `DynamicCode=False; JsonReflection=False`. This validates these SDK libraries and exercised paths; it is not a NativeAOT certification of the entire WinUI app or EF backend.

A separate loopback integration harness successfully made **31 read-only requests** against the actual backend, using existing database fixtures. It covered profiles, music, waveform (4,384 points), stems, lyrics/lines, media, library/playlists, following activities, features, stories and streamed search. No account, session or database data was created. The isolated server was stopped after verification.

## Measured allocation changes

These are managed bytes allocated per operation, **not process working set or total application RAM**.

| Scenario | Before | After |
|---|---:|---:|
| Complete-file download, 8 MiB, warm buffers | 8,396,352 B | 6,008 B |
| Small image, 2 KiB, warm buffers | 173,448 B | 14,384 B |
| Deserialize 411,072-byte JSON / 1,200 rows | 1,683,670 B | 448,112 B |
| Cached GET | 896 B | 464 B |

The complete-file measurements use the same source-linked before/after fixture and file bytes. Cold 8 MiB allocations were 16,807,456 B before and 155,200 B after; pool warming affects results. The progressive `DownloadAsync` path intentionally still allocates the chunk bytes required by its contract.

The transport benchmark used .NET 10.0.9 Release JIT, disabled tiering, reflection disabled, a synchronous in-memory HTTP handler, warmup, and 18 samples per scenario with reversed process order. Decoded content hashes matched. Cached GET median was 1.543 → 0.368 microseconds. The large-response median was **2.630 → 2.806 milliseconds (6.7% slower)** while allocating 73.4% less memory. Do not generalize these synthetic timings to real network latency, SQL execution time or total app memory. No backend-query speed claim is made by this SDK change.

Local evidence: `D:\MizonTaskDiagnostics\SdkOptimization` contains final native publish/run logs, integration logs, original source snapshots, and before/after benchmark fixtures/results. No tokens or JWT secrets are included in test output.

## Running the checks

```powershell
dotnet run --project Tools/SdkDiagnostics/SdkDiagnostics.csproj -c Release
dotnet publish Tools/SdkDiagnostics/SdkDiagnostics.csproj -c Release -r win-x64 -p:PublishAot=true
```

On the current laptop build artifacts, temporary files and downloaded test runtimes were placed on D because C was full. Native compilation requires the installed Visual C++ build tools. The diagnostics project is separate from the production package assemblies.

## Adding operations

Supply `RequestJsonTypeInfo`, `ResponseJsonTypeInfo` and, for GET, `RequestQueryString` from generated metadata/typed input. Streaming operations use item metadata rather than an envelope. The legacy fallback remains only for untrimmed JIT consumers with JSON reflection enabled; missing metadata is rejected in NativeAOT. One narrowly scoped IL2026 suppression documents this guarded compatibility path; all built-in operations and the native diagnostic runner bypass it.

NativeAOT library configuration follows [Microsoft's library guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/). Incremental multiple-value JSON uses the supported [System.Text.Json API](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.deserializeasyncenumerable?view=net-10.0).
