using System.Numerics;
using SharpSafeNumerics;
using Xunit;

/// <summary>Boundary and finite-state regression checks for the IEEE qualification campaign.</summary>
public sealed class IeeeMutationRegressionTests
{
    [Fact]
    public void EveryFlagSubsetPreservesUnselectedBitsAndModes()
    {
        var modes=new IeeeDecimalModes(IeeeDecimalRounding.ToNearestAway,IeeeDecimalRounding.TowardZero,IeeeDecimalFlags.Invalid);
        // Exhaustive five-bit algebra checks state-management semantics independently of arithmetic.
        for(int raw=0;raw<32;raw++)for(int mask=0;mask<32;mask++) {
            var state=new IeeeDecimalState(modes,(IeeeDecimalFlags)raw);var selection=(IeeeDecimalFlags)mask;
            var raised=IeeeDecimalEnvironment.RaiseFlags(state,selection).Value;
            var lowered=IeeeDecimalEnvironment.LowerFlags(state,selection).Value;
            Assert.Equal((IeeeDecimalFlags)(raw|mask),raised.Flags);Assert.Equal(modes,raised.Modes);
            Assert.Equal((IeeeDecimalFlags)(raw&~mask),lowered.Flags);Assert.Equal(modes,lowered.Modes);
            Assert.Equal((raw&mask)!=0,IeeeDecimalEnvironment.TestFlags(state,selection).Value);
            Assert.Equal((raw&mask)!=0,IeeeDecimalEnvironment.TestSavedFlags(state.Flags,selection).Value);
            Assert.Equal(state.Flags,IeeeDecimalEnvironment.SaveFlags(state).Value);
            Assert.Equal(modes,IeeeDecimalEnvironment.SaveModes(state).Value);
            Assert.Equal(IeeeDecimalRounding.ToNearestAway,IeeeDecimalEnvironment.GetDecimalRoundingDirection(state).Value);
            Assert.Equal(IeeeDecimalRounding.TowardZero,IeeeDecimalEnvironment.GetBinaryRoundingDirection(state).Value);
            var restored=IeeeDecimalEnvironment.RestoreFlags(state,selection).Value;Assert.Equal(selection,restored.Flags);Assert.Equal(modes,restored.Modes);
        }
    }

