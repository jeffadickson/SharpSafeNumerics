// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
namespace SharpSafeNumerics;
/// <summary>IEEE decimal rounding direction.</summary>
public enum IeeeDecimalRounding
{
    /// <summary>Nearest; ties have even last digit.</summary>
    ToNearestEven,
    /// <summary>Nearest; ties increase magnitude.</summary>
    ToNearestAway,
    /// <summary>Truncate toward zero.</summary>
    TowardZero,
    /// <summary>Round toward negative infinity.</summary>
    TowardNegative,
    /// <summary>Round toward positive infinity.</summary>
    TowardPositive
}
/// <summary>IEEE exception status, independent of API validation errors.</summary>
[Flags]
public enum IeeeDecimalFlags
{
    /// <summary>No exception.</summary>
    None = 0,
    /// <summary>An invalid operation occurred.</summary>
    Invalid = 1,
    /// <summary>A finite nonzero value was divided by zero.</summary>
    DivisionByZero = 2,
    /// <summary>The rounded exponent exceeded the format range.</summary>
    Overflow = 4,
    /// <summary>The exact result is tiny before rounding and the rounded result is inexact.</summary>
    Underflow = 8,
    /// <summary>Rounding discarded nonzero digits.</summary>
    Inexact = 16
}
/// <summary>Explicit per-call rounding and optional traps; default means nearest even without traps.</summary>
public readonly record struct IeeeDecimalContext(IeeeDecimalRounding Rounding = default, IeeeDecimalFlags Traps = default);
/// <summary>An IEEE value and status; default storage is invalid and failures clear the value.</summary>
public readonly struct IeeeDecimalResult<T> where T : struct
{
    private readonly T _value;
    /// <summary>API or trap failure.</summary>
    public ArithmeticError Error { get; }
    /// <summary>IEEE exception flags raised by this operation.</summary>
    public IeeeDecimalFlags Flags { get; }
    /// <summary>Whether a value is available, including IEEE NaNs and infinities.</summary>
    public bool IsSuccess => Error == ArithmeticError.None;
    /// <summary>Returns the value or throws if no value is available.</summary>
    public T Value => IsSuccess ? _value : throw new InvalidOperationException("IEEE decimal result is unsuccessful.");
    private IeeeDecimalResult(T value, ArithmeticError error, IeeeDecimalFlags flags) { _value = value; Error = error; Flags = flags; }
    /// <summary>Returns availability and writes cleared storage on failure.</summary>
    public bool TryGetValue(out T value) { value = _value; return IsSuccess; }
    internal static IeeeDecimalResult<T> Ok(T value, IeeeDecimalFlags flags = default) => new(value, ArithmeticError.None, flags);
    internal static IeeeDecimalResult<T> Fail(ArithmeticError error, IeeeDecimalFlags flags = default) => new(default, error, flags);
}


