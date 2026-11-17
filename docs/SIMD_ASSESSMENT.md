# SIMD assessment for RC-1

The BCL-only production `SafeInteger.AddBatch` and `TryAddBatch` APIs add Int32
buffers with per-lane ArithmeticError values and cleared failure outputs.
Dispatch selects hardware-accelerated Vector256, then Vector128, then scalar;
short tails remain scalar. BatchAddVectorWidth reports maximum supported width,
while the value-returning BatchArithmeticResult reports the width actually used.
Its default state is invalid. Returning metadata by value avoids an out parameter
aliasing numeric input or output storage.

Lengths, capacities, byte-count bounds and overlapping written prefixes are
validated before writes. Read-only inputs may alias; unused destination suffixes
remain untouched. Overflow uses ((a ^ sum) & (b ^ sum)) < 0 and does not expose
wrapping candidate bits as successful results. Tests compare BigInteger lane
oracles, poison tails and scalar/128/256 ceilings without forcing unsupported ISAs.

Reuse assessment: existing generic math handles scalar semantics; fixed-width
BCL intrinsics provide explicit dispatch without runtime packages. Vector<T> was
considered for variable-width portability; ISA-specific AVX2/AdvSimd would add
qualification burden without a demonstrated need. The benchmark-only prototype
uses a different status layout, so its speedups are not production measurements.
See the dated RC report for measured scope and limitations.

Primary documentation:
- https://learn.microsoft.com/en-us/dotnet/standard/simd
- https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.vector128.ishardwareaccelerated
- https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.vector256.ishardwareaccelerated

SIMD support here covers Int32 batch addition. Scalar APIs retain their semantics;
Int128/UInt128, division and other policies have no vector implementation claim.
