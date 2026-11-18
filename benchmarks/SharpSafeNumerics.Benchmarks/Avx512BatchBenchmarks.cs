using System.Runtime.InteropServices;
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

    [Params(17, 257, 4099, 262147)]
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

        Verify(SafeInteger.AddBatch(left, right, values, errors).VectorWidthUsed, 256);
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
    public int Add256() => SafeInteger.AddBatch(left, right, values, errors).VectorWidthUsed;

    [Benchmark]
    public int Add512() => Add512Core(left, right, values, errors);

    private static int Add512Core(ReadOnlySpan<int> left, ReadOnlySpan<int> right,
        Span<int> values, Span<ArithmeticError> errors)
    {
        // Match production validation and byte-backed output before timing any stores.
        int count = left.Length;
        if (right.Length != count || count > int.MaxValue / sizeof(int) ||
            values.Length < count || errors.Length < count) return 0;
        Span<int> written = values[..count];
        Span<byte> statusBytes = MemoryMarshal.AsBytes(errors[..count]);
        Span<byte> valueBytes = MemoryMarshal.AsBytes(written);
        var leftBytes = MemoryMarshal.AsBytes(left);
        var rightBytes = MemoryMarshal.AsBytes(right);
        if (valueBytes.Overlaps(leftBytes) || valueBytes.Overlaps(rightBytes) ||
            statusBytes.Overlaps(leftBytes) || statusBytes.Overlaps(rightBytes) || valueBytes.Overlaps(statusBytes))
            return 0;

        int i = 0;
        for (; i <= count - Vector512<int>.Count; i += Vector512<int>.Count)
        {
            var a = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(left), (nuint)i);
            var b = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(right), (nuint)i);
            var sum = a + b;
            var overflow = Vector512.LessThan((a ^ sum) & (b ^ sum), Vector512<int>.Zero);
            var failure = Vector512.ConditionalSelect(Vector512.LessThan(a, Vector512<int>.Zero),
                Vector512.Create((int)ArithmeticError.Underflow), Vector512.Create((int)ArithmeticError.Overflow));
            var status = Vector512.ConditionalSelect(overflow, failure, Vector512.Create((int)ArithmeticError.None));
            Vector512.ConditionalSelect(overflow, Vector512<int>.Zero, sum)
                .StoreUnsafe(ref MemoryMarshal.GetReference(written), (nuint)i);
            // Each validated status fits in one byte. VPMOVDB packs all sixteen lanes.
            Avx512F.ConvertToVector128Byte(status.AsUInt32())
                .StoreUnsafe(ref MemoryMarshal.GetReference(statusBytes), (nuint)i);
        }

        int width = i > 0 ? 512 : 0;
        if (i == count) return width;
        var tail = SafeInteger.AddBatch(left[i..], right[i..], written[i..], errors[i..count]);
        return Math.Max(width, tail.VectorWidthUsed);
    }
}
