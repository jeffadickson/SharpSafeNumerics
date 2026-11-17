using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Xunit;

namespace SharpSafeNumerics.UnitTests;

/// <summary>Per-lane independent oracles and transactional buffer contracts for detected SIMD addition.</summary>
public sealed class BatchArithmeticTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(15)] [InlineData(16)]
    [InlineData(17)] [InlineData(257)] [InlineData(4099)]
    public void All_dispatch_widths_match_exact_lane_oracle_and_preserve_suffixes(int count)
    {
        int[] left = new int[count], right = new int[count], values = new int[count + 3];
        var errors = new ArithmeticError[count + 3];
        var random = new Random(731924);
        int[] extremes = [int.MinValue, int.MaxValue, 0, 1, -1];
        for (int i = 0; i < count; i++)
        {
            left[i] = i < 25 ? extremes[i % 5] : (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
            right[i] = i < 25 ? extremes[(i / 5) % 5] : (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
        }
        foreach (int width in new[] { 0, 128, 256 })
        {
            Array.Fill(values, 0x13579BDF); Array.Fill(errors, (ArithmeticError)255);
            Assert.Equal(BatchArithmeticError.None, SafeInteger.TryAddBatchCore(left, right, values, errors, width, out int used));
            Assert.Equal(ExpectedWidth(count, width), used);
            Verify(left, right, values, errors);
            Assert.All(values[count..], x => Assert.Equal(0x13579BDF, x));
            Assert.All(errors[count..], x => Assert.Equal((ArithmeticError)255, x));
        }
        Array.Fill(values, 0x13579BDF); Array.Fill(errors, (ArithmeticError)255);
        Assert.Equal(BatchArithmeticError.None, SafeInteger.TryAddBatch(left, right, values, errors, out int detected));
        Assert.Equal(ExpectedWidth(count, 256), detected);
        Verify(left, right, values, errors);
    }

    [Fact]
    public void Contract_failures_preserve_both_destinations()
    {
        int[] values = [91, 92]; ArithmeticError[] errors = [(ArithmeticError)251, (ArithmeticError)252];
        Assert.Equal(BatchArithmeticError.LengthMismatch, SafeInteger.TryAddBatch([1], [1, 2], values, errors));
        Assert.Equal(BatchArithmeticError.DestinationTooSmall, SafeInteger.TryAddBatch([1, 2], [3, 4], values.AsSpan(0, 1), errors));
        Assert.Equal(BatchArithmeticError.DestinationTooSmall, SafeInteger.TryAddBatch([1, 2], [3, 4], values, errors.AsSpan(0, 1)));
        Assert.Equal(new[] { 91, 92 }, values); Assert.Equal(new[] { (ArithmeticError)251, (ArithmeticError)252 }, errors);
    }

    [Fact]
    public void Oversized_logical_span_rejects_without_dereferencing_memory()
    {
        int sentinel = 91;
        // This test-only logical span must never be accessed: its declared length
        // deliberately exceeds its backing storage to verify the early size guard.
        ReadOnlySpan<int> oversized = MemoryMarshal.CreateReadOnlySpan(ref sentinel, int.MaxValue / sizeof(int) + 1);
        Assert.Equal(BatchArithmeticError.InputTooLarge, SafeInteger.TryAddBatch(oversized, oversized, [], [], out int width));
        Assert.Equal(0, width); Assert.Equal(91, sentinel);
    }

    [Fact]
    public void Output_aliases_reject_before_writes_but_readonly_aliasing_is_valid()
    {
        int[] input = [int.MaxValue, 1, -1, int.MinValue, 5, 6, 7, 8, 9];
        int[] original = (int[])input.Clone(); var errors = new ArithmeticError[8];
        Assert.Equal(BatchArithmeticError.OverlappingBuffers, SafeInteger.TryAddBatch(input.AsSpan(0, 8), input.AsSpan(1, 8), input.AsSpan(1, 8), errors));
        Assert.Equal(original, input);
        int[] output = new int[8];
        Span<ArithmeticError> aliasedErrors = MemoryMarshal.Cast<int, ArithmeticError>(input.AsSpan()).Slice(0, 8);
        Assert.Equal(BatchArithmeticError.OverlappingBuffers, SafeInteger.TryAddBatch(input.AsSpan(0, 8), input.AsSpan(1, 8), output, aliasedErrors));
        Assert.Equal(original, input); Assert.All(output, x => Assert.Equal(0, x));
        aliasedErrors = MemoryMarshal.Cast<int, ArithmeticError>(output.AsSpan()).Slice(0, 8);
        Assert.Equal(BatchArithmeticError.OverlappingBuffers, SafeInteger.TryAddBatch(input.AsSpan(0, 8), input.AsSpan(1, 8), output, aliasedErrors));
        Assert.All(output, x => Assert.Equal(0, x));
        Assert.Equal(BatchArithmeticError.None, SafeInteger.TryAddBatch(input.AsSpan(0, 8), input.AsSpan(0, 8), output, errors));
        Verify(input.AsSpan(0, 8), input.AsSpan(0, 8), output, errors);
    }

    [Fact]
    public void Warmed_success_failure_and_contract_rejection_do_not_allocate()
    {
        int[] left = [int.MaxValue, int.MinValue, 1, 0, -1, 2, -2, 3, 4];
        int[] right = [1, -1, 1, 0, 1, 2, -2, 3, 4];
        int[] values = new int[9]; var errors = new ArithmeticError[9];
        foreach (int width in new[] { 0, 128, 256 })
        {
            for (int i = 0; i < 1000; i++) _ = SafeInteger.TryAddBatchCore(left, right, values, errors, width, out _);
            long before = GC.GetAllocatedBytesForCurrentThread(); int checksum = 0;
            for (int i = 0; i < 100_000; i++)
            {
                checksum += (int)SafeInteger.TryAddBatchCore(left, right, values, errors, width, out _) + values[8] + (int)errors[0];
                checksum += (int)SafeInteger.TryAddBatch(left, right.AsSpan(0, 8), values, errors);
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(100_000 * (8 + (int)ArithmeticError.Overflow + (int)BatchArithmeticError.LengthMismatch), checksum);
            Assert.Equal(0, bytes);
        }
        Assert.Equal(ExpectedWidth(8, 256), SafeInteger.BatchAddVectorWidth);
    }

    private static int ExpectedWidth(int count, int ceiling) =>
        count >= 8 && ceiling >= 256 && Vector256.IsHardwareAccelerated ? 256 :
        count >= 4 && ceiling >= 128 && Vector128.IsHardwareAccelerated ? 128 : 0;

    private static void Verify(ReadOnlySpan<int> left, ReadOnlySpan<int> right, ReadOnlySpan<int> values, ReadOnlySpan<ArithmeticError> errors)
    {
        for (int i = 0; i < left.Length; i++)
        {
            BigInteger exact = (BigInteger)left[i] + right[i];
            var expected = exact < int.MinValue ? ArithmeticError.Underflow : exact > int.MaxValue ? ArithmeticError.Overflow : ArithmeticError.None;
            Assert.Equal(expected, errors[i]);
            Assert.Equal(expected == ArithmeticError.None ? (int)exact : 0, values[i]);
        }
    }
}
