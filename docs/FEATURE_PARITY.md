# Feature Parity and Implementation Status

| Feature | State | Evidence / caveat |
| --- | --- | --- |
| .NET 11 project targeting | Built locally and in baseline CI | Root solution contains all 12 projects |
| Generic fixed-width add/sub/mul/div/negate | Implemented | Local exhaustive harness: 693,195 assertions |
| Exact TryConvert | Implemented | 144 built-in pairs; BigInteger oracle; warmed success/rejection allocation checks |
| Range admission | Implemented | OutOfRange result |
| Reject / Saturate / Wrap / Throw for add/sub/mul | Implemented | Not yet per-operation benchmarked |
| Non-boxing NumericResult<T> | Implemented | Default is invalid |
| C#15 union convenience adapter | Implemented in separate project | Generated value-type unions box |
| Generic decimal support | Planned | Not in integer core |
| Static range-constrained bounded value type | Implemented | default invalid; CI tests added |
| Bounded type source generator | Initial implementation | BoundedIntegerAttribute + executable generated example |
| Strict-scope Roslyn analyzers | Initial implementation | SSN1002/1003/1004 + analyzer self-test |
| Binary codecs | Planned | Not claimed |
| Exhaustive small integer tests | Executed on Windows x64 | 693,195 assertions; wide arithmetic remains partial |
| Wide integer oracle | Partial | More tests required |
| x64/Arm64 AOT | Windows x64 smoke locally executed; Linux baseline CI green | Arm64 remains unverified |
| Managed allocation validation | Conversion matrix and narrow arithmetic check | Runtime-specific; not whole-library guarantee |
| Performance budget | Planned | Not measured |
| Formal verification | Six locally/CI verified Dafny model methods | add/sub/mul/div, bounded add, conversion admission; not a refinement proof |
| CsCheck 5.0.1 | Executed | 40,000 cases; fixed replay seed |
| Hedgehog 2.0.4 | Executed | 3,000 cases; independent generator/shrinker |
| Stryker.NET 5.1.0 | Executed | 91.15%; gate raised to 90%; surviving mutants retained |
| BenchmarkDotNet | In-process ShortRun executed | Fast methods approach measurement floor; no timing qualification |

Exact scope, limitations and dated results: [conversion pass](verification/2026-11-17-conversion.md).
