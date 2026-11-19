// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;
using SharpSafeNumerics;
using Xunit;

/// <summary>Independent published constants and exact identities challenge elementary rounding.</summary>
public class IeeeFunctionTests
{
    private static Decimal64 D(string value) => Decimal64.Parse(value, CultureInfo.InvariantCulture);

    [Fact]
    public void Decimal64ElementaryConstantsAndDirectedRounding()
    {
        Assert.Equal(D("0.8414709848078965"), IeeeDecimal<Decimal64>.Sin(D("1")).Value);
        Assert.Equal(D("0.8414709848078966"), IeeeDecimal<Decimal64>.Sin(D("1"), new(IeeeDecimalRounding.TowardPositive)).Value);
        Assert.Equal(D("2.718281828459045"), IeeeDecimal<Decimal64>.Exp(D("1")).Value);
        Assert.Equal(D("2.718281828459046"), IeeeDecimal<Decimal64>.Exp(D("1"), new(IeeeDecimalRounding.TowardPositive)).Value);
        Assert.Equal(D("0.6931471805599453"), IeeeDecimal<Decimal64>.Log(D("2")).Value);
        Assert.Equal(IeeeDecimalFlags.Inexact, IeeeDecimal<Decimal64>.Sin(D("1")).Flags);
    }

    [Fact]
    public void ExactIdentitiesDoNotRaiseInexact()
    {
        Assert.Equal(IeeeDecimalFlags.None, IeeeDecimal<Decimal64>.Sin(Decimal64.NegativeZero).Flags);
        Assert.True(Decimal64.IsNegative(IeeeDecimal<Decimal64>.Sin(Decimal64.NegativeZero).Value));
        Assert.Equal(Decimal64.One, IeeeDecimal<Decimal64>.Exp(Decimal64.Zero).Value);
        Assert.Equal(IeeeDecimalFlags.None, IeeeDecimal<Decimal64>.Log(Decimal64.One).Flags);
        Assert.Equal(D("1024"), IeeeDecimal<Decimal64>.Pow(D("2"), D("10")).Value);
        Assert.Equal(IeeeDecimalFlags.None, IeeeDecimal<Decimal64>.Pow(D("2"), D("10")).Flags);
    }

