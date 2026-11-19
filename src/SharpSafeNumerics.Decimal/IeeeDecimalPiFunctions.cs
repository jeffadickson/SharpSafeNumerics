// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;

namespace SharpSafeNumerics;

public static partial class IeeeDecimal<T> where T : struct, IFloatingPointIeee754<T>
{
    /// <summary>Computes sin(pi*value), reducing the decimal argument before multiplying by pi.</summary>
    public static IeeeDecimalResult<T> SinPi(T value, IeeeDecimalContext context = default) => PiFunction(value, 0, context);
    /// <summary>Computes cos(pi*value), with exact positive zeros at half-integers.</summary>
    public static IeeeDecimalResult<T> CosPi(T value, IeeeDecimalContext context = default) => PiFunction(value, 1, context);
    /// <summary>Computes tan(pi*value), raising DivisionByZero at half-integer poles.</summary>
    public static IeeeDecimalResult<T> TanPi(T value, IeeeDecimalContext context = default) => PiFunction(value, 2, context);
    /// <summary>Computes asin(value)/pi without an intermediate destination rounding.</summary>
    public static IeeeDecimalResult<T> AsinPi(T value, IeeeDecimalContext context = default) => PiFunction(value, 3, context);
    /// <summary>Computes acos(value)/pi without an intermediate destination rounding.</summary>
    public static IeeeDecimalResult<T> AcosPi(T value, IeeeDecimalContext context = default) => PiFunction(value, 4, context);
    /// <summary>Computes atan(value)/pi without an intermediate destination rounding.</summary>
    public static IeeeDecimalResult<T> AtanPi(T value, IeeeDecimalContext context = default) => PiFunction(value, 5, context);

