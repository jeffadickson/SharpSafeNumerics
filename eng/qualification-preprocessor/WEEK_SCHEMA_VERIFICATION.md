# Week / IEEE soak schema verification — 2026-11-18

Requirement: SN-QUAL-COMPACT. `week-schema-source-hashes.json` identifies the
uncommitted sources tested against its recorded base revision. No Decimal shipping
source, test, active job or normal .NET output was modified by this work.

The new version1 Kind envelopes are normalized before legacy detection. IEEE
Inconclusive remains Incomplete; a week Running checkpoint remains Unknown.
Failure counters, fatal child exits and explicit failures dominate a contradictory
pass. An interruption recovery reason is incomplete evidence, not an invented
numerical failure. Source-refresh and preflight inconclusiveness prevent a pass.
Unknown explicit kinds cannot borrow a legacy Passed:true result.

Chunk identities are taken from each record, not the latest top-level revision.
Compact counts are per-revision record/status counts, never recomputed numerical
assertion/sample totals across revisions. Full details retain all chunks and
source-update records; compact output gives exact omissions and a raw file hash.
The existing streamed log reader and fresh-output protections remain in use.

## Executed checks

Existing installations were reused: rustc/cargo1.99.1, nextest0.9.152,
cargo-llvm-cov0.9.1 and cargo-mutants27.1.0. Cargo.lock and exact dependencies are
unchanged. All builds/reports/scratch use
`E:/Artifacts/SharpSafeNumerics/windows-x64/qualcompact-20261118`.

- TDD red: initial week tests6 failed/1 passed; count-contradiction1 failed;
  IEEE schema3 failed; unknown-kind1 failed; interrupted-recovery1 failed.
  These were missing/wrong behavior, not compiler errors.
- Final nextest: **63 passed, zero failed/skipped**. Includes directory discovery,
  unfinished reserved summaries, malformed/missing/null counters, unsupported
  versions/statuses, exact large integers, failure/inconclusive precedence,
  independent revisions, missing source scope and 1000-chunk compact omissions.
- Proptest: existing two128-case campaigns seed731925 and the new128-case
  campaign seed7542032. Its original minimized red case n=0/seed=0 remains in
  `tests/campaigns.proptest-regressions` and passes after implementation.
- `cargo fmt --check`, Clippy all-targets with `-D warnings`, and optimized native
  release build passed. exFAT incremental-cache hardlink fallbacks are filesystem
  copy warnings, not ignored test/compiler failures.
- LLVM source coverage: **808/868 lines (93.09%)**, 1484/1627 regions (91.21%).
  New campaigns module:261/281 lines (92.88%). Branch coverage is **unavailable**:
  this stable instrumentation report contains zero branch counters; region
  coverage is not substituted for branch coverage. Dependencies are excluded.
- Scoped mutation: six return-value mutants of unsigned_nonzero/integer_nonzero
  were all caught; clean unmutated baseline; exit0; one worker; ten minutes.
  This is six representative counter-validator mutants, not a whole-module score.
  It ran on the retained snapshot before the final preflight/interruption policy
  additions; validator bodies were unchanged, and those later additions have
  directed red/green regressions. Raw snapshot hashes are retained separately.
  No runaway workers or memory intervention were observed.

Raw artifacts reside under `week-schemas/`, including red logs,
`nextest-recovery.log`, `clippy-recovery.log`, `coverage-recovery.json`, release
logs and `mutation/mutants.out`. No tools were installed and no memory caps used.

## Actual producer smoke execution

The final native release executable processed two real emitted IEEE soak summaries:

- `ieee-soak/smoke-7542021-57ea35b5b1bd4990b6ccac55bed93209.json`:
  14 samples,70 passed checks,210 assertions,zero failed/inconclusive checks.
- `week-runner-tests/actual-child-smoke.json`:
  43 samples,215 passed checks,645 assertions,zero failed/inconclusive checks.

Both declared Passed and normalized Passed; exit0,zero diagnostics; source
references/hashes and independent runs were retained. Final receipt:
`week-schemas/actual-producer-smokes-final-verification.json`. Native invocation
took444.9868ms in that observation, including file I/O; no speedup is inferred.
Combined inputs5024 bytes produced6053 compact bytes: small reports incur metadata
overhead, so **no token/byte saving is claimed for these two small inputs**.

These are bounded producer/process integration checks, not a successful week-long
campaign or proof that the producer's numerical algorithms conform globally.
Week report admission is covered by real-contract fixtures; actual retained wrapper
report execution is recorded separately when its smoke artifact is supplied.
