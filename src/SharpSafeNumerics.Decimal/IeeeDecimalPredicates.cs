// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
namespace SharpSafeNumerics;

public static partial class IeeeDecimal<T> where T : struct, IFloatingPointIeee754<T>
{
    /// <summary>Evaluates the quiet Equal predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietEqual(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 0, false, context);
    /// <summary>Evaluates the signaling Equal predicate; every NaN raises Invalid.</summary>
    public static IeeeDecimalResult<bool> SignalingEqual(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 0, true, context);
    /// <summary>Evaluates the quiet NotEqual predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietNotEqual(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 1, false, context);
    /// <summary>Evaluates the signaling NotEqual predicate; every NaN raises Invalid.</summary>
    public static IeeeDecimalResult<bool> SignalingNotEqual(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 1, true, context);
    /// <summary>Evaluates the quiet Greater predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietGreater(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 2, false, context);
    /// <summary>Evaluates the signaling Greater predicate; every NaN raises Invalid.</summary>
    public static IeeeDecimalResult<bool> SignalingGreater(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 2, true, context);
    /// <summary>Evaluates the quiet GreaterEqual predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietGreaterEqual(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 3, false, context);
    /// <summary>Evaluates the signaling GreaterEqual predicate; every NaN raises Invalid.</summary>
    public static IeeeDecimalResult<bool> SignalingGreaterEqual(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 3, true, context);
    /// <summary>Evaluates the quiet Less predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietLess(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 4, false, context);
    /// <summary>Evaluates the signaling Less predicate; every NaN raises Invalid.</summary>
    public static IeeeDecimalResult<bool> SignalingLess(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 4, true, context);
    /// <summary>Evaluates the quiet LessEqual predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietLessEqual(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 5, false, context);
    /// <summary>Evaluates the signaling LessEqual predicate; every NaN raises Invalid.</summary>
    public static IeeeDecimalResult<bool> SignalingLessEqual(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 5, true, context);
    /// <summary>Evaluates the quiet Unordered predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietUnordered(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 6, false, context);
    /// <summary>Evaluates the quiet NotGreater predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietNotGreater(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 7, false, context);
    /// <summary>Evaluates the signaling NotGreater predicate; every NaN raises Invalid.</summary>
    public static IeeeDecimalResult<bool> SignalingNotGreater(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 7, true, context);
    /// <summary>Evaluates the quiet LessUnordered predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietLessUnordered(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 8, false, context);
    /// <summary>Evaluates the signaling LessUnordered predicate; every NaN raises Invalid.</summary>
    public static IeeeDecimalResult<bool> SignalingLessUnordered(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 8, true, context);
    /// <summary>Evaluates the quiet NotLess predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietNotLess(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 9, false, context);
    /// <summary>Evaluates the signaling NotLess predicate; every NaN raises Invalid.</summary>
    public static IeeeDecimalResult<bool> SignalingNotLess(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 9, true, context);
    /// <summary>Evaluates the quiet GreaterUnordered predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietGreaterUnordered(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 10, false, context);
    /// <summary>Evaluates the signaling GreaterUnordered predicate; every NaN raises Invalid.</summary>
    public static IeeeDecimalResult<bool> SignalingGreaterUnordered(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 10, true, context);
    /// <summary>Evaluates the quiet Ordered predicate; only signaling NaNs raise Invalid.</summary>
    public static IeeeDecimalResult<bool> QuietOrdered(T left, T right, IeeeDecimalContext context = default) => Predicate(left, right, 11, false, context);