    [Fact]
    public void EveryRequiredComparisonPredicateMatchesOrderedAndUnorderedTruthTables()
    {
        Func<Decimal64,Decimal64,IeeeDecimalContext,IeeeDecimalResult<bool>>[] quiet=[
            IeeeDecimal<Decimal64>.QuietEqual,IeeeDecimal<Decimal64>.QuietNotEqual,IeeeDecimal<Decimal64>.QuietGreater,IeeeDecimal<Decimal64>.QuietGreaterEqual,
            IeeeDecimal<Decimal64>.QuietLess,IeeeDecimal<Decimal64>.QuietLessEqual,IeeeDecimal<Decimal64>.QuietUnordered,IeeeDecimal<Decimal64>.QuietNotGreater,
            IeeeDecimal<Decimal64>.QuietLessUnordered,IeeeDecimal<Decimal64>.QuietNotLess,IeeeDecimal<Decimal64>.QuietGreaterUnordered,IeeeDecimal<Decimal64>.QuietOrdered];
        Func<Decimal64,Decimal64,IeeeDecimalContext,IeeeDecimalResult<bool>>[] signaling=[
            IeeeDecimal<Decimal64>.SignalingEqual,IeeeDecimal<Decimal64>.SignalingNotEqual,IeeeDecimal<Decimal64>.SignalingGreater,IeeeDecimal<Decimal64>.SignalingGreaterEqual,
            IeeeDecimal<Decimal64>.SignalingLess,IeeeDecimal<Decimal64>.SignalingLessEqual,IeeeDecimal<Decimal64>.SignalingNotGreater,
            IeeeDecimal<Decimal64>.SignalingLessUnordered,IeeeDecimal<Decimal64>.SignalingNotLess,IeeeDecimal<Decimal64>.SignalingGreaterUnordered];
        bool[][] truth=[
            [false,true,false,false,true,true,false,true,true,false,false,true],
            [true,false,false,true,false,true,false,true,false,true,false,true],
            [false,true,true,true,false,false,false,false,false,true,true,true],
            [false,true,false,false,false,false,true,true,true,true,true,false]];
        var snan=IeeeDecimal<Decimal64>.CreateNaN(17,true).Value;
        Decimal64[] left=[Decimal64.Zero,Decimal64.One,Decimal64.CreateChecked(2),Decimal64.NaN,snan];
        for(int row=0;row<left.Length;row++) {
            int expectedRow=Math.Min(row,3);
            for(int i=0;i<quiet.Length;i++) {
                var result=quiet[i](left[row],Decimal64.One,default);
                Assert.True(result.IsSuccess);Assert.Equal(truth[expectedRow][i],result.Value);
                Assert.Equal(row==4?IeeeDecimalFlags.Invalid:IeeeDecimalFlags.None,result.Flags);
            }
            int[] indexes=[0,1,2,3,4,5,7,8,9,10];
            for(int i=0;i<signaling.Length;i++) {
                var result=signaling[i](left[row],Decimal64.One,default);
                Assert.Equal(truth[expectedRow][indexes[i]],result.Value);
                Assert.Equal(row>=3?IeeeDecimalFlags.Invalid:IeeeDecimalFlags.None,result.Flags);
                if(row>=3) {var trapped=signaling[i](left[row],Decimal64.One,new(Traps:IeeeDecimalFlags.Invalid));Assert.False(trapped.TryGetValue(out var cleared));Assert.False(cleared);Assert.Equal(IeeeDecimalFlags.Invalid,trapped.Flags);}
            }
        }
    }
    [Fact]
    public void ReductionSpecialValuesCohortsAndZeroSignsAllFormats()
    { ReductionBoundary<Decimal32>(); ReductionBoundary<Decimal64>(); ReductionBoundary<Decimal128>(); }
    private static void ReductionBoundary<T>() where T:struct,IFloatingPointIeee754<T>
    {
        T One(string value)=>T.Parse(value,System.Globalization.CultureInfo.InvariantCulture);
        var qnan=IeeeDecimal<T>.CreateNaN(23).Value;var snan=IeeeDecimal<T>.CreateNaN(47,true).Value;
        foreach(var mode in Enum.GetValues<IeeeDecimalRounding>()) {
            var context=new IeeeDecimalContext(mode);
            foreach(var pair in new[]{(One("1.0"),One("1.00")),(One("-1.0"),One("-1.00"))}) {
                var raw=IeeeDecimal<T>.EncodeBinary(pair.Item1).Value;
                foreach(var result in new[]{IeeeDecimal<T>.Minimum(pair.Item1,pair.Item2,context),IeeeDecimal<T>.Maximum(pair.Item1,pair.Item2,context),IeeeDecimal<T>.MinimumMagnitude(pair.Item1,pair.Item2,context),IeeeDecimal<T>.MaximumMagnitude(pair.Item1,pair.Item2,context)})
                    Assert.Equal(raw,IeeeDecimal<T>.EncodeBinary(result.Value).Value);
            }
            var sum=IeeeDecimal<T>.Sum([T.NegativeZero,T.NegativeZero],context);
            Assert.True(T.IsNegative(sum.Value));Assert.Equal(IeeeDecimalFlags.None,sum.Flags);
            var cancellation=IeeeDecimal<T>.Sum([T.One,-T.One],context);Assert.Equal(mode==IeeeDecimalRounding.TowardNegative,T.IsNegative(cancellation.Value));
            Assert.False(T.IsNegative(IeeeDecimal<T>.AbsSum([T.NegativeZero],context).Value));
            Assert.False(T.IsNegative(IeeeDecimal<T>.SumSquare([T.NegativeZero],context).Value));
            Assert.True(T.IsNegative(IeeeDecimal<T>.Dot([T.NegativeZero],[T.One],context).Value));
            foreach(var result in new[]{IeeeDecimal<T>.Sum([T.PositiveInfinity],context),IeeeDecimal<T>.AbsSum([T.NegativeInfinity],context),IeeeDecimal<T>.SumSquare([T.NegativeInfinity],context),IeeeDecimal<T>.Dot([T.NegativeInfinity],[-T.One],context)})Assert.Equal(T.PositiveInfinity,result.Value);
            var nan=IeeeDecimal<T>.Dot([qnan,T.Zero],[snan,T.PositiveInfinity],context);Assert.Equal(IeeeDecimalFlags.Invalid,nan.Flags);Assert.Equal(new BigInteger(47),IeeeDecimal<T>.GetPayload(nan.Value).Value);Assert.False(IeeeDecimal<T>.IsSignaling(nan.Value).Value);
            var scaled=IeeeDecimal<T>.ScaledProd([T.NegativeZero,T.One],context);Assert.True(T.IsNegative(scaled.Value.Fraction));Assert.Equal(BigInteger.Zero,scaled.Value.Scale);
            var inf=IeeeDecimal<T>.ScaledProd([-T.One,T.PositiveInfinity],context);Assert.Equal(T.NegativeInfinity,inf.Value.Fraction);Assert.Equal(BigInteger.Zero,inf.Value.Scale);
            Assert.Equal(T.PositiveInfinity,IeeeDecimal<T>.ScaledProdSum([T.PositiveInfinity],[T.One],context).Value.Fraction);
            Assert.Equal(T.NegativeInfinity,IeeeDecimal<T>.ScaledProdSum([T.One],[T.NegativeInfinity],context).Value.Fraction);
            var opposing=IeeeDecimal<T>.ScaledProdSum([T.PositiveInfinity],[T.NegativeInfinity],context);Assert.True(T.IsNaN(opposing.Value.Fraction));Assert.Equal(IeeeDecimalFlags.Invalid,opposing.Flags);
            var differ=IeeeDecimal<T>.ScaledProdDiff([T.PositiveInfinity],[T.PositiveInfinity],context);Assert.True(T.IsNaN(differ.Value.Fraction));Assert.Equal(IeeeDecimalFlags.Invalid,differ.Flags);
            var scaledNan=IeeeDecimal<T>.ScaledProd([qnan,snan,T.Zero,T.PositiveInfinity],context);Assert.Equal(new BigInteger(47),IeeeDecimal<T>.GetPayload(scaledNan.Value.Fraction).Value);Assert.Equal(IeeeDecimalFlags.Invalid,scaledNan.Flags);Assert.Equal(BigInteger.Zero,scaledNan.Value.Scale);
            var trapped=IeeeDecimal<T>.ScaledProd([snan],new(mode,IeeeDecimalFlags.Invalid));Assert.False(trapped.TryGetValue(out var cleared));Assert.Equal(default,cleared);
            var exactZero=IeeeDecimal<T>.ScaledProdSum([T.One],[-T.One],context).Value;Assert.Equal(mode==IeeeDecimalRounding.TowardNegative,T.IsNegative(exactZero.Fraction));Assert.Equal(BigInteger.Zero,exactZero.Scale);
        }
    }

