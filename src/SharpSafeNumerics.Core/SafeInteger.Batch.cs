using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpSafeNumerics;

public static partial class SafeInteger
{
    // The hosted comparison favored AVX-512 for mid-size batches and SIMD256 at 17 lanes.
    private const int BatchAdd512MinimumLength = 256;
    private static bool BatchAdd512Supported => Avx512F.IsSupported && Vector512.IsHardwareAccelerated;

    /// <summary>
    /// Maximum hardware-accelerated width used for 32-bit batch addition: 512, 256, 128,
    /// or zero for scalar execution. Short batches and tails may use narrower paths.
    /// </summary>
    public static int BatchAddVectorWidth => BatchAdd512Supported ? 512 : Vector256.IsHardwareAccelerated ? 256 : Vector128.IsHardwareAccelerated ? 128 : 0;

    /// <summary>
    /// Add signed 32-bit input lanes with detected SIMD and scalar fallback. AVX-512 is used from 256 lanes.
    /// Each lane receives None and its exact sum, or Overflow/Underflow and zero.
    /// Inputs must have equal length and destinations sufficient capacity. Written
    /// destination prefixes must be disjoint from all inputs and each other;
    /// read-only inputs may alias. Contract failure leaves both destinations untouched.
    /// Unused destination suffixes remain untouched, including for an empty batch.
    /// </summary>
    public static BatchArithmeticError TryAddBatch(ReadOnlySpan<int> left, ReadOnlySpan<int> right,
        Span<int> values, Span<ArithmeticError> errors) => TryAddBatchCore(left, right, values, errors, 512, out _);

    /// <summary>
    /// Add a batch with the same contract as TryAddBatch and report the widest vector
    /// loop actually executed (512, 256, 128 or zero). Contract failures report zero.
    /// This diagnostic describes execution width, not a timing or throughput guarantee.
    /// </summary>
    public static BatchArithmeticResult AddBatch(ReadOnlySpan<int> left, ReadOnlySpan<int> right,
        Span<int> values, Span<ArithmeticError> errors)
    {
        // Return diagnostics by value. An out parameter could alias a lane/input
        // and overwrite either arithmetic data or a destination on contract failure.
        var error = TryAddBatchCore(left, right, values, errors, 512, out int vectorWidthUsed);
        return new BatchArithmeticResult(error, vectorWidthUsed);
    }