    private static IeeeDecimalResult<bool> Predicate(T left, T right, int predicate, bool signaling, IeeeDecimalContext context)
    {
        var compared = signaling ? CompareSignaling(left, right, context) : CompareQuiet(left, right, context);
        if (!compared.IsSuccess) return IeeeDecimalResult<bool>.Fail(compared.Error, compared.Flags);
        int c = compared.Value;
        bool value = predicate switch
        {
            0 => c == 0, // Equal
            1 => c != 0, // NotEqual
            2 => c == 1, // Greater
            3 => c == 1 || c == 0, // GreaterEqual
            4 => c == -1, // Less
            5 => c == -1 || c == 0, // LessEqual
            6 => c == 2, // Unordered
            7 => c != 1, // NotGreater
            8 => c == -1 || c == 2, // LessUnordered
            9 => c != -1, // NotLess
            10 => c == 1 || c == 2, // GreaterUnordered
            11 => c != 2, // Ordered
            _ => throw new InvalidOperationException("Unknown internal predicate.")
        };
        return RepresentationResult(value, compared.Flags, context);
    }
    /// <summary>Returns the format's arithmetic radix, ten, without changing IEEE flags.</summary>
    public static IeeeDecimalResult<int> Radix(IeeeDecimalContext context = default) => Valid(context) ? RepresentationResult(10, 0, context) : IeeeDecimalResult<int>.Fail(ArithmeticError.OutOfRange);
    /// <summary>Tests whether the operand is finite; signaling NaNs remain unchanged.</summary>
    public static IeeeDecimalResult<bool> IsFinite(T value, IeeeDecimalContext context = default) => Classification(value, 0, context);
    /// <summary>Tests for either signed infinity without signaling on NaNs.</summary>
    public static IeeeDecimalResult<bool> IsInfinite(T value, IeeeDecimalContext context = default) => Classification(value, 1, context);
    /// <summary>Tests for a quiet or signaling NaN without quieting it.</summary>
    public static IeeeDecimalResult<bool> IsNaN(T value, IeeeDecimalContext context = default) => Classification(value, 2, context);
    /// <summary>Tests for either signed zero, including noncanonical zero encodings.</summary>
    public static IeeeDecimalResult<bool> IsZero(T value, IeeeDecimalContext context = default) => Classification(value, 3, context);
    /// <summary>Tests for a normal finite nonzero operand.</summary>
    public static IeeeDecimalResult<bool> IsNormal(T value, IeeeDecimalContext context = default) => Classification(value, 4, context);
    /// <summary>Tests for a nonzero finite operand below the normal exponent range.</summary>
    public static IeeeDecimalResult<bool> IsSubnormal(T value, IeeeDecimalContext context = default) => Classification(value, 5, context);
    private static IeeeDecimalResult<bool> Classification(T value, int predicate, IeeeDecimalContext context)
    {
        if (!Valid(context)) return IeeeDecimalResult<bool>.Fail(ArithmeticError.OutOfRange);
        var p = IeeeDecimalCodec<T>.Decode(value);
        bool finite = p.Kind == DecimalKind.Finite;
        bool zero = finite && p.Coefficient.IsZero;
        bool subnormal = finite && !zero && Digits(p.Coefficient) - 1 + p.Exponent < IeeeDecimalCodec<T>.MinExponent + IeeeDecimalCodec<T>.Precision - 1;
        bool result = predicate switch { 0 => finite, 1 => p.Kind == DecimalKind.Infinity, 2 => NaN(p), 3 => zero, 4 => finite && !zero && !subnormal, 5 => subnormal, _ => false };
        return RepresentationResult(result, 0, context);
    }
    /// <summary>Returns the integral base-ten exponent; zero returns negative infinity with DivisionByZero.</summary>
    public static IeeeDecimalResult<T> LogB(T value, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var p = IeeeDecimalCodec<T>.Decode(value);
        if (NaN(p)) return Propagate(context, p);
        if (p.Kind == DecimalKind.Infinity) return Special(p with { Negative = false }, context);
        if (p.Coefficient.IsZero) return Special(new(true, 0, 0, DecimalKind.Infinity), context, IeeeDecimalFlags.DivisionByZero);
        int exponent = Digits(p.Coefficient) - 1 + p.Exponent;
        return Round(BigInteger.Abs(exponent), 1, 0, exponent < 0, 0, context);
    }
    /// <summary>Returns ten raised to the operand's quantum exponent; infinity maps to positive infinity.</summary>
    public static IeeeDecimalResult<T> GetQuantum(T value, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var p = IeeeDecimalCodec<T>.Decode(value);
        if (NaN(p)) return Propagate(context, p);
        return p.Kind == DecimalKind.Infinity ? Special(p with { Negative = false }, context) : Special(new(false, 1, p.Exponent), context);
    }
}

