// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

namespace SharpSafeNumerics;

/// <summary>A signed Q32.32 value whose exact value is <see cref="RawValue"/> divided by 2^32. Default is zero.</summary>
public readonly struct Fixed64 : IEquatable<Fixed64>
{
    /// <summary>The signed scaled integer representation; no floating-point conversion is performed.</summary>
    public long RawValue { get; }
    private Fixed64(long rawValue) => RawValue = rawValue;
    /// <summary>Construct from the exact signed scaled representation. Every raw bit pattern is valid.</summary>
    public static Fixed64 FromRaw(long rawValue) => new(rawValue);
    /// <summary>Compare exact raw representations.</summary>
    public bool Equals(Fixed64 other) => RawValue == other.RawValue;
    /// <summary>Compare another object to this value.</summary>
    public override bool Equals(object? obj) => obj is Fixed64 other && Equals(other);
    /// <summary>Hash the exact scaled representation.</summary>
    public override int GetHashCode() => RawValue.GetHashCode();
}

/// <summary>Explicit policies for fractions discarded when quantizing to Q32.32.</summary>
public enum FixedRounding : byte
{
    /// <summary>Reject any nonzero discarded fraction.</summary>
    Exact = 0,
    /// <summary>Round to nearest, choosing the even raw integer on a tie.</summary>
    ToEven = 1,
    /// <summary>Discard the fraction toward zero.</summary>
    ToZero = 2,
    /// <summary>Round toward negative infinity.</summary>
    Floor = 3,
    /// <summary>Round toward positive infinity.</summary>
    Ceiling = 4,
    /// <summary>Round any nonzero discarded fraction away from zero.</summary>
    AwayFromZero = 5
}

/// <summary>Non-throwing, allocation-free fixed-point admission and arithmetic with typed failures.</summary>
/// <remarks>Quantization precedes range validation, so rounding may admit an endpoint. Exact mode rejects any discarded fraction with Inexact before testing range, even when the exact result also exceeds the range.</remarks>
public static class SafeFixed
{
    private const long Scale = 1L << 32;
    /// <summary>Admit an exact signed integer, rejecting directional range overflow.</summary>
    public static NumericResult<Fixed64> FromInt64(long value) => Finish((Int128)value * Scale);
    /// <summary>Quantize an exact signed ratio. Invalid rounding is rejected before checking the denominator.</summary>
    public static NumericResult<Fixed64> FromRatio(long numerator, long denominator, FixedRounding rounding = FixedRounding.Exact)
        => Quantize((Int128)numerator * Scale, denominator, rounding);
    /// <summary>Add without wrapping or saturation.</summary>
    public static NumericResult<Fixed64> Add(Fixed64 left, Fixed64 right) => Finish((Int128)left.RawValue + right.RawValue);
    /// <summary>Subtract without wrapping or saturation.</summary>
    public static NumericResult<Fixed64> Subtract(Fixed64 left, Fixed64 right) => Finish((Int128)left.RawValue - right.RawValue);
    /// <summary>Multiply with an exact wide intermediate before quantizing.</summary>
    public static NumericResult<Fixed64> Multiply(Fixed64 left, Fixed64 right, FixedRounding rounding = FixedRounding.Exact)
        => Quantize((Int128)left.RawValue * right.RawValue, Scale, rounding);
    /// <summary>Divide with an exact scaled wide numerator before quantizing.</summary>
    public static NumericResult<Fixed64> Divide(Fixed64 left, Fixed64 right, FixedRounding rounding = FixedRounding.Exact)
        => Quantize((Int128)left.RawValue * Scale, right.RawValue, rounding);
    /// <summary>Negate, rejecting the unrepresentable positive counterpart of the minimum value.</summary>
    public static NumericResult<Fixed64> Negate(Fixed64 value) => Finish(-(Int128)value.RawValue);

    private static NumericResult<Fixed64> Quantize(Int128 numerator, Int128 denominator, FixedRounding rounding)
    {
        if ((byte)rounding > (byte)FixedRounding.AwayFromZero) return NumericResult<Fixed64>.Fail(ArithmeticError.OutOfRange);
        if (denominator == 0) return NumericResult<Fixed64>.Fail(ArithmeticError.DivideByZero);
        // All callers produce at most 126 numerator magnitude bits and 64 denominator bits;
        // normalization, doubled remainder and the final rounding carry therefore fit Int128.
        if (denominator < 0) { numerator = -numerator; denominator = -denominator; }
        Int128 quotient = numerator / denominator;
        Int128 remainder = numerator % denominator;
        if (remainder != 0)
        {
            if (rounding == FixedRounding.Exact) return NumericResult<Fixed64>.Fail(ArithmeticError.Inexact);
            Int128 magnitude = remainder < 0 ? -remainder : remainder;
            bool increment = rounding switch
            {
                FixedRounding.AwayFromZero => true,
                FixedRounding.Floor => numerator < 0,
                FixedRounding.Ceiling => numerator > 0,
                FixedRounding.ToEven => magnitude * 2 > denominator || (magnitude * 2 == denominator && (quotient & 1) != 0),
                _ => false
            };
            if (increment) quotient += numerator < 0 ? -1 : 1;
        }
        return Finish(quotient);
    }

    private static NumericResult<Fixed64> Finish(Int128 raw)
    {
        if (raw > long.MaxValue) return NumericResult<Fixed64>.Fail(ArithmeticError.Overflow);
        if (raw < long.MinValue) return NumericResult<Fixed64>.Fail(ArithmeticError.Underflow);
        return NumericResult<Fixed64>.Ok(Fixed64.FromRaw((long)raw));
    }
}

