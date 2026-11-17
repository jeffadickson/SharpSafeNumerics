# M1–M5 implementation plan

## M1 — Arithmetic core
**Implemented and CI exercised.** Generic fixed-width integer add/subtract/multiply/divide/negate, range admission, explicit errors, policy modes, default-invalid result, exhaustive byte/sbyte verification, Native AOT smoke.

## M2 — Declarative numeric domains
**Initial implementation complete.** `BoundedIntegerAttribute` is the public declaration surface; `BoundedIntegerGenerator` generates a partial value type with explicit validity, bounds, TryCreate/TryAdd/TrySubtract. This deliberately combines annotations with source generation: attributes are the specification; generated code is the zero-reflection implementation.

RC-1 implements invariant span parsing/formatting, fixed-width endian codecs,
invalid/zero-valid defaults, policy helpers, metadata, exact conversions,
comparison operators and declaration/collision diagnostics. Supported declarations
and verification scope are specified in [GENERATOR_RC1.md](GENERATOR_RC1.md).

## M3 — Compile-time policy enforcement
**Initial implementation complete.** `StrictNumericAttribute` plus analyzer rules:
- SSN1002: unchecked arithmetic in strict scope — error
- SSN1003: ignored NumericResult<T> — warning
- SSN1004: Throw policy in strict scope — error

RC-1 adds SSN1001 and SSN1005–SSN1008, operation-based scope handling and proper
Roslyn unit tests. All eight rules have positive and negative tests; see
[ANALYZERS_RC1.md](ANALYZERS_RC1.md) for static detection limitations.

## M4 — Verification stack
**Implemented infrastructure; qualification evolves continuously.**
- exhaustive deterministic harness
- xUnit regression suite
- CsCheck 5.0.1 randomized/shrinking properties
- Hedgehog 2.0.4 independent generator/shrinker
- Stryker.NET 5.1.0 mutation configuration and CI
- Dafny 4.11.0 proofs for 32-bit add/sub/mul/div and bounded addition
- allocation-focused hot-path check
- analyzer self-test

No single test technology is considered an oracle for itself.

## M5 — Qualification and release evidence
**Infrastructure implemented, evidence incomplete.**
- Linux x64 Native AOT smoke in core CI
- Windows/Linux cross-platform test workflow
- BenchmarkDotNet baseline project
- mutation score threshold (break below 90; local conversion pass achieved 91.15%)
- formal verification workflow
- generated-domain executable example
- explicit feature-parity/status documentation

Conversion pass: exact nonexception-driven conversion, 144-pair BigInteger oracle,
100k-call allocation checks, core coverage and survivor-driven regression additions.
Windows x64 AOT smoke has executed locally. See [dated evidence](verification/2026-11-17-conversion.md).

RC-1 adds wide arithmetic/policy campaigns, package consumer verification and
expanded platform qualification. Final evidence is recorded in the
[RC report](verification/2026-11-17-rc1.md). Physical target WCET, actual
RISCue/VeloSim integration, package signing and universal allocation/behavioral
qualification remain outside this release candidate's evidence.
