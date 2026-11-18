param(
    [Parameter(Mandatory)] [string] $Checkout,
    [Parameter(Mandatory)] [string] $Report,
    [Parameter(Mandatory)] [string] $ArtifactDirectory,
    [string[]] $Mutants = @('1834','1592','1602','1699','1737')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
foreach ($directory in @($Checkout,$ArtifactDirectory)) {
    if (!(Test-Path -LiteralPath $directory -PathType Container)) { throw "Verified directory missing: $directory" }
}
if (!(Test-Path -LiteralPath $Report -PathType Leaf)) { throw 'Stryker report missing' }
$production = Join-Path $Checkout 'src\SharpSafeNumerics.Decimal'
$test = Join-Path $Checkout 'tests\SharpSafeNumerics.DecimalTests\IeeeFunctionMatrixTests.cs'
if (!(Test-Path -LiteralPath $production -PathType Container)) { throw 'Production source missing' }
if (!(Test-Path -LiteralPath $test -PathType Leaf)) { throw 'Counterexample tests missing' }
$sources = Join-Path $ArtifactDirectory 'sources'
if (!(Test-Path -LiteralPath $sources -PathType Container)) { New-Item -ItemType Directory -Path $sources | Out-Null }
# Only disposable source copies are mutated. Authoritative Stryker locations and
# replacements are replayed verbatim; this is not a new mutation-generation engine.
Get-ChildItem -LiteralPath $production -Filter '*.cs' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $sources $_.Name) }
Copy-Item -LiteralPath $test -Destination (Join-Path $ArtifactDirectory 'IeeeFunctionMatrixTests.cs')
$data = Get-Content -LiteralPath $Report -Raw | ConvertFrom-Json
$affected = $data.files.PSObject.Properties | Where-Object { @($_.Value.mutants | Where-Object { $_.id -in $Mutants }).Count -gt 0 }
foreach ($entry in $affected) { [IO.File]::WriteAllText((Join-Path $sources ([IO.Path]::GetFileName($entry.Name))),$entry.Value.source) }
$core = Join-Path $Checkout 'src\SharpSafeNumerics.Core\SharpSafeNumerics.Core.csproj'
$snapshot = @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>15.0</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>SharpSafeNumerics.Decimal</AssemblyName></PropertyGroup><ItemGroup><Compile Include="sources/*.cs"/><ProjectReference Include="$core"/></ItemGroup></Project>
"@
[IO.File]::WriteAllText((Join-Path $ArtifactDirectory 'DecimalSnapshot.csproj'),$snapshot)
$tests = @'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><LangVersion>15.0</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors><EnableDefaultCompileItems>false</EnableDefaultCompileItems><IsTestProject>true</IsTestProject></PropertyGroup><ItemGroup><Compile Include="IeeeFunctionMatrixTests.cs"/><ProjectReference Include="DecimalSnapshot.csproj"/><PackageReference Include="PeterO.Numbers" Version="1.8.2"/><PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0"/><PackageReference Include="xunit" Version="2.9.3"/><PackageReference Include="xunit.runner.visualstudio" Version="4.0.1"/></ItemGroup></Project>
'@
$project = Join-Path $ArtifactDirectory 'Replay.Tests.csproj'
[IO.File]::WriteAllText($project,$tests)
$filter = 'FullyQualifiedName~SurvivorCounterexamplesForExactPowersQuantumAndPiLattice|FullyQualifiedName~CoreSpecialValuesQuadrantsAndUnsupportedCarrierOrdering'
$results = [Collections.Generic.List[object]]::new()
foreach ($id in @('baseline') + $Mutants) {
    foreach ($entry in $affected) { [IO.File]::WriteAllText((Join-Path $sources ([IO.Path]::GetFileName($entry.Name))),$entry.Value.source) }
    if ($id -ne 'baseline') {
        $entry = $affected | Where-Object { $_.Value.mutants.id -contains $id } | Select-Object -First 1
        $mutant = $entry.Value.mutants | Where-Object id -eq $id
        if (!$mutant) { throw "Unknown authoritative mutant $id" }
        $source = $entry.Value.source
        $lines = $source.Split("`n")
        $start = $mutant.location.start.column - 1; $end = $mutant.location.end.column - 1
        for ($i=0; $i -lt $mutant.location.start.line-1; $i++) { $start += $lines[$i].Length + 1 }
        for ($i=0; $i -lt $mutant.location.end.line-1; $i++) { $end += $lines[$i].Length + 1 }
        $changed = $source.Substring(0,$start) + $mutant.replacement + $source.Substring($end)
        [IO.File]::WriteAllText((Join-Path $sources ([IO.Path]::GetFileName($entry.Name))),$changed)
    }
    $log = Join-Path $ArtifactDirectory "$id.log"
    & dotnet test $project -c Release --artifacts-path (Join-Path $ArtifactDirectory 'build') --filter $filter --nologo > $log 2>&1
    $exitCode = $LASTEXITCODE
    $summary = Get-Content -LiteralPath $log | Where-Object { $_ -match 'Passed!|Failed!|error ' } | Select-Object -Last 1
    $results.Add([ordered]@{ mutant=$id; exitCode=$exitCode; summary=$summary; log=$log })
    Write-Output "$id exit=$exitCode $summary"
    if ($id -eq 'baseline' -and ($exitCode -ne 0 -or $summary -notmatch '^Passed!')) { throw 'Unmutated control failed' }
    if ($id -ne 'baseline' -and ($exitCode -ne 1 -or $summary -notmatch '^Failed!')) { throw "Mutant $id did not produce the required observed test failure (compile errors are not kills)" }
}
foreach ($entry in $affected) { [IO.File]::WriteAllText((Join-Path $sources ([IO.Path]::GetFileName($entry.Name))),$entry.Value.source) }
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'replay-summary.json')
Get-FileHash -LiteralPath $Report,$test | Select-Object Path,Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'inputs-sha256.json')
Get-ChildItem -LiteralPath $sources -Filter '*.cs' | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'snapshot-sources-sha256.json')
