// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SharpSafeNumerics;

/// <summary>Exact-source integer and binary interchange conversions for decimal formats.</summary>
public static partial class IeeeDecimal<T> where T : struct, IFloatingPointIeee754<T>
{
    /// <summary>Converts a built-in integer or BigInteger directly, with one decimal rounding.</summary>
    public static IeeeDecimalResult<T> FromInteger<TInteger>(TInteger value, IeeeDecimalContext context = default)
        where TInteger : struct, IBinaryInteger<TInteger>
    {
        if (!Valid(context) || !ConversionIntegerSupported<TInteger>()) return RepresentationFailure<T>();
        BigInteger integer = BigInteger.CreateChecked(value);
        return Round(BigInteger.Abs(integer), BigInteger.One, 0, integer.Sign < 0, 0, context);
    }

    /// <summary>
    /// Converts to a built-in integer or BigInteger in the context rounding direction.
    /// Invalid (NaN, infinity, or rounded out-of-range) returns the selected integer zero
    /// with Invalid unless trapped. When exact is false, discarded fractions do not signal Inexact.
    /// </summary>
    public static IeeeDecimalResult<TInteger> ToInteger<TInteger>(T value, IeeeDecimalContext context = default, bool exact = true)
        where TInteger : struct, IBinaryInteger<TInteger>
    {
        if (!Valid(context) || !ConversionIntegerSupported<TInteger>()) return RepresentationFailure<TInteger>();
        var part = IeeeDecimalCodec<T>.Decode(value);
        if (part.Kind != DecimalKind.Finite) return RepresentationResult(TInteger.Zero, IeeeDecimalFlags.Invalid, context);
        BigInteger numerator = part.Coefficient, denominator = BigInteger.One;
        if (part.Exponent >= 0) numerator *= Pow(part.Exponent); else denominator = Pow(-part.Exponent);
        var integer = BigInteger.DivRem(numerator, denominator, out var remainder);
        if (Increment(integer, remainder, denominator, part.Negative, context.Rounding)) integer++;
        if (part.Negative) integer = -integer;
        var (width, signed) = ConversionIntegerWidth<TInteger>();
        if (width != 0)
        {
            BigInteger minimum = signed ? -(BigInteger.One << (width - 1)) : BigInteger.Zero;
            BigInteger maximum = (BigInteger.One << (signed ? width - 1 : width)) - 1;
            if (integer < minimum || integer > maximum) return RepresentationResult(TInteger.Zero, IeeeDecimalFlags.Invalid, context);
        }
        var flags = exact && !remainder.IsZero ? IeeeDecimalFlags.Inexact : IeeeDecimalFlags.None;
        return RepresentationResult(TInteger.CreateChecked(integer), flags, context);
    }

    /// <summary>
    /// Converts Half, float or double from its exact bit-decoded rational value.
    /// Signaling NaNs are quieted and signal Invalid. Payload narrowing retains low-order
    /// decimal digits modulo 10^(destination precision-1); a fitting payload is unchanged.
    /// </summary>
    public static IeeeDecimalResult<T> FromBinary<TBinary>(TBinary value, IeeeDecimalContext context = default)
        where TBinary : struct, IFloatingPointIeee754<TBinary>
    {
        if (!Valid(context) || !ConversionBinarySupported<TBinary>()) return RepresentationFailure<T>();
        var (fractionBits, exponentBits, bias) = ConversionBinaryShape<TBinary>();
        ulong bits = ConversionBinaryBits(value), fractionMask = (1UL << fractionBits) - 1;
        bool negative = (bits & (1UL << (fractionBits + exponentBits))) != 0;
        ulong fraction = bits & fractionMask, exponent = (bits >> fractionBits) & ((1UL << exponentBits) - 1);
        if (exponent == (1UL << exponentBits) - 1)
        {
            if (fraction == 0) return Special(new(negative, 0, 0, DecimalKind.Infinity), context);
            bool signaling = (fraction & (1UL << (fractionBits - 1))) == 0;
            BigInteger payload = fraction & ((1UL << (fractionBits - 1)) - 1);
            payload %= Pow(IeeeDecimalCodec<T>.Precision - 1);
            return Special(new(negative, 0, 0, DecimalKind.QuietNaN, payload), context, signaling ? IeeeDecimalFlags.Invalid : 0);
        }
        BigInteger numerator = exponent == 0 ? fraction : fraction | (1UL << fractionBits);
        int binaryQuantum = (exponent == 0 ? 1 - bias : (int)exponent - bias) - fractionBits;
        BigInteger denominator = BigInteger.One;
        if (binaryQuantum >= 0) numerator <<= binaryQuantum; else denominator <<= -binaryQuantum;
        // Reducing the power-of-two denominator gives the shortest exact decimal quantum.
        // There is no intermediate rounding or binary64 widening for smaller binary formats.
        BigInteger gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
        numerator /= gcd; denominator /= gcd;
        int preferred = numerator.IsZero ? 0 : -(int)(denominator.GetBitLength() - 1);
        return Round(numerator, denominator, 0, negative, preferred, context);
    }

