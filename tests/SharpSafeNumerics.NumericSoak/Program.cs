// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CsCheck;
using PeterO.Numbers;
using SharpSafeNumerics;

// Nonshipping qualification entry point. CsCheck owns generation and shrinking;
// all case choices are functions of its two shrinkable UInt128 inputs.
return Campaign.Run(args);

internal static class Campaign
{
    private static long samples, assertions, fixedOperations, decimalOperations, conversions, rawChecks;
    private static readonly long[] Edges = [long.MinValue, long.MinValue+1, long.MaxValue, long.MaxValue-1, 0, 1, -1, 3, -3, 1L<<32, (1L<<32)-1, (1L<<32)+1];
    private static readonly Stopwatch Timer = new();
    private static readonly long[] WidthCases = new long[3], FixedCases = new long[6], DecimalCases = new long[4];
    private static double nextCheckpoint = 300;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    internal static int Run(string[] args)
    {
        int seconds=900, seedNumber=731930;
        string? summary=null;
        var seen=new HashSet<string>(StringComparer.Ordinal);
        if(args.Length%2!=0) return Invalid();
        for(int i=0;i<args.Length;i+=2)
        {
            if(!seen.Add(args[i])) return Invalid();
            if(args[i]=="--summary") { summary=args[i+1]; continue; }
            if(!int.TryParse(args[i+1],NumberStyles.None,Inv,out int n)) return Invalid();
            switch(args[i])
            {
                case "--seconds" when n is >=1 and <=7200: seconds=n; break;
                case "--seed" when n>0: seedNumber=n; break;
                default:return Invalid();
            }
        }
        if(summary is not null)
        {
            try
            {
                summary=Path.GetFullPath(summary);
                if(!Directory.Exists(Path.GetDirectoryName(summary)) || File.Exists(summary) || Directory.Exists(summary)) return Invalid();
            }
            catch(Exception e) when(e is ArgumentException or NotSupportedException or PathTooLongException) { return Invalid(); }
        }
        string seed=new PCG(1,(ulong)seedNumber).ToString();
        long allocated=GC.GetTotalAllocatedBytes(true);
        int[] gc=[GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)];
        DateTime start=DateTime.UtcNow;
        Console.WriteLine($"Numeric soak start UTC={start:O}; seconds={seconds}; seed={seedNumber}; CsCheck={seed}; workers=1; runtime={RuntimeInformation.FrameworkDescription}; architecture={RuntimeInformation.ProcessArchitecture}; pointer-bits={IntPtr.Size*8}; server-GC={System.Runtime.GCSettings.IsServerGC}");
        bool passed=false; string? failure=null;
        ValidateOperandDistribution();
        Timer.Start();
        try
        {
            Gen.Select(Gen.UInt128,Gen.UInt128).Sample(pair=>
            {
                UInt128 a=pair.Item1,b=pair.Item2;
                Fixed(a,b);
                int width=(int)((b>>64)%3); WidthCases[width]++;
                switch(width)
                {
                    case 0: Decimal<Decimal32>(a,b,EContext.Decimal32,7,-101,90); Raw(Decimal32.DecodeBinary((uint)b)); break;
                    case 1: Decimal<Decimal64>(a,b,EContext.Decimal64,16,-398,369); Raw(Decimal64.DecodeBinary((ulong)b)); break;
                    default: Decimal<Decimal128>(a,b,EContext.Decimal128,34,-6176,6111); Raw(Decimal128.DecodeBinary(b)); break;
                }
                samples++;
                if(Timer.Elapsed.TotalSeconds>=nextCheckpoint)
                {
                    Console.WriteLine($"checkpoint UTC={DateTime.UtcNow:O}; elapsed={Timer.Elapsed.TotalSeconds:F3}; samples={samples}; assertions={assertions}; fixed={fixedOperations}; decimal={decimalOperations}; conversions={conversions}; raw={rawChecks}");
                    nextCheckpoint+=300;
                }
            },seed:seed,iter:long.MaxValue,time:seconds,threads:1);
            passed=true;
        }
        catch(Exception e) { failure=e.ToString(); Console.Error.WriteLine(failure); }
        Timer.Stop();
        var report=new CampaignReport { Passed=passed, Seed=seedNumber, CsCheckSeed=seed, StartUtc=start, EndUtc=DateTime.UtcNow, ElapsedSeconds=Timer.Elapsed.TotalSeconds, Samples=samples, Assertions=assertions, FixedOperations=fixedOperations, DecimalOperations=decimalOperations, Conversions=conversions, RawCases=rawChecks, Decimal32Cases=WidthCases[0], Decimal64Cases=WidthCases[1], Decimal128Cases=WidthCases[2], FixedCaseCounts=FixedCases, DecimalCaseCounts=DecimalCases, Runtime=RuntimeInformation.FrameworkDescription, Architecture=RuntimeInformation.ProcessArchitecture.ToString(), PointerBits=IntPtr.Size*8, ServerGC=System.Runtime.GCSettings.IsServerGC, AllocatedBytes=GC.GetTotalAllocatedBytes(true)-allocated, LiveHeapBytes=GC.GetTotalMemory(false), GC0=GC.CollectionCount(0)-gc[0], GC1=GC.CollectionCount(1)-gc[1], GC2=GC.CollectionCount(2)-gc[2], Failure=failure };
        string json=JsonSerializer.Serialize(report,CampaignJsonContext.Default.CampaignReport);
        Console.WriteLine(json);
        if(summary is not null)
        {
            // CreateNew prevents overwriting a file introduced after CLI validation.
            using var file=new FileStream(summary,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
            using var writer=new StreamWriter(file); writer.WriteLine(json);
        }
        return passed?0:1;
    }
    private static int Invalid() { Console.Error.WriteLine("Usage: --seconds 1..7200 --seed positive-integer --summary fresh-file-in-existing-parent (optional; no duplicates)."); return 2; }
    private static void Require(bool condition,string detail)
    {
        assertions++;
        if(!condition) throw new InvalidOperationException("Numeric oracle mismatch: "+detail);
    }
    private static long Operand(UInt128 bits) => (bits&3)==0 ? Edges[(int)((bits>>2)%(uint)Edges.Length)] : unchecked((long)bits);
    private static void Fixed(UInt128 a,UInt128 b)
    {
        long x=Operand(a),y=Operand(b);
        var left=Fixed64.FromRaw(x); var right=Fixed64.FromRaw(y);
        int op=(int)((a>>64)%6); FixedCases[op]++;
        // Every selected case exercises all rounding policies, including Exact.
        for(int m=0;m<6;m++)
        {
            var mode=(FixedRounding)m;
            var result=op switch {0=>SafeFixed.Add(left,right),1=>SafeFixed.Subtract(left,right),2=>SafeFixed.Multiply(left,right,mode),3=>SafeFixed.Divide(left,right,mode),4=>SafeFixed.Negate(left),_=>SafeFixed.FromRatio(x,y,mode)};
            BigInteger n=op switch {0=>(BigInteger)x+y,1=>(BigInteger)x-y,2=>(BigInteger)x*y,3 or 5=>(BigInteger)x<<32,_=>-(BigInteger)x};
            BigInteger d=op switch {2=>BigInteger.One<<32,3 or 5=>y,_=>BigInteger.One};
            FixedCheck(result,n,d,mode); fixedOperations++;
        }
    }
    private static void FixedCheck(NumericResult<Fixed64> result,BigInteger numerator,BigInteger denominator,FixedRounding mode)
    {
        ArithmeticError error=ArithmeticError.None; BigInteger q=0;
        if(denominator.IsZero) error=ArithmeticError.DivideByZero;
        else
        {
            // Positive-magnitude oracle: signed division/remainder implementation is
            // deliberately avoided. Apply the sign only after rounding magnitude.
            int sign=numerator.Sign*denominator.Sign;
            BigInteger den=BigInteger.Abs(denominator);
            q=BigInteger.DivRem(BigInteger.Abs(numerator),den,out var rem);
            if(!rem.IsZero)
            {
                if(mode==FixedRounding.Exact) error=ArithmeticError.Inexact;
                else if(mode==FixedRounding.AwayFromZero || mode==FixedRounding.Floor&&sign<0 || mode==FixedRounding.Ceiling&&sign>0 || mode==FixedRounding.ToEven&&(2*rem>den || 2*rem==den&&!q.IsEven)) q++;
            }
            q*=sign;
            if(error==ArithmeticError.None) error=q>long.MaxValue?ArithmeticError.Overflow:q<long.MinValue?ArithmeticError.Underflow:ArithmeticError.None;
        }
        Require(result.Error==error,$"fixed {numerator}/{denominator}, {mode}, expected={error}, actual={result.Error}");
        bool success=result.TryGetValue(out var value);
        Require(success==(error==ArithmeticError.None),"fixed success");
        Require(value.RawValue==(success?(long)q:0),"fixed value/clearing");
    }
    private static T Value<T>(UInt128 a,UInt128 b,int precision,int min,int max) where T:struct,IFloatingPointIeee754<T>
    {
        int kind=(int)((a>>96)%16);
        if(kind==0) return T.NaN;
        if(kind==1) return T.PositiveInfinity;
        if(kind==2) return T.NegativeInfinity;
        if(kind==3) return T.Zero;
        if(kind==4) return -T.Zero;
        if(kind==5) return T.BitIncrement(T.Zero);
        if(kind==6) return -T.BitIncrement(T.Zero);
        string digits=a.ToString(Inv).PadLeft(precision,'1')[..precision];
        int exponent=min+(int)((ulong)b%(uint)(max-min+1));
        return T.Parse(((b>>127)==0?"":"-")+digits+"E"+exponent,Inv);
    }
    /// <summary>Deterministically guards independence of operand sign and exponent parity.</summary>
    private static void ValidateOperandDistribution()
    {
        UInt128 coefficient=(UInt128)7<<96; // Select a finite, non-special operand.
        Decimal32 first=Value<Decimal32>(coefficient,0,7,-101,90);
        Decimal32 next=Value<Decimal32>(coefficient,1,7,-101,90);
        foreach(UInt128 exponent in new UInt128[]{0,1})
        {
            var positive=Value<Decimal32>(coefficient,exponent,7,-101,90);
            var negative=Value<Decimal32>(coefficient,exponent|((UInt128)1<<127),7,-101,90);
            Require(Decimal32.IsPositive(positive)&&Decimal32.IsNegative(negative),"generator sign at both exponent parities");
            Require(positive==-negative,"generator exponent independent of sign");
        }
        Require(next==first*Decimal32.CreateChecked(10),"generator adjacent exponent probe");
    }
    private static EDecimal Reference<T>(T value) where T:struct,IFloatingPointIeee754<T> => EDecimal.FromString(value.ToString("R",Inv));
    private static void Decimal<T>(UInt128 a,UInt128 b,EContext context,int precision,int min,int max) where T:struct,IFloatingPointIeee754<T>
    {
        T x=Value<T>(a,b,precision,min,max),y=Value<T>(b,a,precision,min,max);
        if((b&15)==0) y=-x;
        int op=(int)((a>>48)%4); DecimalCases[op]++;
        DecimalResult<T> Call(DecimalRounding mode)=>op switch {0=>SafeDecimal.TryAdd(x,y,mode),1=>SafeDecimal.TrySubtract(x,y,mode),2=>SafeDecimal.TryMultiply(x,y,mode),_=>SafeDecimal.TryDivide(x,y,mode)};
        var rounded=Call(DecimalRounding.ToEven); var strict=Call(DecimalRounding.Exact);
        decimalOperations+=2;
        if(!T.IsFinite(x)||!T.IsFinite(y)) { Rejected(rounded,ArithmeticError.NonFinite); Rejected(strict,ArithmeticError.NonFinite); }
        else if(op==3&&T.IsZero(y)) { Rejected(rounded,ArithmeticError.DivideByZero); Rejected(strict,ArithmeticError.DivideByZero); }
        else
        {
            EDecimal rx=Reference(x),ry=Reference(y);
            EDecimal expected=op switch {0=>rx.Add(ry,context),1=>rx.Subtract(ry,context),2=>rx.Multiply(ry,context),_=>rx.Divide(ry,context)};
            EDecimal exact=op switch {0=>rx.Add(ry),1=>rx.Subtract(ry),2=>rx.Multiply(ry),_=>rx};
            DecimalCheck(rounded,strict,expected,exact,op==3?ry:null);
        }
        switch((int)((b>>48)%3))
        {
            case 0: Convert<T,Decimal32>(x,EContext.Decimal32); break;
            case 1: Convert<T,Decimal64>(x,EContext.Decimal64); break;
            default: Convert<T,Decimal128>(x,EContext.Decimal128); break;
        }
    }
    private static void Convert<S,D>(S source,EContext context) where S:struct,IFloatingPointIeee754<S> where D:struct,IFloatingPointIeee754<D>
    {
        var rounded=SafeDecimal.TryConvert<S,D>(source,DecimalRounding.ToEven); var strict=SafeDecimal.TryConvert<S,D>(source);
        conversions+=2;
        if(!S.IsFinite(source)) { Rejected(rounded,ArithmeticError.NonFinite); Rejected(strict,ArithmeticError.NonFinite); return; }
        EDecimal exact=Reference(source);
        DecimalCheck(rounded,strict,exact.RoundToPrecision(context),exact,null);
        if(S.IsZero(source)) Require(D.IsNegative(rounded.Value)==S.IsNegative(source),"conversion signed zero");
    }
    private static void DecimalCheck<T>(DecimalResult<T> rounded,DecimalResult<T> strict,EDecimal expected,EDecimal exact,EDecimal? divisor) where T:struct,IFloatingPointIeee754<T>
    {
        if(expected.IsInfinity())
        {
            var error=expected.IsNegative?ArithmeticError.Underflow:ArithmeticError.Overflow;
            Rejected(rounded,error); Rejected(strict,error); return;
        }
        Require(rounded.IsSuccess,"decimal rounded admission");
        T wanted=T.Parse(expected.ToString(),Inv);
        Require(rounded.Value==wanted,$"decimal numerical expected={expected} actual={rounded.Value}");
        EDecimal represented=Reference(rounded.Value);
        bool isExact=(divisor is null?represented:represented.Multiply(divisor)).CompareTo(exact)==0;
        Require(rounded.IsInexact==!isExact,"decimal rounded exactness flag");
        if(isExact) { Require(strict.IsSuccess&&!strict.IsInexact,"decimal exact admission"); Require(strict.Value==wanted,"decimal exact numerical"); }
        else Rejected(strict,ArithmeticError.Inexact);
    }
    private static void Rejected<T>(DecimalResult<T> result,ArithmeticError error) where T:struct,IFloatingPointIeee754<T>
    {
        Require(result.Error==error,$"decimal expected={error} actual={result.Error}");
        Require(!result.TryGetValue(out var cleared)&&cleared.Equals(default(T)),"decimal failure clearing");
        Require(!result.IsInexact,"decimal failed flag");
    }
    private static void Raw<T>(T x) where T:struct,IFloatingPointIeee754<T>
    {
        rawChecks++;
        var sum=SafeDecimal.TryAdd(x,T.Zero); var diff=SafeDecimal.TrySubtract(x,x); var conversion=SafeDecimal.TryConvert<T,T>(x);
        decimalOperations+=2; conversions++;
        if(!T.IsFinite(x)) { Rejected(sum,ArithmeticError.NonFinite); Rejected(diff,ArithmeticError.NonFinite); Rejected(conversion,ArithmeticError.NonFinite); return; }
        Require(sum.IsSuccess&&!sum.IsInexact&&sum.Value==x,"raw add identity");
        Require(diff.IsSuccess&&!diff.IsInexact&&T.IsZero(diff.Value),"raw cancellation");
        Require(conversion.IsSuccess&&!conversion.IsInexact&&conversion.Value==x,"raw same-width conversion");
        var negated=SafeDecimal.TryNegate(x); var restored=SafeDecimal.TryNegate(negated.Value); decimalOperations+=2;
        Require(negated.IsSuccess&&restored.IsSuccess&&restored.Value==x,"raw double negate");
        if(T.IsZero(x)) Require(T.IsNegative(conversion.Value)==T.IsNegative(x),"raw signed zero");
        var quotient=SafeDecimal.TryDivide(x,x); decimalOperations++;
        if(T.IsZero(x)) Rejected(quotient,ArithmeticError.DivideByZero);
        else Require(quotient.IsSuccess&&!quotient.IsInexact&&quotient.Value==T.One,"raw self divide");
    }
}
