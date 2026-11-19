// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using SharpSafeNumerics;
using Xunit;
public class DecimalTests
{
    [Fact] public void AllWidths() { Check<Decimal32>(); Check<Decimal64>(); Check<Decimal128>(); }
    private static void Check<T>() where T : struct, IFloatingPointIeee754<T>
    {
        var one = T.One; var three = T.CreateChecked(3);
        Assert.Equal(ArithmeticError.Inexact, SafeDecimal.TryDivide(one, three).Error);
        var rounded = SafeDecimal.TryDivide(one, three, DecimalRounding.ToEven);
        Assert.True(rounded.IsSuccess); Assert.True(rounded.IsInexact);
        Assert.Equal(T.Zero, SafeDecimal.TrySubtract(one, one).Value);
        Assert.Equal(ArithmeticError.NonFinite, SafeDecimal.TryAdd(T.NaN, one).Error);
        Assert.Equal(ArithmeticError.DivideByZero, SafeDecimal.TryDivide(one, T.Zero).Error);
        Assert.Equal(ArithmeticError.OutOfRange, SafeDecimal.TryAdd(one, one, (DecimalRounding)255).Error);
        Assert.False(default(DecimalResult<T>).TryGetValue(out var cleared)); Assert.Equal(default, cleared);
    }
}
