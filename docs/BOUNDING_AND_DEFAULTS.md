# Bounded domains: valid-by-construction and the default problem

`Bounded<T, TRange>` uses .NET's static abstract interface members rather than runtime reflection. A static range policy provides `Minimum` and `Maximum` at compile time. `TryCreate` rejects out-of-domain inputs, and `TryAdd` / `TrySubtract` check primitive arithmetic *then* the domain range.

**Crucial invariant:** `default(Bounded<T,TRange>)` is intentionally INVALID. All strict callers must check `IsValid` or `TryGetValue` before using a value. This avoids pretending that zero is valid for arbitrary range domains.

This provides a useful stopgap until source generators produce named values such as `QueueDepth`, `TimerTicks`, or `PacketLength`. Range policies are currently required to be well-formed; future generator/analyzer rules should reject `Minimum > Maximum` during compilation.

Potential risk: storing `T` and a validity flag increases struct size relative to the raw integer; measure copy overhead and AOT layout on target hardware. No benchmark claim has been made.
