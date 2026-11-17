# Feature Parity and Implementation Status

| Feature | State | Evidence / caveat |
| --- | --- | --- |
| .NET 11 project targeting | Implemented in project files | CI compilation pending |
| Generic fixed-width add/sub/mul/div/negate | Implemented | Exhaustive harness present; execution pending |
| TryConvert with checked conversion | Implemented | Catches and translates OverflowException; failure may allocate |
| Range admission | Implemented | OutOfRange result |
| Reject / Saturate / Wrap / Throw for add/sub/mul | Implemented | Not yet per-operation benchmarked |
| Non-boxing NumericResult<T> | Implemented | Default is invalid |
| C#15 union convenience adapter | Implemented in separate project | Generated value-type unions box |
| Generic decimal support | Planned | Not in integer core |
| Static range-constrained bounded value type | Implemented | default invalid; CI tests added |
| Bounded type source generator | Initial implementation | BoundedIntegerAttribute + executable generated example |
| Strict-scope Roslyn analyzers | Initial implementation | SSN1002/1003/1004 + analyzer self-test |
| Binary codecs | Planned | Not claimed |
| Exhaustive small integer tests | Harness present | Not run in authoring container |
| Wide integer oracle | Partial | More tests required |
| x64/Arm64 AOT | Linux x64 smoke project | Not yet built or executed |
| Managed allocation validation | Narrow 100k-iteration test added | CI result pending; not whole-library guarantee |
| Performance budget | Planned | Not measured |
| Formal verification | Initial Dafny model | add/sub/mul/div + bounded add; not a refinement proof |
\n| CsCheck 5.0.1 | Integrated | randomized + shrinking BigInteger oracle |\n| Hedgehog 2.0.4 | Integrated | independent property generator/shrinker |\n| Stryker.NET 5.1.0 | Configured | scheduled/manual mutation workflow |\n| BenchmarkDotNet | Configured | manual benchmark workflow |\n