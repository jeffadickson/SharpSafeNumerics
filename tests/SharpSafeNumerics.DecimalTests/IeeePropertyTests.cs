// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;
using CsCheck;
using PeterO.Numbers;
using SharpSafeNumerics;
using Xunit;

/// <summary>Reproducible shrinking properties use an independently implemented arbitrary-precision oracle.</summary>
public class IeeePropertyTests
{
    [Fact]
    public void ElementaryFunctionsMatchHighPrecisionReference()
    {
        Check<Decimal32>(7); Check<Decimal64>(16); Check<Decimal128>(34);
    }
    private static void Check<T>(int precision) where T : struct, IFloatingPointIeee754<T>
    {
        string seed = new PCG(1, 7542030).ToString();
        Gen.Int.Sample(i =>
        {
            string text = (1m + Math.Abs(i % 1000) / 1000m).ToString(CultureInfo.InvariantCulture);
            T x = T.Parse(text, CultureInfo.InvariantCulture);
            var reference = EDecimal.FromString(text);
            var high = EContext.ForPrecision(120).WithRounding(ERounding.HalfEven);
            var exponential = reference.Exp(high); var logarithm = reference.Log(high);
            for (int mode = 0; mode < 5; mode++)
            {
                var rounding = mode switch { 0 => ERounding.HalfEven, 1 => ERounding.HalfUp, 2 => ERounding.Down, 3 => ERounding.Floor, _ => ERounding.Ceiling };
                var target = EContext.ForPrecision(precision).WithRounding(rounding);
                T expectedExp = T.Parse(exponential.RoundToPrecision(target).ToString(), CultureInfo.InvariantCulture);
                T expectedLog = T.Parse(logarithm.RoundToPrecision(target).ToString(), CultureInfo.InvariantCulture);
                if (IeeeDecimal<T>.Exp(x, new((IeeeDecimalRounding)mode)).Value != expectedExp || IeeeDecimal<T>.Log(x, new((IeeeDecimalRounding)mode)).Value != expectedLog) return false;
            }
            return true;
        }, seed: seed, iter: 64, threads: 1);
    }

    [Fact]
    public void MalformedTextIsRejectedWithoutExceptionsOrValues()
    {
        string seed = new PCG(1, 7542031).ToString();
        Gen.String.Sample(text =>
        {
            var result = IeeeDecimal<Decimal128>.Parse("#" + text);
            return !result.TryGetValue(out var value) && value == default && result.Flags == IeeeDecimalFlags.None;
        }, seed: seed, iter: 1000, threads: 1);
    }

    [Fact]
    public void ContextValidationPrecedesNaNsAndDomainErrors()
    {
        foreach (var context in new[] { new IeeeDecimalContext((IeeeDecimalRounding)255), new IeeeDecimalContext(Traps: (IeeeDecimalFlags)128) })
        {
            Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<Decimal64>.Log(Decimal64.NaN, context).Error);
            Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<Decimal64>.Pow(Decimal64.NaN, Decimal64.Zero, context).Error);
            Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<Decimal64>.QuietEqual(Decimal64.NaN, Decimal64.Zero, context).Error);
        }
    }
}
