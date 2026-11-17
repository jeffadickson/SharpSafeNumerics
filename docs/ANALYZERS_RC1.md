# Strict numeric diagnostics

Reference the SharpSafeNumerics analyzer with the core library to enforce the
rules below in a consumer build. Apply `[StrictNumeric]` to a type or method.
Its scope includes the type's field initializers and member bodies, nested types,
and local or anonymous function bodies. A local function, accessor, or
parenthesized lambda can also carry the attribute. Scope is lexical, including
code following an `await` in an annotated body. Calls into other methods and
subtypes do not inherit the annotation from the caller or base type.

`SSN1003` applies throughout consumer source. It and `SSN1007` are warnings by
default. The other six diagnostics are errors. A build can promote the
warnings to errors if the consumer's requirements demand it. The analyzer
uses the bound operation tree and recognizes the library's actual symbols,
including attributes retained in assemblies and Core metadata available
through an extern alias. Generated bodies are excluded from consumer analysis.

The analyzer keeps the existing Roslyn dependency. Its operation model
provides bound symbols, typed conversions, and compiler constants, which
distinguish similarly named methods and variables from library contracts.
A syntax action retains the explicit `unchecked` wrapper because its
operation children only expose the compiler's checked flags.
[Roslyn operation model](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.ioperation)
[Generic math contract](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.inumberbase-1?view=net-11.0)

| ID | Reports | Preferred operation |
| --- | --- | --- |
| SSN1001 | Explicit conversion between built-in integer types when the destination cannot represent the source's entire range, or an exact source constant is outside the destination range. Includes tuple elements and lifted casts and checks native widths at 32 and 64 bits. | `SafeInteger.TryConvert` or a generated `TryCreateFrom` |
| SSN1002 | Explicit `unchecked` scopes and nonconstant unchecked built-in addition, subtraction, multiplication, negation, increment, or decrement. Covers compound assignments to built-in integers. | The matching `Try` arithmetic API or a named overflow policy |
| SSN1003 | Discarded `NumericResult<T>` values, including a discarded `await` result, expression-bodied void methods or delegates, explicit assignment discards, tuple deconstruction discards, and `out` discards. The result type can also be nullable. | Inspect or propagate the result |
| SSN1004 | A compile-time value equal to `ArithmeticPolicy.Throw`, including a statically imported field, constant alias, enum cast, omitted nullable or nonnullable optional argument, or an invocation of generated `Add` or `Subtract` on a domain configured with `Throw`. | `Reject`, another explicitly allowed policy, or a `Try` operation |
| SSN1005 | Direct calls to generic math `CreateSaturating` or `CreateTruncating`, including static interface dispatch through a constrained type parameter. | `SafeInteger.TryConvert` or an explicitly approved policy helper |
| SSN1006 | Direct use of the generator's raw constructor or storage, generated input casts, and reads or casts out that can throw on invalid fields. Also catches named BCL reinterpretation calls involving a bounded or nullable bounded type, such as `Unsafe.As`, `Unsafe.BitCast` and `MemoryMarshal.Read`. | Generated `TryCreate`, `TryGetValue`, `TryConvertTo`, or domain codec |
| SSN1007 | Evident managed allocation sites such as reference or anonymous object creation, reference-constrained `new T()`, explicit arrays, nonempty array collection expressions and expanded array `params`, boxing, nonconstant interpolated strings materialized as strings or format objects, and evident capture by a lambda. The capture check follows statically bound local-function calls, and the rule also reports explicit or instance-bound method-group delegates and local-function method-group delegates. | Value types, caller-owned buffers, and direct calls when appropriate |
| SSN1008 | Explicit `default`, `default(TDomain)`, or implicit zero-initializing parameterless `new` of a bounded domain configured with `DefaultValuePolicy.Invalid`, including when the option is omitted. | Generated `TryCreate` |

The analyzer keeps checked narrowing under SSN1001 because a range failure
throws even when wraparound is disabled. Exact constant casts,
range-preserving widening, and lifted casts of known-null values pass.
SSN1002 follows the compiler's overflow flags
for the listed operators. A checked operator can throw and has to be assessed
on that basis. Division, shifts, bitwise operators and custom arithmetic
operator bodies are outside its operator detection. An explicit `unchecked`
override is reported even when the enclosed expression is constant. It
takes precedence over the implicit arithmetic diagnostic in the same scope.

Generated domain reads through `Value` or equivalent storage casts pass for
`ZeroValid` domains. Using a generated member name with `nameof` evaluates
no domain value. Nullable defaults that represent absence and ordinary
generated `Try` APIs also pass. An assignment to a variable called `_`
binds as an ordinary assignment.

## Static limits

