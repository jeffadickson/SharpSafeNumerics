using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharpSafeNumerics.Generator;
using Xunit;

namespace SharpSafeNumerics.GeneratorTests;

public sealed class GeneratorCompilationTests
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp15,
        DocumentationMode.Diagnose);
    private static readonly PortableExecutableReference[] References = LoadReferences();

    [Fact]
    public void Same_type_name_in_two_namespaces_generates_distinct_sources()
    {
        const string source = """
            using SharpSafeNumerics;
            namespace @class.A { [BoundedInteger(typeof(int), "0", "1")] internal partial struct @event; }
            namespace A.@class { [BoundedInteger(typeof(int), "0", "2")] internal partial struct @event; }
            namespace a.@class { [BoundedInteger(typeof(int), "0", "2")] internal partial struct @event; }
            """;
        var (output, result) = Generate(source);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(3, result.GeneratedTrees.Length);
        AssertCompiles(output);
    }

    [Fact]
    public void Generated_names_bind_to_the_annotated_type_inside_a_generic_member()
    {
        const string source = """
            using SharpSafeNumerics;
            [BoundedInteger(typeof(int), "-4", "7", Unit = null)]
            internal partial struct TFrom;
            internal partial struct TFrom : System.IDisposable
            {
                public int Read() => Value;
                void System.IDisposable.Dispose() { }
            }
            """;
        var (output, result) = Generate(source);
        Assert.Empty(result.Diagnostics);
        AssertCompiles(output);
        var domain = Assert.IsAssignableFrom<INamedTypeSymbol>(output.GetTypeByMetadataName("TFrom"));
        Assert.Contains(domain.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Diagnostics.DebuggerDisplayAttribute");
    }

    [Theory]
    [InlineData("SSNG001", "[BoundedInteger(typeof(byte), \"0\", \"1\")] struct D;")]
    [InlineData("SSNG002", "[BoundedInteger(typeof(float), \"0\", \"1\")] partial struct D;")]
    [InlineData("SSNG003", "[BoundedInteger(typeof(byte), \"-1\", \"1\")] partial struct D;")]
    [InlineData("SSNG003", "[BoundedInteger(typeof(short), \"+1\", \"-1\")] partial struct D;")]
    [InlineData("SSNG003", "[BoundedInteger(typeof(int), \"1\\0\", \"4\")] partial struct D;")]
    [InlineData("SSNG003", "[BoundedInteger(typeof(int), null!, \"4\")] partial struct D;")]
    [InlineData("SSNG004", "[BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D<T>;")]
    [InlineData("SSNG004", "partial class Outer { [BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D; }")]
    [InlineData("SSNG004", "[BoundedInteger(typeof(byte), \"0\", \"1\")] partial record struct D;")]
    [InlineData("SSNG004", "[BoundedInteger(typeof(byte), \"0\", \"1\")] file partial struct D;")]
    [InlineData("SSNG004", "[BoundedInteger(typeof(byte), \"0\", \"1\")] ref partial struct D;")]
    [InlineData("SSNG004", "[BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D(int value);")]
    [InlineData("SSNG004", "[BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D; partial struct D(int value);")]
    [InlineData("SSNG004", "[BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D { public int Extra; }")]
    [InlineData("SSNG004", "[BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D { public event System.Action? Extra; }")]
    [InlineData("SSNG004", "[BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D; partial struct D { public D(int input) { } }")]
    [InlineData("SSNG004", "[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)] [BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D;")]
    [InlineData("SSNG004", "[System.Runtime.InteropServices.StructLayout((short)2)] [BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D;")]
    [InlineData("SSNG004", "[System.Runtime.CompilerServices.InlineArray(2)] [BoundedInteger(typeof(byte), \"0\", \"1\", DefaultPolicy = DefaultValuePolicy.ZeroValid)] partial struct D;")]
    [InlineData("SSNG005", "[BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D; partial struct D { public int Minimum => 0; }")]
    [InlineData("SSNG005", "interface IRange { byte Minimum(); } [BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D : IRange { byte IRange.Minimum() => 0; }")]
    [InlineData("SSNG005", "[BoundedInteger(typeof(byte), \"0\", \"1\")] partial struct D { public byte get_Value() => 0; }")]
    [InlineData("SSNG006", "[BoundedInteger(typeof(byte), \"0\", \"1\", OverflowPolicy = (ArithmeticPolicy)255)] partial struct D;")]
    [InlineData("SSNG006", "[BoundedInteger(typeof(byte), \"0\", \"1\", TextPolicy = (IntegerTextPolicy)255)] partial struct D;")]
    [InlineData("SSNG006", "[BoundedInteger(typeof(byte), \"0\", \"1\", DefaultPolicy = (DefaultValuePolicy)255)] partial struct D;")]
    [InlineData("SSNG006", "[BoundedInteger(typeof(byte), \"0\", \"1\", Endianness = (IntegerEndianness)255)] partial struct D;")]
    [InlineData("SSNG006", "[BoundedInteger(typeof(byte), \"0\", \"1\", Unit = \" \")] partial struct D;")]
    [InlineData("SSNG006", "[BoundedInteger(typeof(byte), \"0\", \"1\", Unit = \"a\\0b\")] partial struct D;")]
    [InlineData("SSNG007", "[BoundedInteger(typeof(byte), \"1\", \"2\", DefaultPolicy = DefaultValuePolicy.ZeroValid)] partial struct D;")]
    [InlineData("SSNG007", "[BoundedInteger(typeof(sbyte), \"-2\", \"-1\", DefaultPolicy = DefaultValuePolicy.ZeroValid)] partial struct D;")]
    [InlineData("SSNG008", "[BoundedInteger(typeof(int), \"-129\", \"127\", SerializationWidth = 1)] partial struct D;")]
    [InlineData("SSNG008", "[BoundedInteger(typeof(uint), \"0\", \"256\", SerializationWidth = 1)] partial struct D;")]
    [InlineData("SSNG008", "[BoundedInteger(typeof(byte), \"0\", \"1\", SerializationWidth = 2)] partial struct D;")]
    [InlineData("SSNG008", "[BoundedInteger(typeof(int), \"0\", \"1\", SerializationWidth = 3)] partial struct D;")]
    [InlineData("SSNG008", "[BoundedInteger(typeof(int), \"0\", \"1\", SerializationWidth = -1)] partial struct D;")]
    public void Invalid_contract_emits_a_diagnostic_and_no_generated_code(string expectedId, string declaration)
    {
        var (_, result) = Generate("using SharpSafeNumerics;\n" + declaration);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.Descriptor.Title.ToString()));
        Assert.False(string.IsNullOrWhiteSpace(diagnostic.GetMessage()));
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Every_reserved_generated_name_is_diagnosed_before_emission()
    {
        // Exercise the declared contract with handwritten members in a single Roslyn compilation.
        string[] reserved =
        [
            "__ssnValue", "__ssnValid", "__ssnAdmitArithmetic",
            "Minimum", "Maximum", "IsValid", "Value", "TryGetValue", "TryCreate", "TryCreateFrom", "TryConvertTo",
            "get_Minimum", "get_Maximum", "get_IsValid", "get_Value",
            "TryAdd", "TrySubtract", "Add", "Subtract", "TryParse", "TryFormat", "TryRead", "TryWrite",
            "OverflowPolicy", "DefaultPolicy", "TextPolicy", "SerializationWidth", "SerializationEndianness", "Unit",
            "ToString", "Equals", "GetHashCode", "CompareTo", "op_Explicit", "op_CheckedExplicit", "op_Implicit",
            "op_Equality", "op_Inequality", "op_LessThan", "op_GreaterThan", "op_LessThanOrEqual", "op_GreaterThanOrEqual"
        ];
        var source = new System.Text.StringBuilder("using SharpSafeNumerics;\n");
        for (int index = 0; index < reserved.Length; index++)
        {
            source.AppendLine($$"""
                [BoundedInteger(typeof(int), "-1", "1")]
                internal partial struct Collision{{index}} { public static int {{reserved[index]}} => 0; }
                """);
        }

        var (_, result) = Generate(source.ToString());
        Assert.Empty(result.GeneratedTrees);
        Assert.Equal(reserved.Length, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, diagnostic => Assert.Equal("SSNG005", diagnostic.Id));
    }

    [Fact]
    public void Range_diagnostic_reports_the_actual_type_width_and_both_bounds()
    {
        const string source = """
            using SharpSafeNumerics;
            [BoundedInteger(typeof(byte), "-1", "256")]
            internal partial struct InvalidByte;
            """;
        var (_, result) = Generate(source);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("SSNG003", diagnostic.Id);
        var message = diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains("-1", message);
        Assert.Contains("256", message);
        Assert.Contains("8-bit storage type 'Byte'", message);
    }

    [Fact]
    public void Native_storage_cannot_bind_to_a_global_alias()
    {
        const string source = """
            global using @nint = System.Int64;
            global using @nuint = System.UInt64;
            using SharpSafeNumerics;

            [BoundedInteger(typeof(System.IntPtr), "0", "10")]
            internal partial struct Signed;
            [BoundedInteger(typeof(System.UIntPtr), "0", "10")]
            internal partial struct Unsigned;
            """;
        var (output, result) = Generate(source);
        Assert.Empty(result.Diagnostics);
        AssertCompiles(output);
        AssertStorage("Signed", SpecialType.System_IntPtr);
        AssertStorage("Unsigned", SpecialType.System_UIntPtr);

        void AssertStorage(string name, SpecialType expected)
        {
            var type = Assert.IsAssignableFrom<INamedTypeSymbol>(output.GetTypeByMetadataName(name));
            var value = Assert.IsAssignableFrom<IPropertySymbol>(Assert.Single(type.GetMembers("Value")));
            Assert.Equal(expected, value.Type.SpecialType);
        }
    }

    [Fact]
    public void Native_storage_does_not_bind_to_an_escaped_domain_name()
    {
        const string source = """
            using SharpSafeNumerics;
            [BoundedInteger(typeof(System.IntPtr), "0", "10")]
            internal partial struct @nint;
            """;
        var (output, result) = Generate(source);
        Assert.Empty(result.Diagnostics);
        AssertCompiles(output);
    }

    [Fact]
    public void Construction_cannot_read_or_write_members_named_like_discards()
    {
        const string source = """
            using SharpSafeNumerics;

            [BoundedInteger(typeof(int), "0", "10")]
            internal partial struct ReadOnlyUnderscore
            {
                public static readonly int _ = 50;
            }

            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            [BoundedInteger(typeof(int), "0", "10")]
            internal partial struct WritableUnderscore
            {
                public static int _ = 70;
                public static event System.Action? Notification;
                public event System.Action Custom { add { } remove { } }
                public static void Notify() => Notification?.Invoke();
            }

            public static class Probe
            {
                public static string Run()
                {
                    if (ReadOnlyUnderscore.TryCreate(1, out _) != ArithmeticError.None ||
                        ReadOnlyUnderscore._ != 50)
                        return "readonly underscore field";
                    if (WritableUnderscore.TryCreate(2, out _) != ArithmeticError.None ||
                        WritableUnderscore._ != 70)
                        return "writable underscore field";
                    return "";
                }
            }
            """;
        AssertProbeSucceeds(source);
    }

    [Fact]
    public void Runtime_compiled_defaults_support_singletons_and_preserve_ordinary_invalid_states()
    {
        const string source = """
            using SharpSafeNumerics;

            [BoundedInteger(typeof(byte), "0", "0", DefaultPolicy = DefaultValuePolicy.ZeroValid)]
            internal partial struct Singleton;

            [BoundedInteger(typeof(short), "0", "5", DefaultPolicy = DefaultValuePolicy.ZeroValid)]
            internal partial struct Count;

            internal sealed class MarkerAttribute(int value) : System.Attribute
            {
                public int Value { get; } = value;
            }

            [Marker(2)]
            [BoundedInteger(typeof(int), "2", "10")]
            public partial struct Positive
            {
                public void Invalidate() => this = default;
            }

            public static class Probe
            {
                public static string Run()
                {
                    if (Singleton.Minimum != 0 || Singleton.Maximum != 0 ||
                        !default(Singleton).TryGetValue(out var zero) || zero != 0 ||
                        Singleton.TryCreate(1, out _) != ArithmeticError.OutOfRange)
                        return "zero-valid singleton";

                    if (!default(Count).IsValid || default(Count).Value != 0 ||
                        default(Count).TryAdd(1, out var one) != ArithmeticError.None || one.Value != 1)
                        return "zero-valid lower endpoint";
                    if (one.TryAdd(1, out one) != ArithmeticError.None || one.Value != 2 ||
                        one.TrySubtract(1, out one) != ArithmeticError.None || one.Value != 1 ||
                        one.TrySubtract(2, out one) != ArithmeticError.OutOfRange || one.Value != 0)
                        return "zero-valid in-place operation";

                    if (default(Positive).IsValid || default(Positive).TryGetValue(out _) ||
                        Positive.TryCreate(2, out var two) != ArithmeticError.None || !two.IsValid)
                        return "invalid positive-range default";
                    if (two.TryAdd(1, out two) != ArithmeticError.None || two.Value != 3 ||
                        two.TrySubtract(1, out two) != ArithmeticError.None || two.Value != 2)
                        return "in-place operation on valid receiver";
                    if (two.TrySubtract(1, out two) != ArithmeticError.OutOfRange || two.IsValid)
                        return "in-place failure must clear the receiver";

                    two = (Positive)2;
                    two.Invalidate();
                    if (two.IsValid)
                        return "mutable partial receiver";
                    try
                    {
                        _ = two.Value;
                        return "invalid Value did not throw";
                    }
                    catch (System.InvalidOperationException) { }

                    return "";
                }
            }
            """;
        AssertProbeSucceeds(source);
    }

    [Fact]
    public void Runtime_compiled_options_drive_native_hex_big_endian_and_throw()
    {
        const string source = """
            using System;
            using SharpSafeNumerics;

            [BoundedInteger(typeof(nint), "-2147483648", "2147483647", TextPolicy = IntegerTextPolicy.InvariantHexadecimal)]
            internal partial struct NativeHex;

            [BoundedInteger(typeof(ushort), "0", "1024", OverflowPolicy = ArithmeticPolicy.Throw,
                Endianness = IntegerEndianness.BigEndian, SerializationWidth = 2, Unit = "sensor \"counts\"")]
            internal partial struct Measurement;

            public static class Probe
            {
                public static string Run()
                {
                    Span<char> chars = stackalloc char[8];
                    if (!NativeHex.TryParse("FFFFFFFF", out var nativeValue) || nativeValue.Value != -1 ||
                        !nativeValue.TryFormat(chars, out var charCount) || charCount != 8 ||
                        !chars.SequenceEqual("FFFFFFFF") || nativeValue.ToString() != "FFFFFFFF" ||
                        NativeHex.TryParse("0xFF", out _))
                        return "portable native hex";

                    Span<byte> bytes = stackalloc byte[3];
                    bytes[2] = 0x5A;
                    if (Measurement.Unit != "sensor \"counts\"" || !Measurement.TryParse("513", out var reading) ||
                        reading.ToString() != "513" ||
                        !reading.TryWrite(bytes, out var written) || written != 2 || bytes[0] != 2 ||
                        bytes[1] != 1 || bytes[2] != 0x5A ||
                        !Measurement.TryRead(bytes[..written], out var decoded) || decoded != reading ||
                        Measurement.TryRead(bytes, out _) || Measurement.TryRead(new byte[] { 4, 1 }, out _))
                        return "configured binary codec";

                    if (reading.Add(1).Value.Value != 514 ||
                        reading.TryAdd(512, out _) != ArithmeticError.OutOfRange)
                        return "try and policy arithmetic";

                    try { _ = reading.Add(512); }
                    catch (OverflowException) { return ""; }
                    return "domain overflow did not throw";
                }
            }
            """;
        AssertProbeSucceeds(source);
    }

    [Fact]
    public void Runtime_compiled_single_byte_codecs_and_saturation_preserve_signedness()
    {
        const string source = """
            using System;
            using SharpSafeNumerics;

            [BoundedInteger(typeof(sbyte), "-100", "100", OverflowPolicy = ArithmeticPolicy.Saturate)]
            public readonly partial struct SignedWire;

            [BoundedInteger(typeof(byte), "0", "255")]
            public readonly partial struct UnsignedWire;

            public static class Probe
            {
                public static string Run()
                {
                    Span<byte> bytes = stackalloc byte[1];
                    if (!SignedWire.TryRead(new byte[] { 0x9C }, out var signed) ||
                        signed.Value != -100 || !signed.TryWrite(bytes, out var signedCount) ||
                        signedCount != 1 || bytes[0] != 0x9C)
                        return "signed byte codec";
                    if (!UnsignedWire.TryRead(new byte[] { 0xFF }, out var unsigned) ||
                        unsigned.Value != 255 || !unsigned.TryWrite(bytes, out var unsignedCount) ||
                        unsignedCount != 1 || bytes[0] != 0xFF)
                        return "unsigned byte codec";
                    if (signed.Subtract(80).Value.Value != -100 ||
                        ((SignedWire)100).Add(100).Value.Value != 100 ||
                        signed.Add(1).Value.Value != -99)
                        return "saturating arithmetic";
                    return "";
                }
            }
            """;
        AssertProbeSucceeds(source);
    }

    [Theory]
    [InlineData(Platform.AnyCpu, false)]
    [InlineData(Platform.AnyCpu32BitPreferred, false)]
    [InlineData(Platform.X86, false)]
    [InlineData(Platform.Arm, false)]
    [InlineData(Platform.X64, true)]
    [InlineData(Platform.Arm64, true)]
    public void Native_extrema_require_explicit_64_bit_compilation(Platform platform, bool accepts64BitBounds)
    {
        const string source = """
            using SharpSafeNumerics;
            [BoundedInteger(typeof(nint), "-9223372036854775808", "9223372036854775807")]
            internal readonly partial struct SignedNative;
            [BoundedInteger(typeof(nuint), "0", "18446744073709551615")]
            internal readonly partial struct UnsignedNative;
            """;
        var (output, result) = Generate(source, platform);
        if (accepts64BitBounds)
        {
            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedTrees.Length);
            AssertCompiles(output);
        }
        else
        {
            Assert.Empty(result.GeneratedTrees);
            Assert.Equal(2, result.Diagnostics.Length);
            Assert.All(result.Diagnostics, diagnostic => Assert.Equal("SSNG003", diagnostic.Id));
        }
    }

    [Theory]
    [InlineData(Platform.AnyCpu)]
    [InlineData(Platform.X86)]
    [InlineData(Platform.X64)]
    public void All_storage_types_accept_their_portable_extrema(Platform platform)
    {
        const string source = """
            using System;
            using SharpSafeNumerics;
            [BoundedInteger(typeof(sbyte), "-128", "127")] internal partial struct S8;
            [BoundedInteger(typeof(byte), "0", "255")] internal partial struct U8;
            [BoundedInteger(typeof(short), "-32768", "32767")] internal partial struct S16;
            [BoundedInteger(typeof(ushort), "0", "65535")] internal partial struct U16;
            [BoundedInteger(typeof(int), "-2147483648", "2147483647")] internal partial struct S32;
            [BoundedInteger(typeof(uint), "0", "4294967295")] internal partial struct U32;
            [BoundedInteger(typeof(long), "-9223372036854775808", "9223372036854775807")] internal partial struct S64;
            [BoundedInteger(typeof(ulong), "0", "18446744073709551615")] internal partial struct U64;
            [BoundedInteger(typeof(Int128), "-170141183460469231731687303715884105728", "170141183460469231731687303715884105727")]
            internal partial struct S128;
            [BoundedInteger(typeof(UInt128), "0", "340282366920938463463374607431768211455")]
            internal partial struct U128;
            [BoundedInteger(typeof(IntPtr), "-2147483648", "2147483647")] internal partial struct SNative;
            [BoundedInteger(typeof(UIntPtr), "0", "4294967295")] internal partial struct UNative;
            """;
        var (output, result) = Generate(source, platform);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(12, result.GeneratedTrees.Length);
        AssertCompiles(output);
    }

    private static (Compilation Output, GeneratorDriverRunResult Result) Generate(string source,
        Platform platform = Platform.AnyCpu)
    {
        var outputKind = platform == Platform.AnyCpu32BitPreferred
            ? OutputKind.ConsoleApplication
            : OutputKind.DynamicallyLinkedLibrary;
        var compilation = CSharpCompilation.Create("DomainFixture", [CSharpSyntaxTree.ParseText(source, ParseOptions)],
            References, new CSharpCompilationOptions(outputKind, platform: platform, checkOverflow: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new BoundedIntegerGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (output, driver.GetRunResult());
    }

    private static void AssertCompiles(Compilation output)
    {
        // Test source types omit XML docs. Generated public declarations must provide their own.
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error ||
            (diagnostic.Severity == DiagnosticSeverity.Warning &&
             diagnostic.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true)));
    }

    private static void AssertProbeSucceeds(string source)
    {
        var (output, result) = Generate(source);
        Assert.Empty(result.Diagnostics);
        AssertCompiles(output);
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        image.Position = 0;

        // Reflection here invokes test code emitted by the generator under test. The runtime
        // domain API uses ordinary typed operations. A collectible context isolates fixtures.
        var loadContext = new AssemblyLoadContext("GeneratedDomainFixture", isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromStream(image);
            var method = assembly.GetType("Probe", throwOnError: true)!.GetMethod("Run");
            Assert.NotNull(method);
            Assert.Equal(string.Empty, Assert.IsType<string>(method.Invoke(null, null)));
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static PortableExecutableReference[] LoadReferences()
    {
        // Metadata references come from this executing runtime; the production generator does not reflect.
        string assemblies = Assert.IsType<string>(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"));
        return assemblies.Split(Path.PathSeparator)
            .Append(typeof(BoundedIntegerAttribute).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
