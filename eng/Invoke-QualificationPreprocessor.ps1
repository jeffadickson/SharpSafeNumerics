# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

# Builds the optional native evidence reader incrementally and preserves its exit status.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$InputPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$DetailsPath,
    [string]$ArtifactDirectory,
    [ValidateRange(256, 2147483647)][int]$MaxBytes = 8192,
    [ValidateRange(0, 2147483647)][int]$MaxFindings = 12
)
$ErrorActionPreference = 'Stop'
foreach ($inputItem in $InputPath) {
    if (-not (Test-Path -LiteralPath $inputItem)) { throw "Input missing: $inputItem" }
}
foreach ($destination in @($OutputPath, $DetailsPath)) {
    if (-not $destination) { continue }
    $full = [IO.Path]::GetFullPath($destination)
    if (-not (Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($full)) -PathType Container)) { throw "Output parent missing: $full" }
    if (Test-Path -LiteralPath $full) { throw "Refusing to overwrite: $full" }
}
$manifest = Join-Path $PSScriptRoot 'qualification-preprocessor/Cargo.toml'
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'Preprocessor manifest missing' }
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) { throw 'Install Rust with rustup before running this optional tool.' }
if (-not $ArtifactDirectory) {
    $root = $env:CODEX_ARTIFACT_ROOT
    if (-not $root) { $root = 'E:\Artifacts' }
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Specify -ArtifactDirectory on this machine.' }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $checkoutKey = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($PSScriptRoot)))).Replace('-', '').Substring(0, 12) }
    finally { $sha.Dispose() }
    $platform = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { "windows-$env:PROCESSOR_ARCHITECTURE" } else { "unix-$([IntPtr]::Size * 8)" }
    $ArtifactDirectory = Join-Path $root "SharpSafeNumerics/qualification-preprocessor/$checkoutKey/$platform"
}
$ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
if (-not (Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) {
    $ancestor = [IO.Path]::GetDirectoryName($ArtifactDirectory)
    while ($ancestor -and -not (Test-Path -LiteralPath $ancestor -PathType Container)) { $ancestor = [IO.Path]::GetDirectoryName($ancestor) }
    if (-not $ancestor) { throw 'Artifact directory has no existing parent' }
    New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null
}
$target = Join-Path $ArtifactDirectory 'target'
& cargo build --release --locked --manifest-path $manifest --target-dir $target
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$name = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { 'qualcompact.exe' } else { 'qualcompact' }
$executable = Join-Path $target "release/$name"
$arguments = @($InputPath) + @('--output', $OutputPath, '--max-bytes', "$MaxBytes", '--max-findings', "$MaxFindings")
if ($DetailsPath) { $arguments += @('--details', $DetailsPath) }
& $executable @arguments
# Exit zero means evidence was processed; read the report for qualification verdicts.
exit $LASTEXITCODE
