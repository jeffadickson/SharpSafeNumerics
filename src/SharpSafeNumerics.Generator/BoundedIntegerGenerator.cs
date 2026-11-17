using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharpSafeNumerics.Generator;

[Generator]
public sealed class BoundedIntegerGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "SSNG001", "Bounded numeric type must be partial",
        "Type '{0}' must be declared partial to use BoundedIntegerAttribute",
        "SharpSafeNumerics.Generator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor UnsupportedStorage = new(
        "SSNG002", "Unsupported bounded integer storage",
        "Storage type '{0}' is not a supported fixed-width integer",
        "SharpSafeNumerics.Generator", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidBounds = new(
        "SSNG003", "Invalid bounded integer range",
        "Bounds '{0}'..'{1}' are invalid for storage type '{2}'",
        "SharpSafeNumerics.Generator", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var declarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            "SharpSafeNumerics.BoundedIntegerAttribute",
            static (node, _) => node is StructDeclarationSyntax,
            static (ctx, _) => CreateModel(ctx));

        context.RegisterSourceOutput(declarations, static (spc, result) =>
        {
            if (result.Diagnostic is { } d)
            {
                spc.ReportDiagnostic(d);
                return;
            }

            if (result.Model is { } model)
                spc.AddSource(model.HintName, SourceText.From(Render(model), Encoding.UTF8));
        });
    }

    private static GenerationResult CreateModel(GeneratorAttributeSyntaxContext ctx)
    {
        var syntax = (StructDeclarationSyntax)ctx.TargetNode;
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        if (!syntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)))
            return new(null, Diagnostic.Create(MustBePartial, syntax.Identifier.GetLocation(), symbol.Name));

        var attribute = ctx.Attributes[0];
        if (attribute.ConstructorArguments.Length != 3 ||
            attribute.ConstructorArguments[0].Value is not INamedTypeSymbol storage ||
            attribute.ConstructorArguments[1].Value is not string minimum ||
            attribute.ConstructorArguments[2].Value is not string maximum)
            return new(null, Diagnostic.Create(InvalidBounds, syntax.Identifier.GetLocation(), "?", "?", "?"));

        var storageName = storage.SpecialType switch
        {
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Byte => "byte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            _ when storage.ToDisplayString() == "System.Int128" => "global::System.Int128",
            _ when storage.ToDisplayString() == "System.UInt128" => "global::System.UInt128",
            _ when storage.ToDisplayString() == "nint" || storage.ToDisplayString() == "System.IntPtr" => "nint",
            _ when storage.ToDisplayString() == "nuint" || storage.ToDisplayString() == "System.UIntPtr" => "nuint",
            _ => null
        };
        if (storageName is null)
            return new(null, Diagnostic.Create(UnsupportedStorage, syntax.Identifier.GetLocation(), storage.ToDisplayString()));

        if (!BigInteger.TryParse(minimum, NumberStyles.Integer, CultureInfo.InvariantCulture, out var min) ||
            !BigInteger.TryParse(maximum, NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) ||
            min > max || !Fits(storageName, min, max))
            return new(null, Diagnostic.Create(InvalidBounds, syntax.Identifier.GetLocation(), minimum, maximum, storage.ToDisplayString()));

        var ns = symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString();
        var access = symbol.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        var readonlyModifier = symbol.IsReadOnly ? "readonly " : string.Empty;
        return new(new DomainModel(ns, symbol.Name, access, readonlyModifier, storageName,
            Literal(storageName, min), Literal(storageName, max), symbol.Name + ".SharpSafeNumerics.g.cs"), null);
    }

    private static bool Fits(string type, BigInteger min, BigInteger max)
    {
        (BigInteger lo, BigInteger hi) = type switch
        {
            "sbyte" => (sbyte.MinValue, sbyte.MaxValue),
            "byte" => (byte.MinValue, byte.MaxValue),
            "short" => (short.MinValue, short.MaxValue),
            "ushort" => (ushort.MinValue, ushort.MaxValue),
            "int" => (int.MinValue, int.MaxValue),
            "uint" => (uint.MinValue, uint.MaxValue),
            "long" => (long.MinValue, long.MaxValue),
            "ulong" => (ulong.MinValue, ulong.MaxValue),
            "global::System.Int128" => (-(BigInteger.One << 127), (BigInteger.One << 127) - 1),
            "global::System.UInt128" => (BigInteger.Zero, (BigInteger.One << 128) - 1),
            "nint" => (long.MinValue, long.MaxValue),
            "nuint" => (BigInteger.Zero, ulong.MaxValue),
            _ => (BigInteger.One, BigInteger.Zero)
        };
        return min >= lo && max <= hi;
    }

    private static string Literal(string type, BigInteger value) => type switch
    {
        "uint" => value.ToString(CultureInfo.InvariantCulture) + "u",
        "long" => value.ToString(CultureInfo.InvariantCulture) + "L",
        "ulong" => value.ToString(CultureInfo.InvariantCulture) + "UL",
        "global::System.Int128" => "(global::System.Int128)" + value.ToString(CultureInfo.InvariantCulture),
        "global::System.UInt128" => "(global::System.UInt128)" + value.ToString(CultureInfo.InvariantCulture),
        "nint" => "(nint)" + value.ToString(CultureInfo.InvariantCulture),
        "nuint" => "(nuint)" + value.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    private static string Render(DomainModel m)
    {
        var nsOpen = m.Namespace is null ? string.Empty : "namespace " + m.Namespace + ";\n\n";
        return $$"""
// <auto-generated/>
#nullable enable
{{nsOpen}}{{m.Accessibility}} {{m.ReadonlyModifier}}partial struct {{m.Name}}
{
    private readonly {{m.StorageType}} __ssnValue;
    private readonly bool __ssnValid;

    private {{m.Name}}({{m.StorageType}} value)
    {
        __ssnValue = value;
        __ssnValid = true;
    }

    public static {{m.StorageType}} Minimum => {{m.MinimumLiteral}};
    public static {{m.StorageType}} Maximum => {{m.MaximumLiteral}};
    public bool IsValid => __ssnValid;
    public {{m.StorageType}} Value => __ssnValid
        ? __ssnValue
        : throw new global::System.InvalidOperationException("Uninitialized bounded numeric value.");

    public bool TryGetValue(out {{m.StorageType}} value)
    {
        value = __ssnValid ? __ssnValue : default;
        return __ssnValid;
    }

    public static global::SharpSafeNumerics.ArithmeticError TryCreate(
        {{m.StorageType}} input, out {{m.Name}} value)
    {
        value = default;
        var error = global::SharpSafeNumerics.SafeInteger.TryInRange(input, Minimum, Maximum, out _);
        if (error != global::SharpSafeNumerics.ArithmeticError.None)
            return error;
        value = new {{m.Name}}(input);
        return global::SharpSafeNumerics.ArithmeticError.None;
    }

    public global::SharpSafeNumerics.ArithmeticError TryAdd(
        {{m.StorageType}} delta, out {{m.Name}} value)
    {
        value = default;
        if (!__ssnValid)
            return global::SharpSafeNumerics.ArithmeticError.Uninitialized;
        var error = global::SharpSafeNumerics.SafeInteger.TryAdd(__ssnValue, delta, out var sum);
        return error == global::SharpSafeNumerics.ArithmeticError.None
            ? TryCreate(sum, out value)
            : error;
    }

    public global::SharpSafeNumerics.ArithmeticError TrySubtract(
        {{m.StorageType}} delta, out {{m.Name}} value)
    {
        value = default;
        if (!__ssnValid)
            return global::SharpSafeNumerics.ArithmeticError.Uninitialized;
        var error = global::SharpSafeNumerics.SafeInteger.TrySubtract(__ssnValue, delta, out var difference);
        return error == global::SharpSafeNumerics.ArithmeticError.None
            ? TryCreate(difference, out value)
            : error;
    }

    public override string ToString() => __ssnValid ? __ssnValue.ToString()! : "<invalid>";
}
""";
    }

    private sealed record DomainModel(
        string? Namespace, string Name, string Accessibility, string ReadonlyModifier,
        string StorageType, string MinimumLiteral, string MaximumLiteral, string HintName);

    private sealed record GenerationResult(DomainModel? Model, Diagnostic? Diagnostic);
}
