# Decimal vertical-slice evidence — 2026-11-17

Requirement: SN-019-DECIMAL. This is a finite arithmetic slice, not full IEEE754
status/quantum/payload conformance. Production code uses only the BCL and Core;
PeterO.Numbers 1.8.2 is an independent test-only arithmetic reference.

SDK: 11.0.100, Windows x64, net11.0. Initial test compilation failed because
SafeDecimal, DecimalRounding and DecimalResult did not exist; implementation was
then added. Final scoped test run: 11 passed, 0 failed, 0 skipped. The independent
reference campaign uses seed 731927, 120 operand pairs per format across add,
subtract, multiply and divide (1,440 numerical comparisons), plus independent
arbitrary-precision product comparisons for quotient exactness.

All nine format conversion pairs, all invalid rounding bytes 2..255, NaN and
infinity admission, maximum overflow direction, minimum subnormals, signed zero,
lost tiny addends, cancellation and positive/negative ties have targeted tests.
Tests do not exhaust all format bit patterns. See the expanded exponent campaign below.

Commands from the repository root:

```powershell
dotnet test tests/SharpSafeNumerics.DecimalTests/SharpSafeNumerics.DecimalTests.csproj -p:RunAnalyzers=false --verbosity quiet
dotnet format analyzers src/SharpSafeNumerics.Decimal/SharpSafeNumerics.Decimal.csproj --diagnostics RS0016 --no-restore --verbosity quiet
dotnet build src/SharpSafeNumerics.Decimal/SharpSafeNumerics.Decimal.csproj --no-restore -p:BuildProjectReferences=false --verbosity quiet
```

The analyzer-enabled own-project build succeeded with 0 warnings/errors. The
project-reference suppression reuses the existing Core build while its separate
API baseline is integrated. Full integration verification belongs to the
coordinator. Decimal PublicAPI files were generated with the official formatter.

Coverage invocation added `--collect:'XPlat Code Coverage' --results-directory`
with run-specific output under E:/Artifacts/SharpSafeNumerics/decimal-20261117-final.
Coverlet measured the Decimal assembly at 100% line coverage and approximately
96.96% branch coverage. Core appears at 0% in that narrowly scoped report and is
not qualified by decimal tests. Coverage alone does not establish correctness.
Raw TRX and Cobertura artifacts remain outside Git. Mutation/property-framework,
Native AOT and Linux checks are coordinated separately; this file makes no claim
that those checks have run.

Tested SafeDecimal.cs SHA256:
96023707C5ECD697CCF36CCD9BC132450581173C312CD52447162AA0F23AF893

Exactness uses rational cross-products derived from invariant roundtrip decimal
coefficient/exponent text, never a reverse floating-point operation heuristic.
BigInteger work and formatting may allocate, intentionally outside integer Core
hot paths. Expected failures return typed errors with default storage. Value
access on an unsuccessful result intentionally throws; TryGetValue does not.

The expanded exponent-distributed campaign uses seed 731928 and 160 operand
pairs per format. It generates 7/16/34 significant digits, both signs, coefficient
exponents -101..90 / -398..369 / -6176..6111 respectively, including cancellation
every eighth pair. All four operations compare rounded contextual PeterO results;
add/subtract/multiply additionally compare unlimited-precision exact PeterO
results for IsInexact and exact-mode rejection, while division checks the exact
product relation. Both signs of odd/even arithmetic halfway ties run at every
format precision. This expands rather than replaces the seed 731927 campaign.
The final expanded test run passed all 11 tests; the earlier 10-test coverage run
is retained as the coverage evidence above, without claiming a repeated coverage
measurement on the expanded campaign.

