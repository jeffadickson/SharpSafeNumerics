# Verification matrix

| Concern | Primary | Independent check | Current evidence |
| --- | --- | --- | --- |
| Wide arithmetic/policies | All 12 built-in types, boundaries and seeded UInt128 pairs | BigInteger; CsCheck and Hedgehog | RC report |
| Conversion pairs | 144-pair boundary matrix | BigInteger admission oracle | Conversion and RC reports |
| Generated domains | 88 Roslyn/runtime tests | Golden codecs, malformed input, seeded round trips | Generator report |
| Strict policy | 108 analyzer tests, SSN1001–1008 | Positive/negative operation and metadata fixtures | Analyzer report |
| SIMD batch addition | Poison buffers, overlaps, dispatch ceilings | BigInteger lane oracle | RC report |
| Mutation sensitivity | Stryker.NET Standard | Survivor dispositions and regression hardening | Scores retain source/scope distinctions in reports |
| Arithmetic specification | Dafny, 12 obligations verified | C# property tests | No refinement proof |
| Allocation | Warmed GC byte counts | BenchmarkDotNet MemoryDiagnoser | Bounded API cases only |
| Native AOT/platforms | Windows/Linux x64 and Linux ARM64 | Gated one-time macOS ARM64 | Run IDs in RC report |
| Coverage | Coverlet line/branch metrics | Mutation and negative tests | Per-assembly scope in reports |
| WCET | Target hardware required | Application workload | Open |

The [RC report](verification/2026-11-17-rc1.md) records exact counts, source scope,
commands and limitations. Historical measurements remain historical evidence.
