using System.Numerics;
using SharpSafeNumerics;
using Xunit;

public class IeeePayloadAndParseTests
{
    [Fact]
    public void PayloadOperationsHaveNoncomputationalIeeeDefaults()
    {
        var ordinary = IeeeDecimal<Decimal32>.GetPayload(Decimal32.One);
        Assert.True(ordinary.IsSuccess); Assert.Equal(-BigInteger.One, ordinary.Value); Assert.Equal(IeeeDecimalFlags.None, ordinary.Flags);
        Assert.Equal(Decimal32.Zero, IeeeDecimal<Decimal32>.SetPayload(Decimal32.NegativeOne).Value);
        Assert.Equal(Decimal32.Zero, IeeeDecimal<Decimal32>.SetPayload(Decimal32.CreateChecked(0.5m)).Value);
        Assert.Equal(Decimal32.Zero, IeeeDecimal<Decimal32>.SetPayload(Decimal32.PositiveInfinity).Value);
        var signaling = IeeeDecimal<Decimal32>.SetPayloadSignaling(Decimal32.CreateChecked(123));
        Assert.True(IeeeDecimal<Decimal32>.IsSignaling(signaling.Value).Value);
        Assert.Equal(new BigInteger(123), IeeeDecimal<Decimal32>.GetPayload(signaling.Value).Value);
        Assert.Equal(IeeeDecimalFlags.None, signaling.Flags);
    }

    [Theory]
    [InlineData("1E2\0")]
    [InlineData("1E2\0\0")]
    [InlineData("1E+")]
    [InlineData("1E- 3")]
    public void ExponentGrammarRejectsNonDigits(string text) => Assert.False(IeeeDecimal<Decimal32>.Parse(text).IsSuccess);

    [Fact]
    public void ArbitrarilyLargeSyntacticallyValidExponentsRoundByValue()
    {
        var overflow = IeeeDecimal<Decimal32>.Parse("1E999999999999999999999999999999999");
        Assert.True(Decimal32.IsPositiveInfinity(overflow.Value));
        Assert.Equal(IeeeDecimalFlags.Overflow | IeeeDecimalFlags.Inexact, overflow.Flags);
        var tiny = IeeeDecimal<Decimal32>.Parse("1E-99999999999999999999999999999999");
        Assert.Equal(Decimal32.Zero, tiny.Value);
        Assert.Equal(IeeeDecimalFlags.Underflow | IeeeDecimalFlags.Inexact, tiny.Flags);
        var zero = IeeeDecimal<Decimal32>.Parse("-0E999999999999999999999999999999");
        Assert.True(Decimal32.IsNegative(zero.Value)); Assert.Equal(IeeeDecimalFlags.None, zero.Flags);
    }
}
