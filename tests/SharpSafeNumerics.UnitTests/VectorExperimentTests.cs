using System.Numerics;
using SharpSafeNumerics.Experiments;
using Xunit;

namespace SharpSafeNumerics.UnitTests;

/// <summary>Independent per-lane oracle, tails and precondition checks for the SIMD research prototype.</summary>
public sealed class VectorExperimentTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    [InlineData(15)] [InlineData(16)] [InlineData(17)] [InlineData(257)]
    public void Every_lane_matches_exact_arithmetic_in_both_dispatch_modes(int length)
    {
        var random = new Random(731923);
        int[] left = new int[length], right = new int[length], values = new int[length], statuses = new int[length];
        for (int i = 0; i < length; i++)
        {
            left[i] = (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
            right[i] = i % 4 == 0 ? int.MaxValue : i % 4 == 1 ? int.MinValue : (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
        }
        foreach (bool hardware in new[] { false, true })
        {
            // Fresh poison is required for each dispatch: stale scalar outputs must
            // not conceal missing vector stores or missing failure-value clearing.
            Array.Fill(values, 0x13579BDF);
            Array.Fill(statuses, -101);
            VectorAddExperiment.Add(left, right, values, statuses, hardware);
            for (int i = 0; i < length; i++)
            {
                BigInteger exact = (BigInteger)left[i] + right[i];
                var expected = exact > int.MaxValue ? ArithmeticError.Overflow : exact < int.MinValue ? ArithmeticError.Underflow : ArithmeticError.None;
                Assert.Equal((int)expected, statuses[i]);
                Assert.Equal(expected == ArithmeticError.None ? (int)exact : 0, values[i]);
            }
        }
    }

    [Fact]
    public void Readonly_inputs_may_alias_while_outputs_remain_disjoint()
    {
        int[] input = [1, int.MaxValue];
        int[] values = new int[2], statuses = new int[2];
        VectorAddExperiment.Add(input, input, values, statuses);
        Assert.Equal(new[] { 2, 0 }, values);
        Assert.Equal(new[] { (int)ArithmeticError.None, (int)ArithmeticError.Overflow }, statuses);
    }

    [Fact]
    public void Invalid_buffers_do_not_publish_partial_results()
    {
        int[] output = [91, 92];
        Assert.Throws<ArgumentException>(() => VectorAddExperiment.Add([1], [2], output, new int[2]));
        Assert.Equal(new[] { 91, 92 }, output);
        Assert.Throws<ArgumentException>(() => VectorAddExperiment.Add(output, [1, 2], output, new int[2]));
        Assert.Equal(new[] { 91, 92 }, output);
    }
}
