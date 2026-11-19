# Workstation IEEE decimal week campaign review

The workstation campaign `week-20261118T175953Z-c68e0d9f` published its final
report on results commit `8c78514cb493fcb1be69b75fa3f7c4831f783610`.
The native `qualcompact` preprocessor reviewed the published snapshot before raw
report inspection. Its verdict was Failed with one failed chunk and ten
inconclusive chunks, covering6,175,528 samples and92,631,423 assertions on
source revision `9eea7ed823d09cace81328786aafa6e7d709f168`.

The failure was Decimal32 nearest-even multiplication of `6E-60` by
`1.766751E-38`. PeterO.Numbers1.8.2 expected `1.0600E-97`, while the package
returned `1.0601E-97`; both reported Underflow and Inexact. Exact integer
analysis gives `1.0600506E-97`, and Python decimal independently returns
`1.0601E-97` under the Decimal32 precision and exponent limits. The soak oracle
now forms and rounds the exact integer product itself. It preserves the
preferred quantum, signed zero, special-value signaling and all five rounding
directions. A regression replays the published raw operands.

All ten earlier chunks were inconclusive because occasional elementary results
rounded differently at160 and224 working digits. A captured case was Decimal32
`ExpM1(1E-80)` toward positive, where the target results were `1.000000E-80`
and `1.000001E-80`. The oracle now increases working precision through1,280
digits until two consecutive target-format roundings agree. A failure to
stabilize remains inconclusive. The captured raw operands are a permanent
regression.

The immutable published snapshot, compact JSON and full preprocessor details
are retained under
`E:/Artifacts/SharpSafeNumerics/workstation-week-review-8c78514c-71e42f`.
Diagnostic replays are under
`E:/Artifacts/SharpSafeNumerics/ieee-soak-diagnostics/seed-7542020`.
This failed campaign does not qualify the corrected oracle or current main. A
replacement long campaign must use the corrected source revision.

The focused regressions first failed against the published counterexamples and
then passed after the oracle corrections. All21 soak-runner tests passed. A
150-second replay of workstation seed7542020 completed16,921 samples and253,815
assertions with0 failed and0 inconclusive checks, crossing both captured failure
locations. The replay summary is
`E:/Artifacts/SharpSafeNumerics/ieee-soak-replay-clean/seed-7542020-7fa7d1cf54cf429494a46e2e4fa57368.json`.
Focused coverage measured CaseVerifier at95.93% line and79.82% branch. The whole
instrumented dependency closure was67.46% line and31.80% branch because it also
included production assemblies outside these harness tests. Coverage is retained
under
`E:/Artifacts/SharpSafeNumerics/windows-x64/soak-oracle-cleanup-f6f32a43176649968863b01cfe5289ce`.
