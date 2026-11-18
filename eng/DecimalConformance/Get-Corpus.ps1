# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

param([string] $ArtifactDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!$ArtifactDirectory) {
    $root = if ($env:CODEX_ARTIFACT_ROOT) { $env:CODEX_ARTIFACT_ROOT } else { 'E:\Artifacts' }
    if (!(Test-Path -LiteralPath $root -PathType Container)) { throw "Artifact root missing: $root" }
    $ArtifactDirectory = Join-Path $root 'SharpSafeNumerics\windows-x64\ieee-decimal-20261118\conformance'
}
if (!(Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) {
    $parent = Split-Path -Parent $ArtifactDirectory
    if (!(Test-Path -LiteralPath $parent -PathType Container)) { throw "Create a verified run parent first: $parent" }
    New-Item -ItemType Directory -Path $ArtifactDirectory | Out-Null
}
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'provenance.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest.artifacts | Where-Object { $_.PSObject.Properties.Name -contains 'sha256' }) {
    $filename = [Uri]::new($entry.url).Segments[-1]
    $archive = Join-Path $ArtifactDirectory $filename
    # An existing download is verified, never overwritten blindly.
    if (!(Test-Path -LiteralPath $archive -PathType Leaf)) { Invoke-WebRequest $entry.url -OutFile $archive }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Archive hash mismatch: $archive" }
}
$license = Join-Path $ArtifactDirectory 'ICU-license.html'
if (!(Test-Path -LiteralPath $license -PathType Leaf)) { Invoke-WebRequest 'https://speleotrove.com/decimal/ICU-license.html' -OutFile $license }
foreach ($pair in @(@('dectest.zip', 'corpus'), @('decNumber-icu-368.zip', 'decNumber'))) {
    $destination = Join-Path $ArtifactDirectory $pair[1]
    if (!(Test-Path -LiteralPath $destination -PathType Container)) {
        Expand-Archive -LiteralPath (Join-Path $ArtifactDirectory $pair[0]) -DestinationPath $destination
    }
}
Write-Output "Verified archives and retained original license/notices in $ArtifactDirectory"
