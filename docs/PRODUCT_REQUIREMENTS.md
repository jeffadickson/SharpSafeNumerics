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
| SN-017 | P1 | SharpUnits integration | Actual pinned-source whole-volt bridge sample, compile and dimension/precision assertions; see StrictDoc SN-017 |
| SN-018 | P2; included in expanded RC1 | Fixed point semantics | Scoped Q32.32 contract SN-018-FIXED; precision/rounding reference tests |
| SN-019 | P2; included in expanded RC1 | Decimal32/64/128 operations | Scoped finite safe BCL wrapper SN-019-DECIMAL; special-value admission, precision/reference tests |
| SN-020 | P2 | SMT/formal rules | machine-readable proof evidence |

## Negative requirements
No silent clamp/wrap; no hidden BigInteger work in strict integer arithmetic; no runtime reflection; no false promises of hard-real-time behavior; no blanket ban on exceptions across the application.

## Current bounded implementation contract
For `Try*`, failure writes `default(T)` to the out value. `TryConvert` admits only exact, sign-preserving conversions and returns `OutOfRange` otherwise, without expected range exceptions for built-in types. Its scoped StrictDoc baseline is [requirements.sdoc](requirements.sdoc), preserving SN-003/004/005. Generic fast paths target built-in integral types only. Caller owns synchronization and domain-state management.

## Blocking qualification gaps
Later integer campaigns supersede the conversion-only status: see [RC closeout](verification/2026-11-17-rc1-closeout.md) for wide policies, Linux/ARM64 AOT and mutation gates. Application-specific allocation/timing and 32-bit execution remain unqualified. The user expanded RC1 to include owned Q32.32 and safe BCL decimal APIs on 2026-11-17; their new verification is separate from the earlier integer evidence and pinned soak. SharpUnits SN-017 now has a bounded real-source example; general unit adapters and published cross-architecture benchmark baselines SN-015 remain open.