The diagnostics inspect each compilation without executing user code.
SSN1001 uses representable type ranges and compiler constants. If a branch
proves that a variable is in range, the caller can preserve that proof in a
status-returning conversion. SSN1003 checks syntactic discards and permits
assignment and propagation without proving the result is inspected later.
SSN1004 recognizes bound constants and the generated policy metadata.
Runtime policy values, indirect calls through delegates, and arbitrary
exceptions from transitive callees require separate verification.
SSN1005 matches the bound generic math contract, including implementations,
and cannot infer a similarly named wrapper's runtime policy.

SSN1006 covers the listed direct bypasses. Domain validity across pointers,
aliases, native interop, reflection, arbitrary serialization, and caller
state requires separate verification. An invalidated domain can still pass
through ordinary assignment. SSN1008 targets explicit zero construction.
Implicit zero-initialization of arrays or fields and flow of invalid values
through other methods need separate review. The memory of a nullable
domain strongly typed as `null` does not represent a domain value.

SSN1007 is a source-level warning. It excludes constant interpolated strings,
handler content, stack allocation, object construction of a value type,
and empty implicit array `params`. Allocation behavior from user API
internals, handler code, async suspension, iterator lowering, delegate caches,
JIT decisions, runtime data, and Native AOT execution requires runtime
measurement in each qualified environment.

## Verification

The xUnit harness compiles consumer C# with Roslyn using real core metadata.
It checks compiler warnings and errors, generator diagnostics where applicable, and the
exact diagnostic ID, severity, and source span. A cross-assembly fixture
emits and references a generated domain library to verify metadata-based
enforcement. The tests cover both scope entry points and deliberate near
matches, plus all 169 ordered built-in integer casts, including `char`,
against independent range endpoints. CsCheck seed `PCG(1, 114938)` generates
64 source constants alongside eight directed extremes and endpoint cases.
The same fixture checks malformed binding for 18 missing names and a
missing expression.

Run the suite from the repository root.

```powershell
$testDirectory = Join-Path (Get-Location).Path 'tests/SharpSafeNumerics.AnalyzerUnitTests'
if (!(Test-Path -LiteralPath $testDirectory -PathType Container)) {
    throw 'Analyzer test directory missing.'
}
dotnet test (Join-Path $testDirectory 'SharpSafeNumerics.AnalyzerUnitTests.csproj') --verbosity minimal
```

On Windows x64, .NET SDK 11.0.100, Roslyn 5.12.0, and Coverlet collector 10.1.1
reported 97 passing tests, zero failures, and zero skips. The analyzer assembly
had 441 of 457 instrumented lines covered (96.49%) and 473 of 564 instrumented
branches covered (83.86%). The measured scope is `SharpSafeNumerics.Analyzers`.
Uncovered paths include anonymous-object construction, method-group delegate
construction, and the compilation guard for an absent StrictNumeric attribute.
The existing probe reproduced SSN1002, two SSN1003 warnings, and SSN1004.
The review regression run recorded 12 failures against the pre-review
analyzer and all 97 passing with the fixes.

With `$run` set to a checked results directory under `CODEX_ARTIFACT_ROOT`,
the coverage run used the same project and
`--collect 'XPlat Code Coverage' --results-directory $run -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include='[SharpSafeNumerics.Analyzers]*'`.
Its report is under the artifact root at
`SharpSafeNumerics/windows-x64/analyzer-rc1/560a9048-18ee-4d47-942d-234114c0fb13/coverage.cobertura.xml`.
The source baseline was `15a138c6e04e4830aa729282af1ce9026c58efc4` plus the RC1
working changes. The tested file SHA-256 values were
`28cf64b1491da1314078e5c179b9164d87c07fc0887723a0ae2260656d4f655f`
for `StrictNumericAnalyzer.cs`,
`53370eb7237660a47d90f7cec52e61289d137f141184b4fe07712c1e4bdd5fcc`
for `NumericSymbols.cs`, and
`e32f773af03a5210a7c49e13df655fe63c0eed9af5e2f5146958708b001b4a3c`
for `StrictNumericAnalyzerTests.cs`.

The Standard mutation run uses the manifest-pinned Stryker.NET 5.1.0.
Use an isolated source snapshot with the repository's build props, tool
manifest, core, analyzer, generator, and analyzer tests. Run
`dotnet tool restore` from the snapshot root, then run the mutation command
from the analyzer test project directory. On this Windows installation,
the process-local `MSBUILD_EXE_PATH` points to the verified .NET 11.0.100
`MSBuild.dll` and the explicit framework selects the .NET 11 test host.

```powershell
dotnet tool run dotnet-stryker --project SharpSafeNumerics.Analyzers.csproj --target-framework net11.0 `
    --skip-version-check --concurrency 4 --mutation-level Standard --reporter dots `
    --reporter cleartext --reporter json --break-on-initial-test-failure --output $run
```

StrictDoc 0.30.4 exported the analyzer requirements and the central
requirements together and built the traceability graph. The eight analyzer
obligations are traced to `SN-013`.
[Analyzer requirements](analyzer-requirements.sdoc)
