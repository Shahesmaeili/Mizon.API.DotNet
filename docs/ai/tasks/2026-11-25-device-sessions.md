# 2026-11-25 — Device session SDK contract

- Status: implementation for Windows and other .NET consumers.
- Owner: Codex Windows/server. Branch: main.
- Owned scope: typed client session operations, generated JSON, revocation notifications and SDK diagnostics.
- Related repositories: Mizon.Server.API and Mizon.Windows.

The SDK mirrors the private session routes and disables caching for them.
Subscribe to MizonSocket.SessionsChanged to refresh session state and
NewSessionUnconfirmed to present a recognition prompt. MizonApi.SessionRevoked
and MizonSocket.SessionRevoked report the affected sign-in. Call
MizonApi.IsCurrentSession after dispatching a notice to the UI before clearing
authentication. An ordinary 401 does not raise revocation.

The operation diagnostics validate the source-generated wire contract without
JSON reflection, uncached reads, and the effect of stale and current 401 responses.
The standalone SignalR presence suite remains the connection-lifecycle regression check.