    [Fact]
    public void InvalidEnvironmentSnapshotsNeverPublishValues()
    {
        var unknown=(IeeeDecimalFlags)32;var invalidRounding=(IeeeDecimalRounding)5;
        IeeeDecimalState[] bad=[new(new(invalidRounding)),new(new(BinaryRounding:invalidRounding)),new(new(Traps:unknown)),new(Flags:unknown)];
        foreach(var state in bad) {
            Failure(IeeeDecimalEnvironment.GetDecimalRoundingDirection(state));Failure(IeeeDecimalEnvironment.GetBinaryRoundingDirection(state));
            Failure(IeeeDecimalEnvironment.SetDecimalRoundingDirection(state,default));Failure(IeeeDecimalEnvironment.SetBinaryRoundingDirection(state,default));
            Failure(IeeeDecimalEnvironment.SaveModes(state));Failure(IeeeDecimalEnvironment.RestoreModes(state,default));Failure(IeeeDecimalEnvironment.DefaultModes(state));
            Failure(IeeeDecimalEnvironment.RaiseFlags(state,default));Failure(IeeeDecimalEnvironment.LowerFlags(state,default));Failure(IeeeDecimalEnvironment.TestFlags(state,default));
            Failure(IeeeDecimalEnvironment.SaveFlags(state));Failure(IeeeDecimalEnvironment.RestoreFlags(state,default));
        }
        Failure(IeeeDecimalEnvironment.TestSavedFlags(unknown,default));Failure(IeeeDecimalEnvironment.TestSavedFlags(default,unknown));
        Failure(IeeeDecimalEnvironment.RestoreModes(default,new(BinaryRounding:invalidRounding)));Failure(IeeeDecimalEnvironment.RestoreModes(default,new(Traps:unknown)));
        Failure(IeeeDecimalEnvironment.RestoreFlags(default,unknown));Failure(IeeeDecimalEnvironment.LowerFlags(default,unknown));Failure(IeeeDecimalEnvironment.TestFlags(default,unknown));
    }
    private static void Failure<TValue>(IeeeDecimalResult<TValue> result) where TValue:struct
    { Assert.Equal(ArithmeticError.OutOfRange,result.Error);Assert.Equal(IeeeDecimalFlags.None,result.Flags);Assert.False(result.TryGetValue(out var cleared));Assert.Equal(default,cleared); }
    [Fact]
    public void ClassificationQuantumAndRepresentationBoundariesAllFormats()
    { RepresentationBoundary<Decimal32>(-95,7,32); RepresentationBoundary<Decimal64>(-383,16,64); RepresentationBoundary<Decimal128>(-6143,34,128); }
    private static void RepresentationBoundary<T>(int normalExponent,int precision,int width) where T:struct,IFloatingPointIeee754<T>
    {
        T Parse(string text)=>T.Parse(text,System.Globalization.CultureInfo.InvariantCulture);
        var sn=IeeeDecimal<T>.CreateNaN(37,true).Value;var qn=IeeeDecimal<T>.CreateNaN(19).Value;
        var minimum=Parse($"1E{normalExponent}");
        Assert.True(IeeeDecimal<T>.IsZero(default(T)).Value);Assert.False(IeeeDecimal<T>.IsSubnormal(default(T)).Value);Assert.False(IeeeDecimal<T>.IsNormal(default(T)).Value);
        var largestSubnormal=T.BitDecrement(minimum);Assert.True(IeeeDecimal<T>.IsSubnormal(largestSubnormal).Value);Assert.False(IeeeDecimal<T>.IsNormal(largestSubnormal).Value);
        var qLow=IeeeDecimal<T>.CreateNaN(11).Value;var qHigh=IeeeDecimal<T>.CreateNaN(22).Value;
        Assert.True(IeeeDecimal<T>.TotalOrder(qLow,qHigh).Value);Assert.False(IeeeDecimal<T>.TotalOrder(qHigh,qLow).Value);
        Assert.True(IeeeDecimal<T>.TotalOrder(IeeeDecimal<T>.Negate(qHigh).Value,IeeeDecimal<T>.Negate(qLow).Value).Value);
        Assert.False(IeeeDecimal<T>.TotalOrder(Parse("1.0"),Parse("1.00")).Value);Assert.True(IeeeDecimal<T>.TotalOrder(Parse("1.00"),Parse("1.0")).Value);
        Assert.False(IeeeDecimal<T>.IsSignaling(IeeeDecimal<T>.ScaleB(sn,1).Value).Value);Assert.False(IeeeDecimal<T>.IsSignaling(IeeeDecimal<T>.RoundToIntegral(sn).Value).Value);
        Assert.Equal(5,IeeeDecimal<T>.Class(IeeeDecimal<T>.Negate(qn).Value).Value);Assert.Equal(6,IeeeDecimal<T>.Class(IeeeDecimal<T>.Negate(sn).Value).Value);
        T[] values=[T.Zero,T.NegativeZero,T.Epsilon,-T.Epsilon,minimum,-minimum,T.One,-T.One,T.PositiveInfinity,T.NegativeInfinity,qn,sn];
        int[] classes=[1,-1,2,-2,3,-3,3,-3,4,-4,5,6];
        for(int i=0;i<values.Length;i++) {
            var value=values[i];bool finite=i<8,zero=i<2,subnormal=i is 2 or 3,normal=i is >=4 and <8,infinite=i is 8 or 9,nan=i>=10;
            Assert.Equal(classes[i],IeeeDecimal<T>.Class(value).Value);
            Assert.Equal(finite,IeeeDecimal<T>.IsFinite(value).Value);Assert.Equal(zero,IeeeDecimal<T>.IsZero(value).Value);
            Assert.Equal(subnormal,IeeeDecimal<T>.IsSubnormal(value).Value);Assert.Equal(normal,IeeeDecimal<T>.IsNormal(value).Value);
            Assert.Equal(infinite,IeeeDecimal<T>.IsInfinite(value).Value);Assert.Equal(nan,IeeeDecimal<T>.IsNaN(value).Value);
            Assert.Equal(T.IsNegative(value),IeeeDecimal<T>.IsSignMinus(value).Value);
            var raw=IeeeDecimal<T>.EncodeBinary(value).Value;
            Assert.Equal(raw,IeeeDecimal<T>.EncodeBinary(IeeeDecimal<T>.Copy(value).Value).Value);
            var abs=IeeeDecimal<T>.Abs(value);Assert.Equal(IeeeDecimalFlags.None,abs.Flags);Assert.False(T.IsNegative(abs.Value));
            Assert.Equal(raw&~(BigInteger.One<<(width-1)),IeeeDecimal<T>.EncodeBinary(abs.Value).Value);
            var negative=IeeeDecimal<T>.CopySign(value,T.NegativeZero);Assert.True(T.IsNegative(negative.Value));Assert.Equal(raw|(BigInteger.One<<(width-1)),IeeeDecimal<T>.EncodeBinary(negative.Value).Value);
            if(nan) {
                var next=IeeeDecimal<T>.NextUp(value);Assert.False(IeeeDecimal<T>.IsSignaling(next.Value).Value);Assert.Equal(i==11?IeeeDecimalFlags.Invalid:IeeeDecimalFlags.None,next.Flags);
                var scaled=IeeeDecimal<T>.ScaleB(value,7);Assert.Equal(next.Flags,scaled.Flags);Assert.Equal(IeeeDecimal<T>.GetPayload(value).Value,IeeeDecimal<T>.GetPayload(scaled.Value).Value);
                Assert.Equal(next.Flags,IeeeDecimal<T>.RoundToIntegral(value).Flags);Assert.False(IeeeDecimal<T>.Quantum(value).IsSuccess);
            }
            else if(infinite) {
                Assert.Equal(T.PositiveInfinity,IeeeDecimal<T>.LogB(value).Value);Assert.Equal(T.PositiveInfinity,IeeeDecimal<T>.GetQuantum(value).Value);Assert.False(IeeeDecimal<T>.Quantum(value).IsSuccess);
            }
        }
        var quantum=IeeeDecimal<T>.GetQuantum(Parse("-1.23"));Assert.Equal(Parse("0.01"),quantum.Value);
        Assert.Equal(Parse("-2"),IeeeDecimal<T>.LogB(Parse("-0.0123")).Value);Assert.Equal(T.Zero,IeeeDecimal<T>.LogB(T.One).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid,IeeeDecimal<T>.GetQuantum(sn).Flags);Assert.Equal(IeeeDecimalFlags.Invalid,IeeeDecimal<T>.LogB(sn).Flags);
        Assert.Equal(ArithmeticError.NonFinite,IeeeDecimal<T>.CompareSignaling(qn,T.One,new(Traps:IeeeDecimalFlags.Invalid)).Error);
        T[] total=[IeeeDecimal<T>.Negate(qn).Value,T.NegativeInfinity,-T.One,T.NegativeZero,T.Zero,T.One,T.PositiveInfinity,sn,qn];
        for(int i=0;i<total.Length;i++)for(int j=0;j<total.Length;j++)Assert.Equal(i<=j,IeeeDecimal<T>.TotalOrder(total[i],total[j]).Value);
        Assert.True(IeeeDecimal<T>.TotalOrderMag(-T.One,Parse("2")).Value);Assert.False(IeeeDecimal<T>.TotalOrderMag(Parse("-2"),T.One).Value);
        foreach(var a in new[]{T.Zero,T.PositiveInfinity,qn,sn})foreach(var b in new[]{T.Zero,T.PositiveInfinity,qn,sn}) {
            bool expected=T.IsNaN(a)&&T.IsNaN(b)||T.IsInfinity(a)&&T.IsInfinity(b)||T.IsZero(a)&&T.IsZero(b);
            Assert.Equal(expected,IeeeDecimal<T>.SameQuantum(a,b).Value);
        }
        foreach(var mode in Enum.GetValues<IeeeDecimalRounding>())foreach(string text in new[]{"-3.5","-2.6","-2.5","-2.1","-2.0","0.1","2.0","2.1","2.5","2.6","3.5"}) {
            decimal x=decimal.Parse(text,System.Globalization.CultureInfo.InvariantCulture);
            var midpoint=mode switch{IeeeDecimalRounding.ToNearestEven=>MidpointRounding.ToEven,IeeeDecimalRounding.ToNearestAway=>MidpointRounding.AwayFromZero,IeeeDecimalRounding.TowardZero=>MidpointRounding.ToZero,IeeeDecimalRounding.TowardNegative=>MidpointRounding.ToNegativeInfinity,_=>MidpointRounding.ToPositiveInfinity};
            var result=IeeeDecimal<T>.RoundToIntegralExact(Parse(text),new(mode));Assert.Equal(T.CreateChecked(decimal.Round(x,0,midpoint)),result.Value);
            Assert.Equal(x==decimal.Truncate(x)?IeeeDecimalFlags.None:IeeeDecimalFlags.Inexact,result.Flags);
        }
        Assert.True(IeeeDecimal<T>.IsSignaling(IeeeDecimal<T>.SetPayloadSignaling(17L).Value).Value);
        BigInteger limit=BigInteger.Pow(10,precision-1);
        Assert.False(IeeeDecimal<T>.CreateNaN(limit).IsSuccess);Assert.Equal(limit-1,IeeeDecimal<T>.GetPayload(IeeeDecimal<T>.CreateNaN(limit-1).Value).Value);
        Assert.True(T.IsZero(IeeeDecimal<T>.SetPayload(Parse(limit.ToString())).Value));Assert.True(T.IsZero(IeeeDecimal<T>.SetPayload((Int128)(-1)).Value));
        Assert.True(T.IsZero(IeeeDecimal<T>.SetPayload((UInt128)limit).Value));Assert.True(IeeeDecimal<T>.IsSignaling(IeeeDecimal<T>.SetPayloadSignaling(17L,new()).Value).Value);
        Assert.True(IeeeDecimal<T>.IsSignaling(IeeeDecimal<T>.SetPayloadSignaling(Parse("17"),new()).Value).Value);
        Assert.Equal(new BigInteger(17),IeeeDecimal<T>.GetPayload(IeeeDecimal<T>.SetPayload(17L,new()).Value).Value);
        Assert.Equal(new BigInteger(17),IeeeDecimal<T>.GetPayload(IeeeDecimal<T>.SetPayload(Parse("17"),new()).Value).Value);
        Assert.True(T.IsZero(IeeeDecimal<T>.DecodeBinary(0).Value));Assert.False(IeeeDecimal<T>.DecodeDecimal(BigInteger.One<<width).IsSuccess);
        foreach(string malformed in new[]{"NaNx","sNaN-1","NaN"+limit.ToString(),"sNaN"+limit.ToString()})Assert.False(IeeeDecimal<T>.Parse(malformed).IsSuccess);
        var invalid=new IeeeDecimalContext((IeeeDecimalRounding)5);Failure(IeeeDecimal<T>.Radix(invalid));Failure(IeeeDecimal<T>.Copy(T.One,invalid));Failure(IeeeDecimal<T>.Negate(T.One,invalid));Failure(IeeeDecimal<T>.Abs(T.One,invalid));Failure(IeeeDecimal<T>.CopySign(T.One,T.One,invalid));Failure(IeeeDecimal<T>.Class(T.One,invalid));Failure(IeeeDecimal<T>.IsSignaling(sn,invalid));Failure(IeeeDecimal<T>.IsSignMinus(T.One,invalid));Failure(IeeeDecimal<T>.TotalOrderMag(T.One,T.One,invalid));Failure(IeeeDecimal<T>.SetPayload(17L,invalid));Failure(IeeeDecimal<T>.SetPayloadSignaling(17L,invalid));Failure(IeeeDecimal<T>.SetPayload(Parse("17"),invalid));Failure(IeeeDecimal<T>.SetPayloadSignaling(Parse("17"),invalid));Failure(IeeeDecimal<T>.Parse("1",invalid));
    }

