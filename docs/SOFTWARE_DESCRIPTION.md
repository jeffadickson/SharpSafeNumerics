# SharpSafeNumerics — Software Description
**Origin:** User-approved .NET 11 architecture and the preceding SafeNumerics product-design response, Revision 0.1.  
**Status:** Design baseline, distinct from implemented feature set.

## Vision and motivation
Port the **concept**, not Boost.SafeNumerics itself. C# already has `checked`, `unchecked`, checked casts, generic math and conversions. The new value is *range-constrained domain types, explicit failure-as-data, policy-driven behavior, and enforceable numerical contracts*.

It is a .NET 11-native library for RISCue, VeloSim, SharpUnits, BenchForge, MemForge and deterministic simulation. Recoverable errors are ordinary values; programming errors and violated internal invariants can still throw.

## Railway-oriented numeric handling
A numeric operation has two tracks: `Success(value)` or `Failure(error)`. This is appropriate for data overflow, bounded queue capacity, illegal telemetry values and other expected rejection conditions. It is **not** a blanket exception ban.

| Condition | Intended behavior |
| --- | --- |
| Recoverable arithmetic overflow | Typed failure |
| Value outside domain | Typed failure |
| Queue full | Typed failure |
| Requested saturation | Clamped result |
| Programmer-contract violation | Exception |
| Corrupted invariant | Exception or fail-fast, by subsystem policy |

## Layered architecture
1. **.NET 11 primitives**: `IBinaryInteger<T>`, checked operator interfaces, `INumberBase<T>`, `Int128` and `UInt128`; optional `Decimal32`, `Decimal64`, `Decimal128`.
2. **Arithmetic core**: `TryAdd`, `TrySubtract`, `TryMultiply`, `TryDivide`, `TryNegate`, `TryConvert`; reject, saturate, wrap and throw policies.
3. **Typed results and domains**: non-boxing `NumericResult<T>`, optional C# 15 union result, source-generated bounded types such as `QueueDepth`, `TimerTicks` and `PacketLength`.
4. **Analyzers**: diagnostics for unsafe narrowing, unchecked arithmetic, ignored failures, throwing calls in Strict scope, implicit truncation, range bypass, allocations and invalid defaults.
5. **Applications**: RISCue and VeloSim prototypes, SharpUnits integration.

## Three exception profiles
- **Strict** (RTOS-style): expected failures always values; no exceptions or allocations on *qualified* hot paths; caller-defined handling of unexpected invariants.
- **Balanced** (default): explicit results at external boundaries; exceptions for programmer errors; opt-in allocation constraints on designated methods.
- **Convenient**: throwing wrappers and richer messages for UI/reporting code.

## .NET 11 and C# 15 integration
- Generic math interfaces supply arithmetic primitives and conversion functionality.
- C# 15 union syntax supports exhaustive pattern handling and a closed alternative set.
- **Important corrected limitation:** the C# 15 compiler-generated union stores case values in `object` and boxes value types. Therefore the strict zero-allocation core uses `NumericResult<T>`, and union support is a separate convenience assembly.
- Closed hierarchies may express rich policy/error families, but do not belong in the fixed-size core by default.
- Native AOT compatibility, static code generation and no runtime reflection are goals.
- Decimal floating-point types are optional future support, not a reason to delay fixed-width integer correctness.
- C# 15 memory safety is preview; the strict core must not depend on it.

## The proposed public APIs
```csharp
ArithmeticError error = SafeInteger.TryAdd(int.MaxValue, 1, out int result);
NumericResult<byte> bounded = SafeInteger.Add((byte)250, (byte)10, ArithmeticPolicy.Saturate);
```
The original design also proposed generated `QueueDepth.From(42)`, a `TryAdd` returning a range-aware domain type and pattern matching over `Success`/`Overflow`/`OutOfRange` union cases. RC-1 implements `TryCreate`/`TryAdd`/`TrySubtract`, policy-aware Add/Subtract, span text, endian codecs, exact conversions and comparisons. The original From/union-domain convenience proposal is distinct from this implemented status-based surface; see GENERATOR_RC1.md.

## Requirement catalog from initial design
| ID | Priority | Requirement |
| --- | --- | --- |
| SN-001 | P0 | net11.0 and C# 15, x64/Arm64 Native AOT |
| SN-002 | P0 | Checked addition/subtraction/multiplication/division/negation/conversion |
| SN-003 | P0 | Nonthrowing explicit arithmetic failures |
| SN-004 | P0 | byte through UInt128, signed/unsigned and native-sized integers |
| SN-005 | P0 | No allocations in qualified arithmetic paths |
| SN-006 | P0 | No reflection/runtime code generation in core |
| SN-007 | P0 | Reject/saturate/wrap/throw policies |
| SN-008 | P0 | Validate numeric range, not merely primitive overflow |
| SN-009 | P0 | Specify divide-by-zero and MinValue/-1 |
| SN-010 | P0 | Exhaustive boundary testing and independent oracle |
| SN-011 | P1 | Source-generated strongly typed numeric domains |
| SN-012 | P1 | C# 15 union outcomes and exhaustive handling |
| SN-013 | P1 | Roslyn analyzer enforcement |
| SN-014 | P1 | Binary serialization / Span integration |
| SN-015 | P1 | Benchmark performance budgets |
| SN-016 | P1 | Deterministic diagnostics |
| SN-017 | P1 | SharpUnits interoperability |
| SN-018 | P2 | Fixed-point arithmetic |
| SN-019 | P2 | Decimal floating-point |
| SN-020 | P2 | SMT/formal verification |

## Analyzer design
| Rule | Purpose |
| --- | --- |
| SSN1001 | Unsafe narrowing conversion in protected domain |
| SSN1002 | Unchecked arithmetic within Strict scope |
| SSN1003 | Ignored numeric result |
| SSN1004 | Throwing numeric API in Strict scope |
| SSN1005 | Implicit saturation/truncation |
| SSN1006 | Domain range bypass |
| SSN1007 | Potential allocation in qualified method |
| SSN1008 | Invalid default state of generated domain type |

## Verification and acceptance
Exhaust small integer domains; use `BigInteger` independent oracle for wider values, property tests, mutation testing, non-allocating success/failure path profiling, Native AOT for x64/Arm64 and deterministic replay. Benchmark against raw `checked`, manual guards and generic math. Target ≤10% overhead against equivalently safe manual guards in representative kernels, pending empirical qualification. Distinguish microbenchmark medians from worst-case execution times.

## Suggested development milestones
1. Arithmetic core and oracle tests.
2. Domain types, generated ranges, default-state validity, serialization.
3. C# 15 union adaptation, analyzer suite and enforceable policies.
4. Mutation, Native AOT, benchmarking and real RISCue/VeloSim qualification.

## Repository organization
```text
src/SharpSafeNumerics.Core/
src/SharpSafeNumerics.Unions/
src/SharpSafeNumerics.Generator/
src/SharpSafeNumerics.Analyzers/
tests/SharpSafeNumerics.Tests/
tests/SharpSafeNumerics.AotSmoke/
docs/PRODUCT_REQUIREMENTS.md
docs/DESIGN.md
docs/EXCEPTION_POLICY.md
docs/VERIFICATION_PLAN.md
docs/FEATURE_PARITY.md
```

## Licensing
MIT for this repository. Any future reuse of external code must follow its own license; the implementation is not a copied Boost library.

## Primary platform documentation
- https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15
- https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union
- https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries
- https://learn.microsoft.com/en-us/dotnet/api/system.numerics.inumberbase-1?view=net-11.0
