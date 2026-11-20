# Track discussion SDK contracts

- Status: complete
- Owner: Codex
- Date: 2026-11-20
- Repository and branch: Mizon.API.DotNet, `codex/music-comment-discussion`
- Base commit: `35c9b29`
- Related repositories: Mizon.Database, Mizon.Server.API, Mizon.Windows
- Owned areas: typed music discussion operations and SDK serialization checks.

## Request and acceptance criteria

Support paged comments and replies with platform, timestamp, votes and
typed entity tags without changing existing server transport or forcing
callers to use the new read options.

## Implementation and decisions

The existing comments read accepts optional cursor, parent and sort fields,
plus a waveform-only filter. Create accepts the parent and tags. Dedicated
vote and tag lookup operations use the same transport and generated JSON.
Music video tags include the owning track public ID for opening playback.
Comment reads continue to bypass the SDK cache so writes can be read promptly.

## Validation

| Command / environment | Result | Limits |
| --- | --- | --- |
| Release SDK build and `SdkDiagnostics --operations-only` | Passed | Generated request/response metadata with JSON reflection disabled |
| Full SDK diagnostics | Passed on rerun | Initial run had an intermittent failure in the existing presence fixture |

## Handoff

Use a server with the matching comment discussion endpoints. Clients that
omit the pagination fields still request the original comment listing.

## Publication

Shared migration dependency: `05f3a3ebf084a1b848acd30589ef7ee2d63ff0db`. Target branch: `main`.
