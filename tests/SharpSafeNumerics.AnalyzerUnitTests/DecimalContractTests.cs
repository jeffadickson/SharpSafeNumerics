using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SharpSafeNumerics.Analyzers;
using Xunit;

namespace SharpSafeNumerics.AnalyzerUnitTests;

/// <summary>Checks optional decimal contracts against actual separately compiled metadata.</summary>
public sealed class DecimalContractTests
{
    [Theory]
    [InlineData(true, false, "SSN1003")]
    [InlineData(false, true, "SSN1003")]
    [InlineData(false, false, null)]
    public async Task Result_registration_handles_isolated_optional_references(bool core, bool decimals, string? expected)
    {
        // TPA includes project dependencies in a test host. Remove every library assembly
        // before explicitly choosing references, so optional-assembly absence is real.
        var references = FrameworkReferences().ToList();
        if (core) references.Add(MetadataReference.CreateFromFile(typeof(SafeInteger).Assembly.Location));
        if (decimals) references.Add(MetadataReference.CreateFromFile(typeof(SafeDecimal).Assembly.Location));
        string result = core ? "SharpSafeNumerics.NumericResult<int>" : decimals
            ? "SharpSafeNumerics.DecimalResult<int>" : "int";
        var compilation = CreateCompilation($$"""
            class Consumer
            {
                static {{result}} Result() => default;
                static void Run() { Result(); }
            }
            """, "IsolatedConsumer", references);
        var diagnostics = await AnalyzeCompilation(compilation);
        if (expected is null) Assert.Empty(diagnostics);
        else Assert.Equal(expected, Assert.Single(diagnostics).Id);
    }

    [Theory]
    [InlineData("SharpSafeNumerics.Core", "NumericResult")]
    [InlineData("SharpSafeNumerics.Decimal", "DecimalResult")]
    public async Task Library_source_compilations_resolve_their_own_result_contract(string assemblyName, string result)
    {
        // An analyzer also runs while building its contract assembly. These minimal source
        // contracts isolate that supported resolution path from external library metadata.
        var compilation = CreateCompilation($$"""
            namespace SharpSafeNumerics
            {
                public readonly struct {{result}}<T> where T : struct { }
                class Consumer
                {
                    static {{result}}<int> Result() => default;
                    static void Run() { Result(); }
                }
            }
            """, assemblyName, FrameworkReferences());
        Assert.Equal("SSN1003", Assert.Single(await AnalyzeCompilation(compilation)).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Decimal_results_are_checked_with_global_and_alias_only_metadata(bool aliasOnly)
    {
        var diagnostics = await Analyze("""
            class Consumer
            {
                static DecimalResult<Decimal32> Result() => default;
                static void Run() { Result(); _ = Result(); }
            }
            """, aliasOnly);
        Assert.Equal(new[] { "SSN1003", "SSN1003" }, diagnostics.Select(d => d.Id));
    }

    [Theory]
    [InlineData("TryAdd", "value, value")]
    [InlineData("TrySubtract", "value, value")]
    [InlineData("TryMultiply", "value, value")]
    [InlineData("TryDivide", "value, value")]
    [InlineData("TryConvert<Decimal32, Decimal64>", "value")]
    public async Task Allocating_decimal_helpers_warn_only_inside_strict_scope(string method, string arguments)
    {
        string call = $"SafeDecimal.{method}({arguments})";
        var diagnostics = await Analyze($$"""
            class Consumer
            {
                [StrictNumeric] static void Strict(Decimal32 value) { var result = {{call}}; }
                static void Loose(Decimal32 value) { var result = {{call}}; }
            }
            """);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SSN1007", diagnostic.Id);
        Assert.Equal(call, diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        Assert.Contains("decimal exactness", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task Negation_and_observed_results_do_not_warn()
    {
        Assert.Empty(await Analyze("""
            [StrictNumeric] class Consumer
            {
                static DecimalResult<Decimal32> Run(Decimal32 value) => SafeDecimal.TryNegate(value);
            }
            """));
    }

    [Fact]
    public async Task Nullable_and_out_decimal_results_cannot_be_discarded()
    {
        var diagnostics = await Analyze("""
            class Consumer
            {
                static DecimalResult<Decimal32>? Result() => default;
                static void Write(out DecimalResult<Decimal32> value) { value = default; }
                static void Run() { _ = Result(); Write(out _); }
            }
            """);
        Assert.Equal(new[] { "SSN1003", "SSN1003" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Alias_only_decimal_helpers_preserve_both_contracts()
    {
        var diagnostics = await Analyze("""
            [StrictNumeric] class Consumer
            {
                static void Run(Decimal32 value) { Optional.SafeDecimal.TryAdd(value, value); }
            }
            """, aliasOnly: true);
        Assert.Equal(new[] { "SSN1003", "SSN1007" }, diagnostics.Select(d => d.Id).Order());
    }

    [Fact]
    public async Task Lookalike_contracts_from_an_unrelated_assembly_do_not_match()
    {
        var fake = CreateCompilation("""
            namespace SharpSafeNumerics
            {
                public struct DecimalResult<T>;
                public static class SafeDecimal
                {
                    public static DecimalResult<T> TryAdd<T>(T a, T b) => default;
                }
            }
            """, "Unrelated.Decimal", References());
        using var stream = new MemoryStream();
        Assert.True(fake.Emit(stream).Success);
        var reference = MetadataReference.CreateFromImage(stream.ToArray()).WithAliases(["fake"]);
        Assert.Empty(await Analyze("""
            [StrictNumeric] class Consumer
            {
                static void Run(int value) { fake::SharpSafeNumerics.SafeDecimal.TryAdd(value, value); }
            }
            """, additional: reference));
    }

    private static MetadataReference[] References() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(SafeInteger).Assembly.Location)
        .Where(path => !string.Equals(path, typeof(SafeDecimal).Assembly.Location, StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase).Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    private static IEnumerable<MetadataReference> FrameworkReferences() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Where(path => !Path.GetFileName(path).StartsWith("SharpSafeNumerics.", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase).Select(path => MetadataReference.CreateFromFile(path));

    private static CSharpCompilation CreateCompilation(string code, string name, IEnumerable<MetadataReference> references) =>
        CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp15))],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static async Task<Diagnostic[]> Analyze(string source, bool aliasOnly = false, MetadataReference? additional = null)
    {
        var decimalReference = MetadataReference.CreateFromFile(typeof(SafeDecimal).Assembly.Location);
        string preamble = (additional is null ? "" : "extern alias fake; ") +
            "using System; using System.Numerics; using SharpSafeNumerics;\n";
        if (aliasOnly)
        {
            decimalReference = decimalReference.WithAliases(["optional"]);
            preamble = "extern alias optional; using System; using System.Numerics; using SharpSafeNumerics; " +
                "using Optional = optional::SharpSafeNumerics;\n";
        }
        if (aliasOnly) source = source.Replace("DecimalResult<", "Optional.DecimalResult<", StringComparison.Ordinal);
        var references = References().Append(decimalReference);
        if (additional is not null) references = references.Append(additional);
        var compilation = CreateCompilation(preamble + source, "DecimalConsumer", references);
        return await AnalyzeCompilation(compilation);
    }

    private static async Task<Diagnostic[]> AnalyzeCompilation(CSharpCompilation compilation)
    {
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new StrictNumericAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        return diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ToArray();
    }
}


