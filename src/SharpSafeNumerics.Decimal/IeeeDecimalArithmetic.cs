using System.Numerics;
namespace SharpSafeNumerics;
/// <summary>Stateless IEEE decimal operations with explicit rounding and status.</summary>
public static partial class IeeeDecimal<T> where T : struct, IFloatingPointIeee754<T>
{
    internal static bool Valid(IeeeDecimalContext context) => IeeeDecimalCodec<T>.Supported && (uint)context.Rounding <= 4 && (context.Traps & ~(IeeeDecimalFlags)31) == 0;
    // All numeric and metadata result carriers use the same trap priority. Keeping
    // this policy in one implementation prevents conversion/arithmetic divergence.
    internal static IeeeDecimalResult<T> Finish(T value, IeeeDecimalFlags flags, IeeeDecimalContext context) =>
        RepresentationResult(value, flags, context);
    private static BigInteger Pow(int n) => BigInteger.Pow(10, n);
    private static int Digits(BigInteger n) => n.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
    private static IeeeDecimalResult<T> Invalid(IeeeDecimalContext c) => Finish(IeeeDecimalCodec<T>.Encode(new(false, 0, 0, DecimalKind.QuietNaN)), IeeeDecimalFlags.Invalid, c);
    private static IeeeDecimalResult<T> Special(DecimalParts p, IeeeDecimalContext c, IeeeDecimalFlags flags = 0) => Finish(IeeeDecimalCodec<T>.Encode(p), flags, c);
    private static bool NaN(DecimalParts p) => p.Kind is DecimalKind.QuietNaN or DecimalKind.SignalingNaN;
    private static IeeeDecimalResult<T> Propagate(IeeeDecimalContext c, params DecimalParts[] values)
    {
        var p = values.FirstOrDefault(x => x.Kind == DecimalKind.SignalingNaN); bool signaling = p.Kind == DecimalKind.SignalingNaN;
        if (!signaling) p = values.First(NaN);
        return Special(p with { Kind = DecimalKind.QuietNaN }, c, signaling ? IeeeDecimalFlags.Invalid : 0);
    }
    // Round the exact positive rational n/d * 10^exponent once. Preferred quantum is retained whenever precision permits.
    internal static IeeeDecimalResult<T> Round(BigInteger n, BigInteger d, int exponent, bool negative, int preferredExponent, IeeeDecimalContext context)
    {
        int p = IeeeDecimalCodec<T>.Precision, min = IeeeDecimalCodec<T>.MinExponent, max = IeeeDecimalCodec<T>.MaxExponent;
        if (n.IsZero) return Special(new(negative, 0, Math.Clamp(preferredExponent, min, max)), context);
        int adjusted = Digits(n) - Digits(d); if (adjusted >= 0 ? n < d * Pow(adjusted) : n * Pow(-adjusted) < d) adjusted--;
        int q = Math.Max(min, exponent + adjusted - p + 1);
        BigInteger nn = n, dd = d; int scale = exponent - q; if (scale >= 0) nn *= Pow(scale); else dd *= Pow(-scale);
        var coeff = BigInteger.DivRem(nn, dd, out var rem); bool inexact = !rem.IsZero;
        if (Increment(coeff, rem, dd, negative, context.Rounding)) coeff++;
        if (coeff >= Pow(p)) { coeff /= 10; q++; }
        if (!inexact) while (q < Math.Min(preferredExponent, max) && coeff % 10 == 0) { coeff /= 10; q++; }
        // Above the maximum quantum the coefficient still has p digits: initial
        // normal rounding fills precision, carry normalization preserves it, and
        // exact trailing-zero coarsening is capped at max. No exponent clamping
        // can rescue such a value without losing significant digits.
        if (q > max)
        {
            bool infinity = context.Rounding is IeeeDecimalRounding.ToNearestEven or IeeeDecimalRounding.ToNearestAway
                || context.Rounding == IeeeDecimalRounding.TowardPositive && !negative
                || context.Rounding == IeeeDecimalRounding.TowardNegative && negative;
            return Special(infinity ? new(negative, 0, 0, DecimalKind.Infinity) : new(negative, Pow(p) - 1, max), context, IeeeDecimalFlags.Overflow | IeeeDecimalFlags.Inexact);
        }
        var flags = inexact ? IeeeDecimalFlags.Inexact : 0; // IEEE 754-2019 section 7.5 requires decimal tininess before rounding;
        // default underflow status additionally requires an inexact rounded result.
        if (inexact && exponent + adjusted < min + p - 1) flags |= IeeeDecimalFlags.Underflow;
        return Special(new(negative, coeff, q), context, flags);
    }
    private static bool Increment(BigInteger c, BigInteger remainder, BigInteger divisor, bool negative, IeeeDecimalRounding rounding) => !remainder.IsZero && rounding switch { IeeeDecimalRounding.ToNearestEven => 2 * remainder > divisor || 2 * remainder == divisor && !c.IsEven, IeeeDecimalRounding.ToNearestAway => 2 * remainder >= divisor, IeeeDecimalRounding.TowardPositive => !negative, IeeeDecimalRounding.TowardNegative => negative, _ => false };
    /// <summary>Adds with one final rounding and preferred exponent.</summary>
    public static IeeeDecimalResult<T> Add(T left, T right, IeeeDecimalContext context = default) => Binary(left, right, context, 0);
    /// <summary>Subtracts with one final rounding.</summary>
    public static IeeeDecimalResult<T> Subtract(T left, T right, IeeeDecimalContext context = default) => Binary(left, right, context, 1);
    /// <summary>Multiplies with one final rounding.</summary>
    public static IeeeDecimalResult<T> Multiply(T left, T right, IeeeDecimalContext context = default) => Binary(left, right, context, 2);
    /// <summary>Divides with one final rounding.</summary>
    public static IeeeDecimalResult<T> Divide(T left, T right, IeeeDecimalContext context = default) => Binary(left, right, context, 3);
    private static IeeeDecimalResult<T> Binary(T left, T right, IeeeDecimalContext c, int op)
    {
        if (!Valid(c)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(left); var b = IeeeDecimalCodec<T>.Decode(right);
        if (NaN(a) || NaN(b)) return Propagate(c, a, b); if (op == 1) b = b with { Negative = !b.Negative }; bool neg = a.Negative ^ b.Negative;
        if (a.Kind == DecimalKind.Infinity || b.Kind == DecimalKind.Infinity) { if (op < 2) return a.Kind == b.Kind && a.Negative != b.Negative ? Invalid(c) : Special(a.Kind == DecimalKind.Infinity ? a : b, c); if (op == 2) return (a.Kind == DecimalKind.Finite && a.Coefficient.IsZero) || (b.Kind == DecimalKind.Finite && b.Coefficient.IsZero) ? Invalid(c) : Special(new(neg, 0, 0, DecimalKind.Infinity), c); return a.Kind == b.Kind ? Invalid(c) : a.Kind == DecimalKind.Infinity ? Special(new(neg, 0, 0, DecimalKind.Infinity), c) : Special(new(neg, 0, IeeeDecimalCodec<T>.MinExponent), c); }
        if (op == 3) { if (b.Coefficient.IsZero) return a.Coefficient.IsZero ? Invalid(c) : Special(new(neg, 0, 0, DecimalKind.Infinity), c, IeeeDecimalFlags.DivisionByZero); return Round(a.Coefficient, b.Coefficient, a.Exponent - b.Exponent, neg, a.Exponent - b.Exponent, c); }
        if (op == 2) return Round(a.Coefficient * b.Coefficient, 1, a.Exponent + b.Exponent, neg, a.Exponent + b.Exponent, c);
        int e = Math.Min(a.Exponent, b.Exponent); var n = (a.Negative ? -a.Coefficient : a.Coefficient) * Pow(a.Exponent - e) + (b.Negative ? -b.Coefficient : b.Coefficient) * Pow(b.Exponent - e);
        return Round(BigInteger.Abs(n), 1, e, n.Sign < 0 || n.IsZero && (a.Negative && b.Negative || a.Negative != b.Negative && c.Rounding == IeeeDecimalRounding.TowardNegative), e, c);
    }
    /// <summary>Converts a supported decimal directly with one rounding.</summary>
    public static IeeeDecimalResult<T> Convert<TSource>(TSource value, IeeeDecimalContext context = default) where TSource : struct, IFloatingPointIeee754<TSource>
    {
        if (!Valid(context) || !IeeeDecimalCodec<TSource>.Supported) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var p = IeeeDecimalCodec<TSource>.Decode(value); if (NaN(p)) return Propagate(context, p); return p.Kind == DecimalKind.Infinity ? Special(p, context) : Round(p.Coefficient, 1, p.Exponent, p.Negative, p.Exponent, context);
    }
    /// <summary>Computes an exact product and sum followed by a single rounding.</summary>
    public static IeeeDecimalResult<T> FusedMultiplyAdd(T left, T right, T addend, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(left); var b = IeeeDecimalCodec<T>.Decode(right); var z = IeeeDecimalCodec<T>.Decode(addend);
        bool invalidProduct = (a.Kind == DecimalKind.Infinity && b.Kind == DecimalKind.Finite && b.Coefficient.IsZero) || (b.Kind == DecimalKind.Infinity && a.Kind == DecimalKind.Finite && a.Coefficient.IsZero);
        if (invalidProduct) return Invalid(context); if (NaN(a) || NaN(b) || NaN(z)) return Propagate(context, a, b, z);
        if (a.Kind == DecimalKind.Infinity || b.Kind == DecimalKind.Infinity) { bool neg = a.Negative ^ b.Negative; return z.Kind == DecimalKind.Infinity && z.Negative != neg ? Invalid(context) : Special(new(neg, 0, 0, DecimalKind.Infinity), context); }
        if (z.Kind == DecimalKind.Infinity) return Special(z, context);
        int productExponent = a.Exponent + b.Exponent, e = Math.Min(productExponent, z.Exponent); bool productNegative = a.Negative ^ b.Negative;
        var n = (productNegative ? -1 : 1) * a.Coefficient * b.Coefficient * Pow(productExponent - e) + (z.Negative ? -1 : 1) * z.Coefficient * Pow(z.Exponent - e);
        return Round(BigInteger.Abs(n), 1, e, n.Sign < 0 || n.IsZero && (productNegative && z.Negative || productNegative != z.Negative && context.Rounding == IeeeDecimalRounding.TowardNegative), e, context);
    }
    /// <summary>Returns the nearest-even quotient remainder exactly, independent of rounding direction.</summary>
    public static IeeeDecimalResult<T> Remainder(T left, T right, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(left); var b = IeeeDecimalCodec<T>.Decode(right); if (NaN(a) || NaN(b)) return Propagate(context, a, b);
        if (a.Kind == DecimalKind.Infinity || b.Kind == DecimalKind.Finite && b.Coefficient.IsZero) return Invalid(context); if (b.Kind == DecimalKind.Infinity) return Special(a, context);
        int e = Math.Min(a.Exponent, b.Exponent); var x = a.Coefficient * Pow(a.Exponent - e); var y = b.Coefficient * Pow(b.Exponent - e); var quotient = BigInteger.DivRem(x, y, out var rem); if (2 * rem > y || 2 * rem == y && !quotient.IsEven) quotient++;
        var n = x - quotient * y; if (a.Negative) n = -n; return Round(BigInteger.Abs(n), 1, e, n.Sign < 0 || n.IsZero && a.Negative, e, context);
    }
    /// <summary>Rounds to the second operand's quantum; invalid when precision cannot hold that quantum.</summary>
    public static IeeeDecimalResult<T> Quantize(T value, T quantum, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(value); var b = IeeeDecimalCodec<T>.Decode(quantum); if (NaN(a) || NaN(b)) return Propagate(context, a, b);
        if (a.Kind == DecimalKind.Infinity || b.Kind == DecimalKind.Infinity) return a.Kind == b.Kind ? Special(a, context) : Invalid(context);
        int shift = a.Exponent - b.Exponent; BigInteger n = a.Coefficient, d = 1; if (shift >= 0) n *= Pow(shift); else d = Pow(-shift);
        var coeff = BigInteger.DivRem(n, d, out var rem); if (Increment(coeff, rem, d, a.Negative, context.Rounding)) coeff++;
        return Digits(coeff) > IeeeDecimalCodec<T>.Precision ? Invalid(context) : Special(new(a.Negative, coeff, b.Exponent), context, rem.IsZero ? 0 : IeeeDecimalFlags.Inexact);
    }
    /// <summary>Square root with exact integer midpoint comparisons and one final rounding.</summary>
    public static IeeeDecimalResult<T> Sqrt(T value, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(value); if (NaN(a)) return Propagate(context, a); if (a.Negative && !a.Coefficient.IsZero || a.Negative && a.Kind == DecimalKind.Infinity) return Invalid(context); if (a.Kind == DecimalKind.Infinity) return Special(a, context);
        int preferred = (int)Math.Floor(a.Exponent / 2.0); if (a.Coefficient.IsZero) return Round(0, 1, 0, a.Negative, preferred, context);
        int adjusted = Digits(a.Coefficient) - 1 + a.Exponent;
        int rootAdjusted = (int)Math.Floor(adjusted / 2.0);
        int q = rootAdjusted - IeeeDecimalCodec<T>.Precision + 1;
        // Let k be coefficient digits and p format precision. All finite decimal sqrt
        // results are normal: floor(eminQuantum/2) exceeds eminQuantum+p-1.
        // Thus scale=e-2q equals 2p-k-1 or 2p-k, hence is at least p-1>=6.
        // The scaled integer radicand is divisible by 10. An exact midpoint would
        // require 4*n=(2*r+1)^2, impossible since the right side is odd.
        // Consequently nearest-even and nearest-away agree for these carriers.
        int scale = a.Exponent - 2 * q;
        BigInteger n = a.Coefficient * Pow(scale);
        var root = IntegerSqrt(n);
        bool exact = root * root == n;
        bool up = !exact && context.Rounding switch
        {
            IeeeDecimalRounding.TowardPositive => true,
            IeeeDecimalRounding.ToNearestAway or IeeeDecimalRounding.ToNearestEven => 4 * n > (2 * root + 1) * (2 * root + 1),
            _ => false
        };
        if (up) root++;
        if (Digits(root) > IeeeDecimalCodec<T>.Precision) { root /= 10; q++; }
        if (exact) while (q < preferred && root % 10 == 0) { root /= 10; q++; }
        return Special(new(false, root, q), context, exact ? 0 : IeeeDecimalFlags.Inexact);
    }
    private static BigInteger IntegerSqrt(BigInteger n)
    {
        if (n.IsZero) return 0; BigInteger x = BigInteger.One << ((int)n.GetBitLength() + 1) / 2;
        while (true) { var y = (x + n / x) / 2; if (y >= x) return x; x = y; }
    }
}







