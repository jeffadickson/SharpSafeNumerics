using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

/// <summary>Paired steady-state timings with alternating order and a lane oracle.</summary>
internal static class Avx512Sweep
{
    internal static int Run()
    {
        if (!Avx512F.IsSupported || !Vector512.IsHardwareAccelerated || !Vector256.IsHardwareAccelerated)
        {
            Console.Error.WriteLine("The runner lacks hardware AVX-512 or SIMD256.");
            return 3;
        }

        Console.WriteLine($"# {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; stopwatchHz={Stopwatch.Frequency}");
        Console.WriteLine("lanes,round,first,simd256_ns,avx512_ns");
        foreach (int length in new[] { 4099, 8195, 16387, 24579, 32771, 49155, 65539, 98307, 131075, 196611, 262147, 393219, 524291 })
        {
            var batch = new Avx512Comparison { Length = length };
            batch.Setup();
            int iterations = Math.Max(64, 8_388_608 / length);
            _ = Time(batch.Add256, iterations);
            _ = Time(batch.Add512, iterations);
            for (int round = 0; round < 9; round++)
            {
                // Reverse order each round to reduce directional effects from clock drift.
                bool wideFirst = (round & 1) != 0;
                double wide = wideFirst ? Time(batch.Add512, iterations) : 0;
                double narrow = Time(batch.Add256, iterations);
                if (!wideFirst) wide = Time(batch.Add512, iterations);
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{length},{round},{(wideFirst ? 512 : 256)},{narrow:F3},{wide:F3}"));
            }
        }
        return 0;
    }

    private static double Time(Func<int> add, int iterations)
    {
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++) _ = add();
        return Stopwatch.GetElapsedTime(start).TotalNanoseconds / iterations;
    }
}
