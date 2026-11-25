# Account presence in the .NET SDK

Owner: SDK workstream. Base: `9108396` on SDK `origin/main`. Dependency:
[server `fd64e0f`](https://github.com/Shahesmaeili/Mizon.Server.API/commit/fd64e0f0032964be74551626a79fcbe94b343a0a).

The SDK exposes `WatchAccountPresenceAsync` on an authenticated connected socket.
Register a new watch on reconnect. The server sends `AccountPresenceChanged` only
to watchers for the same account. The highest revision wins within a connection,
including when an event reaches the client before the initial watch reply. Reading
`AccountPresence` after disconnect, stop or an authentication change returns null.
The existing `SetWindowPresenceAsync` continues publishing and acknowledging the
Windows window’s own focus. Clients can fall back to that separate acknowledgment
when an older server does not have the watch method.

The generated JSON context and socket diagnostics cover typed delivery with
reflection disabled, an inactive observer, stale/foreign payloads, the initial
snapshot race, cancellation, reconnect, and sign-out. No real account is required.
Run the socket diagnostics with
`dotnet run --project Tools/SdkDiagnostics/SdkDiagnostics.csproj -c Release -- --presence-only`.
