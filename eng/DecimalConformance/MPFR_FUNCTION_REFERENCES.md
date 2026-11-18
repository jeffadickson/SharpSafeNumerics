# Independent recommended-function references

`MpfrFunctionReference.c` is original, verification-only glue to installed GNU
MPFR4.2.1. The native library is dynamically linked, not copied or added to
production dependencies. Numerical outputs are reproducible data, not copied
library implementation. MPFR is LGPL3-or-later; retain its licensing information
if distributing the library. Primary documentation:
https://www.mpfr.org/mpfr-4.2.1/mpfr.html and https://www.mpfr.org/.

The 79 public-API fixtures cover all 40 elementary/root/power/Pi-scaled functions
in this expansion. Cases are a deterministic, explicitly listed grid (no random
seed). Integer and dyadic inputs parse exactly. Non-dyadic decimal inputs use
both directed parsing endpoints. In those bounded domains the generator explicitly
orders endpoints using monotonicity: increasing for the listed positive-base
exponentials/logarithms/powers and odd increasing functions; decreasing for
cos/acos on their listed positive intervals, reciprocal square root, negative
root indices and negative compound exponents. Negative-base powers have positive
odd integer exponents. Binary second arguments must parse exactly; generation
rejects others. This is **not a general interval implementation**.

Each lower function endpoint is evaluated with MPFR_RNDD and upper with
MPFR_RNDU. Decimal emission also rounds outward (`%.199RDe`, `%.199RUe`, 200
significant decimal digits). The nearest column is diagnostic only. The staged
xUnit matrix rounds both bounds with independent PeterO.Numbers1.8.2 to the
selected decimal format/mode and requires agreement before checking production
values. Thus the finite cases do not rely on simply trusting a nearest binary
approximation. MPFR's documented per-operation correct-rounding contract and
the stated fixture-domain/monotonicity assumptions support these references;
they do not prove C# implementation equivalence or whole-standard conformance.

Both 1024-bit and 2048-bit executions produced identical public fixture bytes:
SHA256 `6EFC5225C472869AF559AE84870B2203859D89ABCAF3D31B0A8329226FFDC43F`.
The JSON file records these data and provenance. The test source embeds the same
bounded fixture rows so CI needs no artifact drive or native library.

The separate `internal` mode emits 24 exact-input bounds for Pi, Exp, Log, Atan,
Sin and Cos at valid members of {-2,-0.125,0.125,2,10}. It asserts input endpoints
are identical, so no monotonic interval assumption is needed for those point
references. Decimal emission has 80 significant digits (`%.79RDe`, `%.79RUe`).
1024/2048 outputs match with SHA256
`E909B3802C5ECAB0E3A9005CD3E434026336AD78186C58D5BD19AB36AC539555`.

Reproduction uses existing WSL Ubuntu24.04 gcc13.3.0, MPFR4.2.1 and GMP:

```powershell
$repo = 'C:\dev\SharpSafeNumerics-ieee' # actual checkout
$run = 'E:\Artifacts\SharpSafeNumerics\windows-x64\ieee-decimal-20261118\functions-matrix'
if (!(Test-Path -LiteralPath $repo -PathType Container)) { throw 'Checkout missing' }
if (!(Test-Path -LiteralPath $run -PathType Container)) { throw 'Verified run directory missing' }
& (Join-Path $repo 'eng\DecimalConformance\Generate-MpfrFunctions.ps1') -ArtifactDirectory $run
```

Native build uses C11, -O3 and enabled loop/SLP vectorization. No SIMD or speedup
claim is made. Linux executable/cache stays under `/tmp` on a native filesystem;
raw reports and fixtures are explicitly routed to the run directory. No tools
are installed or changed. The generator source hash and installed library hash
are retained with artifacts. The script reuses the fixed-profile executable by
source hash; change its build profile/cache policy if changing compilers/libraries.

Executed staged test result: 1,260 passed, zero failures/skips. These comprise
1,185 independently bounded reference cases (79×3formats×5modes) and 75 analytic
groups covering every API's NaNs, payloads, trap/context failures, signed zeros,
domain/poles, infinities, Pi lattice, exact identities/cohorts, bounded huge
cutoffs and integer-index extremes. Analytic groups contain multiple assertions;
they are not counted as additional xUnit test cases.

Staged-suite-only Coverlet evidence: IeeeDecimalFunctions94.62% line/92.7% branch;
IeeeDecimalPiFunctions100% line/87.22% branch. This scope differs from integrated
suite coverage. Internal interval exceptional/refinement paths and remaining
mutants are still open. No full-domain proof, dedicated fuzz campaign, Native AOT
or cross-platform qualification is claimed by this matrix. Production source was
unchanged during this task; root integration and mutation are separate steps.
