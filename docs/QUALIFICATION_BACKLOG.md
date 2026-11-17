# RC-1 qualification backlog

Implemented RC-1 contracts and bounded verification evidence are recorded in the
[RC report](verification/2026-11-17-rc1.md), [generator report](GENERATOR_RC1.md)
and [analyzer report](ANALYZERS_RC1.md). Historical conversion-only gaps have been
superseded by wide BigInteger campaigns, policy validation, generated codecs/text,
and all eight strict diagnostics.

| Priority | Remaining qualification | Evidence needed |
| --- | --- | --- |
| P0 | Application allocation and hard real-time behavior | Representative target kernels, contention and timing audit |
| P1 | Final-source compiler-extension mutation breadth | Resolve remaining symbol/formatting guards and diagnostic variants; full scores 89.89% generator / 85.82% analyzer precede the last diagnostic-only test hardening |
| P1 | Release reproducibility and public API baselines | Package consumer checks passed; corresponding archive entry bytes matched, but whole-archive hashes differed; public API baselines remain open |
| P1 | Production SIMD performance | Matched output/status layout and representative workload timings |
| P1 | Application integration | Actual RISCue/VeloSim/SharpUnits consumers, beyond repository examples |
| P1 | Additional architecture coverage | 32-bit runtime execution and native-width contracts |

Analyzer static detection has documented dataflow and indirect-allocation limits.
Dafny models do not prove C# implementation equivalence. Coverage and finite
campaigns do not establish safety certification or universal provider conformance.