    /// <summary>
    /// Converts directly to Half, float or double with one rounding of the exact decimal rational.
    /// Binary tininess is detected after rounding to destination precision with unbounded exponent;
    /// Underflow status additionally requires inexactness. Signaling NaNs quiet with Invalid.
    /// Payload narrowing retains low-order payload bits; a fitting payload is unchanged.
    /// </summary>
    public static IeeeDecimalResult<TBinary> ToBinary<TBinary>(T value, IeeeDecimalContext context = default)
        where TBinary : struct, IFloatingPointIeee754<TBinary>
    {
        if (!Valid(context) || !ConversionBinarySupported<TBinary>()) return RepresentationFailure<TBinary>();
        var (fractionBits, exponentBits, bias) = ConversionBinaryShape<TBinary>();
        var part = IeeeDecimalCodec<T>.Decode(value);
        ulong sign = part.Negative ? 1UL << (fractionBits + exponentBits) : 0;
        ulong infinity = ((1UL << exponentBits) - 1) << fractionBits;
        if (part.Kind != DecimalKind.Finite)
        {
            ulong encoding = sign | infinity;
            if (NaN(part)) encoding |= (1UL << (fractionBits - 1)) | (ulong)(part.Payload & ((BigInteger.One << (fractionBits - 1)) - 1));
            return RepresentationResult(ConversionBinaryValue<TBinary>(encoding), part.Kind == DecimalKind.SignalingNaN ? IeeeDecimalFlags.Invalid : 0, context);
        }
        if (part.Coefficient.IsZero) return RepresentationResult(ConversionBinaryValue<TBinary>(sign), 0, context);
        BigInteger numerator = part.Coefficient, denominator = BigInteger.One;
        if (part.Exponent >= 0) numerator *= Pow(part.Exponent); else denominator = Pow(-part.Exponent);
        int adjusted = (int)(numerator.GetBitLength() - denominator.GetBitLength());
        if (adjusted >= 0 ? numerator < (denominator << adjusted) : (numerator << -adjusted) < denominator) adjusted--;
        int precision = fractionBits + 1, minimumNormal = 1 - bias, maximumNormal = bias;
        int unboundedQuantum = adjusted - fractionBits;
        // Detect binary tininess using its own unbounded-exponent precision rounding. The
        // final subnormal rounding below is still performed directly on the exact source.
        var (unboundedCoefficient, _) = ConversionRoundBinary(numerator, denominator, unboundedQuantum, part.Negative, context.Rounding);
        bool tiny = adjusted + (unboundedCoefficient >= (BigInteger.One << precision) ? 1 : 0) < minimumNormal;
        int quantum = Math.Max(unboundedQuantum, minimumNormal - fractionBits);
        var (coefficient, inexact) = ConversionRoundBinary(numerator, denominator, quantum, part.Negative, context.Rounding);
        if (coefficient >= (BigInteger.One << precision)) { coefficient >>= 1; quantum++; }
        if (quantum > maximumNormal - fractionBits)
        {
            bool toInfinity = context.Rounding is IeeeDecimalRounding.ToNearestEven or IeeeDecimalRounding.ToNearestAway ||
                context.Rounding == IeeeDecimalRounding.TowardPositive && !part.Negative || context.Rounding == IeeeDecimalRounding.TowardNegative && part.Negative;
            ulong encoding = sign | (toInfinity ? infinity : infinity - 1);
            return RepresentationResult(ConversionBinaryValue<TBinary>(encoding), IeeeDecimalFlags.Overflow | IeeeDecimalFlags.Inexact, context);
        }
        ulong packed = sign;
        if (coefficient < (BigInteger.One << fractionBits)) packed |= (ulong)coefficient;
        else
        {
            int field = quantum + fractionBits + bias;
            packed |= (ulong)field << fractionBits;
            packed |= (ulong)(coefficient - (BigInteger.One << fractionBits));
        }
        var flags = inexact ? IeeeDecimalFlags.Inexact : IeeeDecimalFlags.None;
        if (tiny && inexact) flags |= IeeeDecimalFlags.Underflow;
        return RepresentationResult(ConversionBinaryValue<TBinary>(packed), flags, context);
    }

