param([Parameter(Mandatory)][string]$PlaywrightModule, [Parameter(Mandatory)][string]$ArtifactDirectory)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) { throw 'Missing run-specific artifact directory' }
if (-not (Test-Path -LiteralPath $PlaywrightModule -PathType Container)) { throw 'Missing existing Playwright module' }
$repo = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing checkout' }
$html = [IO.File]::ReadAllText((Join-Path $repo 'docs/interactive-deep-dive.html'))
$mutations = @(
    @{ Name='saturation'; From="policy==='Saturate'?clamp(exact,min,max):wrap(exact,type)"; To="policy==='Saturate'?wrap(exact,type):wrap(exact,type)" },
    @{ Name='conversion'; From="return input<min||input>max?{status:'OutOfRange',value:0n}:{status:'None',value:input};"; To="return {status:'None',value:input};" },
    @{ Name='domain'; From='saturated:clamp(exact,0n,63n)'; To='saturated:clamp(exact,0n,64n)' }
)
# Mutate isolated document copies, never the version-controlled source or C# library.
foreach ($mutation in $mutations) {
    if (-not $html.Contains($mutation.From)) { throw "Mutation no longer applies: $($mutation.Name)" }
    $file = Join-Path $ArtifactDirectory ($mutation.Name + '.html')
    [IO.File]::WriteAllText($file, $html.Replace($mutation.From, $mutation.To))
    $log = Join-Path $ArtifactDirectory ($mutation.Name + '.log')
    & node (Join-Path $PSScriptRoot 'VerifyDeepDive.cjs') $PlaywrightModule (Join-Path $ArtifactDirectory $mutation.Name) $file *> $log
    if ($LASTEXITCODE -eq 0) { throw "Mutation survived: $($mutation.Name)" }
    # The expected assertion must be fixture disagreement, not a setup failure.
    if (-not ([IO.File]::ReadAllText($log).Contains('false !== true'))) { throw "Unexpected failure; inspect $log" }
    Write-Output "Killed browser-model mutation: $($mutation.Name)"
}
exit 0
