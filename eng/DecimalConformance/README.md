# Decimal conformance references

This verification-only .NET 11/C#15 tool replays **explicit** GDA decTest inputs
against PeterO.Numbers 1.8.2, including per-file precision, rounding, adjusted
exponent bounds, clamp, expected tuple and every mapped GDA condition. It also
supports `--production decimal32|decimal64|decimal128 FILE...` to execute
`IeeeDecimal<T>` against explicit GDA results and five IEEE flags.
A passing **reference** replay qualifies the
corpus adapter/oracle agreement, not the production implementation.

`provenance.json` pins the original primary URLs and archive SHA256 hashes.
The primary decimal homepage explicitly applies the ICU license to its testcases.
Keep the downloaded license and each original source/header notice with the
artifacts; no third-party source/corpus is copied into Git. The corpus is beta,
not exhaustive, and predates IEEE754-2019. decNumber3.68 has published errata.

PowerShell reproduction (check/create the run parent deliberately first):

```powershell
$repo = (Get-Location).Path # run from the actual checkout
if (!(Test-Path -LiteralPath $repo -PathType Container)) { throw 'Checkout missing' }
$root = if ($env:CODEX_ARTIFACT_ROOT) { $env:CODEX_ARTIFACT_ROOT } else { 'E:\Artifacts' }
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'Artifact root missing' }
$run = Join-Path $root 'SharpSafeNumerics\windows-x64\ieee-decimal-20261118\conformance'
if (!(Test-Path -LiteralPath $run -PathType Container)) { New-Item -ItemType Directory -Path $run | Out-Null }
$project = Join-Path $repo 'eng\DecimalConformance\DecimalConformance.csproj'
& (Join-Path $repo 'eng\DecimalConformance\Get-Corpus.ps1') -ArtifactDirectory $run
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$exe = Join-Path $repo 'eng\DecimalConformance\bin\Release\net11.0\DecimalConformance.dll'
dotnet $exe --probe > (Join-Path $run 'bcl-probes.jsonl')
dotnet $exe (Join-Path $repo 'eng\DecimalConformance\smoke.decTest')
dotnet $exe (Join-Path $run 'corpus\add.decTest') > (Join-Path $run 'reference-add.jsonl') 2> (Join-Path $run 'reference-add-summary.json')
```

The normal replay exit codes are 0 for no mismatches/malformed records, 1 for
reference disagreement, 2 for malformed input/invalid usage. Skipped operation,
rounding or condition names are **reported skips**, never passes. Includes are
reported but not followed; pass child filenames explicitly. Subset arithmetic,
GDA `#` missing-operand sentinel, width-qualified/encoded operands, unsupported operations and directives are
explicit exclusions. `CompareToTotal` checks cohorts, signed zeros and NaN
representations rather than only numerical equality. A mismatch is evidence to
investigate, not proof that the corpus or implementation is wrong.

`--probe` emits BCL values and 63 finite exp/log/sqrt comparisons against a
160-digit independent oracle rounded once to the target precision. A JSONL
`matches:false` record is a discrepancy. This small sample does not qualify
correct rounding for every input, directed modes, flags or raw NaN propagation.
The pinned .NET11 source routes all formats through software binary128, with
its potential `double` dispatch gate disabled; do not misdescribe it as an
unconditional double fallback.

## Native independent reference

The optional adapter is thin C glue to unmodified decNumber3.68, compiled with
the existing WSL gcc. No engine is reimplemented. There is no native Windows
compiler on the inspected PATH. Build with:

```powershell
if (!(Test-Path -LiteralPath $repo -PathType Container)) { throw 'Checkout missing' }
if (!(Test-Path -LiteralPath $run -PathType Container)) { throw 'Run directory missing' }
& (Join-Path $repo 'eng\DecimalConformance\Build-NativeReference.ps1') -ArtifactDirectory $run
```

The script prints the unique Linux executable path. Invocation arguments:
`OP PRECISION EMIN EMAX CLAMP ROUND OPERAND1 OPERAND2 [OPERAND3]`.
Unary operations still take dummy operand2 `0`. Supported operations:
add, subtract, multiply, divide, quantize, squareroot, exp, ln, log10, fma.
Rounding uses **decNumber's enum**, not the project's enum:
0=ceiling, 1=up, 2=half-up, 3=half-even, 4=half-down, 5=down, 6=floor, 7=05up.
Output line 1 is the exact decimal representation; line 2 is raw decNumber
status (not IEEE flags). For example Rounded|Inexact is 2080.
Inputs that cannot be parsed exactly at 2000 digits are rejected; this bound
is an adapter limitation, not an engine or standard limit. Unknown operations
and malformed options return exit2. Sources/executables remain out of Git.

```powershell
# Replace the executable path with the actual path printed by the build script.
wsl -d Ubuntu-24.04 --exec /tmp/sharpsafe-decnumber-12345/decimal-reference divide 7 -95 96 1 3 1 3
```

Output should be `0.3333333` followed by `2080`; record actual output and tool
versions. This is a native Linux reference, not a verified native Windows build.
Native vectorization flags are enabled, but no SIMD speedup is claimed.

Production replay demands matching fixed-format precision/exponent/clamp and
**exact operand admission**, including cohort. Unsupported rounding and NaN input
are skipped explicitly. For example:
`dotnet $exe --production decimal64 (Join-Path $run 'corpus\ddAdd.decTest')`.
The initial linked build required temporary `-p:NoWarn=RS0016` while public API
baselines were under concurrent development; this is not a clean repository
acceptance build.

Subsequent integrated qualification added all-format/five-mode MPFR point bounds
for 39 IEEE elementary functions plus Cbrt, internal interval invariants, properties,
malformed-input controls, coverage, mutation and Windows Native AOT. See the
repository verification report for exact source scopes. This helper's Coverlet
scope measured 172/191 lines and 243/305 branches; it excludes native reference
glue, scripts and dependencies. Literal malformed token framing and unreadable
files return exit2; malformed fixtures may not count as passing numerical cases.
`malformed.decTest` retains four original negative controls.

Open corpus work: raw sNaN/payload admission and all-format external encoding
vectors. Those exclusions do not erase direct API raw-bit tests. Cross-platform
CI and exhaustive/global correct-rounding conformance are separate evidence axes;
do not publish a whole-IEEE conformance claim from finite probes.
