// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using SharpSafeNumerics;
using Xunit;

namespace SharpSafeNumerics.UnitTests;

/// <summary>Independent exact-integer admission oracle and allocation regressions (SN-003/004/005).</summary>
public sealed class ConversionTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public void All_destination_types_match_oracle(int source) => RunSource(source, allocations: false);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public void Allocation_paths(int source) => RunSource(source, allocations: true);

    private static void RunSource(int source, bool allocations)
    {
        switch (source)
        {
            case 0: Destinations<byte>(allocations); break;
            case 1: Destinations<sbyte>(allocations); break;
            case 2: Destinations<short>(allocations); break;
            case 3: Destinations<ushort>(allocations); break;
            case 4: Destinations<int>(allocations); break;
            case 5: Destinations<uint>(allocations); break;
            case 6: Destinations<long>(allocations); break;
            case 7: Destinations<ulong>(allocations); break;
            case 8: Destinations<Int128>(allocations); break;
            case 9: Destinations<UInt128>(allocations); break;
            case 10: Destinations<nint>(allocations); break;
            case 11: Destinations<nuint>(allocations); break;
            default: throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private static void Destinations<TFrom>(bool allocations)
        where TFrom : struct, IBinaryInteger<TFrom>, IMinMaxValue<TFrom>
    {
        Pair<TFrom, byte>(allocations); Pair<TFrom, sbyte>(allocations);
        Pair<TFrom, short>(allocations); Pair<TFrom, ushort>(allocations);
        Pair<TFrom, int>(allocations); Pair<TFrom, uint>(allocations);
        Pair<TFrom, long>(allocations); Pair<TFrom, ulong>(allocations);
        Pair<TFrom, Int128>(allocations); Pair<TFrom, UInt128>(allocations);
        Pair<TFrom, nint>(allocations); Pair<TFrom, nuint>(allocations);
    }

    private static void Pair<TFrom, TTo>(bool allocations)
        where TFrom : struct, IBinaryInteger<TFrom>, IMinMaxValue<TFrom>
        where TTo : struct, IBinaryInteger<TTo>, IMinMaxValue<TTo>
    {
        BigInteger lo = BigInteger.CreateChecked(TFrom.MinValue);
        BigInteger hi = BigInteger.CreateChecked(TFrom.MaxValue);
        BigInteger targetLo = BigInteger.CreateChecked(TTo.MinValue);
        BigInteger targetHi = BigInteger.CreateChecked(TTo.MaxValue);
        if (allocations)
        {
            Measure<TFrom, TTo>(TFrom.Zero, ArithmeticError.None);
            if (lo < targetLo) Measure<TFrom, TTo>(TFrom.MinValue, ArithmeticError.OutOfRange);
            if (hi > targetHi) Measure<TFrom, TTo>(TFrom.MaxValue, ArithmeticError.OutOfRange);
            return; // Widening pairs have no representable source that can fail.
        }

        var samples = new HashSet<BigInteger> { lo, hi, 0 };
        foreach (BigInteger boundary in new[] { lo, hi, targetLo, targetHi, BigInteger.Zero })
            for (int offset = -2; offset <= 2; offset++) samples.Add(boundary + offset);
        // Every power-of-two transition, not just the source and destination endpoints.
        for (int bit = 0; bit <= 128; bit++)
            for (int offset = -1; offset <= 1; offset++)
            {
                samples.Add((BigInteger.One << bit) + offset);
                samples.Add(-(BigInteger.One << bit) + offset);
            }
        if (hi - lo == 255)
            for (BigInteger n = lo; n <= hi; n++) samples.Add(n);

        foreach (BigInteger sample in samples)
            if (sample >= lo && sample <= hi) Check<TFrom, TTo>(sample, targetLo, targetHi);

        // Uniform full-width byte inputs; replay seed and count are part of the contract.
        var random = new Random(731921);
        byte[] bytes = new byte[TFrom.Zero.GetByteCount()];
        for (int i = 0; i < 2_048; i++)
        {
            random.NextBytes(bytes);
            BigInteger sample = new(bytes, isUnsigned: true, isBigEndian: false);
            if (lo < 0 && sample > hi) sample -= hi - lo + 1;
            Check<TFrom, TTo>(sample, targetLo, targetHi);
        }
    }

    private static void Check<TFrom, TTo>(BigInteger exact, BigInteger lo, BigInteger hi)
        where TFrom : struct, IBinaryInteger<TFrom>
        where TTo : struct, IBinaryInteger<TTo>
    {
        var expected = exact >= lo && exact <= hi ? ArithmeticError.None : ArithmeticError.OutOfRange;
        var actual = SafeInteger.TryConvert<TFrom, TTo>(TFrom.CreateChecked(exact), out TTo value);
        Assert.Equal(expected, actual);
        Assert.Equal(expected == ArithmeticError.None ? exact : BigInteger.Zero, BigInteger.CreateChecked(value));
    }

    private static void Measure<TFrom, TTo>(TFrom input, ArithmeticError expected)
        where TFrom : struct, IBinaryInteger<TFrom>
        where TTo : struct, IBinaryInteger<TTo>
    {
        for (int i = 0; i < 1_000; i++) _ = SafeInteger.TryConvert<TFrom, TTo>(input, out _);
        long checksum = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100_000; i++)
        {
            var error = SafeInteger.TryConvert<TFrom, TTo>(input, out TTo value);
            checksum += (int)error + (value == TTo.Zero ? 0 : 1);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(100_000L * (int)expected, checksum);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Arbitrary_width_generic_callers_retain_exact_conversion()
    {
        BigInteger large = BigInteger.One << 512;
        Assert.Equal(ArithmeticError.None, SafeInteger.TryConvert<BigInteger, BigInteger>(large, out var copy));
        Assert.Equal(large, copy);
        Assert.Equal(ArithmeticError.OutOfRange, SafeInteger.TryConvert<BigInteger, UInt128>(large, out var rejected));
        Assert.Equal(UInt128.Zero, rejected);
        Assert.Equal(ArithmeticError.None, SafeInteger.TryConvert<Int128, BigInteger>(Int128.MinValue, out copy));
        Assert.Equal(BigInteger.CreateChecked(Int128.MinValue), copy);
    }

    [Fact]
    public void Rejected_conversion_does_not_allocate()
    {
        for (int i = 0; i < 1_000; i++)
            _ = SafeInteger.TryConvert<int, byte>(256, out _);

        int checksum = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100_000; i++)
        {
            var error = SafeInteger.TryConvert<int, byte>(256, out byte value);
            checksum += (int)error + value;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(100_000 * (int)ArithmeticError.OutOfRange, checksum);
        Assert.Equal(0, allocated);
    }
}
