// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using SharpSafeNumerics;

namespace SharpSafeNumerics.Unions;

/// <summary>Represents the successful case of an arithmetic outcome.</summary>
/// <typeparam name="T">The numeric value type.</typeparam>
/// <param name="Value">The successfully computed value.</param>
public readonly record struct NumericSuccess<T>(T Value) where T : struct;

/// <summary>Represents the failed case of an arithmetic outcome.</summary>
/// <param name="Error">The reason arithmetic did not produce a value.</param>
public readonly record struct NumericFailure(ArithmeticError Error);

/// <summary>
/// C# 15 generated union. IMPORTANT: struct cases are boxed by the generated implementation.
/// Use NumericResult&lt;T&gt; or Try* in low-allocation paths.
/// </summary>
/// <typeparam name="T">The numeric value type carried by the success case.</typeparam>
public union ArithmeticOutcome<T>(NumericSuccess<T>, NumericFailure) where T : struct;

/// <summary>Creates union outcomes from the allocation-free core result type.</summary>
public static class ArithmeticOutcome
{
    /// <summary>Converts a core result into its corresponding success or failure union case.</summary>
    /// <typeparam name="T">The numeric value type.</typeparam>
    /// <param name="result">The core result to adapt.</param>
    /// <returns>A success case containing the value, or a failure case containing the error.</returns>
    public static ArithmeticOutcome<T> FromResult<T>(NumericResult<T> result) where T : struct
        => result.TryGetValue(out var value)
            ? new NumericSuccess<T>(value)
            : new NumericFailure(result.Error);
}
