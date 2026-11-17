# Qualification backlog after the conversion pass

These items are open; a green conversion pass does not close them.

| Priority | Gap | Next evidence needed |
| --- | --- | --- |
| P0 | Wide arithmetic and policy qualification is incomplete | All built-in add/sub/mul/div/negate and named policies against BigInteger, adversarial boundaries and seeded sequences |
| P0 | No target-wide allocation or hard real-time qualification | Representative Native AOT application kernels, contention and target-specific timing/exception audit |
| P1 | Invalid arithmetic policy is checked only on an overflowing operation | Regression for successful Add/Subtract/Multiply with an undefined enum; specify and consistently enforce programmer-contract validation |
| P1 | Generator permits declaration shapes without full qualification | Roslyn tests for nesting, generic names, member collisions, namespaces and malformed attributes; 128-bit extreme literal compilation |
| P1 | Remaining enforcement rules and analyzer test infrastructure | SSN1001/1005–1008; nested/async/lambda/local-function scope and false-positive tests |
| P1 | Binary codecs, span parsing/formatting and policy annotations incomplete | Golden vectors, malformed-input campaigns and round trips, including generated domains |
| P1 | Arm64 and native-width portability evidence incomplete | Actual ARM64 AOT execution; architecture-specific nint/nuint contracts and generated ranges |
| P1 | Benchmark fast paths approach the empty-method measurement floor | Batch kernels with representative success/failure distributions, out-of-process net11 toolchain, disassembly and comparable work |
| P1 | Release and application qualification incomplete | Deterministic packages, centralized versions, API baselines, code-size comparison and actual RISCue/VeloSim/SharpUnits integration |

The invalid-policy observation is source inspection of the current early-success
return in SafeInteger; it is recorded for a separate specified fix, without
claiming a new runtime regression test. Existing generator and analyzer examples
are useful smoke checks but do not qualify the full planned contract.
