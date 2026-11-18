using System.Globalization;
using System.Numerics;
using PeterO.Numbers;

namespace SharpSafeNumerics.IeeeSoak;

/// <summary>Deterministic shrinkable recipes with independent, test-only numerical references.</summary>
internal static class CaseVerifier
{
    internal const int OperationCount=16;
    private static readonly string[] Names=["Add","Subtract","Multiply","Divide","FusedMultiplyAdd","Sqrt","Quantize","Remainder","FromInteger","ToIntegerInt64","FromBinary64","ToBinary64","Exp","Log","ExpM1","LogP1"];
    internal static string Describe(UInt128 a,UInt128 b)=>$"decimal{new[]{32,64,128}[(int)(a%3)]}/{Names[(int)(b%OperationCount)]}/a={a:X32}/b={b:X32}";
    internal static void Verify(UInt128 a,UInt128 b,Action<CaseObservation> observe)
    {
        switch((int)(a%3))
        {case 0:Verify<Decimal32>(a,b,EContext.Decimal32,7,-101,90,observe);break;case 1:Verify<Decimal64>(a,b,EContext.Decimal64,16,-398,369,observe);break;default:Verify<Decimal128>(a,b,EContext.Decimal128,34,-6176,6111,observe);break;}
    }
    private static void Verify<T>(UInt128 bits,UInt128 other,EContext format,int precision,int qmin,int qmax,Action<CaseObservation> observe) where T:struct,IFloatingPointIeee754<T>
    {
        int operation=(int)(other%OperationCount);string op=Names[operation];
        T a=Operand<T>(bits,precision,qmin,qmax),b=Operand<T>(other,precision,qmin,qmax),z=Operand<T>(bits^other^((UInt128)0x12345<<64),precision,qmin,qmax);
        if(operation>=12)
        {
            int coefficient=(int)((bits>>12)%40001)-20000;
            string text=operation==13?$"{(bits>>12)%999999+1}E-3":operation==15?$"{(int)((bits>>12)%6001)-900}E-3":$"{coefficient}E-3";
            if(operation is 14 or 15 && ((bits>>8)&15)==0)text=((bits&8)!=0?"-":"")+"1E-"+(20+(int)((bits>>16)%61));
            a=T.Parse(text,CultureInfo.InvariantCulture);
        }
        EDecimal x=Reference(a),y=Reference(b),c=Reference(z);
        foreach(var mode in Enum.GetValues<IeeeDecimalRounding>())
        {
            var context=new IeeeDecimalContext(mode);EContext oracle=format.WithRounding(Rounding(mode)).WithBlankFlags();
            CaseObservation observation;
            if(operation<8)
            {
                // GDA's finite-precision remainder-near rejects a quotient that does not
                // fit its context. IEEE remainder has no such quotient-precision limit.
                EContext remainderContext=EContext.Unlimited.WithBlankFlags();
                EDecimal expected=operation switch{0=>x.Add(y,oracle),1=>x.Subtract(y,oracle),2=>x.Multiply(y,oracle),3=>x.Divide(y,oracle),4=>x.MultiplyAndAdd(y,c,oracle),5=>x.Sqrt(oracle),6=>x.Quantize(y,oracle),_=>x.RemainderNear(y,remainderContext).RoundToPrecision(oracle)};
                var actual=operation switch{0=>IeeeDecimal<T>.Add(a,b,context),1=>IeeeDecimal<T>.Subtract(a,b,context),2=>IeeeDecimal<T>.Multiply(a,b,context),3=>IeeeDecimal<T>.Divide(a,b,context),4=>IeeeDecimal<T>.FusedMultiplyAdd(a,b,z,context),5=>IeeeDecimal<T>.Sqrt(a,context),6=>IeeeDecimal<T>.Quantize(a,b,context),_=>IeeeDecimal<T>.Remainder(a,b,context)};
                observation=Compare(op,mode,x,y,c,expected,Flags(oracle.Flags|remainderContext.Flags),actual,true);
            }
            else if(operation==8)
            {
                BigInteger integer=(BigInteger)bits;if((other&8)!=0)integer=-integer;
                int exponent=(int)((other>>8)%81);if(((other>>4)&15)==15)exponent=7000;
                integer*=BigInteger.Pow(10,exponent);
                EDecimal source=EDecimal.FromString(integer.ToString(CultureInfo.InvariantCulture));
                EDecimal expected=source.RoundToPrecision(oracle);
                observation=Compare(op,mode,source,y,c,expected,Flags(oracle.Flags),IeeeDecimal<T>.FromInteger(integer,context),false);
            }
            else if(operation==9)observation=IntegerConversion(a,x,mode);
            else if(operation==10)
            {
                double source=BitConverter.UInt64BitsToDouble((ulong)other);
                EDecimal exact=EDecimal.FromEFloat(EFloat.FromDouble(source));EDecimal expected=exact.RoundToPrecision(oracle);
                observation=Compare(op,mode,exact,y,c,expected,Flags(oracle.Flags),IeeeDecimal<T>.FromBinary(source,context),false);
            }
            else if(operation==11)
            {
                // PeterO's radix conversion preserves a signaling NaN representation;
                // IEEE arithmetic conversion additionally quiets/signals it. Apply the
                // reference precision operation to establish that required boundary policy.
                EContext binary=EContext.Binary64.WithRounding(Rounding(mode)).WithBlankFlags();EFloat expected=x.ToEFloat(binary).RoundToPrecision(binary);
                var actual=IeeeDecimal<T>.ToBinary<double>(a,context);
                ulong expectedBits=unchecked((ulong)expected.ToDoubleBits());ulong actualBits=actual.IsSuccess?BitConverter.DoubleToUInt64Bits(actual.Value):0;
                bool value=actual.IsSuccess&&(expected.IsNaN()?double.IsNaN(actual.Value)&&(actualBits&(1UL<<51))!=0:actualBits==expectedBits);
                int flags=Flags(binary.Flags);string outcome=value&&flags==(int)actual.Flags?"Passed":"Failed";string? reason=null;
                if(value&&(flags^(int)actual.Flags)==(int)IeeeDecimalFlags.Underflow){outcome="Inconclusive";reason="Independent binary tininess-policy boundary";}
                observation=new(outcome,typeof(T).Name,op,mode.ToString(),x.ToString(),"","",$"0x{expectedBits:X16}",$"0x{actualBits:X16}",flags,(int)actual.Flags,reason);
            }
            else
            {
                EDecimal low=Elementary(x,operation,160),high=Elementary(x,operation,224);
                EDecimal expected=low.RoundToPrecision(oracle),second=high.RoundToPrecision(format.WithRounding(Rounding(mode)));
                if(expected.CompareTo(second)!=0||expected.IsZero&&second.IsZero&&expected.IsNegative!=second.IsNegative)
                    observation=new("Inconclusive",typeof(T).Name,op,mode.ToString(),x.ToString(),"","",expected.ToString(),second.ToString(),0,0,"160/224-digit target rounding disagrees",0);
                else
                {
                    var actual=operation switch{12=>IeeeDecimal<T>.Exp(a,context),13=>IeeeDecimal<T>.Log(a,context),14=>IeeeDecimal<T>.ExpM1(a,context),_=>IeeeDecimal<T>.LogP1(a,context)};
                    bool exact=operation==13?x.CompareTo(EDecimal.One)==0:x.IsZero;
                    observation=Compare(op,mode,x,y,c,expected,exact?0:(int)IeeeDecimalFlags.Inexact,actual,false);
                }
            }
            observe(observation);if(observation.Outcome=="Failed")throw new OracleMismatchException(observation);
        }
    }

