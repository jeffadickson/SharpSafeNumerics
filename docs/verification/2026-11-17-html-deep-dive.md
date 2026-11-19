# Interactive HTML deep dive verification — 2026-11-17

Requirement SN-DOC-INTERACTIVE. Package source: 3db0779293deb5095c6f7d592e85c5ffca7793fb
plus this documentation/tooling change; runtime library sources are unchanged.
The standalone page uses browser BigInt models and explicitly labels their scope.
It does not execute .NET, Roslyn or SIMD in the browser.

## Executed checks

.NET 11.0.100 compiled eng/DeepDiveEvidence in Release with zero warnings/errors.
Its real-package export produced 2,424 fixtures: 2,304 arithmetic/policy cases
(12 types, 16 boundary operand pairs, three operations, four policies),
96 exact conversions and 24 generated-domain arithmetic/codec cases. Integer
values are decimal strings. Native-sized fixtures record this host's 64-bit width.
The --verify command agreed with the embedded JSON. An isolated document with
an incorrect native width was rejected by that command, as expected.

Node v24.20.0 and an existing Playwright browser installation were reused.
Chromium 130.0.6723.31 verified all embedded cases and tested policy changes,
128-bit boundaries, malformed integers, oversize inputs, conversion rejection,
domain rejection/primitive overflow, big-endian bytes, all eight diagnostic
selectors, SIMD 256/128/scalar partitions, scalar tails, empty batches and all
buffer error modes. Copy success/denial paths were tested with an isolated mock
clipboard; the user's clipboard was preserved. Keyboard focus was checked.

All browser assertions passed at 1,440, 390 and 320 CSS-pixel widths, with no
horizontal page overflow, clipped controls, page exceptions or external requests.
Desktop and phone renderings were visually inspected. Screenshots, console
summary and V8 coverage are under the run-specific bulk artifact directory:
E:\Artifacts\SharpSafeNumerics\windows-x64\wide-20261117T175912Z\deep-dive-release-qa.

V8 range measurement: 12,208 / 12,562 script character positions executed (97.18%).
This is nested-range coverage, not line, statement or branch coverage. It cannot
be compared with Coverlet metrics. Remaining ranges include the model-mismatch
reporting branch and other unselected display/validation cases. Three deliberate
semantic mutations on isolated document copies (saturation replaced by wrapping,
lossy conversion accepted, domain clamp endpoint changed) were all detected by
fixture disagreement. No exclusion or score threshold was changed.

The small managed fixture exporter has positive and negative execution evidence,
but no separate Coverlet line/branch measurement or Native AOT qualification.
The browser verifier and regeneration/mutation PowerShell scripts also have no
.NET coverage. Runtime library implementation and its previously recorded
qualification metrics were not changed by this documentation work.

## Reproduction

Run from a checked repository directory. Reuse an existing Playwright module
and browser installation; these checks do not install Node/npm dependencies.
The module parameter is the package directory exporting chromium (for example,
the package shipped with an existing Microsoft.Playwright installation).

```powershell
$repo = (Get-Location).Path
if (-not (Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing checkout' }
$artifactRoot = if ($env:CODEX_ARTIFACT_ROOT) { $env:CODEX_ARTIFACT_ROOT } else { 'E:\Artifacts' }
if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container)) { throw 'Missing artifact root' }
$run = Join-Path $artifactRoot ('SharpSafeNumerics-html-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))
if (-not (Test-Path -LiteralPath $run -PathType Container)) { New-Item -ItemType Directory -Path $run | Out-Null }
dotnet run --project eng/DeepDiveEvidence -c Release -- --verify docs/interactive-deep-dive.html
# Only when intentionally refreshing fixtures:
./eng/UpdateDeepDive.ps1 -ArtifactDirectory $run
# Set this to an existing, checked Playwright package directory.
if (-not (Test-Path -LiteralPath $PlaywrightModule -PathType Container)) { throw 'Missing Playwright module' }
node eng/VerifyDeepDive.cjs $PlaywrightModule (Join-Path $run 'browser')
./eng/VerifyDeepDiveMutations.ps1 -PlaywrightModule $PlaywrightModule -ArtifactDirectory $run
```

Core CI now verifies embedded fixture agreement using the actual package. Browser
checks are an explicit local workflow and do not add Node to ordinary .NET builds.
The document cites Microsoft primary documentation for checked contexts, numeric
promotion, generic math and SIMD; those pages were checked during authoring.