    private static IeeeDecimalResult<T> PiFunction(T value, int operation, IeeeDecimalContext context)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(value);
        if (NaN(a)) return Propagate(context, a);
        if (a.Kind == DecimalKind.Infinity)
            return operation == 5 ? Special(new(a.Negative, 5, -1), context) : Invalid(context);
        if (operation is 3 or 4 && T.Abs(value) > T.One) return Invalid(context);
        if (a.Coefficient.IsZero)
            return operation is 1 ? Special(new(false, 1, 0), context) : operation == 4 ? Special(new(false, 5, -1), context) : Special(new(a.Negative, 0, 0), context);
        if (operation < 3)
        {
            BigInteger numerator = a.Coefficient * 2, denominator = 1;
            if (a.Exponent >= 0) numerator *= Pow(a.Exponent); else denominator = Pow(-a.Exponent);
            var twice = BigInteger.DivRem(numerator, denominator, out var remainder);
            if (remainder.IsZero)
            {
                bool oddHalf = !twice.IsEven, oddInteger = !(twice / 2).IsEven;
                if (operation == 0) return oddHalf ? Special(new(a.Negative ^ (twice % 4 == 3), 1, 0), context) : Special(new(a.Negative, 0, 0), context);
                if (operation == 1) return oddHalf ? Special(new(false, 0, 0), context) : Special(new(oddInteger, 1, 0), context);
                return oddHalf ? Special(new(a.Negative ^ (twice % 4 == 3), 0, 0, DecimalKind.Infinity), context, IeeeDecimalFlags.DivisionByZero) : Special(new(a.Negative ^ oddInteger, 0, 0), context);
            }
            if (operation == 2 && 2 * remainder == denominator)
                return Special(new(a.Negative ^ !twice.IsEven, 1, 0), context);
            // Reduce modulo two exactly before any irrational multiplication.
            // This also keeps large decimal arguments inexpensive and bounded.
            // A nonnegative quantum makes the operand integral, which already
            // returned through the exact lattice branch. Here its quantum is negative.
            numerator = a.Coefficient; denominator = Pow(-a.Exponent);
            numerator %= 2 * denominator;
            var reduced = new DecimalParts(a.Negative, numerator, a.Exponent);
            return Certified(reduced, context, (m, x) => { var pair = m.SinCos(m.Multiply(x, m.Pi())); return operation == 0 ? pair.Sin : operation == 1 ? pair.Cos : m.Divide(pair.Sin, pair.Cos); });
        }
        if (T.Abs(value) == T.One)
        {
            if (operation == 3) return Special(new(a.Negative, 5, -1), context);
            if (operation == 4) return Special(new(false, a.Negative ? 1 : 0, 0), context);
            return Special(new(a.Negative, 25, -2), context);
        }
        return Certified(a, context, (m, x) => m.Divide(operation == 3 ? AsinInterval(m, x) : operation == 4 ? m.Subtract(m.Divide(m.Pi(), 2), AsinInterval(m, x)) : m.Atan(x), m.Pi()));
    }

    /// <summary>Computes the signed angle atan2(y,x) in radians, including signed-zero quadrants.</summary>
    public static IeeeDecimalResult<T> Atan2(T y, T x, IeeeDecimalContext context = default) => Angle(y, x, false, context);
    /// <summary>Computes atan2(y,x)/pi, preserving exact quarter- and half-turn values.</summary>
    public static IeeeDecimalResult<T> Atan2Pi(T y, T x, IeeeDecimalContext context = default) => Angle(y, x, true, context);

    private static IeeeDecimalResult<T> Angle(T y, T x, bool scaled, IeeeDecimalContext context)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(y); var b = IeeeDecimalCodec<T>.Decode(x);
        if (NaN(a) || NaN(b)) return Propagate(context, a, b);
        int quarters = -1;
        if (a.Kind == DecimalKind.Infinity) quarters = b.Kind == DecimalKind.Infinity ? b.Negative ? 3 : 1 : 2;
        else if (b.Kind == DecimalKind.Infinity) quarters = b.Negative ? 4 : 0;
        else if (a.Coefficient.IsZero) quarters = b.Negative ? 4 : 0;
        else if (b.Coefficient.IsZero) quarters = 2;
        else if (T.Abs(y) == T.Abs(x)) quarters = b.Negative ? 3 : 1;
        if (quarters >= 0)
        {
            if (quarters == 0) return Special(new(a.Negative, 0, 0), context);
            if (scaled) return Round(quarters, 4, 0, a.Negative, 0, context);
            return Certified(a, context, (m, _) => { var angle = m.Divide(m.Multiply(m.Pi(), quarters), 4); return a.Negative ? m.Negate(angle) : angle; });
        }
        return Certified(a, context, (m, numerator) =>
        {
            var angle = m.Atan(m.Divide(numerator, m.Decimal(b)));
            if (b.Negative) angle = a.Negative ? m.Subtract(angle, m.Pi()) : m.Add(angle, m.Pi());
            return scaled ? m.Divide(angle, m.Pi()) : angle;
        });
    }

    /// <summary>Raises to an integer power without first converting the exponent to the decimal format.</summary>
    public static IeeeDecimalResult<T> PowN(T value, int n, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        // An Int32 can exceed Decimal32's exact coefficient precision; parsing
        // its exponent as Decimal32 would silently change the requested power.
        var a = IeeeDecimalCodec<T>.Decode(value);
        if (n == 0) return a.Kind == DecimalKind.SignalingNaN ? Propagate(context, a) : Special(new(false, 1, 0), context);
        if (NaN(a)) return Propagate(context, a);
        bool negative = a.Negative && (n & 1) != 0;
        if (a.Kind == DecimalKind.Infinity) return Special(n < 0 ? new(negative, 0, 0) : new(negative, 0, 0, DecimalKind.Infinity), context);
        if (a.Coefficient.IsZero) return n < 0 ? Special(new(negative, 0, 0, DecimalKind.Infinity), context, IeeeDecimalFlags.DivisionByZero) : Round(0, 1, 0, negative, ClampPreferred((BigInteger)a.Exponent * n), context);
        if (T.Abs(value) == T.One) return Round(1, 1, 0, negative, ClampPreferred((BigInteger)a.Exponent * n), context);
        long count = Math.Abs((long)n);
        if (count <= 100000)
        {
            var power = BigInteger.Pow(a.Coefficient, (int)count); int exponent = checked(a.Exponent * (int)count);
            return n > 0 ? Round(power, 1, exponent, negative, exponent, context) : Round(1, power, -exponent, negative, -exponent, context);
        }
        return Certified(a with { Negative = false }, context, (m, x) => { var result = m.Exp(m.Multiply(m.Log(x), n)); return negative ? m.Negate(result) : result; }, negative);
    }
    /// <summary>Computes exp(power*log(value)); unlike Pow, a negative base is always invalid.</summary>
    public static IeeeDecimalResult<T> PowR(T value, T power, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(value); var b = IeeeDecimalCodec<T>.Decode(power);
        if (value < T.Zero) return Invalid(context);
        if (NaN(a) || NaN(b)) return Propagate(context, a, b);
        if (value < T.Zero || T.IsZero(value) && T.IsZero(power) || T.IsInfinity(value) && T.IsZero(power) || value == T.One && T.IsInfinity(power)) return Invalid(context);
        if (T.IsZero(value)) value = IeeeDecimalCodec<T>.Encode(a with { Negative = false });
        return Pow(value, power, context);
    }
    /// <summary>Computes (1+value) raised to an integer power without rounding the addition first.</summary>
    public static IeeeDecimalResult<T> Compound(T value, int n, IeeeDecimalContext context = default)
    {
        if (!Valid(context)) return IeeeDecimalResult<T>.Fail(ArithmeticError.OutOfRange);
        var a = IeeeDecimalCodec<T>.Decode(value);
        if (n == 0 && a.Kind == DecimalKind.QuietNaN) return Special(new(false, 1, 0), context);
        if (NaN(a)) return Propagate(context, a);
        if (value < -T.One) return Invalid(context);
        if (a.Kind != DecimalKind.Finite) return PowN(value, n, context);
        int e = Math.Min(a.Exponent, 0);
        var coefficient = (a.Negative ? -a.Coefficient : a.Coefficient) * Pow(a.Exponent - e) + Pow(-e);
        // The validated finite domain value>=-1 guarantees 1+value>=0 exactly.
        // Zero is handled below, so no negative-result path is reachable.
        var sum = new DecimalParts(false, coefficient, e);
        if (sum.Coefficient.IsZero || n == 0) return n == 0 ? Special(new(false, 1, 0), context) : n < 0 ? Special(new(false, 0, 0, DecimalKind.Infinity), context, IeeeDecimalFlags.DivisionByZero) : Round(0, 1, 0, false, ClampPreferred((BigInteger)e * n), context);
        long count = Math.Abs((long)n);
        if (sum.Coefficient == Pow(-e)) return Round(1, 1, 0, false, ClampPreferred((BigInteger)e * n), context);
        if (count <= 100000)
        {
            var power = BigInteger.Pow(sum.Coefficient, (int)count); int exponent = checked(e * (int)count);
            return n > 0 ? Round(power, 1, exponent, false, exponent, context) : Round(1, power, -exponent, false, -exponent, context);
        }
        return Certified(sum, context, (m, x) => m.Exp(m.Multiply(m.Log(x), n)));
    }
}