    private static EDecimal Elementary(EDecimal x,int operation,int precision)
    {
        EContext context=EContext.ForPrecisionAndRounding(precision,ERounding.HalfEven);
        return operation switch{12=>x.Exp(context),13=>x.Log(context),14=>x.Exp(context).Subtract(EDecimal.One,context),_=>x.Add(EDecimal.One).Log(context)};
    }

    private static CaseObservation IntegerConversion<T>(T value,EDecimal source,IeeeDecimalRounding mode) where T:struct,IFloatingPointIeee754<T>
    {
        EContext oracle=EContext.Unlimited.WithRounding(Rounding(mode)).WithBlankFlags();
        BigInteger integer=0;int expectedFlags;
        if(!source.IsFinite)expectedFlags=(int)IeeeDecimalFlags.Invalid;
        else
        {
            EDecimal rounded=source.RoundToIntegerExact(oracle);integer=BigInteger.Parse(rounded.ToEIntegerIfExact().ToString(),CultureInfo.InvariantCulture);
            if(integer<long.MinValue||integer>long.MaxValue){integer=0;expectedFlags=(int)IeeeDecimalFlags.Invalid;}else expectedFlags=Flags(oracle.Flags);
        }
        var actual=IeeeDecimal<T>.ToInteger<long>(value,new(mode));
        bool passed=actual.IsSuccess&&new BigInteger(actual.Value)==integer&&(int)actual.Flags==expectedFlags;
        return new(passed?"Passed":"Failed",typeof(T).Name,"ToIntegerInt64",mode.ToString(),source.ToString(),"","",integer.ToString(CultureInfo.InvariantCulture),actual.IsSuccess?actual.Value.ToString(CultureInfo.InvariantCulture):actual.Error.ToString(),expectedFlags,(int)actual.Flags);
    }

