# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

BeforeAll {
    $script:weekRunner=Join-Path (Split-Path -Parent $PSScriptRoot) 'eng/Invoke-WeekQualification.ps1'
    if (!(Test-Path -LiteralPath $script:weekRunner -PathType Leaf)) { throw 'Missing week runner' }
    $tokens=$null; $errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile($script:weekRunner,[ref]$tokens,[ref]$errors)
    if ($errors.Count) { throw ($errors.Message -join '; ') }
    foreach ($name in @('New-WeekPlan','Get-WeekSeed','Get-WeekDecision','Get-WeekFinalStatus','Save-WeekCheckpoint','Confirm-WeekOwnedPath','Remove-WeekSubmittedFiles','Read-WeekChildSummary','Confirm-WeekResumeState','Invoke-WeekLoop','New-WeekReport','Complete-WeekUploads','Set-WeekResultsBytePreservation')) {
        $definition=$ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$false) | Where-Object Name -eq $name
        . ([scriptblock]::Create($definition.Extent.Text))
    }
    function New-TestWeekState {
        return @{ Status='Running'; WorkDeadlineUtc='2026-11-18T00:02:00Z'; NextReportUtc='2026-11-18T00:00:36Z'; ReportIntervalHours=0.01;
            ChunkSeconds=10; InitialSeed=731940; NextChunkIndex=0; Chunks=@(); SourceRevision=('a'*40); FollowSource=$true;
            Failure=$null; SourceUpdateFailures=0; PreflightInconclusive=0 }
    }
    function New-TestOwnedResult([string]$Directory) {
        if (!(Test-Path -LiteralPath $Directory -PathType Container)) { New-Item -ItemType Directory -Path $Directory | Out-Null }
        $owner=@{ RunId='week-test'; Root=[IO.Path]::GetFullPath($Directory) }
        $owner | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Directory '.week-qualification-owner.json')
        $file=Join-Path $Directory 'submitted.json'
        Set-Content -LiteralPath $file -Value 'submitted'
        return @{ LocalPath=$file; DeleteAfterUpload=$true; ExpectedBlob=('b'*40); VerifiedBlob=('b'*40); SHA256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
    }
    function Invoke-WeekGit { param($Name,$Arguments,$Directory,$Limit) throw 'Mock the Git boundary in control-flow tests' }
}

Describe 'Week qualification plans and contracts' {
    It 'plans seven days with daily branch refresh without creating files' {
        $plan=(& $script:weekRunner -PlanOnly -CheckoutRoot (Join-Path $TestDrive 'checkouts') -ArtifactRoot (Join-Path $TestDrive 'artifacts')) | ConvertFrom-Json
        $plan.Hours | Should -Be 168
        $plan.ReportIntervalHours | Should -Be 24
        $plan.SourceRef | Should -Be 'main'
        $plan.FollowSource | Should -BeTrue
        $plan.ResultsBranch | Should -Match '^qualification/issue-1/week-'
        Test-Path -LiteralPath $plan.Artifacts | Should -BeFalse
    }
    It 'accepts explicit redirected Documents roots and pinned mode in Windows PowerShell 5.1' -Skip:(-not $IsWindows) {
        $powershell=Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
        $output=& $powershell -NoProfile -ExecutionPolicy Bypass -File $script:weekRunner -PlanOnly -Hours 0.01 -ReportIntervalHours 0.005 -PinnedSource -CheckoutRoot (Join-Path $TestDrive 'GitHub') -ArtifactRoot (Join-Path $TestDrive 'QualificationArtifacts')
        $LASTEXITCODE | Should -Be 0
        ($output | ConvertFrom-Json).FollowSource | Should -BeFalse
    }
    It 'treats literal source commits as pinned and validates duration and seeds' {
        ((& $script:weekRunner -PlanOnly -SourceRef ('a'*40)) | ConvertFrom-Json).FollowSource | Should -BeFalse
        { & $script:weekRunner -PlanOnly -Hours 0 } | Should -Throw
        { & $script:weekRunner -PlanOnly -Seed 0 } | Should -Throw
        (Get-WeekSeed ([int]::MaxValue) 1) | Should -Be 1
        (Get-WeekSeed 731940 2) | Should -Be 731942
    }
    It 'requires the exact IEEE child schema and source/seed/verdict agreement' {
        $path=Join-Path $TestDrive 'child.json'
        $child=@{ Kind='ieeeDecimalSoak'; SchemaVersion=1; SourceRevision=('a'*40); Seed=7; Status='Passed'; Samples=1; FailedChecks=0; InconclusiveChecks=0; Failure=$null }
        $child | ConvertTo-Json | Set-Content -LiteralPath $path
        (Read-WeekChildSummary $path 0 7 ('a'*40)).Status | Should -Be 'Passed'
        { Read-WeekChildSummary $path 0 8 ('a'*40) } | Should -Throw
        $child.Status='Inconclusive'; $child.InconclusiveChecks=1
        $child | ConvertTo-Json | Set-Content -LiteralPath $path
        { Read-WeekChildSummary $path 0 7 ('a'*40) } | Should -Throw
        (Read-WeekChildSummary $path 3 7 ('a'*40)).Status | Should -Be 'Inconclusive'
        { Read-WeekChildSummary $path 1 7 ('a'*40) } | Should -Throw
    }
}

