# Reviewing soak evidence with a native preprocessor

`qualcompact` is an optional Rust program. It reads qualification reports locally
and writes a deterministic JSON triage report with a default 8 KiB budget. Give
the compact report to a reviewer first; use its paths and SHA256 references to
open specific raw evidence only when needed. `--details` retains all normalized
findings separately. No report is uploaded by this tool.

From an existing checkout with Rust installed, PowerShell:

```powershell
$checkout = (Get-Location).Path
if (-not (Test-Path -LiteralPath (Join-Path $checkout 'eng/Invoke-QualificationPreprocessor.ps1') -PathType Leaf)) { throw 'Run from the SharpSafeNumerics checkout' }
$results = Read-Host 'Path to the published qualification-results run directory'
if (-not (Test-Path -LiteralPath $results -PathType Container)) { throw 'Results directory missing' }
$reportParent = Read-Host 'Existing directory for compact and full reports'
if (-not (Test-Path -LiteralPath $reportParent -PathType Container)) { throw 'Report directory missing' }
$id = [guid]::NewGuid().ToString('N')
& (Join-Path $checkout 'eng/Invoke-QualificationPreprocessor.ps1') `
    -InputPath $results `
    -OutputPath (Join-Path $reportParent "triage-$id.json") `
    -DetailsPath (Join-Path $reportParent "details-$id.json")
```

The wrapper builds incrementally in `CODEX_ARTIFACT_ROOT`, falling back to
`E:\Artifacts`; pass `-ArtifactDirectory` explicitly on other machines. It does
not install tools or change the running soak. Output parents must exist and
outputs must be fresh. Native CLI usage and supported schemas are documented in
[the tool README](../eng/qualification-preprocessor/README.md).

Read each run's `verdict`, issues, omission counts and source identity. Exit zero
means preprocessing succeeded; a qualification can still have `Failed` verdict.
Missing source revisions stay unknown. Separate reports are never summed into
one mutation score or one soak total. Unsupported/corrupt evidence is reported
and returns a nonzero preprocessing status. A too-small byte budget fails
explicitly rather than dropping runs; increase `-MaxBytes` or process a smaller
explicit input set. Directory scanning covers only top-level report files.

Rust is optional for normal .NET builds. Windows and Linux have a separate native
tool workflow. The current pinned workstation job needs no change: preprocess
its published reports after they arrive.

The pinned18-hour-budget workstation campaign has now completed. Its published
scalar/SIMD128/SIMD256 reports were processed with the native tool before review;
see [the results](verification/2026-11-18-workstation-18h.md). Keep each mode's
verdict, source, seed and elapsed record separate. Any sum of executed workload
counts is not distinct-input coverage or one combined qualification duration.
