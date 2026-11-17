# Verification matrix

| Concern | Primary | Independent check | Status |
| --- | --- | --- | --- |
| 8-bit arithmetic | exhaustive loops | integer oracle | green before M2 |
| 32-bit arithmetic | xUnit boundaries | CsCheck + BigInteger | implemented |
| Shrinking | CsCheck | Hedgehog | implemented |
| Domain generation | executable generated example | generator diagnostics | implemented |
| Strict policy | Roslyn analyzer | analyzer self-test | implemented |
| Mutation sensitivity | Stryker.NET | score threshold | configured |
| Arithmetic specification | Dafny | C# properties | modeled |
| Allocation | GC byte count | BenchmarkDotNet MemoryDiagnoser | narrow check + benchmark project |
| AOT | linux-x64 smoke | Windows runner | workflow coverage |
| WCET | none yet | target hardware | not qualified |

Formal verification proves the Dafny specification, not the C# implementation. The relationship is strengthened through shared golden/property semantics but is not a machine-checked refinement proof.
