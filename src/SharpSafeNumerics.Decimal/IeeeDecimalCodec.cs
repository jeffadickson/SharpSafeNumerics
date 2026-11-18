using System.Numerics;
using System.Runtime.CompilerServices;
namespace SharpSafeNumerics;

internal enum DecimalKind { Finite, Infinity, QuietNaN, SignalingNaN }
internal readonly record struct DecimalParts(bool Negative, BigInteger Coefficient, int Exponent, DecimalKind Kind = DecimalKind.Finite, BigInteger Payload = default);
// BID interchange is decoded directly: text conversion would lose cohorts and NaN signaling/payload bits.
internal static class IeeeDecimalCodec<T> where T : struct, IFloatingPointIeee754<T>
{
    internal static bool Supported => typeof(T) == typeof(Decimal32) || typeof(T) == typeof(Decimal64) || typeof(T) == typeof(Decimal128);
    internal static int Precision => typeof(T) == typeof(Decimal32) ? 7 : typeof(T) == typeof(Decimal64) ? 16 : 34;
    internal static int MinExponent => typeof(T) == typeof(Decimal32) ? -101 : typeof(T) == typeof(Decimal64) ? -398 : -6176;
    internal static int MaxExponent => typeof(T) == typeof(Decimal32) ? 90 : typeof(T) == typeof(Decimal64) ? 369 : 6111;
    private static int Width => typeof(T) == typeof(Decimal32) ? 32 : typeof(T) == typeof(Decimal64) ? 64 : 128;
    private static int PayloadBits => typeof(T) == typeof(Decimal32) ? 20 : typeof(T) == typeof(Decimal64) ? 50 : 110;
    private static BigInteger CanonicalPayload(BigInteger bits) { var p = bits & ((BigInteger.One << PayloadBits) - 1); return p < BigInteger.Pow(10, Precision - 1) ? p : 0; }
    private static int ExponentBits => typeof(T) == typeof(Decimal32) ? 8 : typeof(T) == typeof(Decimal64) ? 10 : 14;
    internal static DecimalParts Decode(T value)
    {
        BigInteger bits = typeof(T) == typeof(Decimal32) ? Decimal32.EncodeBinary(Unsafe.As<T, Decimal32>(ref value)) : typeof(T) == typeof(Decimal64) ? Decimal64.EncodeBinary(Unsafe.As<T, Decimal64>(ref value)) : (BigInteger)Decimal128.EncodeBinary(Unsafe.As<T, Decimal128>(ref value));
        int w = Width, cb = w - ExponentBits - 1; bool neg = !((bits >> (w - 1)) & 1).IsZero;
        int combination = (int)((bits >> (w - 6)) & 31);
        if (combination >= 30) return new(neg, 0, 0, combination == 30 ? DecimalKind.Infinity : ((bits >> (w - 7)) & 1).IsZero ? DecimalKind.QuietNaN : DecimalKind.SignalingNaN, CanonicalPayload(bits));
        bool steering = ((bits >> (w - 3)) & 3) == 3;
        BigInteger coefficient = steering ? (bits & ((BigInteger.One << (cb - 2)) - 1)) + (BigInteger.One << cb) : bits & ((BigInteger.One << cb) - 1);
        int exponent = (int)((bits >> (steering ? cb - 2 : cb)) & ((1 << ExponentBits) - 1)) + MinExponent;
        if (coefficient >= BigInteger.Pow(10, Precision)) coefficient = 0;
        return new(neg, coefficient, exponent);
    }
    internal static T Encode(DecimalParts value)
    {
        int w = Width, cb = w - ExponentBits - 1; BigInteger bits = value.Negative ? BigInteger.One << (w - 1) : 0;
        if (value.Kind != DecimalKind.Finite) { bits |= (BigInteger)(value.Kind == DecimalKind.Infinity ? 30 : 31) << (w - 6); if (value.Kind == DecimalKind.SignalingNaN) bits |= BigInteger.One << (w - 7); if (value.Kind != DecimalKind.Infinity) bits |= value.Payload >= 0 && value.Payload < BigInteger.Pow(10, Precision - 1) ? value.Payload : 0; }
        else { int e = value.Exponent - MinExponent; if (value.Coefficient < (BigInteger.One << cb)) bits |= ((BigInteger)e << cb) | value.Coefficient; else bits |= (BigInteger)3 << (w - 3) | ((BigInteger)e << (cb - 2)) | (value.Coefficient & ((BigInteger.One << (cb - 2)) - 1)); }
        if (typeof(T) == typeof(Decimal32)) { var x = Decimal32.DecodeBinary((uint)bits); return Unsafe.As<Decimal32, T>(ref x); }
        if (typeof(T) == typeof(Decimal64)) { var x = Decimal64.DecodeBinary((ulong)bits); return Unsafe.As<Decimal64, T>(ref x); }
        var y = Decimal128.DecodeBinary((UInt128)bits); return Unsafe.As<Decimal128, T>(ref y);
    }
}


