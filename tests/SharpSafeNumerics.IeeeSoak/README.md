# Nonshipping IEEE decimal soak

This independent-reference campaign exercises the explicit IEEE Decimal API. It
does not replace the existing finite-only NumericSoak. Production dependencies,
solution files and global settings are unchanged. Requires the pinned .NET11 SDK.

CLI (all three pairs required, no duplicates):

```text
--seconds 1..7200 --seed positive-Int32 --summary fresh-file
[--source-revision exact-40-hex]
```

The summary's parent must already exist. The summary and its two sibling paths
must be fresh. `CreateNew` prevents overwriting an output introduced after argument
validation. The final summary is reserved during the run; a crash can leave an
empty/incomplete file, which must **never** be interpreted as a pass.

Exit codes: **0 Passed;1 Failed;2 invalid arguments/output-I/O failure;3 Inconclusive**.
An inconclusive chunk is not a numerical pass. A week wrapper may continue it but
must retain its incomplete qualification status. Unexpected harness exceptions
also produce Failed, separately identified from numerical counterexamples.

## Actual workload and references

CsCheck5.0.1 owns UInt128-pair generation and shrinking. Each generated case
selects one of three decimal formats and one of16 operations, then executes all
five rounding directions. Batches contain at most64 generator callbacks; each
batch gets a reproducible distinct PCG state within the supplied seed stream.
Changing chunk seeds produces new campaigns rather than replaying fixed fixtures.
Only one worker executes. Deadline/cancellation checks occur before each bounded
case, and Ctrl+C requests a graceful final summary. One case and bounded shrinking
may finish after the requested duration; no hard real-time deadline is claimed.

PeterO.Numbers1.8.2 is test-only and independent of the production BigInteger glue:

- Add, subtract, multiply, divide, FMA, square root, quantize and IEEE remainder:
  exact admitted decimal inputs, preferred finite cohorts, signed zero and five
  status flags. GDA remainder-near is computed in an **unlimited quotient context**
  before destination admission; its limited-quotient Invalid rule is not IEEE.
- BigInteger→decimal (up to bounded7000-digit powers), decimal→Int64 and
  binary64↔decimal. Signaling NaNs are explicitly quieted through the reference
  precision operation at arithmetic conversion boundaries. NaN sign/payload
  propagation alternatives are not qualified; quieting and exception flags are.
- Exp, Log, ExpM1 and LogP1 use independent160/224-digit calculations before
  target rounding. Numerical/sign-zero disagreement is **Inconclusive**, not pass
  or a presumed implementation defect. This consistency test is not a rigorous
  outward enclosure proof. Inputs are bounded: exponential magnitude<=20,
  logarithm arguments in[0.001,1000], log1p arguments in[-0.9,5.1], and selected
  cancellation inputs down to10^-80. Elementary cohorts are not compared.
- Binary tininess-only policy disagreements with the reference near a normal
  boundary are retained as Inconclusive. No other mismatch is silently skipped.

Arithmetic input recipes include varied full-precision coefficients, signed
zeros, infinities, quiet/signaling NaNs, ordinary exponent ranges and supported
minimum/maximum quantum boundaries. Huge arbitrary exponents/powers and
unbounded transcendental searches are not requested by this runner.

Primary references: https://github.com/peteroupc/Numbers (NuGet1.8.2),
https://github.com/AnthonyLloyd/CsCheck (NuGet5.0.1),
https://speleotrove.com/decimal/decarith.html and IEEE754-2019
https://doi.org/10.1109/IEEESTD.2019.8766229. Package versions reuse the repository's
central pins. The final report records supplied source revision, runtime/platform
and SHA256 of the actual Decimal and runner assemblies.

## Outputs and counts

- Summary: `Kind="ieeeDecimalSoak"`, `SchemaVersion=1`, exact status, seed/PCG seed,
  timing, provenance, failure and operation/format/mode counts. See `summary.schema.json`.
- `<summary>.events.jsonl`: Started, Checkpoint (~60s), Inconclusive, failed
  comparison (`Failed`), terminal Failure and Completed records. Its kind is
  `ieeeDecimalSoakCheckpoint`; a checkpoint is not a passed final result.
- `<summary>.cscheck.log`: generator/shrinking diagnostics. The final failure
  retains the engine's seed, bits and shrink result. LastObservedShrinkingFailure
  is diagnostic; it is not asserted to be a globally minimal counterexample.

Samples counts executed generated cases; ShrinkEvaluations counts subsequent
shrinking attempts. Checks/Assertions aggregate executed comparisons, including
shrinking, so FailedChecks is not a count of unique bugs. Deadline-skipped callback
iterations in raw CsCheck logs are **not** numerical Samples. Inconclusive cases
are streamed individually with inputs/mode/reason and counted separately.

## Short reproduction (do not start a long run implicitly)

```powershell
$repo = 'C:\dev\SharpSafeNumerics-ieee' # actual checkout
$root = if ($env:CODEX_ARTIFACT_ROOT) { $env:CODEX_ARTIFACT_ROOT } else { 'E:\Artifacts' }
if (!(Test-Path -LiteralPath $repo -PathType Container)) { throw 'Checkout missing' }
if (!(Test-Path -LiteralPath $root -PathType Container)) { throw 'Artifact root missing' }
$run = Join-Path $root ('SharpSafeNumerics\ieee-soak\smoke-' + [Guid]::NewGuid().ToString('N'))
if (!(Test-Path -LiteralPath $run -PathType Container)) { New-Item -ItemType Directory -Path $run | Out-Null }
Push-Location -LiteralPath $repo
try {
    dotnet build 'tests\SharpSafeNumerics.IeeeSoak\SharpSafeNumerics.IeeeSoak.csproj' -c Release --artifacts-path (Join-Path $run 'build')
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $dll = Join-Path $run 'build\bin\SharpSafeNumerics.IeeeSoak\release\SharpSafeNumerics.IeeeSoak.dll'
    $revision = (git rev-parse HEAD).Trim()
    dotnet $dll --seconds 2 --seed 7542021 --summary (Join-Path $run 'summary.json') --source-revision $revision
    # Inspect exit0/1/2/3 and the actual summary; do not relabel exit3 as passed.
} finally { Pop-Location }
```

The week wrapper should build incrementally once per source revision and invoke
bounded chunks with changing seeds and fresh summary paths. No week-long run was
started while implementing this project.

## Qualification limits

Finite generated testing is not a proof or full IEEE754 qualification. Missing
scope includes trigonometric independent oracles, every integer/binary carrier,
payload/sign propagation policies, and globally certified elementary rounding.
Coverage/mutation results apply only to their reported helper scope; do not merge
them with shipping-library coverage as though their scopes were equal. A failure
may identify a harness/reference policy mismatch; retain it before investigating.
