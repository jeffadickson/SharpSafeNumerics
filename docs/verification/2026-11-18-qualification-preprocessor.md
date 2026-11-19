# Native qualification evidence preprocessing

Requirement: SN-QUAL-COMPACT. Base revision:
`cbdfafee4931e182a89488552d97b18692bf7bcd`; tool source is the same commit as
this report. Windows x64, rustc/cargo 1.99.1, native release opt-level 3 / thin
LTO / portable target defaults. Rust remains optional for .NET builds.

Local artifacts: `E:\Artifacts\SharpSafeNumerics\windows-x64\qualcompact-20261118`.
Cargo.lock pins resolved dependencies. Primary release binaries used:
cargo-nextest 0.9.152, cargo-llvm-cov 0.9.1, cargo-mutants 27.1.0. Download SHA256:

| Archive | SHA256 |
|---|---|
| llvm-cov Windows x64 | 656590CFA13BB7478595BBFDD3BC6BC075A4F7F3F0DF5A439C1856CB03C5A4D2 |
| nextest Windows x64 | CC6BF4B6A7AC18E462CD6C9BAE67E26921B888249C2988C6ACABF3DF387ED9D6 |
| mutants Windows x64 | 2A2F00E47D4B458262A41501B0820AA26015FD35779903D2C8B30B2993F36791 |

The implementation used red/green regression tests. Independent simulated code
review reproduced false passes for huge exit codes and negative/fractional log
counts, output alias partial publication, missing source revision indications,
and directory selection overwhelming the byte budget. Fixes retain exact exit
codes, reject malformed declarations/counts, check Windows case aliases before
publication, mark unknown revisions, and select known report files. Full details
retain successful as well as failed check commands and log references. Compact
output records omitted successful-check details explicitly. Input hashes are
checked again after parsing to detect changing evidence.

## Executed checks

- `cargo nextest run --locked` and
  `cargo llvm-cov --locked --json --output-path .../coverage-48.json`:
  48 tests passed (2 library, 20 CLI, 26 normalization), zero failed/skipped.
  Seed environment 731932; two Proptest properties each use 128 cases and
  explicit ChaCha seed 731925 in source (the explicit seed takes precedence).
- Production Rust source line coverage: **517/567 = 91.18%**. Normalization
  library 232/243 = 95.47%; CLI 285/324 = 87.96%. Region coverage 957/1066 =
  89.77%. These figures exclude dependencies and tests. Uncovered paths include
  OS/I/O errors and concurrent input-change handling.
- Branch measurement was attempted with `--branch` and failed because stable
  rustc rejects the required nightly `-Z` option. Branch coverage is unmeasured;
  LLVM's zero branch count is not evidence of 100% coverage.
- StrictDoc 0.30.4 exported the actual requirements.sdoc successfully to the
  run-specific artifact directory. No numerical/formal models changed; this
  evidence-processing tool makes no new Dafny proof claim.
- `cargo fmt --check` and `cargo clippy --locked --all-targets -- -D warnings`
  passed. The PowerShell wrapper parsed and ran successfully against the real
  workstation directory, incrementally reusing the native release build.
- Mutation: `cargo mutants --file src/lib.rs --shard 1/4 --no-shuffle --jobs 2`
  examined 10 mutations: 7 caught, 3 missed, zero timeouts/unviable. Two misses
  exposed missing assertions for declared Failed status and unsupported schemas;
  the new tests close those gaps. Targeted replay against the final source with
  `--re 'Some\("Failed"|replace && with \|\|'` examined 7 mutations: **6 caught,
  1 compile-unviable, zero missed/timeouts**. The initial remaining missed mutation
  deletes the explicit Incomplete arm; the wildcard arm has identical behavior,
  so this is equivalent by source inspection. This is a bounded normalization
  campaign, not full-crate mutation coverage; CLI mutations remain unmeasured.

## Real report reduction

The compiled release executable processed the retained one-hour workstation
directory (summary, mutation, coverage and three terminal soak tails) together
with the versioned 75-minute numeric summary. Inputs selected by the tool totalled
**121,199 bytes**; the default compact report was **7,860 bytes**, a **93.51% byte
reduction**. The separate full-details output was 97,599 bytes. One wall-clock
measurement was 1,009 ms, including native process startup and repeated SHA256
reads. This is a single local observation, not a benchmark or token-count claim.
After the final Clippy-only source rewrite and two new tests, the rebuilt release
produced byte-identical output in 265 ms on a warm repeat. The retained
[compact report](2026-11-18-qualification-preprocessor-triage.json) is reviewable.

Verified production source SHA256:

- lib.rs: F888FC3468A13B44B388D191DA25E96216DBA8C9956B5CAF12C7295E1E53E233
- main.rs: 72DC7237A4030D56856E307893FE1B0E554A831079B5E3B0D7EF34CD56696131

All seven evidence records remained separate. The workstation verdict remained
Failed; its unsuccessful gate names/statuses remained visible even though issue
excerpts were omitted. The three integer tails and numeric summary retained
Passed terminal verdicts with unknown source revisions explicitly represented
by missing SourceRevision plus issue/omission counts. Coverage and mutation
records retained Unknown acceptance verdicts because no thresholds were inferred.
All 138 mutation findings remained in full details (139 issues including unknown
revision); compact output recorded their omission and retained status counts.

The original workstation source was
`b98767333406e39c66d9e9a9e07e7daf27e91cb2` from results commit
`a3dca0274e271fd0c0bebac0317a8c5517a49100`. Retained files were extracted using
PowerShell UTF8 output, so their hashes identify these retained copies rather
than asserting byte-identical remote blobs. No metrics were added across runs.

Coverage tooling builds instrumented native tests; release execution is measured
separately. exFAT does not support Cargo's incremental hardlinks, so Cargo copied
files and emitted a warning. No global toolchain or filesystem settings changed.

## Reproduction

Use [the native tool README](../../eng/qualification-preprocessor/README.md) and
[PowerShell workflow](../QUALIFICATION_PREPROCESSING.md). Raw reports remain local;
compact output retains raw path, byte count and SHA256. Reports from different
sources stay separate. Preprocessing exit zero does not assert qualification
success. Unknown/malformed evidence is never silently treated as passing.
