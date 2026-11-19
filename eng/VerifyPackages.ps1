# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$WorkDirectory,
    [string]$Version = '0.1.0-rc.1'
)
$ErrorActionPreference = 'Stop'
# This consumer deliberately has package references only. A successful source-tree
# project-reference build would not verify NuGet layout or compiler-extension loading.
if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) { throw 'Missing package directory' }
$parent = Split-Path -Parent $WorkDirectory
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'Missing consumer parent' }
if (-not (Test-Path -LiteralPath $WorkDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $WorkDirectory | Out-Null
}
if (-not (Test-Path -LiteralPath $PSScriptRoot -PathType Container)) { throw 'Missing engineering scripts' }
$source = Join-Path $PSScriptRoot 'package-smoke/Program.cs'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Missing package consumer source' }
Copy-Item -LiteralPath $source -Destination (Join-Path $WorkDirectory 'Program.cs')
$safeVersion = [Security.SecurityElement]::Escape($Version)
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net11.0</TargetFramework><LangVersion>15.0</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SharpSafeNumerics.Core" Version="$safeVersion" />
    <PackageReference Include="SharpSafeNumerics.Decimal" Version="$safeVersion" />
    <PackageReference Include="SharpSafeNumerics.Generator" Version="$safeVersion" PrivateAssets="all" />
    <PackageReference Include="SharpSafeNumerics.Analyzers" Version="$safeVersion" PrivateAssets="all" />
    <PackageReference Include="SharpSafeNumerics.Unions" Version="$safeVersion" />
  </ItemGroup>
</Project>
"@
$projectPath = Join-Path $WorkDirectory 'PackageSmoke.csproj'
[IO.File]::WriteAllText($projectPath, $project)
# All five packages and their inter-package dependencies must resolve from
# this local feed; successful restore also checks the intended BCL-only closure.
dotnet restore $projectPath --source $PackageDirectory --packages (Join-Path $WorkDirectory 'packages')
if ($LASTEXITCODE -ne 0) { throw 'Package consumer restore failed' }
dotnet run --project $projectPath -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Package consumer behavior failed' }
$probe = Join-Path $WorkDirectory 'InvalidStrictProbe.cs'
[IO.File]::WriteAllText($probe, @'
using SharpSafeNumerics;
[StrictNumeric]
static class InvalidStrictProbe
{
    static void Probe()
    {
        _ = SafeInteger.Add(1, 2, ArithmeticPolicy.Throw);
        _ = SafeDecimal.TryAdd(System.Numerics.Decimal64.One, System.Numerics.Decimal64.One);
    }
}
'@)
try {
    $output = dotnet build $projectPath -c Release --no-restore 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0 -or $output -notmatch 'SSN1004' -or $output -notmatch 'SSN1007' -or $output -notmatch 'SSN1003') {
        throw "Packaged analyzer did not reject the deliberate Strict violation: $output"
    }
    Write-Output 'Packaged analyzer rejected deliberate Strict/decimal violations with SSN1003, SSN1004 and SSN1007.'
    # Use a separate IEEE-only compilation: legacy SafeDecimal diagnostics must not
    # accidentally satisfy this expansion's package-loading regression check.
    [IO.File]::WriteAllText($probe, @'
using System.Numerics;
using SharpSafeNumerics;
[StrictNumeric]
static class InvalidIeeeProbe
{
    static void Discard(IeeeDecimalResult<Decimal64> result) { _ = result; }
    static IeeeDecimalResult<Decimal64> Allocate() => IeeeDecimal<Decimal64>.Add(Decimal64.One, Decimal64.One);
}
'@)
    $output = dotnet build $projectPath -c Release --no-restore 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0 -or $output -notmatch 'SSN1003' -or $output -notmatch 'SSN1007') {
        throw "Packaged analyzer did not reject the deliberate IEEE result/allocation violations: $output"
    }
    Write-Output 'Packaged analyzer rejected independent IEEE result/allocation violations with SSN1003 and SSN1007.'
} finally {
    if (Test-Path -LiteralPath $probe -PathType Leaf) { Remove-Item -LiteralPath $probe }
}
# The deliberate failing compiler probe is expected. Publish an explicit success
# exit so callers do not mistake its retained native exit code for a failed check.
exit 0
