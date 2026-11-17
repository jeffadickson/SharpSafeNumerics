# RC-1 qualification backlog

Implemented RC-1 contracts and bounded verification evidence are recorded in the
[RC report](verification/2026-11-17-rc1.md), [generator report](GENERATOR_RC1.md)
and [analyzer report](ANALYZERS_RC1.md). Historical conversion-only gaps have been
superseded by wide BigInteger campaigns, policy validation, generated codecs/text,
and all eight strict diagnostics.

| Priority | Remaining qualification | Evidence needed |
| --- | --- | --- |
| P0 | Application allocation and hard real-time behavior | Representative target kernels, contention and timing audit |
| P1 | Final-source compiler-extension mutation breadth | Full Standard gates passed: 90.71% generator / 92.29% analyzer. Retain dispositions for remaining guarded/equivalent variants, uncovered paths and timeouts |
| P1 | Release reproducibility and public API baselines | Same-checkout rebuild reproducibility is measured separately for each package set. Shipping source signatures and targeted generic metadata are guarded; general binary/released-package compatibility and cross-path/platform identity remain open |
| P1 | Production SIMD performance | Matched production-layout local timings are recorded; target-specific budgets and lower-variance campaigns remain open |
| P1 | Application integration | Actual RISCue/VeloSim/SharpUnits consumers, beyond repository examples |
| P1 | Additional architecture coverage | 32-bit runtime execution and native-width contracts |
| P1 | Expanded RC1 fixed/decimal qualification | Q32.32 and safe BCL Decimal32/64/128 need their own final-source evidence; the pinned integer soak predates them |
| P1 | SharpUnits integration SN-017 | Real referenced integration sample and compilation; Unit display metadata is insufficient |

Analyzer static detection has documented dataflow and indirect-allocation limits.
Dafny models do not prove C# implementation equivalence. Coverage and finite
campaigns do not establish safety certification or universal provider conformance.
