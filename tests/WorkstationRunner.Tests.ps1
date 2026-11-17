BeforeAll {
    $repository=Split-Path -Parent $PSScriptRoot
    if(-not(Test-Path -LiteralPath $repository -PathType Container)){throw 'Missing repository'}
    $script:runner=Join-Path $repository 'eng/Invoke-WorkstationQualification.ps1'
    $tokens=$null;$parseErrors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile($script:runner,[ref]$tokens,[ref]$parseErrors)
    if($parseErrors.Count){throw ($parseErrors.Message -join '; ')}
    foreach($name in @('Ensure-Directory','Initialize-ProcessJob','Start-Step','Complete-Step','Read-Step','Commit-Results','Network-Seconds')){
        $definition=$ast.FindAll({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)|Where-Object Name -eq $name
        if(-not $definition){throw "Missing process helper $name"}
        Invoke-Expression $definition.Extent.Text
    }
    $script:pwsh=(Get-Process -Id $PID).Path
}
Describe 'Bounded workstation runner' {
BeforeEach {
    $script:run=Join-Path $TestDrive 'logs with spaces'
    Ensure-Directory $script:run
    $script:workDeadline=[DateTime]::UtcNow.AddMinutes(1)
    $script:records=[Collections.Generic.List[object]]::new()
    $script:counter=0
}
    It 'plans the requested hour limit without creating a checkout' {
        $json=& $script:runner -PlanOnly -Hours 3 -IssueNumber 17 -CheckoutRoot (Join-Path $TestDrive 'github') -ArtifactRoot (Join-Path $TestDrive 'artifacts')
        $plan=$json|ConvertFrom-Json
        $plan.Hours|Should -Be 3
        $plan.ResultsBranch|Should -Match '^qualification/issue-17/'
        $plan.IssueNumber|Should -Be 17
        Test-Path -LiteralPath $plan.Checkout|Should -BeFalse
    }
    It 'preserves literal arguments and both output streams with the actual exit code' {
        $context=Start-Step 'streams' $script:pwsh @('-NoProfile','-Command','[Console]::Out.WriteLine("literal path with spaces"); [Console]::Error.WriteLine("failure details"); exit 7') $TestDrive 20
        (Complete-Step $context)|Should -Be 7
        (Get-Content -LiteralPath $context.Stdout -Raw)|Should -Match 'literal path with spaces'
        (Get-Content -LiteralPath $context.Stderr -Raw)|Should -Match 'failure details'
        $script:records[0].Status|Should -Be 'Failed'
    }
    It 'reports and terminates a task-owned command at its declared timeout' {
        $context=Start-Step 'timeout' $script:pwsh @('-NoProfile','-Command','Start-Sleep -Seconds 30') $TestDrive 1
        [void](Complete-Step $context)
        $script:records[0].Status|Should -Be 'TimedOut'
        $script:records[0].ElapsedSeconds|Should -BeLessThan 10
    }
    It 'does not start work after the verification deadline' {
        $script:workDeadline=[DateTime]::UtcNow.AddSeconds(-1)
        $context=Start-Step 'expired' $script:pwsh @('-NoProfile','-Command','exit 0') $TestDrive 10
        $context|Should -BeNullOrEmpty
        $script:records[0].Status|Should -Be 'SkippedBudget'
    }
    It 'preserves literal empty, quoted, shell metacharacter and trailing backslash arguments' {
        $sample=@('','a "quoted" thing','C:\a b\','$(never);&|','two\\\"q')
        $scriptFile=Join-Path $TestDrive 'print arguments.ps1'
        $runnerSource='[Console]::Write([Text.Json.JsonSerializer]::Serialize([string[]]$args, [Text.Json.JsonSerializerOptions]::Default))'
        Set-Content -LiteralPath $scriptFile -Value $runnerSource
        $text=Read-Step 'argument-test' $script:pwsh (@('-NoProfile','-File',$scriptFile)+$sample) $TestDrive 15
        $actual=@(ConvertFrom-Json $text)
        $actual.Count|Should -Be $sample.Count
        for($i=0;$i -lt $sample.Count;$i++){$actual[$i]|Should -BeExactly $sample[$i]}
    }
    It 'cleans up a descendant after its parent exits, without waiting on inherited handles' {
        $program='$p=Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList @("-NoProfile","-Command","sleep 40") -NoNewWindow -PassThru; [Console]::Write($p.Id)'
        $text=Read-Step 'descendant' $script:pwsh @('-NoProfile','-Command',$program) $TestDrive 8
        $child=Get-Process -Id ([int]$text) -ErrorAction SilentlyContinue
        if($child){$child.WaitForExit(2500)|Should -BeTrue}
        $script:records[-1].ElapsedSeconds|Should -BeLessThan 7
    }
    It 'can cancel active work immediately following a launch failure' {
        $live=Start-Step 'still-running' $script:pwsh @('-NoProfile','-Command','Start-Sleep 40') $TestDrive 60
        try{ {Start-Step 'missing' (Join-Path $TestDrive 'missing.exe') @() $TestDrive 10} | Should -Throw }
        finally{[void](Complete-Step $live -Cancel)}
        $script:records[0].Status|Should -Be 'LaunchFailed'
        $script:records[-1].Status|Should -Be 'Cancelled'
        $script:records[-1].ElapsedSeconds|Should -BeLessThan 5
    }
    It 'refuses downloads as soon as the five-minute publication reserve starts' {
        $script:absoluteDeadline=[DateTime]::UtcNow.AddMinutes(4)
        {Network-Seconds}|Should -Throw '*deadline expired*'
    }
    It 'commits reports while preserving an unrelated staged modification' {
        $script:git=(Get-Command git.exe).Source
        $script:checkout=Join-Path $TestDrive 'private test checkout'
        Ensure-Directory $script:checkout
        $identityKeys=@('GIT_AUTHOR_NAME','GIT_AUTHOR_EMAIL','GIT_COMMITTER_NAME','GIT_COMMITTER_EMAIL')
        $saved=@{}
        try{
            foreach($key in $identityKeys){$saved[$key]=[Environment]::GetEnvironmentVariable($key);$value=if($key.EndsWith('EMAIL')){'fixture@example.invalid'}else{'Fixture'};[Environment]::SetEnvironmentVariable($key,$value)}
            [void](Read-Step 'init' $script:git @('init') $script:checkout)
            [void](Read-Step 'base' $script:git @('commit','--allow-empty','-m','base') $script:checkout)
            $base=Read-Step 'revision' $script:git @('rev-parse','HEAD') $script:checkout
            Set-Content -LiteralPath (Join-Path $script:checkout 'unrelated.txt') -Value staged
            [void](Read-Step 'stage-unrelated' $script:git @('add','unrelated.txt') $script:checkout)
            $relative='qualification-results/workstation/test';$result=Join-Path $script:checkout $relative
            Ensure-Directory $result
            Set-Content -LiteralPath (Join-Path $result 'summary.json') -Value '{}'
            Commit-Results $relative $base 'fixture report'
            (Read-Step 'still-staged' $script:git @('diff','--cached','--name-only') $script:checkout)|Should -BeExactly 'unrelated.txt'
        }finally{foreach($key in $identityKeys){[Environment]::SetEnvironmentVariable($key,$saved[$key])}}
    }
}
