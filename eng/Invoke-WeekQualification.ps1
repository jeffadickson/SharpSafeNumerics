# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

<#
.SYNOPSIS
Run a resumable week of managed IEEE qualification with daily bounded results.
.DESCRIPTION
PowerShell 5.1 may launch this wrapper; the established workstation helper bootstraps
modern PowerShell when necessary. No long run occurs with PlanOnly. Source follows
the configured branch only at daily boundaries unless PinnedSource is selected.
#>
[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$Repository = 'jeffadickson/SharpSafeNumerics',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._/-]*$')][string]$SourceRef = 'ieee-decimal-completion',
    [ValidateRange(1,2147483647)][int]$IssueNumber = 1,
    [ValidateRange(0.001,8760)][double]$Hours = 168,
    [ValidateRange(0.001,168)][double]$ReportIntervalHours = 24,
    [ValidateRange(1,7200)][int]$ChunkSeconds = 3600,
    [ValidateRange(1,2147483647)][int]$Seed = 7542020,
    [string]$CheckoutRoot,
    [string]$ArtifactRoot,
    [string]$ResumeDirectory,
    [switch]$PinnedSource,
    [switch]$KeepResults,
    [switch]$PlanOnly,
    [Parameter(DontShow=$true)][ValidatePattern('^week-[A-Za-z0-9-]+$')][string]$RunId,
    [Parameter(DontShow=$true)][string]$DeadlineUtc
)
$ErrorActionPreference = 'Stop'

function New-WeekPlan {
    param([DateTime]$Now, [double]$DurationHours, [double]$IntervalHours, [int]$Seconds, [string]$Id,
          [string]$CheckoutBase, [string]$ArtifactBase, [string]$Ref, [bool]$Pinned, [int]$Issue)
    $reserve = [Math]::Min(300, [Math]::Max(1, $DurationHours * 360))
    $deadline = $Now.AddHours($DurationHours)
    return [ordered]@{ Kind='weekQualificationPlan'; SchemaVersion=1; RunId=$Id; Hours=$DurationHours;
        ReportIntervalHours=$IntervalHours; ChunkSeconds=$Seconds; SourceRef=$Ref;
        FollowSource=(!$Pinned -and $Ref -notmatch '^[a-fA-F0-9]{40}$'); StartedUtc=$Now.ToString('O');
        DeadlineUtc=$deadline.ToString('O'); WorkDeadlineUtc=$deadline.AddSeconds(-$reserve).ToString('O');
        CleanupReserveSeconds=$reserve; ResultsBranch="qualification/issue-$Issue/$Id";
        Checkout=(Join-Path $CheckoutBase "SharpSafeNumerics-$Id-results");
        Artifacts=(Join-Path $ArtifactBase "SharpSafeNumerics/workstation/$Id") }
}

function Get-WeekSeed([int]$InitialSeed, [long]$Index) {
    # Modular arithmetic keeps every child seed positive without Int32 overflow.
    return [int](1 + (([long]$InitialSeed - 1 + $Index) % [int]::MaxValue))
}

function Get-WeekDecision($State, [DateTime]$Now) {
    $Now=$Now.ToUniversalTime()
    $workEnd = [DateTime]::Parse($State.WorkDeadlineUtc).ToUniversalTime()
    $next = [DateTime]::Parse($State.NextReportUtc).ToUniversalTime()
    if ($Now -ge $workEnd) { return @{ Action='Complete'; Seconds=0 } }
    # End the last chunk a little before reporting, keeping upload time within the interval.
    $reportReserve = [Math]::Min(60, [Math]::Max(1, $State.ReportIntervalHours * 36))
    $untilReport = ($next.AddSeconds(-$reportReserve) - $Now).TotalSeconds
    if ($untilReport -lt 1) { return @{ Action='Report'; Seconds=0 } }
    $seconds = [int][Math]::Floor([Math]::Min($State.ChunkSeconds, [Math]::Min(($workEnd-$Now).TotalSeconds, $untilReport)))
    if ($seconds -lt 1) { return @{ Action='Complete'; Seconds=0 } }
    return @{ Action='Chunk'; Seconds=$seconds }
}

function Get-WeekFinalStatus($State) {
    if ($State.Failure -or @($State.Chunks | Where-Object Status -eq 'Failed').Count) { return 'Failed' }
    if (!$State.Chunks.Count -or $State.SourceUpdateFailures -gt 0 -or $State.PreflightInconclusive -gt 0 -or
        @($State.Chunks | Where-Object Status -in @('Inconclusive','Interrupted','Running')).Count) { return 'Incomplete' }
    return 'Passed'
}

function Save-WeekCheckpoint($State) {
    $directory = [IO.Path]::GetFullPath($State.RunDirectory)
    if (!(Test-Path -LiteralPath $directory -PathType Container)) { throw 'Missing checkpoint directory' }
    $path = Join-Path $directory 'checkpoint.json'
    $temporary = Join-Path $directory ('checkpoint-' + [guid]::NewGuid().ToString('N') + '.partial')
    $State.UpdatedUtc = [DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText($temporary, ($State | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $path -PathType Leaf) { Copy-Item -LiteralPath $path -Destination (Join-Path $directory 'checkpoint.previous.json') -Force }
    [IO.File]::Move($temporary, $path, $true)
}

function Confirm-WeekOwnedPath([string]$Root, [string]$Path) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $full = [IO.Path]::GetFullPath($Path)
    if (!$full.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup path is outside the owned run' }
    $cursor = $full
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Cleanup refuses reparse points and junctions' }
        }
        $parent=Split-Path -Parent $cursor
        if (!$parent -or $parent -eq $cursor) { break }
        $cursor=$parent
    }
    return $full
}

