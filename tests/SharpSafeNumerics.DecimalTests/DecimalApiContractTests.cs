// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Reflection;
using SharpSafeNumerics;
using Xunit;

/// <summary>Guards generic metadata omitted from source-signature API lists.</summary>
public sealed class DecimalApiContractTests
{
    [Fact]
    public void ResultRetainsStructConstraint()
    {
        var parameter = typeof(DecimalResult<>).GetGenericArguments()[0];
        AssertFlags(parameter);
        Assert.Equal(new[] { typeof(ValueType) }, parameter.GetGenericParameterConstraints());
    }

    [Fact]
    public void PublicOperationsRetainFloatingPointConstraints()
    {
        foreach (string name in new[] { "TryAdd", "TrySubtract", "TryMultiply", "TryDivide", "TryNegate", "TryConvert" })
        {
            var method = Assert.Single(typeof(SafeDecimal).GetMethods(BindingFlags.Static | BindingFlags.Public), m => m.Name == name);
            var parameters = method.GetGenericArguments();
            Assert.Equal(name == "TryConvert" ? 2 : 1, parameters.Length);
            foreach (var parameter in parameters)
            {
                AssertFlags(parameter);
                var expected = new[] { typeof(ValueType), typeof(IFloatingPointIeee754<>).MakeGenericType(parameter) };
                Assert.Equal(expected.OrderBy(t => t.ToString()), parameter.GetGenericParameterConstraints().OrderBy(t => t.ToString()));
            }
        }
    }

    private static void AssertFlags(Type parameter)
    {
        // Reflection is test-only; the shipping decimal wrapper uses no reflection.
        Assert.Equal(GenericParameterAttributes.NotNullableValueTypeConstraint | GenericParameterAttributes.DefaultConstructorConstraint,
            parameter.GenericParameterAttributes);
        Assert.DoesNotContain(parameter.GetCustomAttributesData(), a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IsUnmanagedAttribute");
    }
}
