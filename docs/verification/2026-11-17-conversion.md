# Conversion development evidence — 2026-11-17

Baseline main: `4970be40517c12ca968f48fc1e01baec8766128c`.
Environment: Windows x64, .NET SDK 11.0.100, runtime 11.0.0, C# 15,
warnings as errors. Implementation and test paths are traced in requirements.sdoc.
Results below are local execution on the working source for this pass, not new CI results.

Tested source SHA-256:

| File | SHA-256 |
| --- | --- |
| Core/SafeInteger.cs | 30FE795057F1D7142517D1C4673263CF1CB437F7E2F3E1D3824A4788F3E4DF91 |
| UnitTests/ConversionTests.cs | C777CC6E46E2700BF7ECC12C75A20D75CCCB7A3A96D1848BDBF84E2A27540FE2 |
| UnitTests/MutationSurvivorTests.cs | AE3D0811DBD686DFD94D9B617A9A931A3ABC8D42114E94E237B400E72574E022 |
| formal/SafeInteger.dfy | FC4ABCA4BDC32ADF16C9A57915C41F3E5F10517B931D59F75266BEAD745956DD |

## Results

- Baseline xUnit: 92 passed, 0 failed, 0 skipped.
- Before implementation, Rejected_conversion_does_not_allocate failed as intended:
  37,305,992 allocated bytes over 100,000 calls. After implementation: zero.
- Final expanded xUnit: 123 passed, 0 failed, 0 skipped.
- Conversion: all 144 ordered built-in integer pairs; 2,048 deterministic random
  byte-generated inputs per pair (294,912 total), seed 731921. Additional power-of-two,
  source/destination endpoint neighbors and exhaustive byte/sbyte source values.
  Exact BigInteger bounds independently determine expected status/value; rejected
  values must clear output. Finite samples do not prove the full wide domain.
- Allocation: each pair's zero success plus each out-of-destination-range source
  endpoint, when one exists; warmup 1,000 calls, measure 100,000 calls per path.
  All measured paths returned expected statuses/zero and allocated zero bytes.
  Wholly widening pairs have no failure inputs. BigInteger/custom providers excluded.
- CsCheck 5.0.1: 40,000 cases, seed `00000002OIk1`, one worker for reproducibility;
  10k each addition/subtraction/multiplication/int-to-sbyte conversion.
- Hedgehog 2.0.4: 3,000 cases, seed 731921; 1k each addition/multiplication/conversion.
  No failing counterexamples found; therefore no minimized failure artifacts.
- Exhaustive console harness: 693,195 assertions. Both examples and analyzer probe
  pass; union adapter builds. These are separate from the xUnit count.
- Coverlet 10.1.1, core through xUnit only: 98/99 lines (98.98%), 94/97 branches
  (96.90%). TryConvert: 100% lines and branches. Generator/analyzer/union code is
  outside this coverage scope; executable probes are not a coverage measurement.
- Stryker 5.1.0 initial conversion pass: 97 killed, 14 survived, 2 no-coverage,
  0 timeouts, score 85.84%. Survivor-driven tests added representable multiplication,
  modular subtraction/multiplication and bounded nonthrowing reads.
- Hardened Stryker: 132 generated, 103 killed, 10 survived, 0 no-coverage,
  0 timeouts, 1 compile error, 18 tool-filtered/ignored; score 91.15%.
  No source exclusion was added. Gate increased from 70% to 90%.
- Dafny 4.11.0 with Z3 4.12.1: 6 verified, 0 errors, including mathematical
  conversion admission. This is not a refinement/linking proof of C#.
- Windows x64 Native AOT publish and native execution passed, including sign-changing
  int/uint, 128-bit and native-sized conversion cases. Linux/Windows workflows will
  provide revision-specific remote evidence after push; Arm64 not exercised.
- BenchmarkDotNet 0.15.8 default toolchain fails net11 detection with
  GetRuntimeVersion(NotRecognized). InProcessEmit ShortRun completed all 9 cases
  (1 launch, 3 warmups, 3 measurements) on Intel Core i5-10505, Windows 11
  10.0.26300.9974, x64 RyuJIT x86-64-v3. Rejected checked conversions measured
  2,529.53 ns mean / 2,542.03 ns median (-1) and 2,407.55 ns mean / 2,398.80 ns
  median (256), allocating 320 B/op. SafeConversion allocated zero in each case.
  SafeConversion timing was indistinguishable from empty-method overhead for -1
  and 255; for 256 it reported 0.3907 ns mean with 4.4839 ns error. Manual guards
  also approached the measurement floor. Those timings do not establish a speedup,
  a <=10% performance budget, or WCET. Batch kernels and out-of-process support
  remain necessary for useful fast-path timing qualification. Runtime contention
  from documentation installation limits interpretation further.

## Surviving mutation dispositions

Mutant IDs refer to the hardened local report. All remain in the score denominator.

- 3, 19: exception-message removal; programmer-misuse text is not a stable contract.
- 5, 21: invalid read selects stored field instead of zero. Source inspection finds
  failed/default objects can only store zero through the public API; equivalent
  for reachable states, without a machine-checked proof.
