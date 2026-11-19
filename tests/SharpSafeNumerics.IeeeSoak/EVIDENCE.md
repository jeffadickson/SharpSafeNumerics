# Bounded runner qualification, 2026-11-18

This evidence qualifies the nonshipping campaign helper, not complete IEEE754
conformance. Shipping Decimal coverage and mutation gates are separate.

- Strict Release test run: 19 passed, zero failed/skipped (`qualified-tests.log`).
  Includes malformed CLI/output handling, deterministic all-format/all-mode
  recipes, CsCheck 100 cases with seed 7542019, reference-policy regressions,
  signaling NaNs and an actual short campaign.
- Actual CLI: `--seconds 2 --seed 7542021`, exit 0, Status Passed, 21 generated
  samples, 105 checks, 315 assertions, zero failures/inconclusive, 2.4931552 s.
  The deadline is checked between numerical cases; it is not a hard interrupt.
- Coverlet runner-assembly scope: 89.87% line, 71.81% branch. Error/cancellation
  paths, process entry point and generated model code are not fully exercised.
- Stryker 5.1 parser-only scope (`RunOptions.cs`, eleven control tests, two
  workers): 88.89%, 56 killed, six survived, one no coverage, thirteen compile
  errors. This diagnostic does not meet 90%; no threshold or exclusion was
  changed to represent it as passing. Survivors include diagnostics text,
  seed-one rejection, the revision predicate and missing-summary guard.

Two initial independent-reference defects were retained and corrected with
regressions: finite-precision RemainderNear rejected a huge integral quotient
(use exact unlimited remainder before target rounding); binary conversion of a
signaling NaN needed the reference precision operation to quiet and signal
Invalid. These were oracle corrections, not shipping-library defect claims.

Raw evidence is local under
`E:\Artifacts\SharpSafeNumerics\windows-x64\ieee-decimal-20261118\ieee-soak`:
`qualified-tests.log`, `qualified-smoke-a5ef3e757dae4049930b316c24cd89eb.json`,
`coverage-qualified/5aa1027f-63d8-47a3-a859-40b3a8d82cfb/coverage.cobertura.xml`,
`mutation-options.log`, `mutation-options/reports/mutation-report.json`.
The tested runner DLL SHA256 is
`DD99BEEEABFB7CE4C316E614616D03E80697152BA0B25065554C6EEB93B19425`.
Each summary also records actual runner/Decimal DLL hashes, runtime, reference
versions and the caller-supplied source revision. Checkpoints are recovery
observations, not successful final summaries. Native AOT was not qualified.

Elementary references at 160 and 224 decimal digits are independent numerical
checks, not directed error enclosures. Disagreeing target-round results are
Inconclusive (exit 3); skipped/ambiguous checks are never counted as passed.
No long-running campaign was started during this qualification.
