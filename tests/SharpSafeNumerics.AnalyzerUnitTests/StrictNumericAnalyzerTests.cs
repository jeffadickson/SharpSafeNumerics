using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using CsCheck;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using SharpSafeNumerics.Analyzers;
using SharpSafeNumerics.Generator;
using Xunit;

namespace SharpSafeNumerics.AnalyzerUnitTests;

/// <summary>Compiles C# consumers and checks each diagnostic identity, severity, and exact source span.</summary>
public sealed class StrictNumericAnalyzerTests
{
    private static readonly string CoreAssemblyPath = typeof(SafeInteger).Assembly.Location;

    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Append(CoreAssemblyPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();

    private const string Preamble = """
        using System;
        using System.Numerics;
        using System.Threading.Tasks;
        using SharpSafeNumerics;

        """ + "\n";

    [Fact]
    public void Diagnostic_catalog_has_actionable_default_enabled_editor_metadata()
    {
        // IDEs and editorconfig consume these descriptors before a diagnostic fires.
        var descriptors = new StrictNumericAnalyzer().SupportedDiagnostics;
        Assert.Equal(8, descriptors.Length);
        Assert.Equal(8, descriptors.Select(rule => rule.Id).Distinct().Count());
        foreach (var rule in descriptors)
        {
            Assert.Equal("SharpSafeNumerics.Safety", rule.Category);
            Assert.True(rule.IsEnabledByDefault);
            Assert.False(string.IsNullOrWhiteSpace(rule.Title.ToString(CultureInfo.InvariantCulture)));
            Assert.False(string.IsNullOrWhiteSpace(rule.MessageFormat.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal(rule.Id is "SSN1003" or "SSN1007" ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error,
                rule.DefaultSeverity);
        }
    }

    [Theory]
    [InlineData("byte Cast(int x) => {|SSN1001:(byte)x|};")]
    [InlineData("int Cast(uint x) => checked({|SSN1001:(int)x|});")]
    [InlineData("ulong Cast(long value) => {|SSN1002:unchecked({|SSN1001:(ulong)value|})|};")]
    [InlineData("Int128 Cast(UInt128 value) => {|SSN1001:(Int128)value|};")]
    [InlineData("long? Cast(Int128? value) => {|SSN1001:(long?)value|};")]
    [InlineData("nint Cast(long value) => {|SSN1001:(nint)value|};")]
    [InlineData("int Cast(nint value) => {|SSN1001:(int)value|};")]
    [InlineData("byte Cast() => (byte)42;")]
    [InlineData("byte Cast() => (byte)(short)42;")]
    [InlineData("sbyte Cast() => (sbyte)(byte)42;")]
    [InlineData("sbyte Cast() => (sbyte)(ushort)42;")]
    [InlineData("long Cast(uint value) => (long)value;")]
    [InlineData("Int128 Cast(ulong value) => value;")]
    [InlineData("long Cast(nint value) => (long)value;")]
    [InlineData("int Cast(byte? value) => (int)(value ?? 0);")]
    [InlineData("byte? CastNull() => (byte?)(int?)null;")]
    [InlineData("byte? CastDefault() => (byte?)default(int?);")]
    [InlineData("(byte, byte) Cast((int, int) value) => {|SSN1001:((byte, byte))value|};")]
    [InlineData("(byte, (int, int)) Cast((int, (int, int)) value) => checked({|SSN1001:((byte, (int, int)))value|});")]
    [InlineData("(byte, byte) Cast() => ((byte, byte))(1, 255);")]
    [InlineData("(long, int) Cast((int, byte) value) => ((long, int))value;")]
    [InlineData("ArithmeticError Cast(int source, out byte result) => SafeInteger.TryConvert(source, out result);")]
    public async Task Narrowing_requires_a_representable_source_range_or_a_status_boundary(string member)
    {
        await Verify("[StrictNumeric] class Consumer { " + member + " }");
    }

    [Theory]
    [InlineData("int M(int x) => {|SSN1002:x + 1|};")]
    [InlineData("Int128 M(Int128 x) => {|SSN1002:x * x|};")]
    [InlineData("int M(int x) { {|SSN1002:x++|}; return {|SSN1002:-x|}; }")]
    [InlineData("int M(int x) { {|SSN1002:x -= 2|}; return x; }")]
    [InlineData("int M(int x) { {|SSN1002:unchecked { x -= 2; }|} return x; }")]
    [InlineData("int M(int x) => checked(x + 1);")]
    [InlineData("int M(int x) { checked { x++; x -= 2; } return checked(-x); }")]
    [InlineData("int M(int x) { x |= 2; x >>= 2; return x; }")]
    [InlineData("int M(int x) => x & (3 << 2);")]
    [InlineData("int M(int x) => x / 2 + 0;", true)]
    [InlineData("int M() => 2 + 3;")]
    public async Task Unchecked_operations_and_explicit_overrides_follow_the_compiler_context(
        string member, bool checkOverflow = false)
    {
        await Verify("[StrictNumeric] class Consumer { " + member + " }", checkOverflow: checkOverflow);
    }

    [Theory]
    [InlineData("void M() { {|SSN1003:SafeInteger.Add(1, 2)|}; }")]
    [InlineData("void M() => _ = ({|SSN1003:SafeInteger.Add(1, 2)|});")]
    [InlineData("void M() => {|SSN1003:SafeInteger.Add(1, 2)|};")]
    [InlineData("Action M() => () => {|SSN1003:SafeInteger.Add(1, 2)|};")]
    [InlineData("async Task M(Task<NumericResult<int>> pending) => {|SSN1003:await pending|};")]
    [InlineData("void M(NumericResult<int> value) => _ = {|SSN1003:value|};")]
    [InlineData("void M() { ({|SSN1003:_|}, var y) = (SafeInteger.Add(1, 2), 0); _ = y; }")]
    [InlineData("void M() => Read(out {|SSN1003:_|}); static void Read(out NumericResult<int> result) => result = default;")]
    [InlineData("void M() { NumericResult<int> _ = default; _ = SafeInteger.Add(1, 2); Consume(_); } void Consume(NumericResult<int> value) { }")]
    [InlineData("NumericResult<int> M() => SafeInteger.Add(1, 2);")]
    [InlineData("bool M() => SafeInteger.Add(1, 2).IsSuccess;")]
    public async Task Discarded_results_are_warnings_in_all_scopes(string member)
    {
        await Verify("class Consumer { " + member + " }");
    }

    [Theory]
    [InlineData("NumericResult<int> M() => SafeInteger.Add(1, 2, {|SSN1004:ArithmeticPolicy.Throw|});")]
    [InlineData("NumericResult<int> M() => SafeInteger.Add(1, 2, {|SSN1004:Throw|});")]
    [InlineData("ArithmeticPolicy M() => {|SSN1004:LegacyProvider.Policy|};")]
    [InlineData("ArithmeticPolicy M() => {|SSN1004:(ArithmeticPolicy)3|};")]
    [InlineData("ArithmeticPolicy M() { const ArithmeticPolicy p = {|SSN1004:(ArithmeticPolicy)3|}; return {|SSN1004:p|}; }")]
    [InlineData("void M() => {|SSN1004:LegacyProvider.Apply()|};")]
    [InlineData("void M() => {|SSN1004:LegacyProvider.ApplyNullable()|};")]
    [InlineData("LegacyConstruction M() => {|SSN1004:{|SSN1007:new LegacyConstruction()|}|};")]
    [InlineData("void M() => LegacyProvider.ApplyNullable(null);")]
    [InlineData("ArithmeticPolicy M() => (ArithmeticPolicy)(int){|SSN1004:ArithmeticPolicy.Throw|};")]
    [InlineData("ArithmeticPolicy M() => {|SSN1004:ArithmeticPolicy.Wrap | ArithmeticPolicy.Saturate|};")]
    [InlineData("string M() => nameof(ArithmeticPolicy.Throw);")]
    [InlineData("ArithmeticPolicy M() => ArithmeticPolicy.Reject;")]
    [InlineData("ArithmeticPolicy M(ArithmeticPolicy policy) => policy;")]
    [InlineData("OtherPolicy M() => OtherPolicy.Throw;")]
    [InlineData("OtherBytePolicy M() => OtherBytePolicy.Throw;")]
    [InlineData("byte M() => (byte)3;")]
    [InlineData("byte M() { const byte code = 3; return code; }")]
    public async Task Throw_policy_is_detected_when_the_bound_value_is_known(string member)
    {
        await Verify("""
            using static SharpSafeNumerics.ArithmeticPolicy;
            enum OtherPolicy { Throw = 3 }
            enum OtherBytePolicy : byte { Throw = 3 }
            [StrictNumeric] class Consumer
            {
            """ + member + """
            }
            static class LegacyProvider
            {
                internal const ArithmeticPolicy Policy = ArithmeticPolicy.Throw;
                internal static void Apply(ArithmeticPolicy policy = ArithmeticPolicy.Throw) { }
                internal static void ApplyNullable(ArithmeticPolicy? policy = ArithmeticPolicy.Throw) { }
            }
            class LegacyConstruction
            {
                internal LegacyConstruction(ArithmeticPolicy? policy = ArithmeticPolicy.Throw) { }
            }
            """);
    }

    [Theory]
    [InlineData("byte M(int x) => {|SSN1005:byte.CreateSaturating(x)|};")]
    [InlineData("ushort M(int x) => {|SSN1005:ushort.CreateTruncating(x)|};")]
    [InlineData("T M<T>(int x) where T : INumberBase<T> => {|SSN1005:T.CreateSaturating(x)|};")]
    [InlineData("byte M(int x) => byte.CreateChecked(x);")]
    [InlineData("byte M(int x) => Utility.CreateSaturating(x);")]
    [InlineData("NumericResult<int> M(int x) => SafeInteger.Add(x, 1, ArithmeticPolicy.Saturate);")]
    public async Task Raw_generic_math_policy_conversion_requires_a_named_numeric_boundary(string member)
    {
        await Verify("[StrictNumeric] class Consumer { " + member +
            " } static class Utility { internal static byte CreateSaturating(int x) => 0; }");
    }

    [Fact]
    public async Task Generic_math_rule_ignores_a_same_named_contract_from_an_unrelated_interface()
    {
        await Verify("""
            interface ILookalike
            {
                static abstract byte CreateSaturating<T>(T value);
            }
            readonly struct Adapter : ILookalike
            {
                public static byte CreateSaturating<T>(T value) => 0;
            }
            [StrictNumeric] class Consumer
            {
                byte M(int value) => Adapter.CreateSaturating(value);
            }
            """);
    }

    [Fact]
    public async Task Generated_domain_can_be_bypassed_only_at_the_diagnosed_use_site()
    {
        await Verify("""
            [BoundedInteger(typeof(int), "1", "4")] readonly partial struct Port
            {
                [StrictNumeric] static Port Raw(int value) => {|SSN1006:new Port(value)|};
                [StrictNumeric] public int RawValue() => {|SSN1006:__ssnValue|};
                [StrictNumeric] public bool RawValidity() => {|SSN1006:__ssnValid|};
                [StrictNumeric] public string StorageName() => nameof(__ssnValue);
            }
            [StrictNumeric] class Consumer
            {
                Port Cast(int value) => {|SSN1006:(Port)value|};
                int Cast(Port value) => {|SSN1006:(int)value|};
                int Read(Port value) => {|SSN1006:value.Value|};
                Port Reinterpret(int value) => {|SSN1006:System.Runtime.CompilerServices.Unsafe.BitCast<int, Port>(value)|};
                static ref int RawStorage(ref Port p) => ref {|SSN1006:System.Runtime.CompilerServices.Unsafe.As<Port, int>(ref p)|};
                static ref Port? NullableStorage(ref long data) => ref {|SSN1006:System.Runtime.CompilerServices.Unsafe.As<long, Port?>(ref data)|};
                Port GetBytes(ReadOnlySpan<byte> bytes) => {|SSN1006:System.Runtime.InteropServices.MemoryMarshal.Read<Port>(bytes)|};
                Port Copy(Port value) => System.Runtime.CompilerServices.Unsafe.BitCast<Port, Port>(value);
                ArithmeticError Create(int value, out Port port) => Port.TryCreate(value, out port);
                bool ReadSafely(Port p, out int result) => p.TryGetValue(out result);
                int Bound() => Port.Maximum;
                string PropertyName(Port value) => nameof(value.Value);
            }
            """, generate: true);
    }

    [Fact]
    public async Task Raw_reinterpretation_matches_bound_intrinsics_and_a_bounded_payload()
    {
        await Verify("""
            using RawMemory = System.Runtime.InteropServices.MemoryMarshal;
            using Ordinary = Providers.Ordinary;
            [BoundedInteger(typeof(int), "1", "4")] readonly partial struct Port;
            [StrictNumeric] class Consumer
            {
                int __ssnValue = 1;
                int Value => __ssnValue;
                int UnannotatedRead() => Value;
                Span<Port> Cast(Span<byte> bytes) => {|SSN1006:RawMemory.Cast<byte, Port>(bytes)|};
                Span<byte> AsBytes(Span<Port> ports) => {|SSN1006:RawMemory.AsBytes(ports)|};
                ref Port AsRef(Span<byte> bytes) => ref {|SSN1006:RawMemory.AsRef<Port>(bytes)|};
                bool TryRead(ReadOnlySpan<byte> bytes, out Port port) => {|SSN1006:RawMemory.TryRead(bytes, out port)|};
                int PlainRead(ReadOnlySpan<byte> bytes) => RawMemory.Read<int>(bytes);
                Span<int> PlainCast(Span<byte> bytes) => RawMemory.Cast<byte, int>(bytes);
                int PlainBitCast(uint value) => System.Runtime.CompilerServices.Unsafe.BitCast<uint, int>(value);
                Port UserRead(Port port) => Ordinary.Read(port);
                Port UserAs(Port port) => Ordinary.As(0, port);
                Port UserGeneric(Port port) => Ordinary.Forward(0, port);
            }
            namespace Providers
            {
                static class Ordinary
                {
                    internal static T Read<T>(T value) => value;
                    internal static TTo As<TFrom, TTo>(TFrom input, TTo value) => value;
                    internal static TTo Forward<TFrom, TTo>(TFrom input, TTo value) => value;
                }
            }
            """, generate: true);
    }

    [Theory]
    [InlineData("object M() => {|SSN1007:new object()|};")]
    [InlineData("object M() => {|SSN1007:new { Value = 1 }|};")]
    [InlineData("int[] M() => {|SSN1007:new int[0]|};")]
    [InlineData("int[] M() => {|SSN1007:[1, 2]|};")]
    [InlineData("int[] M() => [];")]
    [InlineData("object M(int value) => {|SSN1007:value|};")]
    [InlineData("object M(string value) => value;")]
    [InlineData("string M(object value) => (string)value;")]
    [InlineData("object M<T>(T value) where T : class => value;")]
    [InlineData("IFormattable M(int value) => {|SSN1007:value|};")]
    [InlineData("string M(int value) => {|SSN1007:$\"Data: {value}\"|};")]
    [InlineData("FormattableString M(int value) => {|SSN1007:$\"Data: {value}\"|};")]
    [InlineData("Func<int> M(int value) => {|SSN1007:() => value|};")]
    [InlineData("int state = 1; Func<int> M() => {|SSN1007:() => state|};")]
    [InlineData("Func<int> M(int value) { int local = value; return {|SSN1007:() => local|}; }")]
    [InlineData("Func<int, int> M() => value => { int local = value; return local; };")]
    [InlineData("Func<int> M() => {|SSN1007:Read|}; int Read() => 42;")]
    [InlineData("Func<int> M() => {|SSN1007:new Func<int>(Read)|}; static int Read() => 42;")]
    [InlineData("Func<int> M() => Read; static int Read() => 42;")]
    [InlineData("int M() => {|SSN1007:Count(1)|}; static int Count(params int[] args) => args.Length;")]
    [InlineData("int M() => Count(); static int Count(params int[] args) => args.Length;")]
    [InlineData("int? M(int value) => value;")]
    [InlineData("object? M() => (int?)null;")]
    [InlineData("DateTime M() => new DateTime(1);")]
    [InlineData("T M<T>() where T : class, new() => {|SSN1007:new T()|};")]
    [InlineData("T M<T>() where T : struct => new T();")]
    [InlineData("Func<int> M() => static () => 42;")]
    [InlineData("Func<int> M() => () => 42;")]
    [InlineData("Func<int> M() { const int value = 42; return () => value; }")]
    [InlineData("string M() => $\"{nameof(Consumer)}\";")]
    [InlineData("void M(System.Text.StringBuilder destination, int value) => destination.Append($\"Data: {value}\");")]
    [InlineData("void M(System.Text.StringBuilder destination, int value) => destination.Append($\"Data: {value}\" + $\" More: {value}\");")]
    [InlineData("int M() { Span<int> values = stackalloc int[1]; return values.Length; }")]
    [InlineData("int M(int value) { int Local() => value; return Local(); }")]
    [InlineData("Func<int> M(int value) { int Local() => value; return {|SSN1007:() => Local()|}; }")]
    [InlineData("Func<int> M(int value) { int Inner() => value; int Local() => Inner(); return {|SSN1007:() => Local()|}; }")]
    [InlineData("Func<int> M() { int Local() { int Nested() => 42; return Nested(); } return () => Local(); }")]
    [InlineData("Func<int> M() { int Local(int value) { int Nested() => value; return Nested(); } return () => Local(42); }")]
    [InlineData("Func<int> M() { int Loop(int x) => x == 0 ? 0 : Loop(0); return () => Loop(1); }")]
    public async Task Evident_allocations_are_warnings_without_flagging_value_construction(string member)
    {
        await Verify("[StrictNumeric] class Consumer { " + member + " }");
    }

    [Fact]
    public async Task Invalid_defaults_and_zero_valid_defaults_follow_the_domain_contract()
    {
        await Verify("""
            [BoundedInteger(typeof(int), "0", "4")] readonly partial struct InvalidByDefault;
            [BoundedInteger(typeof(int), "1", "4", DefaultPolicy = DefaultValuePolicy.Invalid)] readonly partial struct InvalidExplicit;
            [BoundedInteger(typeof(int), "0", "4", DefaultPolicy = DefaultValuePolicy.ZeroValid)] readonly partial struct ZeroValid;
            [BoundedInteger(typeof(int), "0", "4", OverflowPolicy = ArithmeticPolicy.Saturate)] readonly partial struct Saturating;
            [StrictNumeric] class Consumer
            {
                InvalidByDefault Invalid() => {|SSN1008:default|};
                InvalidByDefault InvalidNew() => {|SSN1008:new()|};
                InvalidExplicit ExplicitDefault() => {|SSN1008:default(InvalidExplicit)|};
                ZeroValid Valid() => default;
                ZeroValid ValidNew() => new();
                Saturating NoDefaultPolicy() => {|SSN1008:default|};
                InvalidByDefault? Absent() => default;
                bool TestState(InvalidByDefault value) => value.IsValid;
            }
            class NonStrict { InvalidByDefault Accepted() => default; }
            """, generate: true);
    }

    [Fact]
    public async Task Generated_contracts_survive_assembly_boundaries()
    {
        var declarations = Compile(Preamble + """
            [BoundedInteger(typeof(int), "1", "4", OverflowPolicy = ArithmeticPolicy.Throw)]
            public readonly partial struct Port;
            [BoundedInteger(typeof(int), "0", "4", DefaultPolicy = DefaultValuePolicy.ZeroValid)]
            public readonly partial struct ZeroPort;
            """, "DomainLibrary", generate: true);
        using var image = new MemoryStream();
        var emitted = declarations.Emit(image);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        await Verify("""
            [StrictNumeric] class Consumer
            {
                NumericResult<Port> Add(Port value) => {|SSN1004:value.Add(1)|};
                NumericResult<Port> Subtract(Port value) => {|SSN1004:value.Subtract(1)|};
                int Read(Port value) => {|SSN1006:value.Value|};
                string PropertyName(Port value) => nameof(value.Value);
                Port Make(int value) => {|SSN1006:(Port)value|};
                Port Invalid() => {|SSN1008:new()|};
                ZeroPort Zero() => default;
                int ReadZero(ZeroPort value) => value.Value;
                int CastZero(ZeroPort value) => (int)value;
                ArithmeticError TryAdd(Port port, out Port sum) => port.TryAdd(1, out sum);
            }
            """, additionalReferences: [MetadataReference.CreateFromImage(image.ToArray())]);
    }

    [Fact]
    public async Task All_built_in_integer_pairs_match_exact_range_containment()
    {
        // The expected ranges use BigInteger endpoints and evaluate both native widths.
        var representations = new (string Name, int X86, int X64, bool Signed)[]
        {
            ("byte", 8, 8, false), ("sbyte", 8, 8, true), ("char", 16, 16, false),
            ("short", 16, 16, true), ("ushort", 16, 16, false),
            ("int", 32, 32, true), ("uint", 32, 32, false),
            ("long", 64, 64, true), ("ulong", 64, 64, false),
            ("nint", 32, 64, true), ("nuint", 32, 64, false),
            ("Int128", 128, 128, true), ("UInt128", 128, 128, false)
        };
        var source = new StringBuilder("[StrictNumeric] static class Consumer {");
        int method = 0;
        foreach (var from in representations)
        foreach (var to in representations)
        {
            var cast = $"({to.Name})value";
            if (!Contains(from.X86, to.X86, from.Signed, to.Signed) ||
                !Contains(from.X64, to.X64, from.Signed, to.Signed))
            {
                cast = "{|SSN1001:" + cast + "|}";
            }
            source.Append(CultureInfo.InvariantCulture, $"static {to.Name} Test{method++}({from.Name} value) => {cast};");
        }
        await Verify(source.Append('}').ToString());

        static bool Contains(int from, int to, bool fromSigned, bool toSigned)
        {
            BigInteger fromMin = fromSigned ? -BigInteger.Pow(2, from - 1) : BigInteger.Zero;
            BigInteger fromMax = BigInteger.Pow(2, from - (fromSigned ? 1 : 0)) - 1;
            BigInteger toMin = toSigned ? -BigInteger.Pow(2, to - 1) : BigInteger.Zero;
            BigInteger toMax = BigInteger.Pow(2, to - (toSigned ? 1 : 0)) - 1;
            return toMin <= fromMin && toMax >= fromMax;
        }
    }

    [Fact]
    public async Task Seeded_literal_casts_and_malformed_operands_use_compiler_binding()
    {
        // Keep each failing expression visible in the diagnostic-span assertions, with a
        // reproducible seed for the randomly selected constant and unresolved name.
        string seed = new PCG(1, 114938).ToString();
        var samples = new List<int> { int.MinValue, -1, 0, 1, 254, 255, 256, int.MaxValue };
        Gen.Int.Sample(value => { samples.Add(value); return true; }, seed: seed, iter: 64, threads: 1);

        var source = new StringBuilder("[StrictNumeric] static class Consumer {");
        var errors = new List<string>();
        for (int index = 0; index < samples.Count; index++)
        {
            int value = samples[index];
            string literal = value.ToString(CultureInfo.InvariantCulture);
            string cast = "(byte)(" + literal + ")";
            if (value < byte.MinValue || value > byte.MaxValue)
                cast = "{|SSN1001:" + cast + "|}";
            source.Append(CultureInfo.InvariantCulture, $"static byte Cast{index}() => {{|SSN1002:unchecked({cast})|}};");

            if (index % 4 != 0)
                continue;
            source.Append(CultureInfo.InvariantCulture, $"static int Missing{index}() => Undefined_{(uint)value:X8};");
            errors.Add("CS0103");
        }
        source.Append("static int Broken() => ;");
        errors.Add("CS1525");
        await Verify(source.Append('}').ToString(), compilerErrors: errors.ToArray());
    }

    [Fact]
    public async Task Explicit_unchecked_context_does_not_cross_function_boundaries()
    {
        await Verify("""
            [StrictNumeric] class Consumer
            {
                Func<int, int> Lambda()
                {
                    {|SSN1002:unchecked { return static x => {|SSN1002:x + 1|}; }|}
                }
                int Local(int value)
                {
                    {|SSN1002:unchecked {
                        int AddOne(int x) => {|SSN1002:x + 1|};
                        return AddOne(value);
                    }|}
                }
            }
            """);
    }

    [Fact]
    public async Task Strict_scope_follows_lexical_symbols_including_nested_and_async_bodies()
    {
        await Verify("""
            [StrictNumeric] class Outer
            {
                static int seed = GetValue();
                static int GetValue() => 1;
                static int initialized = {|SSN1002:seed + 1|};
                internal record Child(int Number)
                {
                    internal Task<int> Read(Task<int> value) => Work(value);
                    static async Task<int> Work(Task<int> value)
                    {
                        var resumed = await value;
                        int Local() => {|SSN1002:resumed + 1|};
                        Func<int, int> transform = static x => {|SSN1002:x + 1|};
                        return transform(Local());
                    }
                }
            }
            class Peer
            {
                int Loose(int x) => x + 1;
                int Property { [StrictNumeric] get => {|SSN1002:Loose(0) + 1|}; }
                int LocalScope(int x)
                {
                    [StrictNumeric] int Local(int y) => {|SSN1002:y + 1|};
                    return Local(x + 1);
                }
                Func<int, int> Lambda() => [StrictNumeric] static (x) => {|SSN1002:x + 1|};
            }
            """);
    }

    [Fact]
    public async Task Unrelated_annotations_and_generic_result_types_do_not_match()
    {
        await Verify("""
            namespace Local
            {
                class StrictNumericAttribute : Attribute;
                struct NumericResult<T>;
                [StrictNumeric] class Consumer
                {
                    byte Cast(int value) => (byte)value;
                    NumericResult<int> Fake() => default;
                    void IgnoreFake() => Fake();
                }
            }
            """);
    }

    [Fact]
    public async Task Alias_only_core_references_resolve_the_real_contracts()
    {
        await Verify("""
            [StrictNumeric] class Consumer
            {
                byte Narrow(int value) => {|SSN1001:(byte)value|};
                int Wrap(int value) => {|SSN1002:value + 1|};
                void Ignore() => {|SSN1003:SafeInteger.Add(1, 2, {|SSN1004:ArithmeticPolicy.Throw|})|};
            }
            """, coreAlias: "actual");
    }

    [Fact]
    public async Task Malformed_code_does_not_crash_or_create_safety_diagnostics_for_unbound_types()
    {
        await Verify("""
            [StrictNumeric] class Consumer
            {
                MissingType M(UnknownType value) => (MissingType)value;
            }
            """, compilerErrors: ["CS0246", "CS0246", "CS0246"]);
    }

    /// <summary>Removes nested diagnostic markup while preserving each marked expression's final span.</summary>
    private static async Task Verify(string source, bool checkOverflow = false, bool generate = false,
        string[]? compilerErrors = null, MetadataReference[]? additionalReferences = null, string? coreAlias = null)
    {
        var expected = new List<(string Id, TextSpan Span)>();
        var stack = new Stack<(string Id, int Start)>();
        string marked = (coreAlias is null ? Preamble :
            "extern alias " + coreAlias + ";\n" +
            Preamble.Replace("using SharpSafeNumerics;", "using " + coreAlias + "::SharpSafeNumerics;", StringComparison.Ordinal))
            + source;
        const string marker = @"\{\|(SSN\d+):|\|\}";
        string code = Regex.Replace(marked, marker, "");
        // Matches are processed left to right. Subtract the markup already removed.
        int removed = 0;
        foreach (Match match in Regex.Matches(marked, marker))
        {
            int offset = match.Index - removed;
            if (match.Groups[1].Success)
                stack.Push((match.Groups[1].Value, offset));
            else
            {
                var (id, start) = stack.Pop();
                expected.Add((id, TextSpan.FromBounds(start, offset)));
            }
            removed += match.Length;
        }

        var compilation = Compile(code, "StrictNumericAnalyzerFixture", checkOverflow, generate, additionalReferences,
            coreAlias);
        var tree = compilation.SyntaxTrees.First();

        var compilerIssues = compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).ToArray();
        if (compilerErrors is null)
            Assert.Empty(compilerIssues);
        else
            Assert.Equal(compilerErrors.Order(), compilerIssues.Select(d => d.Id).Order());

        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new StrictNumericAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        var actual = diagnostics.Select(d => (d.Id, d.Location.SourceSpan)).ToList();
        Assert.Equal(expected.OrderBy(d => d.Span.Start).ThenBy(d => d.Id),
            actual.OrderBy(d => d.SourceSpan.Start).ThenBy(d => d.Id));
        foreach (var diagnostic in diagnostics)
        {
            Assert.Same(tree, diagnostic.Location.SourceTree);
            Assert.Equal(diagnostic.Id is "SSN1003" or "SSN1007" ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error,
                diagnostic.Severity);
            if (diagnostic.Id == "SSN1007")
            {
                // An allocation warning must say what may allocate, not just name its scope.
                string message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
                const string prefix = "Potential managed allocation in StrictNumeric scope: ";
                Assert.StartsWith(prefix, message);
                Assert.False(string.IsNullOrWhiteSpace(message[prefix.Length..]));
            }
        }
    }

    private static Compilation Compile(string code, string assemblyName, bool checkOverflow = false, bool generate = false,
        MetadataReference[]? additionalReferences = null, string? coreAlias = null)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp15);
        var tree = CSharpSyntaxTree.ParseText(code, parseOptions);
        IEnumerable<MetadataReference> references = coreAlias is null ? References :
            References.Select(reference => string.Equals(reference.Display, CoreAssemblyPath, StringComparison.OrdinalIgnoreCase)
                ? reference.WithAliases([coreAlias]) : reference);
        Compilation compilation = CSharpCompilation.Create(assemblyName, [tree],
            additionalReferences is null ? references : references.Concat(additionalReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, checkOverflow: checkOverflow,
                nullableContextOptions: NullableContextOptions.Enable));
        if (generate)
        {
            var driver = CSharpGeneratorDriver.Create([new BoundedIntegerGenerator().AsSourceGenerator()],
                parseOptions: parseOptions);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out compilation, out var generatorDiagnostics);
            Assert.Empty(generatorDiagnostics);
        }
        return compilation;
    }
}
