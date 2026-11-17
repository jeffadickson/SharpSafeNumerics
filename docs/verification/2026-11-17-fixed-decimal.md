# Expanded RC1 fixed-point and safe decimal verification

Date: 2026-11-17; implementation base 2e242881cb9a5075cb4fceb9f4df7cb7585efc3a
plus the new source in this commit. SDK11.0.100, Windows x64. This is separate
from the active workstation run pinned to the earlier integer source. It does
not claim that the long soak exercised the new APIs.

Contracts: SN-018-FIXED and SN-019-DECIMAL in requirements.sdoc;
[API usage and limitations](../FIXED_AND_DECIMAL.md). Optional decimal runtime
dependencies are Core and the BCL. PeterO.Numbers1.8.2 is test-only.

## Regression, independent references and malformed options

Final local Release tests: **437 passed, 0 failed, 0 skipped**:197 Core/union
metadata and integer/fixed tests,15 decimal tests,89 generator tests,136 analyzer
tests. Initial missing-API compilation failures were observed before implementing
each numerical slice. Final analyzer-enabled solution build passed without
warnings/errors. Expected failures clear result storage; unsuccessful Value
access intentionally throws while TryGetValue does not.

Fixed-point tests include6,000 random full-width raw operand pairs with six
rounding policies and BigInteger references (seed731927), an endpoint matrix,
manual signed expected rounding tables, negative denominators, carries, cleared
failures and all250 undefined rounding bytes. An independent positive-magnitude
nearest-even reference is also exercised through CsCheck/Hedgehog. Exact
fractional rejection precedes range checks; other modes round before admission.
The warmed tested fixed arithmetic loop allocated zero managed bytes; this
does not establish application-wide allocation or real-time behavior.

Decimal tests compare all formats with independent PeterO arithmetic: seed731927
has120 pairs/format across four operations; seed731928 has160 pairs/format with
7/16/34-digit coefficients, both signs and format-wide exponents, cancellation,
rounded results and exactness flags. Additional targeted tests cover fractional
scales, odd/even halfway addition for both signs at all widths, all nine format
conversion pairs, subnormal halves, lost tiny addends, signed zeros, extrema,
unsupported double and all254 undefined rounding bytes. Arithmetic exactness is
compared with unlimited-precision reference sums/products; division uses an
exact product relation. No public text parser is added; malformed numeric-policy
bytes are exhaustively challenged. Conversion tie examples cover128-to32;
other conversion boundaries and exact values are covered without claiming every
conversion rounding boundary or arbitrary bit-pattern conformance.

A subsequent raw-encoding campaign (seed731929) decoded1,024 random encodings
per format, including noncanonical and special values, and checked finite
admission, clearing and exact algebra identities. All3,072 samples passed;
this is seeded fuzz/property evidence, not an exhaustive encoding sweep.

Property frameworks passed with CsCheck seed00000002OIk1 and Hedgehog731921.
Existing42,000 CsCheck/4,000 Hedgehog top-level integer cases are retained;
new10,000 CsCheck/1,000 Hedgehog fixed pairs and2,000 CsCheck/1,000 Hedgehog
decimal pairs exercise the new paths (decimal pairs expand across three formats).
The first new property run failed in the harness at CreateChecked<BigInteger>,
unsupported by the BCL decimal types; minimized case(0,0), replayaHy2QFXrRkI1.
The bounded exact-reference calculations now use representable Int32 values.
This was a harness conversion defect, not a swallowed numerical failure.

## Coverage

Coverlet collector10.1.1; ReportGenerator5.6.0; final solution regression scope:

| Assembly | Lines | Branches |
| --- | --- | --- |
| Core |204/204 (100%)|165/170 (97.06%)|
| Decimal |52/52 (100%)|64/66 (96.97%)|
| Analyzer |378/385 (98.18%)|523/592 (88.34%)|
| Generator |541/543 (99.63%)|256/269 (95.17%)|
| Optional unions, incidental instrumentation |0/6|0/2|

Analyzer coverage merges the final solution run and the later136-test isolated
reference run against unchanged production source; other assembly scopes are
unchanged. The new fixed implementation's classes have100% line/branch coverage. Decimal
branches for impossible finite invariant-format situations remain unexecuted.
Union behavior executes in the package consumer but that separate run is not
instrumented; reflection-only metadata tests do not cover its executable bodies.
PowerShell control scripts and the generated proof smoke have no new line/branch
coverage claim. Coverage does not establish correctness.

## Mutation

Stryker.NET5.1.0, Standard level, unchanged90% break thresholds, concurrency4:

| Scope | Candidates | Killed | Timeout | Survived | No coverage | Compile errors | Ignored | Score |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Core, including fixed point |269|207|2|13|2|20|25|93.30%|
| Decimal, first campaign |100|75|0|16|0|2|7|82.42%, failed gate|
| Decimal, strengthened tests |100|84|0|7|0|2|7|92.31%, passed gate|
| Analyzer, optional decimal integration |548|391|1|30|5|87|34|91.80%, passed gate|

Timeouts remain distinct from assertion kills. Core assertion kills alone are
207/224=92.41%; decimal has no timeouts. No exclusions or lowered gates were
introduced. Block-filter ignored candidates are not counted as kills.
The decimal replay used14 tests; the later encoding-fuzz addition changes no
production source. The analyzer full campaign used131 tests and took7m26.115s;
assertion kills alone are391/427=91.57%. Its remaining survivors, uncovered paths
and timeout are retained, separate from prior campaign scores.

