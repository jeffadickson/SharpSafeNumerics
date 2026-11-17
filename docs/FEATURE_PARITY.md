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
| Generic decimal support | Planned | Not in integer core |
| Static range-constrained bounded value type | Implemented | default invalid; CI tests added |
| Bounded type source generator | RC-1 supported contract implemented | Policies/defaults/text/codecs/conversion/operators; explicit unsupported-shape diagnostics; see GENERATOR_RC1.md |
| Strict-scope Roslyn analyzers | SSN1001–SSN1008 implemented | Proper Roslyn unit tests, actual generated/cross-assembly metadata; static limits in ANALYZERS_RC1.md |
| Binary codecs | Implemented for generated domains | Fixed width, signedness and endian golden/reference vectors |
| Exhaustive small integer tests | Executed on Windows x64 | 693,195 assertions; wide arithmetic remains partial |
| Wide integer oracle | All 12 built-in types exercised | 62,220 deterministic pairs, additional independently generated wide type pairs; finite evidence |
| x64/Arm64 AOT | Windows x64 smoke locally executed; Linux baseline CI green | Arm64 remains unverified |
| Managed allocation validation | Conversion matrix and narrow arithmetic check | Runtime-specific; not whole-library guarantee |
| Performance budget | Planned | Not measured |
| Formal verification | Six locally/CI verified Dafny model methods | add/sub/mul/div, bounded add, conversion admission; not a refinement proof |
| CsCheck 5.0.1 | Executed | 42,000 property-program cases; fixed replay seed; analyzer suite adds its own campaign |
| Hedgehog 2.0.4 | Executed | 4,000 cases; independent generator/shrinker |
| Stryker.NET 5.1.0 | Executed on Core | 94.12%, seven retained equivalent candidates, no uncovered mutants; tooling campaigns separately scoped |
| BenchmarkDotNet | In-process ShortRun executed | Fast methods approach measurement floor; no timing qualification |

Exact scope, limitations and dated results: [conversion pass](verification/2026-11-17-conversion.md).

RC-1 integration/platform/package evidence: [RC report](verification/2026-11-17-rc1.md).
