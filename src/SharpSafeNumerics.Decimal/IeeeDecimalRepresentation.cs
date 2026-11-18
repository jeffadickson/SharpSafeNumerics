using System.Globalization;
using System.Numerics;

namespace SharpSafeNumerics;

/// <summary>IEEE representation, comparison and nonarithmetic decimal operations.</summary>
public static partial class IeeeDecimal<T> where T : struct, IFloatingPointIeee754<T>
{
    // All boundaries validate the carrier before any static generic arithmetic executes.
    private static bool RepresentationValid(IeeeDecimalContext context) => IeeeDecimalCodec<T>.Supported && Valid(context);
    private static IeeeDecimalResult<U> RepresentationResult<U>(U value, IeeeDecimalFlags flags, IeeeDecimalContext context) where U : struct
    {
        var trapped = flags & context.Traps;
        var error = (trapped & IeeeDecimalFlags.Invalid) != 0 ? ArithmeticError.NonFinite : (trapped & IeeeDecimalFlags.DivisionByZero) != 0 ? ArithmeticError.DivideByZero : (trapped & IeeeDecimalFlags.Overflow) != 0 ? ArithmeticError.Overflow : (trapped & IeeeDecimalFlags.Underflow) != 0 ? ArithmeticError.Underflow : ArithmeticError.Inexact;
        return trapped != 0 ? IeeeDecimalResult<U>.Fail(error, flags) : IeeeDecimalResult<U>.Ok(value, flags);
    }
    private static IeeeDecimalResult<U> RepresentationFailure<U>() where U : struct => IeeeDecimalResult<U>.Fail(ArithmeticError.OutOfRange);
    /// <summary>Copies a representation, including signaling state and payload, without signaling.</summary>
    public static IeeeDecimalResult<T> Copy(T value, IeeeDecimalContext context = default) => RepresentationValid(context) ? Finish(value, 0, context) : RepresentationFailure<T>();
    /// <summary>Copies the representation with an inverted sign, without signaling.</summary>
    public static IeeeDecimalResult<T> Negate(T value, IeeeDecimalContext context = default) => RepresentationValid(context) ? SetSign(value, !T.IsNegative(value), context) : RepresentationFailure<T>();
    /// <summary>Copies the representation with a positive sign, without signaling.</summary>
    public static IeeeDecimalResult<T> Abs(T value, IeeeDecimalContext context = default) => SetSign(value, false, context);
    /// <summary>Copies the sign of the second operand onto the first, without signaling.</summary>
    public static IeeeDecimalResult<T> CopySign(T value, T sign, IeeeDecimalContext context = default) => RepresentationValid(context) ? SetSign(value, T.IsNegative(sign), context) : RepresentationFailure<T>();
    private static IeeeDecimalResult<T> SetSign(T value, bool negative, IeeeDecimalContext context)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<T>();
        int width = typeof(T) == typeof(Decimal32) ? 32 : typeof(T) == typeof(Decimal64) ? 64 : 128;
        BigInteger sign = BigInteger.One << (width - 1), bits = EncodeBinary(value).Value;
        return DecodeBinary(negative ? bits | sign : bits & ~sign, context);
    }
    /// <summary>Tests whether the BID carrier representation is canonical; never signals.</summary>
    public static IeeeDecimalResult<bool> IsCanonical(T value, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<bool>();
        var canonical = IeeeDecimalCodec<T>.Encode(IeeeDecimalCodec<T>.Decode(value));
        return RepresentationResult(EncodeBinary(value).Value == EncodeBinary(canonical).Value, 0, context);
    }    /// <summary>Returns whether the representation is a signaling NaN; never signals.</summary>
    public static IeeeDecimalResult<bool> IsSignaling(T value, IeeeDecimalContext context = default) => RepresentationValid(context) ? RepresentationResult(IeeeDecimalCodec<T>.Decode(value).Kind == DecimalKind.SignalingNaN, 0, context) : RepresentationFailure<bool>();
    /// <summary>Returns the sign bit, including signs of zeros and NaNs.</summary>
    public static IeeeDecimalResult<bool> IsSignMinus(T value, IeeeDecimalContext context = default) => RepresentationValid(context) ? RepresentationResult(T.IsNegative(value), 0, context) : RepresentationFailure<bool>();
    /// <summary>Classifies as negative infinity (-4), normal (-3), subnormal (-2), zero (-1), positive zero (1), subnormal (2), normal (3), infinity (4), quiet NaN (5), or signaling NaN (6).</summary>
    public static IeeeDecimalResult<int> Class(T value, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<int>();
        var p = IeeeDecimalCodec<T>.Decode(value);
        int c = p.Kind == DecimalKind.SignalingNaN ? 6 : p.Kind == DecimalKind.QuietNaN ? 5 : p.Kind == DecimalKind.Infinity ? 4 : T.IsZero(value) ? 1 : T.IsSubnormal(value) ? 2 : 3;
        if (c < 5 && p.Negative) c = -c;
        return RepresentationResult(c, 0, context);
    }
    /// <summary>Compares numerically; returns -1, 0, 1, or 2 for unordered. Only signaling NaNs signal invalid.</summary>
    public static IeeeDecimalResult<int> CompareQuiet(T left, T right, IeeeDecimalContext context = default) => CompareRepresentation(left, right, false, context);
    /// <summary>Compares numerically; returns -1, 0, 1, or 2 for unordered. Every NaN signals invalid.</summary>
    public static IeeeDecimalResult<int> CompareSignaling(T left, T right, IeeeDecimalContext context = default) => CompareRepresentation(left, right, true, context);
    private static IeeeDecimalResult<int> CompareRepresentation(T left, T right, bool signaling, IeeeDecimalContext context)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<int>();
        var a = IeeeDecimalCodec<T>.Decode(left); var b = IeeeDecimalCodec<T>.Decode(right);
        bool nan = a.Kind is DecimalKind.QuietNaN or DecimalKind.SignalingNaN || b.Kind is DecimalKind.QuietNaN or DecimalKind.SignalingNaN;
        bool invalid = nan && (signaling || a.Kind == DecimalKind.SignalingNaN || b.Kind == DecimalKind.SignalingNaN);
        return RepresentationResult(nan ? 2 : left < right ? -1 : left > right ? 1 : 0, invalid ? IeeeDecimalFlags.Invalid : 0, context);
    }
    /// <summary>Tests IEEE totalOrder, including sign, NaN kind and payload, and finite cohorts; never signals.</summary>
    public static IeeeDecimalResult<bool> TotalOrder(T left, T right, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<bool>();
        return RepresentationResult(TotalCompare(left, right) <= 0, 0, context);
    }
    /// <summary>Tests totalOrder after removing both signs; never signals.</summary>
    public static IeeeDecimalResult<bool> TotalOrderMag(T left, T right, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<bool>();
        return TotalOrder(Abs(left).Value, Abs(right).Value, context);
    }
    private static int TotalCompare(T left, T right)
    {
        var a = IeeeDecimalCodec<T>.Decode(left); var b = IeeeDecimalCodec<T>.Decode(right);
        if (a.Negative != b.Negative) return a.Negative ? -1 : 1;
        int direction = a.Negative ? -1 : 1;
        bool an = a.Kind is DecimalKind.QuietNaN or DecimalKind.SignalingNaN, bn = b.Kind is DecimalKind.QuietNaN or DecimalKind.SignalingNaN;
        if (an || bn)
        {
            if (an != bn) return (an ? 1 : -1) * direction;
            int c = (a.Kind == DecimalKind.QuietNaN ? 1 : 0).CompareTo(b.Kind == DecimalKind.QuietNaN ? 1 : 0);
            return (c != 0 ? c : a.Payload.CompareTo(b.Payload)) * direction;
        }
        if (left < right) return -1; if (left > right) return 1;
        return a.Exponent.CompareTo(b.Exponent) * direction;
    }
    /// <summary>Tests equal quantum; two NaNs or two infinities have equal quantum.</summary>
    public static IeeeDecimalResult<bool> SameQuantum(T left, T right, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<bool>();
        var a = IeeeDecimalCodec<T>.Decode(left); var b = IeeeDecimalCodec<T>.Decode(right);
        bool an = a.Kind is DecimalKind.QuietNaN or DecimalKind.SignalingNaN, bn = b.Kind is DecimalKind.QuietNaN or DecimalKind.SignalingNaN;
        return RepresentationResult(an && bn || a.Kind == DecimalKind.Infinity && b.Kind == DecimalKind.Infinity || a.Kind == DecimalKind.Finite && b.Kind == DecimalKind.Finite && a.Exponent == b.Exponent, 0, context);
    }
    /// <summary>Returns the stored finite quantum exponent. Nonfinite inputs have no integer quantum.</summary>
    public static IeeeDecimalResult<int> Quantum(T value, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<int>(); var p = IeeeDecimalCodec<T>.Decode(value);
        return p.Kind == DecimalKind.Finite ? RepresentationResult(p.Exponent, 0, context) : IeeeDecimalResult<int>.Fail(ArithmeticError.NonFinite);
    }
    /// <summary>Returns the next representable value toward positive infinity; NaNs are quieted.</summary>
    public static IeeeDecimalResult<T> NextUp(T value, IeeeDecimalContext context = default) => Adjacent(value, true, context);
    /// <summary>Returns the next representable value toward negative infinity; NaNs are quieted.</summary>
    public static IeeeDecimalResult<T> NextDown(T value, IeeeDecimalContext context = default) => Adjacent(value, false, context);
    private static IeeeDecimalResult<T> Adjacent(T value, bool up, IeeeDecimalContext context)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<T>(); var p = IeeeDecimalCodec<T>.Decode(value);
        if (p.Kind is DecimalKind.QuietNaN or DecimalKind.SignalingNaN) return Finish(IeeeDecimalCodec<T>.Encode(p with { Kind = DecimalKind.QuietNaN }), p.Kind == DecimalKind.SignalingNaN ? IeeeDecimalFlags.Invalid : 0, context);
        return Finish(up ? T.BitIncrement(value) : T.BitDecrement(value), 0, context);
    }
    /// <summary>Scales by an integral power of ten, rounding once under the context.</summary>
    public static IeeeDecimalResult<T> ScaleB(T value, int scale, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<T>(); var p = IeeeDecimalCodec<T>.Decode(value);
        if (p.Kind != DecimalKind.Finite) return AdjacentSpecial(value, context);
        // Saturating the exponent outside the format range bounds work without changing rounding.
        long exponent = (long)p.Exponent + scale; int bounded = (int)Math.Clamp(exponent, IeeeDecimalCodec<T>.MinExponent - IeeeDecimalCodec<T>.Precision - 2, IeeeDecimalCodec<T>.MaxExponent + IeeeDecimalCodec<T>.Precision + 2);
        return Round(p.Coefficient, BigInteger.One, bounded, p.Negative, bounded, context);
    }
    private static IeeeDecimalResult<T> AdjacentSpecial(T value, IeeeDecimalContext context)
    {
        var p = IeeeDecimalCodec<T>.Decode(value); return Finish(IeeeDecimalCodec<T>.Encode(p.Kind == DecimalKind.SignalingNaN ? p with { Kind = DecimalKind.QuietNaN } : p), p.Kind == DecimalKind.SignalingNaN ? IeeeDecimalFlags.Invalid : 0, context);
    }
    /// <summary>Rounds to an integral value in the selected direction without signaling inexact.</summary>
    public static IeeeDecimalResult<T> RoundToIntegral(T value, IeeeDecimalContext context = default) => Integral(value, false, context);
    /// <summary>Rounds to an integral value in the selected direction and signals discarded fractional digits.</summary>
    public static IeeeDecimalResult<T> RoundToIntegralExact(T value, IeeeDecimalContext context = default) => Integral(value, true, context);
    private static IeeeDecimalResult<T> Integral(T value, bool exact, IeeeDecimalContext context)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<T>(); var p = IeeeDecimalCodec<T>.Decode(value);
        if (p.Kind != DecimalKind.Finite) return AdjacentSpecial(value, context);
        if (p.Exponent >= 0) return Finish(value, 0, context);
        var divisor = BigInteger.Pow(10, -p.Exponent); var q = BigInteger.DivRem(p.Coefficient, divisor, out var remainder);
        bool increment = context.Rounding switch
        {
            IeeeDecimalRounding.TowardPositive => !p.Negative && !remainder.IsZero,
            IeeeDecimalRounding.TowardNegative => p.Negative && !remainder.IsZero,
            IeeeDecimalRounding.ToNearestAway => remainder * 2 >= divisor,
            IeeeDecimalRounding.ToNearestEven => remainder * 2 > divisor || remainder * 2 == divisor && !q.IsEven,
            _ => false
        };
        if (increment) q++;
        return Finish(IeeeDecimalCodec<T>.Encode(p with { Coefficient = q, Exponent = 0 }), exact && !remainder.IsZero ? IeeeDecimalFlags.Inexact : 0, context);
    }
    /// <summary>Extracts the canonical NaN diagnostic payload; non-NaNs return minus one without signaling.</summary>
    public static IeeeDecimalResult<BigInteger> GetPayload(T value, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<BigInteger>(); var p = IeeeDecimalCodec<T>.Decode(value);
        return RepresentationResult(p.Kind is DecimalKind.QuietNaN or DecimalKind.SignalingNaN ? p.Payload : -BigInteger.One, 0, context);
    }
    /// <summary>Creates a positive NaN with a canonical diagnostic payload, without signaling.</summary>
    public static IeeeDecimalResult<T> CreateNaN(BigInteger payload, bool signaling = false, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context) || payload < 0 || payload >= BigInteger.Pow(10, IeeeDecimalCodec<T>.Precision - 1)) return RepresentationFailure<T>();
        return Finish(IeeeDecimalCodec<T>.Encode(new(false, 0, 0, signaling ? DecimalKind.SignalingNaN : DecimalKind.QuietNaN, payload)), 0, context);
    }
    /// <summary>Creates a quiet NaN from an admissible nonnegative integral payload operand; otherwise returns positive zero without signaling.</summary>
    public static IeeeDecimalResult<T> SetPayload(T payload) => PayloadOperand(payload, false, default);
    /// <summary>Creates a quiet NaN under an explicit context; an inadmissible operand returns positive zero without signaling.</summary>
    public static IeeeDecimalResult<T> SetPayload(T payload, IeeeDecimalContext context) => PayloadOperand(payload, false, context);
    /// <summary>Creates a signaling NaN from an admissible nonnegative integral payload operand; otherwise returns positive zero without signaling.</summary>
    public static IeeeDecimalResult<T> SetPayloadSignaling(T payload) => PayloadOperand(payload, true, default);
    /// <summary>Creates a signaling NaN from a source-format operand under an explicit context, without signaling.</summary>
    public static IeeeDecimalResult<T> SetPayloadSignaling(T payload, IeeeDecimalContext context) => PayloadOperand(payload, true, context);
    /// <summary>Creates a quiet NaN from an exact built-in integer payload operand; inadmissible payloads return positive zero.</summary>
    public static IeeeDecimalResult<T> SetPayload<TInteger>(TInteger payload) where TInteger : struct, IBinaryInteger<TInteger> => IntegerPayload(payload, false, default);
    /// <summary>Creates a quiet NaN from an exact integer payload under an explicit context.</summary>
    public static IeeeDecimalResult<T> SetPayload<TInteger>(TInteger payload, IeeeDecimalContext context) where TInteger : struct, IBinaryInteger<TInteger> => IntegerPayload(payload, false, context);
    /// <summary>Creates a signaling NaN from an exact built-in integer payload operand without raising Invalid.</summary>
    public static IeeeDecimalResult<T> SetPayloadSignaling<TInteger>(TInteger payload) where TInteger : struct, IBinaryInteger<TInteger> => IntegerPayload(payload, true, default);
    /// <summary>Creates a signaling NaN from an exact integer payload under an explicit context.</summary>
    public static IeeeDecimalResult<T> SetPayloadSignaling<TInteger>(TInteger payload, IeeeDecimalContext context) where TInteger : struct, IBinaryInteger<TInteger> => IntegerPayload(payload, true, context);
    private static IeeeDecimalResult<T> IntegerPayload<TInteger>(TInteger payload, bool signaling, IeeeDecimalContext context) where TInteger : struct, IBinaryInteger<TInteger>
    {
        if (!Valid(context) || !ConversionIntegerSupported<TInteger>()) return RepresentationFailure<T>();
        var integer = BigInteger.CreateChecked(payload);
        if (integer < 0 || integer >= BigInteger.Pow(10, IeeeDecimalCodec<T>.Precision - 1)) return Finish(IeeeDecimalCodec<T>.Encode(new(false, 0, 0)), 0, context);
        return CreateNaN(integer, signaling, context);
    }
    private static IeeeDecimalResult<T> PayloadOperand(T payload, bool signaling, IeeeDecimalContext context)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<T>();
        var p = IeeeDecimalCodec<T>.Decode(payload);
        if (p.Kind != DecimalKind.Finite || p.Negative && !p.Coefficient.IsZero || !TryInteger(p, out var integer) || integer >= BigInteger.Pow(10, IeeeDecimalCodec<T>.Precision - 1)) return Finish(IeeeDecimalCodec<T>.Encode(new(false, 0, 0)), 0, context);
        return CreateNaN(integer, signaling, context);
    }
    /// <summary>Returns the unsigned binary-integer decimal interchange encoding.</summary>
    public static IeeeDecimalResult<BigInteger> EncodeBinary(T value, IeeeDecimalContext context = default) => EncodeInterchange(value, false, context);
    /// <summary>Returns the unsigned densely-packed decimal interchange encoding.</summary>
    public static IeeeDecimalResult<BigInteger> EncodeDecimal(T value, IeeeDecimalContext context = default) => EncodeInterchange(value, true, context);
    private static IeeeDecimalResult<BigInteger> EncodeInterchange(T value, bool dpd, IeeeDecimalContext context)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<BigInteger>();
        BigInteger raw = typeof(T) == typeof(Decimal32) ? dpd ? Decimal32.EncodeDecimal((Decimal32)(object)value) : Decimal32.EncodeBinary((Decimal32)(object)value) : typeof(T) == typeof(Decimal64) ? dpd ? Decimal64.EncodeDecimal((Decimal64)(object)value) : Decimal64.EncodeBinary((Decimal64)(object)value) : dpd ? (BigInteger)Decimal128.EncodeDecimal((Decimal128)(object)value) : (BigInteger)Decimal128.EncodeBinary((Decimal128)(object)value);
        return RepresentationResult(raw, 0, context);
    }
    /// <summary>Decodes an unsigned binary-integer decimal encoding, preserving special values.</summary>
    public static IeeeDecimalResult<T> DecodeBinary(BigInteger encoding, IeeeDecimalContext context = default) => DecodeInterchange(encoding, false, context);
    /// <summary>Decodes an unsigned densely-packed decimal encoding, preserving special values.</summary>
    public static IeeeDecimalResult<T> DecodeDecimal(BigInteger encoding, IeeeDecimalContext context = default) => DecodeInterchange(encoding, true, context);
    private static IeeeDecimalResult<T> DecodeInterchange(BigInteger encoding, bool dpd, IeeeDecimalContext context)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<T>(); int bits = typeof(T) == typeof(Decimal32) ? 32 : typeof(T) == typeof(Decimal64) ? 64 : 128;
        if (encoding < 0 || encoding >= BigInteger.One << bits) return RepresentationFailure<T>();
        T result = typeof(T) == typeof(Decimal32) ? (T)(object)(dpd ? Decimal32.DecodeDecimal((uint)encoding) : Decimal32.DecodeBinary((uint)encoding)) : typeof(T) == typeof(Decimal64) ? (T)(object)(dpd ? Decimal64.DecodeDecimal((ulong)encoding) : Decimal64.DecodeBinary((ulong)encoding)) : (T)(object)(dpd ? Decimal128.DecodeDecimal((UInt128)encoding) : Decimal128.DecodeBinary((UInt128)encoding));
        return Finish(result, 0, context);
    }
    /// <summary>Parses invariant decimal syntax, Infinity, NaN or sNaN with optional diagnostic digits, rounding once under the context. Whitespace and grouping are rejected.</summary>
    public static IeeeDecimalResult<T> Parse(ReadOnlySpan<char> text, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context) || text.IsEmpty) return RepresentationFailure<T>();
        bool negative = text[0] == '-'; if (text[0] is '-' or '+') text = text[1..]; if (text.IsEmpty) return RepresentationFailure<T>();
        if (text.SequenceEqual("Infinity")) return Finish(IeeeDecimalCodec<T>.Encode(new(negative, 0, 0, DecimalKind.Infinity)), 0, context);
        bool signaling = text.StartsWith("sNaN", StringComparison.Ordinal); bool nan = signaling || text.StartsWith("NaN", StringComparison.Ordinal);
        if (nan)
        {
            var digits = text[(signaling ? 4 : 3)..]; BigInteger payload = 0;
            foreach (char c in digits) { if (c < '0' || c > '9') return RepresentationFailure<T>(); payload = payload * 10 + c - '0'; }
            var result = CreateNaN(payload, signaling, context); return result.IsSuccess ? SetSign(result.Value, negative, context) : result;
        }
        BigInteger coefficient = 0; int fractional = 0; bool point = false, any = false; int index = 0;
        for (; index < text.Length && text[index] is not ('e' or 'E'); index++)
        {
            char c = text[index]; if (c == '.' && !point) { point = true; continue; }
            if (c < '0' || c > '9') return RepresentationFailure<T>(); any = true; coefficient = coefficient * 10 + c - '0'; if (point) fractional++;
        }
        if (!any) return RepresentationFailure<T>();
        long exponent = 0;
        if (index < text.Length)
        {
            var exp = text[(index + 1)..]; if (exp.IsEmpty) return RepresentationFailure<T>();
            bool exponentNegative = exp[0] == '-'; if (exp[0] is '-' or '+') exp = exp[1..]; if (exp.IsEmpty) return RepresentationFailure<T>();
            // Validate even after saturation. Integer parsing accepts trailing
            // NULs and rejects syntactically valid very-large exponents. The
            // bound exceeds every possible span-length adjustment, preserving
            // the destination rounding decision without unbounded exponent work.
            const long limit = long.MaxValue / 4;
            foreach (char c in exp) { if (c < '0' || c > '9') return RepresentationFailure<T>(); exponent = exponent > (limit - (c - '0')) / 10 ? limit : exponent * 10 + c - '0'; }
            if (exponentNegative) exponent = -exponent;
        }
        long effective = exponent - fractional;
        // Exponents beyond the representable envelope share the same overflow/underflow result.
        int bounded = (int)Math.Clamp(effective, (long)IeeeDecimalCodec<T>.MinExponent - coefficient.ToString(CultureInfo.InvariantCulture).Length - 2, IeeeDecimalCodec<T>.MaxExponent + IeeeDecimalCodec<T>.Precision + 2);
        return Round(coefficient, BigInteger.One, bounded, negative, bounded, context);
    }
    /// <summary>Writes invariant exact coefficient/exponent syntax, including signed zeros, NaN signaling state and payload. Returns characters written or a typed failure when the destination is too short.</summary>
    public static IeeeDecimalResult<int> Format(T value, Span<char> destination, IeeeDecimalContext context = default)
    {
        if (!RepresentationValid(context)) return RepresentationFailure<int>(); var p = IeeeDecimalCodec<T>.Decode(value);
        string sign = p.Negative ? "-" : "";
        string text = p.Kind switch
        {
            DecimalKind.Infinity => sign + "Infinity",
            DecimalKind.SignalingNaN => sign + "sNaN" + p.Payload.ToString(CultureInfo.InvariantCulture),
            DecimalKind.QuietNaN => sign + "NaN" + p.Payload.ToString(CultureInfo.InvariantCulture),
            _ => sign + p.Coefficient.ToString(CultureInfo.InvariantCulture) + "E" + p.Exponent.ToString(CultureInfo.InvariantCulture)
        };
        if (!text.AsSpan().TryCopyTo(destination)) return RepresentationFailure<int>(); return RepresentationResult(text.Length, 0, context);
    }
}





