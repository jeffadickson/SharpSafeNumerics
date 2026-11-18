using SharpSafeNumerics;
using Xunit;

public class IeeeEnvironmentTests
{
    [Fact]
    public void ExplicitModesAndFlagsCanBeSavedRestoredAndComposed()
    {
        var initial = default(IeeeDecimalState);
        var changed = IeeeDecimalEnvironment.SetDecimalRoundingDirection(initial, IeeeDecimalRounding.TowardPositive).Value;
        changed = IeeeDecimalEnvironment.SetBinaryRoundingDirection(changed, IeeeDecimalRounding.TowardNegative).Value;
        var modes = IeeeDecimalEnvironment.SaveModes(changed).Value;
        changed = IeeeDecimalEnvironment.RaiseFlags(changed, IeeeDecimalFlags.Inexact | IeeeDecimalFlags.Underflow).Value;
        var saved = IeeeDecimalEnvironment.SaveFlags(changed).Value;
        Assert.True(IeeeDecimalEnvironment.TestFlags(changed, IeeeDecimalFlags.Inexact).Value);
        var cleared = IeeeDecimalEnvironment.LowerFlags(changed, IeeeDecimalFlags.Inexact).Value;
        Assert.Equal(IeeeDecimalFlags.Underflow, cleared.Flags);
        Assert.Equal(saved, IeeeDecimalEnvironment.RestoreFlags(cleared, saved).Value.Flags);
        var defaults = IeeeDecimalEnvironment.DefaultModes(changed).Value;
        Assert.Equal(changed.Flags, defaults.Flags);
        Assert.Equal(IeeeDecimalRounding.ToNearestEven, defaults.DecimalContext.Rounding);
        Assert.Equal(IeeeDecimalRounding.TowardNegative, IeeeDecimalEnvironment.RestoreModes(defaults, modes).Value.BinaryContext.Rounding);
        Assert.Equal(IeeeDecimalRounding.ToNearestEven, initial.DecimalContext.Rounding);
    }

    [Fact]
    public void InvalidStateDoesNotPublishPartialChanges()
    {
        Assert.False(IeeeDecimalEnvironment.RaiseFlags(default, (IeeeDecimalFlags)128).TryGetValue(out var cleared));
        Assert.Equal(default, cleared);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimalEnvironment.SetDecimalRoundingDirection(default, (IeeeDecimalRounding)255).Error);
    }
}
