# Pinned real-source SharpUnits integration — 2026-11-17

Requirement SN-017: sample project and actual compilation. This report closes
that bounded example requirement, not all downstream application integration.
Numerics base:4df84e5b5394c98aa0fa1125ae12e744e152b61f plus the new sample/helper.
Upstream SharpUnits source:7ebdbcda9958e4eb4cee623065ecdd37395dac89, net11.0.
This is development-source compatibility, not a published NuGet compatibility
claim. The main README is older than that source; actual project/API inspection
and execution govern this evidence.

## Reuse and exactness decision

The actual upstream Quantity stores magnitude as double and dimensions as exact
rational exponent vectors. Reuse Electrical.Volts and its dimensional arithmetic
through a referenced source project. Do not copy/vend the library or add it to
Core's dependency graph. A source-only optional sample is appropriate because
no usable SharpUnits/SharpUnits.Core NuGet flat-container index was found.

WholeVolts is generated from an inclusive ±2^53 Int64 domain, with Unit metadata
V. Every integer in that contiguous interval is exactly representable by double.
The explicit VoltageBridge validates initialization and produces coherent SI
volts; an invalid default returns OutOfRange and clears the quantity. Unit display
metadata alone does not supply interoperability. Values outside the admitted
interval reject even when some isolated larger integers could be represented.

Subsequent upstream arithmetic remains double-valued, may round/allocate and is
not transformed into this package's exact arithmetic. Tests choose exactly
representable5V/2ohms=2.5A while checking the real current dimension. Other units,
fractional voltage adapters and a general-purpose SharpUnits arithmetic policy
layer are outside this example.

## Executed checks

SDK11.0.100, Windows. Initial sample compilation failed because VoltageBridge
did not exist. The implemented bridge and real source then compiled and ran.
The current program passed **4,030 assertions**, seed731931: generated unit
metadata, integer arithmetic/domain admission, explicit unit bridge, actual
dimensional division, invalid-default clearing, ±2^53 endpoints, rejection at
one unit beyond each endpoint, bounded addition before conversion, and1,000
random magnitudes checked independently by BigInteger conversion.

Both supplied-clean-source and fresh-clone helper routes passed; the first
pre-metadata-assert run had4,029 checks and is retained separately. Four Pester
helper boundary tests passed. A dirty pinned checkout was rejected without
deleting its deliberately introduced file; the coordinator removed only its own
probe afterward. Git trust exceptions are explicit per-invocation paths for
exFAT, not global safe.directory changes. Existing source is never checked out
or reset by the supplied-source route.

Managed execution passed on actual x64 and x86: the program printed64/X64 and
32/X86 respectively. The x86 result is separate from the437-case library suite;
see [architecture evidence](2026-11-17-x86.md).

Windows x64 Native AOT published and executed the4,030 numerical assertions.
The first global PublishAot=true attempt failed NETSDK1207 because it propagated
to the netstandard Roslyn generator. The local SharpUnitsInteropAot opt-in fixes
the build graph without relaxing warnings or changing the generator target.
The upstream project emitted19 pre-existing CS1591 documentation warnings during
its builds; these are not claimed as warning-free upstream qualification.
The adapter itself retains the repository warning policy. No Native AOT x86 or
general upstream AOT compatibility claim is made.

## Coverage and negative controls

Coverlet.console10.1.1 measured VoltageBridge only: **4/4 lines,2/2 branches**.
The generated domain, assertion program and upstream library are excluded from
that narrow metric; their execution does not imply measured total coverage.

A disposable copy first passed the real baseline, then three independently
compiled source variants were rejected by behavioral assertions: inverted valid
input guard, Electrical.Amperes in place of Volts, and Unit=A in place of V.
All three exited nonzero with the expected integration assertion context. The
original source was untouched. These are manual negative controls, not a
Stryker score or an exhaustive sample mutation campaign.

## Provenance and reproduction

Artifacts: E:/Artifacts/SharpSafeNumerics/windows-x64/two-hour-20261118T011044Z.
sharpunits-managed-final/summary.json retains numeric base revision, upstream
pin and tracked upstream/sample/helper source hashes. x86 copies the matching
manifest and logs its actual process ABI. sharpunits-controls contains baseline,
negative-control logs and bridge-coverage.cobertura.xml. Other directories retain
the fresh-clone, dirty-source, initial red and native publish attempts.

From a verified repository and artifact parent:

```powershell
if (-not (Test-Path -LiteralPath (Get-Location).Path -PathType Container)) { throw 'Missing checkout' }
$artifactRoot = if ($env:CODEX_ARTIFACT_ROOT) { $env:CODEX_ARTIFACT_ROOT } else { 'E:\Artifacts' }
if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container)) { throw 'Missing artifact parent' }
$run = Join-Path $artifactRoot ('SharpSafeNumerics-units-' + [guid]::NewGuid().ToString('N'))
./eng/VerifySharpUnitsIntegration.ps1 -ArtifactDirectory $run
```

An existing unchanged checkout can be supplied using SharpUnitsSource; it must
match the exact pinned SHA. The standalone optional sample is intentionally
outside the normal solution, so normal integer builds need no SharpUnits source.
CI executes the same default-clone integration helper explicitly. Actual CI
success is recorded only after that workflow completes.
