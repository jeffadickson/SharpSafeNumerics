# Feature Parity and Implementation Status

| Feature | State | Evidence / caveat |
| --- | --- | --- |
| .NET 11 project targeting | Built locally and in baseline CI | Root solution contains all 12 projects |
| Generic fixed-width add/sub/mul/div/negate | Implemented | 693,195 exhaustive assertions plus all-12-type wide BigInteger campaigns |
| Exact TryConvert | Implemented | 144 built-in pairs; BigInteger oracle; warmed success/rejection allocation checks |
| Range admission | Implemented | OutOfRange result |
| Reject / Saturate / Wrap / Throw for add/sub/mul | Implemented | Not yet per-operation benchmarked |
| Non-boxing NumericResult<T> | Implemented | Default is invalid |
| C#15 union convenience adapter | Implemented in separate project | Generated value-type unions box |
| Safe IEEE decimal support | Implemented; expanded RC1 qualification | Optional BCL Decimal32/64/128 finite wrapper; exact/default or explicit nearest-even with inexactness; may allocate |
| Signed Q32.32 fixed point | Implemented; expanded RC1 qualification | Owned Int128 intermediate arithmetic, six rounding modes, BigInteger references |
| SharpUnits integration SN-017 | Referenced source example implemented | Exact whole-volt ingress through ±2^53, real dimensional checks; pinned development source, not general exact unit arithmetic |
| Static range-constrained bounded value type | Implemented | default invalid; CI tests added |
| Bounded type source generator | RC-1 supported contract implemented | Policies/defaults/text/codecs/conversion/operators; explicit unsupported-shape diagnostics; see GENERATOR_RC1.md |
| Strict-scope Roslyn analyzers | SSN1001–SSN1008 implemented | Proper Roslyn unit tests, actual generated/cross-assembly metadata; static limits in ANALYZERS_RC1.md |
| Binary codecs | Implemented for generated domains | Fixed width, signedness and endian golden/reference vectors |
| Exhaustive small integer tests | Executed on Windows x64 | 693,195 assertions; later all-12-type wide campaigns remain finite evidence |
| Wide integer oracle | All 12 built-in types exercised | 62,220 deterministic pairs, additional independently generated wide type pairs; finite evidence |
| x64/Arm64 AOT | Prior integer source executed on Windows/Linux x64, Linux ARM64 and one macOS ARM64 run | New fixed/decimal source needs final CI evidence; macOS remains off |
| Managed allocation validation | Conversion matrix and narrow arithmetic check | Runtime-specific; not whole-library guarantee |
| Performance budget | Target budgets open | Local matched SIMD timings exist; no application WCET or cross-architecture performance qualification |
| Formal verification | Twelve integer obligations and four fixed-point obligations verified locally | Mathematical models, not CSharp refinement; fixed model also generated/executed CSharp smoke |
| CsCheck 5.0.1 | Executed | 42,000 property-program cases; fixed replay seed; analyzer suite adds its own campaign |
| Hedgehog 2.0.4 | Executed | 4,000 cases; independent generator/shrinker |
| Stryker.NET 5.1.0 | Executed on Core, generator and analyzer | Prior full compiler gates passed; expanded fixed/decimal/analyzer campaigns recorded separately |
| BenchmarkDotNet | In-process ShortRun executed | Fast methods approach measurement floor; no timing qualification |

Exact scope, limitations and dated results: [conversion pass](verification/2026-11-17-conversion.md).

RC-1 integration/platform/package evidence: [RC report](verification/2026-11-17-rc1.md).

SharpUnits SN-017 now has a real pinned-source example; see its scoped evidence.
SN-018/SN-019 were included in RC1
by the user's later scope decision; their bounded implementation contracts are
SN-018-FIXED and SN-019-DECIMAL, not general fixed-point or full IEEE754 coverage.
See [fixed and decimal contracts](FIXED_AND_DECIMAL.md).
