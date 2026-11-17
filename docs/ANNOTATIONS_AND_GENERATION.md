# Attributes + source generation

Attributes are the **declarative contract surface**. Source generation is the **mechanism that makes the contract cheap at runtime**.

Example:

```csharp
[BoundedInteger(typeof(int), "0", "63")]
public readonly partial struct QueueDepth;
```

The generator emits storage, explicit validity, bounds, exact construction and conversion, arithmetic, equality/order operators, invariant span parsing/formatting, fixed-width endian codecs, XML documentation and debugger display. See [the RC-1 contract](GENERATOR_RC1.md) for signatures, supported declaration shapes, policy semantics and executable evidence.

This is more powerful than a generator configured only through project files because the numeric policy lives next to the domain declaration and is visible to analyzers, IDE tooling, documentation generators and code review. It is more efficient than runtime attribute inspection because the runtime core performs **no reflection**.

## Implemented RC-1 annotation dimensions
- Bounds and built-in storage type, including 128-bit extrema and portable native-width validation.
- Explicit `OverflowPolicy` consumed by generated `Add`/`Subtract`; Try arithmetic always rejects.
- `DefaultPolicy`: invalid default or validated zero-valid default.
- `TextPolicy`: invariant decimal or hexadecimal span parsing and formatting.
- `SerializationWidth` and `Endianness`: range-checked fixed-width codecs.
- Optional `Unit` metadata; numeric values are unchanged and unit conversion is not implied.
- Exact generic conversion helpers with explicit failure status.
- `StrictNumericAttribute` and [SSN1001–SSN1008](ANALYZERS_RC1.md), with documented static detection scope.

Nested/generic and other unsupported declaration shapes receive diagnostics rather
than malformed generated source. Reflection-based converters are not part of the
contract. Integration with SharpUnits remains separate from display metadata.

Attributes must describe policy; they must not hide runtime behavior. Any option that can silently truncate, wrap or saturate requires an explicit named policy.
