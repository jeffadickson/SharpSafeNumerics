# Attributes + source generation

Attributes are the **declarative contract surface**. Source generation is the **mechanism that makes the contract cheap at runtime**.

Example:

```csharp
[BoundedInteger(typeof(int), "0", "63")]
public readonly partial struct QueueDepth;
```

The generator emits storage, an explicit validity bit, minimum/maximum constants, `TryCreate`, `TryAdd`, `TrySubtract`, `TryGetValue`, and diagnostics for malformed declarations.

This is more powerful than a generator configured only through project files because the numeric policy lives next to the domain declaration and is visible to analyzers, IDE tooling, documentation generators and code review. It is more efficient than runtime attribute inspection because the runtime core performs **no reflection**.

## Planned annotation dimensions
- bounds and storage type (implemented)
- overflow policy (attribute property exists; generated policy helpers pending)
- strict-scope enforcement (`StrictNumericAttribute`, implemented)
- parsing/formatting policy
- serialization width/endianness
- units interoperability
- whether zero is a valid default versus explicit-invalid default

Attributes must describe policy; they must not hide runtime behavior. Any option that can silently truncate, wrap or saturate requires an explicit named policy.
