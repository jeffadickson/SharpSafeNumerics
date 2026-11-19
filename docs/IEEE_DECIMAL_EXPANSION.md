# IEEE decimal expansion assessment and decision

User request, 2026-11-18: implement the entire IEEE operation set. The working
interpretation is IEEE 754-2019 decimal32, decimal64 and decimal128. The user
authorized implementing the gaps on 2026-11-18. Implementation scope includes
required and recommended decimal operations; binary-only recommendations are
identified separately. No whole host-language/environment conformance claim is made.

## Executed reconnaissance

The installed .NET 11.0.0 runtime exposes 195 public static Decimal64 signatures,
including overloads, operators and properties. A reflection inventory was executed
as a research-only C# file-based app; production numerical code uses no reflection.
Output: E:/Artifacts/SharpSafeNumerics/windows-x64/ieee-decimal-assessment-20261118/
decimal64-public-static-api.txt. This is API availability evidence, not numerical
or status-flag qualification. Local reference-pack XML does not enumerate these
decimal APIs, so the inventory used the actual installed runtime.

Observed APIs include FusedMultiplyAdd, Sqrt, Ieee754Remainder, Quantize,
GetQuantum, HaveSameQuantum, ScaleB, rounding, classification, transcendental
functions, and EncodeBinary/DecodeBinary/EncodeDecimal/DecodeDecimal. Their
presence does not establish support for explicit arithmetic rounding contexts,
IEEE status flags, NaN payload propagation or correctly rounded transcendental
operations. Those need independent behavioral probes.

## Recommended architecture

Preserve SafeDecimal's existing exact-by-default finite admission contract.
Introduce a separate explicit IEEE operation context/result surface in the
optional Decimal assembly. IEEE results can legitimately be infinity or NaN;
its five exception flags and rounding directions cannot be represented by simply
adding methods to the finite-only success/failure API. Context state should be
explicit and per-call, with no process-global rounding or sticky state.

Evaluate reuse in this order:

1. Existing .NET 11 decimal engine, already deployed and compatible with BCL-only
   runtime dependencies. Reuse each operation only after checking required value,
   representation, rounding and exception semantics.
2. Existing test-only PeterO.Numbers 1.8.2 and exact rational/BigInteger references
   for independent oracles. Avoid making an oracle the only implementation and
   only reference for the same operation.
3. decNumber/decFloats and Intel decimal libraries as independent native references
   or candidate engines where a concrete BCL gap is established. decNumber 3.68
   predates IEEE 754-2019 and has published errata; it cannot be assumed to qualify
   every 2019 behavior unchanged. Native deployment and licensing require an
   explicit compatibility assessment before introducing a runtime dependency.

The implementation retains BCL format carriers and exact integer arithmetic,
adding decimal rounding/status glue and outward interval elementary functions.
PeterO lacks the whole trigonometric surface, while BCL elementary results do not
expose rounding contexts or rigorous error intervals. GNU MPFR/GMP is a maintained
native reference alternative (https://www.mpfr.org/, https://gmplib.org/), but adopting
it at runtime would change the BCL-only deployment and Native AOT platform contract.
The custom interval work is limited to the unmet directed-rounding/flags contract;
it is not selected because of implementation-language preference. Its mathematical
tail bounds and outward operations require independent reference checks and review.
Do not implement directed rounding by nudging an already rounded result without
a correct exact residual argument. Do not infer IEEE underflow from directional
range errors or reduce NaNs to text and claim payload preservation.

## Acceptance matrix to establish

Track required and recommended operations separately with stable StrictDoc child
IDs under SN-019. For each operation and each decimal format, record normative
reference, implementation path, supported rounding modes, special-value and NaN
behavior, preferred exponent/cohort rules, flags, test corpus applicability,
independent reference and executed evidence. An API-count checklist is insufficient.

Required families to map include arithmetic/FMA/square-root/remainder; decimal
quantization and quantum; rounding and conversions; comparisons and ordering;
classification/sign/copy operations; next-value/scaling operations; encoding,
interchange and text conversion; and exception handling. Recommended families
must be explicitly listed if included, rather than silently grouped as optional.

Retain SafeDecimal compatibility tests. Add malformed context/options, signaling
and quiet NaNs, signed zero, infinities, normal/subnormal boundaries, exponent
and precision boundaries, halfway cases and format conversions. Qualify applicable
General Decimal Arithmetic corpus vectors with exclusions and version/provenance
recorded. Corpus execution alone is not a complete IEEE 754-2019 conformance claim.
Run independent properties, coverage, mutation, AOT, Windows/Linux checks and
new-format soak campaigns. The ongoing pinned integer soak cannot qualify this
new surface; preprocess its eventual results with qualcompact as requested.

## Primary sources

- IEEE working-group 2019 changes and required/recommended distinctions:
  https://grouper.ieee.org/groups/msc/ANSI_IEEE-Std-754-2019/background/
- .NET runtime implementation:
  https://github.com/dotnet/runtime/blob/v11.0.0/src/libraries/System.Private.CoreLib/src/System/Numerics/Decimal64.cs
- .NET 11 decimal feature overview:
  https://github.com/dotnet/core/blob/main/release-notes/11.0/preview/preview7/libraries.md
- General Decimal Arithmetic specification, corpus and implementation:
  https://speleotrove.com/decimal/
- decNumber errata: https://speleotrove.com/decimal/decnumerr.html

The operation matrix and verification report distinguish implementation from
qualification. Existing SafeDecimal is unchanged. Explicit immutable modes/status
snapshots bind IEEE environment operations without changing host process state;
they do not establish conformance of the host-language floating-point environment.
