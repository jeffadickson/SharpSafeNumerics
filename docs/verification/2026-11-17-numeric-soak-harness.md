# Expanded numerical campaign harness — 2026-11-17

Scope: SN-018-FIXED and SN-019-DECIMAL qualification infrastructure only.
No production arithmetic changes. This report records a bounded smoke campaign,
not completion of the separate longer qualification or whole RC1 closure.

## Reproduction and dependencies

.NET SDK 11.0.100; runtime .NET 11.0.0; Windows X64; workstation GC.
Nonshipping `tests/SharpSafeNumerics.NumericSoak` references the existing central
CsCheck 5.0.1 and PeterO.Numbers 1.8.2 pins. Build incrementally with:

```powershell
$repo = 'C:\dev\SharpSafeNumerics'
if (!(Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing checkout' }
$project = Join-Path $repo 'tests\SharpSafeNumerics.NumericSoak\SharpSafeNumerics.NumericSoak.csproj'
dotnet build $project -c Release --nologo
$dll = Join-Path $repo 'tests\SharpSafeNumerics.NumericSoak\bin\Release\net11.0\SharpSafeNumerics.NumericSoak.dll'
dotnet $dll --seconds 5 --seed 731930
Invoke-Pester -Path (Join-Path $repo 'tests\NumericSoakRunner.Tests.ps1') -Output Detailed
```

Optional `--summary` must name a fresh file in an existing parent directory.
Seconds accept 1..7200; seed accepts a positive Int32. Unknown options, duplicate
options, odd argument counts and malformed numbers reject with exit code 2.
The summary writer uses CreateNew to avoid overwriting a raced-in file.

The coordinator records production/source revision provenance for the longer run.
Starting production revision: 4df84e5b5394c98aa0fa1125ae12e744e152b61f.
Harness inputs at the smoke run (SHA256):

| File | SHA256 |
| --- | --- |
| Program.cs | BD4C07114C52BD06A55D83EDECADD95BFE8A244AD42C9D44561FB5FF890631DC |
| SharpSafeNumerics.NumericSoak.csproj | A359F488D2342156A5238D4E5471C798F39B029FEBC2EBC78BA5E511CF1795E3 |
| NumericSoakRunner.Tests.ps1 | 4C740E5536A31488B48FBC1FFB0C4EAB5E18AC7FA5DB6997840DE1ABD7DE9E63 |

## Oracle and case design

CsCheck generates two UInt128 values with one worker, `iter: long.MaxValue`,
`time: seconds`. All choices and operands are deterministic functions of these
shrinkable values. Failure exceptions retain the CsCheck seed and minimized pair;
the summary retains the complete exception. Seed731930 maps to PCG00000002OIt1.
Restarting the same runtime/harness seed replays the sample prefix; a time limit
may stop at a different final sample count.

Each sample selects one fixed operation (add, subtract, multiply, divide, negate,
or FromRatio) and exercises all six rounding policies. Endpoints, zero, small
values and scale boundaries are mixed with full Int64 patterns. The independent
BigInteger oracle divides positive magnitudes, rounds magnitude, then applies
sign. It checks exact rejection precedence, directional range failures and
cleared failed output.

Each sample also selects one decimal width and one of four arithmetic operations.
It compares ToEven and Exact against independent PeterO EDecimal contexts;
division exactness uses multiplication in the arbitrary-precision oracle rather
than rounded reverse BCL arithmetic. Cases mix precision-length coefficients,
full format exponent intervals, cancellation, positive/negative smallest
subnormal, signed zero, NaN and infinities. A selected destination exercises all
nine source/destination pairs over the campaign, including same-width conversions.
Raw decoded bits additionally challenge admission, add-zero, cancellation,
same-width conversion, double-negation and self-division identities. Rejections
check status, cleared output and false inexact flags. Signed-zero conversion
checks inspect the sign separately from numerical equality.

This is finite property testing and metamorphic testing, not IEEE754 conformance,
proof, exhaustive encoding coverage, or proof of the BCL arithmetic implementation.
The exactness text bridge shares invariant BCL roundtrip formatting with existing
tests; it does not independently qualify that formatting implementation. Raw
identities complement but do not eliminate this shared-boundary limitation.

## Executed evidence

