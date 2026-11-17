using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SharpSafeNumerics.Analyzers;

const string source = """
using SharpSafeNumerics;
class Demo
{
    [StrictNumeric]
    static void Strict()
    {
        int x = unchecked(int.MaxValue + 1);
        _ = SafeInteger.Add(1, 2, ArithmeticPolicy.Throw);
        SafeInteger.Add(1, 2);
    }
}
""";

var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
    .Split(Path.PathSeparator)
    .Select(path => MetadataReference.CreateFromFile(path))
    .Append(MetadataReference.CreateFromFile(typeof(SharpSafeNumerics.SafeInteger).Assembly.Location));

var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp15));
var compilation = CSharpCompilation.Create("AnalyzerProbe", [tree], refs,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

var diagnostics = await compilation
    .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new StrictNumericAnalyzer()))
    .GetAnalyzerDiagnosticsAsync();

var ids = diagnostics.Select(d => d.Id).OrderBy(x => x).ToArray();
string[] expected = ["SSN1002", "SSN1003", "SSN1003", "SSN1004"];
if (!ids.SequenceEqual(expected))
    throw new Exception("Analyzer diagnostics mismatch: " + string.Join(",", ids));
Console.WriteLine("Analyzer rules verified: " + string.Join(", ", ids));
