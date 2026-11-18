# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

param(
 [string]$Dotnet = 'dotnet',
 [string]$ProbePath = '',
 [int]$ExpectedBits = 64,
 [switch]$SourceBuild,
 [string]$ArtifactsPath = ''
)
# Accept an explicit host and isolated build tree so the same contracts exercise x86 and x64.
BeforeAll {
 $script:hostCommand = $Dotnet
 $script:expected = $ExpectedBits
 $repo = Split-Path -Parent $PSScriptRoot
 if (!(Test-Path -LiteralPath $repo -PathType Container)) { throw 'Missing checkout' }
 $project = Join-Path $repo 'eng\NativeWidthProbe\NativeWidthProbe.csproj'
 if ($SourceBuild) {
  if ([string]::IsNullOrWhiteSpace($ArtifactsPath) -or !(Test-Path -LiteralPath $ArtifactsPath -PathType Container)) { throw 'SourceBuild requires an existing ArtifactsPath directory' }
  & dotnet build $project -c Release --artifacts-path $ArtifactsPath -m:2 -p:UseSharedCompilation=false
  if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
  $ProbePath = Join-Path $ArtifactsPath 'bin\NativeWidthProbe\release\NativeWidthProbe.dll'
 }
 elseif ([string]::IsNullOrWhiteSpace($ProbePath)) {
  $ProbePath = Join-Path $repo 'eng\NativeWidthProbe\bin\Release\net11.0\NativeWidthProbe.dll'
  # Normal aggregate discovery works after a solution build, or builds this small
  # nonshipping tool once when its default output does not exist yet.
  if (!(Test-Path -LiteralPath $ProbePath -PathType Leaf)) {
   & dotnet build $project -c Release -m:2 -p:UseSharedCompilation=false
   if ($LASTEXITCODE -ne 0) { throw 'Default probe build failed' }
  }
 }
 if (!(Test-Path -LiteralPath $ProbePath -PathType Leaf)) { throw "Missing probe: $ProbePath" }
 $script:probe = $ProbePath
}
Describe 'Native width probe CLI' {
 It 'executes the explicit matching architecture and independent oracle' {
  $output = & $script:hostCommand $script:probe --expected-bits $script:expected 2>&1
  $LASTEXITCODE | Should -Be 0
  ($output -join "`n") | Should -Match "PointerBits=$script:expected"
  ($output -join "`n") | Should -Match 'Seed=731922'
  ($output -join "`n") | Should -Match 'Passed nint boundaries=\d+, pairs=\d+; nuint boundaries=\d+, pairs=\d+'
 }
 It 'defaults to asserting the actual process width' {
  & $script:hostCommand $script:probe 2>&1 | Out-Null
  $LASTEXITCODE | Should -Be 0
 }
 It 'rejects mismatched width before running the oracle' {
  $other = if ($script:expected -eq 32) { 64 } else { 32 }
  $output = & $script:hostCommand $script:probe --expected-bits $other 2>&1
  $LASTEXITCODE | Should -Be 1
  ($output -join "`n") | Should -Match 'Architecture mismatch'
  ($output -join "`n") | Should -Not -Match 'Passed nint'
 }
 It 'rejects missing, duplicate, unknown and malformed options' {
  foreach ($arguments in @(@('--expected-bits'), @('--expected-bits','32','--expected-bits','32'), @('--unknown','32'), @('--expected-bits','0'), @('--expected-bits','+32'), @('--expected-bits','032'), @('--expected-bits',' 32'), @('--expected-bits','64.0'), @('--expected-bits','2147483648'), @('--expected-bits','64','extra'))) {
   $output = & $script:hostCommand $script:probe @arguments 2>&1
   $LASTEXITCODE | Should -Be 2
   ($output -join "`n") | Should -Not -Match 'Passed nint'
  }
 }
}
