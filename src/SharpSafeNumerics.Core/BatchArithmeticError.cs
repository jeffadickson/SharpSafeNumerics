// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

namespace SharpSafeNumerics;

/// <summary>Buffer-contract outcome for batch arithmetic; lane arithmetic errors are reported separately.</summary>
public enum BatchArithmeticError : byte
{
    /// <summary>The batch completed; inspect each lane's arithmetic status.</summary>
    None = 0,
    /// <summary>Input spans have different lengths.</summary>
    LengthMismatch = 1,
    /// <summary>A destination cannot hold every input lane.</summary>
    DestinationTooSmall = 2,
    /// <summary>A written destination overlaps an input or the other written destination.</summary>
    OverlappingBuffers = 3,
    /// <summary>The input byte length cannot be represented by the span overlap validator.</summary>
    InputTooLarge = 4,
    /// <summary>No batch operation has supplied an outcome.</summary>
    Uninitialized = 255
}

/// <summary>Non-boxing batch outcome and observed execution width; its default is uninitialized.</summary>
public readonly struct BatchArithmeticResult
{
    private readonly bool _initialized;
    private readonly BatchArithmeticError _error;
    /// <summary>Buffer-contract outcome, separate from each lane's arithmetic error.</summary>
    public BatchArithmeticError Error => _initialized ? _error : BatchArithmeticError.Uninitialized;
    /// <summary>Widest vector loop actually executed: 256, 128, or zero. Failures report zero.</summary>
    public int VectorWidthUsed { get; }
    /// <summary>Whether the batch completed; individual lanes may still report arithmetic failure.</summary>
    public bool IsSuccess => _initialized && _error == BatchArithmeticError.None;

    internal BatchArithmeticResult(BatchArithmeticError error, int vectorWidthUsed)
    {
        _initialized = true;
        _error = error;
        VectorWidthUsed = vectorWidthUsed;
    }
}
