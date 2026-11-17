# Design Decisions

## Numeric core
`SafeInteger` accepts fixed-width `IBinaryInteger<T>` with `IMinMaxValue<T>`. It checks bounds *before* integer arithmetic to avoid relying on exceptions for overflow and underflow. Outputs are initialized deterministically on failure.

Signed multiplication uses sign cases and quotient-based bounds. Division detects zero and the signed min/-1 case. Unsigned negation rejects nonzero operands. Conversion forms a `CreateTruncating` candidate, checks matching signs and exact conversion back to the source, and publishes only exact values. The candidate is internal; a changed numeric value never escapes. This avoids expected range exceptions for built-in integers without reflection, runtime BigInteger intermediates, buffers, or extra generic constraints. See [conversion design and alternatives](CONVERSION_PLAN.md).

## Result storage
`NumericResult<T>` is a readonly struct containing status and value. `default(NumericResult<T>)` has `Uninitialized`, not success. `Value` throws for misuse; strict paths use `TryGetValue`. The C#15 `union` wrapper is separated into its own assembly; generated unions box value-type cases (documented C#15 behavior).

## Policy
Reject returns failure; Saturate returns Min/Max; Wrap executes explicit unchecked arithmetic; Throw creates an OverflowException. Policies are per operation, never ambient global state. Invalid policy throws programmer-contract exception.

## Limitations
The current policy API handles addition, subtraction and multiplication, not policy-driven division or negation. Range-aware source generation, analyzers, decimal and formal proof support are future deliverables. All generic math providers outside built-in integral primitives require explicit qualification.

## Sources
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union
https://learn.microsoft.com/en-us/dotnet/api/system.numerics.ibinaryinteger-1?view=net-11.0
