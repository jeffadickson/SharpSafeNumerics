# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

param([Parameter(Mandatory)] [string] $ArtifactDirectory, [string] $Distribution = 'Ubuntu-24.04')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) { throw 'Verified artifact directory missing' }
$source = Join-Path $PSScriptRoot 'MpfrFunctionReference.c'
if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw 'MPFR generator source missing' }
$version = (& wsl -d $Distribution --exec pkg-config --modversion mpfr).Trim()
if ($LASTEXITCODE -ne 0 -or $version -ne '4.2.1') { throw "This evidence is pinned to installed MPFR4.2.1; observed $version" }
$linuxSource = (& wsl -d $Distribution --exec wslpath -a $source).Trim()
if ($LASTEXITCODE -ne 0) { throw 'wslpath failed' }
$hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$scratch = '/tmp/sharpsafe-mpfr-functions-' + $hash.Substring(0,12).ToLowerInvariant()
& wsl -d $Distribution --exec test -d /tmp
if ($LASTEXITCODE -ne 0) { throw 'Linux /tmp missing' }
& wsl -d $Distribution --exec mkdir -p $scratch
if ($LASTEXITCODE -ne 0) { throw 'Linux scratch creation failed' }
$exe = "$scratch/reference"
& wsl -d $Distribution --exec test -f $exe
# The source-hash directory is immutable for this fixed compiler/library profile.
# Build once, then reuse the native executable rather than rebuilding per fixture.
if ($LASTEXITCODE -ne 0) {
    & wsl -d $Distribution --exec gcc -std=c11 -O3 -ftree-loop-vectorize -ftree-slp-vectorize -Wall -Wextra $linuxSource -lmpfr -lgmp -o $exe
    if ($LASTEXITCODE -ne 0) { throw 'MPFR reference compilation failed' }
}
foreach ($kind in @('bounds', 'internal')) {
    foreach ($precision in @(1024,2048)) {
        $destination = Join-Path $ArtifactDirectory "mpfr-$kind-$precision.tsv"
        & wsl -d $Distribution --exec $exe $precision $kind > $destination
        if ($LASTEXITCODE -ne 0) { throw "MPFR generation failed: $kind/$precision" }
    }
    $first = (Get-FileHash -LiteralPath (Join-Path $ArtifactDirectory "mpfr-$kind-1024.tsv")).Hash
    $second = (Get-FileHash -LiteralPath (Join-Path $ArtifactDirectory "mpfr-$kind-2048.tsv")).Hash
    if ($first -ne $second) { throw "Precision stability check failed for $kind" }
    Write-Output "$kind SHA256=$second"
}
& wsl -d $Distribution --exec gcc --version > (Join-Path $ArtifactDirectory 'mpfr-gcc-version.txt')
& wsl -d $Distribution --exec sha256sum /lib/x86_64-linux-gnu/libmpfr.so.6 > (Join-Path $ArtifactDirectory 'mpfr-library-sha256.txt')
Write-Output "Generator SHA256=$hash; native reference=$exe; MPFR=$version"
