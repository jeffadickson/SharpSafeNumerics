// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
namespace SharpSafeNumerics;

public static partial class IeeeDecimal<T> where T : struct, IFloatingPointIeee754<T>
{
    /// <summary>Returns the lesser operand, propagating NaNs; negative zero precedes positive zero.</summary>
    public static IeeeDecimalResult<T> Minimum(T left, T right, IeeeDecimalContext context = default) => Extremum(left, right, context, false, false, false);
    /// <summary>Returns the greater operand, propagating NaNs; positive zero follows negative zero.</summary>
    public static IeeeDecimalResult<T> Maximum(T left, T right, IeeeDecimalContext context = default) => Extremum(left, right, context, true, false, false);
    /// <summary>Returns the lesser number, preferring a numeric operand over either NaN kind; signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<T> MinimumNumber(T left, T right, IeeeDecimalContext context = default) => Extremum(left, right, context, false, false, true);
    /// <summary>Returns the greater number, preferring a numeric operand over either NaN kind; signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<T> MaximumNumber(T left, T right, IeeeDecimalContext context = default) => Extremum(left, right, context, true, false, true);
    /// <summary>Returns the lesser magnitude, breaking equal magnitudes by minimum and propagating NaNs.</summary>
    public static IeeeDecimalResult<T> MinimumMagnitude(T left, T right, IeeeDecimalContext context = default) => Extremum(left, right, context, false, true, false);
    /// <summary>Returns the greater magnitude, breaking equal magnitudes by maximum and propagating NaNs.</summary>
    public static IeeeDecimalResult<T> MaximumMagnitude(T left, T right, IeeeDecimalContext context = default) => Extremum(left, right, context, true, true, false);
    /// <summary>Returns the lesser numeric magnitude, breaking ties by minimum; signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<T> MinimumMagnitudeNumber(T left, T right, IeeeDecimalContext context = default) => Extremum(left, right, context, false, true, true);
    /// <summary>Returns the greater numeric magnitude, breaking ties by maximum; signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<T> MaximumMagnitudeNumber(T left, T right, IeeeDecimalContext context = default) => Extremum(left, right, context, true, true, true);
    private static IeeeDecimalResult<T> Extremum(T left, T right, IeeeDecimalContext c, bool maximum, bool magnitude, bool number)
    {
        if (!Valid(c)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(left); var b = IeeeDecimalCodec<T>.Decode(right);
        if (NaN(a) || NaN(b))
        {
            if (number && NaN(a) != NaN(b)) return Special(NaN(a) ? b : a, c, a.Kind == DecimalKind.SignalingNaN || b.Kind == DecimalKind.SignalingNaN ? IeeeDecimalFlags.Invalid : 0);
            return Propagate(c, a, b);
        }
        int order = ReductionMagnitudeCompare(a, b);
        if (!magnitude && a.Negative != b.Negative) order = a.Negative ? -1 : 1;
        else if (!magnitude && a.Negative) order = -order;
        // Equal numerical cohorts retain the left operand; opposite signs use numeric order even in magnitude operations.
        if (order == 0 && a.Negative != b.Negative) order = a.Negative ? -1 : 1;
        return Special((maximum ? order >= 0 : order <= 0) ? a : b, c);
    }
    private static int ReductionMagnitudeCompare(DecimalParts a, DecimalParts b)
    {
        if (a.Kind == DecimalKind.Infinity || b.Kind == DecimalKind.Infinity) return a.Kind == b.Kind ? 0 : a.Kind == DecimalKind.Infinity ? 1 : -1;
        int e = Math.Min(a.Exponent, b.Exponent);
        return (a.Coefficient * Pow(a.Exponent - e)).CompareTo(b.Coefficient * Pow(b.Exponent - e));
    }
    /// <summary>Accumulates an exact sum and rounds once. Preferred quantum is the least input quantum; empty input returns +0E0.</summary>
    /// <remarks>Exact cancellation uses negative zero only toward negative infinity. Uniformly signed zero terms retain their sign.</remarks>
    public static IeeeDecimalResult<T> Sum(ReadOnlySpan<T> values, IeeeDecimalContext context = default) => Reduce(values, default, context, 0);
    /// <summary>Accumulates exact absolute values and rounds once; zero results are positive.</summary>
    public static IeeeDecimalResult<T> AbsSum(ReadOnlySpan<T> values, IeeeDecimalContext context = default) => Reduce(values, default, context, 1);
    /// <summary>Accumulates exact squares without intermediate rounding. Preferred quantum is the least doubled input quantum.</summary>
    public static IeeeDecimalResult<T> SumSquare(ReadOnlySpan<T> values, IeeeDecimalContext context = default) => Reduce(values, default, context, 2);
    /// <summary>Accumulates exact products and rounds once. Unequal lengths return OutOfRange with cleared storage.</summary>
    /// <remarks>Preferred quantum is the least product quantum. Empty input returns +0E0; zero signs follow Sum.</remarks>
    public static IeeeDecimalResult<T> Dot(ReadOnlySpan<T> left, ReadOnlySpan<T> right, IeeeDecimalContext context = default) => Reduce(left, right, context, 3);
    /// <summary>Returns the product as fraction times ten to an unbounded scale; empty input represents one as 0.1E1.</summary>
    /// <remarks>Finite nonzero fractions have magnitude in [0.1,1). Only the final normalized fraction is rounded.</remarks>
    public static IeeeDecimalResult<ScaledDecimal<T>> ScaledProd(ReadOnlySpan<T> values, IeeeDecimalContext context = default) => ScaledProduct(values, default, context, 0);
    /// <summary>Returns the scaled product of exact pairwise sums, with one final fraction rounding; lengths must match.</summary>
    public static IeeeDecimalResult<ScaledDecimal<T>> ScaledProdSum(ReadOnlySpan<T> left, ReadOnlySpan<T> right, IeeeDecimalContext context = default) => ScaledProduct(left, right, context, 1);
    /// <summary>Returns the scaled product of exact pairwise differences, with one final fraction rounding; lengths must match.</summary>
    public static IeeeDecimalResult<ScaledDecimal<T>> ScaledProdDiff(ReadOnlySpan<T> left, ReadOnlySpan<T> right, IeeeDecimalContext context = default) => ScaledProduct(left, right, context, 2);
    private static IeeeDecimalResult<ScaledDecimal<T>> ScaledProduct(ReadOnlySpan<T> values, ReadOnlySpan<T> right, IeeeDecimalContext c, int operation)
    {
        if (!Valid(c) || operation != 0 && values.Length != right.Length) return IeeeDecimalResult<ScaledDecimal<T>>.Fail(ArithmeticError.OutOfRange);
        BigInteger coefficient = 1, scale = 0; bool sign = false, infinity = false, zero = false, invalid = false; DecimalParts? quiet = null, signaling = null;
        for (int i = 0; i < values.Length; i++)
        {
            var a = IeeeDecimalCodec<T>.Decode(values[i]); var b = operation == 0 ? default : IeeeDecimalCodec<T>.Decode(right[i]);
            if (NaN(a)) { if (a.Kind == DecimalKind.SignalingNaN) signaling ??= a; else quiet ??= a; }
            if (operation != 0 && NaN(b)) { if (b.Kind == DecimalKind.SignalingNaN) signaling ??= b; else quiet ??= b; }
            if (NaN(a) || operation != 0 && NaN(b)) continue;
            if (operation != 0)
            {
                if (operation == 2) b = b with { Negative = !b.Negative };
                if (a.Kind == DecimalKind.Infinity || b.Kind == DecimalKind.Infinity)
                {
                    if (a.Kind == b.Kind && a.Negative != b.Negative) { invalid = true; continue; }
                    a = a.Kind == DecimalKind.Infinity ? a : b;
                }
                else
                {
                    int e = Math.Min(a.Exponent, b.Exponent);
                    var n = (a.Negative ? -a.Coefficient : a.Coefficient) * Pow(a.Exponent - e) + (b.Negative ? -b.Coefficient : b.Coefficient) * Pow(b.Exponent - e);
                    bool negativeZero = a.Negative && b.Negative || a.Negative != b.Negative && c.Rounding == IeeeDecimalRounding.TowardNegative;
                    a = new(n.Sign < 0 || n.IsZero && negativeZero, BigInteger.Abs(n), e);
                }
            }
            sign ^= a.Negative;
            if (a.Kind == DecimalKind.Infinity) infinity = true;
            else if (a.Coefficient.IsZero) zero = true;
            else { coefficient *= a.Coefficient; scale += a.Exponent; }
        }
        invalid |= zero && infinity;
        IeeeDecimalResult<T> result;
        if (signaling.HasValue || quiet.HasValue) result = Special((signaling ?? quiet)!.Value with { Kind = DecimalKind.QuietNaN }, c, invalid || signaling.HasValue ? IeeeDecimalFlags.Invalid : 0);
        else if (invalid) result = Invalid(c);
        else if (infinity) result = Special(new(sign, 0, 0, DecimalKind.Infinity), c);
        else if (zero) result = Special(new(sign, 0, 0), c);
        else
        {
            int digits = Digits(coefficient); scale += digits;
            result = Round(coefficient, 1, -digits, sign, -IeeeDecimalCodec<T>.Precision, c);
            if (result.IsSuccess)
            {
                var p = IeeeDecimalCodec<T>.Decode(result.Value);
                // Rounding .999... upward may produce one; renormalize without a second rounding.
                if (p.Coefficient * Pow(Math.Max(p.Exponent, 0)) >= Pow(Math.Max(-p.Exponent, 0)))
                { result = Special(p with { Exponent = p.Exponent - 1 }, c, result.Flags); scale++; }
            }
        }
        if (!result.IsSuccess) return IeeeDecimalResult<ScaledDecimal<T>>.Fail(result.Error, result.Flags);
        if (invalid || infinity || zero || signaling.HasValue || quiet.HasValue) scale = 0;
        return IeeeDecimalResult<ScaledDecimal<T>>.Ok(new(result.Value, scale), result.Flags);
    }
    private static IeeeDecimalResult<T> Reduce(ReadOnlySpan<T> values, ReadOnlySpan<T> right, IeeeDecimalContext c, int operation)
    {
        if (!Valid(c) || operation == 3 && values.Length != right.Length) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        BigInteger accumulator = 0; int exponent = 0; bool haveFinite = false, positive = false, negative = false, positiveInfinity = false, negativeInfinity = false, invalid = false;
        DecimalParts? quiet = null, signaling = null;
        for (int i = 0; i < values.Length; i++)
        {
            var a = IeeeDecimalCodec<T>.Decode(values[i]); var b = operation == 3 ? IeeeDecimalCodec<T>.Decode(right[i]) : a;
            if (NaN(a)) { if (a.Kind == DecimalKind.SignalingNaN) signaling ??= a; else quiet ??= a; }
            if (operation == 3 && NaN(b)) { if (b.Kind == DecimalKind.SignalingNaN) signaling ??= b; else quiet ??= b; }
            if (NaN(a) || operation == 3 && NaN(b)) continue;
            bool product = operation >= 2; bool sign = operation == 1 || operation == 2 ? false : operation == 3 ? a.Negative ^ b.Negative : a.Negative;
            if (product && (a.Kind == DecimalKind.Infinity && b.Kind == DecimalKind.Finite && b.Coefficient.IsZero || b.Kind == DecimalKind.Infinity && a.Kind == DecimalKind.Finite && a.Coefficient.IsZero)) { invalid = true; continue; }
            if (a.Kind == DecimalKind.Infinity || product && b.Kind == DecimalKind.Infinity) { if (sign) negativeInfinity = true; else positiveInfinity = true; continue; }
            var coefficient = product ? a.Coefficient * b.Coefficient : a.Coefficient; int e = product ? a.Exponent + b.Exponent : a.Exponent;
            if (!haveFinite) { exponent = e; haveFinite = true; }
            else if (e < exponent) { accumulator *= Pow(exponent - e); exponent = e; }
            accumulator += (sign ? -coefficient : coefficient) * Pow(e - exponent);
            if (sign) negative = true; else positive = true;
        }
        invalid |= positiveInfinity && negativeInfinity;
        var flags = invalid || signaling.HasValue ? IeeeDecimalFlags.Invalid : IeeeDecimalFlags.None;
        if (signaling.HasValue || quiet.HasValue) return Special((signaling ?? quiet)!.Value with { Kind = DecimalKind.QuietNaN }, c, flags);
        if (invalid) return Invalid(c);
        if (positiveInfinity || negativeInfinity) return Special(new(negativeInfinity, 0, 0, DecimalKind.Infinity), c);
        bool resultNegative = accumulator.Sign < 0 || accumulator.IsZero && negative && (!positive || c.Rounding == IeeeDecimalRounding.TowardNegative);
        return Round(BigInteger.Abs(accumulator), 1, exponent, resultNegative, exponent, c);
    }
}

/// <summary>A scaled decimal value representing Fraction times ten raised to Scale.</summary>
/// <param name="Fraction">A signed normalized fraction in [0.1,1) in magnitude, or signed zero, infinity or NaN.</param>
/// <param name="Scale">Unbounded decimal scale; zero for special and zero fractions.</param>
public readonly record struct ScaledDecimal<T>(T Fraction, BigInteger Scale) where T : struct;
