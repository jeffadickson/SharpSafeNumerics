// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using SharpSafeNumerics;
using Xunit;

public sealed class SafeIntegerTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 2, 3)]
    [InlineData(-2, 1, -1)]
    [InlineData(int.MaxValue - 1, 1, int.MaxValue)]
    [InlineData(int.MinValue + 1, -1, int.MinValue)]
    public void Add_valid_values(int a, int b, int expected)
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TryAdd(a, b, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact] public void Add_detects_overflow() =>
        Assert.Equal(ArithmeticError.Overflow, SafeInteger.TryAdd(int.MaxValue, 1, out _));

    [Fact] public void Add_detects_underflow() =>
        Assert.Equal(ArithmeticError.Underflow, SafeInteger.TryAdd(int.MinValue, -1, out _));

    [Fact] public void Subtract_detects_underflow() =>
        Assert.Equal(ArithmeticError.Underflow, SafeInteger.TrySubtract(int.MinValue, 1, out _));

    [Fact] public void Subtract_detects_overflow() =>
        Assert.Equal(ArithmeticError.Overflow, SafeInteger.TrySubtract(int.MaxValue, -1, out _));

    [Fact] public void Multiply_detects_positive_overflow() =>
        Assert.Equal(ArithmeticError.Overflow, SafeInteger.TryMultiply(int.MaxValue, 2, out _));

    [Fact] public void Multiply_detects_negative_overflow() =>
        Assert.Equal(ArithmeticError.Overflow, SafeInteger.TryMultiply(int.MinValue, -1, out _));

    [Fact] public void Multiply_detects_underflow() =>
        Assert.Equal(ArithmeticError.Underflow, SafeInteger.TryMultiply(int.MinValue, 2, out _));

    [Fact] public void Divide_rejects_zero() =>
        Assert.Equal(ArithmeticError.DivideByZero, SafeInteger.TryDivide(1, 0, out _));

    [Fact] public void Divide_detects_min_by_minus_one() =>
        Assert.Equal(ArithmeticError.Overflow, SafeInteger.TryDivide(int.MinValue, -1, out _));

    [Fact] public void Negate_detects_signed_min() =>
        Assert.Equal(ArithmeticError.Overflow, SafeInteger.TryNegate(int.MinValue, out _));

    [Fact] public void Negate_rejects_unsigned_nonzero() =>
        Assert.Equal(ArithmeticError.Underflow, SafeInteger.TryNegate(1u, out _));

    [Fact] public void Saturate_clamps_high() =>
        Assert.Equal(byte.MaxValue, SafeInteger.Add((byte)250, (byte)10, ArithmeticPolicy.Saturate).Value);

    [Fact] public void Wrap_is_explicit() =>
        Assert.Equal((byte)4, SafeInteger.Add((byte)250, (byte)10, ArithmeticPolicy.Wrap).Value);

    [Fact] public void Reject_returns_failure() =>
        Assert.Equal(ArithmeticError.Overflow, SafeInteger.Add((byte)250, (byte)10).Error);

    [Fact] public void Default_result_is_not_success() =>
        Assert.False(default(NumericResult<int>).IsSuccess);

    [Fact] public void Range_is_inclusive()
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TryInRange(0, 0, 10, out _));
        Assert.Equal(ArithmeticError.None, SafeInteger.TryInRange(10, 0, 10, out _));
        Assert.Equal(ArithmeticError.OutOfRange, SafeInteger.TryInRange(11, 0, 10, out _));
    }

    [Fact] public void Conversion_rejects_narrowing_overflow() =>
        Assert.Equal(ArithmeticError.OutOfRange, SafeInteger.TryConvert<int, byte>(256, out _));

    [Fact] public void UInt128_overflow_is_detected() =>
        Assert.Equal(ArithmeticError.Overflow, SafeInteger.TryAdd(UInt128.MaxValue, UInt128.One, out _));

    [Fact] public void Output_is_cleared_on_failure()
    {
        _ = SafeInteger.TryAdd(int.MaxValue, 1, out var value);
        Assert.Equal(0, value);
    }
}
