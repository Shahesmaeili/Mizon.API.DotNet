# Mizon .NET API and client SDK

Reviewed from source on 2026-11-10. Read [AGENTS.md](../../AGENTS.md), [COLLABORATION.md](COLLABORATION.md) and [HANDOFF.md](HANDOFF.md) before changing code.

## Repository boundary

The GitHub repository is [Shahesmaeili/Mizon.API.DotNet](https://github.com/Shahesmaeili/Mizon.API.DotNet). The checkout directory, project names, namespaces and NuGet IDs remain `Mizon.API` and `Mizon.API.Client`; a repository rename does not rename the API packages.

| Location | Responsibility |
| --- | --- |
| `Mizon.API/` | Shared HTTP request/response envelopes, transport, session/cache ownership and SignalR client |
| `Mizon.API.Client/` | Typed endpoint operations, requests/responses, JSON contexts and media downloader |
| `Mizon.API.Client/Endpoints.cs` | Central service endpoint definitions; inspect before running integration calls |
| `Mizon.API.Client/Serialization/` | Per-domain `System.Text.Json` source-generation contexts |
| `Tools/SdkDiagnostics/` | Executable transport, operation, media and loopback socket checks |
| `Tools/SdkBenchmarks/` | Reproducible real-transport/cache/NDJSON benchmarks with synthetic payloads |
| `PERFORMANCE.md` | Dated optimization record, benchmark methodology, limitations and previous verification |
| `Mizon.API.sln` | The two production library projects; diagnostics are run separately |

Both production projects target `net11.0`, declare `IsAotCompatible`, and currently declare package version `1.1.6`. Do not automatically increment or publish NuGet packages merely because a source commit is requested. App-specific UI and EF/database behavior do not belong in these libraries. `global.json` selects .NET SDK 11.0.100 with patch roll-forward; SignalR.Client, Caching.Memory and Extensions.Http use 11.0.0.

## Related repositories

- [Mizon.Server.API](https://github.com/Shahesmaeili/Mizon.Server.API): routes, middleware, operation execution and business operations. Server request/response models live under `Mizon.Server.Logic`; coordinate wire-compatible changes with this SDK.
- [Mizon.Windows](https://github.com/Shahesmaeili/Mizon.Windows): WinUI app, Windows Search provider, taskbar companions and MCP adapter. Its project references this SDK source in the current development layout.
- `Mizon.Database`: EF model, configuration and migrations. Changes to persistent behavior go through that project and server operations, not direct SQL or a second SDK-side business implementation.
- Other platform clients consume the same backend contract. A successful .NET build does not prove a Swift/Kotlin/web client remains compatible with a breaking wire change.

On the current laptop the SDK is at `D:/source/repos/Mizon.API`, the server is at `C:/Users/Ali/source/repos/Mizon.Server.API`, and the Windows repository is at `D:/source/repos/Mizon.Windows/Mizon.Windows`. Resolve actual checkouts on another machine instead of creating duplicate projects at those absolute paths.

## HTTP and typed operation flow

`MizonApi.SendRequestAsync` receives a `MizonApiRequest<TRequest,TResponse>` and returns a `BaseApiResponse<TResponse>`. Feature-specific classes under `Operations/` select the endpoint/method, authorization, caching, timeout and generated metadata. Keep each request/response in the existing domain folder and preserve nullable properties and wire naming.

When adding or changing an operation:

1. Coordinate the server operation, endpoint and DTOs. Preserve separate data properties such as names rather than combining them into presentation strings in transport code.
2. Add/update its endpoint entry and typed request/response models.
3. Supply `RequestJsonTypeInfo` and `ResponseJsonTypeInfo` from the appropriate domain context. Include the envelope and nested DTO graph required by the operation.
4. For GET requests, build `RequestQueryString` explicitly and escape text with `Uri.EscapeDataString`. Do not fall back to reflection to enumerate arbitrary request properties.
5. Choose authorization and client cache policy explicitly. Mutation operations, including logout, must not be suppressed by response caching. Nonpositive TTLs must not become cache entries.
6. Exercise both success and failure/cancellation behavior in the diagnostic runner. Keep source-generated JSON enabled in those checks.

Examples worth reading before editing are `Operations/Musics/MusicPlaylistCreateOperation.cs`, `Operations/Search/GlobalSearchOperation.cs`, and `Serialization/MusicLibraryJsonContext.cs`.

## Streamed search

`GlobalSearchOperation` derives from `MizonApiStreamRequest<GlobalSearchRequest,GlobalSearchItemResponse>`. `MizonApi.SendStreamRequestAsync` reads individual JSON values from the HTTP response using `DeserializeAsyncEnumerable` with `topLevelValues: true`; it is not a buffered array request.

- Preserve item-level generated metadata and the existing NDJSON wire contract.
- Propagate cancellation through the request and enumeration; stopping enumeration early must release the HTTP response/stream.
- Do not materialize all results with `ToList` before exposing the stream to consumers.
- Search items carry `ItemType`, public IDs and separate profile/music fields. UI layout, debounce and navigation belong to clients; search querying belongs to backend operations.
- Ordering is arrival-driven for the combined search. Do not silently reintroduce a client-first or music-first sequential pass.

## Media, cache and resource ownership

`Operations/Media/MediaDownloader.cs` owns the existing cached download behavior:

- `DownloadAsync` exposes progressive chunks and independently owned chunk bytes for streaming/progress consumers.
- `DownloadToCacheAsync` returns a `ValueTask<string>` only after the full file is available. Prefer it for image bindings that do not need chunk-level data.
- Preserve the established final/`.part` path layout, complete-file validation, range/resume handling and concurrent-writer protection. A `.part` file is not a completed cache hit.
- `DownloadSession` owns cancellation/pause and its cache lease; dispose it when the consumer is finished. Do not add a second unrelated cache for another UI surface.

`MizonApi` has explicit ownership rules for externally provided `HttpClient` and `IMemoryCache`. Its DI registration uses a named HTTP client and a singleton API instance to retain the current session. Do not accidentally replace that registration with an unconfigured client or create a singleton-per-page session.

Cached response DTO content is shared within its cache entry and should be treated as read-only. Different sessions must remain isolated, and a late login response must not restore a token after logout. Never include tokens or credentials in diagnostics or committed fixtures.

`MizonSocket` exposes connection lifecycle, entity membership and music-listener presence. Keep its callbacks, JSON metadata, reconnect membership and disposal consistent with the server hub. Adding a value to a hub message may also require explicit source-generated metadata for the runtime argument type.

## Verification commands

Run from this repository root, in the task's own worktree:

```powershell
dotnet build Mizon.API.sln -c Release
dotnet run --project Tools/SdkDiagnostics/SdkDiagnostics.csproj -c Release
dotnet publish Tools/SdkDiagnostics/SdkDiagnostics.csproj -c Release -r win-x64 -p:PublishAot=true -o artifacts/sdk-aot
& ./artifacts/sdk-aot/SdkDiagnostics.exe
```

The diagnostics exercise fake HTTP handlers, temporary media and a loopback SignalR server. They do not require a real user account or database writes. Inspect a harness before running it if its fixtures have changed. Native publishing requires the matching .NET SDK/runtime packs and Windows native C++ build tools. Do not run parallel builds against the same `obj`/`bin` directories from multiple assistants.

The historical solution lists Any CPU configurations, while the projects also declare x64. Keep a consuming solution and its project references aligned; do not override shared output directories to hide an architecture mismatch.

An AOT-compatible library and a passing native diagnostics executable cover exercised SDK paths, not every possible DTO, the entire WinUI application, or EF query compilation. Re-run targeted checks after a relevant change and report exactly what ran. Existing performance numbers are dated evidence, not a promise about SQL latency, server load or total app RAM.

## MCP contract coordination

This repository does not host the app's MCP server. A new observable feature still needs its Windows MCP contract, independent permissions and meaningful integration checks using the same business behavior. Current domains are authentication, appearance, declared navigation and taskbar pinning. Navigation permission does not imply music/profile read/write permission.

Use the Windows repository's `Mcp/DEVELOPMENT.md` and existing adapters. Do not add arbitrary HTTP invocation, SQL, reflection dispatch, shell execution or a second public backend just to make testing easier. Initial consent and revocation belong to the user in Mizon. Synthetic permission tests cannot approve a real connected assistant.
