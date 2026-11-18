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

## AVX-512 comparison (2026-11-18)

GitHub Actions run 44392125979 tested commit 956f927 on Ubuntu x64, AMD
EPYC 9V74, .NET 11.0.0 and BenchmarkDotNet 0.15.8. ShortRun used three
warmups and three measurement iterations. Both methods validate the buffer
contract and write byte-sized lane statuses, with deterministic seed 731923.
Setup checked every lane against a widened integer oracle and asserted
each method's executed width. The AVX-512 prototype delegates its tail to
the validated public batch entry point.

| Lanes | SIMD256 mean | AVX-512 mean |
| ---: | ---: | ---: |
| 17 | 7.839 ns | 11.142 ns |
| 257 | 83.164 ns | 31.743 ns |
| 4099 | 766.519 ns | 555.860 ns |
| 262147 | 57741.757 ns | 55407.580 ns |

No managed allocation was measured. The large-batch difference is small
relative to measurement uncertainty. The Windows hosted EPYC 7763 lacked
the necessary acceleration and produced no timing. The local i5-10505
supports AVX2. These measurements do not establish benefits across CPUs
or a universal optimal threshold. Production uses 256 lanes as an initial
conservative threshold and reports the widest loop actually executed.

Local fallback verification: 201 Core unit tests passed on the i5-10505,
including an explicit SIMD256 execution assertion. Core coverage was 94.06%
line and 95.10% branch on this machine; AVX-512 instructions were unavailable.
The five-second seeded fallback soak passed 2,182,907 batch lanes (seed 731925).
The initial Linux fallback check caught use of the wrong runtime switch:
.NET 11 reads DOTNET_EnableAVX512. The assertion observed 512 instead of 256
until the workflow was corrected.
