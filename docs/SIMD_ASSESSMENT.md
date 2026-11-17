# SIMD assessment for RC-1

The production API remains scalar and portable. SIMD requires a batch contract
covering per-lane failures, cleared outputs, overlaps, tails and hardware fallback;
inserting intrinsics into a single scalar generic call has no demonstrated benefit.

A bounded BCL-only `Vector256<int>` addition prototype lives in the benchmark
project and is linked into unit tests. It keeps wrapping candidate bits internal,
detects signed overflow with `((a ^ sum) & (b ^ sum)) < 0`, writes the same per-lane
status and cleared failure value as the scalar API, rejects overlapping output or
mismatched buffers before writes (read-only inputs may alias), and retains a scalar fallback/tail. It is not shipped as a
runtime API. Tests use BigInteger, forced scalar dispatch and vector-width neighbors.
Benchmarks compare identical mixed-input result/status-buffer work; hardware
availability alone is not a performance result or disassembly confirmation.

Existing facilities considered: generic-math scalar code (already used), variable
width Vector<T> (portable lanes), fixed-width Vector256<T> (explicit dispatch), and
platform-specific AVX2/AdvSimd (more qualification burden). Fixed-width BCL APIs
are sufficient for this experiment; no package or ISA-specific production path is
justified before target-specific benchmark and generated-code evidence.

Primary documentation:
- https://learn.microsoft.com/en-us/dotnet/standard/simd
- https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.vector256
- https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.vector256.ishardwareaccelerated

No SIMD support is inferred for Int128/UInt128, integer division, or the complete
numeric policy surface. Measurements and hardware scope belong in the dated RC report.
