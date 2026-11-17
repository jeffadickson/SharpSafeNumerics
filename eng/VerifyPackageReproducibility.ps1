param([Parameter(Mandatory)][string]$ArtifactDirectory, [string]$Configuration = 'Release', [switch]$RebuildBetweenPasses, [string]$BuildArtifactsPath)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing checkout' }
$ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
if ($BuildArtifactsPath) {
    $BuildArtifactsPath = [IO.Path]::GetFullPath($BuildArtifactsPath)
    if (-not (Test-Path -LiteralPath $BuildArtifactsPath -PathType Container)) { throw 'Missing compiled build-artifact directory' }
}
$parent = Split-Path -Parent $ArtifactDirectory
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'Missing artifact parent' }
if (Test-Path -LiteralPath $ArtifactDirectory) { throw 'Use a fresh reproducibility directory to prevent stale-package comparisons' }
New-Item -ItemType Directory -Path $ArtifactDirectory | Out-Null
$hashes = @{}
Push-Location -LiteralPath $repo
try {
    foreach ($pass in @('first', 'second')) {
        if ($pass -eq 'second' -and $RebuildBetweenPasses) {
            $buildArguments = @('build', '-c', $Configuration, '--no-restore', '-t:Rebuild')
            if ($BuildArtifactsPath) { $buildArguments += @('--artifacts-path', $BuildArtifactsPath) }
            & dotnet @buildArguments *> (Join-Path $ArtifactDirectory 'rebuild.log')
            if ($LASTEXITCODE -ne 0) { throw 'Rebuild failed; inspect rebuild.log' }
        }
        $output = Join-Path $ArtifactDirectory $pass
        # Compilation must precede this command. Repack identical existing inputs,
        # including compiler extensions, without unrelated concurrent build writes.
        $packArguments = @('pack', '-c', $Configuration, '--no-build', '-o', $output)
        if ($BuildArtifactsPath) { $packArguments += @('--artifacts-path', $BuildArtifactsPath) }
        & dotnet @packArguments *> (Join-Path $ArtifactDirectory "$pass.log")
        if ($LASTEXITCODE -ne 0) { throw "Pack failed; inspect $pass.log" }
        $files = @(Get-ChildItem -LiteralPath $output -File | Where-Object Extension -in @('.nupkg', '.snupkg'))
        if ($files.Count -ne 8) { throw 'Expected five runtime/compiler packages and three symbol packages' }
        foreach ($file in $files) {
            $key = $file.Name
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            if ($pass -eq 'first') { $hashes[$key] = $hash }
            elseif (-not $hashes.ContainsKey($key) -or $hashes[$key] -ne $hash) {
                throw "Archive bytes differ: $key"
            }
        }
    }
    $hashes.GetEnumerator() | Sort-Object Name | ForEach-Object {
        [pscustomobject]@{ Package=$_.Name; SHA256=$_.Value }
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'sha256.json')
    Write-Output "All eight package archives reproduced byte-for-byte; rebuilt between passes: $RebuildBetweenPasses."
}
finally { Pop-Location }
