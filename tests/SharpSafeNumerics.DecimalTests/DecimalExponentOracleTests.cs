// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;
using PeterO.Numbers;
using SharpSafeNumerics;
using Xunit;

public class DecimalExponentOracleTests
{
    [Fact] public void ExponentDistributedOracleSeed731928()
    {
        Check<Decimal32>(EContext.Decimal32,7,-101,90);
        Check<Decimal64>(EContext.Decimal64,16,-398,369);
        Check<Decimal128>(EContext.Decimal128,34,-6176,6111);
    }
    private static void Check<T>(EContext context,int precision,int minExponent,int maxExponent) where T : struct, IFloatingPointIeee754<T>
    {
        var random=new Random(731928);
        for(int i=0;i<160;i++)
        {
            T a=Operand<T>(random,precision,minExponent,maxExponent);
            T b=i%8==0 ? -a : Operand<T>(random,precision,minExponent,maxExponent);
            var x=EDecimal.FromString(a.ToString("R",CultureInfo.InvariantCulture));
            var y=EDecimal.FromString(b.ToString("R",CultureInfo.InvariantCulture));
            CheckResult(SafeDecimal.TryAdd(a,b,DecimalRounding.ToEven),SafeDecimal.TryAdd(a,b),x.Add(y,context),x.Add(y),null);
            CheckResult(SafeDecimal.TrySubtract(a,b,DecimalRounding.ToEven),SafeDecimal.TrySubtract(a,b),x.Subtract(y,context),x.Subtract(y),null);
            CheckResult(SafeDecimal.TryMultiply(a,b,DecimalRounding.ToEven),SafeDecimal.TryMultiply(a,b),x.Multiply(y,context),x.Multiply(y),null);
            if(!T.IsZero(b)) CheckResult(SafeDecimal.TryDivide(a,b,DecimalRounding.ToEven),SafeDecimal.TryDivide(a,b),x.Divide(y,context),x,y);
        }
        // Arithmetic halfway cases require retaining an odd/even last digit in every width.
        foreach(string sign in new[]{"","-"})
        {
            var half=T.Parse(sign+"5E-"+precision,CultureInfo.InvariantCulture);
            foreach(int last in new[]{0,1})
            {
                string digits="1."+new string('0',precision-2)+last;
                T a=T.Parse(sign+digits,CultureInfo.InvariantCulture);
                var x=EDecimal.FromString(a.ToString("R",CultureInfo.InvariantCulture));
                var y=EDecimal.FromString(half.ToString("R",CultureInfo.InvariantCulture));
                CheckResult(SafeDecimal.TryAdd(a,half,DecimalRounding.ToEven),SafeDecimal.TryAdd(a,half),x.Add(y,context),x.Add(y),null);
            }
        }
    }
    private static T Operand<T>(Random random,int precision,int minExponent,int maxExponent) where T : struct, IFloatingPointIeee754<T>
    {
        var digits=new char[precision]; digits[0]=(char)('1'+random.Next(9));
        for(int i=1;i<digits.Length;i++) digits[i]=(char)('0'+random.Next(10));
        int exponent=random.Next(minExponent,maxExponent+1);
        return T.Parse((random.Next(2)==0?"-":"")+new string(digits)+"E"+exponent,CultureInfo.InvariantCulture);
    }
    private static void CheckResult<T>(DecimalResult<T> rounded,DecimalResult<T> strict,EDecimal expected,EDecimal exact,EDecimal? divisor) where T : struct, IFloatingPointIeee754<T>
    {
        if(expected.IsInfinity())
        {
            var error=expected.IsNegative?ArithmeticError.Underflow:ArithmeticError.Overflow;
            Assert.Equal(error,rounded.Error); Assert.Equal(error,strict.Error); return;
        }
        Assert.True(rounded.IsSuccess);
        Assert.Equal(T.Parse(expected.ToString(),CultureInfo.InvariantCulture),rounded.Value);
        var represented=EDecimal.FromString(rounded.Value.ToString("R",CultureInfo.InvariantCulture));
        bool isExact=(divisor is null?represented:represented.Multiply(divisor)).CompareTo(exact)==0;
        Assert.Equal(!isExact,rounded.IsInexact);
        Assert.Equal(isExact?ArithmeticError.None:ArithmeticError.Inexact,strict.Error);
    }
}
