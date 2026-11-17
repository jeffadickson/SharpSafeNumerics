using SharpSafeNumerics;
using Xunit;

namespace SharpSafeNumerics.UnitTests;

/// <summary>Undefined policy values are programmer errors even for exact arithmetic (SN-007-VALIDATION).</summary>
public sealed class PolicyValidationTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Every_undefined_policy_is_rejected_before_successful_arithmetic(int operation)
    {
        for (int raw = 4; raw <= byte.MaxValue; raw++)
        {
            var policy = (ArithmeticPolicy)raw;
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Invoke(operation, 1, 1, policy));
            Assert.Equal("policy", exception.ParamName);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Every_undefined_policy_is_rejected_for_overflowing_arithmetic(int operation)
    {
        for (int raw = 4; raw <= byte.MaxValue; raw++)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                Invoke(operation, operation == 1 ? int.MinValue : int.MaxValue, operation == 2 ? 2 : 1, (ArithmeticPolicy)raw));
            Assert.Equal("policy", exception.ParamName);
        }
    }

    private static NumericResult<int> Invoke(int operation, int left, int right, ArithmeticPolicy policy) => operation switch
    {
        0 => SafeInteger.Add(left, right, policy),
        1 => SafeInteger.Subtract(left, right, policy),
        2 => SafeInteger.Multiply(left, right, policy),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
}
