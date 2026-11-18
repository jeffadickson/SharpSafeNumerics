# Function qualification and invariant cleanup after campaign03

Campaign03's seven-file diagnostic score **87.02%** is historical evidence for
its own snapshot. The repository gate applies to the **full Decimal assembly at
90%**, not an invented independent threshold for this subset. No mutation ignores,
exclusions or threshold changes were added here. A later full-package Stryker run
must supply its own result; none is inferred from individual replays.

## Changes preserving the supported contract

- In the finite nonintegral Pow branch, a decimal exponent q>=0 is impossible:
  its value C×10^q would be integral. The exact rational now constructs its
  denominator10^-q directly, removing an unreachable conversion arm.
- NthRootFloor starts with high=2^ceil(bitLength(value)/n), hence high^n>value.
  Updates preserve low^n<=value<high^n. At adjacent endpoints low is the floor
  root, so the redundant final high^n comparison was removed. The proof is a
  mathematical loop-invariant argument, not a formal C# refinement proof.
- Nonlattice Pi reduction cannot receive q>=0, because that operand is an integer
  and the preceding exact twice-value lattice branch already returns. The
  remaining negative quantum is used directly for its rational denominator.
- Compound validates finite value>=-1 before forming exact1+value. Its coefficient
  is nonnegative, and zero returns before subsequent arithmetic. The dead
  negative-result state and sign-negation paths were removed. Quantum, flags,
  rounding and trap contracts are unchanged.

Checked arithmetic and numerically stable sign-oriented elementary formulas were
retained. No tests were added solely to require private optimization thresholds.

## New semantic qualification

The staged45 cases were integrated as `IeeeFunctionFollowupTests.cs`; thirty more
format/mode cases were added. The **75 follow-ups** cover supported decimal32/64/
128 and all five rounding directions:

- even signed-zero roots and preferred quantum, negative-infinity invalid roots,
  signaling-NaN identity quieting/payload/Invalid;
- zero/infinity powers, PowR-specific NaN propagation and unrelated-domain operands;
- positive-quantum Pi periodicity, exact Compound scaling and independent1/121;
- reciprocalSqrt(4.0) odd quantum, hypot(3E1,40.0) exact scale, exp2(1E1),
  log2p1(3E1) analytical bounds, and sin(256) negative-quadrant behavior;
- exact minimum-normal inputs: log1p(x)<x<expm1(x). Tiny-before-rounding means
  log1p raises Underflow|Inexact even when rounded to normal x, whereas expm1
  raises Inexact alone. Directed adjacent values and Underflow traps are checked.

The normal-boundary cases exposed unnecessary high-precision work in the interval
engine: Log evaluated log10 even when multiplying it by zero. The interval owner
added the exact exponent0 shortcut. A pre-fix test run was stopped after98.05CPU
seconds; this was a known-cost diagnostic, not a memory limit or numerical failure.
The complete75-case post-fix run passed in895ms. These are not comparable complete
benchmarks and no speedup ratio is claimed.

## Executed evidence

Artifacts:
`E:/Artifacts/SharpSafeNumerics/windows-x64/ieee-decimal-20261118/functions-matrix`.

| Scope | Result |
|---|---|
| Before cleanup, integrated follow-ups | 45 passed, zero failed/skipped |
| After cleanup, nonboundary follow-ups | 60 passed, zero failed/skipped |
| After interval exponent0 shortcut, all follow-ups | 75 passed, zero failed/skipped;895ms |
| Full Decimal test assembly | **1487 passed**, zero failed/skipped;4s |
| Full-suite IeeeDecimalFunctions coverage | **98.34% line /97.32% branch** |
| Full-suite IeeeDecimalPiFunctions coverage | **100% line /98.83% branch** |

Strict builds used SDK11.0.100/net11.0/C#15 without warning suppression. Outputs
were isolated through `--artifacts-path .../campaign04-build`; no ordinary build
directory was used. Source hashes before/after are
`campaign04-before-source-hashes.json` and `campaign04-after-source-hashes.json`.
Logs: `campaign04-before-tests.log`, `campaign04-after-tests.log`,
`campaign04-full-regression.log`. Coverlet XML:
`campaign04-full-coverage/21d79de6-3b43-4612-9711-802705bb7999/coverage.cobertura.xml`.
Coverage scope is the Decimal test assembly, not the whole repository.

Nine **exact campaign03** mutations were separately replayed in disposable source
copies using authoritative Stryker locations and replacements: **1829,1654,1652,
1979,1787,1763,1493,1305,1392**. The unmutated control passed60/60 selected tests.
Each mutant compiled and produced15 failures/45passes, zero skips; no compile
error was counted as detection. Inputs/source hashes, precise replacements,
individual logs and `replay-summary.json` are retained under
`campaign03-exact-replay`. The original production tree was never mutated by
this replay. `Replay-FunctionMutants.ps1` accepts `-Mutants`, `-TestFileName` and
`-TestCaseFilter` for reproduction; its default prior-campaign usage is documented
in FUNCTION_MUTANT_COUNTEREXAMPLES.md.

These individual detections are not an aggregate mutation score. Remaining
survivors, all-domain numerical proofs, dedicated fuzz/property coverage, CI and
cross-platform qualification remain governed by the repository's broader evidence.
Source/tests were frozen after these results for the next full-package campaign.
