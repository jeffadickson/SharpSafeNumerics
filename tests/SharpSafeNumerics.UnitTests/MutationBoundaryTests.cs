// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using SharpSafeNumerics;
using Xunit;

public sealed class MutationBoundaryTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(int.MaxValue, 0)]
    [InlineData(int.MinValue, 0)]
    public void Add_zero_is_identity(int value, int zero)
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TryAdd(value, zero, out var result));
        Assert.Equal(value, result);
    }

    [Theory]
    [InlineData(10, -3, 7)]
    [InlineData(int.MinValue + 1, -1, int.MinValue)]
    [InlineData(-10, 3, -7)]
    [InlineData(int.MaxValue - 1, 1, int.MaxValue)]
    public void Add_signed_boundary_neighbors(int left, int right, int expected)
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TryAdd(left, right, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 0, 10)]
    [InlineData(-10, 0, -10)]
    [InlineData(0, 1, -1)]
    [InlineData(0, -1, 1)]
    [InlineData(int.MinValue + 1, 1, int.MinValue)]
    [InlineData(int.MaxValue - 1, -1, int.MaxValue)]
    public void Subtract_boundary_neighbors(int left, int right, int expected)
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TrySubtract(left, right, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(int.MinValue, -1, ArithmeticError.Overflow)]
    [InlineData(int.MaxValue, -1, ArithmeticError.None)]
    [InlineData(int.MinValue, 1, ArithmeticError.None)]
    [InlineData(int.MaxValue, 1, ArithmeticError.None)]
    [InlineData(-1, int.MinValue, ArithmeticError.Overflow)]
    [InlineData(-1, int.MaxValue, ArithmeticError.None)]
    [InlineData(2, int.MaxValue, ArithmeticError.Overflow)]
    [InlineData(-2, int.MaxValue, ArithmeticError.Underflow)]
    [InlineData(2, int.MinValue, ArithmeticError.Underflow)]
    [InlineData(-2, int.MinValue, ArithmeticError.Overflow)]
    public void Multiply_sign_matrix(int left, int right, ArithmeticError expected)
    {
        var error = SafeInteger.TryMultiply(left, right, out var result);
        Assert.Equal(expected, error);
        if (error == ArithmeticError.None)
            Assert.Equal((long)left * right, result);
        else
            Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(0, int.MaxValue)]
    [InlineData(0, int.MinValue)]
    [InlineData(int.MaxValue, 0)]
    [InlineData(int.MinValue, 0)]
    public void Multiply_by_zero_is_zero(int left, int right)
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TryMultiply(left, right, out var result));
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(12, 3, 4)]
    [InlineData(-12, 3, -4)]
    [InlineData(12, -3, -4)]
    [InlineData(-12, -3, 4)]
    [InlineData(int.MinValue, 1, int.MinValue)]
    public void Divide_valid_cases(int left, int right, int expected)
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TryDivide(left, right, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Divide_only_special_signed_pair_overflows()
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TryDivide(int.MaxValue, -1, out var result));
        Assert.Equal(-int.MaxValue, result);
    }

    [Theory]
    [InlineData(1, -1)]
    [InlineData(-1, 1)]
    [InlineData(1234, -1234)]
    [InlineData(-1234, 1234)]
    public void Negate_regular_signed_values(int input, int expected)
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TryNegate(input, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Negate_unsigned_zero_is_valid()
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TryNegate(0u, out var result));
        Assert.Equal(0u, result);
    }

    [Theory]
    [InlineData(0, 0, 0, ArithmeticError.None)]
    [InlineData(0, 0, 1, ArithmeticError.None)]
    [InlineData(1, 0, 1, ArithmeticError.None)]
    [InlineData(-1, 0, 1, ArithmeticError.OutOfRange)]
    [InlineData(2, 0, 1, ArithmeticError.OutOfRange)]
    public void Range_boundary_semantics(int input, int minimum, int maximum, ArithmeticError expected)
    {
        var error = SafeInteger.TryInRange(input, minimum, maximum, out var result);
        Assert.Equal(expected, error);
        Assert.Equal(error == ArithmeticError.None ? input : 0, result);
    }

    [Fact]
    public void Invalid_range_is_rejected_even_when_input_matches()
    {
        Assert.Equal(ArithmeticError.OutOfRange, SafeInteger.TryInRange(5, 6, 5, out var value));
        Assert.Equal(0, value);
    }

    [Theory]
    [InlineData(int.MaxValue, 1, int.MaxValue)]
    [InlineData(int.MinValue, -1, int.MinValue)]
    public void Saturating_add_uses_correct_endpoint(int left, int right, int expected)
    {
        var result = SafeInteger.Add(left, right, ArithmeticPolicy.Saturate);
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Saturating_subtract_uses_low_endpoint()
    {
        var result = SafeInteger.Subtract(int.MinValue, 1, ArithmeticPolicy.Saturate);
        Assert.Equal(int.MinValue, result.Value);
    }

    [Fact]
    public void Saturating_subtract_uses_high_endpoint()
    {
        var result = SafeInteger.Subtract(int.MaxValue, -1, ArithmeticPolicy.Saturate);
        Assert.Equal(int.MaxValue, result.Value);
    }

    [Fact]
    public void Saturating_multiply_selects_endpoint_by_failure()
    {
        Assert.Equal(int.MaxValue, SafeInteger.Multiply(int.MaxValue, 2, ArithmeticPolicy.Saturate).Value);
        Assert.Equal(int.MinValue, SafeInteger.Multiply(int.MinValue, 2, ArithmeticPolicy.Saturate).Value);
    }

    [Fact]
    public void Throw_policy_throws_only_for_failure()
    {
        Assert.Equal(3, SafeInteger.Add(1, 2, ArithmeticPolicy.Throw).Value);
        Assert.Throws<OverflowException>(() => SafeInteger.Add(int.MaxValue, 1, ArithmeticPolicy.Throw));
    }

    [Fact]
    public void Numeric_result_failure_value_accessor_throws()
    {
        var result = NumericResult<int>.Fail(ArithmeticError.Overflow);
        Assert.False(result.IsSuccess);
        Assert.Equal(ArithmeticError.Overflow, result.Error);
        Assert.False(result.TryGetValue(out var value));
        Assert.Equal(0, value);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Numeric_result_success_round_trips()
    {
        var result = NumericResult<int>.Ok(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(ArithmeticError.None, result.Error);
        Assert.True(result.TryGetValue(out var value));
        Assert.Equal(42, value);
        Assert.Equal(42, result.Value);
    }

    [Theory]
    [InlineData(ArithmeticError.None)]
    [InlineData(ArithmeticError.Uninitialized)]
    public void Numeric_result_rejects_non_failure_codes(ArithmeticError error)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NumericResult<int>.Fail(error));
    }
}

