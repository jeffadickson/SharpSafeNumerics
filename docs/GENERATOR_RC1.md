# Generated bounded integers

Apply `BoundedInteger` to a top-level partial struct. Bounds are inclusive, use
invariant decimal text, and must fit the selected storage type. The generator
validates attributes with Roslyn and emits ordinary typed C#. Applications need
only the core assembly at runtime.

```csharp
[BoundedInteger(typeof(int), "0", "63",
    OverflowPolicy = ArithmeticPolicy.Saturate,
    DefaultPolicy = DefaultValuePolicy.ZeroValid,
    SerializationWidth = 2,
    Endianness = IntegerEndianness.BigEndian,
    Unit = "entries")]
public readonly partial struct QueueDepth;
```

## Annotation options

| Option | Values and behavior |
|---|---|
| `OverflowPolicy` | `Reject` by default. `Saturate`, `Wrap`, and `Throw` configure `Add` and `Subtract`. |
| `DefaultPolicy` | `Invalid` by default. `ZeroValid` admits the all-zero default when the bounds contain zero and omits the validity field. |
| `TextPolicy` | `InvariantDecimal` by default. `InvariantHexadecimal` accepts hex digits without a prefix, sign, whitespace, or separator. |
| `SerializationWidth` | Zero selects the storage width. An explicit 1, 2, 4, 8, or 16 byte width must fit the storage and represent the entire declared interval. |
| `Endianness` | `LittleEndian` by default. `BigEndian` selects the other byte order. |
| `Unit` | An optional nonblank string without control characters, emitted as constant metadata. |

Supported storage types are `sbyte`, `byte`, `short`, `ushort`, `int`,
`uint`, `long`, `ulong`, `Int128`, `UInt128`, `nint`, and `nuint`.
An AnyCPU native integer declaration uses the portable 32-bit range and
a four-byte default binary width. Explicit 64-bit compilation targets
can declare the full 64-bit native range. The native integer codec and
hex conventions follow the compilation width. Primitive native arithmetic
follows the executing architecture. Use a fixed-width integer when wrap
arithmetic must be identical across 32-bit and 64-bit processes.
The generator qualifies native storage as System.IntPtr or System.UIntPtr to
preserve its identity in source that defines native-integer aliases.

## Generated members

- `TryCreate` admits a storage value. `TryCreateFrom<TFrom>` first checks
  exact representability in the storage type, then applies the domain bounds.
  `TryGetValue` retrieves a valid value, and `TryConvertTo<TTo>` checks
  exact representability in the destination built-in integer type. The
  generic signatures use .NET generic math. Qualification covers built-in
  integer conversions. Accessing `Value` on an invalid instance raises
  `InvalidOperationException`. Explicit casts between the domain and its
  storage enforce the same admission and validity rules.
- `TryAdd` and `TrySubtract` always reject primitive overflow, primitive
  underflow, or results outside the declared range. `Add` and `Subtract`
  return `NumericResult<Domain>`. `Reject` returns the same failures as the
  Try methods. `Saturate` clamps to the nearest domain endpoint. `Wrap` uses
  primitive modulo arithmetic and then rejects any result outside the declared
  bounds. `Throw` raises `OverflowException` for primitive or domain overflow.
  An invalid receiver produces `Uninitialized` for all four policies.
- `TryParse(ReadOnlySpan<char>, out Domain)` uses the configured convention
  and verifies the declared bounds. Decimal allows the invariant integer
  leading sign and leading or trailing whitespace. Hex output is uppercase,
  with signed negative values represented in two's complement at the storage
  width. Text containing a NUL is malformed. `TryFormat(Span<char>, out int)`
  writes invariant text and reports zero characters on failure. `ToString()`
  uses the same convention and returns `<invalid>` for an invalid instance.
  Unit metadata leaves the numeric text unchanged.
- `TryRead(ReadOnlySpan<byte>, out Domain)` requires exactly
  `SerializationWidth` bytes and rejects an out-of-domain decoded integer.
  `TryWrite(Span<byte>, out int)` writes that many bytes at the start of
  a sufficiently large destination. Failure leaves the destination
  untouched and reports zero bytes written. Signed encodings use two's
  complement. `SerializationEndianness` gives the configured byte order.
- Typed equality, hashing, and comparisons use validity and the storage value.
  Invalid values compare equal to each other and sort before valid values.
  `IEquatable<T>`, `IComparable<T>`, and all six relational and equality
  operators share these semantics. XML documentation is emitted with the API,
  and the debugger displays the invariant text representation.

Every failed domain-producing Try method assigns `default(Domain)`. With
`ZeroValid`, that value is valid zero, so callers must use the returned status
or Boolean to determine success. A failed raw conversion clears the output to
zero.

## Declaration diagnostics

