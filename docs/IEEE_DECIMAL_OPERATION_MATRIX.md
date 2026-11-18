# IEEE 754-2019 decimal operation and qualification matrix

Status: implementation/qualification inventory, 2026-11-18. This is **not** a
conformance declaration. Scope is decimal32, decimal64 and decimal128. A row
applies separately to each format; no row is closed merely because a BCL method
exists. Stable requirement parent: SN-019. The child requirements are in
requirements.sdoc: SN-019-IEEE-CONTEXT, SN-019-IEEE-REPRESENTATION,
SN-019-IEEE-CONVERSIONS, SN-019-IEEE-FUNCTIONS, SN-019-IEEE-REDUCTIONS and
SN-019-IEEE-ENVIRONMENT. Availability and qualification are separate axes.

## Normative and implementation sources

- **N**: IEEE Std 754-2019, DOI
  [10.1109/IEEESTD.2019.8766229](https://doi.org/10.1109/IEEESTD.2019.8766229),
  [publisher record](https://standards.ieee.org/ieee/754/6210/). Clause numbers in
  this document refer to this edition, not 1985 or 2008. The standard is the
  normative authority; public working-group commentary below explains changes
  but is not a replacement for normative text.
- **WG**: IEEE working-group [2019 background](https://grouper.ieee.org/groups/msc/ANSI_IEEE-Std-754-2019/background/),
  particularly [conformance/options](https://grouper.ieee.org/groups/msc/ANSI_IEEE-Std-754-2019/background/conformance-and-options.txt),
  [comparison predicates](https://grouper.ieee.org/groups/msc/ANSI_IEEE-Std-754-2019/background/predicates.txt),
  [payload functions](https://grouper.ieee.org/groups/msc/ANSI_IEEE-Std-754-2019/background/payload-functions.txt),
  [tanPi](https://grouper.ieee.org/groups/msc/ANSI_IEEE-Std-754-2019/background/tanpi.txt),
  and [power functions](https://grouper.ieee.org/groups/msc/ANSI_IEEE-Std-754-2019/background/power.txt).
- **B**: actual .NET 11.0.0 implementation, pinned
  [Decimal64.cs](https://github.com/dotnet/runtime/blob/v11.0.0/src/libraries/System.Private.CoreLib/src/System/Numerics/Decimal64.cs),
  [decimal engine](https://github.com/dotnet/runtime/blob/v11.0.0/src/libraries/System.Private.CoreLib/src/System/Number.DecimalIeee754.cs),
  [transcendental dispatch](https://github.com/dotnet/runtime/blob/v11.0.0/src/libraries/System.Private.CoreLib/src/System/Number.DecimalIeee754.Transcendental.cs).
  Analogous Decimal32/128 carriers must be qualified separately.
- **G**: [General Decimal Arithmetic](https://speleotrove.com/decimal/),
  [specification](https://speleotrove.com/decimal/decarith.html),
  [decTest format](https://speleotrove.com/decimal/dtfile.html), and
  [decNumber errata](https://speleotrove.com/decimal/decnumerr.html). GDA has
  operations and conditions beyond IEEE; its 2010 corpus predates IEEE 2019.

## Cross-cutting acceptance dimensions

| Dimension | Required qualification for each applicable operation |
|---|---|
| Format | decimal32: p=7, emin=-95, emax=96, quantum -101..90; decimal64: p=16, emin=-383, emax=384, quantum -398..369; decimal128: p=34, emin=-6143, emax=6144, quantum -6176..6111. emin/emax are adjusted exponents, not coefficient exponents. |
| Rounding (N §4) | All five decimal directions: nearest-even, nearest-away, toward zero, toward negative, toward positive. A `Round` midpoint option does not change arithmetic rounding globally or per call. |
| Exceptions (N §7) | Invalid, divisionByZero, overflow, underflow, inexact separately. GDA Rounded, Clamped and Subnormal are additional conditions, not additional IEEE exceptions. Decimal tininess is detected **before rounding** (§7.5); Underflow flag under default handling additionally requires Inexact. Exact subnormal raises neither flag. |
| Special encodings (N §§3,6) | qNaN/sNaN distinction, payload capacity and propagation policy, signs of zero/infinity/NaN, noncanonical encodings, decimal BID/DPD interchange. Text round trips do not qualify payload or encoding preservation. |
| Cohorts (N §§3,5.2) | Preferred exponent, quantize target quantum, exact/inexact zero exponent, cancellation sign, total ordering of equal values with different quantum. Equality of numeric value alone is insufficient. |
| Context | Explicit per-call rounding/traps; no leaking process/thread state. Invalid context/type is API failure, whereas IEEE invalid normally produces qNaN+Invalid. |
| Evidence | Result, five flags, quantum/raw representation where defined, exact input identity, oracle/version, source hashes and explicit exclusions. Finite sampling is not global correctness proof. |

## Required operation families

The language-binding spelling may differ from the operation spelling in N.
`B API` identifies the reused engine facility; IeeeDecimal bindings are implemented.
Implementation and complete conformance qualification remain separate.

| N reference / operation | Binding or candidate B API | Required behavior / qualification gap | Reference coverage |
|---|---|---|---|
| §§5.3–5.4 addition, subtraction, multiplication, division | `+ - * /`; `IeeeDecimal<T>.Add/Subtract/Multiply/Divide` | One final rounding, preferred exponents, all modes/flags; mixed destination formats cannot round first to source format. Implemented; see integrated evidence below. | G add/subtract/multiply/divide; PeterO; native decNumber |
| §§5.3–5.4 squareRoot | `Sqrt`; `IeeeDecimal<T>.Sqrt` | Correct rounding in each mode; -0 preserved, negative nonzero Invalid, exact squares no Inexact. Implemented; see integrated evidence below. | G squareroot; PeterO/native |
| §§5.3–5.4 fusedMultiplyAdd | `FusedMultiplyAdd` | Exact product+sum then one rounding, cancellation, 0×infinity, qNaN interaction and invalid signaling. Ordinary multiply then add is insufficient. Implemented; see integrated evidence below. | G fma; PeterO/native |
| §5.3 quantize | `Quantize` | Target quantum exact; invalid if precision/exponent constraints fail, special operands; **no underflow** from quantize. Implemented; see integrated evidence below. | G quantize; PeterO/native |
| §5.3 remainder | `IeeeDecimal<T>.Remainder` | Nearest-integer quotient with ties-even; exact result, dividend zero sign; distinguish truncating `%` remainder. Implemented; see integrated evidence below. | G remainderNear; independent rational |
| §5.3 roundToIntegralTiesToEven, TiesToAway, TowardZero, TowardPositive, TowardNegative | `Round`, `Truncate`, `Ceiling`, `Floor` | These five named operations suppress Inexact; sNaN still Invalid. Implemented; see integrated evidence below. | G round/integral vectors + raw sNaN |
| §§5.3,5.9 roundToIntegralExact | explicit context adapter | Uses context rounding and raises Inexact iff fraction discarded; distinguish from preceding quiet-inexact operations. Implemented; see integrated evidence below. | G toIntegralX and mode sweeps |
| §5.3 nextUp, nextDown | `BitIncrement`, `BitDecrement` | Adjacent **numerical value**, not adjacent encoding/cohort; boundaries, infinities, zeros, sNaN. Not ordinary arithmetic overflow/underflow. Implemented; see integrated evidence below. | G nextPlus/nextMinus + raw boundaries |
| §5.3 scaleB | `ScaleB` | Decimal radix 10 scaling; flags and preferred exponent, enormous signed scale, every mode. Implemented; see integrated evidence below. | G scaleb + exact powers |
| §5.3 logB | `IeeeDecimal<T>.LogB` | Floor radix-10 exponent for finite nonzero; logB's special-value/flag contract is not automatically integer `ILogB` behavior. Implemented; see integrated evidence below. | G logb + adjusted-exponent oracle |
| §5.4 convertFromInt / convertFormat | casts and `Create*`, explicit conversion adapter | Exact source before destination rounding, all modes, overflow/underflow, NaN payload narrowing policy, cohort conversion. Implemented; see integrated evidence below. | G conversion vectors, rational/raw oracle |
| §§5.4,5.8 convertToIntegerTiesToEven, TiesToAway, TowardZero, TowardPositive, TowardNegative | `IeeeDecimal<T>.ToInteger<TInteger>` | Explicit target width/sign and invalid out-of-range policy; conversion must not silently wrap/saturate without policy. Suppress Inexact for these variants. Implemented; see integrated evidence below. | integer boundary and malformed option tests |
| §§5.4,5.8 convertToIntegerExactTiesToEven, ExactTiesToAway, ExactTowardZero, ExactTowardPositive, ExactTowardNegative | `IeeeDecimal<T>.ToInteger<TInteger>` | Corresponding conversions that signal Inexact for discarded fractions; invalid conversion must not publish a valid numeric value. Implemented; see integrated evidence below. | rational boundary oracle; all target widths |
| §§5.4,5.12 decimal external character conversion, in both directions | `IeeeDecimal<T>.Parse/Format` | Grammar, length bounds, rounding and conversion accuracy, culture/locale contract, signed zeros, quantum. General BCL parsing is not full IEEE binding. Implemented; see integrated evidence below. | G toSci/toEng/apply + malformed grammar |
| §§5.4,5.12 hexadecimal external character conversion / binary conversion | no decimal conformance inference from binary API names | Clause applicability must be checked by source/destination radix and language binding; binary-only conversion requirements do not become decimal arithmetic mandates. Exact-source binary/decimal conversion implemented; wider external radix character bindings are not claimed. | independent exact rational conversions |
| §5.5 copy, negate, abs, copySign | unary operators, `Abs`, `CopySign` | Quiet nonarithmetic raw sign operations; preserve sNaN/payload/cohort, do not signal Invalid merely copying sNaN. Implemented; see integrated evidence below. | raw encoding tests |
| §5.5 decimal re-encoding | `EncodeBinary/DecodeBinary`, `EncodeDecimal/DecodeDecimal` | Verify actual BID/DPD bit contract, noncanonical input and canonicalization; byte order is explicit. Implemented; see integrated evidence below. | G decimal32/64/128 encoding vectors; raw oracle |
| §§5.6,5.11 signaling comparisons | explicit signaling predicates | Every NaN raises Invalid for signaling predicates; Boolean outcome separate from flags. Do not use `CompareTo` as a drop-in. Implemented; see integrated evidence below. | WG predicate truth table, all NaN categories |
| §§5.5,5.11 quiet comparisons | operators + explicit quiet predicates | qNaN is unordered without Invalid; sNaN raises Invalid; zeros equal across signs/cohorts. Implemented; see integrated evidence below. | G compare/compareSig + truth table |
| §5.7 class, isSignMinus, isNormal, isFinite, isZero, isSubnormal, isInfinite, isNaN, isSignaling, isCanonical | `IsNegative/IsNormal/IsFinite/IsZero/IsSubnormal/IsInfinity/IsNaN` plus raw helpers | Classification must not quiet sNaN; ten-way class includes sNaN and signed finite classes. BCL generic predicates do not expose every raw property. Implemented; see integrated evidence below. | G class + raw patterns |
| §§5.7,5.10 totalOrder, totalOrderMag | explicit raw/cohort adapter | NaN sign, signaling bit, payload, signed zero, equal-value exponent ordering; totalOrderMag ignores sign. `CompareTo` not sufficient. Implemented; see integrated evidence below. | G compareTotal/compareTotMag + raw properties |
| §5.7 radix, sameQuantum | `Radix`, `HaveSameQuantum` | radix=10; quantum comparison special rules for pairs of NaNs/infinities and mixed categories. Implemented; see integrated evidence below. | G sameQuantum + specials |
| §5.7 exception status operations | `IeeeDecimalEnvironment` explicit immutable state | Map language-binding status operations (raise/test/lower/save/restore flags and test saved flags) if environment conformance is claimed. Stateless results alone are a library contract, not a complete environment implementation. Explicit immutable library state implemented; no host-environment conformance claim. | flag isolation, trap and composition tests |
| §5.7.1 is754version1985, is754version2008, is754version2019 | no true conformance predicate claimed | Environment-conformance predicates, distinct from API availability. Required for a conforming environment; unqualified for this library binding. | whole-environment qualification |

WG confirms **22 required predicates**. Illustrative names are
`compareQuietEqual`, `compareQuietNotEqual`, `compareQuietGreater`,
`compareQuietGreaterEqual`, `compareQuietLess`, `compareQuietLessEqual`,
`compareQuietUnordered`, `compareQuietNotGreater`, `compareQuietLessUnordered`,
`compareQuietNotLess`, `compareQuietGreaterUnordered`, `compareQuietOrdered`,
and corresponding ten `compareSignaling*` forms **excluding** Unordered and
Ordered. `NotGreater` and `LessUnordered` differ in inclusion of equality.
Less-or-greater, equal-or-unordered, signaling ordered/unordered may be additional
language operations; they are not among the mandatory 22.

## Recommended operations (N §9), kept separate from mandates

All listed elementary functions require individual special-value, rounding and
flag qualification. A method forwarding to BCL with nearest-even result plus an
invented Inexact flag is not qualification in five rounding directions.

| N reference / operation names | BCL candidate / decimal status | Evidence needed |
|---|---|---|
| §9.2 exp, expm1, exp2, exp2m1, exp10, exp10m1 | `Exp/ExpM1/Exp2/Exp2M1/Exp10/Exp10M1` | Independent high-precision enclosure then unique target rounding; threshold and cancellation sweeps |
| §9.2 log, log2, log10, logp1, log2p1, log10p1 | `Log/Log2/Log10/LogP1/Log2P1/Log10P1` | Near-one and near-minus-one cancellation, exact powers, poles/domain flags |
| §9.2 hypot, rSqrt | `Hypot`; `ReciprocalSqrtEstimate` is an estimate, not a correctly-rounded substitute | Overflow-safe exact squared sum/square root; zero/NaN/infinity precedence |
| §9.2 compound, rootn, pown, pow, powr | `RootN`, `Pow` are partial candidates; explicit distinct contracts needed | Integer exponent/root domain, odd negative roots, signed zeros; `powr` differs from `pow`, and `compound` must not round 1+x first |
| §9.2 sin, cos, tan, sinPi, cosPi, tanPi | `Sin/Cos/Tan/SinPi/CosPi/TanPi` | Huge argument reduction, exact integer/half-integer cases; full-mode interval oracle |
| §9.2 asin, acos, atan, atan2, asinPi, acosPi, atanPi, atan2Pi | corresponding BCL methods | Domain endpoints, ordered atan2 arguments and signed-zero quadrants, Pi-scaled exact results |
| §9.2 sinh, cosh, tanh, asinh, acosh, atanh | corresponding BCL methods | Cancellation, saturation that still signals Inexact, overflow/domain/poles |
| §9.3 dynamic modes | `IeeeDecimalEnvironment` mode snapshots; no ambient state | `get/setDecimalRoundingDirection`, `get/setBinaryRoundingDirection`, `saveModes`, `restoreModes`, `defaultModes`; implemented via explicit state; no host global-environment claim |
| §9.4 sum, sumAbs, sumSquare, dot (binding alias SumProd) | `Sum/AbsSum/SumSquare/Dot` exact accumulation implemented | Reduction order/approximation contract, exact products, cancellation, signed zero/NaN/infinity precedence; no avoidable intermediate exceptions |
| §9.4 scaledProd, scaledProdSum, scaledProdDiff | `ScaledProd/ScaledProdSum/ScaledProdDiff` | Return `(sourceFormat fraction, integralFormat scale)` reconstructed by scaleB; products of p[i], (p[i]+q[i]), (p[i]-q[i]) respectively, **not sums of products**. Implemented with exact product and one normalized-fraction rounding; source-format fraction and BigInteger scale; special/boundary/oracle tests |
| §9.5 augmentedAddition, augmentedSubtraction, augmentedMultiplication | **binary-format recommendation**, not a decimal conformance mandate | Decimal helpers, if provided, are extensions; don't infer IEEE binary augmented residual rules apply to decimal |
| §9.6 minimum, maximum, minimumNumber, maximumNumber, minimumMagnitude, maximumMagnitude, minimumMagnitudeNumber, maximumMagnitudeNumber | `Min/Max/MinNumber/MaxNumber/MinMagnitude/MaxMagnitude/MinMagnitudeNumber/MaxMagnitudeNumber` are implemented | qNaN versus sNaN, opposite signed-zero tie, equal-magnitude opposite signs; eight variants separately |
| §9.7 getPayload, setPayload, setPayloadSignaling | `GetPayload/SetPayload/SetPayloadSignaling` implemented | Payload numeric range, rejection/NaN behavior for invalid payloads, exact q/s bit and sign rules |

The 2008 names `minNum`, `maxNum`, `minNumMag`, `maxNumMag` are removed in
2019, not required substitutes for §9.6. Extra BCL methods such as Cbrt,
SinCos, SinCosPi, Lerp, Clamp and degree/radian conversion are useful extensions;
their existence does not establish coverage of another normative operation.

N §9.2.1 exact Pi-scaled specials require particular care: sinPi(nonzero integer)
returns zero with the input's sign; cosPi(representable half-integer) returns
positive zero. tanPi at positive odd/even integers returns negative/positive
zero respectively, and negative inputs follow odd symmetry. Positive half-integer
poles alternate +infinity at 0.5, -infinity at 1.5; signs reverse for negative
inputs, with DivisionByZero. These are exact operations, not tiny approximate
sines/cosines. At signed zero the odd functions retain the input zero sign.

## Executed reconnaissance and limitations

`eng/DecimalConformance` is a test-only .NET 11/C#15 executable using existing
PeterO.Numbers 1.8.2 and a small adapter to unmodified native decNumber 3.68.
There is no production reference dependency. Original archives and expanded
corpora remain outside Git. See its README and provenance manifest for commands.

Executed on 2026-11-18 with SDK 11.0.100/runtime 11.0.0:

- Build: succeeded, zero warnings/errors.
- `add.decTest`: 2,094 reference-vs-corpus passes, zero mismatches, six explicit
  skips (`apply`, missing-operand `#` sentinel), zero malformed records. Exact
  tuple comparison includes cohorts, zero signs and NaN representation; all GDA
  condition bits are checked for supported rows. **This does not execute the
  production implementation.**
- BCL probe: 63 exp/log/sqrt samples (seven inputs × three operations × three
  formats) match independent 160-digit PeterO reference rounded to p=7/16/34.
  No directed-rounding/status/raw-NaN qualification follows from these samples.
- Native decNumber built with existing Ubuntu 24.04 gcc; simple exact addition,
  decimal32 1/3 and decimal128 sqrt(2) executed. decNumber's raw status 2080 on
  inexact examples means Rounded|Inexact, not project enum bit values.
- The pinned B source's `DecimalIeee754UsesDouble<TValue>() => false` routes
  **all formats** through software binary128 transcendental code. It is incorrect
  to claim these methods universally fall back to `double`. Wider internal
  precision is still not proof of correct rounding in every decimal128 hard case.

Production adapter subsequently executed fixed-format decimal64 vectors: 3,758
passed, one discrepancy, 1,072 skipped; decimal128: 3,822 passed, one discrepancy,
1,007 skipped; zero malformed in either. Both (`ddmul906`, `dqmul908`) miss Underflow
when a tiny exact product rounds to minimum normal, confirming the §7.5 issue.
Native decNumber independently confirmed ddmul906. These are regression findings
for repair/replay, not accepted deviations. Production input admission rejects
pre-rounding/cohort changes and raw NaNs are explicitly skipped. Initial linked
build used temporary `-p:NoWarn=RS0016` pending public API baseline integration.

After the arithmetic repair, the same six files per format replayed with
**3,759 decimal64 passes and 3,823 decimal128 passes, zero discrepancies**;
skip counts remain 1,072 and 1,007, with zero malformed rows. The files are
`{dd,dq}{Add,Subtract,Multiply,Divide,FMA,Quantize}.decTest`. There is no
fixed-format `SquareRoot` file in this corpus; none is implied by these counts.
Decimal32 original smoke: ten production passes, two skips, zero discrepancies.
These samples do not close other matrix rows or exclusions.

## Integrated implementation and evidence

The inventory above originated as reconnaissance. The implementation now exposes
the required decimal operation families and all 39 Table9.1 elementary functions,
plus Cbrt as an extension. IeeeDecimalArithmetic supplies exact rounding/status;
Representation/Predicates supply raw encodings, text, payloads, classification and
all 22 Boolean predicates; Conversions supplies exact-source mixed conversions.
IeeeDecimalReductions supplies the eight extrema and all seven reduction/scaled
product operations, including the three scaled-product rows originally marked open.
IeeeDecimalEnvironment supplies immutable explicit mode/flag state operations.
The host environment/version predicates are outside the binding's conformance claim.

The integrated suite passed 1,487 Decimal tests. It includes 1,290 MPFR/analytic function
cases, exact-source conversion and all Half encodings, independent properties,
malformed inputs and internal outward-interval invariants. MPFR4.2.1 directed
bounds reproduced at 1024/2048 bits are retained with provenance. The Decimal
assembly measured 991/996 lines (99.4%) and 1,749/1,801 branches (97.1%) in the integrated ReportGenerator
scope; Windows Native AOT and the three Dafny models executed successfully.
These are bounded executed results, not proof of whole-IEEE or host conformance.

Arithmetic and conversion mutation scopes separately scored 93.91% and 91.69%;
the remaining-file campaign is tracked in the final verification report. Raw NaN
GDA admission, all-format external encoding corpus and exhaustive/global elementary
correct-rounding qualification remain unclosed. The conformance helper measured
172/191 lines (90.05%) and 243/305 branches (79.67%) with Coverlet.Console; native
reference glue, scripts and third-party libraries are outside that coverage scope.
