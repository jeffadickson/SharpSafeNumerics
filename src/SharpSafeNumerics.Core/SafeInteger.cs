using System.Numerics;

namespace SharpSafeNumerics;

/// <summary>
/// Bounded integer arithmetic. The supported no-exception arithmetic domain is built-in fixed-width integral types.
/// Custom implementations of generic math interfaces require separate qualification.
/// </summary>
public static class SafeInteger
{
    public static ArithmeticError TryAdd<T>(T left, T right, out T value)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        value = T.Zero;
        if (right > T.Zero && left > T.MaxValue - right) return ArithmeticError.Overflow;
        if (T.IsNegative(right) && left < T.MinValue - right) return ArithmeticError.Underflow;
        value = left + right;
        return ArithmeticError.None;
    }

    public static ArithmeticError TrySubtract<T>(T left, T right, out T value)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        value = T.Zero;
        if (right > T.Zero && left < T.MinValue + right) return ArithmeticError.Underflow;
        if (T.IsNegative(right) && left > T.MaxValue + right) return ArithmeticError.Overflow;
        value = left - right;
        return ArithmeticError.None;
    }

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
    /// Conversion is non-throwing to callers for range errors, but its failure path catches
    /// OverflowException and may allocate. Strict no-allocation code must not use this method.
    /// </summary>
    public static ArithmeticError TryConvert<TFrom, TTo>(TFrom input, out TTo value)
        where TFrom : struct, IBinaryInteger<TFrom>
        where TTo : struct, IBinaryInteger<TTo>
    {
        try
        {
            value = TTo.CreateChecked(input);
            return ArithmeticError.None;
        }
        catch (OverflowException)
        {
            value = TTo.Zero;
            return ArithmeticError.OutOfRange;
        }
    }

    public static ArithmeticError TryInRange<T>(T input, T minimum, T maximum, out T value)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        value = T.Zero;
        if (minimum > maximum) return ArithmeticError.OutOfRange;
        if (input < minimum || input > maximum) return ArithmeticError.OutOfRange;
        value = input;
        return ArithmeticError.None;
    }

    public static NumericResult<T> Add<T>(T left, T right, ArithmeticPolicy policy = ArithmeticPolicy.Reject)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        var error = TryAdd(left, right, out T value);
        if (error == ArithmeticError.None) return NumericResult<T>.Ok(value);
        return Resolve(left, right, error, policy, static (a, b) => unchecked(a + b));
    }

    public static NumericResult<T> Subtract<T>(T left, T right, ArithmeticPolicy policy = ArithmeticPolicy.Reject)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        var error = TrySubtract(left, right, out T value);
        if (error == ArithmeticError.None) return NumericResult<T>.Ok(value);
        return Resolve(left, right, error, policy, static (a, b) => unchecked(a - b));
    }

    public static NumericResult<T> Multiply<T>(T left, T right, ArithmeticPolicy policy = ArithmeticPolicy.Reject)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        var error = TryMultiply(left, right, out T value);
        if (error == ArithmeticError.None) return NumericResult<T>.Ok(value);
        return Resolve(left, right, error, policy, static (a, b) => unchecked(a * b));
    }

    private static NumericResult<T> Resolve<T>(T left, T right, ArithmeticError error,
        ArithmeticPolicy policy, Func<T, T, T> wrap)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
        => policy switch
        {
            ArithmeticPolicy.Reject => NumericResult<T>.Fail(error),
            ArithmeticPolicy.Saturate => NumericResult<T>.Ok(error == ArithmeticError.Overflow ? T.MaxValue : T.MinValue),
            ArithmeticPolicy.Wrap => NumericResult<T>.Ok(wrap(left, right)),
            ArithmeticPolicy.Throw => throw new OverflowException(error.ToString()),
            _ => throw new ArgumentOutOfRangeException(nameof(policy))
        };
}
