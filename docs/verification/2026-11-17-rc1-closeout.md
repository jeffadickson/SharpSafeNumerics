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
