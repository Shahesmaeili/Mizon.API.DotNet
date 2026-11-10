# SDK handoff

Snapshot reviewed on 2026-11-10. This is shared development context, not a lock or proof that another assistant has read it. Use [COLLABORATION.md](COLLABORATION.md) for ownership, branches, integration and delivery.

## Current implementation

- Two .NET 10 libraries: `Mizon.API` is transport/session/cache/SignalR; `Mizon.API.Client` is typed operations, DTOs, generated metadata and media download/cache.
- The working tree includes typed profile, registration, mood, following-activity, music-feature/library/playlist-detail and playback-related operations in addition to the existing catalog, story and search operations. Their backend implementations live in the server repository.
- Search uses incremental JSON streaming through `SendStreamRequestAsync`. Media has progressive and complete-file download entry points; consumers should choose the appropriate existing method.
- Library projects declare AOT compatibility. Per-domain serialization contexts and `Tools/SdkDiagnostics` support reflection-disabled and native checks.
- MCP remains hosted by the Windows app/companion. Its implemented domains are authentication, appearance, declared navigation and taskbar pinning; this is not blanket exposure of all music or profile data.
- Public `mizon://welcome` navigation was removed in the Windows repository. Welcome remains an internal session-validation page and has dedicated authentication diagnostics there. The SDK has no reason to invent that deep link.

## Delivery policy

The user requests a precise commit and push after every completed implementation task. Future assistants must follow this from [AGENTS.md](../../AGENTS.md), stage only their scoped changes by default, and preserve other work. The current broader publication task is handled by the coordinating agent across repositories; this handoff does not assert a commit SHA or successful push before it is verified.

Use separate worktrees for simultaneous Codex/Claude/other work. Coordinate changes to `Endpoints.cs`, shared DTOs, serialization contexts, `MizonApi`, and `MizonSocket` because they can affect multiple clients. Record corresponding server/Windows branches or commits when a feature spans repositories.

## Verification evidence and limits

`PERFORMANCE.md` records earlier SDK verification dated 2026-11-08, including native diagnostics and benchmark methodology. It must be read as historical evidence with its stated limits; this documentation update did **not** rerun builds, tests, benchmarks, real API calls or database operations.

The executable diagnostic suite and commands are in [PROJECT.md](PROJECT.md). The coordinating publication task should record its own build/test results separately. Do not infer that every current uncommitted change or every other platform client is verified by the earlier performance report.

## Next assistant checklist

1. Read `AGENTS.md`, the collaboration guide, this handoff and the affected source; check the actual branch/worktree status and incoming changes.
2. Identify whether the task changes wire contracts, caching, streaming semantics, ownership or observable MCP behavior.
3. Coordinate the matching server and Windows changes before publishing a breaking contract.
4. Run the relevant diagnostics with synthetic data and propagate cancellation/disposal correctly.
5. Update this handoff or a task note with scope, files, dependencies, exact checks, limitations and unfinished work; commit and push the task's changes and verify the remote ref.

Do not put credentials, runtime tokens, local approval stores, personal query traces or build outputs into handoff notes. Git synchronization and explicit file ownership provide coordination; Markdown alone does not make separate AI sessions aware of each other in real time.
