# Week IEEE qualification

Run from a checkout containing both `eng/Invoke-WeekQualification.ps1` and the existing workstation runner. Windows PowerShell5.1 is a supported launcher; the existing portable bootstrap reuses/discovers modern PowerShell or provisions it locally. Managed .NET11 SDKs are reused first. No native, Dafny, Python or unrelated runtime installation is requested by this campaign.

```powershell
$documents = [Environment]::GetFolderPath('MyDocuments')
if (!(Test-Path -LiteralPath $documents -PathType Container)) { throw 'Missing Documents folder' }
$checkoutRoot = Join-Path $documents 'GitHub'
$artifactRoot = Join-Path $documents 'QualificationArtifacts'
# The runner verifies/creates missing child directories after checking their parents.
$runner = Join-Path $PSScriptRoot '..\eng\Invoke-WeekQualification.ps1'
& $runner -Hours 168 -ReportIntervalHours 24 -IssueNumber 1 `
  -SourceRef main -CheckoutRoot $checkoutRoot -ArtifactRoot $artifactRoot
```

When invoking interactively, replace `$runner` with the actual checked-out script path. `-PlanOnly` prints paths/deadlines without creating files, installing tools or starting tests. E: is not required: the example explicitly places artifacts in redirected Documents. Without ArtifactRoot the runner uses CODEX_ARTIFACT_ROOT, then E:/Artifacts if E: exists, otherwise Documents/QualificationArtifacts. Check free disk space for a week of logs; no memory caps are imposed.

Defaults:168 hours,24-hour reporting,3600-second chunks, one oracle worker per chunk, ascending positive seeds, five-minute maximum final-report reserve. The runner fetches SourceRef at daily boundaries and prepares a fresh immutable detached source checkout/build, real decimal regression gate and short oracle preflight. A failed daily update is reported immediately and explicitly retains the prior qualified revision, making the final verdictIncomplete. `-PinnedSource` or a literal40-hex SourceRef disables updates. Numerical counters remain grouped by source SHA.

Each child receives `--seconds`, `--seed`, a fresh `--summary` path and exact `--source-revision`. Exit0 requires a consistent Passed JSON verdict; exit3 is retained as Inconclusive and subsequent chunks continue. Any inconclusive observation prevents a finalPassed verdict. Exit1/2, counterexamples, unexpected source/binary changes, or invalid summaries are fatal and trigger final reporting. Randomized finite evidence is not a whole-domain IEEE proof; the child report records its oracle scope and limitations.

Reports use a unique `qualification/issue-1/week-*` branch under `qualification-results/week/<runid>`. Every daily/final report is scoped to results only and records the pinned bootstrap/helper hashes, tested source/harness hashes, current status, source update gates, chunk seeds, per-SHA counts and raw summary hashes. Daily reports sayRunning, never finalPassed. Git push success is checked independently through remote branch SHA and exact Git blob hashes; failures remain local/pending for retry. No emails or CI changes are made.

After verified upload, default cleanup removes only manifest-owned local report staging files and small hourly summary JSON files actually copied byte-for-byte into the uploaded commit. Each summary must be at most128KiB; each report bundle is capped at4MiB, with report JSON capped at1MiB. `-KeepResults` disables deletion. Oversized/unsubmitted summaries, raw stdout/stderr, `.events.jsonl`, `.cscheck.log`, builds, checkpoints and source checkouts remain local. A hash reference alone never authorizes deleting raw data. Cleanup refuses foreign roots, changed files, junction/reparse paths, directories and incomplete upload verification. Tracked results in the isolated results checkout remain intact.

Resume with the original pinned bootstrap copy printed/stored under the run:

```powershell
# Set $run to the exact existing artifact directory printed by the runner.
if (!(Test-Path -LiteralPath $run -PathType Container)) { throw 'Missing existing run' }
$runner = Join-Path $run 'bootstrap\Invoke-WeekQualification.ps1'
& $runner -ResumeDirectory $run
```

Resume retains the original deadline, branch/source policy and next seed index. A child whose completion was not observed is markedInterrupted; its seed/output is not overwritten. Unknown checkout changes or mismatched bootstrap hashes stop recovery. The exclusive run lock prevents duplicate orchestration. Resuming after the original hard deadline may require manual publication of preserved reports if the reserved network budget has expired; the runner never extends compute permission silently.

Development evidence (2026-11-18): actual Windows PowerShell5.1 PlanOnly passed with redirected Documents roots; fake-clock/process scheduling covers daily follow/fallback, fatal and inconclusive outcomes; cleanup tests cover false push, wrong remote content, retry, outside paths, ownership and a real Windows junction. A real Git staging test confirmed CRLF bytes remain unchanged despite core.autocrlf=true through path-scoped local info attributes. A real2-second child smoke at seed7542021/source4aa4a602ca02eca15219c51e3e782a8f4b639e2a passed43 samples/645 assertions/215 oracle checks,0 failed/inconclusive. No week-long run or live remote publication was executed during development. Raw evidence is local under `E:/Artifacts/SharpSafeNumerics/windows-x64/ieee-decimal-20261118/week-runner-tests` on the development machine; this is not a required remote-machine path.

Measured orchestration coverage was4.11% commands in an early instrumented run (36/875), with no branch metric: tests import function ASTs into isolated scopes, so Pester does not attribute those executed function bodies back to original-file breakpoints. This instrumentation limitation leaves full live bootstrap/network/runtime orchestration unmeasured; the executable function tests and short real child smoke are separate evidence. No mutation campaign ran for this wrapper. StrictDoc requirements are in `week-qualification.sdoc`; no production IEEE acceptance status is changed by this orchestrator.
