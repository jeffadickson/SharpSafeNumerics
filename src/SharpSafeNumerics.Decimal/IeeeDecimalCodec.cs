// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SharpSafeNumerics;

/// <summary>Classifies the representation categories carried by an IEEE decimal value.</summary>
internal enum DecimalKind { Finite, Infinity, QuietNaN, SignalingNaN }

/// <summary>
/// Holds a decoded decimal representation. Finite values denote
/// <c>(Negative ? -1 : 1) * Coefficient * 10^Exponent</c>; NaNs use <see cref="Payload"/>.
/// </summary>
internal readonly record struct DecimalParts(
    bool Negative,
    BigInteger Coefficient,
    int Exponent,
    DecimalKind Kind = DecimalKind.Finite,
    BigInteger Payload = default);

/// <summary>
/// Encodes and decodes .NET decimal carriers through their unsigned
/// binary-integer-decimal (BID) interchange representations.
/// </summary>
/// <remarks>
/// Text conversion is unsuitable here because it loses finite cohorts as well
/// as NaN signaling state and diagnostic payload bits.
/// </remarks>
internal static class IeeeDecimalCodec<T> where T : struct, IFloatingPointIeee754<T>
{
    /// <summary>Whether <typeparamref name="T"/> is a supported decimal carrier.</summary>
    internal static bool Supported => typeof(T) == typeof(Decimal32) || typeof(T) == typeof(Decimal64) || typeof(T) == typeof(Decimal128);

    /// <summary>Maximum significant decimal digits in the selected format.</summary>
    internal static int Precision => typeof(T) == typeof(Decimal32) ? 7 : typeof(T) == typeof(Decimal64) ? 16 : 34;

    /// <summary>Minimum unbiased quantum exponent in the selected format.</summary>
    internal static int MinExponent => typeof(T) == typeof(Decimal32) ? -101 : typeof(T) == typeof(Decimal64) ? -398 : -6176;

    /// <summary>Maximum unbiased quantum exponent in the selected format.</summary>
    internal static int MaxExponent => typeof(T) == typeof(Decimal32) ? 90 : typeof(T) == typeof(Decimal64) ? 369 : 6111;

    private static int Width => typeof(T) == typeof(Decimal32) ? 32 : typeof(T) == typeof(Decimal64) ? 64 : 128;
    private static int PayloadBits => typeof(T) == typeof(Decimal32) ? 20 : typeof(T) == typeof(Decimal64) ? 50 : 110;
    private static int ExponentBits => typeof(T) == typeof(Decimal32) ? 8 : typeof(T) == typeof(Decimal64) ? 10 : 14;

    /// <summary>Maps a raw NaN payload to its canonical decimal diagnostic.</summary>
    private static BigInteger CanonicalPayload(BigInteger bits)
    {
        var payload = bits & ((BigInteger.One << PayloadBits) - 1);
        return payload < BigInteger.Pow(10, Precision - 1) ? payload : 0;
    }

    /// <summary>Decodes sign, coefficient, quantum exponent, and special-value metadata.</summary>
    internal static DecimalParts Decode(T value)
    {
        // Normalize each BCL carrier to BigInteger so all formats share one
        // audited layout calculation without truncating Decimal128.
        BigInteger bits = typeof(T) == typeof(Decimal32)
            ? Decimal32.EncodeBinary(Unsafe.As<T, Decimal32>(ref value))
            : typeof(T) == typeof(Decimal64)
                ? Decimal64.EncodeBinary(Unsafe.As<T, Decimal64>(ref value))
                : (BigInteger)Decimal128.EncodeBinary(Unsafe.As<T, Decimal128>(ref value));
        int width = Width;
        int coefficientBits = width - ExponentBits - 1;
        bool negative = !((bits >> (width - 1)) & 1).IsZero;

        // Combination values 30 and 31 reserve infinity and NaN. For NaN,
        // the following bit distinguishes quiet from signaling representations.
        int combination = (int)((bits >> (width - 6)) & 31);
        if (combination >= 30)
        {
            var kind = combination == 30
                ? DecimalKind.Infinity
                : ((bits >> (width - 7)) & 1).IsZero ? DecimalKind.QuietNaN : DecimalKind.SignalingNaN;
            return new(negative, 0, 0, kind, CanonicalPayload(bits));
        }

        // Steering bits select the large-coefficient layout. Restore its
        // implicit high coefficient bit after extracting the stored suffix.
        bool steering = ((bits >> (width - 3)) & 3) == 3;
        BigInteger coefficient = steering
            ? (bits & ((BigInteger.One << (coefficientBits - 2)) - 1)) + (BigInteger.One << coefficientBits)
            : bits & ((BigInteger.One << coefficientBits) - 1);
        int exponent = (int)((bits >> (steering ? coefficientBits - 2 : coefficientBits)) & ((1 << ExponentBits) - 1)) + MinExponent;

        // Noncanonical finite coefficients decode as zero while retaining sign
        // and quantum exponent.
        if (coefficient >= BigInteger.Pow(10, Precision))
            coefficient = 0;
        return new(negative, coefficient, exponent);
    }

    /// <summary>Encodes decoded parts into the exact BID carrier representation.</summary>
    internal static T Encode(DecimalParts value)
    {
        int width = Width;
        int coefficientBits = width - ExponentBits - 1;
        BigInteger bits = value.Negative ? BigInteger.One << (width - 1) : 0;

        if (value.Kind != DecimalKind.Finite)
        {
            bits |= (BigInteger)(value.Kind == DecimalKind.Infinity ? 30 : 31) << (width - 6);
            if (value.Kind == DecimalKind.SignalingNaN)
                bits |= BigInteger.One << (width - 7);
            if (value.Kind != DecimalKind.Infinity)
                bits |= value.Payload >= 0 && value.Payload < BigInteger.Pow(10, Precision - 1) ? value.Payload : 0;
        }
        else
        {
            int biasedExponent = value.Exponent - MinExponent;
            if (value.Coefficient < (BigInteger.One << coefficientBits))
            {
                bits |= ((BigInteger)biasedExponent << coefficientBits) | value.Coefficient;
            }
            else
            {
                // Large coefficients use the steering form and omit their
                // implicit high coefficient bit from the stored suffix.
                bits |= (BigInteger)3 << (width - 3) |
                    ((BigInteger)biasedExponent << (coefficientBits - 2)) |
                    (value.Coefficient & ((BigInteger.One << (coefficientBits - 2)) - 1));
            }
        }

        if (typeof(T) == typeof(Decimal32))
        {
            var result = Decimal32.DecodeBinary((uint)bits);
            return Unsafe.As<Decimal32, T>(ref result);
        }
        if (typeof(T) == typeof(Decimal64))
        {
            var result = Decimal64.DecodeBinary((ulong)bits);
            return Unsafe.As<Decimal64, T>(ref result);
        }
        var decimal128 = Decimal128.DecodeBinary((UInt128)bits);
        return Unsafe.As<Decimal128, T>(ref decimal128);
    }
}
