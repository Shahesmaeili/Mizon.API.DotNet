# SDK transport benchmarks

This harness calls the production `MizonApi`, its generated-JSON request pipeline,
its real memory-cache path, and the actual `GlobalSearchOperation` with mixed music
and client DTOs. A fixed in-memory HTTP handler prevents network or database work.
It does not change or time the real user's account, download cache or app session.

## Run

From the SDK repository root:

```powershell
dotnet publish Tools/SdkBenchmarks/SdkBenchmarks.csproj -c Release --no-self-contained -o artifacts/sdk-benchmarks
& ./artifacts/sdk-benchmarks/SdkBenchmarks.exe --verify
$env:DOTNET_TieredCompilation = '0'
try {
    & ./artifacts/sdk-benchmarks/SdkBenchmarks.exe ./artifacts/sdk-benchmark.json
} finally {
    Remove-Item Env:DOTNET_TieredCompilation
}
```

Preserve and restore an existing `DOTNET_TieredCompilation` value instead of removing
it if your shell already defines it. `--verify` checks fixtures without performing
timed measurements. The normal run writes one source-generated JSON report and
prints the same report to stdout. It accepts one optional output path.

## What is measured

| Scenario | Work per operation | Iterations per sample |
| --- | --- | ---: |
| `cached-get` | An already-cached response through `SendRequestAsync` | 50,000 |
| `small-json-get` | HTTP wrapper, generated JSON parse and disposal for one row | 5,000 |
| `large-json-1200-rows` | Same transport for 1,200 music-shaped rows | 100 |
| `ndjson-search-200-rows` | Full incremental enumeration of 200 mixed search rows | 150 |

Each scenario is warmed up, then produces nine timed samples. A sample covers a
batch, not a single stopwatch interval for each operation. The report retains all
samples and medians in microseconds/operation and allocated bytes/operation.
`GC.GetTotalAllocatedBytes` includes asynchronous continuations; allocation is not
retained heap size or total app RAM. Process peak working set is diagnostic context,
not an attribution of memory to an individual feature.

The harness verifies full fixture round-trips before timing, and checks each batch's
row checksum and HTTP call count. Payload SHA-256 hashes, runtime, architecture,
cache assembly version, GC mode and tiering environment are included in the report.
JSON reflection remains disabled. Request/response/cache ownership uses the same
production services as the app; no fake implementation is used as a baseline.

## Comparing runtime upgrades

Build and preserve a framework-dependent baseline **before** changing production
TFMs/packages, then build the candidate separately with the identical harness.
For the 2026-11-10 migration the baseline source was SDK commit `b547c08`, with this
harness targeting `net10.0`; the candidate targets `net11.0`. Build each repository
snapshot in an isolated worktree. Never restore two target variants into the same
`obj` directory concurrently.

The baseline used SDK `10.0.400-preview.0.26322.102`, runtime `10.0.9`, and Microsoft
SDK dependencies `10.0.14`. The candidate uses SDK `11.0.100`, runtime `11.0.0`, and
the corresponding Microsoft dependencies `11.0.0`. This is a **whole upgrade**
comparison, including compiler/package changes, not proof of a runtime-only effect.

Run the already-built executables in alternating order, at least two processes per
variant, with identical settings. Do not compile, run other benchmarks or automate
the UI concurrently. Compare fixture hashes and report the distribution, not only
the fastest sample. The dated cross-repository report is maintained in the server
repository's `docs/performance` directory. Do not reuse historical timings as new
measurements or extrapolate these in-memory numbers to SQL/network/UI latency.
