// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using SharpSafeNumerics;

namespace SharpSafeNumerics.Verification;

/// <summary>Test-only exact arithmetic reference; never linked into the runtime library.</summary>
internal static class WideIntegerOracle
{
    internal const int Seed = 731922;

    internal static void Verify<T>(T left, T right)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        BigInteger a = BigInteger.CreateChecked(left), b = BigInteger.CreateChecked(right);
        BigInteger lo = BigInteger.CreateChecked(T.MinValue), hi = BigInteger.CreateChecked(T.MaxValue);
        Check(SafeInteger.TryAdd(left, right, out T sum), sum, a + b, lo, hi, "add");
        Check(SafeInteger.TrySubtract(left, right, out T difference), difference, a - b, lo, hi, "subtract");
        Check(SafeInteger.TryMultiply(left, right, out T product), product, a * b, lo, hi, "multiply");
        var division = SafeInteger.TryDivide(left, right, out T quotient);
        if (b.IsZero)
            Require(division == ArithmeticError.DivideByZero && quotient == T.Zero, "divide-zero");
        else
            Check(division, quotient, a / b, lo, hi, "divide"); // BigInteger division truncates toward zero.
        Check(SafeInteger.TryNegate(left, out T negated), negated, -a, lo, hi, "negate");

        for (int operation = 0; operation < 3; operation++)
        {
            BigInteger exact = operation switch { 0 => a + b, 1 => a - b, _ => a * b };
            ArithmeticError expectedError = Expected(exact, lo, hi);
            for (int raw = 0; raw <= 3; raw++)
            {
                var policy = (ArithmeticPolicy)raw;
                NumericResult<T> result;
                try
                {
                    result = operation switch
                    {
                        0 => SafeInteger.Add(left, right, policy),
                        1 => SafeInteger.Subtract(left, right, policy),
                        _ => SafeInteger.Multiply(left, right, policy)
                    };
                }
                catch (OverflowException)
                {
                    Require(policy == ArithmeticPolicy.Throw && expectedError != ArithmeticError.None,
                        $"unexpected throw, operation {operation}, policy {policy}, operands {a},{b}");
                    continue;
                }

                if (policy == ArithmeticPolicy.Throw && expectedError != ArithmeticError.None)
                    Require(false, $"missing throw, operation {operation}, operands {a},{b}");

                bool rejected = policy == ArithmeticPolicy.Reject && expectedError != ArithmeticError.None;
                Require(result.Error == (rejected ? expectedError : ArithmeticError.None), "policy-status");
                bool gotValue = result.TryGetValue(out T value);
                Require(gotValue == !rejected, "policy-validity");
                BigInteger expected = rejected ? BigInteger.Zero : policy switch
                {
                    ArithmeticPolicy.Saturate => BigInteger.Clamp(exact, lo, hi),
                    ArithmeticPolicy.Wrap => Wrap(exact, lo, hi),
                    _ => exact
                };
                Require(BigInteger.CreateChecked(value) == expected,
                    $"{typeof(T).Name} policy {policy}, operation {operation}, operands {a},{b}, exact {exact}, expected {expected}, actual {value}");
            }
        }
    }

    internal static (int Boundaries, long Pairs) Campaign<T>(int randomPairs = 4_096)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        BigInteger lo = BigInteger.CreateChecked(T.MinValue), hi = BigInteger.CreateChecked(T.MaxValue);
        var candidates = new HashSet<BigInteger> { lo, hi, 0, 1, -1, 2, -2 };
        foreach (int bit in new[] { 7, 8, 15, 16, 31, 32, 63, 64, 127, 128 })
            for (int offset = -1; offset <= 1; offset++)
            {
                candidates.Add((BigInteger.One << bit) + offset);
                candidates.Add(-(BigInteger.One << bit) + offset);
            }
        for (int offset = 1; offset <= 2; offset++)
        {
            candidates.Add(lo + offset);
            candidates.Add(hi - offset);
        }
        T[] boundaries = candidates.Where(x => lo <= x && x <= hi).Order().Select(T.CreateChecked).ToArray();
        long pairs = 0;
        foreach (T left in boundaries)
            foreach (T right in boundaries) { Verify(left, right); pairs++; }
        var random = new Random(Seed);
        byte[] bytes = new byte[T.Zero.GetByteCount()];
        T Next()
        {
            random.NextBytes(bytes);
            return T.CreateChecked(new BigInteger(bytes, isUnsigned: lo.IsZero, isBigEndian: false));
        }
        for (int i = 0; i < randomPairs; i++) { Verify(Next(), Next()); pairs++; }
        return (boundaries.Length, pairs);
    }

    internal static void VerifyAll(UInt128 a, UInt128 b)
    {
        Verify(byte.CreateTruncating(a), byte.CreateTruncating(b));
        Verify(sbyte.CreateTruncating(a), sbyte.CreateTruncating(b));
        Verify(short.CreateTruncating(a), short.CreateTruncating(b));
        Verify(ushort.CreateTruncating(a), ushort.CreateTruncating(b));
        Verify(int.CreateTruncating(a), int.CreateTruncating(b));
        Verify(uint.CreateTruncating(a), uint.CreateTruncating(b));
        Verify(long.CreateTruncating(a), long.CreateTruncating(b));
        Verify(ulong.CreateTruncating(a), ulong.CreateTruncating(b));
        Verify(Int128.CreateTruncating(a), Int128.CreateTruncating(b));
        Verify(a, b);
        Verify(nint.CreateTruncating(a), nint.CreateTruncating(b));
        Verify(nuint.CreateTruncating(a), nuint.CreateTruncating(b));
    }

    private static BigInteger Wrap(BigInteger exact, BigInteger lo, BigInteger hi)
    {
        BigInteger modulus = hi - lo + 1;
        BigInteger remainder = (exact - lo) % modulus;
        if (remainder.Sign < 0) remainder += modulus;
        return lo + remainder;
    }

    private static ArithmeticError Expected(BigInteger exact, BigInteger lo, BigInteger hi) =>
        exact < lo ? ArithmeticError.Underflow : exact > hi ? ArithmeticError.Overflow : ArithmeticError.None;

    private static void Check<T>(ArithmeticError actual, T value, BigInteger exact, BigInteger lo, BigInteger hi, string operation)
        where T : struct, IBinaryInteger<T>
    {
        var expected = Expected(exact, lo, hi);
        Require(actual == expected && BigInteger.CreateChecked(value) == (expected == ArithmeticError.None ? exact : BigInteger.Zero),
            $"{typeof(T).Name} {operation}: exact {exact}, expected {expected}, actual {actual}/{value}");
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Independent integer oracle mismatch: " + description);
    }
}