- 31, 45: add/subtract right > 0 becomes >= 0; at zero the bound checks cannot reject
  a valid built-in input. Source-inspected equivalent within the qualified set.
- 56: zero-product shortcut OR becomes AND; remaining preflight branches and the
  product still return success/zero for a single zero operand. Performance may differ.
- 61, 67, 80: multiplication > 0 becomes >= 0; the earlier zero shortcut prevents
  those extra cases. Source-inspected equivalent within the qualified set.
- Compile-error 25 creates an impossible enum pattern. 18 ignored mutants are
  Stryker's block-already-covered filter, not project exclusions.

## Reproduction and storage

Run from an existing repository directory (PowerShell):

```powershell
$repo = (Get-Location).Path
if (-not (Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing checkout' }
$root = if ($env:CODEX_ARTIFACT_ROOT) { $env:CODEX_ARTIFACT_ROOT } else { 'E:\Artifacts' }
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Missing artifact root' }
$run = Join-Path $root ('SharpSafeNumerics\windows-x64\' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
if (-not (Test-Path -LiteralPath $run -PathType Container)) {
    New-Item -ItemType Directory -Path $run | Out-Null
}
dotnet build -c Release
dotnet test -c Release --collect:'XPlat Code Coverage' --results-directory (Join-Path $run 'coverage')
dotnet run --project tests/SharpSafeNumerics.Tests -c Release
dotnet run --project tests/SharpSafeNumerics.PropertyTests -c Release
dotnet run --project tests/SharpSafeNumerics.AnalyzerTests -c Release
dotnet run --project examples/GeneratedQueueDepth -c Release
dotnet run --project examples/RiscueQueueDepth -c Release
dotnet tool restore
$sdkVersion = (dotnet --version).Trim()
$dotnetRoot = Split-Path -Parent (Get-Command dotnet).Source
$env:MSBUILD_EXE_PATH = Join-Path $dotnetRoot "sdk\$sdkVersion\MSBuild.dll"
if (-not (Test-Path -LiteralPath $env:MSBUILD_EXE_PATH -PathType Leaf)) { throw 'Missing SDK MSBuild' }
$core = Join-Path $repo 'src\SharpSafeNumerics.Core'
if (-not (Test-Path -LiteralPath $core -PathType Container)) { throw 'Missing core project' }
Push-Location -LiteralPath $core
try {
    dotnet stryker --config-file ../../stryker-config.json --target-framework net11.0 --concurrency 4 --output (Join-Path $run 'mutation')
} finally { Pop-Location }
# Supply an installed Z3 executable compatible with Dafny 4.11.0.
$solver = (Get-Command z3 -ErrorAction Stop).Source
if (-not (Test-Path -LiteralPath $solver -PathType Leaf)) { throw 'Missing Z3' }
dotnet tool run dafny -- verify formal/SafeInteger.dfy --solver-path $solver
dotnet publish tests/SharpSafeNumerics.AotSmoke -c Release -r win-x64 -o (Join-Path $run 'aot') --artifacts-path (Join-Path $run 'aot-build')
& (Join-Path $run 'aot\SharpSafeNumerics.AotSmoke.exe')
dotnet run --project benchmarks/SharpSafeNumerics.Benchmarks -c Release -- --filter '*ConversionBenchmarks*' --job short --inProcess --artifacts (Join-Path $run 'benchmarks')
```

Local bulk artifacts: `E:\Artifacts\SharpSafeNumerics\windows-x64\20261117T173405Z`.
Stryker initially selected Visual Studio 2022 MSBuild 17.14 and could not resolve
the SDK. Supplying --target-framework net11.0 allowed analysis; local successful
commands also set process-local MSBUILD_EXE_PATH to the installed SDK MSBuild.dll.
No global PATH/SDK changes were made. Stryker workers were bounded to four; no
memory caps or free-RAM termination policy were imposed.

Adding the root solution exposed Stryker's automatic solution-mode selection in
the first pushed mutation workflow: it rejects explicit test-projects in solution
context. The workflow and reproduction now invoke Stryker from the core directory
with the root configuration's project-relative test path. The 90% gate is unchanged.

## Remaining P0/P1 qualification gaps

Wide arithmetic/policy oracle campaigns, all-platform allocation qualification,
Arm64 execution, generator declaration/member collision and 128-bit literal tests,
SSN1001/1005–1008 and proper analyzer tests, codecs/parsing/formatting, packaging/API
baselines, performance budgets and real application integrations remain open.
Malformed numeric text is outside this conversion API; no parser-fuzz campaign is
claimed. Seeded conversion value campaigns and mutation checks ran as stated.

## Source review dispositions

A second automated source-review perspective found no built-in conversion defect.
Its two important findings were addressed: original SN-003/004/005 definitions
remain parents, with distinct conversion child IDs; tested implementation/test/model
hashes now identify the source independently of the historical baseline. The
reproduction block also explicitly discovers SDK MSBuild and Z3. This was source
review, not independent execution or an external expert qualification.