Describe 'Week scheduling with fake time and process results' {
    BeforeEach { $script:fakeNow=[DateTime]::Parse('2026-11-18T00:00:00Z').ToUniversalTime(); $script:reports=@(); $script:persisted=0 }
    It 'reports daily while running, refreshes only at boundaries, and finishes without cross-source aggregation' {
        $state=New-TestWeekState
        Invoke-WeekLoop $state { $script:fakeNow } { param($s,$c) $script:fakeNow=$script:fakeNow.AddSeconds($c.RequestedSeconds); @{ExitCode=0;Samples=1;Assertions=3} } { param($s) $s.SourceRevision='c'*40; @{Success=$true} } { param($s,$kind) $script:reports+=@{Kind=$kind;Status=$s.Status} } { param($s) $script:persisted++ }
        $state.Status | Should -Be 'Passed'
        @($state.Chunks | Group-Object { $_.SourceRevision }).Count | Should -Be 2
        @($script:reports | Where-Object { $_.Kind -eq 'Daily' -and $_.Status -ne 'Running' }).Count | Should -Be 0
        $script:reports[-1].Kind | Should -Be 'Final'
        $state.NextChunkIndex | Should -Be $state.Chunks.Count
    }
    It 'continues inconclusive chunks but never calls the final result passed' {
        $state=New-TestWeekState; $state.FollowSource=$false
        Invoke-WeekLoop $state { $script:fakeNow } { param($s,$c) $script:fakeNow=$script:fakeNow.AddSeconds($c.RequestedSeconds); @{ExitCode=3} } { @{Success=$true} } { } { }
        $state.Status | Should -Be 'Incomplete'
        $state.Chunks.Count | Should -BeGreaterThan 1
    }
    It 'publishes fatal failures immediately and does not start another seed' {
        $state=New-TestWeekState
        Invoke-WeekLoop $state { $script:fakeNow } { @{ExitCode=1;Failure='counterexample'} } { @{Success=$true} } { param($s,$kind) $script:reports+=$kind } { }
        $state.Status | Should -Be 'Failed'
        $state.Chunks.Count | Should -Be 1
        $script:reports | Should -Contain 'Final'
    }
    It 'retains the working revision when daily refresh fails and marks final incomplete' {
        $state=New-TestWeekState
        Invoke-WeekLoop $state { $script:fakeNow } { param($s,$c) $script:fakeNow=$script:fakeNow.AddSeconds($c.RequestedSeconds); @{ExitCode=0} } { @{Success=$false} } { param($s,$kind) $script:reports+=$kind } { }
        $state.Status | Should -Be 'Incomplete'
        $state.SourceRevision | Should -Be ('a'*40)
        $script:reports | Should -Contain 'SourceUpdateFailed'
    }
    It 'does not qualify empty or interrupted campaigns and reserves report cleanup time' {
        $state=New-TestWeekState
        (Get-WeekFinalStatus $state) | Should -Be 'Incomplete'
        $state.Chunks=@(@{Status='Interrupted'})
        (Get-WeekFinalStatus $state) | Should -Be 'Incomplete'
        (Get-WeekDecision $state ([DateTime]::Parse('2026-11-18T00:02:01Z'))).Action | Should -Be 'Complete'
    }
}

