# SharpSafeNumerics

A .NET 11 / C# 15 numeric-safety library for bounded integer arithmetic, explicit recoverable errors, and engineering software with deterministic requirements.

**Status:** RC-1 candidate under qualification; see [current evidence](docs/verification/2026-11-17-rc1.md). Embedded and safety-critical application qualification remains open.

## What is implemented

The project is organized as M1–M5; see [milestones](docs/MILESTONES.md). Numeric-domain **attributes are the specification surface** and Roslyn source generation converts them to reflection-free code. [Why attributes + generation](docs/ANNOTATIONS_AND_GENERATION.md).


- BCL-only generic integer core using `IBinaryInteger<T>` and `IMinMaxValue<T>`.
- Arithmetic without exceptions for expected failure: addition, subtraction, multiplication, division, and negation.
- Exact range-checked conversion without expected range exceptions for built-in integers, bounded range admission, four arithmetic policies: Reject, Saturate, Wrap, Throw.
- Explicit `NumericResult<T>` struct with a default *invalid* state.
- Optional C# 15 `union` adapter for ergonomic handling **that boxes value-type cases**; do not use it on no-allocation paths.
- Dependency-free console conformance tests, Native AOT smoke project, GitHub Actions CI.
- Declarative bounded-number attributes, generated arithmetic/default policies, exact conversions, span text and fixed-width endian codecs for all 12 built-in integer types.
- Strict-scope diagnostics SSN1001–SSN1008 with documented static-analysis limits.
- Int32 batch addition with detected AVX-512 for batches of at least 256 lanes, SIMD256/SIMD128, scalar fallback, per-lane errors and validation before writes.
- Owned Q32.32 fixed-point arithmetic with explicit rounding, Int128 intermediates and typed failures.
- Optional safe Decimal32/64/128 APIs over .NET 11 built-ins: exact by default, explicit nearest-even acceptance and reported precision loss. Decimal exactness checks may allocate. See [contracts and examples](docs/FIXED_AND_DECIMAL.md).
- Explicit IEEE decimal operations in the optional Decimal package: five rounding modes, flags/traps, raw encodings, conversions, elementary functions and exact reductions. See the [operation matrix](docs/IEEE_DECIMAL_OPERATION_MATRIX.md) and [bounded qualification](docs/verification/2026-11-18-ieee-decimal.md); full host-environment conformance is not claimed.
- Referenced SharpUnits [integration example](docs/verification/2026-11-17-sharpunits.md): exact whole-volt admission and real dimensional checks, with no SharpUnits dependency in Core.
- CsCheck, Hedgehog, xUnit, Stryker.NET, Dafny and BenchmarkDotNet verification infrastructure.
- Detailed requirements, original software description, architecture, exception policy and verification plan.

## Quick start

Explore the [interactive HTML deep dive](docs/interactive-deep-dive.html) for comparisons
with built-in C#, editable arithmetic/conversion/domain examples, annotation
walkthroughs and conceptual SIMD lane demos. Download the HTML and open it in a
browser; GitHub's source viewer does not execute its scripts. It works offline.

```bash
dotnet build -c Release
dotnet test -c Release
dotnet run --project tests/SharpSafeNumerics.Tests -c Release
dotnet publish tests/SharpSafeNumerics.AotSmoke -c Release -r linux-x64
```

```csharp
using SharpSafeNumerics;
ArithmeticError error = SafeInteger.TryAdd(byte.MaxValue, (byte)1, out byte value);
// error == ArithmeticError.Overflow; value == 0
var outcome = SafeInteger.Add((byte)250, (byte)10, ArithmeticPolicy.Saturate);
// outcome.Value == 255
```

## Scope and limitations
The `TryAdd/TrySubtract/TryMultiply/TryDivide/TryNegate` fast paths use preflight bounds for built-in integral types, avoiding overflow exceptions on expected failures. `TryConvert` validates a sign-preserving round trip before publishing its candidate; range rejection returns `OutOfRange` and zero. All 144 built-in integer conversion pairs have boundary/oracle and warmed allocation tests. See [bounded verification evidence](docs/verification/2026-11-17-conversion.md); this does not establish whole-library or target-hardware timing qualification. Custom `IBinaryInteger<T>` implementations are outside the qualified set.

The C# 15 union adapter uses generated union storage, which boxes struct cases per Microsoft's documentation. The strict core deliberately does not depend on that adapter.

See [Software description](docs/SOFTWARE_DESCRIPTION.md), [Product requirements](docs/PRODUCT_REQUIREMENTS.md), [Design](docs/DESIGN.md), [Exception policy](docs/EXCEPTION_POLICY.md), [Verification](docs/VERIFICATION_PLAN.md).

## Primary sources
- https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15
- https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union
- https://learn.microsoft.com/en-us/dotnet/api/system.numerics.ibinaryinteger-1?view=net-11.0
- https://learn.microsoft.com/en-us/dotnet/api/system.numerics.inumberbase-1?view=net-11.0

License: MIT.
