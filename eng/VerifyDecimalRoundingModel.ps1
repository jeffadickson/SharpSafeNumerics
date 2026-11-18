# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

# Verifies the mathematical model, then exercises C# emitted from that verified source.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ArtifactDirectory,[Parameter(Mandatory)][string]$SolverPath)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not(Test-Path -LiteralPath $repo -PathType Container)){throw 'Repository missing'}
if(-not(Test-Path -LiteralPath $SolverPath -PathType Leaf)){throw 'Solver executable missing'}
$ArtifactDirectory=[IO.Path]::GetFullPath($ArtifactDirectory)
if(-not(Test-Path -LiteralPath $ArtifactDirectory -PathType Container)){
    $parent=Split-Path -Parent $ArtifactDirectory
    if(-not(Test-Path -LiteralPath $parent -PathType Container)){throw 'Artifact parent must exist'}
    New-Item -ItemType Directory -Path $ArtifactDirectory|Out-Null
}
Push-Location -LiteralPath $repo
try{
    & dotnet tool run dafny -- verify formal/DecimalRounding.dfy --solver-path $SolverPath
    if($LASTEXITCODE-ne0){throw 'Decimal rounding model verification failed'}
    $generated=Join-Path $ArtifactDirectory 'DecimalRounding.cs'
    $project=Join-Path $ArtifactDirectory 'DecimalRounding.csproj'
    $text='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net11.0</TargetFramework><OutputType>Exe</OutputType><Nullable>disable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="DecimalRounding.cs" /></ItemGroup></Project>'
    if(Test-Path -LiteralPath $project -PathType Leaf){
        if((Get-Content -LiteralPath $project -Raw).Trim()-cne$text){throw 'Artifact directory contains a different project; use a fresh directory'}
    }elseif(Test-Path -LiteralPath $generated){throw 'Artifact directory contains an unmanaged generated file; use a fresh directory'}
    $modelHash=(Get-FileHash -LiteralPath (Join-Path $repo 'formal/DecimalRounding.dfy') -Algorithm SHA256).Hash
    $toolVersion=(& dotnet tool run dafny -- --version).Trim()
    if($LASTEXITCODE-ne0){throw 'Could not identify the pinned Dafny tool'}
    $key="$modelHash|$toolVersion|cs|include-runtime|net11.0"
    $keyPath=Join-Path $ArtifactDirectory 'translation.key'
    $reuse=(Test-Path -LiteralPath $generated -PathType Leaf)-and(Test-Path -LiteralPath $keyPath -PathType Leaf)
    if($reuse){$reuse=(Get-Content -LiteralPath $keyPath -Raw).Trim()-ceq$key}
    if(-not$reuse){
        & dotnet tool run dafny -- translate cs formal/DecimalRounding.dfy --no-verify --include-runtime --output $generated
        if($LASTEXITCODE-ne0){throw 'Verified model translation failed'}
        Set-Content -LiteralPath $keyPath -Value $key -Encoding utf8
    }
    # Dafny emits its own runtime source. Nullable settings here apply only to
    # generated verification code; no production compiler policy is suppressed.
    if(-not(Test-Path -LiteralPath $project -PathType Leaf)-or(Get-Content -LiteralPath $project -Raw).Trim()-cne$text){Set-Content -LiteralPath $project -Value $text -Encoding utf8}
    & dotnet run --project $project -c Release
    if($LASTEXITCODE-ne0){throw 'Generated C# model checks failed'}
}finally{Pop-Location}
