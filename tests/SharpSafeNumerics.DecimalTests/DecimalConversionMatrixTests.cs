// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;
using SharpSafeNumerics;
using Xunit;

public class DecimalConversionMatrixTests
{
    [Fact] public void AllNineSourceDestinationPairs()
    {
        Source<Decimal32>(); Source<Decimal64>(); Source<Decimal128>();
    }
    private static void Source<T>() where T : struct, IFloatingPointIeee754<T>
    {
        Pair<T,Decimal32>(); Pair<T,Decimal64>(); Pair<T,Decimal128>();
    }
    private static void Pair<S,D>() where S : struct, IFloatingPointIeee754<S> where D : struct, IFloatingPointIeee754<D>
    {
        var exact=S.Parse("-123.5",CultureInfo.InvariantCulture);
        Assert.Equal(D.Parse("-123.5",CultureInfo.InvariantCulture),SafeDecimal.TryConvert<S,D>(exact).Value);
        Assert.False(SafeDecimal.TryConvert<S,D>(exact).IsInexact);
        Assert.True(D.IsNegative(SafeDecimal.TryConvert<S,D>(-S.Zero).Value));
        Assert.Equal(ArithmeticError.NonFinite,SafeDecimal.TryConvert<S,D>(S.NegativeInfinity).Error);
        for(int i=2;i<=255;i++) Assert.Equal(ArithmeticError.OutOfRange,SafeDecimal.TryConvert<S,D>(S.Zero,(DecimalRounding)i).Error);
    }
    [Fact] public void TinyNegativeAndCancellation()
    {
        Tiny<Decimal32>(); Tiny<Decimal64>(); Tiny<Decimal128>();
    }
    private static void Tiny<T>() where T : struct, IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        var tiny=-T.BitIncrement(T.Zero);
        var result=SafeDecimal.TryDivide(tiny,T.CreateChecked(2),DecimalRounding.ToEven);
        Assert.True(result.IsInexact); Assert.True(T.IsNegative(result.Value)); Assert.True(T.IsZero(result.Value));
        Assert.Equal(T.Zero,SafeDecimal.TryAdd(T.MaxValue,-T.MaxValue).Value);
        // Exact arithmetic must detect a lost subnormal even when a reverse add/subtract could appear stable.
        Assert.Equal(ArithmeticError.Inexact,SafeDecimal.TryAdd(T.One,tiny).Error);
        var rounded=SafeDecimal.TryAdd(T.One,tiny,DecimalRounding.ToEven);
        Assert.True(rounded.IsInexact); Assert.Equal(T.One,rounded.Value);
    }
}
