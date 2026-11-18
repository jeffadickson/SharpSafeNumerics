# Native qualification preprocessor

User request2026-11-17: Rust or C++ preprocessing so reviewing soak evidence
does not require feeding large logs/reports to a language model. Select Rust:
rustc/cargo1.99.1 installed, maintained serde_json/clap/sha2 libraries available.
Use exact dependency versions/Cargo.lock, no third-party runtime changes to .NET.

Primary sources checked: crates.io/crates/serde_json (1.0.154), clap(4.6.8),
sha2(0.11.1), proptest(1.12.0). JSON/CLI libraries avoid handwritten parsers;
SHA256 identifies raw evidence. These libraries use MIT/Apache-2.0 licenses;
verify exact resolved metadata and retain Cargo.lock. C++ would add an unnecessary
toolchain choice on this machine. This is integration glue, not a new log engine.

## Deliverable

eng/qualification-preprocessor: native release executable qualcompact, Rust2024,
opt-level3, portable CPU settings, incremental Cargo build. CLI accepts explicit
input files or a published qualification-results directory, optional output file
and full-details file. Directory discovery is top-level only; never recursively
scan build/runtime/source trees by default. Default compact report budget8192
UTF8 bytes; cap displayed details with explicit omitted counts/raw references.
If identity/verdict information alone cannot fit, reject rather than silently
dropping a run/failure. Fresh output files only; source files remain unchanged.

Supported evidence: workstation summary.json Checks/QualificationStatus and
legacy reports without that field; numeric soak Passed/counts/seed JSON; published
mutation-summary.json or full Stryker reports; ReportGenerator coverage summaries;
existing integer-soak stdout tail lines. UTF8 BOM accepted. Preserve exact integer
values, null/missing values and deterministic order. Stream log input; preserve
bounded excerpts and source SHA256, never treat an arbitrary error string as an
authoritative qualification failure (negative probes legitimately emit errors).

## Safety/evidence semantics

- Successful preprocessing is not successful qualification. Never infer Passed
  from upload success, an empty object, missing checks or unreadable evidence.
- Workstation Failed/TimedOut/Cancelled/MissingPrerequisite, fatal errors and
  source changes dominate a contradictory Passed field. Budget skips imply
  Incomplete. ProbeRejected is an expected check status, not ordinary failure.
- Keep independent runs/source revisions separate; no summed metrics across
  snapshots and no combined mutation scores. Mark unknown scope explicitly.
- Retain all normalized failures in optional full details. Compact output states
  counts/omissions and raw file/hash references. Missing/corrupt companions are
  visible diagnostics, not silently ignored evidence.
- No qualification job is started or stopped; no input is changed, and no data is
  uploaded or published by this tool. Output creation rejects input/output aliases.

## Verification

TDD: failing tests before core normalization. Directed actual-schema fixtures,
malformed/truncated/contradictory inputs, missing values, exact large counts,
negative probes, BOM, log parsing, byte-budget/omission behavior and output
preservation. Proptest randomized inputs with replay seeds. Prefer cargo-nextest,
cargo-llvm-cov and cargo-mutants when compatible, recording actual scope and
limitations. Run the compiled release executable against real one-hour workstation
evidence and current75-minute numeric evidence, measuring input/output byte ratios
and elapsed time (not invented token savings). Source JSON/log checksums retained.

Integrate a PowerShell build/run wrapper and dedicated Windows/Linux Rust CI.
Keep Rust optional for ordinary .NET builds. Do not change the running pinned
workstation job; returned legacy reports can be preprocessed locally afterward.
Update StrictDoc SN-QUAL-COMPACT, docs, evidence, commit/push and observe CI.
