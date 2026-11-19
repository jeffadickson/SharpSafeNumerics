# Focused function survivor counterexamples — 2026-11-18

The existing campaign02 aggregate score **81.84%** belongs to its original source
and test snapshot. It is retained unchanged. The following are individual
counterexample replays, **not a new aggregate mutation score**.

The actual `IeeeFunctionMatrixTests.cs` gained two semantic test groups across
three decimal formats and five rounding directions: thirty additional xUnit
cases. Existing MPFR rows and provenance were unchanged. The complete matrix
passed **1,290 tests, zero failures/skips**, in strict .NET11/C#15 builds without
warning suppression. Independent PeterO exact-power checks supplement analytic
values, signs and preferred quantum; no private optimization threshold was made
an acceptance requirement.

| Campaign02 mutant | Counterexample | Correct | Mutated observation |
|---|---|---|---|
| 1834, SinPi half-integer branch forced to zero | sinPi(0.5) | 1, no flags | 0 |
| 1592, negative integer power forced through positive-power branch | pow(0.25,-5) | 1024, no flags | 0.0009765625 |
| 1602, inverse-power decimal exponent sign removed | pow(0.25,-5) | 1024, no flags | 1.024E-17 |
| 1699, reciprocal-root decimal exponent sign removed | rootn(-0.008,-3) | -5, no flags | -0.05 |
| 1737, negative-power preferred-quantum sign ignored | quantum(pow(1.00,-2)) | 0 | -4 |

The tests also cover negative and positive half-grid sine signs, integer cosine,
nonzero source quantum in positive/negative integer powers, finite rational powers,
negative/positive root indices, exact-root cohorts, RootN identity bit preservation,
integer-index extremes, exact negative log1p powers, Compound(-1) zero/pole
behavior, source/context rejection before unsupported binary NaN decoding,
Pi-scaled infinity quadrants and positive-y/negative-x angles from reflected
independent MPFR bounds. These additions challenge user-observable semantics.

`Replay-FunctionMutants.ps1` reads authoritative locations/replacements from the
existing Stryker5.1.0 JSON report. It copies production sources outside Git,
restores the report's original target source, applies one existing mutation at a
time, and compiles an isolated .NET11 snapshot plus the two new test groups. It
does not generate new mutations or alter original production files.

Observed control: **30 passed, zero failed**. Each of the five mutations compiled
and produced **15 failed, 15 passed**, zero skips. Compile errors are explicitly
rejected as a successful replay outcome. Raw logs, summaries, original report
hash and test hash are under:
`E:/Artifacts/SharpSafeNumerics/windows-x64/ieee-decimal-20261118/functions-matrix/counterexample-replay`.

Reproduction (use the actual checkout and retained report):

```powershell
$repo = 'C:\dev\SharpSafeNumerics-ieee'
$run = 'E:\Artifacts\SharpSafeNumerics\windows-x64\ieee-decimal-20261118\functions-matrix'
if (!(Test-Path -LiteralPath $repo -PathType Container)) { throw 'Checkout missing' }
if (!(Test-Path -LiteralPath $run -PathType Container)) { throw 'Verified artifact parent missing' }
$replay = Join-Path $run 'counterexample-replay'
if (!(Test-Path -LiteralPath $replay -PathType Container)) { New-Item -ItemType Directory -Path $replay | Out-Null }
$report = 'E:\Artifacts\SharpSafeNumerics\windows-x64\ieee-decimal-20261118\remaining-mutation\campaign-02\stryker-output\reports\mutation-report.json'
if (!(Test-Path -LiteralPath $report -PathType Leaf)) { throw 'Retained Stryker report missing' }
& (Join-Path $repo 'eng\DecimalConformance\Replay-FunctionMutants.ps1') -Checkout $repo -Report $report -ArtifactDirectory $replay
```

Final matrix-only Coverlet coverage after these additions:
IeeeDecimalFunctions **96.69% line /94.89% branch**;
IeeeDecimalPiFunctions **100% line /95% branch**. Scope is this matrix, not the
entire test suite or library. XML and execution log are under `functions-matrix/
counterexample-coverage` and `counterexample-coverage.log`. Other survivors,
exception/refinement paths, general fuzz/property campaigns and full-domain
correct-rounding proof remain unclosed. No production bug was found in these
unmutated counterexamples; root integration and the next Stryker campaign are
separate qualification steps.
