# 2026-11-10 repository synchronization

- Status: source and documentation complete; publication is represented by this branch's Git history.
- Owner: Codex, coordinated by the user.
- Repository: Shahesmaeili/Mizon.API.DotNet, branch main.
- Base commit: 5e7541aac20a64315e3985d166236068c243c624.
- Scope: publish all accumulated reviewed SDK source and establish shared AI instructions.

## Result

Matching typed operations/contracts cover registration, profiles, moods, following
activity, featured media, library/playlists, music play and likes. Transport,
source-generated serialization, streaming search/media, cache and SignalR changes
are included. The local folder/project names remain Mizon.API; origin now uses the
canonical renamed GitHub repository Mizon.API.DotNet.

Shared AI workflow, scoped commit/push requirement, project map and handoff are
included. No external credentials, generated outputs or diagnostic artifacts are added.

## Validation

- dotnet run --project Tools/SdkDiagnostics/SdkDiagnostics.csproj -c Release: passed.
- Transport, typed operation, media completion/cache/allocation and socket checks ran.
- This run used diagnostic fixtures; it did not submit a real account form or mutate
  the development database. Full application UI/AOT is not claimed by SDK checks.
- Local evidence: D:/MizonTaskDiagnostics/publish-sdk-diagnostics.log.

## Coordination

Matching Database, Server and Windows snapshots use this task ID. The database
source is now tracked separately at c31909d2baf3b741aed604d0e4ab31f42e1dcdb5.
The server task note records the final cross-repository publication set.
Use git log for this file to identify its containing commit.