Review of the analyzer survivors exposed fixture reference contamination: both
libraries appeared in every test host. Five isolated-reference/source-assembly
cases brought analyzer tests to136. A focused character-span replay over
NumericSymbols.cs{1714..4930} scored49 candidates:46 assertion kills,3 survivors,
zero timeouts/uncovered,93.88%. New optional-reference candidates2/4/9/11/37/38/39
were killed. Remaining24/25/32 are older format-helper/scope variants and are
retained; this narrower result is not added to the full548-candidate score.
The initial attempted span35..93 selected no viable candidates (character
positions, not lines); it produced no score and is not counted as verification.

Fixed's60 candidates comprise49 assertion kills,5 survivors,2 compile errors,
4 ignored. Survivors36/49/53/55/71 change strict inequality to include zero after
zero/nonzero guards have already settled that case. Their equivalence is a
control-flow review, not an executed exhaustive proof. Other Core survivors and
uncovered paths retain their prior dispositions; no universal mutation closure.

Decimal regression additions killed9 more candidates: successful value extraction,
negation exactness, useful failure diagnostics and exact mixed-scale fractional
arithmetic. Remaining IDs56/57 are sign-boundary equivalences for nonzero overflow
rationals;70/75 require an exponent marker at impossible positionzero in finite
invariant text;97 has equal results at scalezero. IDs17/66 replace R formatting
with the empty default format and remain retained formatting variants, rather
than excluded. NET11 Number.Formatting.Common routes R to G; observed finite
format probes agree, but universal format identity is not claimed here.

## Formal, packages and native entry point

Dafny4.11.0/Z34.12.1:12 integer obligations and4 fixed-point obligations verified
with zero errors. The fixed model proves signed rounding residual/half-unit
bounds, tie parity, Q32.32 admission and cleared failures. Generated CSharp built
and ran10 expect checks; see [proof report](2026-11-17-fixedpoint-proof.md).
These mathematical models are not refinement proofs of the production CSharp.
Real StrictDoc0.30.4 parsed/exported all three requirement documents.

Windows x64 Native AOT built and executed the expanded real entry point: fixed
admission/inexact/divide-zero and all three decimal formats' exact/inexact,
nonfinite/zero-divisor/invalid-option paths passed alongside the existing3,096
integer type pairs,52,632 oracle outcomes and257 lanes. Observed SIMD128 in this
portable image; no decimal SIMD claim.

All eight archives (five nupkg, three snupkg) reproduced byte-for-byte across a
same-checkout rebuild. Every runtime/package payload was inspected; neither
PublicApiAnalyzers nor PeterO appears as a redistributed dependency, and source
API text files are absent. Fresh package-only consumers exercised fixed/decimal
behavior and required actual packaged SSN1003/1004/1007 diagnostics. New analyzer
tests include real optional assembly metadata, alias-only references and unrelated
lookalikes; TryNegate is excluded from known allocating-helper detection.

Five shipping API lists and targeted metadata tests are active.17 disposable
controls passed, including actual rejection of a weakened NumericResult DLL by
compiled metadata tests. Source signature lists alone do not detect generic
constraint relaxation; general released-package/binary compatibility remains open.
14 Pester workstation runner tests passed after adding future fixed/decimal
qualification steps. The active pinned run is unchanged.

## Reproduction and provenance

Artifacts: E:/Artifacts/SharpSafeNumerics/windows-x64/fixed-decimal-20261117.
source-hashes.json records the numerical/analyzer/formal source hashes; raw logs,
coverage XML, mutation JSON and package SHA256 values remain outside Git.
Commands below run from an existing checked repository; use fresh verified
artifact directories as the scripts require.

```powershell
if (-not (Test-Path -LiteralPath (Get-Location).Path -PathType Container)) { throw 'Missing checkout' }
$artifactRoot = if ($env:CODEX_ARTIFACT_ROOT) { $env:CODEX_ARTIFACT_ROOT } else { 'E:\Artifacts' }
if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container)) { throw 'Missing artifact root' }
$run = Join-Path $artifactRoot ('SharpSafeNumerics-verification-' + [guid]::NewGuid().ToString('N'))
if (-not (Test-Path -LiteralPath $run -PathType Container)) { New-Item -ItemType Directory -Path $run | Out-Null }
$runCoverage = Join-Path $run 'coverage'
$nativeOutput = Join-Path $run 'native'
$coreMutationOutput = Join-Path $run 'mutation-core'
$decimalMutationOutput = Join-Path $run 'mutation-decimal'
foreach ($directory in @($runCoverage, $nativeOutput, $coreMutationOutput, $decimalMutationOutput)) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) { New-Item -ItemType Directory -Path $directory | Out-Null }
}
dotnet build -c Release
dotnet test -c Release --collect:'XPlat Code Coverage' --results-directory $runCoverage
dotnet run --project tests/SharpSafeNumerics.PropertyTests -c Release
dotnet run --project examples/FixedAndDecimal -c Release
dotnet publish tests/SharpSafeNumerics.AotSmoke -c Release -r win-x64 -o $nativeOutput
# From each applicable source directory:
dotnet stryker --config-file ../../stryker-config.json --target-framework net11.0 --concurrency 4 --output $coreMutationOutput
dotnet stryker --config-file ../../stryker-decimal-config.json --target-framework net11.0 --concurrency 4 --output $decimalMutationOutput
```

The mutation commands run from their respective source directories; the other
commands run from the repository root. No final-source Linux/ARM64 CI result is
claimed until observed. macOS
remains disabled. SharpUnits integration, target timing,32-bit runtime execution,
full IEEE754 conformance and cross-path package identity remain outside this
evidence. No NuGet publication or release tag is implied.
