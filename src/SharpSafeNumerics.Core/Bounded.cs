using System.Numerics;

namespace SharpSafeNumerics;

/// <summary>Supply static inclusive limits for a bounded integer domain.</summary>
/// <typeparam name="T">A built-in binary integer type.</typeparam>
public interface IStaticIntegerRange<T>
    where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
{
    /// <summary>The minimum permitted value.</summary>
    static abstract T Minimum { get; }
    /// <summary>The maximum permitted value.</summary>
    static abstract T Maximum { get; }
}

/// <summary>
/// Value-constrained integer with explicit validity. default(Bounded&lt;T,TRange&gt;) is invalid.
/// Does not allocate in its built-in integer Try* paths.
/// </summary>
public readonly struct Bounded<T, TRange>
    where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    where TRange : IStaticIntegerRange<T>
{
    private readonly T _value;
    private readonly bool _valid;

    private Bounded(T value) { _value = value; _valid = true; }

    /// <summary>Whether a valid bounded value was constructed.</summary>
    public bool IsValid => _valid;

    /// <summary>Read the value; use TryGetValue in strict paths.</summary>
    public T Value => _valid ? _value : throw new InvalidOperationException("Uninitialized bounded number.");

    /// <summary>Read a bounded value without throwing.</summary>
    public bool TryGetValue(out T value)
    {
        value = _valid ? _value : T.Zero;
        return _valid;
    }

    /// <summary>Create a value if within the inclusive domain bounds.</summary>
    public static ArithmeticError TryCreate(T input, out Bounded<T, TRange> value)
    {
        value = default;
        var error = SafeInteger.TryInRange(input, TRange.Minimum, TRange.Maximum, out _);
        if (error != ArithmeticError.None) return error;
        value = new Bounded<T, TRange>(input);
        return ArithmeticError.None;
    }

    /// <summary>Add within both the primitive limits and the declared domain limits.</summary>
    public ArithmeticError TryAdd(T delta, out Bounded<T, TRange> value)
    {
        // The caller may use its receiver as the out destination. Snapshot before
        // clearing that destination so the original value and validity survive.
        T current = _value;
        bool valid = _valid;
        value = default;
        if (!valid) return ArithmeticError.Uninitialized;
        var error = SafeInteger.TryAdd(current, delta, out T sum);
        if (error != ArithmeticError.None) return error;
        return TryCreate(sum, out value);
    }

    /// <summary>Subtract within both the primitive and declared domain limits.</summary>
    public ArithmeticError TrySubtract(T delta, out Bounded<T, TRange> value)
    {
        T current = _value;
        bool valid = _valid;
        value = default;
        if (!valid) return ArithmeticError.Uninitialized;
        var error = SafeInteger.TrySubtract(current, delta, out T difference);
        if (error != ArithmeticError.None) return error;
        return TryCreate(difference, out value);
    }
}
