using System.Globalization;
using System.Numerics;
using PeterO.Numbers;
using Xunit;
namespace SharpSafeNumerics.DecimalTests;
public sealed class IeeeReductionTests
{
    [Fact] public void IndependentExactReductionOracleAllFormatsAndModes()
    {
        Check<Decimal32>(EContext.Decimal32); Check<Decimal64>(EContext.Decimal64); Check<Decimal128>(EContext.Decimal128);
    }
    private static void Check<T>(EContext format) where T : struct, IFloatingPointIeee754<T>
    {
        // Seed and independent unbounded PeterO accumulation make failures reproducible.
        var rng = new Random(7542094);
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>())
        for (int sample = 0; sample < 40; sample++)
        {
            var a = new T[7]; var b = new T[7];
            EDecimal sum = EDecimal.Zero, abs = EDecimal.Zero, square = EDecimal.Zero, dot = EDecimal.Zero, product = EDecimal.One, productSum = EDecimal.One, productDiff = EDecimal.One;
            for (int i = 0; i < a.Length; i++)
            {
                string x = $"{rng.Next(-999999,999999)}E{rng.Next(-20,20)}", y = $"{rng.Next(-999999,999999)}E{rng.Next(-20,20)}";
                a[i] = T.Parse(x, CultureInfo.InvariantCulture); b[i] = T.Parse(y, CultureInfo.InvariantCulture);
                var ex = EDecimal.FromString(x); var ey = EDecimal.FromString(y);
                sum = sum.Add(ex); abs = abs.Add(ex.Abs()); square = square.Add(ex.Multiply(ex)); dot = dot.Add(ex.Multiply(ey)); product=product.Multiply(ex); productSum=productSum.Multiply(ex.Add(ey)); productDiff=productDiff.Multiply(ex.Subtract(ey));
            }
            var rounding = mode switch { IeeeDecimalRounding.ToNearestEven => ERounding.HalfEven, IeeeDecimalRounding.ToNearestAway => ERounding.HalfUp, IeeeDecimalRounding.TowardZero => ERounding.Down, IeeeDecimalRounding.TowardNegative => ERounding.Floor, _ => ERounding.Ceiling };
            var c = new IeeeDecimalContext(mode);
            var actual = new[] { IeeeDecimal<T>.Sum(a,c), IeeeDecimal<T>.AbsSum(a,c), IeeeDecimal<T>.SumSquare(a,c), IeeeDecimal<T>.Dot(a,b,c) };
            var scaledActual=new[]{IeeeDecimal<T>.ScaledProd(a,c),IeeeDecimal<T>.ScaledProdSum(a,b,c),IeeeDecimal<T>.ScaledProdDiff(a,b,c)};
            var scaledExpected=new[]{product,productSum,productDiff};
            for(int j=0;j<scaledActual.Length;j++)
            {
                var exact=scaledExpected[j];
                if(exact.IsZero) {Assert.True(T.IsZero(scaledActual[j].Value.Fraction));continue;}
                var digits=exact.UnsignedMantissa.ToString().Length;
                var scale=exact.Exponent.Add(EInteger.FromInt32(digits));
                var fraction=EDecimal.Create(exact.Mantissa,EInteger.FromInt32(-digits)).RoundToPrecision(format.WithRounding(rounding));
                if(fraction.Abs().CompareTo(EDecimal.One)>=0) {fraction=fraction.MovePointLeft(1);scale=scale.Add(EInteger.One);}
                Assert.Equal(T.Parse(fraction.ToString(),CultureInfo.InvariantCulture),scaledActual[j].Value.Fraction);
                Assert.Equal(BigInteger.Parse(scale.ToString(),CultureInfo.InvariantCulture),scaledActual[j].Value.Scale);
            }
            var expected = new[] { sum, abs, square, dot };
            for (int i = 0; i < actual.Length; i++)
            {
                var ec = format.WithRounding(rounding).WithBlankFlags();
                var rounded = expected[i].RoundToPrecision(ec);
                Assert.True(actual[i].IsSuccess); Assert.Equal(T.Parse(rounded.ToString(),CultureInfo.InvariantCulture),actual[i].Value);
                Assert.Equal((ec.Flags & EContext.FlagInexact)!=0,(actual[i].Flags & IeeeDecimalFlags.Inexact)!=0);
            }
        }
    }
    [Fact] public void ExtremaAcrossFormatsAndModes()
    { Extrema<Decimal32>();Extrema<Decimal64>();Extrema<Decimal128>(); }
    private static void Extrema<T>() where T:struct,IFloatingPointIeee754<T>
    {
        var random=new Random(7542096);
        foreach(var mode in Enum.GetValues<IeeeDecimalRounding>())
        {
            var c=new IeeeDecimalContext(mode);
            for(int i=0;i<100;i++)
            {
                var a=T.CreateChecked(random.Next(-10000,10000));var b=T.CreateChecked(random.Next(-10000,10000));
                var min=a<=b?a:b;var max=a>=b?a:b;var minMag=T.Abs(a)<T.Abs(b)?a:T.Abs(a)>T.Abs(b)?b:min;var maxMag=T.Abs(a)>T.Abs(b)?a:T.Abs(a)<T.Abs(b)?b:max;
                Assert.Equal(min,IeeeDecimal<T>.Minimum(a,b,c).Value);Assert.Equal(max,IeeeDecimal<T>.Maximum(a,b,c).Value);
                Assert.Equal(min,IeeeDecimal<T>.MinimumNumber(a,b,c).Value);Assert.Equal(max,IeeeDecimal<T>.MaximumNumber(a,b,c).Value);
                Assert.Equal(minMag,IeeeDecimal<T>.MinimumMagnitude(a,b,c).Value);Assert.Equal(maxMag,IeeeDecimal<T>.MaximumMagnitude(a,b,c).Value);
                Assert.Equal(minMag,IeeeDecimal<T>.MinimumMagnitudeNumber(a,b,c).Value);Assert.Equal(maxMag,IeeeDecimal<T>.MaximumMagnitudeNumber(a,b,c).Value);
            }
            Assert.Equal(T.PositiveInfinity,IeeeDecimal<T>.Maximum(T.PositiveInfinity,T.One,c).Value);
            Assert.Equal(T.NegativeInfinity,IeeeDecimal<T>.Minimum(T.NegativeInfinity,T.One,c).Value);
            Assert.Equal(T.One,IeeeDecimal<T>.MaximumNumber(T.One,T.NaN,c).Value);
            Assert.True(T.IsNaN(IeeeDecimal<T>.MaximumNumber(T.NaN,T.NaN,c).Value));
        }
    }
    [Fact] public void ExtremaNaNsSignsAndMagnitudeTies()
    {
        var sn = Decimal32.DecodeBinary(0x7E000123); var qn = Decimal32.DecodeBinary(0x7C000456);
        Assert.Equal(IeeeDecimalFlags.Invalid,IeeeDecimal<Decimal32>.MinimumNumber(sn,Decimal32.One).Flags);
        Assert.Equal(Decimal32.One,IeeeDecimal<Decimal32>.MinimumNumber(sn,Decimal32.One).Value);
        Assert.True(Decimal32.IsNaN(IeeeDecimal<Decimal32>.Minimum(qn,Decimal32.One).Value));
        Assert.True(Decimal32.IsNegative(IeeeDecimal<Decimal32>.Minimum(Decimal32.Zero,Decimal32.NegativeZero).Value));
        Assert.False(Decimal32.IsNegative(IeeeDecimal<Decimal32>.Maximum(Decimal32.Zero,Decimal32.NegativeZero).Value));
        Assert.Equal(-Decimal32.One,IeeeDecimal<Decimal32>.MinimumMagnitude(Decimal32.One,-Decimal32.One).Value);
        Assert.Equal(Decimal32.One,IeeeDecimal<Decimal32>.MaximumMagnitude(-Decimal32.One,Decimal32.One).Value);
        Assert.Equal(Decimal32.One,IeeeDecimal<Decimal32>.MaximumNumber(qn,Decimal32.One).Value);
        Assert.Equal(Decimal32.One,IeeeDecimal<Decimal32>.MinimumMagnitudeNumber(qn,Decimal32.One).Value);
        Assert.Equal(Decimal32.One,IeeeDecimal<Decimal32>.MaximumMagnitudeNumber(qn,Decimal32.One).Value);
    }
    [Fact] public void ScaledProductsRetainScaleAndExactFactors()
    {
        var empty=IeeeDecimal<Decimal32>.ScaledProd([]).Value; Assert.Equal(Decimal32.Parse("0.1"),empty.Fraction); Assert.Equal(BigInteger.One,empty.Scale);
        var huge=IeeeDecimal<Decimal32>.ScaledProd([Decimal32.Parse("1E90"),Decimal32.Parse("1E90")]); Assert.Equal(Decimal32.Parse("0.1"),huge.Value.Fraction); Assert.Equal(new BigInteger(181),huge.Value.Scale); Assert.Equal(IeeeDecimalFlags.None,huge.Flags);
        var sums=IeeeDecimal<Decimal32>.ScaledProdSum([Decimal32.Parse("1E20"),Decimal32.Parse("1E20")],[Decimal32.One,-Decimal32.One]);
        // (10^20+1)(10^20-1)=10^40-1 rounds to 10^40, with normalized carry.
        Assert.Equal(Decimal32.Parse("0.1"),sums.Value.Fraction); Assert.Equal(new BigInteger(41),sums.Value.Scale); Assert.Equal(IeeeDecimalFlags.Inexact,sums.Flags);
        var difference=IeeeDecimal<Decimal32>.ScaledProdDiff([Decimal32.CreateChecked(5)],[Decimal32.CreateChecked(3)]).Value; Assert.Equal(Decimal32.Parse("0.2"),difference.Fraction); Assert.Equal(BigInteger.One,difference.Scale);
        Assert.Equal(IeeeDecimalFlags.Invalid,IeeeDecimal<Decimal32>.ScaledProd([Decimal32.Zero,Decimal32.PositiveInfinity]).Flags);
        Assert.Equal(ArithmeticError.OutOfRange,IeeeDecimal<Decimal32>.ScaledProdSum([Decimal32.One],[]).Error);
    }
    [Fact] public void ReductionSpecialsValidationAndSingleRounding()
    {
        Assert.Equal(Decimal32.Zero,IeeeDecimal<Decimal32>.Sum([]).Value);
        Assert.Equal(Decimal32.One,IeeeDecimal<Decimal32>.Sum([Decimal32.Parse("1E20"),Decimal32.One,Decimal32.Parse("-1E20")]).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid,IeeeDecimal<Decimal32>.Sum([Decimal32.PositiveInfinity,Decimal32.NegativeInfinity]).Flags);
        Assert.Equal(IeeeDecimalFlags.Invalid,IeeeDecimal<Decimal32>.Dot([Decimal32.Zero],[Decimal32.PositiveInfinity]).Flags);
        var bad=IeeeDecimal<Decimal32>.Dot([Decimal32.One],[]); Assert.Equal(ArithmeticError.OutOfRange,bad.Error); Assert.False(bad.TryGetValue(out var value)); Assert.Equal(default,value);
        Assert.Equal(ArithmeticError.OutOfRange,IeeeDecimal<double>.Sum([1]).Error);
        Assert.Equal(ArithmeticError.OutOfRange,IeeeDecimal<Decimal32>.Sum([],new((IeeeDecimalRounding)99)).Error);
        var trapped=IeeeDecimal<Decimal32>.Sum([Decimal32.PositiveInfinity,Decimal32.NegativeInfinity],new(Traps:IeeeDecimalFlags.Invalid)); Assert.False(trapped.IsSuccess); Assert.False(trapped.TryGetValue(out value)); Assert.Equal(default,value);
    }
}



