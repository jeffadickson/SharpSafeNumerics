# RC1 fixed-point and safe decimal implementation plan

User decision, 2026-11-17: implement fixed-point semantics and safe APIs around
.NET 11 built-in Decimal32/64/128 now, with independent testing. This supersedes
the earlier five-pass recommendation to defer these features. SN-018/SN-019 remain
stable product IDs; scoped StrictDoc children define the executable contract.

## Reuse and architecture decision

Use the existing .NET 11 BCL decimal engine. The local runtime exposes all three
IEEE decimal types; do not confuse them with System.Decimal. No third-party
runtime engine is needed. Microsoft source: dotnet/runtime issue81376 and
dotnet/docs docs/core/whats-new/dotnet-11/overview.md. Keep integer Core BCL-only.
Safe decimal is a separate optional BCL-only assembly: exactness inspection may
allocate bounded BigInteger temporaries, explicitly outside strict integer hot
paths. No claim of universal decimal allocation freedom or real-time timing.

Evaluated alternatives: FixedMathSharp.Lean8.0.0 (MIT, Q32.32, saturating operators,
divide-zero throws), PeterO.Numbers1.8.2 (Unlicense/public domain, useful independent
oracle, arbitrary-precision allocations), JakeSays/decimals commit
a9986bb5539d774b0dcf4f7a6be63dcb470293b2 (MIT, managed decimal candidate),
decNumber3.68 (ICU-licensed C) and Intel decimal library (native integration cost).
No third-party implementation is adopted. Bounded Windows trial seed731927:
12,180 candidate-decimal numerical comparisons against PeterO matched; flags,
quantum and NaN payload were not compared. 10,000 FixedMathSharp saturating raw
additions matched BigInteger. It saturated max+1 and threw DivideByZeroException.
Measured 10,000 warmed divisions: candidate Decimal64 zero allocated bytes,
PeterO decimal64-context 16,800,000 bytes. Not BCL decimal measurements or broad
qualification. Trial artifacts reside under E:/Artifacts/SharpSafeNumerics/
windows-x64/numeric-expansion-assessment-20261117.

Primary references inspected:
- https://github.com/dotnet/runtime/issues/81376
- https://github.com/dotnet/docs/blob/main/docs/core/whats-new/dotnet-11/overview.md
- https://github.com/dotnet/runtime/blob/v11.0.0/src/libraries/System.Private.CoreLib/src/System/Numerics/Decimal64.cs
- https://www.nuget.org/packages/FixedMathSharp.Lean/8.0.0
- https://github.com/mrdav30/FixedMathSharp
- https://www.nuget.org/packages/PeterO.Numbers/1.8.2
- https://github.com/peteroupc/Numbers
- https://github.com/JakeSays/decimals/tree/a9986bb5539d774b0dcf4f7a6be63dcb470293b2
- https://speleotrove.com/decimal/decnumber.html
- https://www.intel.com/content/www/us/en/developer/articles/tool/intel-decimal-floating-point-math-library.html

Custom fixed arithmetic is limited to the small explicit-status/rounding contract
unmet by the evaluated saturating operator library. No vectors, transcendental
functions or replacement decimal engine are built. Later measured qualification,
not a library README's claims, governs acceptance.

## Task1 — owned Q32.32 vertical slice

Create Core Fixed64, FixedRounding and SafeFixed APIs; UnitTests Fixed64Tests.
Raw signed long represents raw/2^32. Default Fixed64 is valid zero (unlike result
default). Provide raw construction, exact integral/ratio admission, add/subtract,
multiply/divide/negate with typed failures and cleared outputs. Int128 avoids
premature overflow. Default rounding Exact rejects discarded fractions; explicit
ToEven, ToZero, Floor, Ceiling and AwayFromZero select rounded results. Invalid
rounding always rejects before operand-dependent paths. Reject overflow with
directional existing statuses. No implicit saturation, wrapping or floating-point
conversion. Add XMLDoc and a runnable example.

Write tests before implementation; observe red. Test half ties both signs,
endpoint/carry boundaries, zero-divisor, invalid rounding bytes, cleared failures,
allocation and exact BigInteger rational reference. Preserve reproducible seeds.

## Task2 — optional safe decimal vertical slice

Create SharpSafeNumerics.Decimal referencing Core, exposing SafeDecimal TryAdd,
TrySubtract, TryMultiply, TryDivide, TryNegate, and exact cross-format conversion
for built-in System.Numerics.Decimal32/64/128 only. NumericResult default stays
invalid; new result metadata must report inexactness when rounding is accepted.
DecimalRounding.Exact is default; ToEven explicitly accepts BCL rounding. Reject
nonfinite inputs, zero divisors, nonfinite overflow results, unsupported types and
invalid options before successful output. Distinguish an exactly representable
result from inexact/tiny rounded results by exact rational arithmetic, not a
reverse-operation heuristic. Expected failures must not throw. Preserve output
default on rejection. Decimal operations may allocate for exactness checking.

Add tests first, red then green, independent PeterO/reference rational cases,
format extrema, cancellation, subnormals, signed zeros, special inputs, rounded
division, conversions and invalid options. Test all three widths. No promise of
transcendental support, arbitrary rounding modes, or whole IEEE754 conformance.

## Task3 — integration, verification and release evidence

Coordinator adds shared ArithmeticError.Inexact and NonFinite values without
renumbering existing values; StrictDoc SN-018-FIXED and SN-019-DECIMAL; solution,
central packages/API baselines, examples, package consumer, CI/AOT smoke and docs.
Review implementation/spec independently. Run scoped coverage (line+branch),
negative/property checks with retained seeds, Stryker on new implementation,
Dafny model where useful, Windows/Linux checks and Native AOT. Explain formal
model scope versus C# execution. Commit/push verified milestones and observe CI.
Keep macOS off and preserve the active soak pinned to2e24288; it cannot qualify
new arithmetic. New qualification campaigns need their own revision evidence.

Acceptance is successful actual verification and documented limitations, not
merely configuration. Full tests means relevant declared scope, not exhaustive
testing of every representable Decimal128 operand or formal C# refinement.
