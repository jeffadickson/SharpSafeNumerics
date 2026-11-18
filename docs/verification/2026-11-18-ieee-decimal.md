# IEEE decimal expansion qualification — 2026-11-18

This records bounded executed evidence for branch `ieee-decimal-completion`, based
on `2afb48e60e1cd6865c146d0dc73bf2acb0a042fd`. Source hashes accompany the final
qualification artifacts; results below do not qualify every input or claim whole
IEEE/host-environment conformance. No release, tag or NuGet publication is made.

## Implemented surface and design

The optional Decimal package adds `IeeeDecimal<T>` for Decimal32/64/128, explicit
five-direction rounding, five IEEE exception flags and trap masks, arithmetic,
FMA, square root, remainder, conversions, BID/DPD encodings, payloads, classification,
comparison, adjacent values, elementary functions, extrema and exact reductions.
`IeeeDecimalEnvironment` supplies immutable explicit modes and sticky flag state.
The existing finite/exact `SafeDecimal` contract remains intact. Production remains
BCL-only. See the operation matrix, accepted reuse assessment and SN-019 child
requirements for implementation and verification links.

Exact integer/rational rounding avoids intermediate decimal rounding. Elementary
functions use outward integer intervals with adaptive refinement until both bounds
round identically; exact identities and special values are treated separately.
Finite reference checks support this design, but are not a global proof of every
function's correctly rounded result or termination bound.

## Executed verification

Environment: Windows x64, .NET SDK11.0.100/runtime11.0.0, Stryker.NET5.1.0,
ReportGenerator5.6.0, Dafny4.11.0, StrictDoc0.30.4. MPFR4.2.1 and GCC13.3 were
reused under WSL Ubuntu24.04. Raw artifacts are outside Git under
`E:/Artifacts/SharpSafeNumerics/windows-x64/ieee-decimal-20261118`.

| Check | Result and scope |
|---|---|
| Strict Release solution test | 1,928 passed: 197 Core, 89 generator, 1,487 Decimal, 155 analyzer; zero failed/skipped; no warning suppressions |
| Integrated Decimal coverage | 991/996 lines (99.4%); 1,749/1,801 branches (97.1%); Coverlet collector + ReportGenerator assembly filter; scripts/native references/dependencies excluded |
| MPFR/analytic function matrix | 1,290 cases across three formats and five rounding directions; 39 IEEE elementary operations plus Cbrt; independent 1024/2048-bit bounds reproduce retained destination decisions |
| General Decimal Arithmetic corpus | Decimal64: 3,759 passed/1,072 skipped; Decimal128: 3,823 passed/1,007 skipped; zero failed/malformed; fixed add/subtract/multiply/divide/FMA/quantize files only |
| Exact conversions | All 65,536 Half bit patterns, 12 built-in integer types plus BigInteger, three formats/five modes; separate conversion tests |
| Randomized/property | Decimal CsCheck seed7542030: 1,920 Exp/Log value comparisons; malformed string seed7542031: 1,000 inputs; arithmetic independent oracle seeds7542019/7542020: 16,500 comparisons |
| Existing property suites | CsCheck and Hedgehog passed, including wide/policy and fixed/decimal tests; Hedgehog seed731921, 1,000 cases/property |
| Existing exhaustive harness | 693,195 assertions passed |
| Interactive guide | 2,424 embedded package fixtures verified |
| Native AOT | Current Windows x64 publish/execution passed; decimal arithmetic/flags, all three formats, golden Sin, conversion, raw DPD/payload and reduction checks; integer oracle52,632 outcomes seed731922 |
| Formal models | SafeInteger12, FixedPoint4, DecimalRounding3 obligations verified, zero errors; generated decimal model C# built/executed; five Main checks |
| StrictDoc | All three requirement documents exported successfully |
| API compatibility controls | 17 controls passed, including deliberate drift rejection and metadata constraints |
| Package integration | Eight archives byte-for-byte reproducible across rebuild; all five packages consumed; packaged analyzer rejected deliberate violations |
| Integrated analyzer coverage | 388/395 lines (98.2%),541/610 branches (88.6%); separate assembly scope |
| Conformance helper coverage | 172/191 lines (90.05%),243/305 branches (79.67%); Coverlet.Console10.1.1 primary assembly included; dependencies/scripts/native glue excluded |
| Malformed helper control | Four malformed token fixtures: zero passed/four malformed, direct exit2; unreadable input also structured exit2 |

