// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;
using PeterO.Numbers;
using SharpSafeNumerics;
using Xunit;

namespace SharpSafeNumerics.DecimalTests;

/// <summary>Proposed semantic counterexamples for prior-campaign survivors; staged during source freeze.</summary>
public sealed class IeeeFunctionFollowupTests
{
    public static IEnumerable<object[]> FormatsAndModes()
    {
        foreach (int format in new[] { 32,64,128 })
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>()) yield return [format,mode];
    }

    [Theory,MemberData(nameof(FormatsAndModes))]
    public void EvenZeroRootsAndSignalingIdentity(int format,IeeeDecimalRounding mode)
    { if(format==32) ZeroRoots<Decimal32>(mode);else if(format==64) ZeroRoots<Decimal64>(mode);else ZeroRoots<Decimal128>(mode); }

    private static void ZeroRoots<T>(IeeeDecimalRounding mode) where T:struct,IFloatingPointIeee754<T>
    {
        var c=new IeeeDecimalContext(mode); T zero=D<T>("-0.00");
        var root=IeeeDecimal<T>.RootN(zero,4,c);
        Assert.Equal(T.Zero,root.Value);Assert.False(T.IsNegative(root.Value));Assert.Equal(-1,IeeeDecimal<T>.Quantum(root.Value).Value);Assert.Equal(IeeeDecimalFlags.None,root.Flags);
        var pole=IeeeDecimal<T>.RootN(zero,-4,c);Assert.True(T.IsPositiveInfinity(pole.Value));Assert.Equal(IeeeDecimalFlags.DivisionByZero,pole.Flags);
        Invalid(IeeeDecimal<T>.RootN(T.NegativeInfinity,4,c));
        Invalid(IeeeDecimal<T>.RootN(D<T>("-4.00"),2,c));
        foreach(bool signaling in new[]{false,true})
        {
            T nan=IeeeDecimal<T>.CreateNaN(123,signaling).Value;
            var identity=IeeeDecimal<T>.RootN(nan,1,c);
            Assert.True(T.IsNaN(identity.Value));Assert.False(IeeeDecimal<T>.IsSignaling(identity.Value).Value);
            Assert.Equal(new BigInteger(123),IeeeDecimal<T>.GetPayload(identity.Value).Value);
            Assert.Equal(signaling?IeeeDecimalFlags.Invalid:IeeeDecimalFlags.None,identity.Flags);
        }
        var even=IeeeDecimal<T>.PowN(zero,2,c);Assert.Equal(T.Zero,even.Value);Assert.False(T.IsNegative(even.Value));Assert.Equal(-4,IeeeDecimal<T>.Quantum(even.Value).Value);Assert.Equal(IeeeDecimalFlags.None,even.Flags);
        var odd=IeeeDecimal<T>.Pow(zero,T.CreateChecked(3),c);Assert.Equal(T.Zero,odd.Value);Assert.True(T.IsNegative(odd.Value));Assert.Equal(-6,IeeeDecimal<T>.Quantum(odd.Value).Value);Assert.Equal(IeeeDecimalFlags.None,odd.Flags);
        var half=IeeeDecimal<T>.Pow(zero,D<T>("0.5"),c);Assert.Equal(T.Zero,half.Value);Assert.False(T.IsNegative(half.Value));Assert.Equal(-1,IeeeDecimal<T>.Quantum(half.Value).Value);Assert.Equal(IeeeDecimalFlags.None,half.Flags);
        var inverseEven=IeeeDecimal<T>.PowN(T.NegativeInfinity,-2,c);Assert.Equal(T.Zero,inverseEven.Value);Assert.False(T.IsNegative(inverseEven.Value));Assert.Equal(IeeeDecimalFlags.None,inverseEven.Flags);
        var inverseOdd=IeeeDecimal<T>.Pow(T.NegativeInfinity,T.CreateChecked(-3),c);Assert.Equal(T.Zero,inverseOdd.Value);Assert.True(T.IsNegative(inverseOdd.Value));Assert.Equal(IeeeDecimalFlags.None,inverseOdd.Flags);
    }

    [Theory,MemberData(nameof(FormatsAndModes))]
    public void PowRDoesNotInheritPowNaNSuppression(int format,IeeeDecimalRounding mode)
    { if(format==32) PowRDomain<Decimal32>(mode);else if(format==64) PowRDomain<Decimal64>(mode);else PowRDomain<Decimal128>(mode); }

    private static void PowRDomain<T>(IeeeDecimalRounding mode) where T:struct,IFloatingPointIeee754<T>
    {
        var c=new IeeeDecimalContext(mode);T nan=IeeeDecimal<T>.CreateNaN(321).Value;
        foreach(var r in new[]{IeeeDecimal<T>.PowR(T.One,nan,c),IeeeDecimal<T>.PowR(nan,T.Zero,c)})
        {Assert.True(T.IsNaN(r.Value));Assert.Equal(new BigInteger(321),IeeeDecimal<T>.GetPayload(r.Value).Value);Assert.Equal(IeeeDecimalFlags.None,r.Flags);}
        var one=IeeeDecimal<T>.PowR(T.One,T.CreateChecked(2),c);Assert.Equal(T.One,one.Value);Assert.Equal(IeeeDecimalFlags.None,one.Flags);
        foreach(var r in new[]{IeeeDecimal<T>.PowR(T.CreateChecked(2),T.PositiveInfinity,c),IeeeDecimal<T>.PowR(T.PositiveInfinity,T.CreateChecked(2),c)})
        {Assert.True(T.IsPositiveInfinity(r.Value));Assert.Equal(IeeeDecimalFlags.None,r.Flags);}
        foreach(var r in new[]{IeeeDecimal<T>.PowR(D<T>("0.5"),T.PositiveInfinity,c),IeeeDecimal<T>.PowR(T.PositiveInfinity,T.CreateChecked(-2),c)})
        {Assert.Equal(T.Zero,r.Value);Assert.False(T.IsNegative(r.Value));Assert.Equal(IeeeDecimalFlags.None,r.Flags);}
    }

    [Theory,MemberData(nameof(FormatsAndModes))]
    public void PositiveQuantumDoesNotChangePeriodicityOrDomainArithmetic(int format,IeeeDecimalRounding mode)
    { if(format==32) PositiveQuantum<Decimal32>(mode);else if(format==64) PositiveQuantum<Decimal64>(mode);else PositiveQuantum<Decimal128>(mode); }

    private static void PositiveQuantum<T>(IeeeDecimalRounding mode) where T:struct,IFloatingPointIeee754<T>
    {
        var c=new IeeeDecimalContext(mode);
        foreach(bool negative in new[]{false,true})
        {
            T x=D<T>(negative?"-5E1":"5E1");Assert.Equal(1,IeeeDecimal<T>.Quantum(x).Value);
            var sin=IeeeDecimal<T>.SinPi(x,c);Assert.Equal(T.Zero,sin.Value);Assert.Equal(negative,T.IsNegative(sin.Value));Assert.Equal(IeeeDecimalFlags.None,sin.Flags);
            var cos=IeeeDecimal<T>.CosPi(x,c);Assert.Equal(T.One,cos.Value);Assert.Equal(IeeeDecimalFlags.None,cos.Flags);
            var tan=IeeeDecimal<T>.TanPi(x,c);Assert.Equal(T.Zero,tan.Value);Assert.Equal(negative,T.IsNegative(tan.Value));Assert.Equal(IeeeDecimalFlags.None,tan.Flags);
        }
        // Since 16<20<32 and log2 is strictly increasing, these independent bounds
        // reject a bogus exact-power recognition without guessing a rounded constant.
        var log=IeeeDecimal<T>.Log2(D<T>("2E1"),c);
        Assert.True(log.Value>T.CreateChecked(4)&&log.Value<T.CreateChecked(5));Assert.Equal(IeeeDecimalFlags.Inexact,log.Flags);
        var exact=IeeeDecimal<T>.Compound(D<T>("1E1"),2,c);Assert.Equal(T.CreateChecked(121),exact.Value);Assert.Equal(IeeeDecimalFlags.None,exact.Flags);
        var oracleContext=(typeof(T)==typeof(Decimal32)?EContext.Decimal32:typeof(T)==typeof(Decimal64)?EContext.Decimal64:EContext.Decimal128).WithRounding(R(mode));
        var expected=EDecimal.One.Divide(EDecimal.FromInt32(121),oracleContext);
        var inverse=IeeeDecimal<T>.Compound(D<T>("1E1"),-2,c);Assert.Equal(D<T>(expected.ToString()),inverse.Value);Assert.Equal(IeeeDecimalFlags.Inexact,inverse.Flags);
    }
    [Theory,MemberData(nameof(FormatsAndModes))]
    public void ScalingAndOddQuantumCases(int format,IeeeDecimalRounding mode)
    { if(format==32) ScaleCases<Decimal32>(mode);else if(format==64) ScaleCases<Decimal64>(mode);else ScaleCases<Decimal128>(mode); }

    private static void ScaleCases<T>(IeeeDecimalRounding mode) where T:struct,IFloatingPointIeee754<T>
    {
        var c=new IeeeDecimalContext(mode);
        var reciprocal=IeeeDecimal<T>.ReciprocalSqrt(D<T>("4.0"),c);
        Assert.Equal(D<T>("0.5"),reciprocal.Value);Assert.Equal(-1,IeeeDecimal<T>.Quantum(reciprocal.Value).Value);Assert.Equal(IeeeDecimalFlags.None,reciprocal.Flags);
        var hypotenuse=IeeeDecimal<T>.Hypot(D<T>("3E1"),D<T>("40.0"),c);
        Assert.Equal(D<T>("50.0"),hypotenuse.Value);Assert.Equal(-1,IeeeDecimal<T>.Quantum(hypotenuse.Value).Value);Assert.Equal(IeeeDecimalFlags.None,hypotenuse.Flags);
        var power=IeeeDecimal<T>.Exp2(D<T>("1E1"),c);Assert.Equal(T.CreateChecked(1024),power.Value);Assert.Equal(IeeeDecimalFlags.None,power.Flags);
        var shifted=IeeeDecimal<T>.Log2P1(D<T>("3E1"),c);
        Assert.True(shifted.Value>T.CreateChecked(4)&&shifted.Value<T.CreateChecked(5));Assert.Equal(IeeeDecimalFlags.Inexact,shifted.Flags);
        // With 3.14159<pi<3.14160, 81*pi<256<81.5*pi. Reducing by80*pi
        // places this angle strictly in the third quadrant, where sine is negative.
        // This rejects a hyperbolic saturation shortcut accidentally applied to sine.
        var sine=IeeeDecimal<T>.Sin(T.CreateChecked(256),c);
        Assert.True(sine.Value<T.Zero&&sine.Value>-T.One);Assert.Equal(IeeeDecimalFlags.Inexact,sine.Flags);
    }

    [Theory,MemberData(nameof(FormatsAndModes))]
    public void ExactNormalBoundaryDistinguishesTinyBeforeRounding(int format,IeeeDecimalRounding mode)
    { if(format==32) NormalBoundary<Decimal32>(mode,-95,-101);else if(format==64) NormalBoundary<Decimal64>(mode,-383,-398);else NormalBoundary<Decimal128>(mode,-6143,-6176); }

    private static void NormalBoundary<T>(IeeeDecimalRounding mode,int emin,int minimumQuantum) where T:struct,IFloatingPointIeee754<T>
    {
        var c=new IeeeDecimalContext(mode);
        EDecimal boundary=EDecimal.FromString($"1E{emin}"),ulp=EDecimal.FromString($"1E{minimumQuantum}");
        T x=D<T>(boundary.ToString());
        // For tiny positive x, 0<log(1+x)<x<exp(x)-1, and both residuals
        // are far below half an ulp. IEEE decimal tininess uses the exact result
        // BEFORE rounding, so the logarithm raises Underflow even when rounding to x.
        T expectedLog=D<T>((mode is IeeeDecimalRounding.TowardZero or IeeeDecimalRounding.TowardNegative?boundary.Subtract(ulp):boundary).ToString());
        var logarithm=IeeeDecimal<T>.LogP1(x,c);
        Assert.Equal(expectedLog,logarithm.Value);Assert.Equal(IeeeDecimalFlags.Underflow|IeeeDecimalFlags.Inexact,logarithm.Flags);
        T expectedExp=D<T>((mode==IeeeDecimalRounding.TowardPositive?boundary.Add(ulp):boundary).ToString());
        var exponential=IeeeDecimal<T>.ExpM1(x,c);
        Assert.Equal(expectedExp,exponential.Value);Assert.Equal(IeeeDecimalFlags.Inexact,exponential.Flags);
        var trapped=IeeeDecimal<T>.LogP1(x,new(mode,IeeeDecimalFlags.Underflow));
        Assert.Equal(ArithmeticError.Underflow,trapped.Error);Assert.Equal(IeeeDecimalFlags.Underflow|IeeeDecimalFlags.Inexact,trapped.Flags);
    }

    private static T D<T>(string s) where T:struct,IFloatingPointIeee754<T> =>T.Parse(s,CultureInfo.InvariantCulture);
    private static ERounding R(IeeeDecimalRounding m)=>m switch{IeeeDecimalRounding.ToNearestEven=>ERounding.HalfEven,IeeeDecimalRounding.ToNearestAway=>ERounding.HalfUp,IeeeDecimalRounding.TowardZero=>ERounding.Down,IeeeDecimalRounding.TowardNegative=>ERounding.Floor,_=>ERounding.Ceiling};
    private static void Invalid<T>(IeeeDecimalResult<T> r) where T:struct,IFloatingPointIeee754<T>{Assert.True(r.IsSuccess);Assert.True(T.IsNaN(r.Value));Assert.Equal(IeeeDecimalFlags.Invalid,r.Flags);}
}
