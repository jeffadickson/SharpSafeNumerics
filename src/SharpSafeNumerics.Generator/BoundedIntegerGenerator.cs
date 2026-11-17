using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharpSafeNumerics.Generator;

/// <summary>Validates bounded integer declarations and emits their typed domain APIs.</summary>
[Generator]
public sealed partial class BoundedIntegerGenerator : IIncrementalGenerator
{
    private const string AnnotationName = "SharpSafeNumerics.BoundedIntegerAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = Rule(
        "SSNG001", "Bounded numeric type must be partial",
        "Type '{0}' must be declared partial to use BoundedIntegerAttribute");

    private static readonly DiagnosticDescriptor UnsupportedStorage = Rule(
        "SSNG002", "Unsupported bounded integer storage",
        "Storage type '{0}' is not a supported built-in integer");

    private static readonly DiagnosticDescriptor InvalidBounds = Rule(
        "SSNG003", "Invalid bounded integer range",
        "Bounds '{0}'..'{1}' are invalid for {2}-bit storage type '{3}'");

    private static readonly DiagnosticDescriptor UnsupportedShape = Rule(
        "SSNG004", "Unsupported bounded integer declaration",
        "Type '{0}' must be an ordinary top-level, non-generic, non-file-local, non-ref struct without instance state, user constructors, explicit layout or inline-array layout");

    private static readonly DiagnosticDescriptor MemberCollision = Rule(
        "SSNG005", "Bounded integer member collision",
        "Type '{0}' already declares or uses the generated member name '{1}'");

    private static readonly DiagnosticDescriptor InvalidOption = Rule(
        "SSNG006", "Invalid bounded integer option",
        "Option '{0}' has an invalid value on bounded integer type '{1}'");

    private static readonly DiagnosticDescriptor InvalidZeroDefault = Rule(
        "SSNG007", "Zero default is outside the range",
        "Type '{0}' requires a range containing zero when DefaultPolicy is ZeroValid");

    private static readonly DiagnosticDescriptor InvalidSerializationWidth = Rule(
        "SSNG008", "Invalid bounded integer binary width",
        "SerializationWidth {0} must be 1, 2, 4, 8 or 16 bytes, fit the storage width, and represent the full range of '{1}'");

    // These names are reserved across all options, so changing a policy cannot introduce a new collision.
    private static readonly string[] ReservedNames =
    [
        "__ssnValue", "__ssnValid", "__ssnAdmitArithmetic",
        "Minimum", "Maximum", "IsValid", "Value", "TryGetValue", "TryCreate", "TryCreateFrom", "TryConvertTo",
        "get_Minimum", "get_Maximum", "get_IsValid", "get_Value",
        "TryAdd", "TrySubtract", "Add", "Subtract", "TryParse", "TryFormat", "TryRead", "TryWrite",
        "OverflowPolicy", "DefaultPolicy", "TextPolicy", "SerializationWidth", "SerializationEndianness", "Unit",
        "ToString", "Equals", "GetHashCode", "CompareTo", "op_Explicit", "op_CheckedExplicit", "op_Implicit",
        "op_Equality", "op_Inequality", "op_LessThan", "op_GreaterThan", "op_LessThanOrEqual", "op_GreaterThanOrEqual"
    ];

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var declarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            AnnotationName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, cancellationToken) => CreateModel(ctx, cancellationToken));

        context.RegisterSourceOutput(declarations, static (spc, result) =>
        {
            if (result.Diagnostic is { } diagnostic)
                spc.ReportDiagnostic(diagnostic);
            else if (result.Model is { } model)
                spc.AddSource(model.HintName, SourceText.From(Render(model), Encoding.UTF8));
        });
    }

    private static GenerationResult CreateModel(GeneratorAttributeSyntaxContext context,
        System.Threading.CancellationToken cancellationToken)
    {
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var location = syntax.Identifier.GetLocation();

        GenerationResult Error(DiagnosticDescriptor descriptor, params object[] arguments) =>
            new(null, Diagnostic.Create(descriptor, location, arguments));

        if (!syntax.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
            return Error(MustBePartial, symbol.Name);

        var members = symbol.GetMembers();
        var attributes = symbol.GetAttributes();
        if (syntax is not StructDeclarationSyntax || symbol.ContainingType is not null ||
            symbol.Arity != 0 || symbol.IsRefLikeType || symbol.IsFileLocal ||
            symbol.DeclaringSyntaxReferences.Any(reference =>
                reference.GetSyntax(cancellationToken) is StructDeclarationSyntax { ParameterList: not null }) ||
            members.Any(member => HasInstanceState(member, cancellationToken) ||
                member is IMethodSymbol { MethodKind: MethodKind.Constructor, IsImplicitlyDeclared: false }) ||
            attributes.Any(HasUnsupportedLayout))
        {
            return Error(UnsupportedShape, symbol.Name);
        }

        var collision = ReservedNames.FirstOrDefault(name => name == symbol.Name ||
            members.Any(member => name == member.Name ||
                (member is IMethodSymbol method && method.ExplicitInterfaceImplementations.Any(
                    implementation => name == implementation.Name))));
        if (collision is not null)
            return Error(MemberCollision, symbol.Name, collision);

        if (attributes.Count(static attr => attr.AttributeClass?.ToDisplayString() == AnnotationName) != 1)
            return Error(InvalidOption, "BoundedIntegerAttribute", symbol.Name);

        var attribute = context.Attributes[0];
        var compilation = (CSharpCompilation)context.SemanticModel.Compilation;
        if (attribute.ConstructorArguments.Length != 3 ||
            attribute.ConstructorArguments[0].Value is not INamedTypeSymbol storage ||
            GetStorage(storage, compilation) is not { } storageInfo)
        {
            return Error(UnsupportedStorage, attribute.ConstructorArguments.FirstOrDefault().Value ?? "(missing)");
        }

        var minimum = attribute.ConstructorArguments[1].Value as string;
        var maximum = attribute.ConstructorArguments[2].Value as string;
        if (!TryParseBound(minimum, out var min) || !TryParseBound(maximum, out var max) ||
            min > max || !Fits(storageInfo.Width, storageInfo.IsSigned, min, max))
        {
            return Error(InvalidBounds, minimum ?? "(null)", maximum ?? "(null)", storageInfo.Width * 8, storage.Name);
        }

        if (!TryGetOptions(attribute, out var options, out var invalidOption))
            return Error(InvalidOption, invalidOption, symbol.Name);

        if (options.DefaultPolicy == nameof(DefaultOption.ZeroValid) && (min.Sign > 0 || max.Sign < 0))
            return Error(InvalidZeroDefault, symbol.Name);

        int binaryWidth = options.SerializationWidth == 0 ? storageInfo.Width : options.SerializationWidth;
        if (binaryWidth is not (1 or 2 or 4 or 8 or 16) || binaryWidth > storageInfo.Width ||
            !Fits(binaryWidth, storageInfo.IsSigned, min, max))
        {
            return Error(InvalidSerializationWidth, options.SerializationWidth, symbol.Name);
        }

        var name = Identifier(symbol.Name);
        var ns = NamespaceName(symbol.ContainingNamespace);
        return new(new DomainModel(ns, name, HintName(symbol), symbol.DeclaredAccessibility == Accessibility.Public,
            symbol.IsReadOnly, storageInfo, Literal(storageInfo, min), Literal(storageInfo, max), options, binaryWidth), null);
    }

    private static DiagnosticDescriptor Rule(string id, string title, string message) =>
        new(id, title, message, "SharpSafeNumerics.Generator", DiagnosticSeverity.Error, true);

    private static bool HasInstanceState(ISymbol member, System.Threading.CancellationToken cancellationToken)
    {
        // Field-like events carry hidden backing fields, even when GetMembers exposes only
        // the event and its accessors. Explicit add/remove implementations need no state.
        return member is IFieldSymbol { IsStatic: false } ||
            member is IEventSymbol { IsStatic: false } &&
            member.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax(cancellationToken)
                .AncestorsAndSelf().Any(static node => node is EventFieldDeclarationSyntax));
    }

    private static bool HasUnsupportedLayout(AttributeData attribute)
    {
        string? name = attribute.AttributeClass?.ToDisplayString();
        if (name == "System.Runtime.CompilerServices.InlineArrayAttribute")
            return true;

        if (name != "System.Runtime.InteropServices.StructLayoutAttribute" ||
            attribute.ConstructorArguments.Length != 1)
            return false;

        // StructLayoutAttribute has overloads accepting LayoutKind and Int16.
        return attribute.ConstructorArguments[0].Value is (int)LayoutKind.Explicit or (short)LayoutKind.Explicit;
    }

    private static string NamespaceName(INamespaceSymbol symbol)
    {
        if (symbol.IsGlobalNamespace)
            return string.Empty;

        var prefix = NamespaceName(symbol.ContainingNamespace);
        return prefix.Length == 0 ? Identifier(symbol.Name) : prefix + "." + Identifier(symbol.Name);
    }

    private static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
            ? "@" + name
            : name;

    private static string HintName(INamedTypeSymbol symbol)
    {
        // Hash the full metadata identity into an ASCII file name, including distinctions that
        // would disappear on a case-insensitive file system. The name length stays bounded.
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        return BitConverter.ToString(hash).Replace("-", string.Empty) + ".SharpSafeNumerics.g.cs";
    }

    private static bool TryParseBound(string? text, out BigInteger bound)
    {
        bound = default;
        // The BCL parser allows trailing NUL characters for compatibility with zero-terminated text.
        return text is not null && text.IndexOf('\0') < 0 &&
            BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out bound);
    }

    private static bool Fits(int width, bool signed, BigInteger min, BigInteger max)
    {
        var exclusiveEnd = BigInteger.One << (width * 8 - (signed ? 1 : 0));
        return min >= (signed ? -exclusiveEnd : BigInteger.Zero) && max < exclusiveEnd;
    }

    private static Storage? GetStorage(INamedTypeSymbol symbol, CSharpCompilation compilation)
    {
        // AnyCPU can execute at either native width. The portable declaration must fit the
        // intersection of the two ranges; only a fixed 64-bit compilation can use wider bounds.
        int nativeWidth = compilation.Options.Platform is Platform.X64 or Platform.Arm64 or Platform.Itanium ? 8 : 4;
        return symbol.SpecialType switch
        {
            SpecialType.System_SByte => new("sbyte", 1, true),
            SpecialType.System_Byte => new("byte", 1, false),
            SpecialType.System_Int16 => new("short", 2, true),
            SpecialType.System_UInt16 => new("ushort", 2, false),
            SpecialType.System_Int32 => new("int", 4, true),
            SpecialType.System_UInt32 => new("uint", 4, false),
            SpecialType.System_Int64 => new("long", 8, true),
            SpecialType.System_UInt64 => new("ulong", 8, false),
            SpecialType.System_IntPtr => new("global::System.IntPtr", nativeWidth, true, isNative: true),
            SpecialType.System_UIntPtr => new("global::System.UIntPtr", nativeWidth, false, isNative: true),
            _ when IsSystemInteger(symbol, compilation, "System.Int128") => new("global::System.Int128", 16, true),
            _ when IsSystemInteger(symbol, compilation, "System.UInt128") => new("global::System.UInt128", 16, false),
            _ => null
        };
    }

    private static bool IsSystemInteger(INamedTypeSymbol symbol, CSharpCompilation compilation, string name) =>
        SymbolEqualityComparer.Default.Equals(symbol, compilation.GetTypeByMetadataName(name)) &&
        SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly,
            compilation.GetSpecialType(SpecialType.System_Int32).ContainingAssembly);

    private static string Literal(Storage storage, BigInteger value)
    {
        if (storage.Width == 16)
        {
            // Reconstitute the exact two's-complement bits using two legal 64-bit literals.
            // This handles Int128.MinValue without applying unary negation to a signed 128-bit value.
            var bits = value.Sign < 0 ? value + (BigInteger.One << 128) : value;
            var upper = (ulong)(bits >> 64);
            var lower = (ulong)(bits & ulong.MaxValue);
            string literal = $"new global::System.UInt128(0x{upper:X16}UL, 0x{lower:X16}UL)";
            return storage.IsSigned ? $"unchecked((global::System.Int128){literal})" : literal;
        }

        string decimalText = value.ToString(CultureInfo.InvariantCulture);
        if (storage.IsNative)
        {
            string suffix = (storage.Width, storage.IsSigned) switch
            {
                (8, true) => "L",
                (8, false) => "UL",
                (_, false) => "U",
                _ => string.Empty
            };
            // C# warns about large native constants regardless of PlatformTarget.
            // Only an explicitly 64-bit compilation reaches the full-width cast.
            string cast = $"({storage.Type})({decimalText}{suffix})";
            return storage.Width == 8 ? $"unchecked({cast})" : cast;
        }

        return storage.Type switch
        {
            "uint" => decimalText + "U",
            "long" => decimalText + "L",
            "ulong" => decimalText + "UL",
            _ => decimalText
        };
    }

    private static bool TryGetOptions(AttributeData attribute, out Options options, out string invalid)
    {
        // This Roslyn component targets netstandard2.0 and cannot reference the net11 runtime.
        // These compiler-side option values mirror the public annotation enum contracts.
        options = new Options();
        foreach (var argument in attribute.NamedArguments)
        {
            invalid = argument.Key;
            switch (argument.Key)
            {
                case "OverflowPolicy" when argument.Value.Value is byte policy && policy <= (byte)OverflowOption.Throw:
                    options.OverflowPolicy = ((OverflowOption)policy).ToString();
                    break;
                case "DefaultPolicy" when argument.Value.Value is byte defaultPolicy && defaultPolicy <= (byte)DefaultOption.ZeroValid:
                    options.DefaultPolicy = ((DefaultOption)defaultPolicy).ToString();
                    break;
                case "TextPolicy" when argument.Value.Value is byte text && text <= (byte)TextOption.InvariantHexadecimal:
                    options.TextPolicy = ((TextOption)text).ToString();
                    break;
                case "Endianness" when argument.Value.Value is byte endian && endian <= (byte)EndianOption.BigEndian:
                    options.Endianness = ((EndianOption)endian).ToString();
                    break;
                case "SerializationWidth" when argument.Value.Value is int width:
                    options.SerializationWidth = width;
                    break;
                case "Unit" when argument.Value.IsNull:
                    break;
                case "Unit" when argument.Value.Value is string unit &&
                    !string.IsNullOrWhiteSpace(unit) && !unit.Any(char.IsControl):
                    options.Unit = unit;
                    break;
                default:
                    return false;
            }
        }

        invalid = string.Empty;
        return true;
    }

    private enum OverflowOption : byte { Reject, Saturate, Wrap, Throw }
    private enum DefaultOption : byte { Invalid, ZeroValid }
    private enum TextOption : byte { InvariantDecimal, InvariantHexadecimal }
    private enum EndianOption : byte { LittleEndian, BigEndian }

    private sealed class Options
    {
        public string OverflowPolicy { get; set; } = nameof(OverflowOption.Reject);
        public string DefaultPolicy { get; set; } = nameof(DefaultOption.Invalid);
        public string TextPolicy { get; set; } = nameof(TextOption.InvariantDecimal);
        public string Endianness { get; set; } = nameof(EndianOption.LittleEndian);
        public int SerializationWidth { get; set; }
        public string? Unit { get; set; }
    }

    private sealed class Storage(string type, int width, bool isSigned, bool isNative = false)
    {
        public string Type { get; } = type;
        public int Width { get; } = width;
        public bool IsSigned { get; } = isSigned;
        public bool IsNative { get; } = isNative;
    }

    private sealed class DomainModel(string @namespace, string name, string hintName, bool isPublic, bool isReadOnly,
        Storage storage, string minimum, string maximum, Options options, int binaryWidth)
    {
        public string Namespace { get; } = @namespace;
        public string Name { get; } = name;
        public string HintName { get; } = hintName;
        public bool IsPublic { get; } = isPublic;
        public bool IsReadOnly { get; } = isReadOnly;
        public Storage Storage { get; } = storage;
        public string Minimum { get; } = minimum;
        public string Maximum { get; } = maximum;
        public Options Options { get; } = options;
        public int BinaryWidth { get; } = binaryWidth;
    }

    private sealed class GenerationResult(DomainModel? model, Diagnostic? diagnostic)
    {
        public DomainModel? Model { get; } = model;
        public Diagnostic? Diagnostic { get; } = diagnostic;
    }
}
