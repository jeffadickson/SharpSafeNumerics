using System.Numerics;
using SharpSafeNumerics;
using Xunit;

public sealed class IeeeRepresentationTests
{
    [Fact] public void AllFormatsPreserveRepresentation() { Check<Decimal32>(); Check<Decimal64>(); Check<Decimal128>(); }
    private static void Check<T>() where T:struct,IFloatingPointIeee754<T>
    {
        var one=T.Parse("1.00",null); var other=T.Parse("1.0",null);
                Assert.False(IeeeDecimal<T>.SameQuantum(one,other).Value);
        Assert.Equal(-2,IeeeDecimal<T>.Quantum(one).Value);
        Assert.True(IeeeDecimal<T>.IsCanonical(one).Value);
        Assert.Equal(-1,IeeeDecimal<T>.Class(T.NegativeZero).Value);
        Assert.Equal(4,IeeeDecimal<T>.Class(T.PositiveInfinity).Value);
        Assert.Equal(2,IeeeDecimal<T>.Class(T.Epsilon).Value);
        Assert.Equal(T.Parse("100",null),IeeeDecimal<T>.ScaleB(T.One,2).Value);
        Assert.Equal(IeeeDecimalFlags.Overflow|IeeeDecimalFlags.Inexact,IeeeDecimal<T>.ScaleB(T.One,int.MaxValue).Flags);
        Assert.Equal(IeeeDecimalFlags.Underflow|IeeeDecimalFlags.Inexact,IeeeDecimal<T>.ScaleB(T.One,int.MinValue).Flags);
        Assert.True(IeeeDecimal<T>.TotalOrder(one,other).Value);
        Assert.True(IeeeDecimal<T>.TotalOrder(T.NegativeZero,T.Zero).Value);
        Assert.False(IeeeDecimal<T>.TotalOrder(T.Zero,T.NegativeZero).Value);
        var nan=IeeeDecimal<T>.CreateNaN(new BigInteger(123),true).Value;
                Assert.True(IeeeDecimal<T>.IsSignaling(nan).Value);
        Assert.True(IeeeDecimal<T>.TotalOrder(nan,IeeeDecimal<T>.SetPayload(0).Value).Value);
        Assert.True(IeeeDecimal<T>.TotalOrder(T.PositiveInfinity,nan).Value);
        Assert.True(IeeeDecimal<T>.TotalOrder(IeeeDecimal<T>.Negate(IeeeDecimal<T>.SetPayload(0).Value).Value,T.NegativeInfinity).Value);
        Assert.False(IeeeDecimal<T>.CompareSignaling(T.NaN,one,new(Traps:IeeeDecimalFlags.Invalid)).IsSuccess);
        Assert.Equal(new BigInteger(123),IeeeDecimal<T>.GetPayload(nan).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid,IeeeDecimal<T>.CompareQuiet(nan,one).Flags);
        Assert.Equal(IeeeDecimalFlags.None,IeeeDecimal<T>.CompareQuiet(T.NaN,one).Flags);
        Assert.Equal(IeeeDecimalFlags.Invalid,IeeeDecimal<T>.CompareSignaling(T.NaN,one).Flags);
        foreach(var x in new[]{one,nan,T.NegativeZero,T.PositiveInfinity}) {
            var bid=IeeeDecimal<T>.EncodeBinary(x).Value;
            Assert.Equal(bid,IeeeDecimal<T>.EncodeBinary(IeeeDecimal<T>.DecodeBinary(bid).Value).Value);
            var dpd=IeeeDecimal<T>.EncodeDecimal(x).Value;
            Assert.Equal(dpd,IeeeDecimal<T>.EncodeDecimal(IeeeDecimal<T>.DecodeDecimal(dpd).Value).Value);
        }
        Assert.Equal(new BigInteger(123),IeeeDecimal<T>.GetPayload(IeeeDecimal<T>.Parse("sNaN123").Value).Value);
        Assert.False(IeeeDecimal<T>.Parse("1e+").IsSuccess);
        Assert.Equal(T.Parse("1.25",null),IeeeDecimal<T>.Parse("1.25").Value);
        Assert.Equal(T.One,IeeeDecimal<T>.NextDown(IeeeDecimal<T>.NextUp(T.One).Value).Value);
        Assert.Equal(T.Parse("2",null),IeeeDecimal<T>.RoundToIntegralExact(T.Parse("2.5",null)).Value);
        Assert.Equal(IeeeDecimalFlags.Inexact,IeeeDecimal<T>.RoundToIntegralExact(T.Parse("2.5",null)).Flags);
        Assert.Equal(IeeeDecimalFlags.None,IeeeDecimal<T>.RoundToIntegral(T.Parse("2.5",null)).Flags);
    }
    [Fact] public void MalformedInputsAreTypedFailures() {
        Assert.False(IeeeDecimal<double>.Copy(1).IsSuccess);
        Assert.False(IeeeDecimal<Decimal32>.DecodeBinary(BigInteger.One<<32).IsSuccess);
        // The BigInteger metadata helper validates its arguments; the IEEE
        // source-format overload instead returns the specified positive zero.
        Assert.False(IeeeDecimal<Decimal32>.CreateNaN(new BigInteger(-1)).IsSuccess);
        Assert.False(IeeeDecimal<Decimal32>.Copy(Decimal32.One,new((IeeeDecimalRounding)255)).IsSuccess);
    }
    [Fact] public void RawPatternsSeed981723AndTextRoundTrip() { Randomized<Decimal32>(32);Randomized<Decimal64>(64);Randomized<Decimal128>(128); }
    private static void Randomized<T>(int width) where T:struct,IFloatingPointIeee754<T>
    {
        var random=new Random(981723);var bytes=new byte[width/8];Span<char> text=stackalloc char[128];
        for(int i=0;i<256;i++){
            random.NextBytes(bytes);var bits=new BigInteger(bytes,true,false);
            var value=IeeeDecimal<T>.DecodeBinary(bits).Value;
            Assert.True(IeeeDecimal<T>.TotalOrder(value,value).Value);
            var negative=IeeeDecimal<T>.Negate(value).Value;
            Assert.Equal(IeeeDecimal<T>.EncodeBinary(value).Value,IeeeDecimal<T>.EncodeBinary(IeeeDecimal<T>.Negate(negative).Value).Value);
            int count=IeeeDecimal<T>.Format(value,text).Value;
            var restored=IeeeDecimal<T>.Parse(text[..count]);Assert.True(restored.IsSuccess,$"width={width} i={i} bits={bits:X} text={new string(text[..count])}");
            Assert.True(IeeeDecimal<T>.TotalOrder(value,restored.Value).Value);
            Assert.True(IeeeDecimal<T>.TotalOrder(restored.Value,value).Value);
        }
    }
    [Fact] public void DirectedIntegralRoundingAndTraps()
    {
        var x=Decimal64.Parse("-2.5",null);
        Assert.Equal(Decimal64.Parse("-3",null),IeeeDecimal<Decimal64>.RoundToIntegralExact(x,new(IeeeDecimalRounding.TowardNegative)).Value);
        Assert.Equal(Decimal64.Parse("-2",null),IeeeDecimal<Decimal64>.RoundToIntegralExact(x,new(IeeeDecimalRounding.TowardPositive)).Value);
        var trapped=IeeeDecimal<Decimal64>.RoundToIntegralExact(x,new(Traps:IeeeDecimalFlags.Inexact));
        Assert.False(trapped.IsSuccess);Assert.Equal(IeeeDecimalFlags.Inexact,trapped.Flags);Assert.False(trapped.TryGetValue(out var cleared));Assert.Equal(Decimal64.Zero,cleared);
        Span<char> shortBuffer=stackalloc char[1];Assert.False(IeeeDecimal<Decimal64>.Format(x,shortBuffer).IsSuccess);
    }}






