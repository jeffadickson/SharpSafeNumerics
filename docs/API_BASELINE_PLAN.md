# RC1 API contract guard plan

This records the initial four-project phase. The subsequent user-approved numeric
expansion adds a fifth guarded optional Decimal project and targeted decimal
metadata tests; see NUMERIC_EXPANSION_PLAN.md and the expanded verification report.

Use Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0, the listed stable release,
as a private build-time dependency on the four shipping projects. The cached
4.14.0 is unlisted, so availability alone does not justify selecting it.
Primary sources: https://www.nuget.org/packages/Microsoft.CodeAnalysis.PublicApiAnalyzers
and the Microsoft PublicApiAnalyzers usage documentation linked from its source.
MIT; no runtime package dependency is introduced.

Alternatives: SDK package validation already checks package consistency and will
support compatibility against a published baseline, but does not by itself keep
a source-reviewed signature list. SDK GenAPI is a generation tool, not a build
change gate. A handwritten reflection/signature serializer would duplicate the
maintained analyzer's behavior, nullability and language handling.

Acceptance: source-controlled nullable-aware API lists for shipping projects;
clean Release build; meaningful negative controls for added/removed/changed API;
verify C#15 union coverage explicitly; unchanged runtime dependency payload;
regression/coverage, package reproducibility/consumer and CI checks. New baselines
are Unshipped until publication, rather than representing unpublished APIs as a
previously stable release. Unsupported surfaces must remain explicitly unmeasured.

Steps: add private package and empty lists, observe missing-API diagnostics;
use the official code fix to generate candidate lists; review source interfaces
against lists; prove detection with isolated/reverted controls; document limits,
trace StrictDoc, verify and commit/push. The active workstation run stays pinned
to 2e242881cb9a5075cb4fceb9f4df7cb7585efc3a throughout.
