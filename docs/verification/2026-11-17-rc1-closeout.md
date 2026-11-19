# RC1 closeout campaign

Base source: 7c13092c6cfbaf71e59da3915cea12ebcc417c13 plus the recorded closeout
changes. Bulk artifacts: E:\Artifacts\SharpSafeNumerics\windows-x64\rc1-closeout-20261117T201811Z.
Exact component/test SHA256 values are retained in source-hashes.json.
This report records an active qualification pass; pending results are not successes.

## Workstation review

See [independent workstation review](2026-11-17-workstation-review.md).
All three 3,017-second soaks passed: 8,733,729,192 arithmetic/policy outcomes and
5,450,598,916 batch lanes on source b987673. Both compiler-extension mutation
gates failed in that older snapshot. The report is not evidence for newer alias
corrections. The original results branch is preserved; no older source was merged.

## Analyzer survivor hardening

117 Release analyzer tests passed, including newly added small constant casts,
unchecked outer blocks with independent lambda/local-function arithmetic,
joined interpolated-string handler content and nonempty allocation explanations.
No analyzer implementation changed. Coverlet measured 367/375 lines (97.87%) / 486/564 branches (86.17%)
branches in this integrated Release analyzer scope, with referenced-assembly
incidental coverage kept separate. Source hashes and the earlier JSON mutation
identities preserve what motivated these behavioral checks.

Current whole Standard generator/analyzer campaigns use Stryker 5.1.0, Release,
net11.0, four sessions each, the existing 89/117 test suites, and explicit
high/low/break thresholds 95/90/90. No source or mutator exclusions were added.
Logs and reports use separate generator/analyzer subdirectories. Final scores
and mutant dispositions will be appended after the campaigns finish.

## Package archive reproducibility

