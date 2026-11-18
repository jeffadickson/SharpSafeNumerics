#requires -Version 7.4
<#
.SYNOPSIS
Builds with the pinned x64 SDK and executes managed tests under an isolated x86 runtime.
.DESCRIPTION
ArtifactDirectory must be fresh with an existing parent. RuntimeDirectory reuses a
read-only .NET 11.0.0 x86 host; otherwise the official archive is SHA512-verified
before extraction. RuntimeArchive optionally supplies a cached archive to verify.
No installed runtime, registry or persistent environment setting is changed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$ArtifactDirectory,
    [string]$RuntimeDirectory,
    [string]$RuntimeArchive,
    [switch]$SkipConsoleTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath $repository -PathType Container)) { throw 'Missing repository' }
if (![Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) { throw 'Windows is required for this x86 qualification' }
$ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
if (!(Test-Path -LiteralPath (Split-Path -Parent $ArtifactDirectory) -PathType Container)) { throw 'Missing artifact parent' }
if (Test-Path -LiteralPath $ArtifactDirectory) { throw 'Use a fresh artifact directory; existing files are preserved' }
if ($RuntimeDirectory -and $RuntimeArchive) { throw 'RuntimeDirectory and RuntimeArchive are mutually exclusive' }
if ($RuntimeArchive) {
    $RuntimeArchive = [IO.Path]::GetFullPath($RuntimeArchive)
    if (!(Test-Path -LiteralPath $RuntimeArchive -PathType Leaf)) { throw 'Missing supplied runtime archive' }
}

function Confirm-X86Runtime([string]$Directory) {
    if (!(Test-Path -LiteralPath $Directory -PathType Container)) { throw 'Missing supplied runtime directory' }
    $hostFile = Join-Path $Directory 'dotnet.exe'
    if (!(Test-Path -LiteralPath $hostFile -PathType Leaf)) { throw 'Missing runtime host' }
    $information = @(& $hostFile --info 2>&1)
    if ($LASTEXITCODE -ne 0 -or ($information -join "`n") -notmatch '(?m)^\s*Architecture:\s*x86\s*$') { throw 'Runtime host must have actual x86 architecture' }
    $versions = @(& $hostFile --list-runtimes 2>&1)
    if ($LASTEXITCODE -ne 0 -or ($versions -join "`n") -notmatch '(?m)^Microsoft.NETCore.App 11\.0\.0 \[') { throw 'Runtime must contain Microsoft.NETCore.App 11.0.0' }
    return $information
}

# Reject a wrong supplied host before creating any output or mutating the process environment.
if ($RuntimeDirectory) {
    $RuntimeDirectory = [IO.Path]::GetFullPath($RuntimeDirectory)
    $runtimeInformation = @(Confirm-X86Runtime $RuntimeDirectory)
}
$sdkHost = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$oldX86Root = [Environment]::GetEnvironmentVariable('DOTNET_ROOT_X86', 'Process')
$summary = [ordered]@{ Passed = $false; StartUtc = [DateTime]::UtcNow.ToString('O'); EndUtc = $null; SDK = $null; Runtime = '11.0.0'; Architecture = 'X86'; Suites = @(); TotalPassed = 0; ConsoleTests = !$SkipConsoleTests; Failure = $null }
$created = $false
Push-Location -LiteralPath $repository
try {
    $sdkVersion = (& $sdkHost --version | Out-String).Trim()
    # The CLI applies this checkout's global.json and its latestPatch policy.
    # Check the selected SDK family without rejecting a valid servicing update.
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^11\.0\.\d+$') { throw 'Qualification requires the repository-selected .NET 11.0 SDK' }
    $sdkInfo = @(& $sdkHost --info 2>&1)
    if ($LASTEXITCODE -ne 0 -or ($sdkInfo -join "`n") -notmatch '(?m)^\s*Architecture:\s*x64\s*$') { throw 'Build controller must use the installed x64 SDK' }
    $summary.SDK = $sdkVersion
    New-Item -ItemType Directory -Path $ArtifactDirectory | Out-Null
    $created = $true
    $sdkInfo | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'sdk-info.log')

    if (!$RuntimeDirectory) {
        $metadataUrl = 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/11.0/releases.json'
        $metadata = Invoke-RestMethod -Uri $metadataUrl
        $release = @($metadata.releases | Where-Object { $_.'release-version' -eq '11.0.0' })
        if ($release.Count -ne 1) { throw 'Official runtime 11.0.0 metadata is unavailable or ambiguous' }
        $package = @($release[0].runtime.files | Where-Object { $_.rid -eq 'win-x86' -and $_.name -eq 'dotnet-runtime-win-x86.zip' })
        if ($package.Count -ne 1 -or $package[0].hash -notmatch '^[0-9a-fA-F]{128}$' -or $package[0].url -notmatch '^https://builds\.dotnet\.microsoft\.com/dotnet/Runtime/11\.0\.0/') { throw 'Unexpected official runtime package metadata' }
        $package[0] | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'runtime-source.json')
        if (!$RuntimeArchive) {
            $RuntimeArchive = Join-Path $ArtifactDirectory $package[0].name
            Invoke-WebRequest -Uri $package[0].url -OutFile $RuntimeArchive
        }
        if ((Get-FileHash -LiteralPath $RuntimeArchive -Algorithm SHA512).Hash -ne $package[0].hash) { throw 'Runtime archive SHA512 does not match official metadata' }
        $RuntimeDirectory = Join-Path $ArtifactDirectory 'runtime'
        if (!(Test-Path -LiteralPath $RuntimeDirectory -PathType Container)) { New-Item -ItemType Directory -Path $RuntimeDirectory | Out-Null }
        Expand-Archive -LiteralPath $RuntimeArchive -DestinationPath $RuntimeDirectory
        $runtimeInformation = @(Confirm-X86Runtime $RuntimeDirectory)
    }
    $runtimeInformation | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'runtime-info.log')
    $runtimeHost = Join-Path $RuntimeDirectory 'dotnet.exe'
    $summary.RuntimeHost = $runtimeHost
    $summary.RuntimeHostSHA256 = (Get-FileHash -LiteralPath $runtimeHost -Algorithm SHA256).Hash
    $summary.SourceRevision = (& git -C $repository rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record source revision' }
    $summary.RepositoryStatus = @(& git -C $repository status --porcelain=v1 --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record repository status' }
    # HEAD is context, not a claim that dirty/untracked inputs are part of that commit.
    # Inventory actual Git-visible inputs; ignored build outputs are excluded.
    $sourceInputs = @(& git -C $repository ls-files --cached --others --exclude-standard -- src tests/Shared eng/NativeWidthProbe eng/Invoke-X86Qualification.ps1 tests/SharpSafeNumerics.UnitTests tests/SharpSafeNumerics.DecimalTests tests/SharpSafeNumerics.GeneratorTests tests/SharpSafeNumerics.AnalyzerUnitTests tests/SharpSafeNumerics.Tests tests/SharpSafeNumerics.AnalyzerTests ':(glob)Directory.Build*.props' ':(glob)Directory.Build*.targets' Directory.Packages.props global.json)
    if ($LASTEXITCODE -ne 0 -or $sourceInputs.Count -eq 0) { throw 'Cannot inventory source inputs' }
    $sourceHashes = @($sourceInputs | Sort-Object -Unique | ForEach-Object {
        [pscustomobject]@{ File = $_; SHA256 = (Get-FileHash -LiteralPath (Join-Path $repository $_) -Algorithm SHA256).Hash }
    })
    $sourceManifest = Join-Path $ArtifactDirectory 'source-sha256.json'
    $sourceHashes | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $sourceManifest
    $summary.SourceManifestSHA256 = (Get-FileHash -LiteralPath $sourceManifest -Algorithm SHA256).Hash
    $summary.SourceInputCount = $sourceHashes.Count
    $buildRoot = Join-Path $ArtifactDirectory 'build'
    if (!(Test-Path -LiteralPath $buildRoot -PathType Container)) { New-Item -ItemType Directory -Path $buildRoot | Out-Null }
    $projects = @('eng/NativeWidthProbe/NativeWidthProbe.csproj')
    $suites = @('UnitTests', 'DecimalTests', 'GeneratorTests', 'AnalyzerUnitTests')
    foreach ($name in $suites) { $projects += "tests/SharpSafeNumerics.$name/SharpSafeNumerics.$name.csproj" }
    if (!$SkipConsoleTests) { foreach ($name in @('Tests', 'AnalyzerTests')) { $projects += "tests/SharpSafeNumerics.$name/SharpSafeNumerics.$name.csproj" } }
    foreach ($project in $projects) {
        $projectPath = Join-Path $repository $project
        $projectName = [IO.Path]::GetFileNameWithoutExtension($projectPath)
        & $sdkHost build $projectPath -c Release --artifacts-path $buildRoot -m:2 -p:UseSharedCompilation=false 2>&1 | Tee-Object -FilePath (Join-Path $ArtifactDirectory "$projectName-build.log") | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $projectName" }
    }
    $summary.ProductionAssemblyHashes = @(@('Core', 'Decimal', 'Unions', 'Generator', 'Analyzers') | ForEach-Object {
        $binary = Join-Path $buildRoot "bin/SharpSafeNumerics.$_/release/SharpSafeNumerics.$_.dll"
        [pscustomobject]@{ Assembly = "SharpSafeNumerics.$_.dll"; SHA256 = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash }
    })

    # Scoped environment selects only these test-host child processes. Finally restores it.
    $env:DOTNET_ROOT_X86 = $RuntimeDirectory
    $probe = Join-Path $buildRoot 'bin/NativeWidthProbe/release/NativeWidthProbe.dll'
    & $runtimeHost $probe --expected-bits 32 2>&1 | Tee-Object -FilePath (Join-Path $ArtifactDirectory 'native-width-before.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Actual x86 native-width preflight failed' }
    foreach ($name in $suites) {
        $assembly = Join-Path $buildRoot "bin/SharpSafeNumerics.$name/release/SharpSafeNumerics.$name.dll"
        $results = Join-Path $ArtifactDirectory "$name-results"
        if (!(Test-Path -LiteralPath $results -PathType Container)) { New-Item -ItemType Directory -Path $results | Out-Null }
        $diagnostic = Join-Path $ArtifactDirectory "$name-vstest-diag.log"
        & $sdkHost vstest $assembly /Platform:x86 /Logger:trx /ResultsDirectory:$results /Diag:$diagnostic 2>&1 | Tee-Object -FilePath (Join-Path $ArtifactDirectory "$name-test.log") | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "x86 tests failed: $name" }
        # The controller is x64; verify that its selected child host is the isolated x86 muxer.
        $trace = [IO.File]::ReadAllText($diagnostic)
        if (!$trace.Contains("Full path of host exe is $runtimeHost")) { throw "x86 host selection not confirmed: $name" }
        $trxFiles = @(Get-ChildItem -LiteralPath $results -Filter '*.trx' -File)
        if ($trxFiles.Count -ne 1) { throw "Missing or ambiguous TRX: $name" }
        [xml]$trx = Get-Content -LiteralPath $trxFiles[0].FullName -Raw
        $counts = $trx.TestRun.ResultSummary.Counters
        if ([int]$counts.failed -ne 0 -or [int]$counts.passed -le 0 -or [int]$counts.total -ne [int]$counts.passed) { throw "Incomplete test execution: $name" }
        $summary.Suites += [pscustomobject]@{ Name = $name; Passed = [int]$counts.passed; Failed = [int]$counts.failed; Skipped = [int]$counts.total - [int]$counts.executed; HostX86Confirmed = $true; AssemblySHA256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash }
        $summary.TotalPassed += [int]$counts.passed
    }
    if (!$SkipConsoleTests) {
        foreach ($name in @('Tests', 'AnalyzerTests')) {
            & $runtimeHost (Join-Path $buildRoot "bin/SharpSafeNumerics.$name/release/SharpSafeNumerics.$name.dll") 2>&1 | Tee-Object -FilePath (Join-Path $ArtifactDirectory "$name-run.log") | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "x86 console test failed: $name" }
        }
    }
    & $runtimeHost $probe --expected-bits 32 2>&1 | Tee-Object -FilePath (Join-Path $ArtifactDirectory 'native-width-after.log') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Actual x86 native-width postflight failed' }
    $summary.Passed = $true
    Write-Output "Managed x86 qualification passed: $($summary.TotalPassed) tests; actual native-width checks before/after; no Native AOT x86 claim."
}
catch {
    $summary.Failure = $_.Exception.Message
    throw
}
finally {
    # PowerShell's string argument conversion can turn a null into an empty value;
    # explicitly remove an originally absent variable to preserve that distinction.
    if ($null -eq $oldX86Root) { Remove-Item -LiteralPath Env:DOTNET_ROOT_X86 -ErrorAction SilentlyContinue }
    else { $env:DOTNET_ROOT_X86 = $oldX86Root }
    Pop-Location
    $summary.EndUtc = [DateTime]::UtcNow.ToString('O')
    if ($created) { $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'summary.json') }
}
