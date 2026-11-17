using SharpSafeNumerics;

namespace SharpSafeNumerics.Unions;

public readonly record struct NumericSuccess<T>(T Value) where T : struct;
public readonly record struct NumericFailure(ArithmeticError Error);

/// <summary>
/// C# 15 generated union. IMPORTANT: struct cases are boxed by the generated implementation.
/// Use NumericResult&lt;T&gt; or Try* in low-allocation paths.
/// </summary>
public union ArithmeticOutcome<T>(NumericSuccess<T>, NumericFailure) where T : struct;

public static class ArithmeticOutcome
{
    public static ArithmeticOutcome<T> FromResult<T>(NumericResult<T> result) where T : struct
        => result.TryGetValue(out var value)
            ? new NumericSuccess<T>(value)
            : new NumericFailure(result.Error);
}
