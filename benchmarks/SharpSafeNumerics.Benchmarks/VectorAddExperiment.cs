// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SharpSafeNumerics;

namespace SharpSafeNumerics.Experiments;

/// <summary>Verification/benchmark-only batch experiment; not a supported library API.</summary>
internal static class VectorAddExperiment
{
    internal static void Add(ReadOnlySpan<int> left, ReadOnlySpan<int> right,
        Span<int> values, Span<int> statuses, bool allowHardware = true)
    {
        // Output buffers must be disjoint; the two read-only inputs may alias.
        // Validate lengths before publishing any result, including on scalar fallback.
        if (right.Length != left.Length || values.Length != left.Length || statuses.Length != left.Length)
            throw new ArgumentException("All experiment buffers must have equal lengths.");
        if (values.Overlaps(left) || values.Overlaps(right) || values.Overlaps(statuses) ||
            statuses.Overlaps(left) || statuses.Overlaps(right))
            throw new ArgumentException("Experiment output buffers must not overlap other buffers.");

        int i = 0;
        if (allowHardware && Vector256.IsHardwareAccelerated)
        {
            for (; i <= left.Length - Vector256<int>.Count; i += Vector256<int>.Count)
            {
                var a = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(left), (nuint)i);
                var b = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(right), (nuint)i);
                // Candidate low bits remain internal until sign-overflow validation.
                var sum = a + b;
                var overflow = Vector256.LessThan((a ^ sum) & (b ^ sum), Vector256<int>.Zero);
                var failure = Vector256.ConditionalSelect(Vector256.LessThan(a, Vector256<int>.Zero),
                    Vector256.Create((int)ArithmeticError.Underflow), Vector256.Create((int)ArithmeticError.Overflow));
                var status = Vector256.ConditionalSelect(overflow, failure, Vector256.Create((int)ArithmeticError.None));
                Vector256.ConditionalSelect(overflow, Vector256<int>.Zero, sum)
                    .StoreUnsafe(ref MemoryMarshal.GetReference(values), (nuint)i);
                status.StoreUnsafe(ref MemoryMarshal.GetReference(statuses), (nuint)i);
            }
        }
        for (; i < left.Length; i++)
            statuses[i] = (int)SafeInteger.TryAdd(left[i], right[i], out values[i]);
    }
}
