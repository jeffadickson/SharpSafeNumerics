# Product Requirements — Strict Baseline
Version: 0.1. Requirements below are independently testable. Status is tracked separately in FEATURE_PARITY.md.

## Scope
NET11/C#15 only. Strict core is BCL-only. Target users: systems, protocol, simulation and verification developers. No safety certification is implied.

| ID | Priority | Requirement | Acceptance evidence |
| --- | --- | --- | --- |
| SN-001 | P0 | Target net11.0/C#15 | build on .NET 11 SDK |
| SN-002 | P0 | Integer add, subtract, multiply, divide, negate | boundary + exhaustive tests |
| SN-003 | P0 | Expected arithmetic failures return statuses, not exceptions | test all failure paths |
| SN-004 | P0 | Signed/unsigned 8–128 bits plus nint/nuint | per-type tests |
| SN-005 | P0 | 0 managed allocations on designated hot paths | 100k iteration allocated-byte tests on success and failure |
| SN-006 | P0 | Core has BCL-only dependencies, no reflection or runtime code gen | package review + Native AOT |
| SN-007 | P0 | Reject/saturate/wrap/throw policies | boundary/policy tests |
| SN-008 | P0 | Inclusive domain range check and no silent narrowing | valid/invalid range tests |
| SN-009 | P0 | Divide-by-zero and MinValue / -1 defined | tests |
| SN-010 | P0 | Oracle-based exhaustive or property tests | exhaustive byte/sbyte + BigInteger oracle |
| SN-011 | P1 | Source-generated strongly typed bounded values | analyzer integration tests incl default |
| SN-012 | P1 | C#15 union convenience API with documented boxing | compilation + allocation characterization |
| SN-013 | P1 | Roslyn analyzer rules SSN1001–SSN1008 | diagnostics tests |
| SN-014 | P1 | Fixed-buffer binary encode/decode APIs | golden vectors |
| SN-015 | P1 | Published baseline benchmarks | reproducible results on x64/Arm64 |
| SN-016 | P1 | Structured deterministic errors without strings on strict paths | allocation and replay tests |
| SN-017 | P1 | SharpUnits integration | sample project + compile |
| SN-018 | P2 | Fixed point semantics | precision/rounding reference tests |
| SN-019 | P2 | Decimal32/64/128 operations | IEEE special-value tests |
| SN-020 | P2 | SMT/formal rules | machine-readable proof evidence |

## Negative requirements
No silent clamp/wrap; no hidden BigInteger work in strict integer arithmetic; no runtime reflection; no false promises of hard-real-time behavior; no blanket ban on exceptions across the application.

## Current bounded implementation contract
For `Try*`, failure writes `default(T)` to the out value. `TryConvert` translates overflow exceptions and **is not** part of the strict zero-allocation set. Generic fast path targets built-in integral types only. Caller owns synchronization and domain-state management.

## Blocking qualification gaps
C#15 compilation and .NET 11 runtime builds were not available in the original authoring environment. CI must provide evidence. No baseline benchmark or physical target measurements have been completed. Do not mark SN-005/SN-010 fully passed until failure-path allocation and wide-type oracle suites are green.