CLI tests were written first: observed 0 passed / 4 failed while the harness did
not exist. After implementation: Release build succeeded with zero warnings and
zero errors; Pester 5.7.1 passed 4/4, zero skipped. The four tests encompass eight
malformed/unknown/duplicate numeric CLI cases, two summary-path rejection cases,
and a real 3-second seeded campaign with fresh summary validation.

A separately logged run started2026-11-18T01:17:40.2852126Z and ended
01:17:45.3239906Z (5.0214683 seconds), seed731930, PCG00000002OIt1, passed:

| Counter | Actual |
| --- | ---: |
| Completed CsCheck callbacks | 17,703 |
| Assertions evaluated | 622,923 |
| Fixed API invocations checked | 106,218 |
| Decimal arithmetic/negation API invocations checked | 121,059 |
| Decimal conversion API invocations checked | 53,109 |
| Raw encoding cases | 17,703 |

Counters increment at the actual checks/call sites; they are not inferred from
elapsed time. Arithmetic counters exclude conversions. Assertions include both
successful and rejected contract checks; they are not distinct operand counts.
Shrinking retries would also increment counters on a failed run, so failed-run
counts must not be interpreted as unique samples.

Reported allocated bytes339,853,760; final live heap2,879,640 bytes; GC0/1/2
collections54/1/0. These measure the whole verification process including
arbitrary-precision oracles, not production allocation behavior. No memory cap
or free-memory gate was imposed. Progress is bounded to five-minute checkpoints
plus startup/final output.

Artifacts (local, not committed):
`E:/Artifacts/SharpSafeNumerics/windows-x64/two-hour-20261118T011044Z/numeric-harness-corrected/`
contains `run.log`, `summary.json`, and coverage artifacts. Pester log from the earlier revision is retained under `numeric-harness-short/pester.log`; the corrected revision was rerun interactively with4/4 passing.

## Limits and remaining verification

No numerical failure occurred in the short campaign. A deliberately broken disposable harness verified shrinking and failure retention as recorded below. The larger campaign and any x86/AOT
execution are recorded separately by the coordinator. Managed oracle libraries
allocate and use general-purpose code; no claim of AOT qualification is made.
The Pester tests alone do not establish C# coverage; the scoped coverage run below does. No Stryker mutation campaign was run on this nonshipping console.

## Review correction and generator probe

Independent source review found that the first revision used the low bit of the
exponent entropy as the sign, restricting sign/exponent parity combinations.
The coordinator interrupted its initial longer preflight and preserved those
logs; it is not a passing completed qualification. The corrected revision uses
low64 bits for the exponent and bit127 independently for sign; width comes from
the other high bits. A deterministic startup property checks both signs at two
adjacent exponents and a factor10 magnitude relationship. This is a direct probe,
not a statistical claim of independence. Decimal width counts in the corrected
5-second run: Decimal32=11,881; Decimal64=3,037; Decimal128=2,785. Counts are not
uniformity claims: CsCheck intentionally biases generation toward simple inputs.
Coefficient/special-kind choices still share portions of one generated input;
this is a mixed qualification generator, not exhaustive independent Cartesian
coverage. Existing targeted boundary tests complement the sampled campaign.

## Harness-only line and branch coverage

