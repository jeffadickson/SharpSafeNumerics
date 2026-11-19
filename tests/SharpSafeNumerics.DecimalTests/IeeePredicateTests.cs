// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using SharpSafeNumerics;
using Xunit;

public class IeeePredicateTests
{
    [Fact]
    public void QuietAndSignalingPredicatesSeparateFlagsFromUnorderedOutcome()
    {
        var quiet = IeeeDecimal<Decimal64>.QuietNotEqual(Decimal64.NaN, Decimal64.One);
        Assert.True(quiet.Value); Assert.Equal(IeeeDecimalFlags.None, quiet.Flags);
        Assert.False(IeeeDecimal<Decimal64>.QuietEqual(Decimal64.NaN, Decimal64.One).Value);
        Assert.True(IeeeDecimal<Decimal64>.QuietUnordered(Decimal64.NaN, Decimal64.One).Value);
        Assert.False(IeeeDecimal<Decimal64>.QuietOrdered(Decimal64.NaN, Decimal64.One).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.SignalingEqual(Decimal64.NaN, Decimal64.One).Flags);
        Assert.True(IeeeDecimal<Decimal64>.QuietEqual(Decimal64.NegativeZero, Decimal64.Zero).Value);
    }

    [Fact]
    public void ClassificationAndLogBObserveDecimalRadix()
    {
        Assert.Equal(10, IeeeDecimal<Decimal64>.Radix().Value);
        Assert.True(IeeeDecimal<Decimal64>.IsZero(Decimal64.NegativeZero).Value);
        Assert.True(IeeeDecimal<Decimal64>.IsNaN(Decimal64.NaN).Value);
        Assert.True(IeeeDecimal<Decimal64>.IsFinite(Decimal64.One).Value);
        Assert.Equal(Decimal64.CreateChecked(2), IeeeDecimal<Decimal64>.LogB(Decimal64.CreateChecked(123)).Value);
        Assert.Equal(IeeeDecimalFlags.DivisionByZero, IeeeDecimal<Decimal64>.LogB(Decimal64.Zero).Flags);
    }
}
