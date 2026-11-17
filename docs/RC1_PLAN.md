# RC-1 implementation and qualification plan

Baseline: 15a138c6e04e4830aa729282af1ce9026c58efc4.
User-directed scope: complete annotation/generator and strict diagnostic contracts,
wide arithmetic/policy oracle campaigns, invalid-policy validation, survivor review,
Linux qualification, one gated macOS run followed by disabling that gate.
Preserve net11.0/C#15, warnings-as-errors, BCL-only reflection-free runtime,
explicit failure statuses and owner-only commit identity. No routine approval pauses.

Reuse: retain Roslyn incremental generation/operation analysis, existing generic
math, BigInteger verification oracles, xUnit, CsCheck, Hedgehog, Stryker and Dafny.
No new runtime arithmetic framework is needed. SIMD applies to independent buffer
lanes, while current APIs are scalar; assess intrinsics separately without claiming
a scalar speedup or weakening per-value rejection semantics.

1. Core: all undefined policy bytes rejected before arithmetic, on success and
   failure alike. Wide boundary Cartesian products and seeded full-width arithmetic
   oracle cases for all 12 integer types, all five Try operations and four policies
   on Add/Subtract/Multiply. Outputs clear on rejection; division truncates to zero;
   wrapping uses independent mathematical modulo and saturation selects endpoints.
2. Generator/annotations: implement policy consumption, explicit default validity,
   invariant span parsing/formatting, fixed-width endian codecs, optional unit
   metadata, explicit exact conversions, equality/order/operators, XML docs and
   debugger display. Diagnose unsupported shapes/collisions/options and all storage
   extrema. Keep reflection out; no type converter is needed for RC-1. Named Wrap
   means primitive modular arithmetic followed by domain admission, not domain
   modulo. Saturate clamps to domain bounds. Add/Subtract Try paths always reject.
3. Analyzer: operation-based SSN1001-1008 with bounded, documented static detection;
   method/type/nested/lambda/local-function scopes and positive/negative regression
   tests. No claim of whole-program allocation or dataflow proof.
4. Integrate examples, AOT smoke, requirements, docs, RC package metadata and
   platform workflows. Collect meaningful scoped coverage and mutation evidence;
   do not exclude source to inflate scores. Preserve equivalent-mutant dispositions.
5. Review source, run all checks, commit/push coherent milestones, verify CI.
   macOS remains gated off except the single explicitly authorized qualification run.

Parallel ownership: generator worker owns NumericAnnotations.cs, Generator source,
GeneratorTests project, generator-specific requirements/docs and generated example.
Analyzer worker owns Analyzers source, AnalyzerUnitTests project, analyzer-specific
requirements/docs and existing analyzer probe. Coordinator owns other files,
solution/workflows/versioning, core/tests/properties/formal and final integration.
Workers do not commit or push the shared checkout. Their reports retain exact
commands/counts, source limitations and remaining gaps; coordinator integrates them.
