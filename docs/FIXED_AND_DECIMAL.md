# Fixed point and safe IEEE decimal APIs

Both features are opt-in through explicit APIs; the integer generator annotations
continue to describe integer domains. Fixed64 is in Core. Install the optional
SharpSafeNumerics.Decimal package for the three NET11 built-in IEEE decimal
formats. It references Core and the BCL, with no third-party arithmetic engine.
System.Decimal is a different representation and is not accepted by these APIs.

## Q32.32 fixed point

Fixed64 is an immutable signed raw Int64 divided by 4294967296. Every raw bit
pattern is valid, so default(Fixed64) is zero. NumericResult<Fixed64> still has
an invalid default. FromRaw admits a representation; FromInt64 and FromRatio
admit mathematical values. There are no implicit floating-point conversions.

```csharp
var half = SafeFixed.FromRatio(1, 2); // raw 2147483648, exact
var tick = SafeFixed.FromRatio(1, 60, FixedRounding.ToEven);
var doubled = SafeFixed.Add(half.Value, half.Value);
```

SafeFixed supplies Add, Subtract, Multiply, Divide and Negate, returning
NumericResult<Fixed64>. Multiplication and division use Int128 intermediates;
they do not saturate or wrap. Expected failures return statuses with zero raw
storage. Value intentionally throws on failure; TryGetValue avoids that access.

| Mode | Discarded fractional raw units |
| --- | --- |
| Exact (default) | Reject with Inexact |
| ToEven | Nearest; halfway chooses an even raw integer |
| ToZero | Truncate toward zero |
| Floor / Ceiling | Toward negative / positive infinity |
| AwayFromZero | Every nonzero remainder rounds away from zero, not only ties |

Undefined byte modes reject with OutOfRange before division-by-zero or numeric
paths. Exact fractional rejection precedes range validation. Otherwise rounding
precedes range validation: a value just beyond an endpoint may round back to it;
a carry across the endpoint fails. Overflow means above the upper bound;
Underflow means below the lower bound. These APIs allocate no managed storage
in the exercised warmed arithmetic paths; that is not an application timing proof.

## Decimal32, Decimal64 and Decimal128

SafeDecimal supplies TryAdd, TrySubtract, TryMultiply, TryDivide and TryNegate,
plus TryConvert<TSource, TDestination>. Supported generic types are exclusively
System.Numerics.Decimal32/64/128. Other implementations, including double,
reject with OutOfRange before invoking their numerical methods.

```csharp
var one = System.Numerics.Decimal64.One;
var three = System.Numerics.Decimal64.CreateChecked(3);
var exact = SafeDecimal.TryDivide(one, three); // Inexact, cleared value
var rounded = SafeDecimal.TryDivide(one, three, DecimalRounding.ToEven);
// rounded.IsSuccess and rounded.IsInexact; rounded.Value is nearest-even 1/3.
```

Exact is the default: an unrepresentable exact result fails with Inexact.
ToEven explicitly accepts the BCL's nearest-even finite result, with IsInexact
reporting whether its mathematical value changed. Tiny results rounded to signed
zero are inexact. NaN/infinite inputs fail with NonFinite; zero divisors fail
with DivideByZero; nonfinite overflow results fail directionally with Overflow
or Underflow. Invalid option/type rejection precedes operand errors. Failures
have default value storage and IsInexact=false. Default DecimalResult is invalid.

Exactness is checked against exact BigInteger rational relations extracted from
invariant roundtrip decimal text. This avoids rounded reverse-operation tests.
**Decimal checks allocate** for formatting and arbitrary-precision temporaries.
They are outside the strict integer/fixed-point allocation contract. The optional
assembly keeps that cost separate. BCL decimal arithmetic remains Microsoft's
implementation; this package owns admission, result contracts and qualification.

This surface does not promise all IEEE754 operations/status flags, NaN payload
preservation, quantum/cohort identity, arbitrary directed decimal rounding,
transcendentals or a general decimal source generator. Negative zero follows BCL
value operations. Numerical exactness compares mathematical values, not encodings.

Run examples/FixedAndDecimal for a complete entry-point example. Requirements
SN-018-FIXED and SN-019-DECIMAL are in requirements.sdoc. The pinned workstation
integer soak predates these APIs and cannot qualify them. See the
[numeric expansion verification report](verification/2026-11-17-fixed-decimal.md)
for exact commands, scopes and remaining limitations.