Describe 'Owned submitted result cleanup and recovery' {
    It 'deletes only exact verified files and preserves unrelated raw logs' {
        $root=Join-Path $TestDrive 'owned'
        $entry=New-TestOwnedResult $root
        $raw=Join-Path $root 'raw.log'; Set-Content -LiteralPath $raw -Value 'not uploaded'
        @(Remove-WeekSubmittedFiles $root 'week-test' @($entry) ('c'*40)).Count | Should -Be 1
        Test-Path -LiteralPath $entry.LocalPath | Should -BeFalse
        Test-Path -LiteralPath $raw | Should -BeTrue
    }
    It 'preserves pending, partial, changed, and opt-out results' {
        $root=Join-Path $TestDrive 'pending'
        $entry=New-TestOwnedResult $root
        { Remove-WeekSubmittedFiles $root 'week-test' @($entry) '' } | Should -Throw
        $entry.VerifiedBlob=$null
        { Remove-WeekSubmittedFiles $root 'week-test' @($entry) ('c'*40) } | Should -Throw
        Test-Path -LiteralPath $entry.LocalPath | Should -BeTrue
        $entry.VerifiedBlob=$entry.ExpectedBlob
        Set-Content -LiteralPath $entry.LocalPath -Value 'changed'
        { Remove-WeekSubmittedFiles $root 'week-test' @($entry) ('c'*40) } | Should -Throw
        @(Remove-WeekSubmittedFiles $root 'week-test' @($entry) ('c'*40) -Keep).Count | Should -Be 0
    }
    It 'rejects outside paths and foreign ownership before removing any file' {
        $root=Join-Path $TestDrive 'bounds'; $entry=New-TestOwnedResult $root
        { Confirm-WeekOwnedPath $root (Join-Path $TestDrive 'foreign.json') } | Should -Throw
        { Remove-WeekSubmittedFiles $root 'foreign-id' @($entry) ('c'*40) } | Should -Throw
        Test-Path -LiteralPath $entry.LocalPath | Should -BeTrue
    }
    It 'rejects a junction escape' -Skip:(-not $IsWindows) {
        $root=Join-Path $TestDrive 'junction-root'; [void](New-TestOwnedResult $root)
        $outside=Join-Path $TestDrive 'junction-outside'; New-Item -ItemType Directory -Path $outside | Out-Null
        $link=Join-Path $root 'link'; New-Item -ItemType Junction -Path $link -Target $outside | Out-Null
        try { { Confirm-WeekOwnedPath $root (Join-Path $link 'file.json') } | Should -Throw }
        finally { Remove-Item -LiteralPath $link -Force }
    }
    It 'keeps a previous checkpoint and round-trips exact seed/source progress' {
        $root=Join-Path $TestDrive 'checkpoint'; New-Item -ItemType Directory -Path $root | Out-Null
        $state=@{RunDirectory=$root;UpdatedUtc=$null;NextChunkIndex=1;SourceRevision=('a'*40)}
        Save-WeekCheckpoint $state; $state.NextChunkIndex=2; Save-WeekCheckpoint $state
        (Get-Content -LiteralPath (Join-Path $root 'checkpoint.json') -Raw | ConvertFrom-Json).NextChunkIndex | Should -Be 2
        (Get-Content -LiteralPath (Join-Path $root 'checkpoint.previous.json') -Raw | ConvertFrom-Json).NextChunkIndex | Should -Be 1
    }
    It 'rejects a resumed checkpoint redirected to a foreign checkout' {
        $root=Join-Path $TestDrive 'resume-owned'; [void](New-TestOwnedResult $root)
        $state=@{Kind='weekQualification';SchemaVersion=1;Repository='jeffadickson/SharpSafeNumerics';RunId='week-test';RunDirectory=$root;CheckoutRoot=$TestDrive;ResultsCheckout=(Join-Path $TestDrive 'SharpSafeNumerics-week-test-results');ResultsBranch='qualification/issue-1/week-test'}
        { Confirm-WeekResumeState $state $root 'jeffadickson/SharpSafeNumerics' } | Should -Not -Throw
        $state.ResultsCheckout=Join-Path $TestDrive 'foreign-checkout'
        { Confirm-WeekResumeState $state $root 'jeffadickson/SharpSafeNumerics' } | Should -Throw
    }
    It 'retains a failed push for retry and removes files only after exact remote/blob verification' {
        $root=Join-Path $TestDrive 'upload-retry'; $entry=New-TestOwnedResult $root
        $entry.RelativePath='qualification-results/week/week-test/report-0001.json'; $entry.VerifiedBlob=$null
        $state=@{RunDirectory=$root;RunId='week-test';ResultsBranch='qualification/issue-1/week-test';KeepResults=$false;PendingUploads=@(@{Verified=$false;CleanupComplete=$false;Entries=@($entry);VerifiedRemoteCommit=$null;RemovedFiles=@()})}
        $script:run=$root; $script:checkout=$root; $script:failPush=$true
        Mock Invoke-WeekGit {
            if ($Name -eq 'push-daily-results' -and $script:failPush) { throw 'network unavailable' }
            if ($Name -eq 'results-current-head') { return ('c'*40) }
            if ($Name -eq 'verify-pushed-results') { return ('c'*40)+' refs/heads/qualification/issue-1/week-test' }
            if ($Name -eq 'verify-submitted-blob') { return ('b'*40) }
        }
        { Complete-WeekUploads $state } | Should -Throw '*network unavailable*'
        Test-Path -LiteralPath $entry.LocalPath | Should -BeTrue
        $state.PendingUploads[0].Verified | Should -BeFalse
        $script:failPush=$false
        Complete-WeekUploads $state
        $state.PendingUploads[0].Verified | Should -BeTrue
        Test-Path -LiteralPath $entry.LocalPath | Should -BeFalse
    }
    It 'does not remove any files when one uploaded content hash is wrong' {
        $root=Join-Path $TestDrive 'partial-upload'; $entry=New-TestOwnedResult $root
        $entry.RelativePath='report.json'; $entry.VerifiedBlob=$null
        $state=@{RunDirectory=$root;RunId='week-test';ResultsBranch='qualification/issue-1/week-test';KeepResults=$false;PendingUploads=@(@{Verified=$false;CleanupComplete=$false;Entries=@($entry)})}
        $script:run=$root; $script:checkout=$root
        Mock Invoke-WeekGit {
            if ($Name -eq 'results-current-head') { return ('c'*40) }
            if ($Name -eq 'verify-pushed-results') { return ('c'*40)+' refs/heads/qualification/issue-1/week-test' }
            if ($Name -eq 'verify-submitted-blob') { return ('d'*40) }
        }
        { Complete-WeekUploads $state } | Should -Throw '*content*'
        Test-Path -LiteralPath $entry.LocalPath | Should -BeTrue
    }
    It 'preserves CRLF JSON bytes despite core.autocrlf when staging results' {
        $git=Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if (!$git) { Set-ItResult -Skipped -Because 'Git is unavailable'; return }
        $root=Join-Path $TestDrive 'crlf-repo'; New-Item -ItemType Directory -Path $root | Out-Null
        & $git.Source -C $root init --quiet
        & $git.Source -C $root config core.autocrlf true
        $script:checkout=$root
        Mock Invoke-WeekGit { '.git/info/attributes' }
        function Ensure-Directory([string]$Path) { if (!(Test-Path -LiteralPath $Path -PathType Container)) { New-Item -ItemType Directory -Path $Path | Out-Null } }
        Set-WeekResultsBytePreservation @{RunId='week-test'}
        $directory=Join-Path $root 'qualification-results/week/week-test'; New-Item -ItemType Directory -Path $directory | Out-Null
        $file=Join-Path $directory 'report.json'
        [IO.File]::WriteAllText($file,"{`r`n  `"Kind`": `"weekQualification`"`r`n}`r`n",[Text.UTF8Encoding]::new($false))
        $raw=& $git.Source -C $root hash-object --no-filters -- $file
        & $git.Source -C $root add -- $file
        $stored=& $git.Source -C $root rev-parse ':qualification-results/week/week-test/report.json'
        $stored | Should -BeExactly $raw
    }
}
