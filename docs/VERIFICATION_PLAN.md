# Verification Plan

## Test tiers
1. Compile Core and C# 15 union adapter against actual .NET 11 SDK.
2. Exhaust all 65,536 byte operand pairs for add/sub/mul/div, and all signed-byte operand pairs for add/sub/mul.
3. Add BigInteger oracle tests for signed/unsigned 16/32/64/128-bit values, including edge triplets and randomized seeded values.
4. For strict operations, run 100k iterations and measure `GC.GetAllocatedBytesForCurrentThread()` on both success and error paths, with warmed delegates and anti-elision.
5. Verify saturate/wrap/reject/throw against reference semantics for each built-in type.
6. Test `default(NumericResult<T>)`, failure out-value clearing and invalid policy rejection.
7. Build and run Native AOT on linux-x64, then windows-x64 and linux-arm64 where available; test trimming.
8. Mutation tests: flip each >, <, >=, <= and overflow boundary to ensure detection.
9. Benchmarks vs checked arithmetic, manual guards, and generic math under controlled conditions.
10. RISCue and VeloSim integration tests, deterministic trace replay, instrumentation of allocations and latency.

## Current state
The Windows x64 .NET 11 SDK executes the harness and unit/property suites locally.
Conversion now has all 144 ordered built-in type-pair boundary and seeded oracle
campaigns, exhaustive 8-bit sources, and 100k-call warmed allocation checks per
success/rejection path. Rejection is impossible for wholly widening source ranges.
Coverlet collects core line and branch coverage through the xUnit project.
See [dated evidence and reproducible commands](verification/2026-11-17-conversion.md).
The numerical API has not received a certified safety review.

## Important validation boundary
A real-time guarantee requires target-specific WCET analysis, scheduler analysis, memory and exception audit. Microbenchmarks alone cannot supply that evidence.
