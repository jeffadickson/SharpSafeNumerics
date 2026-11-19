# Workstation 18-hour-budget integer qualification — 2026-11-18

Results: [qualification branch](https://github.com/jeffadickson/SharpSafeNumerics/tree/qualification/issue-1/20261117T205920Z-mt-209637-b4aeaf66/qualification-results/workstation/20261117T205920Z-mt-209637-b4aeaf66).
Results commit: `9d0fe72717a5b8cd1b99ab4ae3f55fa927e23a77`, whose parent is the
verified source `2e242881cb9a5075cb4fceb9f4df7cb7585efc3a`. Issue1 tracking
remains intact. This is historical integer qualification; it does not qualify
later fixed-point, finite decimal or IEEE decimal additions.

Run ID:20261117T205920Z-mt-209637-b4aeaf66; machineMT-209637; requested18hours.
Authoritative workstation summary declaresPassed with37 passed checks and two
rejected SDK candidate probes. A subsequent SDK candidate passed; the rejected
probes are retained as provisioning outcomes, not hidden or counted as numerical
failures. SourceChanges is empty. Start2026-11-17T20:59:20.3209856Z,
finish2026-11-18T14:53:55.3978455Z. No source checkout or ongoing run was altered.

## Native preprocessing before inspection

Used the previously committed Rust `qualcompact` release executable first, SHA256
`555ce837b513fa7503784417037d915c98894691f4b27903117ed3e0e38d82b1`.
The verified results commit was archived to a unique E: directory; the downloaded
runner script was treated as data, not executed. The nonrecursive processor was
then given the actual run leaf directory. An initial archive-root invocation
returnedNoQualifyingArtifact/exit2; it was not a qualification verdict.

Six recognized inputs totaled86,618 bytes and produced an8,069-byte compact JSON,
with full details retained separately. No token-saving estimate is inferred.
A focused summary-only pass retained both rejected SDK probes. Standalone coverage
and mutation summaries haveUnknown source identity in the compact schema; the
workstation summary, source-revision log and results-commit parent provide the
run-level provenance. They are not numerical evidence for the current source.

Artifacts remain outside Git under
`E:/Artifacts/SharpSafeNumerics/windows-x64/ieee-decimal-20261118/remote-soak-20261117T205920Z-mt-209637-b4aeaf66`.

## Executed integer soak outcomes

All three completion records passed with no reported error references. One worker
per mode, native integer width64, shared seed731925/00000002OIo1. Modes ran in
parallel; their durations must not be added into a wall-clock run length.

| Mode / detected width | Child-reported seconds | Samples | Type pairs | Arithmetic/policy outcomes | Batch lanes |
|---|---:|---:|---:|---:|---:|
| Auto /256 |64,365.046|325,881,737|3,910,580,844|66,479,874,348|41,488,842,023|
| SIMD128 /128 |64,365.103|325,487,053|3,905,844,636|66,399,358,812|41,439,811,909|
| Scalar /0 |64,365.104|320,354,853|3,844,258,236|65,352,390,012|40,786,807,204|
| Total executed |—|971,723,643|11,660,683,716|198,231,623,172|123,715,461,136|

Shared seeds and overlapping mode campaigns mean these are executed counts, not
unique disjoint inputs. Child elapsed values are retained verbatim; parent check
ElapsedSeconds values are approximately64,351s and use a different timing record.
No exact agreement between these clocks or target WCET is claimed. Per-mode
live-heap snapshots and GC counts are not a proof of leak freedom or peak memory.

The retained mutation summary lists historical analyzer371killed/27survived/
4uncovered; Core158killed/2timeout/8survived/2uncovered; generator331killed/
32survived/3uncovered, with compile errors and ignores separately retained. Raw
full mutation artifacts remain on the workstation; this bounded upload cannot
replace them. Coverage combines the earlier Core/analyzer/generator scope and
must not be compared directly with current Decimal-only coverage.

## Reproduction outline

Verify the checkout and artifact parent directories before use. Fetch the exact
results ref without checking it out over working source, archive only its run
folder, and expand into a fresh snapshot directory. Run:

```text
qualcompact.exe <snapshot>/qualification-results/workstation/20261117T205920Z-mt-209637-b4aeaf66 --output <run>/compact.json --details <run>/details.json
```

Processor exit0 means supported artifacts were processed; the individual run
verdicts and authoritative workstation status determine qualification. Do not
merge the results branch as production source or treat this old run as evidence
for the new IEEE operations.
