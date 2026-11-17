using System.Buffers.Binary;
using System.Numerics;
using SharpSafeNumerics;
using Xunit;

/// <summary>Reproducible raw-encoding admission and algebra checks, including noncanonical/special encodings.</summary>
public sealed class DecimalEncodingFuzzTests
{
    [Fact]
    public void RawEncodingCampaignSeed731929()
    {
        var random = new Random(731929);
        Span<byte> bytes = stackalloc byte[16];
        for (int i = 0; i < 1024; i++)
        {
            random.NextBytes(bytes);
            Check(Decimal32.DecodeBinary(BinaryPrimitives.ReadUInt32LittleEndian(bytes)));
            Check(Decimal64.DecodeBinary(BinaryPrimitives.ReadUInt64LittleEndian(bytes)));
            Check(Decimal128.DecodeBinary(BinaryPrimitives.ReadUInt128LittleEndian(bytes)));
        }
    }

    private static void Check<T>(T value) where T : struct, IFloatingPointIeee754<T>
    {
        var sum = SafeDecimal.TryAdd(value, T.Zero);
        var difference = SafeDecimal.TrySubtract(value, value);
        var conversion = SafeDecimal.TryConvert<T, T>(value);
        if (!T.IsFinite(value))
        {
            Assert.Equal(ArithmeticError.NonFinite, sum.Error);
            Assert.Equal(ArithmeticError.NonFinite, difference.Error);
            Assert.Equal(ArithmeticError.NonFinite, conversion.Error);
            Assert.False(sum.TryGetValue(out var cleared));
            Assert.Equal(T.Zero, cleared);
            return;
        }
        // Exact algebra identities independently pin behavior over arbitrary
        // encodings without reproducing the rational parser or BCL arithmetic.
        Assert.True(sum.IsSuccess); Assert.False(sum.IsInexact); Assert.Equal(value, sum.Value);
        Assert.True(difference.IsSuccess); Assert.False(difference.IsInexact); Assert.Equal(T.Zero, difference.Value);
        Assert.True(conversion.IsSuccess); Assert.False(conversion.IsInexact); Assert.Equal(value, conversion.Value);
        Assert.Equal(value, SafeDecimal.TryNegate(SafeDecimal.TryNegate(value).Value).Value);
        var quotient = SafeDecimal.TryDivide(value, value);
        if (T.IsZero(value)) Assert.Equal(ArithmeticError.DivideByZero, quotient.Error);
        else { Assert.True(quotient.IsSuccess); Assert.False(quotient.IsInexact); Assert.Equal(T.One, quotient.Value); }
    }
}
