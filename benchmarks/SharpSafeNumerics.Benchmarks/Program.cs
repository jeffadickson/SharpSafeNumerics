using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using SharpSafeNumerics;

BenchmarkRunner.Run<AddBenchmarks>();

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
