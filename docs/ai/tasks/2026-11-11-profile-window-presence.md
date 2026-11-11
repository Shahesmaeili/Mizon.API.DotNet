# 2026-11-11 — Current main-window focus over the existing SignalR connection

- Owner: Codex (`music_home_contracts`); integration owner: root task.
- Repository/branch: Mizon.API.DotNet, `codex/profile-presence`.
- Base: `dd8860eacf5ed9a82c01918a948615e6a2f0de45`.
- Scope: MizonSocket presence partial/DTO/generated JSON and SDK diagnostics.
- Related: matching Mizon.Server.API hub and Windows header/taskbar/MCP state.

## Usage and lifecycle

`MizonSocket.SetWindowPresenceAsync(bool isActive, CancellationToken)` records
the latest desired focus before awaiting any existing socket operation. It never
starts another connection. While disconnected it records the intent and returns;
the existing connection owner continues to call StartAsync/StopAsync as before.

The hub receives `SetWindowPresence(isActive, revision)` and returns/emits
`ClientWindowPresenceResponse` (`ClientPublicId`, `ConnectionId`, `IsOnline`,
`Revision`). Only an acknowledgement matching the current authenticated identity,
connection, desired revision and desired focus can become `WindowPresence`.

`WindowPresence` is nullable: null means no current server confirmation, including
after intent changes and during disconnect/reconnect/stop. `WindowPresenceChanged`
also signals invalidation with an offline synthetic response; consumers should read
the nullable property to distinguish it from a confirmed offline state. Callbacks
run on the transport thread and must be marshaled to UI ownership by the app.

Reconnect restores the latest focus, including changes made while reconnecting.
Stop clears desired state as well as confirmation; changing account identity cannot
reuse an old connection's acknowledgement. Caller cancellation withdraws an active
intent with a newer offline revision unless a newer caller already superseded it.
Disposal cancels in-flight work and releases subscriptions with the existing socket.
No second audio/taskbar socket, global broadcast, timer or database operation exists.

## Verification

The full Release SDK diagnostic suite passes with JSON reflection disabled. New
loopback SignalR protocol checks exercise actual MizonSocket and generated metadata:

- Disconnected intent does not establish a connection or claim online.
- Delayed online event AND invocation completion cannot override newer inactivity.
- Wrong client, stale connection and stale revision responses are rejected.
- A held reconnect handshake permits focus changes; only the newest value restores.
- Cancellation withdraws current focus; pre-cancellation preserves existing state.
- Stop/new login, account switching, disposal with an in-flight presence request,
  existing music membership/playback, device registration and token refresh.

The diagnostic server uses synthetic identities and source-generated response JSON.
Server-repository tests separately exercise the real authenticated production hub
with its production JSON context and no DbContext. No real account/token/DB writes
or Windows UI automation are involved.

Final source passed both `dotnet run --project Tools/SdkDiagnostics/SdkDiagnostics.csproj -c Release`
and `dotnet publish Tools/SdkDiagnostics/SdkDiagnostics.csproj -c Release -r win-x64 -p:PublishAot=true`,
followed by the native executable. Native output reports `DynamicCode=False` and
`JsonReflection=False`; all transport/media/operation/socket checks pass, including
focus queued while StopAsync waits and disposal with an in-flight invocation.
App rendering and actual Windows
focus/taskbar behavior are owned by the Windows integration and are not established
by headless tests.

## Publication

Root coordinates the matching server, SDK and Windows commits and publishes after
combined verification. This branch must be integrated alongside the authenticated
hub contract; it does not create a parallel endpoint or change API package versions.
The matching server commit is `e51b5fe38f9b4d1282a429d4e9e4aec6308f7de5`.
