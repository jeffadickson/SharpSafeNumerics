// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;

namespace SharpSafeNumerics;

/// <summary>
/// Bounded integer arithmetic. The supported no-exception arithmetic domain is built-in fixed-width integral types.
/// Custom implementations of generic math interfaces require separate qualification.
/// </summary>
public static partial class SafeInteger
{
    /// <summary>Add with explicit overflow or underflow status and deterministic output.</summary>
    public static ArithmeticError TryAdd<T>(T left, T right, out T value)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        value = T.Zero;
        if (right > T.Zero && left > T.MaxValue - right) return ArithmeticError.Overflow;
        if (T.IsNegative(right) && left < T.MinValue - right) return ArithmeticError.Underflow;
        value = left + right;
        return ArithmeticError.None;
    }

    /// <summary>Subtract with explicit overflow or underflow status and deterministic output.</summary>
    public static ArithmeticError TrySubtract<T>(T left, T right, out T value)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        value = T.Zero;
        if (right > T.Zero && left < T.MinValue + right) return ArithmeticError.Underflow;
        if (T.IsNegative(right) && left > T.MaxValue + right) return ArithmeticError.Overflow;
        value = left - right;
        return ArithmeticError.None;
    }

    /// <summary>Multiply with preflight overflow checks, returning a status.</summary>
    public static ArithmeticError TryMultiply<T>(T left, T right, out T value)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        value = T.Zero;
        if (left == T.Zero || right == T.Zero) return ArithmeticError.None;
        if (left > T.Zero)
        {
            if (right > T.Zero && left > T.MaxValue / right) return ArithmeticError.Overflow;
            if (T.IsNegative(right) && right < T.MinValue / left) return ArithmeticError.Underflow;
        }
        else
        {
            if (right > T.Zero && left < T.MinValue / right) return ArithmeticError.Underflow;
            if (T.IsNegative(right) && left < T.MaxValue / right) return ArithmeticError.Overflow;
        }
        value = left * right;
        return ArithmeticError.None;
    }

    /// <summary>Divide, rejecting zero divisor and signed MinValue divided by minus one.</summary>
    public static ArithmeticError TryDivide<T>(T left, T right, out T value)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        value = T.Zero;
        if (right == T.Zero) return ArithmeticError.DivideByZero;
        if (left == T.MinValue && T.IsNegative(right) && right == -T.One)
            return ArithmeticError.Overflow;
        value = left / right;
        return ArithmeticError.None;
    }

    /// <summary>Negate without silent wraparound; reject unsigned nonzero values.</summary>
    public static ArithmeticError TryNegate<T>(T input, out T value)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        value = T.Zero;
        if (input == T.Zero) return ArithmeticError.None;
        if (T.MinValue == T.Zero) return ArithmeticError.Underflow; // unsigned
        if (input == T.MinValue) return ArithmeticError.Overflow;
        value = -input;
        return ArithmeticError.None;
    }

    /// <summary>
    /// Convert exactly, returning OutOfRange and zero if the value is not representable.
    /// Built-in integer conversions do not use exceptions for range rejection or allocate.
    /// Custom generic-math providers require separate qualification and may reject unsupported types.
    /// </summary>
    public static ArithmeticError TryConvert<TFrom, TTo>(TFrom input, out TTo value)
        where TFrom : struct, IBinaryInteger<TFrom>
        where TTo : struct, IBinaryInteger<TTo>
    {
        value = TTo.Zero;
        TTo candidate = TTo.CreateTruncating(input);
        // A modulo round trip alone accepts sign-changing conversions (e.g. -1 to uint).
        // Both sign and source value must survive before the candidate may escape.
        if (TFrom.IsNegative(input) != TTo.IsNegative(candidate) ||
            TFrom.CreateTruncating(candidate) != input)
        {
            return ArithmeticError.OutOfRange;
        }
        value = candidate;
        return ArithmeticError.None;
    }

    /// <summary>Admit a value into an inclusive numeric range.</summary>
    public static ArithmeticError TryInRange<T>(T input, T minimum, T maximum, out T value)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        value = T.Zero;
        if (minimum > maximum) return ArithmeticError.OutOfRange;
        if (input < minimum || input > maximum) return ArithmeticError.OutOfRange;
        value = input;
        return ArithmeticError.None;
    }

    /// <summary>Add using an explicitly selected overflow policy.</summary>
    public static NumericResult<T> Add<T>(T left, T right, ArithmeticPolicy policy = ArithmeticPolicy.Reject)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        ValidatePolicy(policy);
        var error = TryAdd(left, right, out T value);
        if (error == ArithmeticError.None) return NumericResult<T>.Ok(value);
        return Resolve(left, right, error, policy, static (a, b) => unchecked(a + b));
    }

    /// <summary>Subtract using an explicitly selected overflow policy.</summary>
    public static NumericResult<T> Subtract<T>(T left, T right, ArithmeticPolicy policy = ArithmeticPolicy.Reject)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        ValidatePolicy(policy);
        var error = TrySubtract(left, right, out T value);
        if (error == ArithmeticError.None) return NumericResult<T>.Ok(value);
        return Resolve(left, right, error, policy, static (a, b) => unchecked(a - b));
    }

    /// <summary>Multiply using an explicitly selected overflow policy.</summary>
    public static NumericResult<T> Multiply<T>(T left, T right, ArithmeticPolicy policy = ArithmeticPolicy.Reject)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        ValidatePolicy(policy);
        var error = TryMultiply(left, right, out T value);
        if (error == ArithmeticError.None) return NumericResult<T>.Ok(value);
        return Resolve(left, right, error, policy, static (a, b) => unchecked(a * b));
    }

    // Policies are byte-backed and contiguous from Reject through Throw. Validate
    // before the fast success return so caller contract errors are operand-independent.
    private static void ValidatePolicy(ArithmeticPolicy policy)
    {
        if ((byte)policy > (byte)ArithmeticPolicy.Throw)
            throw new ArgumentOutOfRangeException(nameof(policy));
    }

    private static NumericResult<T> Resolve<T>(T left, T right, ArithmeticError error,
        ArithmeticPolicy policy, Func<T, T, T> wrap)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
        => policy switch
        {
            ArithmeticPolicy.Reject => NumericResult<T>.Fail(error),
            ArithmeticPolicy.Saturate => NumericResult<T>.Ok(error == ArithmeticError.Overflow ? T.MaxValue : T.MinValue),
            ArithmeticPolicy.Wrap => NumericResult<T>.Ok(wrap(left, right)),
            // All public callers validated policy; the sole remaining case is Throw.
            _ => throw new OverflowException(error.ToString())
        };
}
