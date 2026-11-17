using System.Numerics;
using Xunit;
namespace SharpSafeNumerics.UnitTests;

public sealed class Fixed64Tests
{
    [Fact]
    public void HalfTiesAndInvalidModes()
    {
        foreach (long sign in new long[] { -1, 1 })
        {
            Assert.Equal(0, SafeFixed.FromRatio(sign, 1L << 33, FixedRounding.ToEven).Value.RawValue);
            Assert.Equal(sign, SafeFixed.FromRatio(sign, 1L << 33, FixedRounding.AwayFromZero).Value.RawValue);
            Assert.Equal(ArithmeticError.Inexact, SafeFixed.FromRatio(sign, 1L << 33).Error);
        }
        for (int mode = 6; mode <= 255; mode++)
        {
            Assert.Equal(ArithmeticError.OutOfRange, SafeFixed.Divide(default, default, (FixedRounding)mode).Error);
            Assert.Equal(ArithmeticError.OutOfRange, SafeFixed.Multiply(default, default, (FixedRounding)mode).Error);
            Assert.Equal(ArithmeticError.OutOfRange, SafeFixed.FromRatio(0, 0, (FixedRounding)mode).Error);
        }
    }

    [Fact]
    public void EndpointsAndClearedFailures()
    {
        Assert.Equal(ArithmeticError.Overflow, SafeFixed.FromInt64(1L << 31).Error);
        Assert.Equal(long.MinValue, SafeFixed.FromInt64(-(1L << 31)).Value.RawValue);
        var max = Fixed64.FromRaw(long.MaxValue);
        var min = Fixed64.FromRaw(long.MinValue);
        Assert.Equal(ArithmeticError.Overflow, SafeFixed.Add(max, Fixed64.FromRaw(1)).Error);
        Assert.Equal(ArithmeticError.Underflow, SafeFixed.Subtract(min, Fixed64.FromRaw(1)).Error);
        Assert.Equal(ArithmeticError.Overflow, SafeFixed.Negate(min).Error);
        var failure = SafeFixed.Divide(max, default);
        Assert.Equal(ArithmeticError.DivideByZero, failure.Error);
        Assert.False(failure.TryGetValue(out var cleared));
        Assert.Equal(0, cleared.RawValue);
        Assert.Equal(default, SafeFixed.Add(default, default).Value);
    }

    [Fact]
    public void IndependentlyExpectedSignedRoundingTables()
    {
        // Entries follow Exact, ToEven, ToZero, Floor, Ceiling, AwayFromZero.
        long?[][] expected = [[null, 0, 0, 0, 1, 1], [null, 0, 0, -1, 0, -1],
            [null, 1, 1, 1, 2, 2], [null, -1, -1, -2, -1, -2],
            [null, 2, 1, 1, 2, 2], [null, -2, -1, -2, -1, -2]];
        long[] numerator = [1, -1, 5, -5, 3, -3];
        long[] denominator = [2L << 32, 2L << 32, 4L << 32, 4L << 32, 2L << 32, 2L << 32];
        for (int row = 0; row < numerator.Length; row++)
        for (int column = 0; column < 6; column++)
        foreach (long denominatorSign in new long[] { -1, 1 })
        {
            var result = SafeFixed.FromRatio(numerator[row] * denominatorSign, denominator[row] * denominatorSign, (FixedRounding)column);
            if (expected[row][column] is long raw) Assert.Equal(raw, result.Value.RawValue);
            else { Assert.Equal(ArithmeticError.Inexact, result.Error); AssertCleared(result); }
        }
        var maximum = Fixed64.FromRaw(long.MaxValue);
        Assert.Equal(ArithmeticError.Inexact, SafeFixed.Multiply(maximum, maximum).Error);
        // (2^63-1)*2^32/(2^32-1) lies above max; directed rounding cannot hide the carry.
        Assert.Equal(ArithmeticError.Overflow, SafeFixed.Divide(maximum, Fixed64.FromRaw((1L << 32) - 1), FixedRounding.ToZero).Error);
        // Independent algebra: (max - (2^31-1))*(2^32+1)/2^32 = max + 1/2.
        var factor = Fixed64.FromRaw((1L << 32) + 1);
        var positive = Fixed64.FromRaw(long.MaxValue - ((1L << 31) - 1));
        Assert.Equal(long.MaxValue, SafeFixed.Multiply(positive, factor, FixedRounding.ToZero).Value.RawValue);
        Assert.Equal(long.MaxValue, SafeFixed.Multiply(positive, factor, FixedRounding.Floor).Value.RawValue);
        Assert.Equal(ArithmeticError.Overflow, SafeFixed.Multiply(positive, factor, FixedRounding.ToEven).Error);
        // The negative product is min - 1/2 - 2^-32; truncation admits min, floor carries out.
        var negative = Fixed64.FromRaw(long.MinValue + ((1L << 31) - 1));
        Assert.Equal(long.MinValue, SafeFixed.Multiply(negative, factor, FixedRounding.ToZero).Value.RawValue);
        Assert.Equal(long.MinValue, SafeFixed.Multiply(negative, factor, FixedRounding.Ceiling).Value.RawValue);
        Assert.Equal(ArithmeticError.Underflow, SafeFixed.Multiply(negative, factor, FixedRounding.Floor).Error);
    }

