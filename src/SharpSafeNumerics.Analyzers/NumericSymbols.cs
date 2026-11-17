using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace SharpSafeNumerics.Analyzers;

/// <summary>Resolves and classifies numeric contracts once per consumer compilation.</summary>
internal sealed class NumericSymbols
{
    private const string CoreAssemblyName = "SharpSafeNumerics.Core";
    private const string DecimalAssemblyName = "SharpSafeNumerics.Decimal";
    private const string GeneratorName = "SharpSafeNumerics.Generator";
    private readonly CSharpCompilation compilation;
    private readonly INamedTypeSymbol? bounded;
    private readonly INamedTypeSymbol? policy;
    private readonly INamedTypeSymbol? defaultPolicy;
    private readonly INamedTypeSymbol? int128;
    private readonly INamedTypeSymbol? uint128;
    private readonly INamedTypeSymbol? numberBase;
    private readonly INamedTypeSymbol? generatedCode;
    private readonly INamedTypeSymbol? unsafeMethods;
    private readonly INamedTypeSymbol? memoryMarshal;
    private readonly INamedTypeSymbol? formattableString;
    private readonly INamedTypeSymbol? iFormattable;
    private readonly INamedTypeSymbol? decimalResult;
    private readonly INamedTypeSymbol? safeDecimal;
    private readonly object? throwValue;
    private readonly object? zeroValidValue;

    internal NumericSymbols(Compilation compilation)
    {
        this.compilation = (CSharpCompilation)compilation;
        // Compilation-wide metadata name lookups exclude references available only through
        // extern aliases. Resolve contracts from the Core assembly, then compare exact symbols.
        var core = compilation.Assembly.Name == CoreAssemblyName ? compilation.Assembly :
            compilation.SourceModule.ReferencedAssemblySymbols.FirstOrDefault(assembly => assembly.Name == CoreAssemblyName);
        Strict = core?.GetTypeByMetadataName("SharpSafeNumerics.StrictNumericAttribute");
        Result = core?.GetTypeByMetadataName("SharpSafeNumerics.NumericResult`1");
        var decimals = compilation.Assembly.Name == DecimalAssemblyName ? compilation.Assembly :
            compilation.SourceModule.ReferencedAssemblySymbols.FirstOrDefault(assembly => assembly.Name == DecimalAssemblyName);
        decimalResult = decimals?.GetTypeByMetadataName("SharpSafeNumerics.DecimalResult`1");
        safeDecimal = decimals?.GetTypeByMetadataName("SharpSafeNumerics.SafeDecimal");
        bounded = core?.GetTypeByMetadataName("SharpSafeNumerics.BoundedIntegerAttribute");
        policy = core?.GetTypeByMetadataName("SharpSafeNumerics.ArithmeticPolicy");
        defaultPolicy = core?.GetTypeByMetadataName("SharpSafeNumerics.DefaultValuePolicy");
        int128 = compilation.GetTypeByMetadataName("System.Int128");
        uint128 = compilation.GetTypeByMetadataName("System.UInt128");
        numberBase = compilation.GetTypeByMetadataName("System.Numerics.INumberBase`1");
        generatedCode = compilation.GetTypeByMetadataName("System.CodeDom.Compiler.GeneratedCodeAttribute");
        unsafeMethods = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.Unsafe");
        memoryMarshal = compilation.GetTypeByMetadataName("System.Runtime.InteropServices.MemoryMarshal");
        formattableString = compilation.GetTypeByMetadataName("System.FormattableString");
        iFormattable = compilation.GetTypeByMetadataName("System.IFormattable");
        throwValue = ConstantField(policy, "Throw");
        zeroValidValue = ConstantField(defaultPolicy, "ZeroValid");
    }

    internal INamedTypeSymbol? Strict { get; }
    internal INamedTypeSymbol? Result { get; }

    internal bool IsStrict(ISymbol? symbol)
    {
        // Containment is lexical, so nested types and functions inherit this scope without
        // imposing it on callers, derived types, or unrelated overloads.
        for (; symbol is not null && symbol.Kind is not (SymbolKind.Namespace or SymbolKind.Assembly);
             symbol = symbol.ContainingSymbol)
        {
            if (Attribute(symbol, Strict) is not null)
                return true;
        }

        return false;
    }

    internal bool HasNumericResult => Result is not null || decimalResult is not null;

    internal bool IsNumericResult(ITypeSymbol? type) =>
        Matches(Result, UnwrapNullable(type)?.OriginalDefinition) ||
        Matches(decimalResult, UnwrapNullable(type)?.OriginalDefinition);

