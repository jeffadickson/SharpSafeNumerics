using System.Globalization;
using System.Numerics;
using Xunit;

namespace SharpSafeNumerics.DecimalTests;

/// <summary>Public-boundary regressions for payload kind, poles and trapped conversions.</summary>
public sealed class IeeeRepresentationEdgeTests
{
    [Fact]
    public void QuietPayloadSettersPreserveTheirDistinctKindAndValidateContext()
    {
        Payloads<Decimal32>(); Payloads<Decimal64>(); Payloads<Decimal128>();
    }

    private static void Payloads<T>() where T : struct, IFloatingPointIeee754<T>
    {
        T payload = T.CreateChecked(123);
        foreach (var result in new[]
        {
            IeeeDecimal<T>.SetPayload(payload), IeeeDecimal<T>.SetPayload(payload, default),
            IeeeDecimal<T>.SetPayload(123), IeeeDecimal<T>.SetPayload(123, default)
        })
        {
            Assert.True(T.IsNaN(result.Value));
            Assert.False(IeeeDecimal<T>.IsSignaling(result.Value).Value);
            Assert.Equal(new BigInteger(123), IeeeDecimal<T>.GetPayload(result.Value).Value);
            Assert.Equal(IeeeDecimalFlags.None, result.Flags);
        }
        var invalid = new IeeeDecimalContext((IeeeDecimalRounding)99);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<T>.SetPayload(123, invalid).Error);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<T>.SetPayloadSignaling(123, invalid).Error);
    }

    [Fact]
    public void ZeroLogarithmExponentIsAPoleAndInfiniteAdjacentValuesStayInfinite()
    {
        Specials<Decimal32>(); Specials<Decimal64>(); Specials<Decimal128>();
    }

    private static void Specials<T>() where T : struct, IFloatingPointIeee754<T>
    {
        foreach (T zero in new[] { T.Zero, T.NegativeZero })
        {
            var pole = IeeeDecimal<T>.LogB(zero);
            Assert.True(T.IsNegativeInfinity(pole.Value));
            Assert.Equal(IeeeDecimalFlags.DivisionByZero, pole.Flags);
            var trapped = IeeeDecimal<T>.LogB(zero, new(Traps: IeeeDecimalFlags.DivisionByZero));
            Assert.Equal(ArithmeticError.DivideByZero, trapped.Error);
        }
        Assert.True(T.IsPositiveInfinity(IeeeDecimal<T>.NextUp(T.PositiveInfinity).Value));
        Assert.True(T.IsNegativeInfinity(IeeeDecimal<T>.NextDown(T.NegativeInfinity).Value));
        Assert.True(T.IsPositiveInfinity(IeeeDecimal<T>.RoundToIntegral(T.PositiveInfinity).Value));
    }

    [Fact]
    public void ExtremaPropagateQuietNaNsAndCompareNumericalMagnitudesAcrossCohorts()
    {
        Extrema<Decimal32>(); Extrema<Decimal64>(); Extrema<Decimal128>();
    }

    private static void Extrema<T>() where T : struct, IFloatingPointIeee754<T>
    {
        T nan = IeeeDecimal<T>.CreateNaN(123).Value;
        foreach (var result in new[]
        {
            IeeeDecimal<T>.Maximum(nan, T.One), IeeeDecimal<T>.Maximum(T.One, nan),
            IeeeDecimal<T>.MinimumMagnitude(nan, T.One), IeeeDecimal<T>.MinimumMagnitude(T.One, nan),
            IeeeDecimal<T>.MaximumMagnitude(nan, T.One), IeeeDecimal<T>.MaximumMagnitude(T.One, nan)
        })
        {
            Assert.True(T.IsNaN(result.Value));
            Assert.Equal(new BigInteger(123), IeeeDecimal<T>.GetPayload(result.Value).Value);
            Assert.Equal(IeeeDecimalFlags.None, result.Flags);
        }
        T fifteen = T.Parse("15", CultureInfo.InvariantCulture), twenty = T.Parse("2E1", CultureInfo.InvariantCulture);
        Assert.Equal(fifteen, IeeeDecimal<T>.MinimumMagnitude(fifteen, twenty).Value);
        Assert.Equal(twenty, IeeeDecimal<T>.MaximumMagnitude(fifteen, twenty).Value);
    }

    [Fact]
    public void BinaryConversionTrapsPublishTheDocumentedErrorAndOriginalFlags()
    {
        // These independently known binary16 cases distinguish an overflow/
        // underflow trap from the accompanying Inexact status and its priority.
        foreach (var entry in new[]
        {
            ("1E10", IeeeDecimalFlags.Overflow, ArithmeticError.Overflow),
            ("1E-100", IeeeDecimalFlags.Underflow, ArithmeticError.Underflow),
            ("0.1", IeeeDecimalFlags.Inexact, ArithmeticError.Inexact)
        })
        {
            var input = Decimal128.Parse(entry.Item1, CultureInfo.InvariantCulture);
            var result = IeeeDecimal<Decimal128>.ToBinary<Half>(input, new(Traps: entry.Item2));
            Assert.False(result.IsSuccess);
            Assert.Equal(entry.Item3, result.Error);
            Assert.True(result.Flags.HasFlag(entry.Item2));
            Assert.True(result.Flags.HasFlag(IeeeDecimalFlags.Inexact));
            Assert.False(result.TryGetValue(out _));
        }
    }
}
