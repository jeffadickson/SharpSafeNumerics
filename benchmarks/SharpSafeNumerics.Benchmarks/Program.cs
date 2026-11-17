using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using SharpSafeNumerics;

BenchmarkSwitcher.FromAssembly(typeof(AddBenchmarks).Assembly).Run(args);

[MemoryDiagnoser]
public class AddBenchmarks
{
    private int a = 1_000_000;
    private int b = 2_000_000;

    [Benchmark(Baseline = true)]
    public bool ManualGuard()
    {
        if (b > 0 && a > int.MaxValue - b) return false;
        if (b < 0 && a < int.MinValue - b) return false;
        _ = a + b;
        return true;
    }

    [Benchmark]
    public ArithmeticError SharpSafeNumerics() => SafeInteger.TryAdd(a, b, out _);

    [Benchmark]
    public int CheckedBuiltIn() => checked(a + b);
}

/// <summary>Common Boolean admission contract on success and both range failures.</summary>
[MemoryDiagnoser]
public class ConversionBenchmarks
{
    /// <summary>Exact endpoint, above-range rejection, and negative rejection.</summary>
    [Params(255, 256, -1)]
    public int Input { get; set; }

    /// <summary>Equivalent explicit guard without exception handling.</summary>
    [Benchmark(Baseline = true)]
    public bool ManualGuard() => Input >= byte.MinValue && Input <= byte.MaxValue;

    /// <summary>Generic library entry point with the same admission result.</summary>
    [Benchmark]
    public bool SafeConversion() => SafeInteger.TryConvert<int, byte>(Input, out _) == ArithmeticError.None;

    /// <summary>Historical checked conversion strategy, including rejected-input allocation.</summary>
    [Benchmark]
    public bool CheckedConversion()
    {
        try { _ = checked((byte)Input); return true; }
        catch (OverflowException) { return false; }
    }
}
