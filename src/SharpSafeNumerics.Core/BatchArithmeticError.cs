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
    InputTooLarge = 4
}