    private static (BigInteger Coefficient, bool Inexact) ConversionRoundBinary(BigInteger numerator, BigInteger denominator, int quantum, bool negative, IeeeDecimalRounding rounding)
    {
        if (quantum >= 0) denominator <<= quantum; else numerator <<= -quantum;
        BigInteger coefficient = BigInteger.DivRem(numerator, denominator, out var remainder);
        if (Increment(coefficient, remainder, denominator, negative, rounding)) coefficient++;
        return (coefficient, !remainder.IsZero);
    }

    // Explicit carrier allowlists prevent unsupported user implementations of generic math
    // from entering reinterpretation code or injecting different conversion policies.
    private static bool ConversionBinarySupported<U>() => typeof(U) == typeof(Half) || typeof(U) == typeof(float) || typeof(U) == typeof(double);
    private static bool ConversionIntegerSupported<U>() => typeof(U) == typeof(BigInteger) || ConversionIntegerWidth<U>().Width != 0;
    private static (int Width, bool Signed) ConversionIntegerWidth<U>() =>
        typeof(U) == typeof(sbyte) ? (8, true) : typeof(U) == typeof(byte) ? (8, false) :
        typeof(U) == typeof(short) ? (16, true) : typeof(U) == typeof(ushort) ? (16, false) :
        typeof(U) == typeof(int) ? (32, true) : typeof(U) == typeof(uint) ? (32, false) :
        typeof(U) == typeof(long) ? (64, true) : typeof(U) == typeof(ulong) ? (64, false) :
        typeof(U) == typeof(Int128) ? (128, true) : typeof(U) == typeof(UInt128) ? (128, false) :
        typeof(U) == typeof(nint) ? (IntPtr.Size * 8, true) : typeof(U) == typeof(nuint) ? (IntPtr.Size * 8, false) : (0, false);
    private static (int FractionBits, int ExponentBits, int Bias) ConversionBinaryShape<U>() =>
        typeof(U) == typeof(Half) ? (10, 5, 15) : typeof(U) == typeof(float) ? (23, 8, 127) : (52, 11, 1023);

    private static ulong ConversionBinaryBits<U>(U value) where U : struct =>
        typeof(U) == typeof(Half) ? BitConverter.HalfToUInt16Bits(Unsafe.As<U, Half>(ref value)) :
        typeof(U) == typeof(float) ? BitConverter.SingleToUInt32Bits(Unsafe.As<U, float>(ref value)) :
        BitConverter.DoubleToUInt64Bits(Unsafe.As<U, double>(ref value));
    private static U ConversionBinaryValue<U>(ulong bits) where U : struct
    {
        if (typeof(U) == typeof(Half)) { Half value = BitConverter.UInt16BitsToHalf((ushort)bits); return Unsafe.As<Half, U>(ref value); }
        if (typeof(U) == typeof(float)) { float value = BitConverter.UInt32BitsToSingle((uint)bits); return Unsafe.As<float, U>(ref value); }
        double wide = BitConverter.UInt64BitsToDouble(bits); return Unsafe.As<double, U>(ref wide);
    }
}