Reuse decision: use NuGet's supported DeterministicTimestamp property; do not
rewrite ZIP archives or add a packaging framework. Primary references:
[NuGet deterministic packaging](https://learn.microsoft.com/en-us/nuget/create-packages/deterministic-packages)
and [.NET 11 RC1 NuGet notes](https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/rc1/nuget.md).
Local SDK 11.0.100 NuGet.Build.Tasks.Pack.targets line 228 defaults this property
to false. A real two-pass regression failed because analyzer archive bytes differed.
Setting true alone also failed on this SDK; ZIP-entry timestamps differed between
pack invocations despite matching entry sizes. A fixed supported Unix timestamp,
315532800 (1980-01-01 UTC, ZIP epoch), produced six identical complete archives.
No post-pack transformation was used. Caller-supplied timestamps can override
this default; repository/source revision metadata remains intact.

The fixed-timestamp comparison used --no-build and matched whole-archive SHA256
values for four nupkg and two snupkg outputs. It qualifies repacking existing
compiled inputs, not independent rebuilds across paths/platforms. The fresh
package-only consumer passed generated-domain/union checks and required packaged
SSN1004 rejection. The reusable script rejects existing output directories to
avoid stale same-version packages. Core CI additionally rebuilds between passes;
its result will be recorded once observed.

No RAM caps or free-RAM gates were used. C/E free space was recorded at startup;
large logs, mutation reports and package passes are in the unique E run directory.
Mutation-worker activity was inspected for runaway behavior; none required
intervention during the recorded portion of this run.

## Full Standard results on the closeout implementation

The recorded component/test source hashes matched the blobs committed in dccc826.
Both complete Standard campaigns passed the explicit 90% break threshold:

| Component | Candidates | Assertion kills | Timeouts | Survived | Uncovered | Compile error | Ignored | Stryker score |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Generator | 439 | 331 | 1 | 31 | 3 | 59 | 14 | 90.71% |
| Analyzer | 522 | 368 | 3 | 27 | 4 | 86 | 34 | 92.29% |

Elapsed times were 13m59.860s / 12m55.621s; both exit codes were zero.
These scores include timeouts as detected under Stryker semantics. Assertion
kills alone account for 331/366 (90.44%) and 368/402 (91.54%) of their scored
populations, so passing does not depend on counting those timeouts as kills.
No full-project source/mutator exclusions were added, and no gate was lowered.
Compiler implementation files were unchanged by this hardening pass.

The generator timeout was candidate 391, an identity-cast text-rendering branch;
it remains a timeout observation, not an assertion kill or proven equivalence.
Remaining generator candidates largely remove redundant partial modifiers,
identity casts/numeric suffixes or guards also enforced elsewhere. Analyzer
variants retain guarded symbol/boxing/traversal paths and several unmeasured
metadata/formatting paths. These are documented limitations, not silently closed
universal-conformance requirements.

A single-session no-bail replay of the three analyzer timeout spans scored seven
candidates: six assertion kills, one survivor, no timeouts/uncovered (85.71%, exit
2 at the unchanged 90% gate). Original timeout candidates 377 and 445 became
assertion kills. Candidate 31 survived: weakening the enum-type guard could
misidentify byte-backed values equal to Throw. Three negative regressions were
then added for an unrelated byte enum and ordinary byte constants. All 18
Throw-policy theory cases passed. The targeted guard replay is recorded below;
it is separate from, and not merged into, the full 117-test campaign above.

## Observed CI and packaging closure

Core CI 44169889729 passed on dccc826, including 381 tests (175/89/117), property
suites, 2,424 HTML/package fixtures, examples, native AOT, package consumers,
StrictDoc and Dafny's 12 verified obligations. Its new reproducibility step
reported all six archives byte-identical with RebuildBetweenPasses=True.
This closes the prior same-checkout archive-timestamp defect. Independent
cross-path/platform rebuild identity and public API baselines remain open.

The current HTML guide was updated with the passing whole-project scores and
rechecked in Chromium at 1440/390/320px: 2,424 fixtures agree, zero page errors
or external requests, V8 executed-character coverage 12,208/12,562 (97.18%).
That metric is not .NET line/branch coverage. The guide's fixture source revision
remains the earlier unchanged implementation snapshot, not a new claim that the
browser executes the package.

## Targeted policy guard closure and final regression

The single-session, no-bail replay of candidate 31's guard with the expanded
Throw-policy cases scored four candidates: four assertion kills, zero survivors,
timeouts or uncovered candidates; 100%, exit 0, elapsed 1m33.935s. This closes the
observed enum/byte identity test gap without changing analyzer implementation.
The narrower result is not added to the full 117-test score. Final source/test
hashes are retained in post-replay-hashes.json alongside the earlier full-run set.

A fresh relocated Release build and subsequent rebuild both succeeded. All 384
regression cases then passed (175 Core / 89 generator / 120 analyzer), zero
failures/skips; CsCheck and Hedgehog suites also passed with the existing seeds.
Final Coverlet scopes: Core 170/170 lines and 133/138 branches; generator 541/543
lines and 256/269 branches; analyzer 367/375 lines and 486/564 branches. Referenced
assembly incidental coverage is not combined with those component measurements.

## Eighteen-hour workstation preparation

The runner now includes package reproducibility before starting mutation/soak
workers, uses the relocated build artifacts for that step, and uploads its six
SHA256 values as bounded evidence. This exact relocated-output/rebuild path
passed locally with all six archives byte-identical. Fourteen Pester runner tests
passed, including immutable-reference eighteen-hour planning and qualification
verdicts for failure, skipped budget, expected probe rejection and source changes.
A manual source-integrity verdict mutation was detected against the expected
Failed contract in an isolated function evaluation; source files were preserved.

Reports now include QualificationStatus in summary.json and README. This is
independent from whether Git successfully uploaded the report. Unexpected source
changes force a failed qualification and a nonzero runner exit while preserving
those changes. Logs and results-publication reserve remain in place. The small
PowerShell reporting/reproducibility additions have executed contract/integration
checks but no new Pester line/branch coverage measurement; no such metric is
claimed. No eighteen-hour execution result is claimed before the workstation runs.
