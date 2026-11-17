using SharpSafeNumerics;
using Xunit;

namespace SharpSafeNumerics.UnitTests;

/// <summary>Behavioral regressions selected from the first conversion-pass mutation report.</summary>
public sealed class MutationSurvivorTests
{
    [Theory]
    [InlineData(2, 2, 4)]
    [InlineData(2, -1, -2)]
    [InlineData(-int.MaxValue, -1, int.MaxValue)]
    public void Multiplication_admits_exact_representable_products(int left, int right, int expected)
    {
        Assert.Equal(ArithmeticError.None, SafeInteger.TryMultiply(left, right, out int actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Wrapping_subtraction_and_multiplication_return_modular_values()
    {
        Assert.Equal(byte.MaxValue, SafeInteger.Subtract((byte)0, (byte)1, ArithmeticPolicy.Wrap).Value);
        Assert.Equal((byte)144, SafeInteger.Multiply((byte)20, (byte)20, ArithmeticPolicy.Wrap).Value);
    }

    [Fact]
    public void Bounded_nonthrowing_read_preserves_valid_value_and_clears_invalid_output()
    {
        Assert.Equal(ArithmeticError.None, Bounded<int, SmallRange>.TryCreate(7, out var valid));
        Assert.True(valid.TryGetValue(out int value));
        Assert.Equal(7, value);
        Assert.False(default(Bounded<int, SmallRange>).TryGetValue(out value));
        Assert.Equal(0, value);
    }

    private readonly struct SmallRange : IStaticIntegerRange<int>
    {
        public static int Minimum => 1;
        public static int Maximum => 9;
    }
}
