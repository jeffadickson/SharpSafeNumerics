# Strict integer conversion pass — 2026-11-17

Goal: preserve the public generic conversion signature and OutOfRange/output-zero
contract while eliminating exception-driven rejection for built-in integer pairs.
The delegated engineering scope authorizes this bounded pass without further design approval.

Reuse assessment: .NET 11 generic math is already a runtime dependency. Its
CreateTruncating operation supplies a candidate without range exceptions. Publish
that candidate only when its sign and conversion back to the source preserve the
original value. Sign comparison is essential: modulo round trips alone can accept
negative-to-unsigned and high unsigned-to-signed conversions. No added runtime
package, reflection, BigInteger intermediate, or signature constraint is needed.
Binary span conversion is another BCL option, but needs a size bound or an
unbounded buffer for existing arbitrary-width generic callers. Catching
CreateChecked preserves semantics but fails the allocation requirement.

Primary API contract: https://learn.microsoft.com/en-us/dotnet/api/system.numerics.inumberbase-1.createtruncating?view=net-11.0

Custom generic-math providers remain unqualified and may throw for unsupported
conversions. BigInteger compatibility is regression-tested, not allocation-qualified.
No proof of the generic-math implementation is claimed. A useful future Dafny
candidate is sign-preserving modulo round-trip admission; this pass instead models
mathematical conversion admission separately, without claiming C# equivalence.

Steps:
- [ ] Introduce StrictDoc requirements preserving SN identifiers; link implementation,
  tests and bounded evidence. Add failing success/failure allocation regression.
- [ ] Implement candidate validation; test all 144 ordered built-in type pairs at
  endpoints, destination boundaries and deterministic byte-generated values.
- [ ] Extend Native AOT smoke and independent property suites for conversion.
- [ ] Run regression, coverage, mutation, formal, AOT and benchmark checks; inspect
  surviving mutants, update status documents and push coherent verified commits.

Baseline: main 4970be40517c12ca968f48fc1e01baec8766128c; Windows x64 .NET
SDK 11.0.100; xUnit 92 passed, 0 failed, 0 skipped. Core CI and Mutation succeeded
on that revision. Bulk evidence is kept in a unique E: artifact run directory.
