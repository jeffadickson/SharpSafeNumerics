using System.Numerics;

namespace SharpSafeNumerics;

public static partial class IeeeDecimal<T> where T : struct, IFloatingPointIeee754<T>
{
    private enum Function { Exp, ExpM1, Exp2, Exp2M1, Exp10, Exp10M1, Log, LogP1, Log2, Log2P1, Log10, Log10P1, Sin, Cos, Tan, Asin, Acos, Atan, Sinh, Cosh, Tanh, Asinh, Acosh, Atanh, Cbrt }

    /// <summary>Computes e raised to the operand, with certified final decimal rounding.</summary>
    public static IeeeDecimalResult<T> Exp(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Exp, context);
    /// <summary>Computes exp(value)-1 without first rounding exp(value).</summary>
    public static IeeeDecimalResult<T> ExpM1(T value, IeeeDecimalContext context = default) => Elementary(value, Function.ExpM1, context);
    /// <summary>Computes 2 raised to the operand.</summary>
    public static IeeeDecimalResult<T> Exp2(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Exp2, context);
    /// <summary>Computes 2 raised to the operand minus one, with one final rounding.</summary>
    public static IeeeDecimalResult<T> Exp2M1(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Exp2M1, context);
    /// <summary>Computes 10 raised to the operand.</summary>
    public static IeeeDecimalResult<T> Exp10(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Exp10, context);
    /// <summary>Computes 10 raised to the operand minus one, with one final rounding.</summary>
    public static IeeeDecimalResult<T> Exp10M1(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Exp10M1, context);
    /// <summary>Computes the natural logarithm, reporting a divide-by-zero pole at zero.</summary>
    public static IeeeDecimalResult<T> Log(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Log, context);
    /// <summary>Computes log(1+value) without rounding the addition first.</summary>
    public static IeeeDecimalResult<T> LogP1(T value, IeeeDecimalContext context = default) => Elementary(value, Function.LogP1, context);
    /// <summary>Computes the base-two logarithm.</summary>
    public static IeeeDecimalResult<T> Log2(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Log2, context);
    /// <summary>Computes log2(1+value) without rounding the addition first.</summary>
    public static IeeeDecimalResult<T> Log2P1(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Log2P1, context);
    /// <summary>Computes the base-ten logarithm.</summary>
    public static IeeeDecimalResult<T> Log10(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Log10, context);
    /// <summary>Computes log10(1+value) without rounding the addition first.</summary>
    public static IeeeDecimalResult<T> Log10P1(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Log10P1, context);
    /// <summary>Computes the sine in radians using outward interval reduction.</summary>
    public static IeeeDecimalResult<T> Sin(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Sin, context);
    /// <summary>Computes the cosine in radians using outward interval reduction.</summary>
    public static IeeeDecimalResult<T> Cos(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Cos, context);
    /// <summary>Computes the tangent in radians.</summary>
    public static IeeeDecimalResult<T> Tan(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Tan, context);
    /// <summary>Computes the inverse sine in radians.</summary>
    public static IeeeDecimalResult<T> Asin(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Asin, context);
    /// <summary>Computes the inverse cosine in radians.</summary>
    public static IeeeDecimalResult<T> Acos(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Acos, context);
    /// <summary>Computes the inverse tangent in radians.</summary>
    public static IeeeDecimalResult<T> Atan(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Atan, context);
    /// <summary>Computes the hyperbolic sine.</summary>
    public static IeeeDecimalResult<T> Sinh(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Sinh, context);
    /// <summary>Computes the hyperbolic cosine.</summary>
    public static IeeeDecimalResult<T> Cosh(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Cosh, context);
    /// <summary>Computes the hyperbolic tangent.</summary>
    public static IeeeDecimalResult<T> Tanh(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Tanh, context);
    /// <summary>Computes the inverse hyperbolic sine.</summary>
    public static IeeeDecimalResult<T> Asinh(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Asinh, context);
    /// <summary>Computes the inverse hyperbolic cosine.</summary>
    public static IeeeDecimalResult<T> Acosh(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Acosh, context);
    /// <summary>Computes the inverse hyperbolic tangent.</summary>
    public static IeeeDecimalResult<T> Atanh(T value, IeeeDecimalContext context = default) => Elementary(value, Function.Atanh, context);
    /// <summary>Computes the real cube root, including negative operands.</summary>
    public static IeeeDecimalResult<T> Cbrt(T value, IeeeDecimalContext context = default) => RootN(value, 3, context);