    [Fact]
    public void ValidRoundingModeBoundariesAreRetainedBySnapshots()
    {
        foreach(var decimalMode in Enum.GetValues<IeeeDecimalRounding>())foreach(var binaryMode in Enum.GetValues<IeeeDecimalRounding>()) {
            var state=new IeeeDecimalState(new(decimalMode,binaryMode,IeeeDecimalFlags.Underflow),IeeeDecimalFlags.Inexact);
            Assert.Equal(decimalMode,IeeeDecimalEnvironment.GetDecimalRoundingDirection(state).Value);Assert.Equal(binaryMode,IeeeDecimalEnvironment.GetBinaryRoundingDirection(state).Value);
            var changed=IeeeDecimalEnvironment.SetBinaryRoundingDirection(state,IeeeDecimalRounding.TowardPositive).Value;Assert.Equal(IeeeDecimalRounding.TowardPositive,changed.Modes.BinaryRounding);Assert.Equal(state.Flags,changed.Flags);Assert.Equal(decimalMode,changed.Modes.DecimalRounding);
            var decimalChanged=IeeeDecimalEnvironment.SetDecimalRoundingDirection(state,IeeeDecimalRounding.TowardPositive).Value;Assert.Equal(IeeeDecimalRounding.TowardPositive,decimalChanged.Modes.DecimalRounding);Assert.Equal(state.Flags,decimalChanged.Flags);Assert.Equal(binaryMode,decimalChanged.Modes.BinaryRounding);
        }
    }    [Fact]
    public void IntervalArithmeticContainsIndependentExactEndpointExtrema()
    {
        var math=new DecimalIntervalMath(3);var scale=new BigInteger(1000);
        Assert.Equal(new DecimalIntervalMath.Interval(scale,scale),math.One);Assert.Equal(new DecimalIntervalMath.Interval(0,0),math.Zero);
        // Decimal arithmetic is exact for these small endpoint sums/products. Its independently implemented rounding supplies floor/ceiling references.
        for(int numerator=-31;numerator<=31;numerator++)for(int denominator=1;denominator<=11;denominator++) {
            decimal exact=(decimal)numerator/denominator;
            var interval=math.Rational(numerator,denominator);
            Assert.Equal(new BigInteger(decimal.Floor(exact*1000)),interval.Lower);Assert.Equal(new BigInteger(decimal.Ceiling(exact*1000)),interval.Upper);
            Assert.Equal(new BigInteger(decimal.Floor(exact)),DecimalIntervalMath.Floor(numerator,denominator));Assert.Equal(new BigInteger(decimal.Ceiling(exact)),DecimalIntervalMath.Ceiling(numerator,denominator));
        }
        DecimalIntervalMath.Interval[] samples=[new(-2100,-1900),new(-1300,700),new(0,0),new(200,1300),new(1700,2300)];
        foreach(var a in samples)foreach(var b in samples) {
            Assert.Equal(new DecimalIntervalMath.Interval(a.Lower+b.Lower,a.Upper+b.Upper),math.Add(a,b));
            Assert.Equal(new DecimalIntervalMath.Interval(a.Lower-b.Upper,a.Upper-b.Lower),math.Subtract(a,b));
            Assert.Equal(new DecimalIntervalMath.Interval(-a.Upper,-a.Lower),math.Negate(a));
            decimal[] products=[(decimal)a.Lower*(decimal)b.Lower/1000,(decimal)a.Lower*(decimal)b.Upper/1000,(decimal)a.Upper*(decimal)b.Lower/1000,(decimal)a.Upper*(decimal)b.Upper/1000];
            Assert.Equal(new DecimalIntervalMath.Interval(new BigInteger(decimal.Floor(products.Min())),new BigInteger(decimal.Ceiling(products.Max()))),math.Multiply(a,b));
            if(b.Lower>0||b.Upper<0) {
                decimal[] quotients=[(decimal)a.Lower*1000/(decimal)b.Lower,(decimal)a.Lower*1000/(decimal)b.Upper,(decimal)a.Upper*1000/(decimal)b.Lower,(decimal)a.Upper*1000/(decimal)b.Upper];
                Assert.Equal(new DecimalIntervalMath.Interval(new BigInteger(decimal.Floor(quotients.Min())),new BigInteger(decimal.Ceiling(quotients.Max()))),math.Divide(a,b));
            }
        }
        var point=new DecimalIntervalMath.Interval(1701,2309);
        Assert.Equal(new DecimalIntervalMath.Interval(-6927,-5103),math.Multiply(point,new BigInteger(-3)));
        Assert.Equal(new DecimalIntervalMath.Interval(-770,-567),math.Divide(point,new BigInteger(-3)));
        foreach(int n in new[]{0,1,2,3,4,15,16,17,999,1000}) {
            var root=DecimalIntervalMath.IntegerSqrt(n);Assert.True(root*root<=n);Assert.True((root+1)*(root+1)>n);
            var input=new DecimalIntervalMath.Interval(n,n+1);var bounds=math.Sqrt(input);
            Assert.True(bounds.Lower*bounds.Lower<=n*scale);Assert.True((bounds.Lower+1)*(bounds.Lower+1)>n*scale);
            Assert.True(bounds.Upper*bounds.Upper>=(n+1)*scale);Assert.True(bounds.Upper.IsZero||(bounds.Upper-1)*(bounds.Upper-1)<(n+1)*scale);
        }
        Assert.Throws<ArgumentOutOfRangeException>(()=>DecimalIntervalMath.IntegerSqrt(-1));Assert.Throws<ArgumentOutOfRangeException>(()=>DecimalIntervalMath.Floor(1,0));Assert.Throws<ArgumentOutOfRangeException>(()=>DecimalIntervalMath.Floor(1,-2));
        Assert.Throws<IntervalRefinementRequiredException>(()=>math.Divide(math.One,new DecimalIntervalMath.Interval(-1,1)));
        Assert.Throws<IntervalRefinementRequiredException>(()=>math.Sqrt(new DecimalIntervalMath.Interval(-1,2)));
        Assert.Throws<IntervalRefinementRequiredException>(()=>math.Log(new DecimalIntervalMath.Interval(0,1)));
        Assert.Throws<IntervalRefinementRequiredException>(()=>math.SinCos(new DecimalIntervalMath.Interval(1570,1580)));
        Assert.True(Assert.Throws<DecimalExponentialRangeException>(()=>math.Exp(math.Integer(50001))).Overflow);
        Assert.False(Assert.Throws<DecimalExponentialRangeException>(()=>math.Exp(math.Integer(-50001))).Overflow);
    }    [Fact]
    public void ElementaryIntervalsContainDirectedMpfrReferenceBounds()
    {
        // GNU MPFR 4.2.1, exact dyadic inputs, directed function evaluation and directed 80-significant-digit decimal formatting.
        // 1024/2048-bit files agree byte-for-byte: SHA256 E909B3802C5ECAB0E3A9005CD3E434026336AD78186C58D5BD19AB36AC539555.
        // Reproduce: eng/DecimalConformance/MpfrFunctionReference.c, `2048 internal`; this is a finite enclosure regression, not a proof for all inputs.
        (string Operation,string Input,string Lower,string Upper)[] references=[
            ("Pi","0","3.1415926535897932384626433832795028841971693993751058209749445923078164062862089e+00","3.1415926535897932384626433832795028841971693993751058209749445923078164062862090e+00"),
            ("Exp","-2","1.3533528323661269189399949497248440340763154590957588146815887265407337410148768e-01","1.3533528323661269189399949497248440340763154590957588146815887265407337410148769e-01"),
            ("Exp","-0.125","8.8249690258459540286489214322905073622200482499065074177030931920810929521130873e-01","8.8249690258459540286489214322905073622200482499065074177030931920810929521130874e-01"),
            ("Exp","0.125","1.1331484530668263168290072278117938725655031317451816259128200360788235778800483e+00","1.1331484530668263168290072278117938725655031317451816259128200360788235778800484e+00"),
            ("Exp","2","7.3890560989306502272304274605750078131803155705518473240871278225225737960790577e+00","7.3890560989306502272304274605750078131803155705518473240871278225225737960790578e+00"),
            ("Exp","10","2.2026465794806716516957900645284244366353512618556781074235426355225202818570792e+04","2.2026465794806716516957900645284244366353512618556781074235426355225202818570793e+04"),
            ("Log","0.125","-2.0794415416798359282516963643745297042265004030807657623620400284801808659090842e+00","-2.0794415416798359282516963643745297042265004030807657623620400284801808659090841e+00"),
            ("Log","2","6.9314718055994530941723212145817656807550013436025525412068000949339362196969471e-01","6.9314718055994530941723212145817656807550013436025525412068000949339362196969472e-01"),
            ("Log","10","2.3025850929940456840179914546843642076011014886287729760333279009675726096773524e+00","2.3025850929940456840179914546843642076011014886287729760333279009675726096773525e+00"),
            ("Atan","-2","-1.1071487177940905030170654601785370400700476454014326466765392074337103389773628e+00","-1.1071487177940905030170654601785370400700476454014326466765392074337103389773627e+00"),
            ("Atan","-0.125","-1.2435499454676143503135484916387102557317019176980408991511411911572226742756676e-01","-1.2435499454676143503135484916387102557317019176980408991511411911572226742756675e-01"),
            ("Atan","0.125","1.2435499454676143503135484916387102557317019176980408991511411911572226742756675e-01","1.2435499454676143503135484916387102557317019176980408991511411911572226742756676e-01"),
            ("Atan","2","1.1071487177940905030170654601785370400700476454014326466765392074337103389773627e+00","1.1071487177940905030170654601785370400700476454014326466765392074337103389773628e+00"),
            ("Atan","10","1.4711276743037345918528755717617308518553063771832382624719635193438804556955538e+00","1.4711276743037345918528755717617308518553063771832382624719635193438804556955539e+00"),
            ("Sin","-2","-9.0929742682568169539601986591174484270225497144789026837897301153096730154078355e-01","-9.0929742682568169539601986591174484270225497144789026837897301153096730154078354e-01"),
            ("Sin","-0.125","-1.2467473338522768995744270871210846758783490564167925788551471462667078739237336e-01","-1.2467473338522768995744270871210846758783490564167925788551471462667078739237335e-01"),
            ("Sin","0.125","1.2467473338522768995744270871210846758783490564167925788551471462667078739237335e-01","1.2467473338522768995744270871210846758783490564167925788551471462667078739237336e-01"),
            ("Sin","2","9.0929742682568169539601986591174484270225497144789026837897301153096730154078354e-01","9.0929742682568169539601986591174484270225497144789026837897301153096730154078355e-01"),
            ("Sin","10","-5.4402111088936981340474766185137728168364301291622389157418401261675720964049343e-01","-5.4402111088936981340474766185137728168364301291622389157418401261675720964049342e-01"),
            ("Cos","-2","-4.1614683654714238699756822950076218976600077107554489075514997378196493612407917e-01","-4.1614683654714238699756822950076218976600077107554489075514997378196493612407916e-01"),
            ("Cos","-0.125","9.9219766722932905314909690778825086954332730473660126346890969719396405884006148e-01","9.9219766722932905314909690778825086954332730473660126346890969719396405884006149e-01"),
            ("Cos","0.125","9.9219766722932905314909690778825086954332730473660126346890969719396405884006148e-01","9.9219766722932905314909690778825086954332730473660126346890969719396405884006149e-01"),
            ("Cos","2","-4.1614683654714238699756822950076218976600077107554489075514997378196493612407917e-01","-4.1614683654714238699756822950076218976600077107554489075514997378196493612407916e-01"),
            ("Cos","10","-8.3907152907645245225886394782406483451993016513316854683595373104879258686627077e-01","-8.3907152907645245225886394782406483451993016513316854683595373104879258686627076e-01"),
        ];
        foreach(int digits in new[]{3,4,6,9,12,24,48,72}) {
            var math=new DecimalIntervalMath(digits);
            foreach(var reference in references) {
                var input=math.Decimal(IeeeDecimalCodec<Decimal64>.Decode(Decimal64.Parse(reference.Input,System.Globalization.CultureInfo.InvariantCulture)));
                var actual=reference.Operation switch {
                    "Pi"=>math.Pi(),"Exp"=>math.Exp(input),"Log"=>math.Log(input),"Atan"=>math.Atan(input),"Sin"=>math.SinCos(input).Sin,"Cos"=>math.SinCos(input).Cos,
                    _=>throw new InvalidOperationException("Unknown reference operation.")};
                Contains(actual,reference.Lower,reference.Upper,digits,$"{reference.Operation}({reference.Input}) scale={digits}");
            }
            var crossZero=new DecimalIntervalMath.Interval(-2*math.Scale,2*math.Scale);
            foreach(string operation in new[]{"Exp","Atan"}) {
                var lower=references.Single(r=>r.Operation==operation&&r.Input=="-2");var upper=references.Single(r=>r.Operation==operation&&r.Input=="2");
                Contains(operation=="Exp"?math.Exp(crossZero):math.Atan(crossZero),lower.Lower,upper.Upper,digits,$"{operation}([-2,2]) scale={digits}");
            }
        }
    }
    private static void Contains(DecimalIntervalMath.Interval actual,string lower,string upper,int digits,string scenario)
    {
        // PeterO performs only exact decimal parsing/scaling/comparison here; all transcendental reference evaluation happened independently in MPFR.
        var expectedLower=PeterO.Numbers.EDecimal.FromString(lower).MovePointRight(digits);
        var expectedUpper=PeterO.Numbers.EDecimal.FromString(upper).MovePointRight(digits);
        Assert.True(actual.Lower<=actual.Upper,scenario);
        Assert.True(PeterO.Numbers.EDecimal.FromString(actual.Lower.ToString(System.Globalization.CultureInfo.InvariantCulture)).CompareTo(expectedLower)<=0,"lower "+scenario);
        Assert.True(PeterO.Numbers.EDecimal.FromString(actual.Upper.ToString(System.Globalization.CultureInfo.InvariantCulture)).CompareTo(expectedUpper)>=0,"upper "+scenario);
    }    [Fact]
    public void ReductionMutationCounterexamplesPreserveFlagsPayloadPriorityAndPreferredQuantum()
    { ReductionCounterexamples<Decimal32>();ReductionCounterexamples<Decimal64>();ReductionCounterexamples<Decimal128>(); }
    private static void ReductionCounterexamples<T>() where T:struct,IFloatingPointIeee754<T>
    {
        T P(string text)=>T.Parse(text,System.Globalization.CultureInfo.InvariantCulture);
        var q1=IeeeDecimal<T>.CreateNaN(11).Value;var q2=IeeeDecimal<T>.CreateNaN(22).Value;var s1=IeeeDecimal<T>.CreateNaN(33,true).Value;var s2=IeeeDecimal<T>.CreateNaN(44,true).Value;
        T[] special=[T.NegativeInfinity,-T.One,T.One,T.PositiveInfinity];
        foreach(var a in special)foreach(var b in special) {
            T min=a<=b?a:b,max=a>=b?a:b;T minMagnitude=T.Abs(a)<T.Abs(b)?a:T.Abs(a)>T.Abs(b)?b:min,maxMagnitude=T.Abs(a)>T.Abs(b)?a:T.Abs(a)<T.Abs(b)?b:max;
            Assert.Equal(min,IeeeDecimal<T>.Minimum(a,b).Value);Assert.Equal(max,IeeeDecimal<T>.Maximum(a,b).Value);
            Assert.Equal(minMagnitude,IeeeDecimal<T>.MinimumMagnitude(a,b).Value);Assert.Equal(maxMagnitude,IeeeDecimal<T>.MaximumMagnitude(a,b).Value);
        }
        Assert.Equal(P("0.001"),IeeeDecimal<T>.Minimum(P("0.01"),P("0.001")).Value);
        foreach(var result in new[]{IeeeDecimal<T>.MinimumNumber(T.One,q1),IeeeDecimal<T>.MaximumNumber(T.One,q1),IeeeDecimal<T>.MinimumMagnitudeNumber(T.One,q1),IeeeDecimal<T>.MaximumMagnitudeNumber(T.One,q1)}) {Assert.Equal(T.One,result.Value);Assert.Equal(IeeeDecimalFlags.None,result.Flags);}
        foreach(var result in new[]{IeeeDecimal<T>.MinimumNumber(T.One,s1),IeeeDecimal<T>.MaximumNumber(T.One,s1)}) {Assert.Equal(T.One,result.Value);Assert.Equal(IeeeDecimalFlags.Invalid,result.Flags);}
        Assert.Equal(new BigInteger(11),IeeeDecimal<T>.GetPayload(IeeeDecimal<T>.Sum([q1,q2]).Value).Value);
        Assert.Equal(new BigInteger(33),IeeeDecimal<T>.GetPayload(IeeeDecimal<T>.Sum([s1,s2]).Value).Value);
        Assert.Equal(new BigInteger(11),IeeeDecimal<T>.GetPayload(IeeeDecimal<T>.ScaledProd([q1,q2]).Value.Fraction).Value);
        Assert.Equal(new BigInteger(33),IeeeDecimal<T>.GetPayload(IeeeDecimal<T>.ScaledProd([s1,s2]).Value.Fraction).Value);
        foreach(var nan in new[]{q1,s1}) {
            var expected=IeeeDecimal<T>.IsSignaling(nan).Value?IeeeDecimalFlags.Invalid:IeeeDecimalFlags.None;
            var product=IeeeDecimal<T>.ScaledProdSum([T.One],[nan]);Assert.Equal(IeeeDecimal<T>.GetPayload(nan).Value,IeeeDecimal<T>.GetPayload(product.Value.Fraction).Value);Assert.Equal(expected,product.Flags);
            var dot=IeeeDecimal<T>.Dot([T.One],[nan]);Assert.Equal(expected,dot.Flags);Assert.Equal(IeeeDecimal<T>.GetPayload(nan).Value,IeeeDecimal<T>.GetPayload(dot.Value).Value);
        }
        foreach(var marker in new[]{T.Zero,T.PositiveInfinity,q1,s1}) {
            var scaled=IeeeDecimal<T>.ScaledProd([P("1E20"),marker]);Assert.Equal(BigInteger.Zero,scaled.Value.Scale);
            Assert.Equal(T.IsNaN(marker),T.IsNaN(scaled.Value.Fraction));
        }
        var multipleInvalid=IeeeDecimal<T>.ScaledProdSum([T.PositiveInfinity,T.Zero,T.PositiveInfinity],[T.NegativeInfinity,T.Zero,T.Zero]);Assert.True(T.IsNaN(multipleInvalid.Value.Fraction));Assert.Equal(IeeeDecimalFlags.Invalid,multipleInvalid.Flags);
        var invalidDot=IeeeDecimal<T>.Dot([T.Zero,T.PositiveInfinity,T.NegativeInfinity],[T.PositiveInfinity,T.One,T.One]);Assert.True(T.IsNaN(invalidDot.Value));Assert.Equal(IeeeDecimalFlags.Invalid,invalidDot.Flags);
        Assert.Equal(2,IeeeDecimal<T>.Quantum(IeeeDecimal<T>.Sum([P("1E2")]).Value).Value);
        Assert.Equal(4,IeeeDecimal<T>.Quantum(IeeeDecimal<T>.Dot([P("1E2")],[P("1E2")]).Value).Value);
    }    [Fact]
    public void WideLogIntervalsUseMonotonicEndpointEnclosures()
    {
        // Directed MPFR log(10) endpoints from the same versioned 80-digit reference corpus above.
        const string log10Lower="2.3025850929940456840179914546843642076011014886287729760333279009675726096773524";
        const string log10Upper="2.3025850929940456840179914546843642076011014886287729760333279009675726096773525";
        foreach(int digits in new[]{3,12,48}) {
            var math=new DecimalIntervalMath(digits);
            var broad=math.Log(new(math.Scale,10*math.Scale));Contains(broad,"0",log10Upper,digits,"log([1,10])");
            var crossing=math.Log(new(99*math.Scale/100,101*math.Scale/100));Assert.True(crossing.Lower<0);Assert.True(crossing.Upper>0);
            var exact=math.Log(math.One);Assert.Equal(math.Zero,exact);
        }
        var precise=new DecimalIntervalMath(12);
        string upper=PeterO.Numbers.EDecimal.FromString(log10Upper).Multiply(10).ToString();
        string lower=PeterO.Numbers.EDecimal.FromString(log10Upper).Multiply(-10).ToString();
        Contains(precise.Log(new(precise.Scale/BigInteger.Pow(10,10),precise.Scale*BigInteger.Pow(10,10))),lower,upper,12,"log([1E-10,1E10])");
        Assert.True(PeterO.Numbers.EDecimal.FromString(log10Lower).CompareTo(PeterO.Numbers.EDecimal.FromString(log10Upper))<0);
    }

