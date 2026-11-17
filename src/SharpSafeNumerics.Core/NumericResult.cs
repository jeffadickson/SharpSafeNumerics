namespace SharpSafeNumerics;

/// <summary>Non-boxing numeric result; default is invalid, not success.</summary>
public readonly struct NumericResult<T> where T : struct
{
    private readonly T _value;
    public ArithmeticError Error { get; }
    public bool IsSuccess => Error == ArithmeticError.None;
    public T Value => IsSuccess ? _value : throw new InvalidOperationException("Numeric result is not successful.");
    private NumericResult(T value, ArithmeticError error) { _value = value; Error = error; }
    public bool TryGetValue(out T value)
    {
        value = IsSuccess ? _value : default;
        return IsSuccess;
    }
    public static NumericResult<T> Ok(T value) => new(value, ArithmeticError.None);
    public static NumericResult<T> Fail(ArithmeticError error)
    {
        if (error is ArithmeticError.None or ArithmeticError.Uninitialized)
            throw new ArgumentOutOfRangeException(nameof(error));
        return new(default, error);
    }
}