public sealed class BoundedMutationTests
{
    [Fact]
    public void Default_bounded_value_is_explicitly_invalid()
    {
        var value = default(Bounded<int, SmallRange>);
        Assert.False(value.IsValid);
        Assert.False(value.TryGetValue(out var raw));
        Assert.Equal(0, raw);
        Assert.Throws<InvalidOperationException>(() => value.Value);
        Assert.Equal(ArithmeticError.Uninitialized, value.TryAdd(1, out _));
        Assert.Equal(ArithmeticError.Uninitialized, value.TrySubtract(1, out _));
    }

    [Theory]
    [InlineData(-2, ArithmeticError.OutOfRange)]
    [InlineData(-1, ArithmeticError.None)]
    [InlineData(0, ArithmeticError.None)]
    [InlineData(2, ArithmeticError.None)]
    [InlineData(3, ArithmeticError.OutOfRange)]
    public void Create_enforces_domain(int input, ArithmeticError expected)
    {
        var error = Bounded<int, SmallRange>.TryCreate(input, out var value);
        Assert.Equal(expected, error);
        Assert.Equal(error == ArithmeticError.None, value.IsValid);
        if (value.IsValid) Assert.Equal(input, value.Value);
    }

    [Fact]
    public void Bounded_add_hits_domain_not_primitive_overflow()
    {
        Assert.Equal(ArithmeticError.None, Bounded<int, SmallRange>.TryCreate(2, out var value));
        Assert.Equal(ArithmeticError.OutOfRange, value.TryAdd(1, out var failed));
        Assert.False(failed.IsValid);
    }

    [Fact]
    public void Bounded_subtract_hits_domain_not_primitive_underflow()
    {
        Assert.Equal(ArithmeticError.None, Bounded<int, SmallRange>.TryCreate(-1, out var value));
        Assert.Equal(ArithmeticError.OutOfRange, value.TrySubtract(1, out var failed));
        Assert.False(failed.IsValid);
    }

    [Fact]
    public void Bounded_operations_preserve_valid_values()
    {
        Assert.Equal(ArithmeticError.None, Bounded<int, SmallRange>.TryCreate(0, out var value));
        Assert.Equal(ArithmeticError.None, value.TryAdd(2, out var high));
        Assert.Equal(2, high.Value);
        Assert.Equal(ArithmeticError.None, high.TrySubtract(3, out var low));
        Assert.Equal(-1, low.Value);
    }

    [Fact]
    public void Bounded_propagates_primitive_overflow_before_domain_check()
    {
        Assert.Equal(ArithmeticError.None, Bounded<int, FullIntRange>.TryCreate(int.MaxValue, out var value));
        Assert.Equal(ArithmeticError.Overflow, value.TryAdd(1, out _));
        Assert.Equal(ArithmeticError.None, Bounded<int, FullIntRange>.TryCreate(int.MinValue, out value));
        Assert.Equal(ArithmeticError.Underflow, value.TrySubtract(1, out _));
    }

    private readonly struct SmallRange : IStaticIntegerRange<int>
    {
        public static int Minimum => -1;
        public static int Maximum => 2;
    }

    private readonly struct FullIntRange : IStaticIntegerRange<int>
    {
        public static int Minimum => int.MinValue;
        public static int Maximum => int.MaxValue;
    }
}

public sealed class AnnotationTests
{
    [Fact]
    public void Bounded_integer_attribute_preserves_declared_contract()
    {
        var attribute = new BoundedIntegerAttribute(typeof(int), "-5", "9")
        {
            OverflowPolicy = ArithmeticPolicy.Saturate
        };
        Assert.Equal(typeof(int), attribute.StorageType);
        Assert.Equal("-5", attribute.Minimum);
        Assert.Equal("9", attribute.Maximum);
        Assert.Equal(ArithmeticPolicy.Saturate, attribute.OverflowPolicy);
    }
}