Corpus skips explicitly exclude unsupported GDA directives/modes, raw NaNs and
operands whose fixed-format admission would change value/cohort. This does not
qualify skipped cases. There is no fixed sqrt corpus file in this selected slice.
The helper has no measured mutation score. Coverlet's instrumented runner maps a
nonzero target exit to1; direct executions independently establish target exit2.

The Dafny proof covers unbounded nonnegative coefficient rounding and its five-mode
bounds/ties properties. It is not a refinement proof of handwritten C#, encoding,
transcendentals or whole IEEE behavior. Generated Dafny runtime emits CS8981 (`nat`);
this generated verification project is separate from production warning policy.

## Mutation evidence

Separate scopes must not be combined into a synthetic aggregate:

- Arithmetic/codec/contracts: 93.91% on the retained 33-test snapshot; 521 killed,
  3 timeout,29 survived,5 uncovered,6 compile errors,23 default block ignores.
  A later FMA regression kills retained mutant465; no updated aggregate is inferred.
- Conversions: 91.69% on the retained 25-test snapshot;287 killed,26 survived,
  zero uncovered/timeouts,1 compile error,12 default block ignores. One later
  directed small-decimal fixture was added without claiming a rerun score.
- Broad remaining seven files: campaign02 scored81.84% (1,346 killed,132 timeout,
  264 survived,64 uncovered). This failed the 90% acceptance threshold. Campaign03 improved to87.02% (1,441 killed,134 timeout,
  209 survived,26 uncovered; denominator1,810), elapsed17m21.821s. This is a
  seven-file diagnostic scope, not the full repository acceptance gate. Final
  full-package mutation is pending on the subsequent fixes. Exploratory campaign configs use break=0 to
  retain reports even on a low score; the repository acceptance threshold remains
  90% and no explicit mutation ignores were added. Process exit0 alone is not a
  qualification pass. Timeouts count as detected under Stryker, not individually
  proven kills.

Exact retained mutant replays1834/1592/1602/1699/1737 compiled and each failed15
focused cases; the original control passed30/30. Interval tests independently
challenge tail enclosure rather than only comparing production to itself.

## Review and remaining limitations

Automated simulated review perspectives covered arithmetic/status, function special
values/cohorts, predicates/payloads/environment, representation and whole-branch
integration. These were not external human experts. Findings fixed include decimal
tininess-before-rounding, extreme Exp10M1/Tanh, power/root/compound identities,
strict payload behavior, malformed corpus tokens and Pow/PowR(±0,−infinity) flags.
The final power replay passed831 checks. Documentation inventory drift was corrected.

Open qualification limits: raw external NaN/payload corpus admission; all-format
external encoding corpus; exhaustive/global elementary correct-rounding proof;
helper mutation and scripts/native-reference coverage. No ambient host state or
whole IEEE-version conformance claim. macOS CI remains disabled. Linux x64/ARM64
and managed x86 results require current remote CI evidence below; local Windows
execution does not stand in for them. Local WSL has SDK10.0.402, not the pinned11,
so no current local Linux product build is claimed.

The running older 18-hour integer soak remains pinned to its original source and
cannot qualify this new surface. Use the committed native `qualcompact` preprocessor
first when those results arrive; retain raw data according to the results policy.

## Reproduction

Run from a verified checkout directory. Use fresh run-specific artifact directories
whose parents already exist, and the SDK pinned by global.json. Principal commands:

```text
dotnet test SharpSafeNumerics.slnx -c Release --artifacts-path <run>/final-solution -m:4 --collect:"XPlat Code Coverage" --results-directory <run>/final-coverage
dotnet publish tests/SharpSafeNumerics.AotSmoke -c Release -r win-x64 --artifacts-path <run>/aot-verified-build -o <run>/aot-verified
<run>/aot-verified/SharpSafeNumerics.AotSmoke.exe
dotnet run --project tests/SharpSafeNumerics.PropertyTests -c Release --artifacts-path <run>/property-build
strictdoc export docs/requirements.sdoc docs/generator-requirements.sdoc docs/analyzer-requirements.sdoc --output-dir <run>/requirements-final
```

