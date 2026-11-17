# Verification matrix

| Concern | Primary | Independent check | Status |
| --- | --- | --- | --- |
| 8-bit arithmetic | exhaustive loops | integer oracle | green before M2 |
| 32-bit arithmetic | xUnit boundaries | CsCheck + BigInteger | implemented |
| Built-in conversion pairs | 144-pair xUnit matrix, seed 731921 | BigInteger admission oracle; CsCheck + Hedgehog | locally green |
| Shrinking | CsCheck | Hedgehog | implemented |
| Domain generation | executable generated example | generator diagnostics | implemented |
| Strict policy | Roslyn analyzer | analyzer self-test | implemented |
| Mutation sensitivity | Stryker.NET | score threshold 90% | locally 91.15%; survivors documented |
| Arithmetic specification | Dafny | C# properties | modeled |
| Allocation | GC byte count, all conversion pairs | BenchmarkDotNet MemoryDiagnoser | warmed local matrix green; conversion benchmarks report zero bytes |
| AOT | linux-x64 smoke | Windows x64 local Native AOT smoke | local Windows execution and baseline Linux CI |
| Core coverage | Coverlet 10.1.1 | mutation regressions | 98/99 lines; 94/97 branches |
| WCET | none yet | target hardware | not qualified |

Formal verification proves the Dafny specification, not the C# implementation. The relationship is strengthened through shared golden/property semantics but is not a machine-checked refinement proof.
