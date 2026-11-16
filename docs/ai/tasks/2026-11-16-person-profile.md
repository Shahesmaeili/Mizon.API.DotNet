# Artist profile SDK

- Owner: Codex
- Branch: `codex/person-profile`, base `28516a1`
- Owns: Persons operation/request/response types, generated JSON and SDK checks

The authenticated operations consume `/Persons/PersonProfile`,
`/Persons/PersonMusics` and `/Persons/PersonFollow`. A track page accepts the
optional previous music public ID. The follow request explicitly passes
`Following=true/false`; a missing value differs from false. The profile follows
the existing media IDs and forwards `Accept-Language` via the shared transport.
These operations bypass client caching so follow state can be read immediately.
An optional `ModelYawInDegrees` carries the model's resting pose. An older
response omitting it leaves the field null.

SDK checks pass with JSON reflection disabled, including headers, payload and
nested track metadata, using managed execution and a win-x64 NativeAOT binary.
The corresponding server operations and Windows page are in sibling repositories.
