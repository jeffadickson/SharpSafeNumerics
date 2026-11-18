Describe 'Pinned SharpUnits integration boundaries' {
    BeforeAll {
        $script:helper = Join-Path (Split-Path -Parent $PSScriptRoot) 'eng/VerifySharpUnitsIntegration.ps1'
        if (-not (Test-Path -LiteralPath $TestDrive -PathType Container)) { throw 'Missing Pester test directory' }
    }
    It 'rejects missing artifact parents' {
        { & $script:helper -ArtifactDirectory (Join-Path $TestDrive 'missing/result') } | Should -Throw '*Missing artifact parent*'
    }
    It 'preserves existing artifact files' {
        $directory = Join-Path $TestDrive 'existing'
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) { New-Item -ItemType Directory -Path $directory | Out-Null }
        $sentinel = Join-Path $directory 'sentinel.txt'
        Set-Content -LiteralPath $sentinel -Value 'preserve'
        { & $script:helper -ArtifactDirectory $directory } | Should -Throw '*fresh integration*'
        (Get-Content -LiteralPath $sentinel -Raw).Trim() | Should -Be 'preserve'
    }
    It 'rejects a missing supplied checkout without creating output' {
        $output = Join-Path $TestDrive 'not-created'
        { & $script:helper -ArtifactDirectory $output -SharpUnitsSource (Join-Path $TestDrive 'missing-source') } | Should -Throw '*Missing supplied SharpUnits*'
        Test-Path -LiteralPath $output | Should -BeFalse
    }
    It 'rejects a nested clone before leaking consumer build policy upstream' {
        $repo = Split-Path -Parent $PSScriptRoot
        $parent = Join-Path $repo 'artifacts'
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent | Out-Null }
        $output = Join-Path $parent ('source-boundary-' + [guid]::NewGuid().ToString('N'))
        { & $script:helper -ArtifactDirectory $output } | Should -Throw '*outside the numeric checkout*'
        Test-Path -LiteralPath $output | Should -BeFalse
    }
    It 'rejects an unpinned checkout without changing it' {
        $source = Join-Path $TestDrive 'unpinned-source'
        if (-not (Test-Path -LiteralPath $source -PathType Container)) { New-Item -ItemType Directory -Path $source | Out-Null }
        & git init --quiet $source
        $LASTEXITCODE | Should -Be 0
        $safe = $source.Replace('\','/')
        { & $script:helper -ArtifactDirectory (Join-Path $TestDrive 'unpinned-result') -SharpUnitsSource $source 2>$null } | Should -Throw '*pinned integration revision*'
        & git -c "safe.directory=$safe" -C $source status --porcelain | Should -BeNullOrEmpty
    }
}
