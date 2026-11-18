param(
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [string]$SharpUnitsSource
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing repository' }
$ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
if (-not (Test-Path -LiteralPath (Split-Path -Parent $ArtifactDirectory) -PathType Container)) { throw 'Missing artifact parent' }
if (Test-Path -LiteralPath $ArtifactDirectory) { throw 'Use a fresh integration artifact directory' }
# This is a source-only integration against the existing project's real API.
# Pin immutable development evidence; do not imply a published SharpUnits package.
$pin = '7ebdbcda9958e4eb4cee623065ecdd37395dac89'
if ($SharpUnitsSource) {
    $SharpUnitsSource = [IO.Path]::GetFullPath($SharpUnitsSource)
    if (-not (Test-Path -LiteralPath $SharpUnitsSource -PathType Container)) { throw 'Missing supplied SharpUnits checkout' }
}
$plannedSource = if ($SharpUnitsSource) { $SharpUnitsSource } else { Join-Path $ArtifactDirectory 'SharpUnits' }
$comparison = if ([IO.Path]::DirectorySeparatorChar -eq '\') { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$consumerPrefix = [IO.Path]::GetFullPath($repo).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
if ([IO.Path]::GetFullPath($plannedSource).StartsWith($consumerPrefix, $comparison)) {
    throw 'Keep SharpUnits source outside the numeric checkout so each project retains its own Directory.Build policy'
}
New-Item -ItemType Directory -Path $ArtifactDirectory | Out-Null
if (-not $SharpUnitsSource) {
    $SharpUnitsSource = Join-Path $ArtifactDirectory 'SharpUnits'
    if (Test-Path -LiteralPath $SharpUnitsSource) { throw 'Clone destination already exists' }
    & git clone --no-checkout -- https://github.com/jeffadickson/SharpUnits.git $SharpUnitsSource *> (Join-Path $ArtifactDirectory 'clone.log')
    if ($LASTEXITCODE -ne 0) { throw 'SharpUnits clone failed; inspect clone.log' }
    # exFAT cannot record ownership. Trust only this explicit, freshly owned path
    # for this invocation; never change the user's global Git safe.directory list.
    & git -c "safe.directory=$($SharpUnitsSource.Replace('\','/'))" -C $SharpUnitsSource checkout --detach $pin *> (Join-Path $ArtifactDirectory 'checkout.log')
    if ($LASTEXITCODE -ne 0) { throw 'Pinned SharpUnits checkout failed' }
}
$safe = $SharpUnitsSource.Replace('\','/')
$revision = & git -c "safe.directory=$safe" -C $SharpUnitsSource rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $revision.Trim() -ne $pin) { throw 'SharpUnits source must match the pinned integration revision' }
$changes = & git -c "safe.directory=$safe" -C $SharpUnitsSource status --porcelain
if ($LASTEXITCODE -ne 0 -or $changes) { throw 'Use an unchanged SharpUnits checkout; existing changes were preserved' }
$library = Join-Path $SharpUnitsSource 'src/SharpUnits/SharpUnits.csproj'
if (-not (Test-Path -LiteralPath $library -PathType Leaf)) { throw 'Missing pinned SharpUnits project' }
$project = Join-Path $repo 'examples/SharpUnitsInterop/SharpUnitsInterop.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw 'Missing integration sample' }
$build = Join-Path $ArtifactDirectory 'build'
if (-not (Test-Path -LiteralPath $build -PathType Container)) { New-Item -ItemType Directory -Path $build | Out-Null }
& dotnet build $project -c Release "-p:SharpUnitsProjectPath=$library" --artifacts-path $build *> (Join-Path $ArtifactDirectory 'build.log')
if ($LASTEXITCODE -ne 0) {
    Get-Content -LiteralPath (Join-Path $ArtifactDirectory 'build.log') -Tail 60 | Write-Output
    throw 'Integration build failed; inspect build.log'
}
& dotnet run --project $project -c Release --no-build "-p:SharpUnitsProjectPath=$library" --artifacts-path $build *> (Join-Path $ArtifactDirectory 'run.log')
if ($LASTEXITCODE -ne 0) { throw 'Integration behavior failed; inspect run.log' }
$output = [IO.File]::ReadAllText((Join-Path $ArtifactDirectory 'run.log'))
if ($output -notmatch 'integration passed (\d+) assertions') { throw 'Missing integration assertion verdict' }
$assertionCount = [int]$Matches[1]
$numericsRevision = & git -C $repo rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot record numeric repository revision' }
$upstreamInputs = @(& git -c "safe.directory=$safe" -C $SharpUnitsSource ls-files -- 'src/SharpUnits/*.cs' 'src/SharpUnits/*.csproj' 'Directory.Build.props' 'Directory.Build.targets' 'Directory.Packages.props' 'global.json')
if ($LASTEXITCODE -ne 0 -or $upstreamInputs.Count -eq 0) { throw 'Cannot inventory pinned source inputs' }
[pscustomobject]@{
    Passed = $true
    SharpUnitsRevision = $pin
    SharpSafeNumericsBaseRevision = $numericsRevision.Trim()
    Assertions = $assertionCount
    Seed = 731931
    Scope = 'Exact whole-volt admission into actual SharpUnits coherent-SI double API; no general exact unit arithmetic claim'
    SourceFiles = @($upstreamInputs | ForEach-Object {
        [pscustomobject]@{ File = $_; SHA256 = (Get-FileHash -LiteralPath (Join-Path $SharpUnitsSource $_) -Algorithm SHA256).Hash }
    })
    IntegrationFiles = @(@('examples/SharpUnitsInterop/Program.cs', 'examples/SharpUnitsInterop/VoltageBridge.cs', 'examples/SharpUnitsInterop/SharpUnitsInterop.csproj', 'eng/VerifySharpUnitsIntegration.ps1') | ForEach-Object {
        [pscustomobject]@{ File = $_; SHA256 = (Get-FileHash -LiteralPath (Join-Path $repo $_) -Algorithm SHA256).Hash }
    })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'summary.json')
Write-Output $output.Trim()
Write-Output "Pinned SharpUnits integration passed: $pin; no runtime dependency added to Core."