    [Fact]
    public void InternalIntervalBoundaryInputsPreserveDomainAndExactIdentities()
    {
        var math=new DecimalIntervalMath(3);
        Assert.Equal(math.Integer(200),math.Decimal(new(false,2,2)));
        Assert.Equal(math.One,math.Exp(math.Zero));Assert.Equal(math.Zero,math.Atan(math.Zero));
        var trig=math.SinCos(math.Zero);Assert.Equal(math.Zero,trig.Sin);Assert.Equal(math.One,trig.Cos);
        Assert.Throws<IntervalRefinementRequiredException>(()=>math.Divide(math.One,new DecimalIntervalMath.Interval(0,math.Scale)));
        Assert.Throws<IntervalRefinementRequiredException>(()=>math.Divide(math.One,new DecimalIntervalMath.Interval(-math.Scale,0)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>math.Divide(math.One,BigInteger.Zero));
        var range=math.Exp(new(0,math.Scale/8));Assert.True(range.Lower<=math.Scale);Assert.True(range.Upper>=1134);
        foreach(var n in new[]{BigInteger.Pow(10,100)-1,BigInteger.Pow(10,100),BigInteger.Pow(10,100)+1}) {
            var root=DecimalIntervalMath.IntegerSqrt(n);Assert.True(root*root<=n);Assert.True((root+1)*(root+1)>n);
        }
    }    [Fact]
    public void NearOneLogAtLargePrecisionContainsAnIndependentAnalyticalBracket()
    {
        var math=new DecimalIntervalMath(6209);var offset=BigInteger.Pow(10,66);
        var result=math.Log(new(math.Scale+offset,math.Scale+offset));
        // x/(1+x) <= log(1+x) <= x for x>0. Cross products keep this reference exact at x=10^-6143.
        Assert.True(result.Lower*(math.Scale+offset)<=math.Scale*offset);
        Assert.True(result.Upper>=offset);
    }}
