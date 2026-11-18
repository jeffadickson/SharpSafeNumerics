using System.Globalization;
using System.Numerics;
using PeterO.Numbers;
using Xunit;

namespace SharpSafeNumerics.DecimalTests;

/// <summary>Independent conversion boundary and bit-level regression tests.</summary>
public sealed class IeeeConversionTests
{
    [Fact]
    public void IntegerAdmissionAndRoundingAllDecimalFormats()
    {
        IntegerOracle<Decimal32>(EContext.Decimal32);
        IntegerOracle<Decimal64>(EContext.Decimal64);
        IntegerOracle<Decimal128>(EContext.Decimal128);
        Assert.Equal(Decimal128.Parse(UInt128.MaxValue.ToString(CultureInfo.InvariantCulture)), IeeeDecimal<Decimal128>.FromInteger(UInt128.MaxValue).Value);
        Assert.Equal(Decimal64.CreateChecked(nint.MinValue), IeeeDecimal<Decimal64>.FromInteger(nint.MinValue).Value);
        Assert.Equal(Decimal64.CreateChecked(nuint.MaxValue), IeeeDecimal<Decimal64>.FromInteger(nuint.MaxValue).Value);
    }

    [Fact]
    public void EveryBuiltInIntegerCarrierAndNativeWidth()
    {
        IntegerCarrier<sbyte>(); IntegerCarrier<byte>(); IntegerCarrier<short>(); IntegerCarrier<ushort>();
        IntegerCarrier<int>(); IntegerCarrier<uint>(); IntegerCarrier<long>(); IntegerCarrier<ulong>();
        IntegerCarrier<Int128>(); IntegerCarrier<UInt128>(); IntegerCarrier<nint>(); IntegerCarrier<nuint>();
        Assert.Equal(long.MinValue, IeeeDecimal<Decimal128>.ToInteger<long>(Decimal128.Parse(long.MinValue.ToString(CultureInfo.InvariantCulture))).Value);
        Assert.Equal(ulong.MaxValue, IeeeDecimal<Decimal128>.ToInteger<ulong>(Decimal128.Parse(ulong.MaxValue.ToString(CultureInfo.InvariantCulture))).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal128>.ToInteger<long>(Decimal128.Parse("9223372036854775808")).Flags);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal128>.ToInteger<ulong>(Decimal128.Parse("18446744073709551616")).Flags);
        Assert.Equal((Int128)1 << 100, IeeeDecimal<Decimal128>.ToInteger<Int128>(Decimal128.Parse("1267650600228229401496703205376")).Value);
    }

    private static void IntegerCarrier<U>() where U : struct, IBinaryInteger<U>, IMinMaxValue<U>
    {
        U input = U.CreateChecked(42);
        Assert.Equal(Decimal32.CreateChecked(42), IeeeDecimal<Decimal32>.FromInteger(input).Value);
        Assert.Equal(input, IeeeDecimal<Decimal64>.ToInteger<U>(Decimal64.CreateChecked(42)).Value);
        bool signed = U.IsNegative(U.MinValue);
        var negative = IeeeDecimal<Decimal128>.ToInteger<U>(Decimal128.NegativeOne);
        if (signed) { Assert.Equal(U.CreateChecked(-1), negative.Value); Assert.Equal(IeeeDecimalFlags.None, negative.Flags); }
        else { Assert.Equal(U.Zero, negative.Value); Assert.Equal(IeeeDecimalFlags.Invalid, negative.Flags); }
        // Directed decimal admission makes these values provably outside the integer
        // bounds even when a 128-bit integer boundary needs more than 34 decimal digits.
        var above = IeeeDecimal<Decimal128>.FromInteger(BigInteger.CreateChecked(U.MaxValue) + 1, new(IeeeDecimalRounding.TowardPositive)).Value;
        var below = IeeeDecimal<Decimal128>.FromInteger(BigInteger.CreateChecked(U.MinValue) - 1, new(IeeeDecimalRounding.TowardNegative)).Value;
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal128>.ToInteger<U>(above).Flags);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal128>.ToInteger<U>(below).Flags);
        var minOracle = EDecimal.FromString(U.MinValue.ToString(null, CultureInfo.InvariantCulture)).RoundToPrecision(EContext.Decimal128);
        Assert.Equal(Decimal128.Parse(minOracle.ToString(), CultureInfo.InvariantCulture), IeeeDecimal<Decimal128>.FromInteger(U.MinValue).Value);
    }

    [Theory]
    [InlineData(IeeeDecimalRounding.ToNearestEven, 2, -2)]
    [InlineData(IeeeDecimalRounding.ToNearestAway, 3, -3)]
    [InlineData(IeeeDecimalRounding.TowardZero, 2, -2)]
    [InlineData(IeeeDecimalRounding.TowardNegative, 2, -3)]
    [InlineData(IeeeDecimalRounding.TowardPositive, 3, -2)]
    public void IntegerDirectionsAndExactFlag(IeeeDecimalRounding mode, int positive, int negative)
    {
        var c = new IeeeDecimalContext(mode);
        Assert.Equal(positive, IeeeDecimal<Decimal32>.ToInteger<int>(Decimal32.Parse("2.5"), c).Value);
        Assert.Equal(negative, IeeeDecimal<Decimal32>.ToInteger<int>(Decimal32.Parse("-2.5"), c).Value);
        Assert.Equal(IeeeDecimalFlags.Inexact, IeeeDecimal<Decimal32>.ToInteger<int>(Decimal32.Parse("2.5"), c).Flags);
        Assert.Equal(IeeeDecimalFlags.None, IeeeDecimal<Decimal32>.ToInteger<int>(Decimal32.Parse("2.5"), c, exact: false).Flags);
    }

    [Fact]
    public void InvalidIntegerConversionsDeliverSelectedZeroAndTrapClearsStorage()
    {
        foreach (var x in new[] { Decimal64.NaN, Decimal64.PositiveInfinity, Decimal64.NegativeInfinity, Decimal64.Parse("128"), Decimal64.Parse("-129") })
        {
            var result = IeeeDecimal<Decimal64>.ToInteger<sbyte>(x);
            Assert.True(result.IsSuccess); Assert.Equal((sbyte)0, result.Value); Assert.Equal(IeeeDecimalFlags.Invalid, result.Flags);
            var trap = IeeeDecimal<Decimal64>.ToInteger<sbyte>(x, new(Traps: IeeeDecimalFlags.Invalid));
            Assert.False(trap.IsSuccess); Assert.False(trap.TryGetValue(out var value)); Assert.Equal((sbyte)0, value);
        }
        Assert.Equal((byte)0, IeeeDecimal<Decimal64>.ToInteger<byte>(Decimal64.Parse("-0.4"), new(IeeeDecimalRounding.TowardZero)).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.ToInteger<byte>(Decimal64.Parse("-0.4"), new(IeeeDecimalRounding.TowardNegative)).Flags);
        Assert.Equal(BigInteger.Pow(10, 100), IeeeDecimal<Decimal128>.ToInteger<BigInteger>(Decimal128.Parse("1e100")).Value);
    }

    [Fact]
    public void ExactBinaryInputAndIndependentOracleAllModes()
    {
        BinaryInputOracle<Decimal32>(EContext.Decimal32);
        BinaryInputOracle<Decimal64>(EContext.Decimal64);
        BinaryInputOracle<Decimal128>(EContext.Decimal128);
        var exact = IeeeDecimal<Decimal128>.FromBinary(0.5);
        Assert.Equal(Decimal128.Parse("0.5"), exact.Value); Assert.Equal(IeeeDecimalFlags.None, exact.Flags);
        Assert.Equal(Decimal128.EncodeBinary(Decimal128.Parse("0.5")), Decimal128.EncodeBinary(exact.Value));
        Assert.Equal(Decimal128.EncodeBinary(Decimal128.Parse("1")), Decimal128.EncodeBinary(IeeeDecimal<Decimal128>.FromBinary(1.0).Value));
        Assert.False(Decimal128.IsNegative(IeeeDecimal<Decimal128>.FromInteger(0).Value));
        Assert.Equal(0x80000000u, BitConverter.SingleToUInt32Bits(IeeeDecimal<Decimal64>.ToBinary<float>(Decimal64.NegativeZero).Value));
        Assert.True(Decimal128.IsNegative(IeeeDecimal<Decimal128>.FromBinary(BitConverter.UInt64BitsToDouble(1UL << 63)).Value));
        FloatInputOracle<Decimal32>(EContext.Decimal32); FloatInputOracle<Decimal64>(EContext.Decimal64); FloatInputOracle<Decimal128>(EContext.Decimal128);
        Assert.True(Decimal32.IsNegativeInfinity(IeeeDecimal<Decimal32>.FromBinary(float.NegativeInfinity).Value));
        Assert.Equal(IeeeDecimalFlags.None, IeeeDecimal<Decimal64>.FromBinary(double.PositiveInfinity).Flags);
        Assert.Equal(IeeeDecimalFlags.Inexact, IeeeDecimal<Decimal32>.FromBinary(0.1f).Flags);
    }

    [Fact]
    public void DecimalToBinaryAgainstIndependentOracleAllFormatsAndModes()
    {
        BinaryOutputOracle<Decimal32>(); BinaryOutputOracle<Decimal64>(); BinaryOutputOracle<Decimal128>();
    }

    [Fact]
    public void SmallDecimalRationalsExerciseDirectedBinaryExponentBoundaries()
    {
        // Small numerator/denominator ratios challenge the floor(log2) correction in
        // cases that a large-coefficient random campaign rarely visits, notably 0.1.
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>())
        foreach (string text in new[] { "0.1", "-0.1", "0.2", "0.3", "0.7", "0.9", "1.1", "0.125" })
        {
            var oracle = EDecimal.FromString(text).ToEFloat(EContext.Binary64.WithRounding(OracleMode(mode)));
            var actual = IeeeDecimal<Decimal128>.ToBinary<double>(Decimal128.Parse(text, CultureInfo.InvariantCulture), new(mode));
            Assert.Equal(unchecked((ulong)oracle.ToDoubleBits()), BitConverter.DoubleToUInt64Bits(actual.Value));
        }
    }

    [Theory]
    [InlineData(IeeeDecimalRounding.ToNearestEven, 0x3f800000u)]
    [InlineData(IeeeDecimalRounding.ToNearestAway, 0x3f800001u)]
    [InlineData(IeeeDecimalRounding.TowardZero, 0x3f800000u)]
    [InlineData(IeeeDecimalRounding.TowardNegative, 0x3f800000u)]
    [InlineData(IeeeDecimalRounding.TowardPositive, 0x3f800001u)]
    public void ExactHalfwayIsRoundedOnce(IeeeDecimalRounding mode, uint expected)
    {
        // Exactly halfway between float 1 and its successor, represented exactly in decimal128.
        var value = Decimal128.Parse("1.000000059604644775390625");
        var result = IeeeDecimal<Decimal128>.ToBinary<float>(value, new(mode));
        Assert.Equal(expected, BitConverter.SingleToUInt32Bits(result.Value)); Assert.Equal(IeeeDecimalFlags.Inexact, result.Flags);
    }

    [Theory]
    [InlineData(IeeeDecimalRounding.ToNearestEven, 0x4000)]
    [InlineData(IeeeDecimalRounding.ToNearestAway, 0x4000)]
    [InlineData(IeeeDecimalRounding.TowardZero, 0x3fff)]
    [InlineData(IeeeDecimalRounding.TowardNegative, 0x3fff)]
    [InlineData(IeeeDecimalRounding.TowardPositive, 0x4000)]
    public void BinaryRoundingCarryCrossesBinadeExactly(IeeeDecimalRounding mode, int expected)
    {
        // Half's binade boundary at2; exact midpoint between its predecessor and2.
        var result = IeeeDecimal<Decimal128>.ToBinary<Half>(Decimal128.Parse("1.99951171875"), new(mode));
        Assert.Equal((ushort)expected, BitConverter.HalfToUInt16Bits(result.Value));
        Assert.Equal(IeeeDecimalFlags.Inexact, result.Flags);
    }

    [Fact]
    public void BinaryRangeAndSubnormalFlags()
    {
        var huge = Decimal128.Parse("1e6000");
        Assert.True(double.IsPositiveInfinity(IeeeDecimal<Decimal128>.ToBinary<double>(huge).Value));
        var limited = IeeeDecimal<Decimal128>.ToBinary<double>(huge, new(IeeeDecimalRounding.TowardZero));
        Assert.Equal(double.MaxValue, limited.Value); Assert.Equal(IeeeDecimalFlags.Overflow | IeeeDecimalFlags.Inexact, limited.Flags);
        var tiny = IeeeDecimal<Decimal128>.ToBinary<double>(Decimal128.Parse("1e-6000"), new(IeeeDecimalRounding.TowardPositive));
        Assert.Equal(double.Epsilon, tiny.Value); Assert.Equal(IeeeDecimalFlags.Underflow | IeeeDecimalFlags.Inexact, tiny.Flags);
        // 2^-24 is exactly representable in decimal and is Half's smallest subnormal.
        var subnormal = IeeeDecimal<Decimal128>.ToBinary<Half>(Decimal128.Parse("0.000000059604644775390625"));
        Assert.Equal((ushort)1, BitConverter.HalfToUInt16Bits(subnormal.Value)); Assert.Equal(IeeeDecimalFlags.None, subnormal.Flags);
        var negativeOverflow = IeeeDecimal<Decimal128>.ToBinary<float>(-huge, new(IeeeDecimalRounding.TowardPositive));
        Assert.Equal(-float.MaxValue, negativeOverflow.Value);
        Assert.Equal(IeeeDecimalFlags.Overflow | IeeeDecimalFlags.Inexact, negativeOverflow.Flags);
        var trap = IeeeDecimal<Decimal128>.ToBinary<float>(huge, new(Traps: IeeeDecimalFlags.Overflow));
        Assert.False(trap.TryGetValue(out var cleared)); Assert.Equal(0f, cleared);
        var negativeTiny = IeeeDecimal<Decimal128>.ToBinary<double>(Decimal128.Parse("-1e-6000"), new(IeeeDecimalRounding.TowardNegative));
        Assert.Equal(0x8000000000000001UL, BitConverter.DoubleToUInt64Bits(negativeTiny.Value));
        Assert.Equal(IeeeDecimalFlags.Underflow | IeeeDecimalFlags.Inexact, negativeTiny.Flags);
        Assert.Equal(0x8000000000000000UL, BitConverter.DoubleToUInt64Bits(IeeeDecimal<Decimal128>.ToBinary<double>(Decimal128.Parse("-1e-6000")).Value));
        Assert.True(Half.IsPositiveInfinity(IeeeDecimal<Decimal64>.ToBinary<Half>(Decimal64.PositiveInfinity).Value));
    }

    [Fact]
    public void BinaryAfterRoundingTininessUsesUnboundedExponentPrecision()
    {
        // Half minimum normal minus 2^-25: midpoint rounds to normal but the exact
        // 11-bit unbounded-exponent result is still tiny, so Underflow is required.
        var midpoint = Decimal128.Parse("0.0000610053539276123046875");
        var tiny = IeeeDecimal<Decimal128>.ToBinary<Half>(midpoint);
        Assert.Equal((ushort)0x0400, BitConverter.HalfToUInt16Bits(tiny.Value));
        Assert.Equal(IeeeDecimalFlags.Underflow | IeeeDecimalFlags.Inexact, tiny.Flags);
        // Minimum normal minus 2^-27 rounds up at unbounded precision: not tiny.
        var near = Decimal128.Parse("0.000061027705669403076171875");
        var notTiny = IeeeDecimal<Decimal128>.ToBinary<Half>(near);
        Assert.Equal((ushort)0x0400, BitConverter.HalfToUInt16Bits(notTiny.Value));
        Assert.Equal(IeeeDecimalFlags.Inexact, notTiny.Flags);
    }

    [Fact]
    public void ExhaustiveHalfInterchangeRoundTripsEveryEncoding()
    {
        // All 65,536 Half encodings, including signed zeros, sNaNs and every payload.
        // Every finite Half is exactly representable in decimal128.
        for (int encoding = 0; encoding <= ushort.MaxValue; encoding++)
        {
            ushort bits = (ushort)encoding;
            Half input = BitConverter.UInt16BitsToHalf(bits);
            var dec = IeeeDecimal<Decimal128>.FromBinary(input);
            var output = IeeeDecimal<Decimal128>.ToBinary<Half>(dec.Value);
            bool nan = (bits & 0x7c00) == 0x7c00 && (bits & 0x03ff) != 0;
            bool signaling = nan && (bits & 0x0200) == 0;
            Assert.Equal(nan ? (ushort)(bits | 0x0200) : bits, BitConverter.HalfToUInt16Bits(output.Value));
            Assert.Equal(signaling ? IeeeDecimalFlags.Invalid : IeeeDecimalFlags.None, dec.Flags);
            Assert.Equal(IeeeDecimalFlags.None, output.Flags);
        }
    }

    [Fact]
    public void NaNSignPayloadAndSignalingSurviveConversionPolicy()
    {
        double source = BitConverter.UInt64BitsToDouble(0xfff0000000000123);
        var decimalResult = IeeeDecimal<Decimal128>.FromBinary(source);
        Assert.Equal(IeeeDecimalFlags.Invalid, decimalResult.Flags);
        var roundtrip = IeeeDecimal<Decimal128>.ToBinary<double>(decimalResult.Value);
        Assert.Equal(0xfff8000000000123UL, BitConverter.DoubleToUInt64Bits(roundtrip.Value));
        var signaling = IeeeDecimal<Decimal128>.CreateNaN(123, signaling: true).Value;
        var binary = IeeeDecimal<Decimal128>.ToBinary<float>(signaling);
        Assert.Equal(IeeeDecimalFlags.Invalid, binary.Flags); Assert.Equal(0x7fc0007bu, BitConverter.SingleToUInt32Bits(binary.Value));
        var narrowed = IeeeDecimal<Decimal32>.FromBinary(BitConverter.UInt64BitsToDouble(0x7ff800000012d687));
        Assert.Equal(new BigInteger(234567), IeeeDecimal<Decimal32>.GetPayload(narrowed.Value).Value);
        var nanTrap = IeeeDecimal<Decimal64>.FromBinary(source, new(Traps: IeeeDecimalFlags.Invalid));
        Assert.False(nanTrap.IsSuccess); Assert.Equal(IeeeDecimalFlags.Invalid, nanTrap.Flags);
        var widePayload = IeeeDecimal<Decimal128>.CreateNaN((BigInteger.One << 80) + 0x345).Value;
        Assert.Equal(0x7ff8000000000345UL, BitConverter.DoubleToUInt64Bits(IeeeDecimal<Decimal128>.ToBinary<double>(widePayload).Value));
    }

    [Fact]
    public void UnknownCarrierAndContextsFailBeforeConversion()
    {
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<double>.FromInteger(1).Error);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<Decimal64>.FromBinary(Decimal32.One).Error);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<Decimal64>.ToBinary<Decimal32>(Decimal64.One).Error);
        var invalid = new IeeeDecimalContext((IeeeDecimalRounding)99);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<Decimal64>.FromBinary(double.NaN, invalid).Error);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<Decimal64>.ToInteger<int>(Decimal64.NaN, invalid).Error);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<Decimal64>.FromInteger(1, new(Traps: (IeeeDecimalFlags)128)).Error);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<double>.ToInteger<int>(1).Error);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<double>.ToBinary<double>(1).Error);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<double>.FromBinary(1f).Error);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<Decimal64>.ToBinary<double>(Decimal64.NaN, invalid).Error);
    }

    private static ERounding OracleMode(IeeeDecimalRounding mode) => mode switch
    {
        IeeeDecimalRounding.ToNearestEven => ERounding.HalfEven, IeeeDecimalRounding.ToNearestAway => ERounding.HalfUp,
        IeeeDecimalRounding.TowardZero => ERounding.Down, IeeeDecimalRounding.TowardNegative => ERounding.Floor, _ => ERounding.Ceiling
    };

    private static void IntegerOracle<T>(EContext format) where T : struct, IFloatingPointIeee754<T>
    {
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>())
        foreach (var n in new[] { BigInteger.Zero, BigInteger.One, -BigInteger.One, (BigInteger.One << 127) - 1, -(BigInteger.One << 127), BigInteger.Pow(10, 45) + 123456789, BigInteger.Pow(10, 6200), -BigInteger.Pow(10, 6200) })
        {
            var c = format.WithRounding(OracleMode(mode)).WithBlankFlags();
            var reference = EDecimal.FromString(n.ToString(CultureInfo.InvariantCulture)).RoundToPrecision(c);
            var actual = IeeeDecimal<T>.FromInteger(n, new(mode));
            if (reference.IsInfinity()) Assert.True(T.IsInfinity(actual.Value));
            else Assert.Equal(T.Parse(reference.ToString(), CultureInfo.InvariantCulture), actual.Value);
            Assert.Equal((c.Flags & EContext.FlagInexact) != 0, (actual.Flags & IeeeDecimalFlags.Inexact) != 0);
        }
    }

    private static void BinaryInputOracle<T>(EContext format) where T : struct, IFloatingPointIeee754<T>
    {
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>())
        foreach (double input in new[] { 0.1, -0.1, 0.5, 1.0, double.Epsilon, double.MaxValue, -double.MaxValue, Math.BitIncrement(1.0) })
        {
            var c = format.WithRounding(OracleMode(mode)).WithBlankFlags();
            var reference = EDecimal.FromEFloat(EFloat.FromDouble(input)).RoundToPrecision(c);
            var actual = IeeeDecimal<T>.FromBinary(input, new(mode));
            if (reference.IsInfinity()) Assert.True(T.IsInfinity(actual.Value));
            else Assert.Equal(T.Parse(reference.ToString(), CultureInfo.InvariantCulture), actual.Value);
            Assert.Equal((c.Flags & EContext.FlagInexact) != 0, (actual.Flags & IeeeDecimalFlags.Inexact) != 0);
            Assert.Equal((c.Flags & EContext.FlagOverflow) != 0, (actual.Flags & IeeeDecimalFlags.Overflow) != 0);
            Assert.Equal((c.Flags & EContext.FlagUnderflow) != 0, (actual.Flags & IeeeDecimalFlags.Underflow) != 0);
        }
    }

    private static void FloatInputOracle<T>(EContext format) where T : struct, IFloatingPointIeee754<T>
    {
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>())
        foreach (uint bits in new[] { 0u, 0x80000000u, 1u, 0x007fffffu, 0x00800000u, 0x3dcccccdu, 0xbdcccccdu, 0x7f7fffffu, 0xff7fffffu })
        {
            float input = BitConverter.UInt32BitsToSingle(bits);
            var c = format.WithRounding(OracleMode(mode)).WithBlankFlags();
            var reference = EDecimal.FromEFloat(EFloat.FromSingle(input)).RoundToPrecision(c);
            var actual = IeeeDecimal<T>.FromBinary(input, new(mode));
            Assert.Equal(T.Parse(reference.ToString(), CultureInfo.InvariantCulture), actual.Value);
            Assert.Equal((c.Flags & EContext.FlagInexact) != 0, (actual.Flags & IeeeDecimalFlags.Inexact) != 0);
        }
    }

    private static void BinaryOutputOracle<T>() where T : struct, IFloatingPointIeee754<T>
    {
        // Reproducible independent random inputs, seed7542019; representation is admitted once
        // before giving the exact same decimal tuple to the reference and implementation.
        var random = new Random(7542019);
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>())
        for (int i = 0; i < 100; i++)
        {
            var value = T.Parse($"{random.Next(-999999, 999999)}E{random.Next(-90, 90)}", CultureInfo.InvariantCulture);
            var oracle = EDecimal.FromString(value.ToString(null, CultureInfo.InvariantCulture));
            var c64 = EContext.Binary64.WithRounding(OracleMode(mode)).WithBlankFlags();
            var r64 = oracle.ToEFloat(c64);
            Assert.Equal(unchecked((ulong)r64.ToDoubleBits()), BitConverter.DoubleToUInt64Bits(IeeeDecimal<T>.ToBinary<double>(value, new(mode)).Value));
            var c32 = EContext.Binary32.WithRounding(OracleMode(mode)).WithBlankFlags();
            var r32 = oracle.ToEFloat(c32);
            var actual = IeeeDecimal<T>.ToBinary<float>(value, new(mode));
            Assert.Equal(unchecked((uint)r32.ToSingleBits()), BitConverter.SingleToUInt32Bits(actual.Value));
            Assert.Equal((c32.Flags & EContext.FlagInexact) != 0, (actual.Flags & IeeeDecimalFlags.Inexact) != 0);
            Assert.Equal((c32.Flags & EContext.FlagOverflow) != 0, (actual.Flags & IeeeDecimalFlags.Overflow) != 0);
            // Half reference is already rounded to Binary16; widening that binary result to
            // float is exact, so packing it back to Half introduces no extra rounding.
            var c16 = EContext.Binary16.WithRounding(OracleMode(mode)).WithBlankFlags();
            var r16 = oracle.ToEFloat(c16);
            var h = IeeeDecimal<T>.ToBinary<Half>(value, new(mode));
            Assert.Equal(BitConverter.HalfToUInt16Bits((Half)r16.ToSingle()), BitConverter.HalfToUInt16Bits(h.Value));
            Assert.Equal((c16.Flags & EContext.FlagInexact) != 0, (h.Flags & IeeeDecimalFlags.Inexact) != 0);
        }
    }
}
