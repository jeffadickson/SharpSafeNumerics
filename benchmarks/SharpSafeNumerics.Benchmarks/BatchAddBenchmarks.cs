// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using BenchmarkDotNet.Attributes;
using SharpSafeNumerics;
using SharpSafeNumerics.Experiments;
using BenchmarkDotNet.Configs;

/// <summary>Equivalent buffer result/status work with mixed rejection and tail handling.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BatchAddBenchmarks
{
    private int[] left = [], right = [], values = [], statuses = [];
    private ArithmeticError[] laneErrors = [];

    /// <summary>Non-multiple sizes exercise the scalar tail as part of the measured operation.</summary>
    [Params(257, 4099)]
    public int Length { get; set; }

    /// <summary>Prepare deterministic operands and output buffers outside measurement.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(731923);
        left = new int[Length]; right = new int[Length]; values = new int[Length]; statuses = new int[Length];
        laneErrors = new ArithmeticError[Length];
        for (int i = 0; i < Length; i++)
        {
            left[i] = (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
            right[i] = i % 4 == 0 ? int.MaxValue : i % 4 == 1 ? int.MinValue : (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
        }
    }

    /// <summary>Production scalar entry point with identical output/status buffers.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Experiment")]
    public int Scalar()
    {
        for (int i = 0; i < Length; i++) statuses[i] = (int)SafeInteger.TryAdd(left[i], right[i], out values[i]);
        return values[^1] + statuses[^1];
    }

    /// <summary>Checked lane experiment with hardware dispatch and scalar tail.</summary>
    [Benchmark]
    [BenchmarkCategory("Experiment")]
    public int VectorExperiment()
    {
        VectorAddExperiment.Add(left, right, values, statuses);
        return values[^1] + statuses[^1];
    }

    /// <summary>Equivalent production-layout baseline: int values and byte-backed error codes.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Production")]
    public int ProductionScalar()
    {
        for (int i = 0; i < Length; i++) laneErrors[i] = SafeInteger.TryAdd(left[i], right[i], out values[i]);
        return values[^1] + (int)laneErrors[^1];
    }

    /// <summary>Public detected SIMD API, including validation and identical result/error layout.</summary>
    [Benchmark]
    [BenchmarkCategory("Production")]
    public int ProductionDetected()
    {
        _ = SafeInteger.TryAddBatch(left, right, values, laneErrors);
        return values[^1] + (int)laneErrors[^1];
    }
}