function Remove-WeekSubmittedFiles([string]$Root, [string]$Id, [object[]]$Entries, [string]$VerifiedCommit, [switch]$Keep) {
    if ($Keep) { return @() }
    if ($VerifiedCommit -notmatch '^[a-fA-F0-9]{40}$') { throw 'Cleanup requires a verified remote commit' }
    if (!(Test-Path -LiteralPath $Root -PathType Container)) { throw 'Missing owned run root' }
    $marker = Join-Path $Root '.week-qualification-owner.json'
    if (!(Test-Path -LiteralPath $marker -PathType Leaf)) { throw 'Missing ownership marker' }
    [void](Confirm-WeekOwnedPath $Root $marker)
    $owner = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($owner.RunId -cne $Id -or [IO.Path]::GetFullPath($owner.Root) -cne [IO.Path]::GetFullPath($Root)) { throw 'Run ownership marker does not match' }
    $eligible = @()
    # Prevalidate the complete manifest before deleting any file. A partial upload,
    # changed file or path escape must preserve all pending results for recovery.
    foreach ($entry in $Entries) {
        if (!$entry.DeleteAfterUpload) { continue }
        if (!$entry.VerifiedBlob -or $entry.ExpectedBlob -cne $entry.VerifiedBlob) { throw 'Submitted content was not completely verified' }
        $full = Confirm-WeekOwnedPath $Root $entry.LocalPath
        if (Test-Path -LiteralPath $full) {
            if (!(Test-Path -LiteralPath $full -PathType Leaf)) { throw 'Cleanup only removes owned files' }
            if ((Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash -cne $entry.SHA256) { throw 'Owned result changed after submission' }
            $eligible += $full
        }
    }
    foreach ($full in $eligible) { Remove-Item -LiteralPath $full -Force }
    return $eligible
}

function Read-WeekChildSummary([string]$Path, [int]$ExitCode, [int]$ExpectedSeed, [string]$ExpectedRevision) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'Child did not produce a completed summary' }
    if ((Get-Item -LiteralPath $Path).Length -gt 1048576) { throw 'Child summary exceeds the bounded input limit; retained locally' }
    $summary = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($summary.Kind -cne 'ieeeDecimalSoak' -or $summary.SchemaVersion -ne 1 -or $summary.SourceRevision -cne $ExpectedRevision -or $summary.Seed -ne $ExpectedSeed) { throw 'Child schema, source revision or seed does not match its invocation' }
    if ($ExitCode -eq 0 -and ($summary.Status -cne 'Passed' -or $summary.FailedChecks -ne 0 -or $summary.InconclusiveChecks -ne 0 -or $summary.Failure -or $summary.Samples -le 0)) { throw 'Exit zero contradicts the child verdict or observed counts' }
    if ($ExitCode -eq 3 -and ($summary.Status -cne 'Inconclusive' -or $summary.FailedChecks -ne 0 -or $summary.Failure)) { throw 'Inconclusive exit contradicts child failure evidence' }
    if ($ExitCode -in @(1,2) -and $summary.Status -cne 'Failed') { throw 'Fatal child exit cannot be an inconclusive or passed result' }
    return $summary
}

function Confirm-WeekResumeState($State, [string]$Directory, [string]$ExpectedRepository) {
    if ($State.Kind -cne 'weekQualification' -or $State.SchemaVersion -ne 1 -or $State.Repository -cne $ExpectedRepository -or
        $State.RunId -notmatch '^week-[A-Za-z0-9-]+$' -or [IO.Path]::GetFullPath($State.RunDirectory) -cne [IO.Path]::GetFullPath($Directory)) { throw 'Resume checkpoint ownership/schema/repository mismatch' }
    $expectedCheckout=Join-Path $State.CheckoutRoot ("SharpSafeNumerics-$($State.RunId)-results")
    if ([IO.Path]::GetFullPath($State.ResultsCheckout) -cne [IO.Path]::GetFullPath($expectedCheckout) -or $State.ResultsBranch -notmatch ('^qualification/issue-[0-9]+/'+[regex]::Escape($State.RunId)+'$')) { throw 'Resume checkout/branch is outside the owned run' }
    $marker=Join-Path $Directory '.week-qualification-owner.json'
    [void](Confirm-WeekOwnedPath $Directory $marker)
    $owner=Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($owner.RunId -cne $State.RunId -or [IO.Path]::GetFullPath($owner.Root) -cne [IO.Path]::GetFullPath($Directory)) { throw 'Resume ownership marker does not match' }
}

function Invoke-WeekLoop($State, [scriptblock]$Clock, [scriptblock]$ExecuteChunk,
                         [scriptblock]$PrepareSource, [scriptblock]$Publish, [scriptblock]$Persist) {
    while ($State.Status -eq 'Running') {
        $now = & $Clock
        $decision = Get-WeekDecision $State $now
        if ($decision.Action -eq 'Complete') { $State.Status = Get-WeekFinalStatus $State; break }
        if ($decision.Action -eq 'Report') {
            & $Publish $State 'Daily'
            if ($State.FollowSource) {
                $update = & $PrepareSource $State
                if (!$update.Success) { $State.SourceUpdateFailures++; & $Publish $State 'SourceUpdateFailed' }
            }
            do { $State.NextReportUtc = ([DateTime]::Parse($State.NextReportUtc).AddHours($State.ReportIntervalHours)).ToString('O') }
            while ([DateTime]::Parse($State.NextReportUtc).ToUniversalTime() -le (& $Clock))
            & $Persist $State
            continue
        }
        $index = [long]$State.NextChunkIndex
        $chunk = [ordered]@{ Index=$index; Seed=(Get-WeekSeed $State.InitialSeed $index); SourceRevision=$State.SourceRevision;
            RequestedSeconds=$decision.Seconds; Status='Running'; StartedUtc=$now.ToString('O'); FinishedUtc=$null;
            ExitCode=$null; SummaryPath=$null; SummarySHA256=$null; Samples=$null; Assertions=$null; Failure=$null }
        $State.NextChunkIndex = $index + 1
        $State.Chunks += $chunk
        & $Persist $State
        try {
            $result = & $ExecuteChunk $State $chunk
            foreach ($key in $result.Keys) { $chunk[$key] = $result[$key] }
            $chunk.Status = if ($chunk.ExitCode -eq 0) { 'Passed' } elseif ($chunk.ExitCode -eq 3) { 'Inconclusive' } else { 'Failed' }
            if ($chunk.Status -eq 'Failed') { $State.Failure = if ($chunk.Failure) { $chunk.Failure } else { "Chunk $index exited $($chunk.ExitCode)" }; $State.Status='Failed' }
        } catch { $chunk.Status='Failed'; $chunk.Failure=$_.Exception.Message; $State.Failure=$_.Exception.Message; $State.Status='Failed' }
        $chunk.FinishedUtc = (& $Clock).ToString('O')
        & $Persist $State
    }
    & $Publish $State 'Final'
    & $Persist $State
}

