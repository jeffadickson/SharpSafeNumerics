param([Parameter(Mandatory)][string]$ArtifactDirectory)
$ErrorActionPreference = 'Stop'
# Keep generated fixtures in the standalone document; raw/export data stays local.
$repo = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing checkout' }
if (-not (Test-Path -LiteralPath $ArtifactDirectory -PathType Container)) { throw 'Create and verify a run-specific artifact directory first' }
$page = Join-Path $repo 'docs/interactive-deep-dive.html'
$project = Join-Path $PSScriptRoot 'DeepDiveEvidence'
if (-not (Test-Path -LiteralPath $project -PathType Container)) { throw 'Missing fixture exporter' }
if (-not (Test-Path -LiteralPath $page -PathType Leaf)) { throw 'Missing deep dive' }
dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'Exporter build failed' }
$raw = & dotnet run --project $project -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw 'Exporter failed' }
$json = ($raw -join "`n").Trim()
$parsed = $json | ConvertFrom-Json
if ($parsed.cases.Count -ne 2424) { throw 'Unexpected fixture scope; review before updating the document' }
$revision = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source revision' }
$content = [IO.File]::ReadAllText($page)
$pattern = '(?s)(<script id="fixtures" type="application/json">).*?(</script>)'
if ([regex]::Matches($content, $pattern).Count -ne 1) { throw 'Fixture placeholder is ambiguous' }
$content = [regex]::Replace($content, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Groups[1].Value + $json + $m.Groups[2].Value })
$content = [regex]::Replace($content, 'Package source snapshot: [0-9a-f]{40}', "Package source snapshot: $revision")
[IO.File]::WriteAllText($page, $content)
[IO.File]::WriteAllText((Join-Path $ArtifactDirectory 'deep-dive-fixtures.json'), $json)
Write-Output "Updated 2424 fixtures; source $revision; native width $($parsed.nativeBits)."
