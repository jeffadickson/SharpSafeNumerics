using System.Globalization;
using System.Numerics;
using PeterO.Numbers;
using SharpSafeNumerics;
using Xunit;

public class DecimalBoundaryTests
{
    [Fact] public void BoundariesAllWidths() { Boundaries<Decimal32>(); Boundaries<Decimal64>(); Boundaries<Decimal128>(); }
    private static void Boundaries<T>() where T : struct, IFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        Assert.Equal(ArithmeticError.Overflow, SafeDecimal.TryAdd(T.MaxValue, T.MaxValue).Error);
        Assert.Equal(ArithmeticError.Underflow, SafeDecimal.TryAdd(T.MinValue, T.MinValue).Error);
        Assert.Equal(ArithmeticError.NonFinite, SafeDecimal.TryNegate(T.PositiveInfinity).Error);
        Assert.Equal(ArithmeticError.NonFinite, SafeDecimal.TryMultiply(T.NegativeInfinity, T.Zero).Error);
        var tiny = T.BitIncrement(T.Zero);
        Assert.Equal(tiny, SafeDecimal.TryAdd(tiny, T.Zero).Value);
        Assert.Equal(ArithmeticError.Inexact, SafeDecimal.TryDivide(tiny, T.CreateChecked(2)).Error);
        var roundedTiny = SafeDecimal.TryDivide(tiny, T.CreateChecked(2), DecimalRounding.ToEven);
        Assert.True(roundedTiny.IsInexact); Assert.Equal(T.Zero, roundedTiny.Value);
        Assert.True(T.IsNegative(SafeDecimal.TryNegate(T.Zero).Value));
        Assert.Equal(T.One, SafeDecimal.TryMultiply(T.One, T.One).Value);
        Assert.Equal(-T.One, SafeDecimal.TryNegate(T.One).Value);
        var failed = SafeDecimal.TryAdd(T.NaN, T.One); Assert.False(failed.TryGetValue(out var cleared)); Assert.Equal(default, cleared); Assert.False(failed.IsInexact);
        Assert.Throws<InvalidOperationException>(() => failed.Value);
        Assert.Equal(ArithmeticError.OutOfRange, SafeDecimal.TryDivide(T.One,T.Zero,(DecimalRounding)123).Error);
        Assert.Equal(ArithmeticError.OutOfRange, SafeDecimal.TryNegate(T.NaN,(DecimalRounding)123).Error);
        Assert.Equal(ArithmeticError.OutOfRange, SafeDecimal.TryConvert<T,T>(T.NaN,(DecimalRounding)123).Error);
    }
    [Fact] public void UnsupportedTypesAndConversions()
    {
        Assert.Equal(ArithmeticError.OutOfRange, SafeDecimal.TryAdd(1.0, 2.0).Error);
        Assert.Equal(ArithmeticError.OutOfRange, SafeDecimal.TryConvert<double,Decimal32>(1.0).Error);
        Assert.Equal(ArithmeticError.OutOfRange, SafeDecimal.TryConvert<Decimal32,double>(Decimal32.One).Error);
        Assert.Equal(ArithmeticError.NonFinite, SafeDecimal.TryConvert<Decimal128,Decimal32>(Decimal128.NaN).Error);
        Assert.Equal(Decimal128.One, SafeDecimal.TryConvert<Decimal32,Decimal128>(Decimal32.One).Value);
        Assert.Equal(ArithmeticError.Overflow, SafeDecimal.TryConvert<Decimal128,Decimal32>(Decimal128.MaxValue).Error);
        Assert.Equal(ArithmeticError.Underflow, SafeDecimal.TryConvert<Decimal128,Decimal32>(Decimal128.MinValue).Error);
        var value = Decimal128.Parse("1.23456789",CultureInfo.InvariantCulture);
        Assert.Equal(ArithmeticError.Inexact, SafeDecimal.TryConvert<Decimal128,Decimal32>(value).Error);
        var rounded = SafeDecimal.TryConvert<Decimal128,Decimal32>(value, DecimalRounding.ToEven);
        Assert.Equal(Decimal32.Parse("1.234568",CultureInfo.InvariantCulture),rounded.Value); Assert.True(rounded.IsInexact);
    }
    [Fact] public void IndependentPeterODecimalContextsSeed731927()
    {
        Oracle<Decimal32>(EContext.Decimal32); Oracle<Decimal64>(EContext.Decimal64); Oracle<Decimal128>(EContext.Decimal128);
    }
    private static void Oracle<T>(EContext context) where T : struct, IFloatingPointIeee754<T>
    {
        var random = new Random(731927);
        for(int i=0;i<120;i++)
        {
            int a=random.Next(-999999,1000000), b=random.Next(1,1000000);
            T x=T.CreateChecked(a),y=T.CreateChecked(b);
            var p=EDecimal.FromInt32(a); var q=EDecimal.FromInt32(b);
            Assert.Equal(T.Parse(p.Add(q,context).ToString(),CultureInfo.InvariantCulture),SafeDecimal.TryAdd(x,y,DecimalRounding.ToEven).Value);
            Assert.Equal(T.Parse(p.Subtract(q,context).ToString(),CultureInfo.InvariantCulture),SafeDecimal.TrySubtract(x,y,DecimalRounding.ToEven).Value);
            Assert.Equal(T.Parse(p.Multiply(q,context).ToString(),CultureInfo.InvariantCulture),SafeDecimal.TryMultiply(x,y,DecimalRounding.ToEven).Value);
            var expected=p.Divide(q,context);
            var result=SafeDecimal.TryDivide(x,y,DecimalRounding.ToEven);
            Assert.Equal(T.Parse(expected.ToString(),CultureInfo.InvariantCulture),result.Value);
            // Independent arbitrary-precision products establish exactness even for repeating quotients.
            bool exact=EDecimal.FromString(result.Value.ToString("R",CultureInfo.InvariantCulture)).Multiply(q).CompareTo(p)==0;
            Assert.Equal(!exact,result.IsInexact);
            Assert.Equal(exact?ArithmeticError.None:ArithmeticError.Inexact,SafeDecimal.TryDivide(x,y).Error);
        }
    }
    [Theory]
    [InlineData("1.0000005","1.000000")]
    [InlineData("1.0000015","1.000002")]
    [InlineData("-1.0000005","-1.000000")]
    [InlineData("-1.0000015","-1.000002")]
    public void ConversionTies(string input,string expected)
    {
        var result=SafeDecimal.TryConvert<Decimal128,Decimal32>(Decimal128.Parse(input,CultureInfo.InvariantCulture),DecimalRounding.ToEven);
        Assert.Equal(Decimal32.Parse(expected,CultureInfo.InvariantCulture),result.Value); Assert.True(result.IsInexact);
    }
}