    internal static BatchArithmeticError TryAddBatchCore(ReadOnlySpan<int> left, ReadOnlySpan<int> right,
        Span<int> values, Span<ArithmeticError> errors, int maximumVectorWidth, out int vectorWidthUsed)
    {
        vectorWidthUsed = 0;
        if (left.Length != right.Length) return BatchArithmeticError.LengthMismatch;
        int count = left.Length;
        // MemoryMarshal.AsBytes has an int byte length; reject very large valid
        // spans before its multiplication could throw, without touching memory.
        if (count > int.MaxValue / sizeof(int)) return BatchArithmeticError.InputTooLarge;
        if (values.Length < count || errors.Length < count) return BatchArithmeticError.DestinationTooSmall;
        Span<int> writtenValues = values[..count];
        Span<byte> errorBytes = MemoryMarshal.AsBytes(errors[..count]);
        ReadOnlySpan<byte> leftBytes = MemoryMarshal.AsBytes(left), rightBytes = MemoryMarshal.AsBytes(right);
        Span<byte> valueBytes = MemoryMarshal.AsBytes(writtenValues);
        if (valueBytes.Overlaps(leftBytes) || valueBytes.Overlaps(rightBytes) ||
            errorBytes.Overlaps(leftBytes) || errorBytes.Overlaps(rightBytes) || valueBytes.Overlaps(errorBytes))
            return BatchArithmeticError.OverlappingBuffers;

        int i = 0;
        if (maximumVectorWidth >= 512 && count >= BatchAdd512MinimumLength && BatchAdd512Supported)
        {
            for (; i <= count - Vector512<int>.Count; i += Vector512<int>.Count)
            {
                vectorWidthUsed = 512;
                var a = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(left), (nuint)i);
                var b = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(right), (nuint)i);
                var sum = a + b;
                // Candidate low bits never escape until per-lane sign overflow is resolved.
                var overflow = Vector512.LessThan((a ^ sum) & (b ^ sum), Vector512<int>.Zero);
                var failure = Vector512.ConditionalSelect(Vector512.LessThan(a, Vector512<int>.Zero),
                    Vector512.Create((int)ArithmeticError.Underflow), Vector512.Create((int)ArithmeticError.Overflow));
                var status = Vector512.ConditionalSelect(overflow, failure, Vector512.Create((int)ArithmeticError.None));
                Vector512.ConditionalSelect(overflow, Vector512<int>.Zero, sum)
                    .StoreUnsafe(ref MemoryMarshal.GetReference(writtenValues), (nuint)i);
                // All status values fit in a byte. Narrowing only packs already-validated
                // internal enum codes; it never truncates a caller's numeric value.
                Avx512F.ConvertToVector128Byte(status.AsUInt32())
                    .StoreUnsafe(ref MemoryMarshal.GetReference(errorBytes), (nuint)i);
            }
        }
        if (maximumVectorWidth >= 256 && Vector256.IsHardwareAccelerated)
        {
            for (; i <= count - Vector256<int>.Count; i += Vector256<int>.Count)
            {
                if (vectorWidthUsed == 0) vectorWidthUsed = 256;
                var a = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(left), (nuint)i);
                var b = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(right), (nuint)i);
                var sum = a + b;
                // Candidate low bits never escape until per-lane sign overflow is resolved.
                var overflow = Vector256.LessThan((a ^ sum) & (b ^ sum), Vector256<int>.Zero);
                var failure = Vector256.ConditionalSelect(Vector256.LessThan(a, Vector256<int>.Zero),
                    Vector256.Create((int)ArithmeticError.Underflow), Vector256.Create((int)ArithmeticError.Overflow));
                var status = Vector256.ConditionalSelect(overflow, failure, Vector256.Create((int)ArithmeticError.None));
                Vector256.ConditionalSelect(overflow, Vector256<int>.Zero, sum)
                    .StoreUnsafe(ref MemoryMarshal.GetReference(writtenValues), (nuint)i);
                // All status values fit in a byte. Narrowing only packs already-validated
                // internal enum codes; it never truncates a caller's numeric value.
                var words = Vector128.Narrow(status.GetLower().AsUInt32(), status.GetUpper().AsUInt32());
                Vector128.Narrow(words, Vector128<ushort>.Zero).GetLower()
                    .StoreUnsafe(ref MemoryMarshal.GetReference(errorBytes), (nuint)i);
            }
        }
        if (maximumVectorWidth >= 128 && Vector128.IsHardwareAccelerated)
        {
            for (; i <= count - Vector128<int>.Count; i += Vector128<int>.Count)
            {
                if (vectorWidthUsed == 0) vectorWidthUsed = 128;
                var a = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(left), (nuint)i);
                var b = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(right), (nuint)i);
                var sum = a + b;
                var overflow = Vector128.LessThan((a ^ sum) & (b ^ sum), Vector128<int>.Zero);
                var failure = Vector128.ConditionalSelect(Vector128.LessThan(a, Vector128<int>.Zero),
                    Vector128.Create((int)ArithmeticError.Underflow), Vector128.Create((int)ArithmeticError.Overflow));
                var status = Vector128.ConditionalSelect(overflow, failure, Vector128.Create((int)ArithmeticError.None));
                Vector128.ConditionalSelect(overflow, Vector128<int>.Zero, sum)
                    .StoreUnsafe(ref MemoryMarshal.GetReference(writtenValues), (nuint)i);
                var words = Vector128.Narrow(status.AsUInt32(), Vector128<uint>.Zero);
                uint packed = Vector128.Narrow(words, Vector128<ushort>.Zero).AsUInt32().GetElement(0);
                MemoryMarshal.Write(errorBytes[i..], in packed);
            }
        }
        for (; i < count; i++) errors[i] = TryAdd(left[i], right[i], out writtenValues[i]);
        return BatchArithmeticError.None;
    }
}