No existing coverlet executable was present in PATH/global tools. Installed
[coverlet.console10.1.1](https://www.nuget.org/packages/coverlet.console/10.1.1)
from the official NuGet feed into the owned short-run artifact tools directory.
The source remained unchanged. A fresh copied output under
`numeric-harness-corrected/coverage-bin` was instrumented; the immutable longer
campaign copy was untouched.

Exact invocation (paths abbreviate the verified run root in the prose only):

```powershell
$root = 'E:\Artifacts\SharpSafeNumerics\windows-x64\two-hour-20261118T011044Z'
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'Missing run root' }
$run = Join-Path $root 'numeric-harness-corrected'
if (!(Test-Path -LiteralPath $run -PathType Container)) { throw 'Missing coverage run' }
$dll = Join-Path $run 'coverage-bin\SharpSafeNumerics.NumericSoak.dll'
& (Join-Path $root 'numeric-harness-short\tools\coverlet.exe') $dll --target (Get-Command pwsh).Source --targetargs "-NoProfile -File $run\coverage-run.ps1 -Dll $dll" --include '[SharpSafeNumerics.NumericSoak]*' --include-test-assembly --format json --format cobertura --output (Join-Path $run 'coverage\harness')
```

The retained coverage-run.ps1 executes ten malformed CLI cases, one3-second
positive seed731930 campaign and rejection of an existing summary file. Exit0.
Corrected-source coverage:164/167 lines (98.20%),163/180 branches (90.55%),100%
methods. The three uncovered lines are checkpoint output/update and the numerical
failure catch, which a successful sub-five-minute run does not execute. Missing
branches include defensive failure paths and short-circuit variants. This scope
includes only NumericSoak; Core/Decimal coverage is explicitly excluded. Earlier
source coverage (separate retained artifacts) was151/154 lines and160/176 branches;
these changed scopes are not evidence of a comparative improvement.

## Disposable manual negative control

Created a separate E: source copy with explicit CsCheck5.0.1/PeterO1.8.2 package
versions and an absolute project reference. Changed only the raw add-identity
oracle comparison from `sum.Value==x` to `sum.Value!=x`. No mutation hook or
mutated source was committed. Mutant Program.cs SHA256:
`08C3454BB5535D4531FFDEC7CBAAE3705F1B4FD0CA07EDEE9CCE17BC3DED1F93`.

`dotnet build NegativeControl.csproj -c Release --nologo` passed with0 warnings,
0 errors. Running its output with `--seconds 3 --seed 731930 --summary summary.json`
returned exit1, `Passed=false`. The actual retained CsCheck exception reported
9 shrinks and minimized input `(0, 0)`, with replay seed `a8eovdVTdJN1`. The summary
retains both the original campaign PCG seed and the full exception including that
minimized replay seed. The positive integer CLI replays from the original seed;
CsCheck's minimized string seed is diagnostic information, not an accepted
positive-integer CLI argument. Counters show0 fully completed callbacks but10
attempted raw cases during failure/shrinking; this demonstrates why assertions
and callback counters must not be confused with unique tested operands.

Artifacts are in `numeric-harness-negative-control/`: source, csproj, build.log,
run.log and summary.json. This single deliberate oracle fault verifies harness
failure detection and failure-summary retention; it is not a Stryker score or
production mutation-coverage claim.

## Bounded Native AOT eligibility trial

After the corrected longer managed campaign had started from its immutable
copy, attempted native publication of the unchanged current harness:

```powershell
$root = 'E:\Artifacts\SharpSafeNumerics\windows-x64\two-hour-20261118T011044Z'
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'Missing artifact root' }
$run = Join-Path $root 'numeric-harness-aot'
if (!(Test-Path -LiteralPath $run -PathType Container)) { New-Item -ItemType Directory -Path $run | Out-Null }
dotnet publish C:\dev\SharpSafeNumerics\tests\SharpSafeNumerics.NumericSoak\SharpSafeNumerics.NumericSoak.csproj -c Release -r win-x64 -p:PublishAot=true --artifacts-path (Join-Path $run 'artifacts') --nologo
```

The trial finished in under one minute and returned exit code 1. The actual
.NET 11 compiler reported IL2026 and IL3050 at Program.cs line 83: reflection-based
`JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)` requires
unreferenced/dynamic code and recommends JsonTypeInfo/JsonSerializerContext
source generation. Existing warnings-as-errors remained enabled. No warning
suppression, project change, oracle change or dependency addition was made.
No native executable was produced or run. At this stage the harness qualified only
as a managed executable; this is a harness summary-serialization limitation,
not a production Core/Decimal package AOT failure. This early compilation failure
does not establish whether all CsCheck/PeterO paths would be AOT-compatible.
Exact output is retained at `numeric-harness-aot/publish.log`; all trial build
outputs were isolated using the explicit artifacts path. The long-running
immutable managed campaign copy was untouched.


## Source-generated JSON correction and successful Native AOT trial

The bounded failure above identified a concrete BCL-supported fix. Added a typed
internal CampaignReport and source-generated CampaignJsonContext; changed only
the report constructor type and the JsonSerializer overload in Program.cs. No
oracle, generator, sampling loop, counters, production code, warnings policy or
package dependencies changed. The initial AOT failure above is historical and
is superseded by the actual native results here.

Current full source SHA256:

| File | SHA256 |
| --- | --- |
| Program.cs | C9B87ECF912258EC5E5CE47F756CFE12C673C44D66DF581180D53324FA7B9597 |
| CampaignReport.cs | 1FBD28DCECBA015290ED3DECFA3338520E7EF6B9B0C3D631894352C1DA37CFC1 |

The running immutable longer managed binary retains the earlier whole Program.cs
hash BD4C07114C52BD06A55D83EDECADD95BFE8A244AD42C9D44561FB5FF890631DC.
Its long qualification must be attributed to that exact pre-serialization-change
binary, not to the whole updated helper. The current helper has the separate
short managed/native evidence below.

A retained text comparison reconstructed the prechange source from the old
disposable negative-control copy by undoing its single deliberate oracle fault.
Undoing the two serialization edits in current Program.cs then reproduced that
source exactly after trimming terminal whitespace. The entire Require-to-end
region (all arithmetic oracles, input generation and metamorphic checks) is
byte-identical after terminal whitespace normalization, SHA256:
`55DFAE5A8F9A7CAAA89D1424D63F83864EDC6EFD5AA5DF2FD430E8ED1D8B5856`.
The comparison also covers the loop because only the two serialization edits
were permitted by the full-source equality check. This is source-diff evidence,
not a claim that different whole binaries are identical.

Artifacts are isolated under `numeric-harness-aot-v2/` in the same verified E:
run root. Repeated the exact Native AOT publish command above with its artifacts
path changed to this new directory. Actual win-x64 native publication returned0,
with no compiler warnings or errors; then executed the produced native file:

```powershell
$root = 'E:\Artifacts\SharpSafeNumerics\windows-x64\two-hour-20261118T011044Z'
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'Missing artifact root' }
$run = Join-Path $root 'numeric-harness-aot-v2'
if (!(Test-Path -LiteralPath $run -PathType Container)) { throw 'Missing trial output' }
& (Join-Path $run 'artifacts\publish\SharpSafeNumerics.NumericSoak\release_win-x64\SharpSafeNumerics.NumericSoak.exe') --seconds 5 --seed 731930 --summary (Join-Path $run 'native-summary.json')
```

Actual current-source results, all one-worker seed731930 / PCG00000002OIt1:

| Execution | Seconds | Completed callbacks | Assertions | Outcome |
| --- | ---: | ---: | ---: | --- |
| Managed | 5.0315412 | 18,608 | 654,750 | exit0, Passed=true |
| Native AOT win-x64 | 5.0025425 | 25,625 | 901,416 | exit0, Passed=true |

These short campaigns establish actual native execution of the exercised oracle
paths, not complete CsCheck/PeterO AOT conformance, long native stability, or a
performance comparison. Concurrent work and different sample counts prohibit a
controlled speed claim. Pester reran with4/4 passing, including summary parsing
and the actual short managed campaign.

Reran the disposable manual negative control with the new CampaignReport.cs and
source-generated context included. Actual result: exit1, Passed=false,11 shrinks,
minimized `(0, 0)`, replay seed `9DjvSczoq6G1` retained in the serialized exception.
Artifacts under `negative-control/` preserve the exact variant, build and run.
This confirms failure-summary retention after the serializer change; it remains
a single deliberate oracle-fault test, not a production mutation score.

Reran Coverlet10.1.1 with the same harness-only include filter and CLI matrix on a
fresh copied current managed output. New scope includes source-generated JSON
code:755/869 lines (86.88%),193/252 branches (76.58%),86.48% methods. Handwritten
Program.cs remains164/167 lines; CampaignReport.cs is27/27. Generated JSON
serialization/deserialization and defensive paths account for the expanded scope
and unexecuted branches. This result cannot be compared directly to the earlier
167-line helper scope as a coverage regression. Production Core/Decimal remain
excluded. Exact artifacts: `coverage/harness.{json,cobertura.xml}`, coverage.log,
coverage-run.ps1, publish.log, managed.log/summary, native.log/summary, pester.log,
and source-diff-proof.json. No warning suppressions or compiler-policy relaxation
were used.

Before committing, trailing blank lines only were removed from Program.cs and
the Pester file. Current Program.cs SHA256 is
F38A0038BC7483BDE94B3C8249DBB59B47709FC575FC140A765443463B5C4A5A.
The prior hashes above identify the executed snapshots; no CSharp token or
oracle behavior changed in this formatting pass. The aggregate Pester run
rebuilds the helper incrementally and exercises the current source.
