// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using SharpSafeNumerics;

/// <summary>Compares packed status stores using the production contract and layout.</summary>
[MemoryDiagnoser]
public class Avx512Comparison
{
    private int[] left = [], right = [], values = [];
    private ArithmeticError[] errors = [];

    [Params(256, 257, 4099, 262147)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!Avx512F.IsSupported || !Vector512.IsHardwareAccelerated || !Vector256.IsHardwareAccelerated)
            throw new PlatformNotSupportedException("The comparison requires hardware-accelerated AVX-512 and Vector256.");

        left = new int[Length];
        right = new int[Length];
        values = new int[Length];
        errors = new ArithmeticError[Length];
        var random = new Random(731923);
        for (int i = 0; i < Length; i++)
        {
            left[i] = (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
            right[i] = (i % 4) switch
            {
                0 => int.MaxValue, 1 => int.MinValue,
                _ => (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1)
            };
        }

        Verify(AddScalar(), 0);
        Array.Fill(values, int.MinValue);
        Array.Fill(errors, (ArithmeticError)255);
        Verify(Add256(), 256);
        Array.Fill(values, int.MinValue);
        Array.Fill(errors, (ArithmeticError)255);
        Verify(Add512(), 512);
    }

    private void Verify(int width, int expectedWidth)
    {
        if (width != expectedWidth) throw new InvalidOperationException($"Expected SIMD{expectedWidth}, ran SIMD{width}.");
        for (int i = 0; i < Length; i++)
        {
            long exact = (long)left[i] + right[i];
            var status = exact > int.MaxValue ? ArithmeticError.Overflow :
                exact < int.MinValue ? ArithmeticError.Underflow : ArithmeticError.None;
            if (errors[i] != status || values[i] != (status == ArithmeticError.None ? (int)exact : 0))
                throw new InvalidOperationException($"Incorrect result or status at lane {i}.");
        }
    }

    [Benchmark(Baseline = true)]
    public int AddScalar() => AddWithCeiling(0);

    [Benchmark]
    public int Add256() => AddWithCeiling(256);

    [Benchmark]
    public int Add512() => AddWithCeiling(512);

    private int AddWithCeiling(int ceiling)
    {
        var error = SafeInteger.TryAddBatchCore(left, right, values, errors, ceiling, out int width);
        if (error != BatchArithmeticError.None)
            throw new InvalidOperationException($"Batch rejected: {error}.");
        return width;
    }
}