    internal bool IsAllocatingDecimalHelper(IMethodSymbol method) =>
        // Exactness checks format values and construct BigInteger rationals. Negation has
        // neither path, so it deliberately remains outside this bounded known-API warning.
        Matches(safeDecimal, method.ContainingType) && method.IsStatic &&
        method.Name is "TryAdd" or "TrySubtract" or "TryMultiply" or "TryDivide" or "TryConvert";

    internal bool IsThrowConstant(IOperation operation)
    {
        if (throwValue is null || !Matches(policy, UnwrapNullable(operation.Type)))
            return false;

        // Default arguments of nullable enum type can wrap an enum constant in a conversion
        // whose own constant value is absent.
        while (operation is IConversionOperation { Conversion.IsNullable: true } lift &&
               !operation.ConstantValue.HasValue)
        {
            operation = lift.Operand;
        }
        return operation.ConstantValue.HasValue && Equals(throwValue, operation.ConstantValue.Value);
    }

    internal bool IsRawNumericConversion(IMethodSymbol method)
    {
        if (numberBase is null || !method.IsStatic || method.Name is not ("CreateSaturating" or "CreateTruncating"))
            return false;
        if (Matches(numberBase, method.ContainingType.OriginalDefinition))
            return true;

        // Match an actual static generic-math implementation, not an arbitrary same-named method.
        var type = method.ContainingType;
        foreach (var implemented in type.AllInterfaces)
        {
            if (!Matches(numberBase, implemented.OriginalDefinition))
                continue;
            foreach (var member in implemented.GetMembers(method.Name))
            {
                if (type.FindImplementationForInterfaceMember(member) is IMethodSymbol implementation &&
                    Matches(implementation.OriginalDefinition, method.OriginalDefinition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal bool IsBounded(ITypeSymbol? type) =>
        type is { TypeKind: TypeKind.Struct } && Attribute(type, bounded) is not null;

    internal bool IsBoundedGeneratedMember(ISymbol member) => IsBounded(member.ContainingType) &&
        Attribute(member, generatedCode) is { ConstructorArguments.Length: 2 } marker &&
        marker.ConstructorArguments[0].Value is GeneratorName;

    internal bool HasThrowOverflowPolicy(ITypeSymbol type) =>
        HasEnumOption(type, "OverflowPolicy", policy, throwValue);

    internal bool IsZeroValid(ITypeSymbol? type) => HasEnumOption(type, "DefaultPolicy", defaultPolicy, zeroValidValue);

    internal bool IsBoundedReinterpretation(IMethodSymbol method)
    {
        var types = method.TypeArguments;
        if (types.Length == 2 && Matches(types[0], types[1]))
            return false;

        // Reinterpreting a bounded type to or from another representation can expose raw
        // storage, including a mutable reference that no longer enforces the generated domain.
        return Matches(unsafeMethods, method.ContainingType) &&
            method.Name is "As" or "BitCast" && types.Length == 2 && types.Any(HasBoundedPayload) ||
            Matches(memoryMarshal, method.ContainingType) &&
            method.Name is "AsBytes" or "AsRef" or "Cast" or "Read" or "TryRead" && types.Any(HasBoundedPayload);
    }

    private bool HasBoundedPayload(ITypeSymbol type) => IsBounded(UnwrapNullable(type));

    internal bool IsFormattingObject(ITypeSymbol? type) =>
        Matches(formattableString, type) || Matches(iFormattable, type);

    internal bool IsInteger(ITypeSymbol? type) => IntegerRange(UnwrapNullable(type), 32) is not null;

    internal bool IsBuiltinIntegerOperator(IMethodSymbol? method) => method is null ||
        Matches(int128, method.ContainingType) || Matches(uint128, method.ContainingType) ||
        method.ContainingType.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr;

    internal bool IsNarrowing(IConversionOperation conversion)
    {
        return !conversion.IsImplicit && conversion.Conversion.Exists &&
            IsBuiltinIntegerOperator(conversion.OperatorMethod) &&
            IsNarrowing(conversion.Operand.Type, conversion.Type, conversion.Operand);
    }

    private bool IsNarrowing(ITypeSymbol? sourceType, ITypeSymbol? targetType, IOperation? operand)
    {
        var source = UnwrapNullable(sourceType);
        var target = UnwrapNullable(targetType);
        if (Matches(source, target) ||
            (operand is not null && !Matches(targetType, target) && IsKnownNull(operand)))
        {
            return false;
        }

        if (source is INamedTypeSymbol { IsTupleType: true } from &&
            target is INamedTypeSymbol { IsTupleType: true } to &&
            from.TupleElements.Length == to.TupleElements.Length)
        {
            // The compiler represents a cast between tuple values as one conversion. Each
            // element still has its own numeric range, and literal operands retain constants.
            var literal = operand as ITupleOperation;
            for (int i = 0; i < from.TupleElements.Length; i++)
            {
                if (IsNarrowing(from.TupleElements[i].Type, to.TupleElements[i].Type, literal?.Elements[i]))
                    return true;
            }
            return false;
        }

        // Use exact compile-time constants when available. Native-width ranges must be safe at
        // both 32 and 64 bits, irrespective of the architecture hosting the analyzer.
        BigInteger? value = operand?.ConstantValue is { HasValue: true, Value: var constant }
            ? IntegerConstant(constant)
            : null;
        return Narrows(source, target, value, 32) || Narrows(source, target, value, 64);
    }

    internal bool IsBoxing(IConversionOperation conversion)
    {
        // Classify the bound conversion from its types to distinguish real boxing from a
        // reference conversion. A statically null Nullable<T> has nothing to box.
        var source = conversion.Operand.Type;
        var target = conversion.Type;
        return conversion.Conversion.Exists &&
            !conversion.Conversion.IsUserDefined &&
            source?.IsValueType == true && target?.IsReferenceType == true &&
            !IsKnownNull(conversion.Operand) && compilation.ClassifyConversion(source, target).IsBoxing;
    }

    private static bool IsKnownNull(IOperation operation) =>
        operation.ConstantValue is { HasValue: true, Value: null } ||
        operation is IDefaultValueOperation && !Matches(operation.Type, UnwrapNullable(operation.Type)) ||
        operation is IConversionOperation { Conversion.IsUserDefined: false } conversion &&
        !Matches(conversion.Type, UnwrapNullable(conversion.Type)) && IsKnownNull(conversion.Operand);

    internal static ITypeSymbol? UnwrapNullable(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? named.TypeArguments[0] : type;

    private bool HasEnumOption(ITypeSymbol? type, string option, INamedTypeSymbol? optionType, object? expected)
    {
        if (expected is null || type is null || Attribute(type, bounded) is not { } declaration)
            return false;
        return declaration.NamedArguments.Any(argument => argument.Key == option &&
            Matches(optionType, argument.Value.Type) && Equals(expected, argument.Value.Value));
    }

    private bool Narrows(ITypeSymbol? source, ITypeSymbol? target, BigInteger? constant, int nativeBits)
    {
        if (IntegerRange(source, nativeBits) is not { } sourceRange ||
            IntegerRange(target, nativeBits) is not { } targetRange)
        {
            return false;
        }

        return constant.HasValue
            ? constant.Value < targetRange.Minimum || constant.Value > targetRange.Maximum
            : sourceRange.Minimum < targetRange.Minimum || sourceRange.Maximum > targetRange.Maximum;
    }

    private (BigInteger Minimum, BigInteger Maximum)? IntegerRange(ITypeSymbol? type, int nativeBits)
    {
        (int bits, bool signed) = type?.SpecialType switch
        {
            SpecialType.System_SByte => (8, true),
            SpecialType.System_Byte => (8, false),
            SpecialType.System_Int16 => (16, true),
            SpecialType.System_UInt16 or SpecialType.System_Char => (16, false),
            SpecialType.System_Int32 => (32, true),
            SpecialType.System_UInt32 => (32, false),
            SpecialType.System_Int64 => (64, true),
            SpecialType.System_UInt64 => (64, false),
            SpecialType.System_IntPtr => (nativeBits, true),
            SpecialType.System_UIntPtr => (nativeBits, false),
            _ when Matches(int128, type) => (128, true),
            _ when Matches(uint128, type) => (128, false),
            _ => (0, false)
        };

        if (bits == 0)
            return null;
        BigInteger limit = BigInteger.One << (signed ? bits - 1 : bits);
        return (signed ? -limit : BigInteger.Zero, limit - BigInteger.One);
    }

    private static BigInteger? IntegerConstant(object? value) => value switch
    {
        sbyte or byte or short or ushort or int or uint or long => Convert.ToInt64(value, CultureInfo.InvariantCulture),
        ulong unsigned => unsigned,
        char character => (ushort)character,
        _ => null
    };

    private static AttributeData? Attribute(ISymbol symbol, INamedTypeSymbol? type) => type is null ? null :
        symbol.GetAttributes().FirstOrDefault(attribute => Matches(type, attribute.AttributeClass));

    private static object? ConstantField(INamedTypeSymbol? type, string name) =>
        type?.GetMembers(name).OfType<IFieldSymbol>().FirstOrDefault()?.ConstantValue;

    private static bool Matches(ISymbol? expected, ISymbol? actual) =>
        expected is not null && SymbolEqualityComparer.Default.Equals(expected, actual);
}