$started = [DateTime]::UtcNow
$documents = [Environment]::GetFolderPath('MyDocuments')
if (!$CheckoutRoot) { $CheckoutRoot = Join-Path $documents 'GitHub' }
if (!$ArtifactRoot) {
    if ($env:CODEX_ARTIFACT_ROOT) { $ArtifactRoot=$env:CODEX_ARTIFACT_ROOT }
    elseif (Test-Path -LiteralPath 'E:\' -PathType Container) { $ArtifactRoot='E:\Artifacts' }
    else { $ArtifactRoot=Join-Path $documents 'QualificationArtifacts' }
}
if (!$RunId) { $RunId='week-'+$started.ToString('yyyyMMddTHHmmssZ')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8) }
$plan = New-WeekPlan $started $Hours $ReportIntervalHours $ChunkSeconds $RunId $CheckoutRoot $ArtifactRoot $SourceRef $PinnedSource.IsPresent $IssueNumber
if ($DeadlineUtc) { $plan.DeadlineUtc=[DateTime]::Parse($DeadlineUtc).ToUniversalTime().ToString('O'); $plan.WorkDeadlineUtc=([DateTime]::Parse($plan.DeadlineUtc).AddSeconds(-$plan.CleanupReserveSeconds)).ToString('O') }
if ($PlanOnly) { $plan | ConvertTo-Json -Depth 5; return }
if ($env:OS -ne 'Windows_NT' -or [IntPtr]::Size -ne 8) { throw 'The week runner requires Windows x64' }

# Import only named functions, not the workstation script's executable orchestration.
# This preserves its tested process containment, SDK selection and report commit checks.
$helperPath = Join-Path $PSScriptRoot 'Invoke-WorkstationQualification.ps1'
if (!(Test-Path -LiteralPath $helperPath -PathType Leaf)) { throw 'Missing workstation runner helpers' }
$tokens=$null; $parseErrors=$null
$helperAst=[Management.Automation.Language.Parser]::ParseFile($helperPath,[ref]$tokens,[ref]$parseErrors)
if ($parseErrors.Count) { throw 'Cannot parse workstation helpers' }
foreach ($functionName in @('Ensure-Directory','Network-Seconds','Read-OfficialUrl','Download','Portable-Tool','Initialize-ProcessJob','Start-Step','Complete-Step','Read-Step','Invoke-Step','Select-QualificationDotnet','Commit-Results')) {
    $definition=@($helperAst.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$false) | Where-Object Name -eq $functionName)
    if ($definition.Count -ne 1) { throw "Missing or ambiguous workstation helper: $functionName" }
    . ([scriptblock]::Create($definition[0].Extent.Text))
}

$absoluteDeadline=[DateTime]::Parse($plan.DeadlineUtc).ToUniversalTime()
$workDeadline=$absoluteDeadline.AddSeconds(-$plan.CleanupReserveSeconds)
$toolRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'SharpSafeNumericsQualification/tools'
$run=$plan.Artifacts
if ($ResumeDirectory) { $run=[IO.Path]::GetFullPath($ResumeDirectory) }
if ($ResumeDirectory -and !(Test-Path -LiteralPath $run -PathType Container)) { throw 'Missing resume directory' }
if (!$ResumeDirectory -and (Test-Path -LiteralPath $run)) {
    $bootstrapMarker=Join-Path $run '.bootstrap-owned'
    if (!(Test-Path -LiteralPath $bootstrapMarker -PathType Leaf) -or [IO.File]::ReadAllText($bootstrapMarker) -cne $RunId) { throw 'Use a fresh run directory' }
}
Ensure-Directory $run
Ensure-Directory $toolRoot
$downloads=New-Object 'System.Collections.Generic.List[object]'
if ($PSVersionTable.PSVersion -lt [Version]'7.2') {
    $pwsh=@(Portable-Tool 'PowerShell/PowerShell' '^PowerShell-[0-9.]+-win-x64\.zip$' 'pwsh.exe')[0]
    $forward=@('-NoProfile','-File',$PSCommandPath)
    foreach ($entry in $PSBoundParameters.GetEnumerator()) {
        if ($entry.Value -is [Management.Automation.SwitchParameter]) { if ($entry.Value.IsPresent) { $forward += '-'+$entry.Key } }
        else { $forward += '-'+$entry.Key; $forward += [string]$entry.Value }
    }
    if (!$PSBoundParameters.ContainsKey('RunId')) { $forward+=@('-RunId',$RunId) }
    if (!$PSBoundParameters.ContainsKey('DeadlineUtc')) { $forward+=@('-DeadlineUtc',$absoluteDeadline.ToString('O')) }
    # The bootstrap already created only its owned artifact directory; permit the
    # modern process to recognize that marker rather than reuse arbitrary outputs.
    [IO.File]::WriteAllText((Join-Path $run '.bootstrap-owned'),$RunId)
    & $pwsh @forward
    exit $LASTEXITCODE
}

