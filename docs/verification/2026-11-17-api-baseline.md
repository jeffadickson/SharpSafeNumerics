# Public API source guard — 2026-11-17

This report records the pre-expansion API-guard phase. Later user-authorized
fixed/decimal additions extend the shipping project set to five; see
[expanded qualification](2026-11-17-fixed-decimal.md). Earlier counts below are
retained as historical scope, not the final expanded candidate's totals.

Implementation base: 2e242881cb9a5075cb4fceb9f4df7cb7585efc3a. Runtime source files
were unchanged. The workstation soak remains pinned to that commit.

## Reuse assessment

Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0 is the listed stable Microsoft
package, MIT licensed and analyzer-only. The locally cached 4.14.0 is unlisted;
it was inspected but not selected. The new centrally pinned reference is private
on the four shipping projects. No existing dependency version was upgraded.

Primary references:
- https://www.nuget.org/packages/Microsoft.CodeAnalysis.PublicApiAnalyzers/5.6.0
- https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/PublicApiAnalyzers/PublicApiAnalyzers.Help.md

Existing SDK package validation remains enabled. It checks a different layer;
a published package baseline can later extend binary/constraint compatibility
checks. A custom reflection serializer was rejected because the maintained
analyzer already supplies signature formatting, nullability tracking and fixes.

## Execution

SDK 11.0.100 / C# 15. Empty nullable-aware API files first produced 95 RS0016
build errors. The official dotnet format code fix generated candidate lists.
The solution-wide include filter initially skipped some projects; per-project
code-fix execution generated all four lists without editing runtime source.
The subsequent full Release build passed with zero warnings/errors.

The lists include Core policies, enum numeric values, statuses, defaults and
nullable Unit metadata; generator/analyzer public entry points; union case
constructors, boxed object? Value getter and record members. All declarations
remain Unshipped because this candidate has not been published. The union's
compiler-synthesized oblivious record members are marked with the analyzer's
standard '~' notation rather than inventing nullability guarantees.

eng/VerifyPublicApi.ps1 passed 14 build controls in a disposable source copy:
four clean shipping-project builds, four rejected public additions (RS0016),
rejected rename/removal, enum value, optional default and nullability changes
(RS0017), one explicit scope-limit probe, and a restored Core build. Only the
snapshot is mutated. Source-link queries are disabled there because it is not a
Git checkout; API diagnostics and warnings-as-errors remain enabled.

Artifacts: E:\Artifacts\SharpSafeNumerics\windows-x64\api-baseline-20261117T211533Z.
controls-verified/summary.json records expected diagnostics and actual exit codes.

## Limits

A compiling relaxation of NumericResult<T>'s struct constraint was NOT detected
by this signature guard. Its disposable probe uses default! solely to prevent
CS8604 from hiding that measurement. This is an observed limitation, not a
compatibility pass. Type layout, complete binary compatibility, arbitrary generic
constraint changes and generated consumer-domain surfaces require additional
review/SDK package-baseline checks. Existing generated-domain tests remain
separate verification evidence; these four API lists are not their universal API
snapshot. Source signature tracking does not prove behavioral conformance.

This build-tool/configuration change adds no numerical runtime implementation.
The script has direct positive/negative build execution evidence, not separate
Pester line/branch coverage. Component coverage and package/dependency verification
are recorded after the final checks. No new numerical mutation campaign is
claimed solely because the API guard was enabled.

## Reproduction

From a verified checkout directory, with a verified existing artifact parent:

```powershell
$repo = (Get-Location).Path
if (-not (Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing checkout' }
$root = if ($env:CODEX_ARTIFACT_ROOT) { $env:CODEX_ARTIFACT_ROOT } else { 'E:\Artifacts' }
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Missing artifact root' }
$run = Join-Path $root ('SharpSafeNumerics-api-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
./eng/VerifyPublicApi.ps1 -ArtifactDirectory $run
# For an intentional API change, review the generated list diff before committing:
dotnet format analyzers src/SharpSafeNumerics.Core/SharpSafeNumerics.Core.csproj --diagnostics RS0016 --severity warn --no-restore
```

## Final local verification

All 384 regression cases passed (175 Core, 89 generator, 120 analyzer), zero
failures/skips. CsCheck and Hedgehog suites passed with their established seeds.
Coverlet scopes remain Core 170/170 lines and 133/138 branches; generator
541/543 lines and 256/269 branches; analyzer 367/375 lines and 486/564 branches.
These measure unchanged component source, not the PowerShell control harness.

The integration Core Standard Stryker run passed its unchanged 90% gate:
209 candidates, 158 assertion kills, three timeouts, seven survivors, two
uncovered, 18 compile errors, 21 ignored; 94.71%. Compiler-error population did
not inflate under the API guard. Assertion kills alone are 92.94% of the scored
population; timeouts are retained separately rather than called assertion kills.
No new full generator/analyzer numerical scores are claimed in this pass.

Windows x64 Native AOT passed with 3,096 type pairs / 52,632 oracle outcomes and
257 batch lanes, observed SIMD128. Rebuilding between package passes reproduced
all six archive SHA256 values. A fresh package consumer exercised generated
code/union results and required packaged SSN1004 failure. All four nupkg archives
were inspected: PublicApiAnalyzers is absent from their payload and dependency
metadata, and the API text files are not redistributed. Only the private build
graph changed; numerical runtime source files were not edited.

## Targeted generic metadata follow-up before numeric expansion

16 additional Core/union metadata cases brought that phase to400 passing tests.
They pin generic flags, exact interface constraints and absence of an unmanaged
strengthening marker.15 controls passed using the compiled tests against an
actual weakened Core DLL:15 constraint tests passed and the intended
NumericResult struct-constraint assertion failed. The latest Core campaign for
that phase scored94.12% (158 kills,2 timeouts,8 survivors,2 uncovered,18 compile
errors,21 ignored of209). This is separate from the subsequent expanded-source
269-candidate Core campaign and its new fixed-point tests.
