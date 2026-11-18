# IEEE decimal gaps implementation plan

User authorization: implement the gaps in the existing Decimal package, 2026-11-18.
Working scope includes required and recommended IEEE 754-2019 decimal operations.
Existing SafeDecimal behavior remains compatible. Preserve .NET 11/C#15 and
BCL-only production dependencies; independent references stay test-only.

Architecture: an explicit, stateless `IeeeDecimal<T>` API for the three built-in
decimal formats. Context selects one of five IEEE rounding directions and optional
trap flags. Results contain five IEEE exception flags independently of API errors.
Valid IEEE NaN/infinity results are not mistaken for API validation failures.
Default context is nearest-even with no traps; default result is invalid.
No thread/process-global rounding modes or status state. Allocation is allowed.

Implementation tasks (TDD, independent review and executed evidence per slice):

- [x] Context, flags, result, raw-format codec, exact finite rational rounding,
  arithmetic/FMA/sqrt/remainder/quantize and conversions. Test all formats/modes,
  tie directions, overflow/underflow, NaNs, preferred quantum and cleared API
  failures against independent references.
- [x] Representation and nonarithmetic operations: classification, sign/copy,
  comparisons including quiet/signaling/total ordering, adjacent values, quantum,
  decimal scaling, integral rounding, parsing/formatting/encoding and payloads.
  Test raw encodings, NaN payloads, signed zeros, cohorts, malformed inputs.
- [x] Recommended operation matrix and implementations: min/max variants,
  reductions and elementary functions. IEEE9.5 augmented operations apply to binary
  formats and are not decimal mandates. Do not label simple
  BCL forwarding correctly rounded in arbitrary modes without evidence. Any
  unverified qualification category stays explicitly open; no placeholder successful results.
- [ ] Integrate corpus/reference checks, property/negative/fuzz campaigns,
  line/branch coverage, mutation, Native AOT and Windows/Linux CI. Extend examples,
  API baseline, StrictDoc and qualification documentation. Retain exact commands,
  revisions/hashes, seeds, exclusions and limitations. Commit/push coherent slices.

Reuse decision: .NET 11 provides many operations but exposes no per-call IEEE
arithmetic rounding/flags context. Therefore retain its format carriers and
well-qualified operations; add exact BigInteger-based rounding/flag glue where
necessary. PeterO.Numbers1.8.2 remains independent/test-only. decNumber/Intel
remain independent corpus/reference candidates; do not add a runtime arithmetic
dependency or infer full conformance from BCL API presence.

Shared contract names for implementation:
`IeeeDecimalRounding` (ToNearestEven,ToNearestAway,TowardZero,TowardNegative,
TowardPositive); `IeeeDecimalFlags` (None,Invalid,DivisionByZero,Overflow,
Underflow,Inexact); `IeeeDecimalContext(Rounding,Traps)`;
`IeeeDecimalResult<T>` exposes Error,Flags,IsSuccess,Value,TryGetValue.
`IeeeDecimal<T>` is a static partial class constrained to
`where T:struct,IFloatingPointIeee754<T>`; all public numeric operations take an
optional `IeeeDecimalContext context=default` and return a typed result.

Review emphasis: false pass on unknown options/types; loss of signaling NaNs or
payload/cohort through text; double rounding in fused/reduction operations;
IEEE tiny-and-inexact underflow versus directional range errors; mutable context
leaks/concurrent use; existing SafeDecimal API compatibility.
