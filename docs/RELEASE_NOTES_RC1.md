# 0.1.0-rc.1 release notes

Candidate packages: SharpSafeNumerics.Core, SharpSafeNumerics.Generator,
SharpSafeNumerics.Analyzers, SharpSafeNumerics.Unions and the optional
SharpSafeNumerics.Decimal. Target: .NET 11 / C# 15.
This document prepares the release; it does not indicate a published NuGet version.

## Implemented

- Exact arithmetic and conversion for all 12 built-in integer types, including
  Int128/UInt128 and native-sized integers, with independent BigInteger oracles.
- Explicit Reject/Saturate/Wrap/Throw arithmetic policies; undefined policy bytes
  reject before both successful and failing arithmetic.
- Generated bounded domains consume arithmetic/default/text/binary annotations,
  provide span parsing/formatting, fixed-width endian codecs, exact conversions,
  equality/order and unit metadata, without runtime reflection.
- SSN1001–SSN1008 enforce documented statically detectable contracts.
- Owned signed Q32.32 fixed-point arithmetic, Int128 intermediates, explicit
  exact/nearest-even/directed rounding and cleared typed failures.
- Safe finite BCL Decimal32/64/128 arithmetic and cross-format conversions:
  exact by default, explicit nearest-even acceptance with inexactness reporting.
  Optional decimal exactness checks allocate; integer/fixed hot paths stay separate.
- SSN1003 recognizes decimal results; SSN1007 warns for known allocating decimal
  arithmetic/conversion helpers in strict scopes, excluding nonallocating negation.
- Int32 batch addition detects SIMD256/SIMD128 and retains scalar fallback/tails,
  per-lane error direction and cleared failures. Buffer-contract errors preserve
  destinations; execution metadata returns by value with an invalid default.
- In-place bounded arithmetic and overlapping parser/decoder input/result storage
  are supported. Write/format destination and count output storage must be distinct.
- Package-only consumer checks, Windows/Linux x64 and Linux ARM64 Native AOT,
  and one explicitly gated macOS ARM64 qualification run.

Build-time public API lists track five shipping projects, including nullable
signatures, enum values and optional defaults. Targeted generic metadata tests
guard current contracts; general released-package binary compatibility remains
separate qualification work.

## Candidate API correction

The development-only five-argument TryAddBatch overload with an out execution
width was replaced by AddBatch returning BatchArithmeticResult. This prevents
metadata output from aliasing and corrupting numeric buffers. Four-argument
TryAddBatch and existing BatchArithmeticError numeric values are retained.

## Evidence and remaining limits

See [RC qualification](verification/2026-11-17-rc1.md) for exact source revisions,
commands, seeds, counts, coverage, mutation scores and CI run IDs. Expanded RC1
fixed/decimal evidence is recorded [separately](verification/2026-11-17-fixed-decimal.md).
The expanded local suite passed437 cases; Core/decimal/analyzer mutation gates
passed at93.30%/92.31%/91.80%, with retained survivors and separately reported timeouts.
The earlier 384-case integer snapshot passed a final 15-minute
SIMD256 soak plus shorter SIMD128/scalar replays. Default-hardware Core mutation
CI passed the unchanged 90% gate. Full Standard generator/analyzer mutation campaigns passed their 90% gates at
90.71% / 92.29%; survivors, uncovered paths and timeouts remain documented.

Portable Native AOT images executed SIMD128 in the observed platform runs;
managed x64 execution also exercised SIMD256. SIMD support covers Int32 batch
addition, and does not vectorize all generic operations or policies.

Analyzers have explicit dataflow/indirect-allocation limits. Custom generic-math
providers, 32-bit runtime execution, application WCET and safety certification
are not qualified by these finite campaigns. Dafny proves mathematical models,
not C# implementation refinement. Fixed package timestamps now produce byte-identical archives across repeated
builds in the same checkout/configuration; cross-path/platform identity is not claimed. The optional union adapter boxes value-type cases.

macOS qualification is gated off again; normal platform CI retains Windows and
Linux. No release tag or NuGet publication is implied by this candidate's checks.
