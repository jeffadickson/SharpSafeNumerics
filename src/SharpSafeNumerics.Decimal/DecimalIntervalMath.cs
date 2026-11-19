// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;

namespace SharpSafeNumerics;

// Fixed-scale outward intervals certify decimal rounding without trusting guard
// digits or a binary intermediary. Every elementary operation rounds its lower
// bound down and upper bound up. Series termination includes a geometric tail
// bound; callers increase precision until both endpoints round identically.
internal sealed class DecimalIntervalMath(int digits)
{
    internal readonly record struct Interval(BigInteger Lower, BigInteger Upper);
    internal BigInteger Scale { get; } = BigInteger.Pow(10, digits);
    internal Interval One => new(Scale, Scale);
    internal Interval Zero => new(0, 0);
    internal Interval Integer(BigInteger n) => new(n * Scale, n * Scale);
    internal Interval Rational(BigInteger numerator, BigInteger denominator) => new(Floor(numerator * Scale, denominator), Ceiling(numerator * Scale, denominator));
    internal Interval Decimal(DecimalParts p) => p.Exponent >= 0
        ? Integer((p.Negative ? -p.Coefficient : p.Coefficient) * BigInteger.Pow(10, p.Exponent))
        : Rational(p.Negative ? -p.Coefficient : p.Coefficient, BigInteger.Pow(10, -p.Exponent));
    internal Interval Add(Interval a, Interval b) => new(a.Lower + b.Lower, a.Upper + b.Upper);
    internal Interval Negate(Interval a) => new(-a.Upper, -a.Lower);
    internal Interval Subtract(Interval a, Interval b) => Add(a, Negate(b));
    internal Interval Multiply(Interval a, Interval b)
    {
        BigInteger p = a.Lower * b.Lower, q = a.Lower * b.Upper, r = a.Upper * b.Lower, s = a.Upper * b.Upper;
        return new(Floor(BigInteger.Min(BigInteger.Min(p, q), BigInteger.Min(r, s)), Scale), Ceiling(BigInteger.Max(BigInteger.Max(p, q), BigInteger.Max(r, s)), Scale));
    }
    internal Interval Divide(Interval a, Interval b)
    {
        if (b.Lower <= 0 && b.Upper >= 0) throw new IntervalRefinementRequiredException();
        if (b.Upper < 0) return Divide(Negate(a), Negate(b));
        var candidates = new[] { (a.Lower, b.Lower), (a.Lower, b.Upper), (a.Upper, b.Lower), (a.Upper, b.Upper) };
        BigInteger lower = candidates.Min(p => Floor(p.Item1 * Scale, p.Item2));
        BigInteger upper = candidates.Max(p => Ceiling(p.Item1 * Scale, p.Item2));
        return new(lower, upper);
    }
    internal Interval Divide(Interval a, BigInteger b) => b.Sign < 0 ? Negate(Divide(a, -b)) : new(Floor(a.Lower, b), Ceiling(a.Upper, b));
    internal Interval Multiply(Interval a, BigInteger b) => b.Sign < 0 ? Negate(Multiply(a, -b)) : new(a.Lower * b, a.Upper * b);
    private static BigInteger Magnitude(Interval a) => BigInteger.Max(BigInteger.Abs(a.Lower), BigInteger.Abs(a.Upper));
    internal Interval Sqrt(Interval a)
    {
        if (a.Lower < 0) throw new IntervalRefinementRequiredException();
        BigInteger lower = IntegerSqrt(a.Lower * Scale), upper = IntegerSqrt(a.Upper * Scale);
        if (upper * upper != a.Upper * Scale) upper++;
        return new(lower, upper);
    }
    internal static BigInteger IntegerSqrt(BigInteger n)
    {
        if (n.Sign < 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (n < 2) return n;
        var x = BigInteger.One << checked((int)((n.GetBitLength() + 1) / 2));
        while (true) { var y = (x + n / x) >> 1; if (y >= x) return x; x = y; }
    }
    internal static BigInteger Floor(BigInteger n, BigInteger d)
    {
        if (d.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(d));
        var q = BigInteger.DivRem(n, d, out var r);
        return r.Sign < 0 ? q - 1 : q;
    }
    internal static BigInteger Ceiling(BigInteger n, BigInteger d) => -Floor(-n, d);
    private Interval Widen(Interval value, BigInteger tail) => new(value.Lower - tail, value.Upper + tail);

    // Machin's identity, using two rapidly convergent rational atan series.
    internal Interval Pi() => Subtract(Multiply(AtanSeries(Rational(1, 5)), 16), Multiply(AtanSeries(Rational(1, 239)), 4));
    internal Interval Exp(Interval x)
    {
        if (x.Lower > Scale * 50000) throw new DecimalExponentialRangeException(true);
        if (x.Upper < -Scale * 50000) throw new DecimalExponentialRangeException(false);
        if (x.Upper < 0) return Divide(One, Exp(Negate(x)));
        if (x.Lower < 0) return new(Exp(new(x.Lower, x.Lower)).Lower, Exp(new(x.Upper, x.Upper)).Upper);
        int squarings = 0;
        while (x.Upper * 8 > Scale) { x = Divide(x, 2); squarings++; }
        var sum = One; var term = One;
        for (int n = 1; ; n++)
        {
            term = Divide(Multiply(term, x), n); sum = Add(sum, term);
            if (term.Upper <= 2 && n > 2)
            {
                // x<=1/8, hence the following tail is less than twice this term.
                sum = new(sum.Lower, sum.Upper + 2 * term.Upper); break;
            }
        }
        while (squarings-- > 0) sum = Multiply(sum, sum);
        return sum;
    }
    internal Interval Log(Interval value)
    {
        if (value.Lower <= 0) throw new IntervalRefinementRequiredException();
        // Log is monotone. Splitting non-point inputs avoids losing the correlation
        // between x-1 and x+1: interval division could otherwise enclose values
        // outside (-1,1), where the logarithm series does not converge.
        if (value.Lower != value.Upper)
            return new(Log(new(value.Lower, value.Lower)).Lower, Log(new(value.Upper, value.Upper)).Upper);
        // The positive point has d decimal digits. Scaling by 10^(d-digits-1)
        // places its outward bounds in [1,10], with width at most one scale unit.
        // No correction loop is needed; Log(10) uses the same bounded series.
        int exponent = value.Lower.ToString(System.Globalization.CultureInfo.InvariantCulture).Length - Scale.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        var scaled = exponent >= 0 ? Divide(value, BigInteger.Pow(10, exponent)) : Multiply(value, BigInteger.Pow(10, -exponent));


        var mantissa = LogMantissa(scaled);
        // Near-one certification can use thousands of digits. Multiplication by
        // zero is exact, so do not evaluate the unused log(10) series in that case.
        return exponent == 0 ? mantissa : Add(mantissa, Multiply(LogMantissa(Integer(10)), exponent));
    }
    private Interval LogMantissa(Interval x)
    {
        var z = Divide(Subtract(x, One), Add(x, One)); var square = Multiply(z, z);
        var power = z; var sum = z;
        for (int n = 1; ; n++)
        {
            power = Multiply(power, square); var term = Divide(power, 2 * n + 1); sum = Add(sum, term);
            if (Magnitude(term) <= 2)
            {
                // Point normalization has at most one integer unit of width. Before
                // outward rounding, |z| <= 9*Scale/(11*Scale-1); one extra
                // unit gives |z|<0.82 for the tested Scale>=1000 (production
                // uses at least 10^39). Thus 1/(1-z*z)<4 bounds the tail.
                return Multiply(Widen(sum, 4 * Magnitude(term)), 2);
            }
        }
    }
    internal Interval Atan(Interval x)
    {
        if (x.Upper < 0) return Negate(Atan(Negate(x)));
        if (x.Lower < 0) return new(Atan(new(x.Lower, x.Lower)).Lower, Atan(new(x.Upper, x.Upper)).Upper);
        if (x.Lower > Scale) return Subtract(Divide(Pi(), 2), Atan(Divide(One, x)));
        int doubles = 0;
        while (x.Upper * 8 > Scale)
        {
            x = Divide(x, Add(One, Sqrt(Add(One, Multiply(x, x))))); doubles++;
        }
        return Multiply(AtanSeries(x), BigInteger.One << doubles);
    }
    private Interval AtanSeries(Interval x)
    {
        var square = Multiply(x, x); var power = x; var sum = x;
        for (int n = 1; ; n++)
        {
            power = Multiply(power, square); var term = Divide(power, 2 * n + 1);
            sum = (n & 1) == 0 ? Add(sum, term) : Subtract(sum, term);
            if (Magnitude(term) <= 2) return Widen(sum, 2 * Magnitude(term));
        }
    }
    internal (Interval Sin, Interval Cos) SinCos(Interval x)
    {
        if (x.Upper < 0) { var pair = SinCos(Negate(x)); return (Negate(pair.Sin), pair.Cos); }
        var halfPi = Divide(Pi(), 2);
        // A unique quadrant is required. Refinement resolves any nonzero finite
        // decimal versus an irrational boundary; zero is handled by the caller.
        var qLower = Floor(x.Lower, halfPi.Upper); var qUpper = Floor(x.Upper, halfPi.Lower);
        if (qLower != qUpper) throw new IntervalRefinementRequiredException();
        var reduced = Subtract(x, Multiply(halfPi, qLower));
        var sine = TrigSeries(reduced, true); var cosine = TrigSeries(reduced, false);
        return (int)(qLower % 4) switch { 0 => (sine, cosine), 1 => (cosine, Negate(sine)), 2 => (Negate(sine), Negate(cosine)), _ => (Negate(cosine), sine) };
    }
    private Interval TrigSeries(Interval x, bool sine)
    {
        var square = Multiply(x, x); var term = sine ? x : One; var sum = term;
        for (int n = 1; ; n++)
        {
            int a = 2 * n + (sine ? 0 : -1), b = a + 1;
            term = Divide(Multiply(term, square), (BigInteger)a * b);
            sum = (n & 1) == 0 ? Add(sum, term) : Subtract(sum, term);
            if (Magnitude(term) <= 2 && n > 3) return Widen(sum, 2 * Magnitude(term));
        }
    }
}

// Internal adaptive control flow; never presented as a mathematical domain error.
internal sealed class IntervalRefinementRequiredException : Exception;
internal sealed class DecimalExponentialRangeException(bool overflow) : Exception
{
    internal bool Overflow { get; } = overflow;
}