    private static CaseObservation Compare<T>(string operation,IeeeDecimalRounding mode,EDecimal x,EDecimal y,EDecimal z,EDecimal expected,int expectedFlags,IeeeDecimalResult<T> actual,bool cohort) where T:struct,IFloatingPointIeee754<T>
    {
        EDecimal? value=actual.IsSuccess?Reference(actual.Value):null;
        bool equal=value is not null&&(expected.IsNaN()?value.IsNaN()&&!IeeeDecimal<T>.IsSignaling(actual.Value).Value:cohort?expected.CompareToTotal(value)==0:expected.CompareTo(value)==0&&(!expected.IsZero||value.IsNegative==expected.IsNegative));
        bool pass=actual.IsSuccess&&equal&&(int)actual.Flags==expectedFlags;
        return new(pass?"Passed":"Failed",typeof(T).Name,operation,mode.ToString(),x.ToString(),y.ToString(),z.ToString(),expected.ToString(),value?.ToString()??actual.Error.ToString(),expectedFlags,(int)actual.Flags);
    }

    private static T Operand<T>(UInt128 bits,int precision,int qmin,int qmax) where T:struct,IFloatingPointIeee754<T>
    {
        bool negative=(bits&8)!=0;int recipe=(int)((bits>>4)&63);
        if(recipe==1)return T.PositiveInfinity;if(recipe==2)return T.NegativeInfinity;
        if(recipe is 3 or 4)return IeeeDecimal<T>.CreateNaN((BigInteger)(bits%999999),recipe==4).Value;
        int shape=(int)((bits>>6)&7);
        int quantum=shape==0?qmin:shape==1?qmax:(int)((bits>>64)%121)-60;
        BigInteger coefficient=recipe==0?0:(BigInteger)(bits>>12)%BigInteger.Pow(10,precision);
        string text=(negative?"-":"")+coefficient.ToString(CultureInfo.InvariantCulture)+"E"+quantum.ToString(CultureInfo.InvariantCulture);
        return T.Parse(text,CultureInfo.InvariantCulture);
    }

    private static EDecimal Reference<T>(T value) where T:struct,IFloatingPointIeee754<T>
    {
        if(T.IsNaN(value))return EDecimal.CreateNaN(EInteger.FromString(IeeeDecimal<T>.GetPayload(value).Value.ToString(CultureInfo.InvariantCulture)),IeeeDecimal<T>.IsSignaling(value).Value,T.IsNegative(value),null);
        if(T.IsPositiveInfinity(value))return EDecimal.PositiveInfinity;if(T.IsNegativeInfinity(value))return EDecimal.NegativeInfinity;
        return EDecimal.FromString(value.ToString(null,CultureInfo.InvariantCulture));
    }
    private static ERounding Rounding(IeeeDecimalRounding mode)=>mode switch{IeeeDecimalRounding.ToNearestEven=>ERounding.HalfEven,IeeeDecimalRounding.ToNearestAway=>ERounding.HalfUp,IeeeDecimalRounding.TowardZero=>ERounding.Down,IeeeDecimalRounding.TowardNegative=>ERounding.Floor,_=>ERounding.Ceiling};
    private static int Flags(int flags)=>((flags&EContext.FlagInvalid)!=0?1:0)|((flags&EContext.FlagDivideByZero)!=0?2:0)|((flags&EContext.FlagOverflow)!=0?4:0)|((flags&EContext.FlagUnderflow)!=0?8:0)|((flags&EContext.FlagInexact)!=0?16:0);
}
