# Design Decisions

## Numeric core
`SafeInteger` accepts fixed-width `IBinaryInteger<T>` with `IMinMaxValue<T>`. It checks bounds *before* integer arithmetic to avoid relying on exceptions for overflow and underflow. Outputs are initialized deterministically on failure.

Signed multiplication uses sign cases and quotient-based bounds. Division detects zero and the signed min/-1 case. Unsigned negation rejects nonzero operands. Conversion forms a `CreateTruncating` candidate, checks matching signs and exact conversion back to the source, and publishes only exact values. The candidate is internal; a changed numeric value never escapes. This avoids expected range exceptions for built-in integers without reflection, runtime BigInteger intermediates, buffers, or extra generic constraints. See [conversion design and alternatives](CONVERSION_PLAN.md).

## Result storage
`NumericResult<T>` is a readonly struct containing status and value. `default(NumericResult<T>)` has `Uninitialized`, not success. `Value` throws for misuse; strict paths use `TryGetValue`. The C#15 `union` wrapper is separated into its own assembly; generated unions box value-type cases (documented C#15 behavior).

## Policy
Reject returns failure; Saturate returns Min/Max; Wrap executes explicit unchecked arithmetic; Throw creates an OverflowException. Policies are per operation, never ambient global state. Invalid policy throws programmer-contract exception.

## Batch arithmetic
Int32 AddBatch/TryAddBatch validate buffers before writing, detect Vector256 then
Vector128, and retain scalar tails/fallback. Per-lane failure clears its value.
BatchArithmeticResult returns execution metadata by value with an invalid default,
avoiding aliasable diagnostic out storage. Numeric enum values remain stable.
Generic and generated bounded arithmetic snapshot the receiver before publishing
an out result so callers may update a bounded value in place.

## Limitations
The current policy API handles addition, subtraction and multiplication, not policy-driven division or negation. RC-1 generated domains consume arithmetic/default/text/codec contracts, and SSN1001–SSN1008 have operation-based enforcement within the documented static detection limits. See GENERATOR_RC1.md and ANALYZERS_RC1.md. Dafny verifies mathematical models, including conversion admission, without proving equivalence to C#. Decimal support remains planned. All generic math providers outside built-in integral primitives require explicit qualification.

## Sources
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union
https://learn.microsoft.com/en-us/dotnet/api/system.numerics.ibinaryinteger-1?view=net-11.0
