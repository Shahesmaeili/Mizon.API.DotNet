# Featured person catalog SDK

- Owner: Codex, branch `codex/featured-persons`.
- Base: `0181f14`.
- Owned paths: Persons SDK route/operation/contracts, PersonJsonContext and transport diagnostics.

The cancellable, authenticated GET `/Persons/FeaturedPersons?TypeCode=music`
supports independent `music`, `movies` and `series` lists. The response contains
ordered Person IDs, image/model references and shared follow state. The music
catalog includes recent tracks and listening aggregates. Client caching is
disabled so refreshes and follow readback can observe the server's current data.
The usual person follow operation remains the write API.

Verified route shape and nested JSON with reflection disabled in SDK diagnostics.
Windows consumes this operation for the spotlight and top-artists rail.
