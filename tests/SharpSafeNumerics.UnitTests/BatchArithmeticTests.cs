// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Xunit;

namespace SharpSafeNumerics.UnitTests;

/// <summary>Per-lane independent oracles and transactional buffer contracts for detected SIMD addition.</summary>
public sealed class BatchArithmeticTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(15)] [InlineData(16)]
    [InlineData(17)] [InlineData(255)] [InlineData(256)] [InlineData(257)] [InlineData(264)] [InlineData(4099)]
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
        foreach (int width in new[] { 0, 128, 256, 512 })
        {
            Array.Fill(values, 0x13579BDF); Array.Fill(errors, (ArithmeticError)255);
            Assert.Equal(BatchArithmeticError.None, SafeInteger.TryAddBatchCore(left, right, values, errors, width, out int used));
            Assert.Equal(ExpectedWidth(count, width), used);
            Verify(left, right, values, errors);
            Assert.All(values[count..], x => Assert.Equal(0x13579BDF, x));
            Assert.All(errors[count..], x => Assert.Equal((ArithmeticError)255, x));
        }
        Array.Fill(values, 0x13579BDF); Array.Fill(errors, (ArithmeticError)255);
        var detailed = SafeInteger.AddBatch(left, right, values, errors);
        Assert.Equal(BatchArithmeticError.None, detailed.Error); Assert.True(detailed.IsSuccess);
        Assert.Equal(ExpectedWidth(count, 512), detailed.VectorWidthUsed);
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
        var detailed = SafeInteger.AddBatch(oversized, oversized, [], []);
        Assert.Equal(BatchArithmeticError.InputTooLarge, detailed.Error);
        Assert.Equal(0, detailed.VectorWidthUsed); Assert.False(detailed.IsSuccess); Assert.Equal(91, sentinel);
        // The largest representable byte length is admissible to the size guard;
        // empty destinations must reject next, still before any memory access.
        ReadOnlySpan<int> boundary = MemoryMarshal.CreateReadOnlySpan(ref sentinel, int.MaxValue / sizeof(int));
        Assert.Equal(BatchArithmeticError.DestinationTooSmall, SafeInteger.AddBatch(boundary, boundary, [], []).Error);
        Assert.Equal(91, sentinel);
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
    public void Each_output_input_alias_is_rejected_independently()
    {
        int[] left = [1, 2, 3, 4, 5, 6, 7, 8, 9], right = [11, 12, 13, 14, 15, 16, 17, 18, 19];
        int[] values = Enumerable.Repeat(91, 8).ToArray();
        var errors = Enumerable.Repeat((ArithmeticError)255, 8).ToArray();
        Assert.Equal(BatchArithmeticError.OverlappingBuffers, SafeInteger.TryAddBatch(left.AsSpan(0, 8), right.AsSpan(0, 8), left.AsSpan(1, 8), errors));
        Assert.Equal(BatchArithmeticError.OverlappingBuffers, SafeInteger.TryAddBatch(left.AsSpan(0, 8), right.AsSpan(0, 8), right.AsSpan(1, 8), errors));
        Assert.Equal(BatchArithmeticError.OverlappingBuffers, SafeInteger.TryAddBatch(left.AsSpan(0, 8), right.AsSpan(0, 8), values, MemoryMarshal.Cast<int, ArithmeticError>(left.AsSpan()).Slice(1, 8)));
        Assert.Equal(BatchArithmeticError.OverlappingBuffers, SafeInteger.TryAddBatch(left.AsSpan(0, 8), right.AsSpan(0, 8), values, MemoryMarshal.Cast<int, ArithmeticError>(right.AsSpan()).Slice(1, 8)));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, left);
        Assert.Equal(new[] { 11, 12, 13, 14, 15, 16, 17, 18, 19 }, right);
        Assert.All(values, x => Assert.Equal(91, x)); Assert.All(errors, x => Assert.Equal((ArithmeticError)255, x));
    }

    [Fact]
    public void Warmed_success_failure_and_contract_rejection_do_not_allocate()
    {
        int[] left = [int.MaxValue, int.MinValue, 1, 0, -1, 2, -2, 3, 4];
        int[] right = [1, -1, 1, 0, 1, 2, -2, 3, 4];
        int[] values = new int[9]; var errors = new ArithmeticError[9];
        foreach (int width in new[] { 0, 128, 256, 512 })
        {
            for (int i = 0; i < 1000; i++) _ = SafeInteger.TryAddBatchCore(left, right, values, errors, width, out _);
            long before = GC.GetAllocatedBytesForCurrentThread(); int checksum = 0;
            for (int i = 0; i < 100_000; i++)
            {
                checksum += (int)SafeInteger.TryAddBatchCore(left, right, values, errors, width, out _) + values[8] + (int)errors[0];
                checksum += (int)SafeInteger.TryAddBatch(left, right.AsSpan(0, 8), values, errors);
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(100_000 * ((int)BatchArithmeticError.None + 8 + (int)ArithmeticError.Overflow + (int)BatchArithmeticError.LengthMismatch), checksum);
            Assert.Equal(0, bytes);
        }
        Assert.Equal(ExpectedWidth(256, 512), SafeInteger.BatchAddVectorWidth);
    }

    [Fact]
    public void Returned_width_cannot_alias_data_and_default_result_is_invalid()
    {
        int[] left = [41], values = [99]; var errors = new ArithmeticError[1];
        var success = SafeInteger.AddBatch(left, [1], values, errors);
        Assert.True(success.IsSuccess); Assert.Equal(41, left[0]); Assert.Equal(42, values[0]);
        values[0] = 99;
        var failure = SafeInteger.AddBatch(left, [], values, errors);
        Assert.Equal(BatchArithmeticError.LengthMismatch, failure.Error);
        Assert.Equal(99, values[0]); Assert.Equal(0, failure.VectorWidthUsed); Assert.False(failure.IsSuccess);
        Assert.Equal(BatchArithmeticError.Uninitialized, default(BatchArithmeticResult).Error);
        Assert.False(default(BatchArithmeticResult).IsSuccess);
    }

    [Fact]
    public void Required_hardware_width_actually_executes()
    {
        // CI runs this in fresh processes with AVX-512 enabled and disabled.
        string? required = Environment.GetEnvironmentVariable("SSN_REQUIRED_BATCH_WIDTH");
        if (required is null) return;
        int[] left = new int[271], right = new int[left.Length], values = new int[left.Length];
        var errors = new ArithmeticError[left.Length];
        for (int i = 0; i < left.Length; i++)
        {
            left[i] = (i % 3) switch { 0 => int.MaxValue, 1 => int.MinValue, _ => i };
            right[i] = i % 3 == 1 ? -1 : 1;
        }
        var result = SafeInteger.AddBatch(left, right, values, errors);
        Assert.True(result.IsSuccess);
        Assert.Equal(required, result.VectorWidthUsed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Verify(left, right, values, errors);
    }

    private static int ExpectedWidth(int count, int ceiling) =>
        count >= 256 && ceiling >= 512 && Avx512F.IsSupported && Vector512.IsHardwareAccelerated ? 512 :
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
