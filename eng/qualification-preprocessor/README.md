# qualcompact

Read-only native preprocessing of qualification evidence. This tool does not run
qualification, upload results, or decide that an unknown scope is qualified.

```
qualcompact <files-or-directories...> [--output fresh.json] [--details fresh.json]
            [--max-bytes 8192] [--max-findings 12]
```

Directories discover known published evidence names at the top level: summary.json, mutation-summary.json, mutation-report.json, coverage-summary.json, numeric soak/summary JSON and the three soak mode stdout tails. Nested
negative controls and build trees are never scanned. Explicit files are also
accepted. Unsupported or malformed artifacts produce visible diagnostics and
exit 2; valid artifacts are retained. Other build logs and metadata companions are skipped during directory discovery; explicit unsupported files produce diagnostics.
Exit 0 means preprocessing succeeded, even when qualification is Failed.
The compact JSON goes to stdout unless `--output` is supplied. Output parents
must exist; existing files, source-file destinations, and output/detail aliases
are rejected. Full details are published even if the compact byte budget fails.
Inputs are never changed. Concurrent writers must use separate fresh paths.

## Schema version 1

The root object has `schemaVersion`, `runs`, `diagnostics`, `diagnosticCount`, and
`omittedDiagnostics`. Each independent run has `kind`, `source` (`path`, raw
`sha256`, raw `bytes`), `identity`, `verdict`, `metrics`, `issues`, `issueCount`,
and `omittedIssues`. Missing values remain missing or null, never invented zeros.
Numbers use serde_json arbitrary precision, preserving integers above u64.

Workstation fatal/source-change evidence and failed gates dominate a declared
Passed. Budget skips and unknown/missing checks yield Incomplete; ProbeRejected
is expected, including its nonzero process exit. Legacy reports derive status
from a nonempty check list. `checkStatuses` preserves non-Passed check names/status/exit codes in compact output (`omittedPassedCheckStatuses` counts omitted successful gates); full details retain all check statuses
when issue excerpts must be omitted. Numeric reports retain explicit Passed,
failure, counts, seeds, runtime, GC and allocation metrics. A missing revision
creates an UnknownSourceRevision issue rather than borrowing a neighboring run.

Mutation summary entries retain separate report totals/status counts; survivors
are issues. Full Stryker JSON groups status counts within that report only.
Mutation and coverage metrics have Unknown qualification verdicts because no
acceptance threshold is assumed. Coverage retains summary and assembly scope;
class breakdowns remain in the raw hashed report. No cross-run mutation score or
coverage aggregation is calculated.

Integer soak logs are streamed with a 64 KiB line bound and at most eight start
and completion records. Counts explicitly report omissions. Terminal records
require timestamp shape, elapsed time and all expected integer counters. Invalid,
multiple, or out-of-order records cannot yield Passed. Error lines are bounded
line references, never authoritative failure classifications. Raw hashes identify
all bytes, including omitted lines; no text interpretation by a language model
is needed. Oversized lines and invalid terminal records are visible diagnostics.

Compact output omits issue excerpts before refusing an impossible byte budget;
run identities, verdicts, metrics, counts and raw references are never removed.
`--details` preserves every normalized authoritative issue and full original `checkDetails`. Compact reports replace checkDetails with `omittedCheckDetails` counts. Text error references
are intentionally bounded with explicit counts. Inputs with exceptionally large
metric/identity skeletons require a larger budget, rather than silent data loss.

## Build and verification

Rust 2024, rustc/cargo 1.99.1 on the original workstation. Dependencies are exact
pins in Cargo.toml and transitive resolutions in Cargo.lock. `cargo build
--release --locked` enables opt-level 3, thin LTO and portable CPU defaults; no
machine-specific ISA flags are added. Cargo incrementally reuses unchanged
outputs. On Windows exFAT artifact storage Rust falls back from hardlinks to
copies; this warning does not indicate test failure.

PowerShell example (run from a verified checkout):

```powershell
$repo = (Get-Location).Path
if (-not (Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing checkout' }
$manifest = Join-Path $repo 'eng\qualification-preprocessor\Cargo.toml'
$art = if ($env:CODEX_ARTIFACT_ROOT) { $env:CODEX_ARTIFACT_ROOT } else { 'E:\Artifacts' }
if (-not (Test-Path -LiteralPath $art -PathType Container)) { throw 'Missing artifact root' }
$target = Join-Path $art 'SharpSafeNumerics\windows-x64\qualcompact-local\target'
if (-not (Test-Path -LiteralPath $target -PathType Container)) {
    New-Item -ItemType Directory -Path $target -Force | Out-Null
}
cargo test --locked --manifest-path $manifest --target-dir $target
cargo build --release --locked --manifest-path $manifest --target-dir $target
```

Tests cover contradiction precedence, legacy reports, negative probes, absent
values, exact huge counts, malformed/BOM JSON, stream logs, byte budgets, fresh
outputs, aliases, deterministic output, full issue retention and nonrecursive
discovery. Proptest campaigns use 128 cases and reproducible seed 731925.
Integration tests generate their temporary data under CARGO_TARGET_DIR and remove
only their own unique directory. Large fixtures are generated rather than stored.
Coverage and mutation tools are separate checks; passing tests alone is not a
coverage or mutation claim.

Primary dependency sources: [serde_json](https://crates.io/crates/serde_json),
[serde](https://crates.io/crates/serde), [clap](https://crates.io/crates/clap),
[sha2](https://crates.io/crates/sha2), [proptest](https://crates.io/crates/proptest).
Direct packages declare MIT OR Apache-2.0. Preserve Cargo.lock and dependency
license metadata when distributing binaries; transitive packages have their own
license declarations. No third-party source or asset files are vendored here.



