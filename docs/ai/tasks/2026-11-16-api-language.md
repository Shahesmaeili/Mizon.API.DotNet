# 2026-11-16 SDK application language

- Owner: Codex
- Branch: `codex/api-language`; base `d464657`
- Related: Mizon.Server.API and Mizon.Windows language handoffs
- Scope: SDK HTTP/SignalR transport and diagnostics

Pass a thread-safe callback returning the selected application language to
`services.AddMizonApi(() => currentLanguage)` or the `MizonApi` constructor.
The callback supplies a BCP 47 tag such as `fa-IR` or `en-US`. The SDK reads it
when creating each request, uses the same snapshot for the cache key and supports
GET, POST, NDJSON and SignalR negotiation/upgrades/reconnects on native clients.
Existing constructors and DI registration remain compatible. Storage/media
downloads have their own transport.

`dotnet run --project Tools/SdkDiagnostics -p:NuGetAudit=false` and the
`win-x64` NativeAOT publish/run of SDK diagnostics passed with JSON reflection
disabled, including language switching, client cache isolation, cancellation
and real loopback SignalR negotiation and reconnect. App preferences remain
owned by consumers. Other platform clients can send the same HTTP header.