    private static IeeeDecimalResult<T> Elementary(T value, Function function, IeeeDecimalContext context)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(value);
        if (NaN(a)) return Propagate(context, a);
        bool logarithm = function is Function.Log or Function.Log2 or Function.Log10;
        bool logarithmP1 = function is Function.LogP1 or Function.Log2P1 or Function.Log10P1;
        bool exponential = function is Function.Exp or Function.Exp2 or Function.Exp10 or Function.ExpM1 or Function.Exp2M1 or Function.Exp10M1;
        bool minusOne = function is Function.ExpM1 or Function.Exp2M1 or Function.Exp10M1;
        if (a.Kind == DecimalKind.Infinity)
        {
            if (exponential) return a.Negative ? Special(minusOne ? new(true, 1, 0) : new(false, 0, 0), context) : Special(a, context);
            if (logarithm || logarithmP1 || function == Function.Acosh) return a.Negative ? Invalid(context) : Special(a, context);
            if (function is Function.Sinh or Function.Asinh) return Special(a, context);
            if (function == Function.Cosh) return Special(a with { Negative = false }, context);
            if (function == Function.Tanh) return Special(new(a.Negative, 1, 0), context);
            if (function == Function.Atan) return Certified(a, context, (m, _) => a.Negative ? m.Negate(m.Divide(m.Pi(), 2)) : m.Divide(m.Pi(), 2));
            return Invalid(context);
        }
        if (a.Coefficient.IsZero)
        {
            if (logarithm) return Special(new(true, 0, 0, DecimalKind.Infinity), context, IeeeDecimalFlags.DivisionByZero);
            if (function == Function.Acosh) return Invalid(context);
            if (function == Function.Acos) return Certified(a, context, (m, _) => m.Divide(m.Pi(), 2));
            if (function is Function.Exp or Function.Exp2 or Function.Exp10 or Function.Cos or Function.Cosh) return Special(new(false, 1, 0), context);
            return Special(new(a.Negative, 0, 0), context);
        }
        // Even exp2(-200) is smaller than 10^-60: all three formats round
        // -1+that positive residual by the same adjacent-value rule.
        if (minusOne && value < -T.CreateChecked(200)) return NearMinusOne(context);
        if (logarithm && a.Negative) return Invalid(context);
        if (logarithmP1 && value <= -T.One) return value == -T.One ? Special(new(true, 0, 0, DecimalKind.Infinity), context, IeeeDecimalFlags.DivisionByZero) : Invalid(context);
        if (function is Function.Asin or Function.Acos && T.Abs(value) > T.One) return Invalid(context);
        if (function == Function.Acosh && value < T.One) return Invalid(context);
        if (function == Function.Atanh && T.Abs(value) >= T.One) return T.Abs(value) == T.One ? Special(new(a.Negative, 0, 0, DecimalKind.Infinity), context, IeeeDecimalFlags.DivisionByZero) : Invalid(context);
        if (logarithm && value == T.One || function is Function.Acos or Function.Acosh && value == T.One) return Special(new(false, 0, 0), context);
        if (function == Function.Log10 && ExactDecimalPower(a, out int decimalPower)) return Round(BigInteger.Abs(decimalPower), 1, 0, decimalPower < 0, 0, context);
        if (function == Function.Log2 && ExactBinaryPower(a, out int binaryPower)) return Round(BigInteger.Abs(binaryPower), 1, 0, binaryPower < 0, 0, context);
        if (logarithmP1)
        {
            int e = Math.Min(a.Exponent, 0);
            var coefficient = (a.Negative ? -a.Coefficient : a.Coefficient) * BigInteger.Pow(10, a.Exponent - e) + BigInteger.Pow(10, -e);
            var shifted = new DecimalParts(false, coefficient, e);
            if (function == Function.Log2P1 && ExactBinaryPower(shifted, out int power2)) return Round(BigInteger.Abs(power2), 1, 0, power2 < 0, 0, context);
            if (function == Function.Log10P1 && ExactDecimalPower(shifted, out int power10)) return Round(BigInteger.Abs(power10), 1, 0, power10 < 0, 0, context);
        }
        if (exponential && TryInteger(a, out var integer))
        {
            if (function is Function.Exp10 or Function.Exp10M1 && integer >= -20000 && integer <= 20000)
            {
                int e = (int)integer;
                if (!minusOne) return Round(1, 1, e, false, 0, context);
                var n = e >= 0 ? BigInteger.Pow(10, e) - 1 : BigInteger.One - BigInteger.Pow(10, -e);
                return Round(BigInteger.Abs(n), 1, Math.Min(e, 0), n.Sign < 0, Math.Min(e, 0), context);
            }
            if (function is Function.Exp2 or Function.Exp2M1 && integer >= -100000 && integer <= 100000)
            {
                int e = (int)integer; BigInteger n = BigInteger.One << Math.Abs(e), d = 1;
                if (e < 0) (n, d) = (d, n);
                if (minusOne) n -= d;
                return Round(BigInteger.Abs(n), d, 0, n.Sign < 0, 0, context);
            }
        }
        // All formats' exp/exp2/exp10 boundaries lie inside +/-50000.
        // The synthetic exponents here are beyond every destination boundary;
        // they select the correct rounded infinity/max-finite/zero/min-subnormal.
        if (exponential && T.Abs(value) > T.CreateChecked(50000))
        {
            if (a.Negative && minusOne) return NearMinusOne(context);
            return Round(1, 1, a.Negative ? IeeeDecimalCodec<T>.MinExponent - 100 : IeeeDecimalCodec<T>.MaxExponent + 100, false, 0, context);
        }
        if (function is Function.Sinh or Function.Cosh && T.Abs(value) > T.CreateChecked(50000))
            return Round(1, 1, IeeeDecimalCodec<T>.MaxExponent + 100, function == Function.Sinh && a.Negative, 0, context);
        if (function == Function.Tanh && T.Abs(value) > T.CreateChecked(200))
        {
            var mirrored = a.Negative ? context : context with { Rounding = context.Rounding == IeeeDecimalRounding.TowardPositive ? IeeeDecimalRounding.TowardNegative : context.Rounding == IeeeDecimalRounding.TowardNegative ? IeeeDecimalRounding.TowardPositive : context.Rounding };
            var result = NearMinusOne(mirrored with { Traps = 0 });
            return Finish(a.Negative ? result.Value : -result.Value, result.Flags, context);
        }
        return Certified(a, context, (m, x) => Evaluate(m, x, function));
    }
    private static IeeeDecimalResult<T> NearMinusOne(IeeeDecimalContext context)
    {
        var minusOne = new DecimalParts(true, 1, 0);
        // -1+positive epsilon rounds upward only in the directed-up/zero modes.
        int q = -IeeeDecimalCodec<T>.Precision;
        var adjacent = IeeeDecimalCodec<T>.Encode(new(true, BigInteger.Pow(10, -q) - 1, q));
        return Finish(context.Rounding is IeeeDecimalRounding.TowardPositive or IeeeDecimalRounding.TowardZero ? adjacent : IeeeDecimalCodec<T>.Encode(minusOne), IeeeDecimalFlags.Inexact, context);
    }
    private static DecimalIntervalMath.Interval Evaluate(DecimalIntervalMath m, DecimalIntervalMath.Interval x, Function function)
    {
        var one = m.One;
        return function switch
        {
            Function.Exp => m.Exp(x),
            Function.ExpM1 => m.Subtract(m.Exp(x), one),
            Function.Exp2 => m.Exp(m.Multiply(x, m.Log(m.Integer(2)))),
            Function.Exp2M1 => m.Subtract(m.Exp(m.Multiply(x, m.Log(m.Integer(2)))), one),
            Function.Exp10 => m.Exp(m.Multiply(x, m.Log(m.Integer(10)))),
            Function.Exp10M1 => m.Subtract(m.Exp(m.Multiply(x, m.Log(m.Integer(10)))), one),
            Function.Log => m.Log(x),
            Function.LogP1 => m.Log(m.Add(one, x)),
            Function.Log2 => m.Divide(m.Log(x), m.Log(m.Integer(2))),
            Function.Log2P1 => m.Divide(m.Log(m.Add(one, x)), m.Log(m.Integer(2))),
            Function.Log10 => m.Divide(m.Log(x), m.Log(m.Integer(10))),
            Function.Log10P1 => m.Divide(m.Log(m.Add(one, x)), m.Log(m.Integer(10))),
            Function.Sin => m.SinCos(x).Sin,
            Function.Cos => m.SinCos(x).Cos,
            Function.Tan => m.Divide(m.SinCos(x).Sin, m.SinCos(x).Cos),
            Function.Atan => m.Atan(x),
            Function.Asin => AsinInterval(m, x),
            Function.Acos => m.Subtract(m.Divide(m.Pi(), 2), AsinInterval(m, x)),
            Function.Sinh => m.Divide(m.Subtract(m.Exp(x), m.Exp(m.Negate(x))), 2),
            Function.Cosh => m.Divide(m.Add(m.Exp(x), m.Exp(m.Negate(x))), 2),
            Function.Tanh => m.Divide(m.Subtract(m.Exp(x), m.Exp(m.Negate(x))), m.Add(m.Exp(x), m.Exp(m.Negate(x)))),
            Function.Asinh => x.Upper < 0 ? m.Negate(m.Log(m.Add(m.Negate(x), m.Sqrt(m.Add(one, m.Multiply(x, x)))))) : m.Log(m.Add(x, m.Sqrt(m.Add(one, m.Multiply(x, x))))),
            Function.Acosh => m.Log(m.Add(x, m.Sqrt(m.Subtract(m.Multiply(x, x), one)))),
            Function.Atanh => m.Divide(m.Log(m.Divide(m.Add(one, x), m.Subtract(one, x))), 2),
            _ => throw new InvalidOperationException("Unhandled elementary operation.")
        };
    }
    private static DecimalIntervalMath.Interval AsinInterval(DecimalIntervalMath m, DecimalIntervalMath.Interval x)
    {
        if (x.Lower == m.Scale && x.Upper == m.Scale) return m.Divide(m.Pi(), 2);
        if (x.Lower == -m.Scale && x.Upper == -m.Scale) return m.Negate(m.Divide(m.Pi(), 2));
        return m.Atan(m.Divide(x, m.Sqrt(m.Subtract(m.One, m.Multiply(x, x)))));
    }
    private static IeeeDecimalResult<T> Certified(DecimalParts argument, IeeeDecimalContext context, Func<DecimalIntervalMath, DecimalIntervalMath.Interval, DecimalIntervalMath.Interval> evaluate, bool negativeResult = false)
    {
        int digits = IeeeDecimalCodec<T>.Precision + 32 + Math.Abs(argument.Exponent);
        var untrapped = context with { Traps = IeeeDecimalFlags.None };
        while (true)
        {
            var m = new DecimalIntervalMath(digits);
            try
            {
                var interval = evaluate(m, argument.Kind == DecimalKind.Finite ? m.Decimal(argument) : m.Zero);
                var low = Round(BigInteger.Abs(interval.Lower), m.Scale, 0, interval.Lower.Sign < 0, IeeeDecimalCodec<T>.MinExponent, untrapped);
                var high = Round(BigInteger.Abs(interval.Upper), m.Scale, 0, interval.Upper.Sign < 0, IeeeDecimalCodec<T>.MinExponent, untrapped);
                if (IeeeDecimalCodec<T>.Decode(low.Value) == IeeeDecimalCodec<T>.Decode(high.Value))
                {
                    // Decimal tininess is tested BEFORE rounding (IEEE 7.5).
                    // A unique value alone is insufficient near the normal
                    // boundary: refine until its tiny/not-tiny status is unique.
                    var normalScale = BigInteger.Pow(10, -(IeeeDecimalCodec<T>.MinExponent + IeeeDecimalCodec<T>.Precision - 1));
                    BigInteger smallest = interval.Lower.Sign >= 0 ? interval.Lower : interval.Upper.Sign <= 0 ? -interval.Upper : 0;
                    BigInteger largest = BigInteger.Max(BigInteger.Abs(interval.Lower), BigInteger.Abs(interval.Upper));
                    bool tiny = largest * normalScale < m.Scale;
                    if (!tiny && smallest * normalScale < m.Scale) { digits = checked(digits * 2); continue; }
                    var flags = ((low.Flags | high.Flags) & ~IeeeDecimalFlags.Underflow) | IeeeDecimalFlags.Inexact;
                    if (tiny) flags |= IeeeDecimalFlags.Underflow;
                    return Finish(low.Value, flags, context);
                }
            }
            catch (IntervalRefinementRequiredException) { /* Wider precision resolves enclosure ambiguity, not the mathematical domain. */ }
            catch (DecimalExponentialRangeException range)
            {
                return Round(1, 1, range.Overflow ? IeeeDecimalCodec<T>.MaxExponent + 100 : IeeeDecimalCodec<T>.MinExponent - 100, negativeResult, 0, context);
            }
            digits = checked(digits * 2);
        }
    }
    private static bool TryInteger(DecimalParts p, out BigInteger integer)
    {
        integer = p.Coefficient;
        if (p.Exponent >= 0) integer *= BigInteger.Pow(10, p.Exponent);
        else { integer = BigInteger.DivRem(integer, BigInteger.Pow(10, -p.Exponent), out var remainder); if (!remainder.IsZero) return false; }
        if (p.Negative) integer = -integer;
        return true;
    }
    private static bool ExactDecimalPower(DecimalParts p, out int exponent)
    {
        exponent = p.Exponent; var coefficient = p.Coefficient;
        while (coefficient > 1 && coefficient % 10 == 0) { coefficient /= 10; exponent++; }
        return coefficient.IsOne;
    }
    private static bool ExactBinaryPower(DecimalParts p, out int exponent)
    {
        BigInteger n = p.Coefficient, d = 1;
        if (p.Exponent >= 0) n *= BigInteger.Pow(10, p.Exponent); else d = BigInteger.Pow(10, -p.Exponent);
        var gcd = BigInteger.GreatestCommonDivisor(n, d); n /= gcd; d /= gcd;
        exponent = 0;
        if ((n & (n - 1)) != 0 || (d & (d - 1)) != 0) return false;
        exponent = checked((int)(n.GetBitLength() - d.GetBitLength())); return true;
    }

    /// <summary>Raises a value to a power; integer exponents use exact rational arithmetic before rounding.</summary>
    public static IeeeDecimalResult<T> Pow(T value, T power, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(value); var b = IeeeDecimalCodec<T>.Decode(power);
        if (a.Kind == DecimalKind.SignalingNaN || b.Kind == DecimalKind.SignalingNaN) return Propagate(context, a, b);
        if (b.Kind == DecimalKind.Finite && b.Coefficient.IsZero || value == T.One)
            return Round(1, 1, 0, false, b.Kind == DecimalKind.Finite ? PowerPreferred(a.Exponent, b) : 0, context);
        if (NaN(a) || NaN(b)) return Propagate(context, a, b);
        bool integral = b.Kind == DecimalKind.Finite && TryInteger(b, out _);
        BigInteger n = 0; if (integral) TryInteger(b, out n);
        bool negative = a.Negative && integral && !n.IsEven;
        if (integral && T.Abs(value) == T.One) return Round(1, 1, 0, negative, PowerPreferred(a.Exponent, b), context);
        if (a.Kind == DecimalKind.Finite && a.Coefficient.IsZero)
        {
            // IEEE 9.2 distinguishes an infinite exponent from a finite negative
            // one: pow(+-0,-Infinity) is +Infinity without DivisionByZero.
            if (b.Kind == DecimalKind.Infinity) return Special(new(false, 0, 0, b.Negative ? DecimalKind.Infinity : DecimalKind.Finite), context);
            if (power < T.Zero) return Special(new(negative, 0, 0, DecimalKind.Infinity), context, IeeeDecimalFlags.DivisionByZero);
            return Round(0, 1, 0, negative, b.Kind == DecimalKind.Finite ? PowerPreferred(a.Exponent, b) : 0, context);
        }
        if (a.Kind == DecimalKind.Infinity) return Special(power < T.Zero ? new(negative, 0, 0) : new(negative, 0, 0, DecimalKind.Infinity), context);
        if (b.Kind == DecimalKind.Infinity)
        {
            if (T.Abs(value) == T.One) return Special(new(false, 1, 0), context);
            bool overflow = (T.Abs(value) > T.One) != b.Negative;
            return Special(overflow ? new(false, 0, 0, DecimalKind.Infinity) : new(false, 0, 0), context);
        }
        if (a.Negative && !integral) return Invalid(context);
        if (integral && BigInteger.Abs(n) <= 100000)
        {
            int count = (int)BigInteger.Abs(n); var numerator = BigInteger.Pow(a.Coefficient, count);
            return n.Sign >= 0 ? Round(numerator, 1, checked(a.Exponent * count), negative, checked(a.Exponent * count), context)
                : Round(1, numerator, checked(-a.Exponent * count), negative, checked(-a.Exponent * count), context);
        }
        if (!integral)
        {
            // A finite decimal C*10^q with q>=0 is integral, so the nonintegral
            // branch necessarily has q<0. Build its exact rational directly.
            BigInteger numerator = b.Coefficient, denominator = BigInteger.Pow(10, -b.Exponent);
            var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator); numerator /= gcd; denominator /= gcd;
            if (denominator <= 10000 && numerator <= 100000)
            {
                int degree = (int)denominator;
                int remainder = (a.Exponent % degree + degree) % degree;
                var coefficient = a.Coefficient * BigInteger.Pow(10, remainder);
                var root = NthRootFloor(coefficient, degree);
                if (BigInteger.Pow(root, degree) == coefficient)
                {
                    int exponent = checked(((a.Exponent - remainder) / degree) * (int)numerator);
                    var exact = BigInteger.Pow(root, (int)numerator);
                    return b.Negative ? Round(1, exact, -exponent, false, -exponent, context) : Round(exact, 1, exponent, false, exponent, context);
                }
            }
        }
        return Certified(a with { Negative = false }, context, (m, x) => { var z = m.Exp(m.Multiply(m.Log(x), m.Decimal(b))); return negative ? m.Negate(z) : z; }, negative);
    }
    /// <summary>Computes an integer-index real root; negative indices request its reciprocal.</summary>
    public static IeeeDecimalResult<T> RootN(T value, int n, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        if (n == 0) return Invalid(context);
        if (n == 1)
        {
            var identity = IeeeDecimalCodec<T>.Decode(value);
            return NaN(identity) ? Propagate(context, identity) : Special(identity, context);
        }
        if (n == 2) return Sqrt(T.IsZero(value) ? IeeeDecimalCodec<T>.Encode(IeeeDecimalCodec<T>.Decode(value) with { Negative = false }) : value, context);
        var a = IeeeDecimalCodec<T>.Decode(value); if (NaN(a)) return Propagate(context, a);
        bool negative = a.Negative && (n & 1) != 0;
        if (a.Negative && (a.Kind == DecimalKind.Infinity || !a.Coefficient.IsZero) && (n & 1) == 0) return Invalid(context);
        if (a.Kind == DecimalKind.Infinity) return Special(n < 0 ? new(negative, 0, 0) : new(negative, 0, 0, DecimalKind.Infinity), context);
        if (a.Coefficient.IsZero) return n < 0 ? Special(new(negative, 0, 0, DecimalKind.Infinity), context, IeeeDecimalFlags.DivisionByZero) : Round(0, 1, 0, negative, ClampPreferred(DecimalIntervalMath.Floor(a.Exponent, n)), context);
        if (T.Abs(value) == T.One) return Round(1, 1, 0, negative, ClampPreferred(DecimalIntervalMath.Floor(a.Exponent * (n < 0 ? -BigInteger.One : BigInteger.One), Math.Abs((long)n))), context);
        // Exact perfect roots must be recognized: an interval around an exact
        // decimal result would otherwise straddle a directed-rounding boundary.
        long degree = Math.Abs((long)n);
        if (degree <= 10000)
        {
            int exponent = a.Exponent; var coefficient = a.Coefficient;
            int remainder = (int)((exponent % degree + degree) % degree);
            coefficient *= BigInteger.Pow(10, remainder); exponent -= remainder;
            var root = NthRootFloor(coefficient, (int)degree);
            if (BigInteger.Pow(root, (int)degree) == coefficient)
                return n > 0 ? Round(root, 1, (int)(exponent / degree), negative, (int)(exponent / degree), context) : Round(1, root, (int)(-exponent / degree), negative, (int)(-exponent / degree), context);
        }
        return Certified(a with { Negative = false }, context, (m, x) => { var z = m.Exp(m.Divide(m.Log(x), n)); return negative ? m.Negate(z) : z; }, negative);
    }
    private static BigInteger NthRootFloor(BigInteger value, int n)
    {
        if (value <= 1) return value;
        if (n >= value.GetBitLength()) return 1;
        BigInteger low = 1, high = BigInteger.One << checked((int)((value.GetBitLength() + n - 1) / n));
        // Initially high^n>=2^bitLength(value)>value. Updates preserve
        // low^n<=value<high^n; adjacent endpoints therefore identify low exactly.
        while (high - low > 1) { var middle = (high + low) >> 1; if (BigInteger.Pow(middle, n) <= value) low = middle; else high = middle; }
        return low;
    }
    private static int ClampPreferred(BigInteger exponent) => (int)BigInteger.Clamp(exponent, IeeeDecimalCodec<T>.MinExponent, IeeeDecimalCodec<T>.MaxExponent);
    private static int PowerPreferred(int exponent, DecimalParts power)
    {
        var numerator = (power.Negative ? -power.Coefficient : power.Coefficient) * exponent;
        return power.Exponent >= 0 ? ClampPreferred(numerator * Pow(power.Exponent)) : ClampPreferred(DecimalIntervalMath.Floor(numerator, Pow(-power.Exponent)));
    }

    /// <summary>Computes sqrt(x*x+y*y) with exact products and addition before the final square-root rounding.</summary>
    public static IeeeDecimalResult<T> Hypot(T x, T y, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(x); var b = IeeeDecimalCodec<T>.Decode(y);
        if (a.Kind == DecimalKind.SignalingNaN || b.Kind == DecimalKind.SignalingNaN) return Propagate(context, a, b);
        if (a.Kind == DecimalKind.Infinity || b.Kind == DecimalKind.Infinity) return Special(new(false, 0, 0, DecimalKind.Infinity), context);
        if (NaN(a) || NaN(b)) return Propagate(context, a, b);
        int exponent = Math.Min(a.Exponent, b.Exponent);
        BigInteger left = a.Coefficient * Pow(a.Exponent - exponent), right = b.Coefficient * Pow(b.Exponent - exponent);
        var sum = left * left + right * right;
        var root = DecimalIntervalMath.IntegerSqrt(sum);
        if (root * root == sum) return Round(root, 1, exponent, false, exponent, context);
        return Certified(new(false, sum, 2 * exponent), context, (m, value) => m.Sqrt(value));
    }

    /// <summary>Computes the reciprocal square root with a single final rounding; this is not an estimate.</summary>
    public static IeeeDecimalResult<T> ReciprocalSqrt(T value, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(value);
        if (NaN(a)) return Propagate(context, a);
        if (a.Kind == DecimalKind.Finite && a.Coefficient.IsZero) return Special(new(a.Negative, 0, 0, DecimalKind.Infinity), context, IeeeDecimalFlags.DivisionByZero);
        if (a.Negative) return Invalid(context);
        if (a.Kind == DecimalKind.Infinity) return Special(new(false, 0, 0), context);
        int exponent = a.Exponent; var coefficient = a.Coefficient;
        if ((exponent & 1) != 0) { coefficient *= 10; exponent--; }
        var root = DecimalIntervalMath.IntegerSqrt(coefficient);
        if (root * root == coefficient) return Round(1, root, -exponent / 2, false, -exponent / 2, context);
        return Certified(a, context, (m, operand) => m.Divide(m.One, m.Sqrt(operand)));
    }
}
