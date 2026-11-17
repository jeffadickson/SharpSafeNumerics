# Issue 1 workstation evidence review

Results commit: a3dca0274e271fd0c0bebac0317a8c5517a49100.
[Results branch](https://github.com/jeffadickson/SharpSafeNumerics/tree/qualification/issue-1/20261117T185652Z-mt-209637-05c03199/qualification-results/workstation/20261117T185652Z-mt-209637-05c03199).
Tested package source: b98767333406e39c66d9e9a9e07e7daf27e91cb2.
This is independent-machine evidence for that earlier candidate, not execution
of the subsequent alias/API corrections or later test hardening.

## Outcome

The one-hour runner started at 18:56:52 UTC and finished qualification at
19:51:42 UTC, retaining the results-publication reserve. Machine MT-209637
reported 20 logical processors, Intel family 6 model 85 stepping 7.
All setup, build, deterministic/property, example, Dafny, StrictDoc, coverage,
AOT, packaging, package consumer, three soak and source-change checks passed.
No fatal error was recorded. Nine Pester cases passed. Regression counts were
173 Core, 82 generator and 97 analyzer (352 total), no failures/skips.

Two checks failed their unchanged 90% mutation break gates:

| Scope | Generated | Killed | Timeout | Survived | Uncovered | Compile error | Ignored | Score / result |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Core | 203 | 151 | 2 | 9 | 2 | 18 | 21 | 93.29%, passed |
| Generator | 440 | 330 | 0 | 34 | 3 | 59 | 14 | 89.92%, failed |
| Analyzer | 522 | 312 | 0 | 77 | 13 | 86 | 34 | 77.61%, failed |

Timeouts are detected under Stryker scoring, not assertion kills. These scores
must not be combined with later local campaigns. The generator result already
included 82 cases; it is newer than the older 79-case local Standard snapshot.
Analyzer survivors agree with the earlier local snapshot and motivate the later
catalog, malformed/null, scope and allocation hardening. Full current-source
campaigns are recorded separately in the RC closeout report.

## Soaks

All three ran for about 3,017 seconds with seed 731925 / 00000002OIo1:

| Mode | Samples | Type pairs | Arithmetic/policy outcomes | Batch lanes |
| --- | ---: | ---: | ---: | ---: |
| Auto, SIMD256 | 14,454,022 | 173,448,264 | 2,948,620,488 | 1,840,346,467 |
| AVX2 disabled, SIMD128 | 14,249,196 | 170,990,352 | 2,906,835,984 | 1,814,071,383 |
| Intrinsics disabled, scalar | 14,109,180 | 169,310,160 | 2,878,272,720 | 1,796,181,066 |
| Total | 42,812,398 | 513,748,776 | 8,733,729,192 | 5,450,598,916 |

Live managed-heap start/end snapshots were 24,960/70,328; 25,080/70,448;
25,072/70,440 bytes, respectively. These finite samples do not prove no leaks.
All modes used one worker; no throughput comparison with another machine or
with different concurrent jobs is claimed.

Native AOT passed 3,096 type pairs / 52,632 outcomes and 257 batch lanes,
selecting SIMD128. This differs from the managed automatic soak's SIMD256
selection and is not a contradiction: the native portable image's ISA scope differs.

## Coverage and disposition

| Assembly | Covered/total lines | Covered/total branches |
| --- | ---: | ---: |
| Core | 158/158 | 129/134 |
| Generator | 511/513 | 256/269 |
| Analyzer | 362/375 | 463/564 |

The report's combined 98.5% line / 87.6% branch figure is retained as a multi-report
summary, not compared as if it had the same scope as later per-component runs.
Subsequent source changes fixed generic/generated receiver-output aliasing,
parser/decoder overlap and aliasable batch-width metadata. This old run cannot
qualify those changes; later local and platform runs exercise the reviewed code.

The runner installed SDK/runtime/documentation copies in its isolated cache.
Reuse detection was corrected in 2f0699a after this immutable runner was downloaded;
that correction is not credited to this run. Raw logs/artifacts remain on the
workstation at the paths recorded in summary.json. No old source was merged into
the development branch and the results branch was preserved. Issue 1 remains open
for final RC acceptance; a results upload alone is not qualification closure.
