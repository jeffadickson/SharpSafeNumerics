# Exception Policy

**Default: Balanced.** Strict is an opt-in architectural profile; no blanket runtime exception ban is attempted.

| Profile | Expected overflow | Invalid API contract | Allocation budget |
| --- | --- | --- | --- |
| Strict | error value only | subsystem fault policy | verified 0 bytes on designated paths |
| Balanced | error value at boundaries | exception permitted | designated paths only |
| Convenient | value or throwing wrapper | rich exception permitted | unrestricted |

- `TryAdd`, `TrySubtract`, `TryMultiply`, `TryDivide`, `TryNegate` must not emit exceptions for normal overflow/underflow/zero-divisor when applied to qualified built-in integer types.
- `TryConvert` does not propagate overflow exceptions, but currently allocates when exceptions occur; not strict-qualified.
- `NumericResult<T>.Value` throws for an unsuccessful result; Strict consumers should use `TryGetValue`.
- `ArithmeticPolicy.Throw` intentionally creates an exception; forbidden by future strict-scope analyzer.
- Reject/saturate/wrap behavior must be caller-selected and visible in code. No implicit fallbacks.
- Unexpected corrupted internal state may be escalated via explicit caller policy; not silently suppressed.
