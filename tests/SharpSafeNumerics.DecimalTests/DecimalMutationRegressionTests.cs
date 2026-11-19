// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;
using SharpSafeNumerics;
using Xunit;

public class DecimalMutationRegressionTests
{
    [Fact] public void ExactFractionalArithmeticAndResultAdmission()
    {
        Check<Decimal32>(); Check<Decimal64>(); Check<Decimal128>();
    }
    private static void Check<T>() where T : struct, IFloatingPointIeee754<T>
    {
        T quarter=T.Parse("0.25",CultureInfo.InvariantCulture);
        T half=T.Parse("0.5",CultureInfo.InvariantCulture);
        T tenth=T.Parse("0.1",CultureInfo.InvariantCulture);
        var mixed=SafeDecimal.TryAdd(tenth,quarter);
        Assert.True(mixed.IsSuccess); Assert.False(mixed.IsInexact);
        Assert.Equal(T.Parse("0.35",CultureInfo.InvariantCulture),mixed.Value);
        var sum=SafeDecimal.TryAdd(quarter,quarter);
        Assert.True(sum.IsSuccess); Assert.False(sum.IsInexact);
        Assert.True(sum.TryGetValue(out var admitted)); Assert.Equal(half,admitted);
        var product=SafeDecimal.TryMultiply(half,half);
        Assert.True(product.IsSuccess); Assert.False(product.IsInexact); Assert.Equal(quarter,product.Value);
        var negation=SafeDecimal.TryNegate(quarter);
        Assert.True(negation.IsSuccess); Assert.False(negation.IsInexact); Assert.Equal(-quarter,negation.Value);
        var roundedNegation=SafeDecimal.TryNegate(half,DecimalRounding.ToEven);
        Assert.False(roundedNegation.IsInexact);
        // A diagnostic must remain useful for direct Value misuse; its exact wording is not contractual.
        var error=Assert.Throws<InvalidOperationException>(()=>default(DecimalResult<T>).Value);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }
}