| Diagnostic | Condition |
|---|---|
| SSNG001 | Missing `partial` |
| SSNG002 | Unsupported storage type |
| SSNG003 | Malformed, reversed, or nonrepresentable bounds |
| SSNG004 | Nested, generic, file-local, ref, or record struct, primary constructor, user instance constructor, additional instance storage including field-like events, explicit layout, or inline-array layout |
| SSNG005 | Generated member or synthesized property accessor name collision in any partial declaration |
| SSNG006 | Unsupported annotation option value or duplicate bounded annotation |
| SSNG007 | `ZeroValid` with an interval that excludes zero |
| SSNG008 | Unsupported serialization width or a width that cannot represent the entire interval |

Generated names are reserved across all policies. Additional computed members and
static members may use other names. User code inside the partial declaration can
access its private members by C# rules. Keep construction in the generated API
so admission is enforced. Roslyn source hint names incorporate the full type
identity, including its namespace and casing.

## Verification

On Windows x64 with .NET SDK 11.0.100 and runtime 11.0.0, the generator tests passed
79 of 79. Roslyn compilation enabled checked arithmetic and checked generated
XML documentation. The Windows tests compiled in-memory x86, Arm, x64 and Arm64 fixtures.
Runtime execution used Windows x64. The normal SDK build ran the generated queue
example and printed `Generated QueueDepth verified: 63 entries`.

- Independent `BigInteger` byte and decimal oracles covered all 12 storage types,
  1,004 values per type. The 12,048 inputs included bit-pattern extrema and
  deterministic random bytes with seed `81334108 + width in bytes`. The
  `UInt128` fixture used big-endian; other full-range fixtures used little-endian.
  Additional fixtures verified both byte orders and narrower widths.
- Additional finite checks covered all 256 signed-byte hexadecimal encodings
  and 1,000 malformed decimal suffixes using seed 418721. The tests retain
  input bytes, seed and case index for replay of randomized binary cases and
  suffix code point, seed and case index for replay of malformed cases.
- The contract harness exercised all 28 SDK-compiled domain fixtures. It used
  explicit bounds, exact BigInteger arithmetic, mathematical modulo for Wrap,
  and an independent fixed-width byte oracle. Inputs include invalid and
  zero-valid defaults, endpoint and near-endpoint values, primitive overflow
  and underflow, failed conversion, malformed input, short buffers, and ordinary
  and zero-valid singleton intervals.
- Dynamic Roslyn fixtures executed generated bodies for the default, invariant
  text, endian and Throw policy options after emitting assemblies in memory.
  Regression cases verified native storage identity under global aliases,
  reserved accessor collisions, explicit and inline-array layouts, field-like
  instance events, and that construction preserves user static underscore fields.
  Seven cases reproduced the review findings before the fixes and passed afterward.
- Coverlet collector 10.1.1 reported 499/513 lines (97.27%) and 254/269 branches
  (94.42%) in the generator implementation. Instrumentation of the 28 generated
  domain fixture types reported 1,889/1,905 lines (99.16%) and 1,375/1,450 branches
  (94.83%). Uncovered paths include range rejection in full-storage domains,
  primitive-width entry conversion rejection in several narrow domains, the
  primitive failure guard when Wrap is active, and invalid-receiver paths in
  ZeroValid fixtures. Coverage measures generated SDK fixture types. In-memory
  probes contribute separate compilation and execution assertions.

From the checkout root, run the focused tests and example with the commands below.
Set `CODEX_ARTIFACT_ROOT` to an existing writable directory to retain coverage and TRX.

```powershell
$generatorProject = 'tests/SharpSafeNumerics.GeneratorTests'
$queueExample = 'examples/GeneratedQueueDepth'
foreach ($path in $generatorProject, $queueExample, $env:CODEX_ARTIFACT_ROOT) {
    if (-not $path -or -not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "Missing directory: $path"
    }
}
$run = Join-Path $env:CODEX_ARTIFACT_ROOT ('SharpSafeNumerics-generator-' + (Get-Date -AsUTC -Format 'yyyyMMddTHHmmssfffZ'))
if (-not (Test-Path -LiteralPath $run -PathType Container)) {
    New-Item -ItemType Directory -Path $run | Out-Null
}
$generated = Join-Path $run 'sources'
if (-not (Test-Path -LiteralPath $generated -PathType Container)) {
    New-Item -ItemType Directory -Path $generated | Out-Null
}
dotnet test $generatorProject -c Release --collect 'XPlat Code Coverage' --results-directory $run --logger 'trx;LogFileName=generator.trx' -p:EmitCompilerGeneratedFiles=true "-p:CompilerGeneratedFilesOutputPath=$generated" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.IncludeTestAssembly=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project $queueExample -c Release
exit $LASTEXITCODE
```

Measured generator sources by Git blob ID:

