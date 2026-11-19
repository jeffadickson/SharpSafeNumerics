# Copyright (c) 2026 Jeff Dickson.
# SPDX-License-Identifier: MIT

Describe 'Numeric soak CLI contract' {
 BeforeAll {
  $script:project = Join-Path $PSScriptRoot 'SharpSafeNumerics.NumericSoak\SharpSafeNumerics.NumericSoak.csproj'
  $script:dll = Join-Path $PSScriptRoot 'SharpSafeNumerics.NumericSoak\bin\Release\net11.0\SharpSafeNumerics.NumericSoak.dll'
 }
 It 'builds the nonshipping harness' { & dotnet build $script:project -c Release --nologo; $LASTEXITCODE | Should -Be 0 }
 It 'rejects malformed, unknown and duplicate options' {
  foreach ($arguments in @(@('--seconds','0'),@('--seconds','7201'),@('--seed','0'),@('--seed','-1'),@('--seconds'),@('--unknown','1'),@('--seed','1','--seed','2'),@('--seconds','+1'))) {
   & dotnet $script:dll @arguments 2>&1 | Out-Null; $LASTEXITCODE | Should -Be 2
  }
 }
 It 'rejects missing summary parents and existing files' {
  & dotnet $script:dll --summary (Join-Path $TestDrive 'missing\summary.json') 2>&1 | Out-Null; $LASTEXITCODE | Should -Be 2
  $existing=Join-Path $TestDrive 'existing.json'; Set-Content -LiteralPath $existing -Value 'preserve'
  & dotnet $script:dll --summary $existing 2>&1 | Out-Null; $LASTEXITCODE | Should -Be 2
  (Get-Content -LiteralPath $existing -Raw).Trim() | Should -Be 'preserve'
 }
 It 'executes a real short seeded campaign and emits actual counters' {
  $summary=Join-Path $TestDrive 'short.json'
  & dotnet $script:dll --seconds 3 --seed 731930 --summary $summary
  $LASTEXITCODE | Should -Be 0
  $result=Get-Content -LiteralPath $summary -Raw | ConvertFrom-Json
  $result.Passed | Should -BeTrue
  $result.Samples | Should -BeGreaterThan 0
  $result.Assertions | Should -BeGreaterThan $result.Samples
  $result.DecimalOperations | Should -BeGreaterThan 0
  $result.FixedOperations | Should -BeGreaterThan 0
  $result.Decimal32Cases | Should -BeGreaterThan 0
  $result.Decimal64Cases | Should -BeGreaterThan 0
  $result.Decimal128Cases | Should -BeGreaterThan 0
 }
}
