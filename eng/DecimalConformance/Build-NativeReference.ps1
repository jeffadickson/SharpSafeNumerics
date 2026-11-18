# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

param([Parameter(Mandatory)] [string] $ArtifactDirectory, [string] $Distribution = 'Ubuntu-24.04')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) { throw 'Verified artifact directory missing' }
$sources = Join-Path $ArtifactDirectory 'decNumber\decNumber'
if (!(Test-Path -LiteralPath $sources -PathType Container)) { throw 'Run Get-Corpus.ps1 first' }
$adapter = Join-Path $PSScriptRoot 'NativeReference.c'
if (!(Test-Path -LiteralPath $adapter -PathType Leaf)) { throw 'Native adapter missing' }
function Convert-ToWslPath([string] $Path) {
    $resolved = & wsl -d $Distribution --exec wslpath -a $Path
    if ($LASTEXITCODE -ne 0) { throw 'wslpath failed' }
    return $resolved.Trim()
}
$linuxSources = Convert-ToWslPath $sources
$linuxAdapter = Convert-ToWslPath $adapter
# Native executable/intermediates stay on Linux's filesystem, not exFAT.
$scratch = "/tmp/sharpsafe-decnumber-$PID"
& wsl -d $Distribution --exec test -d /tmp
if ($LASTEXITCODE -ne 0) { throw 'Linux /tmp missing' }
& wsl -d $Distribution --exec mkdir -p $scratch
if ($LASTEXITCODE -ne 0) { throw 'Linux scratch creation failed' }
$output = "$scratch/decimal-reference"
$compilerArguments = @('-std=c11', '-O3', '-ftree-loop-vectorize', '-ftree-slp-vectorize', '-Wall', '-Wextra', '-DDECNUMDIGITS=2000', "-I$linuxSources", $linuxAdapter, "$linuxSources/decNumber.c", "$linuxSources/decContext.c", '-o', $output)
& wsl -d $Distribution --exec gcc --version
& wsl -d $Distribution --exec gcc @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Native reference compilation failed' }
Write-Output "Native reference: $output (WSL $Distribution); flags: $($compilerArguments -join ' ')"
