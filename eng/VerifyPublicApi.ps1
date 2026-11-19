# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

param([Parameter(Mandatory)][string]$ArtifactDirectory, [string]$ConstraintTestAssembly)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing checkout' }
$ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
if (-not (Test-Path -LiteralPath (Split-Path -Parent $ArtifactDirectory) -PathType Container)) { throw 'Missing artifact parent' }
if (Test-Path -LiteralPath $ArtifactDirectory) { throw 'Use a fresh API-check directory' }
New-Item -ItemType Directory -Path $ArtifactDirectory | Out-Null
$snapshot = Join-Path $ArtifactDirectory 'snapshot'
New-Item -ItemType Directory -Path $snapshot | Out-Null
$projects = @('Core','Decimal','Unions','Generator','Analyzers')
if ($ConstraintTestAssembly) {
    $ConstraintTestAssembly = [IO.Path]::GetFullPath($ConstraintTestAssembly)
    if (-not (Test-Path -LiteralPath $ConstraintTestAssembly -PathType Leaf)) { throw 'Missing compiled constraint tests' }
}
# Git's file inventory honors ignore rules and includes the current uncommitted
# baseline files. Copy only build inputs for shipping projects, never bin/obj.
$inputs = @(& git -C $repo ls-files --cached --others --exclude-standard -- Directory.Build.props Directory.Build.targets Directory.Packages.props global.json src/SharpSafeNumerics.Core src/SharpSafeNumerics.Decimal src/SharpSafeNumerics.Unions src/SharpSafeNumerics.Generator src/SharpSafeNumerics.Analyzers)
if ($LASTEXITCODE -ne 0 -or $inputs.Count -eq 0) { throw 'Cannot inventory source snapshot' }
foreach ($relative in $inputs) {
    $destination = Join-Path $snapshot $relative
    $parent = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Copy-Item -LiteralPath (Join-Path $repo $relative) -Destination $destination
}
$results = [Collections.Generic.List[object]]::new()
function Build-Contract([string]$Name, [string]$Project, [string]$ExpectedDiagnostic) {
    $file = Join-Path $snapshot "src/SharpSafeNumerics.$Project/SharpSafeNumerics.$Project.csproj"
    $log = Join-Path $ArtifactDirectory ($Name + '.log')
    # The disposable copy is not a Git checkout. Disable only source-link queries
    # here; public API diagnostics and warnings-as-errors remain enabled.
    & dotnet build $file -c Release -p:EnableSourceLink=false -p:EnableSourceControlManagerQueries=false *> $log
    $buildExitCode = $LASTEXITCODE
    $text = [IO.File]::ReadAllText($log)
    if ($ExpectedDiagnostic) {
        if ($buildExitCode -eq 0 -or $text -notmatch ("error " + [regex]::Escape($ExpectedDiagnostic) + ':')) { throw "Control $Name did not report $ExpectedDiagnostic; inspect $log" }
    } elseif ($buildExitCode -ne 0) { throw "Baseline $Name failed; inspect $log" }
    $results.Add([pscustomobject]@{Name=$Name;Project=$Project;Expected=$ExpectedDiagnostic;ExitCode=$buildExitCode})
}
foreach ($project in $projects) { Build-Contract "baseline-$project" $project '' }
foreach ($project in $projects) {
    $probe = Join-Path $snapshot "src/SharpSafeNumerics.$project/ApiContractProbe.cs"
    if (Test-Path -LiteralPath $probe) { throw 'Probe file already exists' }
    try {
        [IO.File]::WriteAllText($probe, "namespace SharpSafeNumerics.ApiProbe { /// <summary>Unapproved API addition control.</summary>`n public sealed class Unexpected { } }")
        Build-Contract "addition-$project" $project 'RS0016'
    } finally { Remove-Item -LiteralPath $probe }
}
$core = Join-Path $snapshot 'src/SharpSafeNumerics.Core'
function Change-Contract([string]$Name, [string]$File, [string]$From, [string]$To, [string]$Diagnostic) {
    $path = Join-Path $core $File
    $bytes = [IO.File]::ReadAllBytes($path)
    $text = [IO.File]::ReadAllText($path)
    if (-not $text.Contains($From)) { throw "Control no longer matches: $Name" }
    try {
        [IO.File]::WriteAllText($path, $text.Replace($From, $To))
        Build-Contract $Name 'Core' $Diagnostic
    } finally { [IO.File]::WriteAllBytes($path, $bytes) }
}
Change-Contract 'rename-removal' 'BatchArithmeticError.cs' 'IsSuccess' 'Succeeded' 'RS0017'
Change-Contract 'enum-value' 'ArithmeticError.cs' 'None = 1' 'None = 7' 'RS0017'
Change-Contract 'optional-default' 'SafeInteger.cs' 'policy = ArithmeticPolicy.Reject)' 'policy = ArithmeticPolicy.Wrap)' 'RS0017'
Change-Contract 'nullability' 'NumericAnnotations.cs' 'public string? Unit { get; set; }' 'public string Unit { get; set; } = string.Empty;' 'RS0017'
# Source signature tracking is not a generic-constraint compatibility checker.
# Record that limit using a real compiling relaxation, rather than claiming it.
$constraintFile = Join-Path $core 'NumericResult.cs'
$constraintBytes = [IO.File]::ReadAllBytes($constraintFile)
try {
    # Removing struct also makes default potentially null. Use an explicit
    # null-forgiving expression only in this disposable probe so CS8604 does not
    # obscure the API analyzer's own treatment of generic constraints.
    $constraintText = [IO.File]::ReadAllText($constraintFile).Replace('where T : struct', '').Replace('new(default, error)', 'new(default!, error)')
    [IO.File]::WriteAllText($constraintFile, $constraintText)
    Build-Contract 'constraint-scope-limit' 'Core' ''
    if ($ConstraintTestAssembly) {
        # Run real metadata regressions against the actual weakened DLL, using
        # a copied test host so the developer's build outputs remain untouched.
        $testCopy = Join-Path $ArtifactDirectory 'constraint-test'
        if (Test-Path -LiteralPath $testCopy) { throw 'Constraint test destination already exists' }
        Copy-Item -LiteralPath (Split-Path -Parent $ConstraintTestAssembly) -Destination $testCopy -Recurse
        $coreDll = Join-Path $testCopy 'SharpSafeNumerics.Core.dll'
        if (-not (Test-Path -LiteralPath $coreDll -PathType Leaf)) { throw 'Test host does not contain Core' }
        Copy-Item -LiteralPath (Join-Path $core 'bin/Release/net11.0/SharpSafeNumerics.Core.dll') -Destination $coreDll -Force
        $probeLog = Join-Path $ArtifactDirectory 'constraint-test.log'
        & dotnet vstest (Join-Path $testCopy ([IO.Path]::GetFileName($ConstraintTestAssembly))) '--TestCaseFilter:FullyQualifiedName~ApiConstraintTests' '--logger:console;verbosity=normal' *> $probeLog
        $probeExitCode = $LASTEXITCODE
        $probeText = [IO.File]::ReadAllText($probeLog)
        if ($probeExitCode -eq 0 -or $probeText -notmatch 'NumericResult_requires_nonnullable_struct_constraint' -or
            $probeText -notmatch 'DefaultConstructorConstraint' -or $probeText -notmatch 'Actual:\s+None') {
            throw "Constraint tests did not detect the relaxed metadata; inspect $probeLog"
        }
        $results.Add([pscustomobject]@{Name='constraint-tests-reject-relaxation';Project='Core';Expected='metadata constraint assertion';ExitCode=$probeExitCode})
    }
} finally { [IO.File]::WriteAllBytes($constraintFile, $constraintBytes) }
Build-Contract 'restored-core' 'Core' ''
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'summary.json')
Write-Output "API controls completed: $($results.Count); source signature guard and targeted metadata constraints have separate scopes."
