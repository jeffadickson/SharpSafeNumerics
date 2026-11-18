# Executed bounded reference evidence — 2026-11-18

Host: Windows x64, .NET SDK11.0.100/runtime11.0.0; native reference Ubuntu24.04
WSL, gcc13.3.0. Production sources were under concurrent implementation; see
`tested-source-hashes.json` for exact tested source content, not a claim about a
later commit. All original corpora/reports remain in
`E:/Artifacts/SharpSafeNumerics/windows-x64/ieee-decimal-20261118/conformance`.

| Command scope | Passed | Mismatched | Skipped | Malformed |
|---|---:|---:|---:|---:|
| Reference `add.decTest` | 2094 | 0 | 6 | 0 |
| Reference original `smoke.decTest` | 11 | 0 | 1 | 0 |
| Production decimal32 original smoke | 10 | 0 | 2 | 0 |
| Production decimal64 `dd{Add,Subtract,Multiply,Divide,FMA,Quantize}.decTest` after repair | 3759 | 0 | 1072 | 0 |
| Production decimal128 `dq{Add,Subtract,Multiply,Divide,FMA,Quantize}.decTest` after repair | 3823 | 0 | 1007 | 0 |
| BCL exp/log/sqrt sample comparisons to 160-digit PeterO | 63 | 0 | 0 | 0 |

Production replay command shape:
`dotnet DecimalConformance.dll --production decimal64 FILE.decTest [...]`.
Reference replay omits `--production FORMAT`. `--probe` runs the BCL sample.
Precise paths/checks are in README. JSONL contains expected result/cohort,
expected five IEEE flags and observed values/flags. Each skipped row has a
reason: unsupported rounding/operation, fixed-format context mismatch, operand
not exactly representable with the same cohort, or raw NaN admission unavailable.
Do not combine these counts into a whole-format or whole-standard success rate.

The linked build command used
`dotnet build eng/DecimalConformance/DecimalConformance.csproj -c Release --nologo -p:NoWarn=RS0016`
while API baselines were being integrated. It passed with zero warnings/errors;
the override means this was **not a normal repository acceptance build**. The
earlier independent-only helper build passed without overrides. A subsequent
rebuild initially failed because a broad reference run still held the output
DLL; the identified helper process was stopped, then the build/replays repeated.

Initial production corpus execution found two real regressions: `ddmul906` and
`dqmul908` rounded tiny products to minimum normal without Underflow. IEEE754-2019
§7.5 requires decimal tininess **before** rounding. Native decNumber independently
returned the expected decimal64 result and raw status14368
(Underflow|Subnormal|Rounded|Inexact). The arithmetic worker repaired this and
the same corpus rows passed on replay. The original bad reports were superseded
by final reports; the finding and exact corpus IDs remain here.

Native adapter build/run:
`Build-NativeReference.ps1 -ArtifactDirectory RUN` produced
`/tmp/sharpsafe-decnumber-12484/decimal-reference`. Flags were C11, -O3,
loop+SLP vectorization enabled, -Wall -Wextra, DECNUMDIGITS=2000. The unmodified
upstream decNumber code emitted three maybe-uninitialized warnings in its
CompareTotalMag/exp implementation; no claim is made that those paths are
warning-free. No SIMD performance claim was tested. The glue was also initially
compiled at -O2 before the reproducible -O3 script was added.

Native executed examples: exact addition1+2 ->3/status0; decimal32 1/3 ->
0.3333333/status2080; decimal128 sqrt2 ->
1.414213562373095048801688724209698/status2080; exact ddmul906 above.
Raw decNumber status bits are distinct from the project's IEEE enum.

A broader PeterO campaign emitted 13,202 passing rows with no mismatch before
an expensive power case was stopped to unblock rebuilding. It has **no final
summary**, is incomplete, and is excluded from completed-run counts. Its older
parser reported ten width-qualified quantize rows as malformed; these valid
GDA constructs are now explicitly recognized as unsupported raw-format cases.

Historical slice limitations at this stage: no helper line/branch coverage measurement, no helper mutation,
fuzz/property campaign, no Native AOT qualification. Production tests/coverage
are separate work; reference sampling is not evidence of those checks. Native
source is unmodified upstream3.68 (predates2019, consult errata). No raw NaN
operand payload propagation or encoding corpus is qualified here. No production
transcendental global correct-rounding claim follows from 63 ordinary samples.

## Subsequent integrated evidence

The final helper has measured Coverlet.Console coverage:172/191 lines(90.05%)
and243/305 branches(79.67%), excluding scripts/native glue/dependencies. Four
malformed quote/framing fixtures now return zero passes/four malformed and direct
exit2; unreadable input is also structured exit2. No helper mutation result is
claimed. Production Native AOT, MPFR matrix, property and integrated coverage are
separate later checks recorded in `docs/verification/2026-11-18-ieee-decimal.md`.
The earlier slice above remains historical evidence, not the current total.