    [Fact]
    public void DomainsAndPolesAreIeeeResults()
    {
        Assert.True(Decimal64.IsNaN(IeeeDecimal<Decimal64>.Log(D("-1")).Value));
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.Log(D("-1")).Flags);
        Assert.True(Decimal64.IsNegativeInfinity(IeeeDecimal<Decimal64>.Log(Decimal64.Zero).Value));
        Assert.Equal(IeeeDecimalFlags.DivisionByZero, IeeeDecimal<Decimal64>.Log(Decimal64.Zero).Flags);
    }

    [Fact]
    public void PiScaledSpecialsAndPowerVariants()
    {
        Assert.False(Decimal64.IsNegative(IeeeDecimal<Decimal64>.SinPi(D("1")).Value));
        Assert.True(Decimal64.IsNegative(IeeeDecimal<Decimal64>.SinPi(D("-1")).Value));
        Assert.True(Decimal64.IsNegative(IeeeDecimal<Decimal64>.TanPi(D("1")).Value));
        Assert.True(Decimal64.IsPositiveInfinity(IeeeDecimal<Decimal64>.TanPi(D("0.5")).Value));
        Assert.Equal(IeeeDecimalFlags.DivisionByZero, IeeeDecimal<Decimal64>.TanPi(D("0.5")).Flags);
        Assert.Equal(D("0.25"), IeeeDecimal<Decimal64>.AtanPi(D("1")).Value);
        Assert.Equal(D("0.5"), IeeeDecimal<Decimal64>.AsinPi(D("1")).Value);
        Assert.Equal(D("-0.75"), IeeeDecimal<Decimal64>.Atan2Pi(D("-1"), D("-1")).Value);
        Assert.Equal(D("2"), IeeeDecimal<Decimal64>.Pow(D("4"), D("0.5")).Value);
        Assert.Equal(IeeeDecimalFlags.None, IeeeDecimal<Decimal64>.Pow(D("4"), D("0.5")).Flags);
        Assert.Equal(D("8"), IeeeDecimal<Decimal64>.PowN(D("2"), 3).Value);
        Assert.True(Decimal64.IsNaN(IeeeDecimal<Decimal64>.PowR(D("-2"), D("2")).Value));
        Assert.Equal(D("1.21"), IeeeDecimal<Decimal64>.Compound(D("0.1"), 2).Value);
    }

    [Fact]
    public void ExactHypotenuseReciprocalRootAndLogPowers()
    {
        Assert.Equal(D("5"), IeeeDecimal<Decimal64>.Hypot(D("3"), D("4")).Value);
        Assert.Equal(IeeeDecimalFlags.None, IeeeDecimal<Decimal64>.Hypot(D("3"), D("4")).Flags);
        Assert.Equal(D("0.5"), IeeeDecimal<Decimal64>.ReciprocalSqrt(D("4")).Value);
        Assert.Equal(D("1"), IeeeDecimal<Decimal64>.Log2P1(D("1")).Value);
        Assert.Equal(D("2"), IeeeDecimal<Decimal64>.Log10P1(D("99")).Value);
        Assert.Equal(D("0.25"), IeeeDecimal<Decimal64>.RootN(D("16"), -2).Value);
    }

    [Fact]
    public void ExtremeExponentialMinusOneRetainsTheSubtraction()
    {
        Assert.Equal(D("-1"), IeeeDecimal<Decimal64>.Exp10M1(D("-30000")).Value);
        Assert.Equal(D("-0.9999999999999999"), IeeeDecimal<Decimal64>.Exp10M1(D("-30000"), new(IeeeDecimalRounding.TowardPositive)).Value);
        Assert.Equal(IeeeDecimalFlags.Inexact, IeeeDecimal<Decimal64>.Exp10M1(D("-30000")).Flags);
        Assert.Equal(D("1"), IeeeDecimal<Decimal64>.TanPi(D("0.25")).Value);
        Assert.Equal(IeeeDecimalFlags.None, IeeeDecimal<Decimal64>.TanPi(D("0.25")).Flags);
    }

    [Fact]
    public void ExactPowerIdentitiesPreservePreferredQuantum()
    {
        Assert.Equal(-4, IeeeDecimal<Decimal64>.Quantum(IeeeDecimal<Decimal64>.Pow(D("1.00"), D("2")).Value).Value);
        Assert.Equal(-4, IeeeDecimal<Decimal64>.Quantum(IeeeDecimal<Decimal64>.PowN(D("-1.00"), 2).Value).Value);
        Assert.Equal(-1, IeeeDecimal<Decimal64>.Quantum(IeeeDecimal<Decimal64>.RootN(D("1.00"), 3).Value).Value);
        Assert.Equal(-4, IeeeDecimal<Decimal64>.Quantum(IeeeDecimal<Decimal64>.Compound(D("0.00"), 2).Value).Value);
        Assert.True(Decimal64.IsNaN(IeeeDecimal<Decimal64>.Compound(D("-2"), 0).Value));
        Assert.False(Decimal64.IsNegative(IeeeDecimal<Decimal64>.RootN(Decimal64.NegativeZero, 2).Value));
        Assert.False(Decimal64.IsNegative(IeeeDecimal<Decimal64>.PowR(Decimal64.NegativeZero, D("-3")).Value));
    }

    [Fact]
    public void CompoundZeroExponentOverridesOnlyQuietNaNs()
    {
        Assert.Equal(Decimal64.One, IeeeDecimal<Decimal64>.Compound(Decimal64.NaN, 0).Value);
        Assert.Equal(IeeeDecimalFlags.None, IeeeDecimal<Decimal64>.Compound(Decimal64.NaN, 0).Flags);
        var signaling = IeeeDecimal<Decimal64>.CreateNaN(7, true).Value;
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.Compound(signaling, 0).Flags);
        Assert.True(Decimal64.IsNegativeInfinity(IeeeDecimal<Decimal64>.ReciprocalSqrt(Decimal64.NegativeZero).Value));
    }

    [Fact]
    public void InfiniteNegativePowerOfZeroDoesNotSignalDivisionByZero()
    {
        CheckInfiniteZeroPower<Decimal32>(); CheckInfiniteZeroPower<Decimal64>(); CheckInfiniteZeroPower<Decimal128>();
    }
    private static void CheckInfiniteZeroPower<T>() where T : struct, IFloatingPointIeee754<T>
    {
        foreach (var zero in new[] { T.Zero, T.NegativeZero })
        foreach (var rounding in Enum.GetValues<IeeeDecimalRounding>())
        foreach (var operation in new Func<T,T,IeeeDecimalContext,IeeeDecimalResult<T>>[] { IeeeDecimal<T>.Pow, IeeeDecimal<T>.PowR })
        {
            var result = operation(zero, T.NegativeInfinity, new(rounding, IeeeDecimalFlags.DivisionByZero));
            Assert.True(result.IsSuccess);
            Assert.True(T.IsPositiveInfinity(result.Value));
            Assert.Equal(IeeeDecimalFlags.None, result.Flags);
        }
    }
}