| File | Git blob |
|---|---|
| `NumericAnnotations.cs` | `fc4de762e09af7e1805896679128b7af8c864bff` |
| `BoundedIntegerGenerator.cs` | `4e222d9f35a4b2eafb6e459badb034bbf2ab4464` |
| `BoundedIntegerGenerator.Rendering.cs` | `44e23b3c662d8e4142a0a0692b928336b080d6c5` |
| `GeneratorCompilationTests.cs` | `1d4fa81007ae1c511d5be3f9f77c2834e755e7ff` |
| `GeneratedContractTests.cs` | `bbf21086c6537b6356e67426596b9241b290d5ab` |
| `GeneratedDomainTests.cs` | `8f291759fa339674f2fc1f7a661e75650dacc85b` |


## Generator mutation experiment

Stryker.NET 5.1.0 Basic ran on an isolated snapshot of the generator test project's
three-project build graph with the 64-case pre-expansion test suite. It created
145 mutants. It killed 77, found 15 survivors and three
uncovered mutations, rejected 43 at compilation, and filtered seven as ignored.
The score was 81.05% (77 of 95 scored mutants), with zero timeouts. The run took
4 minutes 21 seconds with two parallel test sessions on Windows x64.
The current 79-test suite extends runtime coverage of SDK-generated output and
executes selected fixtures emitted by the generator assembly under test.
Stryker replaced the generator assembly after building the SDK fixture assembly,
so the mutation experiment principally exercised the in-process Roslyn checks.
A changed emitted behavior could survive if the in-process assertion checked
only successful compilation.

| Mutant IDs | Disposition |
|---|---|
| 25 | Explicit-interface-name collision matching was uncovered. The checks exercised ordinary member collisions, including collisions across partial declarations. |
| 35, 42, 43, 44 | Diagnostic tests assert rule ID, severity and source location. These mutations alter the displayed storage or bounds arguments without changing the rule. Message fidelity remains a coverage gap. |
| 40 | Changes acceptance of singleton intervals. The dynamic fixtures used ranges with unequal endpoints. This mutation is meaningful. |
| 45, 49, 50, 51 | Zero-default validation needs positive and negative dynamic cases across both interval endpoints. SDK-compiled zero-valid tests exercise runtime zero and domain admission. IDs 50 and 51 were uncovered due to short-circuit evaluation. |
| 69 | Changes contextual-keyword escaping. Existing dynamic fixtures verify escaped reserved keywords and namespace isolation. Equivalence for all contextual identifier positions remains undetermined. |
| 76 | Changes the guard for a NUL at offset zero. The dynamic malformed fixture exercises a trailing NUL. The built-in decimal parser's behavior for all leading-NUL inputs was outside this experiment. |
| 110, 118, 122 | Reject known valid Throw, hexadecimal or big-endian options. SDK fixtures exercise their runtime behavior. Dynamic checks should also assert that each option is emitted. |
| 131, 133, 136 | Change the emitted default, text policy or native hexadecimal width. These are runtime contract checks in the SDK fixtures. The dynamic mutation checks did not execute the emitted outputs. |

The ordinary snapshot test project built successfully. Buildalyzer's initial
Visual Studio MSBuild probe could not resolve the SDK. Selecting the SDK MSBuild
path and explicitly selecting the test framework enabled Stryker to proceed.
Run the command below from the snapshot's generator project directory. The
snapshot should include the root build and package configuration files, local
tool manifest, and the core, generator and generator test projects.

```powershell
$testProject = '../../tests/SharpSafeNumerics.GeneratorTests'
if (-not (Test-Path -LiteralPath $testProject -PathType Container)) {
    throw 'Missing generator test project in the mutation snapshot.'
}
$artifactRoot = $env:CODEX_ARTIFACT_ROOT
if (-not $artifactRoot -or -not (Test-Path -LiteralPath $artifactRoot -PathType Container)) {
    throw 'Missing generator artifact root.'
}
$mutation = Join-Path $artifactRoot ('SharpSafeNumerics-generator-mutation-' + (Get-Date -AsUTC -Format 'yyyyMMddTHHmmssfffZ'))
if (-not (Test-Path -LiteralPath $mutation -PathType Container)) {
    New-Item -ItemType Directory -Path $mutation | Out-Null
}
$msbuild = Join-Path $env:ProgramFiles 'dotnet/sdk/11.0.100/MSBuild.dll'
if (-not (Test-Path -LiteralPath $msbuild -PathType Leaf)) {
    throw 'Missing SDK MSBuild.'
}
$env:MSBUILD_EXE_PATH = $msbuild
dotnet stryker --project SharpSafeNumerics.Generator.csproj --test-project "$testProject/SharpSafeNumerics.GeneratorTests.csproj" --target-framework net11.0 --configuration Release --mutation-level Standard --concurrency 4 --reporter Progress --reporter Json --output $mutation --skip-version-check --diag
```

The Windows run data is under `SharpSafeNumerics-generator-20261117T180916050Z`
in the bulk artifact root. It includes the coverage and TRX attachments, the
mutation snapshot, `mutation/sdk-msbuild.log`, and
`mutation/reports/mutation-report.json`.

