// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Reflection;
using SharpSafeNumerics.Unions;
using Xunit;

namespace SharpSafeNumerics.UnitTests;

/// <summary>Checks published generic contracts that source signature lists do not encode.</summary>
public sealed class ApiConstraintTests
{
    private const GenericParameterAttributes Struct = GenericParameterAttributes.NotNullableValueTypeConstraint |
        GenericParameterAttributes.DefaultConstructorConstraint;

    [Fact]
    public void NumericResult_requires_nonnullable_struct_constraint() =>
        AssertConstraints(typeof(NumericResult<>).GetGenericArguments()[0], Struct, typeof(ValueType));

    [Fact]
    public void Bounded_preserves_integer_and_range_provider_contracts()
    {
        var parameters = typeof(Bounded<,>).GetGenericArguments();
        AssertInteger(parameters[0], minMax: true);
        // Class-based providers are intentionally permitted; do not invent a struct restriction.
        AssertConstraints(parameters[1], GenericParameterAttributes.None,
            typeof(IStaticIntegerRange<>).MakeGenericType(parameters[0]));
    }

    [Fact]
    public void Static_range_requires_a_bounded_binary_integer() =>
        AssertInteger(typeof(IStaticIntegerRange<>).GetGenericArguments()[0], minMax: true);

    [Theory]
    [InlineData(nameof(SafeInteger.TryAdd))]
    [InlineData(nameof(SafeInteger.TrySubtract))]
    [InlineData(nameof(SafeInteger.TryMultiply))]
    [InlineData(nameof(SafeInteger.TryDivide))]
    [InlineData(nameof(SafeInteger.TryNegate))]
    [InlineData(nameof(SafeInteger.TryInRange))]
    [InlineData(nameof(SafeInteger.TryConvert))]
    [InlineData(nameof(SafeInteger.Add))]
    [InlineData(nameof(SafeInteger.Subtract))]
    [InlineData(nameof(SafeInteger.Multiply))]
    public void SafeInteger_preserves_generic_math_contracts(string name)
    {
        var method = Assert.Single(typeof(SafeInteger).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == name && method.IsGenericMethodDefinition);
        var parameters = method.GetGenericArguments();
        Assert.Equal(name == nameof(SafeInteger.TryConvert) ? 2 : 1, parameters.Length);
        foreach (var parameter in parameters) AssertInteger(parameter, minMax: name != nameof(SafeInteger.TryConvert));
    }

    [Theory]
    [InlineData(typeof(NumericSuccess<>))]
    [InlineData(typeof(ArithmeticOutcome<>))]
    public void Union_values_retain_struct_constraints(Type definition) =>
        AssertConstraints(definition.GetGenericArguments()[0], Struct, typeof(ValueType));

    [Fact]
    public void Union_adapter_preserves_result_constraint() =>
        AssertConstraints(typeof(ArithmeticOutcome).GetMethod(nameof(ArithmeticOutcome.FromResult))!.GetGenericArguments()[0],
            Struct, typeof(ValueType));

    private static void AssertInteger(Type parameter, bool minMax)
    {
        var constraints = new List<Type> { typeof(ValueType), typeof(IBinaryInteger<>).MakeGenericType(parameter) };
        if (minMax) constraints.Add(typeof(IMinMaxValue<>).MakeGenericType(parameter));
        AssertConstraints(parameter, Struct, constraints.ToArray());
    }

    private static void AssertConstraints(Type parameter, GenericParameterAttributes flags, params Type[] constraints)
    {
        // Reflection is verification-only. Runtime libraries still use no reflection.
        Assert.Equal(flags, parameter.GenericParameterAttributes);
        Assert.Equal(constraints.OrderBy(type => type.ToString()),
            parameter.GetGenericParameterConstraints().OrderBy(type => type.ToString()));
        // An unmanaged marker strengthens a struct contract without changing its flags.
        Assert.DoesNotContain(parameter.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.FullName == "System.Runtime.CompilerServices.IsUnmanagedAttribute");
    }
}
