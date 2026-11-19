# Fixed-point mathematical proof trial — 2026-11-17

Trace: SN-018-FIXED (coordinator-owned StrictDoc requirement). Source:
`formal/FixedPoint.dfy`, SHA256
`1777AEDB4DE9132C9A4B3E1226D8C9CC106D356802EA14B4A521BA5CAECC6872`.
Base HEAD was `2e242881cb9a5075cb4fceb9f4df7cb7585efc3a`; source is an
uncommitted addition in the shared implementation workspace.

## Scope and result

Dafny 4.11.0, Z3 4.12.1 x64: **4 verified, 0 errors**. The signed
rational model proves truncation residual strictly less than the positive
denominator, nearest-even residual at most half a denominator, and even parity
at exact half ties. Division scales the numerator by 2^32, accepts Int64-bounded
raw operands, proves successful output bounds, proves exact division equality
when Exact is selected, propagates the rounding error bound for both divisor
signs, rejects zero divisors, and clears output on every modeled failure.
Proof arithmetic is mathematical integer arithmetic, with Int64 endpoint
preconditions/admission. This is a bounded contract, not exhaustive C# testing.

The model was translated to C#, built with SDK 11.0.100 targeting net11.0,
and executed through the generated Main entry point. **10 expect checks passed**:
positive/negative nearest-even ties, negative truncation, inexact rejection,
zero divisor, upper/lower admission rejection and successful exact division.
The generated C# and Dafny runtime remain only in the artifact directory;
no production runtime dependency or solution change was added.

## Reproduction

From the checked repository (check directories before using them):

```powershell
$repo = 'C:\dev\SharpSafeNumerics'
$artifactRoot = if ($env:CODEX_ARTIFACT_ROOT) { $env:CODEX_ARTIFACT_ROOT } else { 'E:\Artifacts' }
if (!(Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing repository' }
if (!(Test-Path -LiteralPath $artifactRoot -PathType Container)) { throw 'Missing artifact root' }
$run = Join-Path $artifactRoot 'SharpSafeNumerics\windows-x64\fixedpoint-proof-20261117'
if (!(Test-Path -LiteralPath $run -PathType Container)) { New-Item -ItemType Directory -Path $run | Out-Null }
$z3 = Join-Path $artifactRoot 'SharpSafeNumerics\windows-x64\20261117T173405Z\z3\z3-4.12.1-x64-win\bin\z3.exe'
Set-Location -LiteralPath $repo
dotnet dafny verify formal/FixedPoint.dfy --solver-path $z3
dotnet dafny build formal/FixedPoint.dfy --target cs --spill-translation --output "$run\FixedPoint" --solver-path $z3
# Dafny emits net8.0 by default. Retarget only the disposable generated project.
$generated = Join-Path $run 'FixedPoint.csproj'
(Get-Content -LiteralPath $generated -Raw).Replace('net8.0','net11.0') | Set-Content -LiteralPath $generated
dotnet build $generated --nologo
dotnet "$run\bin\Debug\net11.0\FixedPoint.dll"
```

Actual raw logs: `E:/Artifacts/SharpSafeNumerics/windows-x64/fixedpoint-proof-20261117/`
contains verify.log, build.log, net11-build.log and net11-execute.log. Initial
net11 command-line TargetFramework override did not restore the matching target
(NETSDK1005); explicitly retargeting the generated project resolved it. Final
build: 0 errors, 4 NU1510 warnings for Dafny-emitted redundant framework packages.

## Limits

This model does **not** prove refinement/equivalence to handwritten SafeFixed C#,
Int128 implementation safety, allocation properties, invalid rounding byte
validation, Floor/Ceiling/AwayFromZero, decimal operations or product closure.
Production C# tests remain necessary. No model mutation/fuzz/property campaign
or generated-C# line/branch coverage measurement was run; those metrics are
unmeasured, not zero or 100%. The mathematical contracts are solver-verified;
the ten executable examples are a finite translation/entry-point smoke check.
No Native AOT, Linux execution or CI was run for this isolated proof addition.
