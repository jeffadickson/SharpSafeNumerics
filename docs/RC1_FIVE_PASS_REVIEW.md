# RC1 five-specialist, five-pass review

Date: 2026-11-17. Source: commit 2e24288 plus the uncommitted API-guard work.
Later user decision: implement fixed point and safe APIs around NET11 built-in
IEEE decimals now, before RC1. This supersedes the deferral recommendation below;
see NUMERIC_EXPANSION_PLAN.md and FIXED_AND_DECIMAL.md. The package set expands
from six archives to eight. The review remains a dated decision record.
This was a simulated specialist review by five independent review agents, not
external human experts. Reviews inspected source and documents; they did not
execute checks. Separately, the coordinator observed 400 Release tests pass
(191 Core/union, 89 generator, 120 analyzer), with zero failures/skips.

## Panel

| Specialist | Principal finding |
| --- | --- |
| Numerical correctness | Existing integer semantics appear implemented; the product roadmap still includes other numeric models. |
| Roslyn/compiler and public API | Public annotations and SSN1001–1008 are implemented within documented limits; Unit metadata is not SharpUnits integration. |
| SIMD/platform/Native AOT | SIMD is signed Int32 batch addition; configured CI and observed execution must remain distinct. |
| Verification/formal/mutation | A passing mutation gate does not dispose every survivor; model proofs do not prove the C# implementation. |
| Release engineering/product | Requirement dispositions and current status documents must agree before RC tagging. |

All five specialists reviewed five successive refinements from their respective
perspectives. The following consolidated iterations record the resulting plan
changes, rather than counting cosmetic edits as iterations.

## Iteration 1 — correct the release inventory

Initial proposition: finish qualification of an implemented integer release.
All five reviewers rejected the broader interpretation that no product features
remain. SN-017 SharpUnits integration is P1 and has no sample/compile evidence;
SN-018 fixed point and SN-019 Decimal32/64/128 are P2 and remain unimplemented.
SN-015 published x64/ARM64 benchmark baselines remain an evidence deliverable.

Revision: freeze the existing integer/annotation contract as the proposed RC1
scope, retain every existing requirement ID, and show the open product features.
Do not silently mark P1 requirements deferred or implemented. Recommendation:
leave P2 numeric expansions post-RC1; resolve SN-017 and SN-015 explicitly before
the release decision. This recommendation is not an approved scope deletion.

## Iteration 2 — define the actual consumer contract

Numerics: retain five Try operations and four policies on Add/Subtract/Multiply;
generated bounded domains have their documented narrower operation surface.
Compiler: all current annotation options are consumed; analyzers have static
dataflow and indirect-allocation limits. Platform: SIMD128/256 applies only to
Int32 batch addition, with scalar fallback and tails. Verification: retain
independent BigInteger oracles and failure-output checks. Release: actual
SharpUnits interoperability requires a real referenced sample, not a display
string or an example merely named after a downstream application.

Revision: no speculative generic SIMD or decimal expansion during qualification.
If SN-017 is retained for RC1, research the actual SharpUnits API/package and
compile a bounded integration sample before calling it implemented.

## Iteration 3 — close the observed API guard gap

The compiler reviewer identified the distinction between source signature lists
and generic metadata. Verification confirmed that PublicApiAnalyzers accepts a
compiling relaxation of NumericResult<T>'s struct constraint. The proposed fix
uses the maintained analyzer for source signatures plus targeted metadata tests,
not a new universal compatibility engine. Other reviewers require unchanged
runtime dependency payload, numeric semantics and Native AOT behavior.

Revision: finish four nullable-aware API baselines, 16 constraint regression
cases, actual weakened-DLL rejection, restored baseline build and archived
control logs. Full released-package binary compatibility remains unqualified.

## Iteration 4 — reconcile evidence and provenance

Numerics/platform found stale wide-arithmetic and ARM64 status in product/parity
documents. Compiler/release found older test counts and missing scope decisions.
Verification requires survivor dispositions, timeout separation and source
provenance. The independent 18-hour run remains pinned to 2e24288; current work
does not update that run. Later unchanged runtime hashes can support a narrowly
stated relationship, not a claim that the soak tested a newer complete checkout.

Revision: update current status summaries while preserving dated reports. Review
the run's QualificationStatus, failed/skipped steps, source integrity, seeds,
three modes, duration and package hashes after it returns. Do not infer success
from upload. Keep macOS disabled; observe Windows/Linux jobs on the final commit.

## Iteration 5 — make the release gate reviewable

All perspectives converge on a bounded integer RC1, conditional on explicit
disposition of the remaining product requirements and final evidence. No source
review established a new numerical implementation defect; this is not a proof
that none exists. Preserve application WCET/allocation scope, 32-bit execution,
actual downstream integrations, general binary compatibility and cross-path
archive identity as open limitations.

Final revision: complete API guards; reconcile requirement/status documents;
resolve SN-017/SN-015; review surviving mutants and the pinned soak; run and
observe final coverage/property/negative/formal/package/AOT/platform checks;
commit/push verified milestones. Prepare immutable package hashes and release
notes. Do not tag or publish NuGet as an automatic consequence of passing CI.

## Final ordered plan and acceptance

1. **API contract milestone:** commit all eight PublicAPI files, private analyzer
   reference, metadata tests, control script and CI evidence upload. Normal tests
   pass; the weakened DLL fails the intended assertion; restored builds pass.
2. **Requirements/status milestone:** reconcile PRODUCT_REQUIREMENTS.md,
   FEATURE_PARITY.md, QUALIFICATION_BACKLOG.md, RELEASE_NOTES_RC1.md and the real
   StrictDoc requirements. Preserve SN-017/018/019 and their open status. Record
   SN-015/SN-017 release disposition explicitly; an inferred deferral is not closure.
3. **Survivor milestone:** retain each campaign revision and scored population;
   triage potentially behavioral survivors, add meaningful regression tests where
   warranted and replay affected scopes. Report assertion kills and timeouts
   separately. Never combine targeted scores with historical full scores.
4. **Soak milestone:** inspect the uploaded immutable-source run when available;
   require an actual Passed qualification with no unexplained source changes or
   skipped required checks. Failed/incomplete results trigger diagnosis, not release.
5. **Candidate gate:** observe final-source Windows/Linux x64/ARM64 tests and AOT,
   coverage, CsCheck/Hedgehog seeds, Dafny/StrictDoc, fresh package consumers and
   same-checkout six-archive rebuild reproducibility. Preserve measured scopes.
6. **Release decision:** verify requirement dispositions, package metadata/API
   lists, hashes and documentation against the exact commit. RC tagging and public
   package publication require a separate explicit instruction.

Evidence references: PRODUCT_REQUIREMENTS.md, ANNOTATIONS_AND_GENERATION.md,
GENERATOR_RC1.md, ANALYZERS_RC1.md, requirements.sdoc, SafeInteger.Batch.cs,
ApiConstraintTests.cs, VerifyPublicApi.ps1, .github/workflows/platform.yml and
verification/2026-11-17-rc1-closeout.md. This plan does not claim pending gates ran.
