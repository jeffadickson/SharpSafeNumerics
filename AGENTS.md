# Coding agent instructions

Implement measurable requirements; never claim builds or tests passed without logs. Preserve the core's BCL-only dependencies and net11.0/C#15 target. Distinguish API non-throwing behavior from *allocation-free* behavior. Never use compiler-generated value-type unions in strict zero-allocation code, since they box. Prefer bounded data structures and typed errors. Expand exhaustive and BigInteger oracle tests before optimization. Run the actual .NET 11 SDK compiler and GitHub Actions. Publish exact failures and follow up with fixes before calling the implementation green. Do not add runtime reflection or unchecked truncation without a named policy.


## Repository identity and verification rules
Commit messages and repository content must not contain assistant-product attribution. Commits are made through the repository owner's GitHub identity. Use CsCheck and Hedgehog as independent randomized/shrinking layers, xUnit for mutation-friendly regression tests, Stryker.NET for mutation analysis, and Dafny for mathematical specifications where practical. Formal models are not to be described as proofs of C# implementation equivalence unless an actual refinement/linking proof exists. Attributes are preferred as declarative domain contracts; generators and analyzers consume them so runtime reflection is unnecessary.
