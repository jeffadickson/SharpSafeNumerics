namespace SharpSafeNumerics;

/// <summary>Non-boxing numeric result; default is invalid, not success.</summary>
public readonly struct NumericResult<T> where T : struct
{
    private readonly T _value;
    /// <summary>The operation's status; Uninitialized for the default value.</summary>
    public ArithmeticError Error { get; }
    /// <summary>Whether a valid result is available.</summary>
    public bool IsSuccess => Error == ArithmeticError.None;
    /// <summary>The successful value; throws if accessed on failure.</summary>
    public T Value => IsSuccess ? _value : throw new InvalidOperationException("Numeric result is not successful.");
    private NumericResult(T value, ArithmeticError error) { _value = value; Error = error; }
    /// <summary>Read the value without throwing; false on failure.</summary>
    public bool TryGetValue(out T value)
    {
        value = IsSuccess ? _value : default;
        return IsSuccess;
    }
    /// <summary>Create a successful result.</summary>
    public static NumericResult<T> Ok(T value) => new(value, ArithmeticError.None);
    /// <summary>Create a failure result with an explicit non-success error.</summary>
    public static NumericResult<T> Fail(ArithmeticError error)
    {
        if (error is ArithmeticError.None or ArithmeticError.Uninitialized)
            throw new ArgumentOutOfRangeException(nameof(error));
        return new(default, error);
    }
}
