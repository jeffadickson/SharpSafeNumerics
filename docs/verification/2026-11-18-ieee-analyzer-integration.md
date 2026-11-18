# IEEE decimal analyzer integration — 2026-11-18

Base revision: `6dc5fb4`. Exact analyzer/test/probe source hashes are retained in
`2026-11-18-ieee-analyzer-source-hashes.json`. This change closes the integration
gap between the new IEEE API and SN-013-RESULT / SN-013-ALLOCATION; it does not
alter Decimal production code or its tests.

The analyzer resolves `IeeeDecimalResult<T>` and `IeeeDecimal<T>` from the actual
optional assembly, including extern-alias-only references. Nullable/out/expression
discards retain SSN1003. Allocating IEEE helper calls in StrictNumeric scopes
receive SSN1007. Copy, IsSignMinus and Radix avoid allocating codecs and are
excluded; IEEE Negate, Abs and CopySign use allocating interchange code and warn.
These exclusions describe inspected implementation paths, not a general promise
that arbitrary transitive callers are allocation-free.

Executed with .NET SDK11.0.100 on Windows x64:

- Red: DecimalContractTests, 15 failed / 20 passed / zero skipped. Failures were
  missing IEEE diagnostics, not compilation failures.
- Green: complete AnalyzerUnitTests project, 155 passed / zero failed or skipped.
  Includes authentic metadata, source assembly, optional/Core absence, aliases,
  nullable/out results, allocating operations, justified exemptions and unrelated
  lookalike negative controls. Existing malformed-source and seeded CsCheck tests
  ran: PCG(1,114938), 64 randomized constants plus eight directed boundaries.
- Coverlet collector / ReportGenerator5.6.0: analyzer assembly 388/395 lines
  (98.2%), 541/610 branches (88.6%). NumericSymbols: 152/157 lines (96.8%),
  263/294 branches (89.4%). Dependencies, scripts and native code are excluded.
- The scoped `git diff --check` and PowerShell parser accepted these changes.

All test/build/log/coverage outputs use the unique directory
`E:/Artifacts/SharpSafeNumerics/windows-x64/ieee-decimal-20261118/analyzer-ieee`.
Normal repository bin/obj and the active Decimal mutation campaign are untouched.
Principal command, from a verified checkout with an existing run parent:

```text
dotnet test tests/SharpSafeNumerics.AnalyzerUnitTests -c Release --artifacts-path <run>/build --collect:"XPlat Code Coverage" --results-directory <run>/green --logger trx
```

`eng/VerifyPackages.ps1` adds a separate IEEE-only deliberate consumer failure.
It requires SSN1003 for a discarded result and SSN1007 for an observed allocating
IEEE call, so legacy SafeDecimal diagnostics cannot mask a broken integration.
Actual package acceptance is recorded by the root integration run, not inferred
from source-tree tests or script parsing.

Scoped Stryker.NET5.1.0 executed the three result-recognition/allocation-classifier
expressions in NumericSymbols: **26 killed, zero survivors, timeouts or uncovered
tested mutants; 100.00% score; exit0**, with the configured 90% threshold. The run
used two workers and the 35 DecimalContractTests cases in a separate E: source
snapshot. Total elapsed time was 1m35.056s. Global instrumentation also produced
448 candidates outside the mutate filter and 87 compile-error candidates, excluded
from that score; one compile-error candidate lies within the selected expression
scope. This does not qualify constructors or the complete analyzer.

Raw report: `<run>/mutation-qualified/reports/mutation-report.json`. Exact command:

```text
dotnet tool run dotnet-stryker -- --config-file <run>/stryker-ieee.json --output <run>/mutation-qualified --skip-version-check --msbuild-path <sdk11>/MSBuild.dll
```

The configuration selected `**/NumericSymbols.cs{4597..5790}` (Roslyn character
offsets, not line numbers), filtered `FullyQualifiedName~DecimalContractTests`,
and set thresholds high95/low90/break90. MSBUILD_EXE_PATH and MSBuildSDKsPath were
set process-locally to the existing .NET11.0.100 SDK and restored afterward.
Buildalyzer framework-discovery warnings fell back to the correct analyzed
net11.0/netstandard2.0 projects. Initial attempts selected line numbers as character
offsets, excluded every mutant and produced no score; they are not passing checks.
No runaway workers or memory intervention were observed.

No dedicated IEEE analyzer fuzz campaign or whole-analyzer mutation score is
claimed by this directed suite and scoped mutation run.
