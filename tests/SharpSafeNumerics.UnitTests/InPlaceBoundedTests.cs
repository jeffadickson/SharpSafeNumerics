using Xunit;

namespace SharpSafeNumerics.UnitTests;

/// <summary>Receiver/out aliases must observe the original bounded value before publishing output.</summary>
public sealed class InPlaceBoundedTests
{
    [Fact]
    public void In_place_add_and_subtract_preserve_the_original_receiver()
    {
        Assert.Equal(ArithmeticError.None, Bounded<int, Range>.TryCreate(41, out var value));
        Assert.Equal(ArithmeticError.None, value.TryAdd(1, out value)); Assert.Equal(42, value.Value);
        Assert.Equal(ArithmeticError.None, value.TrySubtract(1, out value)); Assert.Equal(41, value.Value);
        Assert.Equal(ArithmeticError.OutOfRange, value.TryAdd(100, out value)); Assert.False(value.IsValid);
        Assert.Equal(ArithmeticError.Uninitialized, value.TrySubtract(1, out value)); Assert.False(value.IsValid);
        Assert.Equal(ArithmeticError.None, Bounded<int, Range>.TryCreate(0, out value));
        Assert.Equal(ArithmeticError.OutOfRange, value.TrySubtract(1, out value)); Assert.False(value.IsValid);
    }

    private readonly struct Range : IStaticIntegerRange<int>
    {
        public static int Minimum => 0;
        public static int Maximum => 63;
    }
}
