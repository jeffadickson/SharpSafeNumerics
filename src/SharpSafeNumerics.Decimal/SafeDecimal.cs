// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;

namespace SharpSafeNumerics;

/// <summary>Selects exact admission or explicitly accepted IEEE round-to-nearest, ties-to-even.</summary>
public enum DecimalRounding : byte
{
    /// <summary>Reject a result that differs from exact rational arithmetic.</summary>
    Exact = 0,
    /// <summary>Accept the BCL round-to-nearest, ties-to-even result and report inexactness.</summary>
    ToEven = 1
}

/// <summary>A decimal result with cleared failure storage; its default value is invalid.</summary>
public readonly struct DecimalResult<T> where T : struct
{
    private readonly T _value;
    /// <summary>The operation status.</summary>
    public ArithmeticError Error { get; }
    /// <summary>Whether a value is available.</summary>
    public bool IsSuccess => Error == ArithmeticError.None;
    /// <summary>Whether an accepted value differs from the exact result; false on failure.</summary>
    public bool IsInexact { get; }
    /// <summary>The successful value; throws when no value is available.</summary>
    public T Value => IsSuccess ? _value : throw new InvalidOperationException("Decimal result is not successful.");
    private DecimalResult(T value, ArithmeticError error, bool inexact) { _value = value; Error = error; IsInexact = inexact; }
    /// <summary>Returns success and writes the value, or clears the output on failure.</summary>
    public bool TryGetValue(out T value) { value = _value; return IsSuccess; }
    internal static DecimalResult<T> Ok(T value, bool inexact) => new(value, ArithmeticError.None, inexact);
    internal static DecimalResult<T> Fail(ArithmeticError error) => new(default, error, false);
}

/// <summary>Finite BCL Decimal32/64/128 arithmetic with exact rational checks. These optional APIs may allocate BigInteger temporaries.</summary>
public static class SafeDecimal
{
    /// <summary>Adds finite supported decimals under the selected precision contract.</summary>
    public static DecimalResult<T> TryAdd<T>(T left, T right, DecimalRounding rounding = DecimalRounding.Exact) where T : struct, IFloatingPointIeee754<T> => Binary(left, right, rounding, 0);
    /// <summary>Subtracts finite supported decimals under the selected precision contract.</summary>
    public static DecimalResult<T> TrySubtract<T>(T left, T right, DecimalRounding rounding = DecimalRounding.Exact) where T : struct, IFloatingPointIeee754<T> => Binary(left, right, rounding, 1);
    /// <summary>Multiplies finite supported decimals under the selected precision contract.</summary>
    public static DecimalResult<T> TryMultiply<T>(T left, T right, DecimalRounding rounding = DecimalRounding.Exact) where T : struct, IFloatingPointIeee754<T> => Binary(left, right, rounding, 2);
    /// <summary>Divides finite supported decimals under the selected precision contract.</summary>
    public static DecimalResult<T> TryDivide<T>(T left, T right, DecimalRounding rounding = DecimalRounding.Exact) where T : struct, IFloatingPointIeee754<T> => Binary(left, right, rounding, 3);
    /// <summary>Negates a finite supported decimal; signed zero follows the BCL.</summary>
    public static DecimalResult<T> TryNegate<T>(T value, DecimalRounding rounding = DecimalRounding.Exact) where T : struct, IFloatingPointIeee754<T>
    {
        if (!Valid<T>(rounding)) return DecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        if (!T.IsFinite(value)) return DecimalResult<T>.Fail(ArithmeticError.NonFinite);
        return DecimalResult<T>.Ok(-value, false);
    }
    /// <summary>Converts between the three BCL decimal formats, rejecting discarded precision by default.</summary>
    public static DecimalResult<TDestination> TryConvert<TSource, TDestination>(TSource value, DecimalRounding rounding = DecimalRounding.Exact)
        where TSource : struct, IFloatingPointIeee754<TSource> where TDestination : struct, IFloatingPointIeee754<TDestination>
    {
        if (!Valid<TSource>(rounding) || !Valid<TDestination>(rounding)) return DecimalResult<TDestination>.Fail(ArithmeticError.OutOfRange);
        if (!TSource.IsFinite(value)) return DecimalResult<TDestination>.Fail(ArithmeticError.NonFinite);
        // IEEE decimal parsing rounds once directly to the destination format, without a binary intermediary.
        var result = TDestination.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return Finish(result, Rational.From(value), rounding);
    }
    private static bool Valid<T>(DecimalRounding rounding) => (rounding is DecimalRounding.Exact or DecimalRounding.ToEven) && (typeof(T) == typeof(Decimal32) || typeof(T) == typeof(Decimal64) || typeof(T) == typeof(Decimal128));
    private static DecimalResult<T> Binary<T>(T a, T b, DecimalRounding rounding, int operation) where T : struct, IFloatingPointIeee754<T>
    {
        if (!Valid<T>(rounding)) return DecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        if (!T.IsFinite(a) || !T.IsFinite(b)) return DecimalResult<T>.Fail(ArithmeticError.NonFinite);
        if (operation == 3 && T.IsZero(b)) return DecimalResult<T>.Fail(ArithmeticError.DivideByZero);
        var x = Rational.From(a); var y = Rational.From(b);
        var exact = operation switch {
            0 => new Rational(x.N * y.D + y.N * x.D, x.D * y.D),
            1 => new Rational(x.N * y.D - y.N * x.D, x.D * y.D),
            2 => new Rational(x.N * y.N, x.D * y.D),
            _ => new Rational(x.N * y.D, x.D * y.N)
        };
        var result = operation switch { 0 => a + b, 1 => a - b, 2 => a * b, _ => a / b };
        return Finish(result, exact, rounding);
    }
    private static DecimalResult<T> Finish<T>(T value, Rational exact, DecimalRounding rounding) where T : struct, IFloatingPointIeee754<T>
    {
        if (!T.IsFinite(value)) return DecimalResult<T>.Fail(exact.N.Sign * exact.D.Sign < 0 ? ArithmeticError.Underflow : ArithmeticError.Overflow);
        var rounded = Rational.From(value);
        bool inexact = rounded.N * exact.D != exact.N * rounded.D;
        return inexact && rounding == DecimalRounding.Exact ? DecimalResult<T>.Fail(ArithmeticError.Inexact) : DecimalResult<T>.Ok(value, inexact);
    }
    // Exact finite rationals preserve all decimal digits and exponent; no reverse-arithmetic heuristic.
    private readonly record struct Rational(BigInteger N, BigInteger D)
    {
        public static Rational From<T>(T value) where T : IFloatingPointIeee754<T>
        {
            ReadOnlySpan<char> text = value.ToString("R", CultureInfo.InvariantCulture);
            int e = text.IndexOfAny('E', 'e');
            int exponent = e < 0 ? 0 : int.Parse(text[(e + 1)..], CultureInfo.InvariantCulture);
            var digits = e < 0 ? text : text[..e];
            bool negative = digits[0] == '-';
            if (digits[0] is '-' or '+') digits = digits[1..];
            BigInteger coefficient = BigInteger.Zero;
            int fractional = 0; bool point = false;
            foreach (char c in digits)
            {
                if (c == '.') { point = true; continue; }
                coefficient = coefficient * 10 + (c - '0');
                if (point) fractional++;
            }
            if (negative) coefficient = -coefficient;
            int scale = exponent - fractional;
            return scale >= 0 ? new(coefficient * BigInteger.Pow(10, scale), BigInteger.One) : new(coefficient, BigInteger.Pow(10, -scale));
        }
    }
}
