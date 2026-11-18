# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

param([string]$FullRunArtifactDirectory = '', [string]$FullRunRuntimeDirectory = '')

BeforeAll {
    $script:qualifier = Join-Path (Split-Path -Parent $PSScriptRoot) 'eng/Invoke-X86Qualification.ps1'
    if (!(Test-Path -LiteralPath $script:qualifier -PathType Leaf)) { throw 'Missing qualification script' }
    $script:fullArtifact = $FullRunArtifactDirectory
    $script:fullRuntime = $FullRunRuntimeDirectory
}

Describe 'Isolated x86 qualification preflight' -Skip:(-not $IsWindows) {
    It 'rejects a missing artifact parent without creating it' {
        $destination = Join-Path $TestDrive 'missing/run'
        { & $script:qualifier -ArtifactDirectory $destination } | Should -Throw
        Test-Path -LiteralPath (Split-Path -Parent $destination) | Should -BeFalse
    }

    It 'preserves an existing artifact directory and its user content' {
        $destination = Join-Path $TestDrive 'existing'
        New-Item -ItemType Directory -Path $destination | Out-Null
        $sentinel = Join-Path $destination 'preserve.txt'
        Set-Content -LiteralPath $sentinel -Value 'preserve'
        { & $script:qualifier -ArtifactDirectory $destination } | Should -Throw
        (Get-Content -LiteralPath $sentinel -Raw).Trim() | Should -Be 'preserve'
    }

    It 'rejects an invalid supplied runtime before creating outputs and preserves the environment' {
        $destination = Join-Path $TestDrive 'invalid-runtime-run'
        $before = $env:DOTNET_ROOT_X86
        { & $script:qualifier -ArtifactDirectory $destination -RuntimeDirectory (Join-Path $TestDrive 'missing-runtime') } | Should -Throw
        Test-Path -LiteralPath $destination | Should -BeFalse
        $env:DOTNET_ROOT_X86 | Should -Be $before
    }

    It 'rejects a supplied x64 host instead of falling back silently' {
        $destination = Join-Path $TestDrive 'wrong-runtime-run'
        $sdkDirectory = Split-Path -Parent (Get-Command dotnet -CommandType Application).Source
        { & $script:qualifier -ArtifactDirectory $destination -RuntimeDirectory $sdkDirectory } | Should -Throw
        Test-Path -LiteralPath $destination | Should -BeFalse
    }

    It 'rejects missing archive inputs and ambiguous provisioning options' {
        $destination = Join-Path $TestDrive 'invalid-archive-run'
        { & $script:qualifier -ArtifactDirectory $destination -RuntimeArchive (Join-Path $TestDrive 'missing.zip') } | Should -Throw
        Test-Path -LiteralPath $destination | Should -BeFalse
        { & $script:qualifier -ArtifactDirectory $destination -RuntimeArchive 'anything.zip' -RuntimeDirectory 'anything' } | Should -Throw
        Test-Path -LiteralPath $destination | Should -BeFalse
    }

    It 'rejects a corrupted cached archive without extracting or modifying it' {
        $destination = Join-Path $TestDrive 'corrupt-archive-run'
        $archive = Join-Path $TestDrive 'corrupt.zip'
        Set-Content -LiteralPath $archive -Value 'preserve invalid archive'
        $before = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
        Mock Invoke-RestMethod { @{ releases = @(@{ 'release-version' = '11.0.0'; runtime = @{ files = @(@{ rid = 'win-x86'; name = 'dotnet-runtime-win-x86.zip'; hash = ('0' * 128); url = 'https://builds.dotnet.microsoft.com/dotnet/Runtime/11.0.0/dotnet-runtime-11.0.0-win-x86.zip' }) } }) } }
        { & $script:qualifier -ArtifactDirectory $destination -RuntimeArchive $archive } | Should -Throw '*SHA512*'
        (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash | Should -Be $before
        Test-Path -LiteralPath (Join-Path $destination 'runtime') | Should -BeFalse
        (Get-Content -LiteralPath (Join-Path $destination 'summary.json') -Raw | ConvertFrom-Json).Passed | Should -BeFalse
    }
}

# Expensive integration is explicitly opt-in; ordinary aggregate discovery remains fast.
if ($FullRunArtifactDirectory) {
    Describe 'Full managed x86 qualification' -Skip:(-not $IsWindows) {
        It 'runs all real suites and restores the caller process environment' {
            $before = $env:DOTNET_ROOT_X86
            $arguments = @{ ArtifactDirectory = $script:fullArtifact }
            if ($script:fullRuntime) { $arguments.RuntimeDirectory = $script:fullRuntime }
            & $script:qualifier @arguments
            $env:DOTNET_ROOT_X86 | Should -Be $before
            $summary = Get-Content -LiteralPath (Join-Path $script:fullArtifact 'summary.json') -Raw | ConvertFrom-Json
            $summary.Passed | Should -BeTrue
            $summary.TotalPassed | Should -BeGreaterThan 0
            @($summary.Suites).Count | Should -Be 4
            @($summary.Suites | Where-Object { !$_.HostX86Confirmed }).Count | Should -Be 0
        }
    }
}