See `eng/DecimalConformance/README.md`, `MPFR_FUNCTION_REFERENCES.md` and
`FUNCTION_MUTANT_COUNTEREXAMPLES.md` for corpus/reference provenance and replay.
`eng/VerifyDecimalRoundingModel.ps1` verifies, emits C#, builds and executes the
coefficient model, requiring explicit artifact and solver paths. It reuses unchanged
translation inputs while still verifying on each invocation.



## Post-checkpoint corrections

Checkpoint6dc5fb4 passed Core CI (core+formal) and Windows x64/Linux x64/Linux
ARM64 tests and Native AOT. Managed x86 exposed a test-fixture issue: returning
an sNaN double from BitConverter quieted it before the API received it. Raw-memory
construction plus an integer-bit precondition preserves the intended input. The
corrected26-test conversion class passed on both x86/x64 against the identical
unchanged binary. No production arithmetic correction was inferred from that
fixture failure. Current remote checks are repeated after integration below.

A further integration review found that analyzers did not yet recognize the new
IEEE result/helper types. SSN1003 now recognizes discarded IeeeDecimalResult,
including nullable/out/aliases, and SSN1007 warns for allocating IEEE helpers.
Only inspected Copy/IsSignMinus/Radix paths are exempt. IEEE sign-changing helpers
use BigInteger codecs and warn. The19 added regressions bring the analyzer suite
to155 passes. Scoped mutation killed26/26 (100%, no survivors/timeouts), distinct
from a full-analyzer score. See the separate analyzer integration report/hashes.
The independent IEEE-only packaged consumer probe passed with SSN1003/SSN1007.

Survivor review reproduced an internal wide-interval Log convergence issue:
Log([1,4]) at scale1000 exceeded the2-second bounded probe; only that owned probe
was stopped. Monotone endpoint evaluation completed the green probe in311ms,
including launch. Point normalization now uses its decimal-digit invariant rather
than redundant correction loops. Near-one Log also avoids computing log(10) when
its exact multiplier is0, supporting the tiny normal-boundary tests without an
unused thousands-digit series. The factor4 log-tail comment accounts for outward
rounding at tested scales>=1000 and production scales>=10^39. No global runtime
bound is established.

Power/Pi/Compound cleanups remove branches excluded by integrality, positive root
upper-bound and validated-domain invariants. Checked bounds remain. Arithmetic and
metadata/conversion carriers now share one trap-priority implementation. Four
additional public-boundary facts challenge quiet payload kind, zero LogB poles,
infinite adjacency, extrema magnitude/cohorts and binary conversion trap errors.
Seventy-five independent function followups passed; nine exact campaign03 mutants
compiled and each failed15 of60 focused cases, while control60/60 passed. These
replays do not replace the final full-package mutation campaign.

The frozen-source repeat passed all1,928 tests with unchanged coverage above;
its raw outputs are `stable-solution.log`, `stable-coverage` and
`stable-coverage-report/Summary.json`. Current Native AOT is `stable-aot`.
The first closeout archive comparison detected a Decimal DLL mismatch; portable
PDB document checksums identify a source update during the initial build
(DecimalIntervalMath SHA25640F815... ->F0EA313...). The frozen-source repeat
passed: all eight archives reproduced byte-for-byte across a rebuild; all five
packages and the independent IEEE analyzer consumer controls passed. The first
mismatched archives remain retained as a failed attempt.
A parallel API-check attempt encountered constraint binaries temporarily removed
by the package rebuild; the retry used the stable Core test binary and all17 API
controls passed. These are recorded orchestration attempts, not hidden successes.

The initial checkpoint full-package Mutation CI scored89.32%, exit2 below90;
Core mutation passed. This historical run44352823343 is superseded by the final
source/configuration verification below. The final local full-package campaign
uses the unchanged repository config, all files/all1,487 tests, break90 and four
workers:2,951 generated,65 compile errors,8 uncovered,161 default block-filter
ignores,2,717 executing mutants. No mutate-filter or test-case-filter exclusions.
Final score and exact result counts are pending.
