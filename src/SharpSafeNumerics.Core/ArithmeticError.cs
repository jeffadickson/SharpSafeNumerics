// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

namespace SharpSafeNumerics;

/// <summary>Uninitialized is zero so default(NumericResult&lt;T&gt;) never looks successful.</summary>
public enum ArithmeticError : byte
{
    /// <summary>Default/uninitialized status; never indicates successful arithmetic.</summary>
    Uninitialized = 0,
    /// <summary>Operation completed successfully.</summary>
    None = 1,
    /// <summary>Result exceeded the representable upper bound.</summary>
    Overflow = 2,
    /// <summary>Result fell below the representable lower bound.</summary>
    Underflow = 3,
    /// <summary>Divisor is zero.</summary>
    DivideByZero = 4,
    /// <summary>Value or conversion is outside the permitted range.</summary>
    OutOfRange = 5,
    /// <summary>The exact result is not representable under the requested precision contract.</summary>
    Inexact = 6,
    /// <summary>An operand is NaN or infinite where a finite value is required.</summary>
    NonFinite = 7
}

/// <summary>Explicit overflow behavior for policy-oriented arithmetic.</summary>
public enum ArithmeticPolicy : byte
{
    /// <summary>Return a typed failure on arithmetic overflow.</summary>
    Reject = 0,
    /// <summary>Clamp overflowing results to the appropriate endpoint.</summary>
    Saturate = 1,
    /// <summary>Explicitly use modular unchecked arithmetic.</summary>
    Wrap = 2,
    /// <summary>Throw an OverflowException on arithmetic overflow.</summary>
    Throw = 3
}