    [Fact]
    public void BoundaryMatrixAndRoundingCarries()
    {
        long[] raw = [long.MinValue, long.MinValue + 1, -(1L << 32), -3, -1, 0, 1, 3, 1L << 32, long.MaxValue - 1, long.MaxValue];
        foreach (long a in raw)
        foreach (long b in raw)
        {
            var left = Fixed64.FromRaw(a);
            var right = Fixed64.FromRaw(b);
            Check(SafeFixed.Add(left, right), (BigInteger)a + b, 1, FixedRounding.Exact);
            Check(SafeFixed.Subtract(left, right), (BigInteger)a - b, 1, FixedRounding.Exact);
            Check(SafeFixed.Negate(left), -(BigInteger)a, 1, FixedRounding.Exact);
            foreach (FixedRounding mode in Enum.GetValues<FixedRounding>())
            {
                Check(SafeFixed.Multiply(left, right, mode), (BigInteger)a * b, BigInteger.One << 32, mode);
                Check(SafeFixed.Divide(left, right, mode), (BigInteger)a << 32, b, mode);
                Check(SafeFixed.FromRatio(a, b, mode), (BigInteger)a << 32, b, mode);
            }
        }
        foreach (long sign in new long[] { -1, 1 })
        {
            Assert.Equal(sign * 2, SafeFixed.FromRatio(sign * 3, 1L << 33, FixedRounding.ToEven).Value.RawValue);
            Assert.Equal(sign * 2, SafeFixed.FromRatio(sign * 5, 1L << 33, FixedRounding.ToEven).Value.RawValue);
        }
        Assert.True(Fixed64.FromRaw(1).Equals((object)Fixed64.FromRaw(1)));
        Assert.False(Fixed64.FromRaw(1).Equals(null));
        Assert.False(Fixed64.FromRaw(1).Equals((object)Fixed64.FromRaw(2)));
        Assert.Equal(1L.GetHashCode(), Fixed64.FromRaw(1).GetHashCode());
    }

    [Fact]
    public void RationalOracleSeed731927()
    {
        var random = new Random(731927);
        for (int i = 0; i < 6000; i++)
        {
            long a = random.NextInt64(long.MinValue, long.MaxValue);
            long b = random.NextInt64(long.MinValue, long.MaxValue);
            foreach (FixedRounding mode in Enum.GetValues<FixedRounding>())
            {
                Check(SafeFixed.Multiply(Fixed64.FromRaw(a), Fixed64.FromRaw(b), mode), (BigInteger)a * b, BigInteger.One << 32, mode);
                if (b != 0) Check(SafeFixed.Divide(Fixed64.FromRaw(a), Fixed64.FromRaw(b), mode), (BigInteger)a << 32, b, mode);
                Check(SafeFixed.FromRatio(a, b, mode), (BigInteger)a << 32, b, mode);
            }
        }
    }

    [Fact]
    public void WarmArithmeticDoesNotAllocate()
    {
        var a = Fixed64.FromRaw(1234567);
        var b = Fixed64.FromRaw(9876543);
        long total = 0;
        for (int i = 0; i < 10000; i++) total ^= SafeFixed.Multiply(a, b, FixedRounding.ToEven).Value.RawValue;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            total ^= SafeFixed.Multiply(a, b, FixedRounding.ToEven).Value.RawValue;
            total ^= SafeFixed.Divide(a, b, FixedRounding.ToEven).Value.RawValue;
            total ^= SafeFixed.Add(a, b).Value.RawValue;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(total);
        Assert.Equal(0, allocated);
    }

    private static void AssertCleared(NumericResult<Fixed64> result)
    {
        Assert.False(result.TryGetValue(out var clear));
        Assert.Equal(0, clear.RawValue);
    }

    private static void Check(NumericResult<Fixed64> actual, BigInteger numerator, BigInteger denominator, FixedRounding mode)
    {
        if (denominator.IsZero) { Assert.Equal(ArithmeticError.DivideByZero, actual.Error); AssertCleared(actual); return; }
        if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        BigInteger q = BigInteger.DivRem(numerator, denominator, out var rem);
        if (!rem.IsZero)
        {
            if (mode == FixedRounding.Exact) { Assert.Equal(ArithmeticError.Inexact, actual.Error); AssertCleared(actual); return; }
            bool increment = mode switch
            {
                FixedRounding.AwayFromZero => true,
                FixedRounding.Floor => numerator.Sign < 0,
                FixedRounding.Ceiling => numerator.Sign > 0,
                FixedRounding.ToEven => BigInteger.Abs(rem) * 2 > denominator || (BigInteger.Abs(rem) * 2 == denominator && !q.IsEven),
                _ => false
            };
            if (increment) q += numerator.Sign;
        }
        if (q > long.MaxValue) Assert.Equal(ArithmeticError.Overflow, actual.Error);
        else if (q < long.MinValue) Assert.Equal(ArithmeticError.Underflow, actual.Error);
        else { Assert.True(actual.IsSuccess); Assert.Equal((long)q, actual.Value.RawValue); }
        if (!actual.IsSuccess) AssertCleared(actual);
    }
}