# Runtime orchestration continues below; pure functions above support bounded tests.
$records=[Collections.Generic.List[object]]::new()
$counter=0
$savedEnvironment=@{}
foreach ($key in @('PATH','DOTNET_ROOT','MSBUILD_EXE_PATH','GIT_AUTHOR_NAME','GIT_AUTHOR_EMAIL','GIT_COMMITTER_NAME','GIT_COMMITTER_EMAIL','GIT_TERMINAL_PROMPT','GH_PROMPT_DISABLED','GCM_INTERACTIVE')) { $savedEnvironment[$key]=[Environment]::GetEnvironmentVariable($key,'Process') }
$git=$null; $gh=$null; $lock=$null; $state=$null; $checkout=$null; $sourceSha=$null

function Invoke-WeekGit([string]$Name, [string[]]$Arguments, [string]$Directory, [int]$Limit=120) {
    # exFAT has no ownership metadata. Trust only this explicit task-owned path,
    # on this invocation; never modify global safe.directory configuration.
    return Read-Step $Name $git (@('-c',('safe.directory='+$Directory.Replace('\','/')))+$Arguments) $Directory $Limit
}

function Get-WeekSdk([string]$Directory) {
    $global=Get-Content -LiteralPath (Join-Path $Directory 'global.json') -Raw | ConvertFrom-Json
    $required=[string]$global.sdk.version
    if ($required -notmatch '^11\.\d+\.\d+(?:-[\w.-]+)?$') { throw 'The managed week campaign requires a repository-pinned .NET 11 SDK' }
    $candidates=@((Get-Command dotnet.exe -CommandType Application -All -ErrorAction SilentlyContinue).Source)
    foreach ($root in @($env:DOTNET_ROOT,(Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'dotnet'),(Join-Path $toolRoot ('dotnet-'+$required)))) { if ($root) { $candidates+=Join-Path $root 'dotnet.exe' } }
    $sdk=Select-QualificationDotnet $candidates $Directory
    if (!$sdk) {
        $root=Join-Path $toolRoot ('dotnet-'+$required)
        Ensure-Directory $root
        $installer=Join-Path $run ('dotnet-install-'+$required+'.ps1')
        if (!(Test-Path -LiteralPath $installer -PathType Leaf)) { Download 'https://dot.net/v1/dotnet-install.ps1' $installer }
        $pwsh=(Get-Process -Id $PID).Path
        if ((Invoke-Step 'install-week-sdk' $pwsh @('-NoProfile','-File',$installer,'-Version',$required,'-Architecture','x64','-InstallDir',$root,'-NoPath') $Directory 1500) -ne 0) { throw 'Isolated .NET SDK installation failed' }
        $sdk=Select-QualificationDotnet @((Join-Path $root 'dotnet.exe')) $Directory
    }
    if (!$sdk -or $sdk.Version -notmatch '^11\.') { throw 'No usable repository-selected .NET 11 x64 SDK' }
    return $sdk
}

function Test-WeekSourceClean([string]$Directory) {
    $changes=Invoke-WeekGit 'source-worktree-status' @('status','--porcelain=v1','--untracked-files=normal') $Directory
    if ($changes) { throw "Source worktree changed unexpectedly; preserved at $Directory" }
}

function Prepare-WeekSource($State) {
    $State.DayIndex++
    $day=[int]$State.DayIndex
    $candidate=$null
    $gate=[ordered]@{ Day=$day; CheckedUtc=[DateTime]::UtcNow.ToString('O'); SourceRevision=$null; Status='Failed'; Failure=$null; ContinuedSourceRevision=$State.SourceRevision }
    try {
        [void](Invoke-WeekGit 'fetch-week-source' @('fetch','--','origin',$State.SourceRef) $checkout 600)
        $candidate=Invoke-WeekGit 'resolve-week-source' @('rev-parse','FETCH_HEAD^{commit}') $checkout
        if ($candidate -notmatch '^[a-f0-9]{40}$') { throw 'Fetched source did not resolve to an immutable commit' }
        $gate.SourceRevision=$candidate
        $directory=Join-Path $CheckoutRoot ("SharpSafeNumerics-$($State.RunId)-day{0:D3}-{1}" -f $day,$candidate.Substring(0,12))
        if (Test-Path -LiteralPath $directory) { throw 'Fresh daily source checkout already exists; preserved' }
        [void](Invoke-WeekGit 'create-week-source-worktree' @('worktree','add','--detach',$directory,$candidate) $checkout 300)
        Test-WeekSourceClean $directory
        $dayRun=Join-Path $run ("revisions/day-{0:D3}-{1}" -f $day,$candidate)
        Ensure-Directory $dayRun
        $sdk=Get-WeekSdk $directory
        $env:DOTNET_ROOT=$sdk.Root
        $env:MSBUILD_EXE_PATH=$sdk.MSBuild
        $sdk.Info | Set-Content -LiteralPath (Join-Path $dayRun 'dotnet-info.txt')
        $project=Join-Path $directory 'tests/SharpSafeNumerics.IeeeSoak/SharpSafeNumerics.IeeeSoak.csproj'
        if (!(Test-Path -LiteralPath $project -PathType Leaf)) { throw 'This source revision does not contain the IEEE soak harness' }
        $build=Join-Path $dayRun 'build'; $publish=Join-Path $dayRun 'publish'
        Ensure-Directory $build; Ensure-Directory $publish
        $buildArgs=@('publish',$project,'-c','Release','--no-self-contained','-p:UseAppHost=false','-p:UseSharedCompilation=false','-m:2','--artifacts-path',$build,'-o',$publish)
        if ((Invoke-Step 'publish-week-ieee-soak' $sdk.Executable $buildArgs $directory 1800) -ne 0) { throw 'Daily IEEE harness build failed' }
        $regression=Join-Path $directory 'tests/SharpSafeNumerics.DecimalTests/SharpSafeNumerics.DecimalTests.csproj'
        if ((Invoke-Step 'daily-decimal-regression' $sdk.Executable @('test',$regression,'-c','Release','--artifacts-path',$build,'-m:2','-p:UseSharedCompilation=false','--logger','trx','--results-directory',(Join-Path $dayRun 'test-results')) $directory 1800) -ne 0) { throw 'Daily decimal regression gate failed' }
        $harness=Join-Path $publish 'SharpSafeNumerics.IeeeSoak.dll'
        $preflight=Join-Path $dayRun 'preflight.json'
        $preflightSeed=Get-WeekSeed $State.InitialSeed ([long]$day+[int]::MaxValue-1)
        $code=Invoke-Step 'daily-ieee-preflight' $sdk.Executable @($harness,'--seconds','1','--seed',[string]$preflightSeed,'--summary',$preflight,'--source-revision',$candidate) $directory 121
        if ($code -notin @(0,3)) { throw "Daily IEEE preflight exited $code" }
        [void](Read-WeekChildSummary $preflight $code $preflightSeed $candidate)
        if ($code -eq 3) { $State.PreflightInconclusive++ }
        Test-WeekSourceClean $directory
        $inputs=@(Invoke-WeekGit 'daily-source-inputs' @('ls-files','--','src','tests/Shared','tests/SharpSafeNumerics.IeeeSoak','tests/SharpSafeNumerics.DecimalTests','global.json','Directory.Build.props','Directory.Build.targets','Directory.Packages.props') $directory) -split '\r?\n'
        $manifest=@($inputs | Where-Object { $_ } | Sort-Object -Unique | ForEach-Object { [pscustomobject]@{ File=$_; SHA256=(Get-FileHash -LiteralPath (Join-Path $directory $_) -Algorithm SHA256).Hash } })
        $manifestPath=Join-Path $dayRun 'source-sha256.json'
        $manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $manifestPath
        $active=[ordered]@{ SourceRevision=$candidate; WorkingDirectory=$directory; DayDirectory=$dayRun; Dotnet=$sdk.Executable; SDK=$sdk.Version;
            Harness=$harness; HarnessSHA256=(Get-FileHash -LiteralPath $harness -Algorithm SHA256).Hash;
            SourceManifestSHA256=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash; PreflightExitCode=$code }
        $gate.Status='Passed'; $gate.ContinuedSourceRevision=$null
        $State.ActiveSource=$active; $State.SourceRevision=$candidate
        $State.HarnessSHA256=$active.HarnessSHA256; $State.SourceManifestSHA256=$active.SourceManifestSHA256
        $State.SourceUpdates+=$gate
        Save-WeekCheckpoint $State
        return @{ Success=$true }
    } catch {
        $gate.Failure=$_.Exception.Message
        $State.SourceUpdates+=$gate
        Save-WeekCheckpoint $State
        return @{ Success=$false; Failure=$gate.Failure }
    }
}

function Invoke-WeekChunk($State, $Chunk) {
    $active=$State.ActiveSource
    Test-WeekSourceClean $active.WorkingDirectory
    $harnessHash=(Get-FileHash -LiteralPath $active.Harness -Algorithm SHA256).Hash
    if ($harnessHash -cne $active.HarnessSHA256) { throw 'Prepared immutable harness binary changed' }
    $chunkDirectory=Join-Path $run ("chunks/chunk-{0:D6}-{1}" -f $Chunk.Index,$Chunk.SourceRevision.Substring(0,12))
    if (Test-Path -LiteralPath $chunkDirectory) { throw 'Fresh chunk directory already exists' }
    Ensure-Directory $chunkDirectory
    $summaryPath=Join-Path $chunkDirectory 'summary.json'
    $arguments=@($active.Harness,'--seconds',[string]$Chunk.RequestedSeconds,'--seed',[string]$Chunk.Seed,'--summary',$summaryPath,'--source-revision',$Chunk.SourceRevision)
    $context=Start-Step ("ieee-hour-{0:D6}" -f $Chunk.Index) $active.Dotnet $arguments $active.WorkingDirectory ($Chunk.RequestedSeconds+120)
    if (!$context) { throw 'Chunk could not start within the remaining deadline' }
    $Chunk.StdoutPath=$context.Stdout; $Chunk.StderrPath=$context.Stderr; $Chunk.SummaryPath=$summaryPath
    Save-WeekCheckpoint $State
    $exitCode=Complete-Step $context
    if ($exitCode -notin @(0,1,2,3)) { throw "IEEE child timed out or exited unexpectedly: $exitCode" }
    $child=Read-WeekChildSummary $summaryPath $exitCode $Chunk.Seed $Chunk.SourceRevision
    Test-WeekSourceClean $active.WorkingDirectory
    return @{ ExitCode=$exitCode; SummaryPath=$summaryPath; SummarySHA256=(Get-FileHash -LiteralPath $summaryPath -Algorithm SHA256).Hash;
        Samples=$child.Samples; Assertions=$child.Assertions; PassedChecks=$child.PassedChecks; FailedChecks=$child.FailedChecks; InconclusiveChecks=$child.InconclusiveChecks;
        Failure=$(if ($child.Failure) { ($child.Failure | ConvertTo-Json -Depth 6 -Compress).Substring(0,[Math]::Min(2048,($child.Failure | ConvertTo-Json -Depth 6 -Compress).Length)) } else { $null }) }
}

function New-WeekReport($State, [string]$Reason) {
    $revisionSummaries=@(foreach ($group in @($State.Chunks | Group-Object { $_.SourceRevision })) {
        [ordered]@{ SourceRevision=$group.Name; ChunkCount=$group.Count;
            PassedChunks=@($group.Group | Where-Object Status -eq 'Passed').Count;
            InconclusiveChunks=@($group.Group | Where-Object Status -eq 'Inconclusive').Count;
            FailedChunks=@($group.Group | Where-Object Status -eq 'Failed').Count;
            Samples=($group.Group | ForEach-Object { $_.Samples } | Measure-Object -Sum).Sum; Assertions=($group.Group | ForEach-Object { $_.Assertions } | Measure-Object -Sum).Sum }
    })
    return [ordered]@{ Kind='weekQualification'; SchemaVersion=1; RunId=$State.RunId; Repository=$State.Repository; SourceRef=$State.SourceRef;
        SourceRevision=$State.SourceRevision; FollowSource=$State.FollowSource; BootstrapRevision=$State.BootstrapRevision;
        BootstrapSHA256=$State.BootstrapSHA256; HelpersSHA256=$State.HelpersSHA256; ResultsBranch=$State.ResultsBranch;
        Status=$State.Status; ReportKind=$Reason; StartedUtc=$State.StartedUtc; UpdatedUtc=[DateTime]::UtcNow.ToString('O');
        DeadlineUtc=$State.DeadlineUtc; Hours=$State.Hours; ReportIntervalHours=$State.ReportIntervalHours; NextReportUtc=$State.NextReportUtc;
        SourceManifestSHA256=$State.SourceManifestSHA256; HarnessSHA256=$State.HarnessSHA256; Failure=$State.Failure;
        SourceUpdateFailures=$State.SourceUpdateFailures; PreflightInconclusive=$State.PreflightInconclusive; SourceUpdates=$State.SourceUpdates;
        ChunkCount=$State.Chunks.Count; PassedChunks=@($State.Chunks | Where-Object Status -eq 'Passed').Count;
        InconclusiveChunks=@($State.Chunks | Where-Object Status -eq 'Inconclusive').Count;
        FailedChunks=@($State.Chunks | Where-Object Status -eq 'Failed').Count; RevisionSummaries=$revisionSummaries; Chunks=$State.Chunks }
}

function Complete-WeekUploads($State) {
    $head=Invoke-WeekGit 'results-current-head' @('rev-parse','HEAD') $checkout
    [void](Invoke-WeekGit 'push-daily-results' @('push','-u','origin',('HEAD:refs/heads/'+$State.ResultsBranch)) $checkout 180)
    $remote=Invoke-WeekGit 'verify-pushed-results' @('ls-remote','--heads','origin',('refs/heads/'+$State.ResultsBranch)) $checkout
    if (($remote -split '\s+')[0] -cne $head) { throw 'Pushed results branch does not resolve to the expected commit' }
    foreach ($pending in @($State.PendingUploads | Where-Object { !$_.Verified -or !$_.CleanupComplete })) {
        foreach ($entry in $pending.Entries) {
            $blob=Invoke-WeekGit 'verify-submitted-blob' @('rev-parse',($head+':'+$entry.RelativePath)) $checkout
            if ($blob -cne $entry.ExpectedBlob) { throw 'Remote commit content does not match the submitted ownership manifest' }
            $entry.VerifiedBlob=$blob
        }
        $pending.Verified=$true; $pending.VerifiedRemoteCommit=$head
        Save-WeekCheckpoint $State
        $removed=@(Remove-WeekSubmittedFiles $run $State.RunId $pending.Entries $head -Keep:$State.KeepResults)
        $pending.RemovedFiles=$removed
        $pending.CleanupComplete=$true
        Save-WeekCheckpoint $State
    }
}

function Publish-WeekReport($State, [string]$Reason) {
    $savedDeadline=$workDeadline
    # Reports may use the reserved final minutes; child compute may not.
    $script:workDeadline=$absoluteDeadline
    try {
        $State.ReportIndex++
        $name='report-{0:D4}.json' -f $State.ReportIndex
        $relative='qualification-results/week/'+$State.RunId
        $destination=Join-Path $checkout $relative
        Ensure-Directory $destination
        $staging=Join-Path $run 'report-staging'
        Ensure-Directory $staging
        $local=Join-Path $staging $name
        $json=(New-WeekReport $State $Reason) | ConvertTo-Json -Depth 12
        if ([Text.Encoding]::UTF8.GetByteCount($json) -gt 1048576) { throw 'Bounded report exceeds 1MiB; raw evidence retained' }
        [IO.File]::WriteAllText($local,$json,[Text.UTF8Encoding]::new($false))
        Copy-Item -LiteralPath $local -Destination (Join-Path $destination $name)
        $files=@([ordered]@{ LocalPath=$local; RelativePath=($relative+'/'+$name); SHA256=(Get-FileHash -LiteralPath $local -Algorithm SHA256).Hash; DeleteAfterUpload=$true; ExpectedBlob=$null; VerifiedBlob=$null })
        $bundleBytes=(Get-Item -LiteralPath $local).Length
        foreach ($chunk in $State.Chunks) {
            if (!$chunk.SummaryPath -or !(Test-Path -LiteralPath $chunk.SummaryPath -PathType Leaf)) { continue }
            $chunkName='chunk-{0:D6}.json' -f $chunk.Index
            $relativeChunk=$relative+'/'+$chunkName
            if (@($State.PendingUploads | ForEach-Object { $_.Entries } | Where-Object RelativePath -eq $relativeChunk).Count) { continue }
            $length=(Get-Item -LiteralPath $chunk.SummaryPath).Length
            if ($length -gt 131072 -or $bundleBytes+$length -gt 4194304) { continue }
            [void](Confirm-WeekOwnedPath $run $chunk.SummaryPath)
            Copy-Item -LiteralPath $chunk.SummaryPath -Destination (Join-Path $destination $chunkName)
            $files += [ordered]@{ LocalPath=$chunk.SummaryPath; RelativePath=$relativeChunk; SHA256=(Get-FileHash -LiteralPath $chunk.SummaryPath -Algorithm SHA256).Hash; DeleteAfterUpload=$true; ExpectedBlob=$null; VerifiedBlob=$null }
            $bundleBytes+=$length
        }
        foreach ($entry in $files) {
            $target=Join-Path $checkout $entry.RelativePath
            $entry.ExpectedBlob=Invoke-WeekGit 'report-blob-before-commit' @('hash-object','--no-filters','--',$target) $checkout
        }
        $previous=Invoke-WeekGit 'report-parent' @('rev-parse','HEAD') $checkout
        Commit-Results $relative $previous ("Record week qualification $($State.RunId) $Reason $($State.ReportIndex)")
        $State.PendingUploads += [ordered]@{ Report=$name; Commit=(Invoke-WeekGit 'report-commit' @('rev-parse','HEAD') $checkout); Verified=$false; CleanupComplete=$false; Entries=$files; VerifiedRemoteCommit=$null; RemovedFiles=@() }
        Save-WeekCheckpoint $State
        Complete-WeekUploads $State
    } catch {
        $State.UploadErrors += [ordered]@{ Utc=[DateTime]::UtcNow.ToString('O'); ReportKind=$Reason; Failure=$_.Exception.Message }
        Save-WeekCheckpoint $State
        Write-Warning "Daily/final results remain local for retry: $($_.Exception.Message)"
    } finally { $script:workDeadline=$savedDeadline }
}

function Set-WeekResultsBytePreservation($State) {
    # Local info attributes outrank tracked attributes. Only this owned results path
    # disables text/filter transformations, so CRLF and LF summaries upload verbatim.
    $attributes=Invoke-WeekGit 'results-attributes-path' @('rev-parse','--git-path','info/attributes') $checkout
    if (![IO.Path]::IsPathRooted($attributes)) { $attributes=Join-Path $checkout $attributes }
    Ensure-Directory (Split-Path -Parent $attributes)
    $rule='qualification-results/week/'+$State.RunId+'/** -text -filter -ident -working-tree-encoding'
    $existing=if (Test-Path -LiteralPath $attributes -PathType Leaf) { [IO.File]::ReadAllText($attributes) } else { '' }
    if (!$existing.Contains($rule)) { [IO.File]::AppendAllText($attributes,"`n"+$rule+"`n",[Text.UTF8Encoding]::new($false)) }
}

try {
    $lock=[IO.FileStream]::new((Join-Path $run '.week-run.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    if ($ResumeDirectory) {
        $checkpoint=Join-Path $run 'checkpoint.json'
        try { $state=Get-Content -LiteralPath $checkpoint -Raw | ConvertFrom-Json -AsHashtable }
        catch { $state=Get-Content -LiteralPath (Join-Path $run 'checkpoint.previous.json') -Raw | ConvertFrom-Json -AsHashtable }
        Confirm-WeekResumeState $state $run $Repository
        if ((Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash -cne $state.BootstrapSHA256 -or (Get-FileHash -LiteralPath $helperPath -Algorithm SHA256).Hash -cne $state.HelpersSHA256) { throw 'Resume requires the original pinned wrapper and helper copies under this run/bootstrap' }
        $RunId=$state.RunId; $SourceRef=$state.SourceRef; $CheckoutRoot=$state.CheckoutRoot
        $absoluteDeadline=[DateTime]::Parse($state.DeadlineUtc).ToUniversalTime(); $workDeadline=[DateTime]::Parse($state.WorkDeadlineUtc).ToUniversalTime()
        foreach ($chunk in $state.Chunks) { if ($chunk.Status -eq 'Running') { $chunk.Status='Interrupted'; $chunk.Failure='Orchestrator restarted before observing completion' } }
        $state.Status='Running'
    } else {
        $state=[ordered]@{ Kind='weekQualification'; SchemaVersion=1; RunId=$RunId; RunDirectory=$run; CheckoutRoot=$CheckoutRoot; ResultsCheckout=$plan.Checkout;
            Repository=$Repository; SourceRef=$SourceRef; SourceRevision=$null; FollowSource=$plan.FollowSource; ResultsBranch=$plan.ResultsBranch;
            Status='Running'; StartedUtc=$plan.StartedUtc; UpdatedUtc=$null; DeadlineUtc=$plan.DeadlineUtc; WorkDeadlineUtc=$plan.WorkDeadlineUtc;
            Hours=$Hours; ReportIntervalHours=$ReportIntervalHours; ChunkSeconds=$ChunkSeconds; InitialSeed=$Seed;
            NextReportUtc=$started.AddHours($ReportIntervalHours).ToString('O'); NextChunkIndex=0; Chunks=@(); DayIndex=0; SourceUpdates=@();
            SourceUpdateFailures=0; PreflightInconclusive=0; Failure=$null; ReportIndex=0; PendingUploads=@(); UploadErrors=@(); KeepResults=$KeepResults.IsPresent;
            BootstrapRevision=$null; BootstrapSHA256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;
            HelpersSHA256=(Get-FileHash -LiteralPath $helperPath -Algorithm SHA256).Hash; SourceManifestSHA256=$null; HarnessSHA256=$null; ActiveSource=$null }
        [IO.File]::WriteAllText((Join-Path $run '.week-qualification-owner.json'),(@{ RunId=$RunId; Root=[IO.Path]::GetFullPath($run) } | ConvertTo-Json))
        $bootstrap=Join-Path $run 'bootstrap'
        Ensure-Directory $bootstrap
        Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $bootstrap 'Invoke-WeekQualification.ps1')
        Copy-Item -LiteralPath $helperPath -Destination (Join-Path $bootstrap 'Invoke-WorkstationQualification.ps1')
    }
    $git=@(Portable-Tool 'git-for-windows/git' '^MinGit-[0-9.]+-64-bit\.zip$' 'git.exe')[0]
    $gh=@(Portable-Tool 'cli/cli' '^gh_[0-9.]+_windows_amd64\.zip$' 'gh.exe')[0]
    $env:PATH=(Split-Path -Parent $git)+';'+(Split-Path -Parent $gh)+';'+$env:PATH
    $env:GIT_TERMINAL_PROMPT='0'; $env:GH_PROMPT_DISABLED='1'; $env:GCM_INTERACTIVE='never'
    $env:GIT_AUTHOR_NAME='jeffadickson'; $env:GIT_COMMITTER_NAME='jeffadickson'
    $env:GIT_AUTHOR_EMAIL='88860296+jeffadickson@users.noreply.github.com'; $env:GIT_COMMITTER_EMAIL=$env:GIT_AUTHOR_EMAIL
    Ensure-Directory $CheckoutRoot
    $checkout=$state.ResultsCheckout
    if ($ResumeDirectory) {
        if (!(Test-Path -LiteralPath $checkout -PathType Container)) { throw 'Resume results checkout is missing' }
        $branch=Invoke-WeekGit 'resume-results-branch' @('branch','--show-current') $checkout
        if ($branch -cne $state.ResultsBranch) { throw 'Resume results branch changed; preserved' }
        $status=Invoke-WeekGit 'resume-results-status' @('status','--porcelain=v1','--untracked-files=normal') $checkout
        if ($status) { throw 'Resume results checkout has unknown local changes; preserved' }
        $origin=Invoke-WeekGit 'resume-origin' @('remote','get-url','origin') $checkout
        if ($origin.TrimEnd('/') -notin @("https://github.com/$Repository.git","https://github.com/$Repository")) { throw 'Resume origin does not match the configured repository' }
        if ($state.ActiveSource) { Test-WeekSourceClean $state.ActiveSource.WorkingDirectory }
    } else {
        $canPush=Read-Step 'week-repository-permission' $gh @('api',"repos/$Repository",'--jq','.permissions.push') $run
        if ($canPush -ne 'true') { throw 'Authenticated account cannot push the bounded results branch' }
        if (Test-Path -LiteralPath $checkout) { throw 'Fresh results checkout already exists' }
        $shellQuote="'"+$gh.Replace('\','/').Replace("'","'\''")+"'"
        $helper='!'+$shellQuote+' auth git-credential'
        [void](Read-Step 'clone-week-results' $git @('clone','--no-checkout','-c','core.autocrlf=false','-c','credential.https://github.com.helper=','-c',('credential.https://github.com.helper='+$helper),'--',"https://github.com/$Repository.git",$checkout) $CheckoutRoot 600)
        [void](Invoke-WeekGit 'initial-week-fetch' @('fetch','--','origin',$SourceRef) $checkout 600)
        $sourceSha=Invoke-WeekGit 'initial-week-sha' @('rev-parse','FETCH_HEAD^{commit}') $checkout
        if ($sourceSha -notmatch '^[a-f0-9]{40}$') { throw 'Initial source is not a commit' }
        [void](Invoke-WeekGit 'initial-week-results-branch' @('checkout','-b',$state.ResultsBranch,$sourceSha) $checkout)
        $bootstrapDirectory=Split-Path -Parent $PSScriptRoot
        $state.BootstrapRevision=Invoke-WeekGit 'week-bootstrap-revision' @('rev-parse','HEAD') $bootstrapDirectory
        Get-PSDrive -PSProvider FileSystem | Select-Object Name,Root,Used,Free | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'disk-start.json')
    }
    Set-WeekResultsBytePreservation $state
    Save-WeekCheckpoint $state
    if (!$state.ActiveSource) {
        $initial=Prepare-WeekSource $state
        if (!$initial.Success) { throw "Initial source qualification failed: $($initial.Failure)" }
    }
    if (@($state.PendingUploads | Where-Object { !$_.Verified -or !$_.CleanupComplete }).Count) {
        try { Complete-WeekUploads $state } catch { Write-Warning 'Previous unsubmitted reports remain pending; later reporting will retry' }
    }
    Invoke-WeekLoop $state { [DateTime]::UtcNow } { param($s,$c) Invoke-WeekChunk $s $c } { param($s) Prepare-WeekSource $s } { param($s,$r) Publish-WeekReport $s $r } { param($s) Save-WeekCheckpoint $s }
} catch {
    if ($state) {
        $state.Failure=$_.Exception.Message; $state.Status='Failed'; Save-WeekCheckpoint $state
        if ($checkout -and (Test-Path -LiteralPath $checkout -PathType Container)) { Publish-WeekReport $state 'FinalFailure' }
    }
    throw
} finally {
    if ($state) { Save-WeekCheckpoint $state }
    if ($lock) { $lock.Dispose() }
    foreach ($key in $savedEnvironment.Keys) {
        if ($null -eq $savedEnvironment[$key]) { Remove-Item -LiteralPath ('Env:'+ $key) -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($key,$savedEnvironment[$key],'Process') }
    }
}
if ($state.Status -eq 'Failed') { throw 'Week qualification failed; bounded report and raw evidence were retained/submitted as recorded' }
if (@($state.PendingUploads | Where-Object { !$_.Verified }).Count) { throw 'Qualification ended with unsubmitted reports; preserve and resume this run to retry' }
Write-Output "Week qualification $($state.Status); reports: $($state.ResultsBranch); local evidence: $run"
