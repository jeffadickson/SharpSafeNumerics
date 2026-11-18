using PeterO.Numbers;
using System.Globalization;
using System.Numerics;
using Xunit;
namespace SharpSafeNumerics.DecimalTests;

public sealed class IeeeArithmeticTests
{
    [Fact] public void DefaultResultIsInvalid() => Assert.False(default(IeeeDecimalResult<Decimal32>).IsSuccess);
    [Theory]
    [InlineData(IeeeDecimalRounding.ToNearestEven, "0.3333333")]
    [InlineData(IeeeDecimalRounding.TowardPositive, "0.3333334")]
    public void DivisionRoundsOnce(IeeeDecimalRounding rounding, string expected)
    {
        var r = IeeeDecimal<Decimal32>.Divide(Decimal32.One, Decimal32.CreateChecked(3), new(rounding));
        Assert.Equal(Decimal32.Parse(expected), r.Value); Assert.Equal(IeeeDecimalFlags.Inexact, r.Flags);
    }
    [Fact]
    public void InvalidIsAnIeeeResult()
    {
        var r = IeeeDecimal<Decimal64>.Divide(Decimal64.Zero, Decimal64.Zero);
        Assert.True(r.IsSuccess); Assert.True(Decimal64.IsNaN(r.Value)); Assert.Equal(IeeeDecimalFlags.Invalid, r.Flags);
    }
    [Fact] public void FmaDoesNotRoundProduct() { var r = IeeeDecimal<Decimal32>.FusedMultiplyAdd(Decimal32.Parse("1.234567"), Decimal32.Parse("1.000001"), Decimal32.Parse("-1.234568")); Assert.Equal(Decimal32.Parse("0.000000234567"), r.Value); }
    [Fact] public void SquareRootUsesExactTieComparison() { var r = IeeeDecimal<Decimal32>.Sqrt(Decimal32.CreateChecked(2)); Assert.Equal(Decimal32.Parse("1.414214"), r.Value); Assert.Equal(IeeeDecimalFlags.Inexact, r.Flags); }
    [Fact] public void QuantizeAndRemainder() { Assert.Equal(Decimal32.Parse("1.2"), IeeeDecimal<Decimal32>.Quantize(Decimal32.Parse("1.25"), Decimal32.Parse("0.1")).Value); Assert.Equal(Decimal32.Parse("-1"), IeeeDecimal<Decimal32>.Remainder(Decimal32.CreateChecked(5), Decimal32.CreateChecked(3)).Value); }
    [Fact]
    public void IndependentOracleAllFormatsAndModes()
    {
        CheckOracle<Decimal32>(EContext.Decimal32); CheckOracle<Decimal64>(EContext.Decimal64); CheckOracle<Decimal128>(EContext.Decimal128);
    }
    private static void CheckOracle<T>(EContext format) where T : struct, IFloatingPointIeee754<T>
    {
        // Reproducible independent oracle campaign: seed 7542019, 200 pairs per mode and format.
        var random = new Random(7542019);
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>())
        {
            var rounding = mode switch { IeeeDecimalRounding.ToNearestEven => ERounding.HalfEven, IeeeDecimalRounding.ToNearestAway => ERounding.HalfUp, IeeeDecimalRounding.TowardZero => ERounding.Down, IeeeDecimalRounding.TowardNegative => ERounding.Floor, _ => ERounding.Ceiling };
            for (int i = 0; i < 200; i++)
            {
                string sa = $"{random.Next(-999999, 999999)}E{random.Next(-30, 30)}", sb = $"{random.Next(1, 999999)}E{random.Next(-30, 30)}";
                T a = T.Parse(sa, CultureInfo.InvariantCulture), b = T.Parse(sb, CultureInfo.InvariantCulture); var x = EDecimal.FromString(sa); var y = EDecimal.FromString(sb);
                for (int operation = 0; operation < 4; operation++)
                {
                    var oracleContext = format.WithRounding(rounding).WithBlankFlags();
                    var expected = operation switch { 0 => x.Add(y, oracleContext), 1 => x.Subtract(y, oracleContext), 2 => x.Multiply(y, oracleContext), _ => x.Divide(y, oracleContext) };
                    var c = new IeeeDecimalContext(mode); var actual = operation switch { 0 => IeeeDecimal<T>.Add(a, b, c), 1 => IeeeDecimal<T>.Subtract(a, b, c), 2 => IeeeDecimal<T>.Multiply(a, b, c), _ => IeeeDecimal<T>.Divide(a, b, c) };
                    Assert.True(actual.IsSuccess); Assert.Equal(T.Parse(expected.ToString(), CultureInfo.InvariantCulture), actual.Value);
                    Assert.Equal((oracleContext.Flags & EContext.FlagInexact) != 0, (actual.Flags & IeeeDecimalFlags.Inexact) != 0);
                }
            }
        }
    }
    [Fact]
    public void TrapsAndUnsupportedOptionsClearStorage()
    {
        var trapped = IeeeDecimal<Decimal64>.Divide(Decimal64.One, Decimal64.Zero, new(Traps: IeeeDecimalFlags.DivisionByZero));
        Assert.False(trapped.TryGetValue(out var cleared)); Assert.Equal(default, cleared); Assert.Equal(IeeeDecimalFlags.DivisionByZero, trapped.Flags);
        var bad = IeeeDecimal<Decimal64>.Add(Decimal64.One, Decimal64.One, new((IeeeDecimalRounding)99)); Assert.Equal(ArithmeticError.OutOfRange, bad.Error); Assert.False(bad.TryGetValue(out cleared)); Assert.Equal(default, cleared);
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<double>.Add(1, 2).Error);
    }
    [Fact]
    public void DecimalUnderflowRequiresTinyBeforeRoundingAndInexact()
    {
        var exact = IeeeDecimal<Decimal32>.Divide(Decimal32.Parse("2E-101"), Decimal32.CreateChecked(2)); Assert.Equal(IeeeDecimalFlags.None, exact.Flags);
        var tiny = IeeeDecimal<Decimal32>.Divide(Decimal32.Parse("1E-101"), Decimal32.CreateChecked(2)); Assert.Equal(IeeeDecimalFlags.Inexact | IeeeDecimalFlags.Underflow, tiny.Flags);
        var toNormal = IeeeDecimal<Decimal32>.Divide(Decimal32.Parse("1999999E-101"), Decimal32.CreateChecked(2)); Assert.Equal(IeeeDecimalFlags.Inexact | IeeeDecimalFlags.Underflow, toNormal.Flags);
    }
    [Fact]
    public void OverflowDirectionAndPreferredQuantum()
    {
        var max = Decimal32.Parse("9.999999E96"); Assert.True(Decimal32.IsPositiveInfinity(IeeeDecimal<Decimal32>.Multiply(max, Decimal32.CreateChecked(2)).Value));
        var finite = IeeeDecimal<Decimal32>.Multiply(max, Decimal32.CreateChecked(2), new(IeeeDecimalRounding.TowardZero)); Assert.Equal(max, finite.Value); Assert.Equal(IeeeDecimalFlags.Overflow | IeeeDecimalFlags.Inexact, finite.Flags);
        var sum = IeeeDecimal<Decimal32>.Add(Decimal32.Parse("1.20"), Decimal32.Parse("2.3")); Assert.Equal(Decimal32.EncodeBinary(Decimal32.Parse("3.50")), Decimal32.EncodeBinary(sum.Value));
    }
    [Fact]
    public void SignalingNaNPayloadAndSignedZero()
    {
        var snan = Decimal32.DecodeBinary(0xFE000123); var r = IeeeDecimal<Decimal32>.Add(snan, Decimal32.One);
        Assert.Equal(IeeeDecimalFlags.Invalid, r.Flags); Assert.Equal(0xFC000123u, Decimal32.EncodeBinary(r.Value));
        var z = IeeeDecimal<Decimal32>.Subtract(Decimal32.One, Decimal32.One, new(IeeeDecimalRounding.TowardNegative)); Assert.True(Decimal32.IsNegative(z.Value));
        var minusZero = Decimal32.DecodeBinary(0xB2800000); Assert.True(Decimal32.IsNegative(IeeeDecimal<Decimal32>.Sqrt(minusZero).Value));
    }
    [Fact]
    public void SqrtFmaAndRemainderIndependentOracle()
    {
        CheckMore<Decimal32>(EContext.Decimal32); CheckMore<Decimal64>(EContext.Decimal64); CheckMore<Decimal128>(EContext.Decimal128);
    }
    private static void CheckMore<T>(EContext format) where T : struct, IFloatingPointIeee754<T>
    {
        var random = new Random(7542020);
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>())
        {
            var rounding = mode switch { IeeeDecimalRounding.ToNearestEven => ERounding.HalfEven, IeeeDecimalRounding.ToNearestAway => ERounding.HalfUp, IeeeDecimalRounding.TowardZero => ERounding.Down, IeeeDecimalRounding.TowardNegative => ERounding.Floor, _ => ERounding.Ceiling };
            for (int i = 0; i < 100; i++)
            {
                string sa = $"{random.Next(1, 999999)}E{random.Next(-20, 20)}", sb = $"{random.Next(1, 999999)}E{random.Next(-20, 20)}";
                T a = T.Parse(sa, CultureInfo.InvariantCulture), b = T.Parse(sb, CultureInfo.InvariantCulture); var x = EDecimal.FromString(sa); var y = EDecimal.FromString(sb); var c = new IeeeDecimalContext(mode);
                // IEEE remainder uses an unbounded integral quotient; a precision-limited oracle context rejects large quotients.
                var remainder = x.RemainderNear(y, EContext.Unlimited); Assert.Equal(T.Parse(remainder.ToString(), CultureInfo.InvariantCulture), IeeeDecimal<T>.Remainder(a, b, c).Value);
                var sqrt = x.Sqrt(format.WithRounding(rounding)); Assert.Equal(T.Parse(sqrt.ToString(), CultureInfo.InvariantCulture), IeeeDecimal<T>.Sqrt(a, c).Value);
                var fma = x.MultiplyAndAdd(y, x.Negate(), format.WithRounding(rounding)); Assert.Equal(T.Parse(fma.ToString(), CultureInfo.InvariantCulture), IeeeDecimal<T>.FusedMultiplyAdd(a, b, -a, c).Value);
            }
        }
    }
    [Fact]
    public void NoncanonicalNaNPayloadIsCanonicalized()
    {
        // Reserved payload bits and coefficients >= 10^(p-1) do not denote numeric payload digits.
        var r = IeeeDecimal<Decimal32>.Add(Decimal32.DecodeBinary(0x7E96574E), Decimal32.One);
        Assert.Equal(0x7C06574Eu, Decimal32.EncodeBinary(r.Value));
        var huge = IeeeDecimal<Decimal32>.Add(Decimal32.DecodeBinary(0x7C0FFFFF), Decimal32.One); Assert.Equal(0x7C000000u, Decimal32.EncodeBinary(huge.Value));
    }
    [Fact]
    public void ConversionRoundsDirectlyAndNarrowsPayloadCanonically()
    {
        var r = IeeeDecimal<Decimal32>.Convert(Decimal128.Parse("1.234567500000000000000000000001")); Assert.Equal(Decimal32.Parse("1.234568"), r.Value); Assert.Equal(IeeeDecimalFlags.Inexact, r.Flags);
        var nan = Decimal64.DecodeBinary(0x7C000000001E8480); var n = IeeeDecimal<Decimal32>.Convert(nan); Assert.Equal(0x7C000000u, Decimal32.EncodeBinary(n.Value));
        Assert.Equal(ArithmeticError.OutOfRange, IeeeDecimal<Decimal32>.Convert(1.0).Error);
    }
    [Fact]
    public void SpecialValueMatrix()
    {
        var infinity = Decimal64.PositiveInfinity; var zero = Decimal64.Zero; var one = Decimal64.One; var nan = Decimal64.NaN;
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.Subtract(infinity, infinity).Flags);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.Multiply(infinity, zero).Flags);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.Divide(infinity, infinity).Flags);
        Assert.Equal(infinity, IeeeDecimal<Decimal64>.Add(one, infinity).Value);
        Assert.Equal(zero, IeeeDecimal<Decimal64>.Divide(one, infinity).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.FusedMultiplyAdd(infinity, zero, nan).Flags);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.FusedMultiplyAdd(infinity, one, -infinity).Flags);
        Assert.Equal(infinity, IeeeDecimal<Decimal64>.FusedMultiplyAdd(one, one, infinity).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.Sqrt(-one).Flags);
        Assert.Equal(infinity, IeeeDecimal<Decimal64>.Sqrt(infinity).Value);
        Assert.Equal(one, IeeeDecimal<Decimal64>.Remainder(one, infinity).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.Remainder(infinity, one).Flags);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.Remainder(one, zero).Flags);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.Quantize(infinity, one).Flags);
        Assert.Equal(infinity, IeeeDecimal<Decimal64>.Quantize(infinity, infinity).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal64>.Quantize(Decimal64.Parse("1E100"), one).Flags);
        Assert.True(Decimal64.IsNaN(IeeeDecimal<Decimal64>.Remainder(nan, one).Value));
    }
    [Theory]
    [InlineData(IeeeDecimalRounding.ToNearestEven, "1.2", "-1.2")]
    [InlineData(IeeeDecimalRounding.ToNearestAway, "1.3", "-1.3")]
    [InlineData(IeeeDecimalRounding.TowardZero, "1.2", "-1.2")]
    [InlineData(IeeeDecimalRounding.TowardNegative, "1.2", "-1.3")]
    [InlineData(IeeeDecimalRounding.TowardPositive, "1.3", "-1.2")]
    public void HalfwayDirections(IeeeDecimalRounding mode, string positive, string negative)
    {
        var q = Decimal32.Parse("0.1"); var c = new IeeeDecimalContext(mode);
        Assert.Equal(Decimal32.Parse(positive), IeeeDecimal<Decimal32>.Quantize(Decimal32.Parse("1.25"), q, c).Value);
        Assert.Equal(Decimal32.Parse(negative), IeeeDecimal<Decimal32>.Quantize(Decimal32.Parse("-1.25"), q, c).Value);
    }
    [Fact]
    public void EveryTrapMapsToAnExplicitErrorAndValueAccessIsGuarded()
    {
        var invalid = IeeeDecimal<Decimal32>.Divide(Decimal32.Zero, Decimal32.Zero, new(Traps: IeeeDecimalFlags.Invalid)); Assert.Equal(ArithmeticError.NonFinite, invalid.Error); Assert.Throws<InvalidOperationException>(() => invalid.Value);
        var div = IeeeDecimal<Decimal32>.Divide(Decimal32.One, Decimal32.Zero, new(Traps: IeeeDecimalFlags.DivisionByZero)); Assert.Equal(ArithmeticError.DivideByZero, div.Error);
        var overflow = IeeeDecimal<Decimal32>.Multiply(Decimal32.Parse("9.999999E96"), Decimal32.CreateChecked(2), new(Traps: IeeeDecimalFlags.Overflow)); Assert.Equal(ArithmeticError.Overflow, overflow.Error);
        var underflow = IeeeDecimal<Decimal32>.Divide(Decimal32.Parse("1E-101"), Decimal32.CreateChecked(3), new(Traps: IeeeDecimalFlags.Underflow)); Assert.Equal(ArithmeticError.Underflow, underflow.Error);
        var inexact = IeeeDecimal<Decimal32>.Divide(Decimal32.One, Decimal32.CreateChecked(3), new(Traps: IeeeDecimalFlags.Inexact)); Assert.Equal(ArithmeticError.Inexact, inexact.Error);
        var valid = IeeeDecimal<Decimal32>.Add(Decimal32.One, Decimal32.One); Assert.True(valid.TryGetValue(out var actual)); Assert.Equal(Decimal32.CreateChecked(2), actual);
        var nan = IeeeDecimal<Decimal32>.Add(Decimal32.NaN, Decimal32.One); Assert.Equal(IeeeDecimalFlags.None, nan.Flags);
    }
    [Fact]
    public void FormatExponentExtremesAndCarryOut()
    {
        Boundary<Decimal32>(7, -101, 90); Boundary<Decimal64>(16, -398, 369); Boundary<Decimal128>(34, -6176, 6111);
    }
    private static void Boundary<T>(int precision, int minimum, int maximum) where T : struct, IFloatingPointIeee754<T>
    {
        T Parse(string s) => T.Parse(s, CultureInfo.InvariantCulture);
        var max = Parse(new string('9', precision) + "E" + maximum); var min = Parse("1E" + minimum); var two = T.CreateChecked(2);
        Assert.Equal(max, IeeeDecimal<T>.Multiply(max, two, new(IeeeDecimalRounding.TowardZero)).Value);
        Assert.True(T.IsNegativeInfinity(IeeeDecimal<T>.Multiply(-max, two, new(IeeeDecimalRounding.TowardNegative)).Value));
        Assert.Equal(-max, IeeeDecimal<T>.Multiply(-max, two, new(IeeeDecimalRounding.TowardPositive)).Value);
        Assert.Equal(min, IeeeDecimal<T>.Divide(min, two, new(IeeeDecimalRounding.TowardPositive)).Value);
        var carry = IeeeDecimal<T>.Add(max, Parse("5E" + (maximum - 1))); Assert.True(T.IsPositiveInfinity(carry.Value));
        Assert.Equal(min, IeeeDecimal<T>.Multiply(min, T.One).Value);
    }
    [Fact]
    public void WideNaNPayloadsAndNoncanonicalFiniteAreHandled()
    {
        ulong raw64 = 0x7E00000000000000UL | (1UL << 49); var q64 = IeeeDecimal<Decimal64>.Add(Decimal64.DecodeBinary(raw64), Decimal64.One); Assert.Equal(raw64 & ~(1UL << 57), Decimal64.EncodeBinary(q64.Value));
        UInt128 raw128 = ((UInt128)0x7E00000000000000UL << 64) | ((UInt128)1 << 109); var q128 = IeeeDecimal<Decimal128>.Add(Decimal128.DecodeBinary(raw128), Decimal128.One); Assert.Equal(raw128 & ~((UInt128)1 << 121), Decimal128.EncodeBinary(q128.Value));
        // Canonical maximum coefficient is exclusive; larger BID coefficients represent zero.
        var noncanonical = Decimal32.DecodeBinary(0x6CB89680); Assert.Equal(Decimal32.One, IeeeDecimal<Decimal32>.Add(noncanonical, Decimal32.One).Value);
    }
    [Fact]
    public void PreferredQuantumAndCarryMatrix()
    {
        void Bits(string expected, IeeeDecimalResult<Decimal32> result, IeeeDecimalFlags flags = 0) { Assert.True(result.IsSuccess); Assert.Equal(Decimal32.EncodeBinary(Decimal32.Parse(expected)), Decimal32.EncodeBinary(result.Value)); Assert.Equal(flags, result.Flags); }
        var a = Decimal32.Parse("1.20"); var b = Decimal32.Parse("2.0");
        Bits("3.20", IeeeDecimal<Decimal32>.Add(a, b)); Bits("-0.80", IeeeDecimal<Decimal32>.Subtract(a, b)); Bits("2.400", IeeeDecimal<Decimal32>.Multiply(a, b)); Bits("0.6", IeeeDecimal<Decimal32>.Divide(a, b));
        Bits("2.4000", IeeeDecimal<Decimal32>.FusedMultiplyAdd(a, b, Decimal32.Parse("0.0000")));
        Bits("10.00000", IeeeDecimal<Decimal32>.Add(Decimal32.Parse("9.999999"), Decimal32.Parse("0.0000005")), IeeeDecimalFlags.Inexact);
        Bits("1E-95", IeeeDecimal<Decimal32>.Multiply(Decimal32.Parse("1E-95"), Decimal32.One));
        var normal = IeeeDecimal<Decimal32>.Multiply(Decimal32.Parse("1000001E-101"), Decimal32.Parse("1.000001")); Assert.Equal(IeeeDecimalFlags.Inexact, normal.Flags);
        Bits("1E90", IeeeDecimal<Decimal32>.Convert(Decimal64.Parse("1E90")));
    }
    [Fact]
    public void SignedZeroRulesAcrossDirectionsAndOperations()
    {
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>())
        {
            var c = new IeeeDecimalContext(mode); bool down = mode == IeeeDecimalRounding.TowardNegative;
            foreach (bool an in new[] { false, true }) foreach (bool bn in new[] { false, true })
            {
                var a = Decimal32.Parse(an ? "-0.00" : "0.00"); var b = Decimal32.Parse(bn ? "-0.0" : "0.0");
                Assert.Equal(an && bn || (an != bn && down), Decimal32.IsNegative(IeeeDecimal<Decimal32>.Add(a, b, c).Value));
                Assert.Equal(an && !bn || (an == bn && down), Decimal32.IsNegative(IeeeDecimal<Decimal32>.Subtract(a, b, c).Value));
                Assert.Equal(an ^ bn, Decimal32.IsNegative(IeeeDecimal<Decimal32>.Multiply(a, b, c).Value));
                Assert.Equal(an ^ bn, Decimal32.IsNegative(IeeeDecimal<Decimal32>.Divide(a, Decimal32.Parse(bn ? "-2" : "2"), c).Value));
                Assert.Equal(an && bn || (an != bn && down), Decimal32.IsNegative(IeeeDecimal<Decimal32>.FusedMultiplyAdd(a, Decimal32.One, b, c).Value));
                Assert.Equal(an, Decimal32.IsNegative(IeeeDecimal<Decimal32>.Remainder(a, Decimal32.One, c).Value));
            }
            Assert.Equal(down, Decimal32.IsNegative(IeeeDecimal<Decimal32>.Subtract(Decimal32.One, Decimal32.One, c).Value));
            Assert.Equal(down, Decimal32.IsNegative(IeeeDecimal<Decimal32>.FusedMultiplyAdd(Decimal32.One, Decimal32.One, -Decimal32.One, c).Value));
        }
    }
    [Fact]
    public void NaNSelectionAcrossEveryOperandPosition()
    {
        var q = Decimal32.DecodeBinary(0xFC000011); var s = Decimal32.DecodeBinary(0x7E000022); var one = Decimal32.One;
        foreach (var op in new Func<Decimal32, Decimal32, IeeeDecimalResult<Decimal32>>[] { (a, b) => IeeeDecimal<Decimal32>.Add(a, b), (a, b) => IeeeDecimal<Decimal32>.Multiply(a, b), (a, b) => IeeeDecimal<Decimal32>.Divide(a, b), (a, b) => IeeeDecimal<Decimal32>.Remainder(a, b), (a, b) => IeeeDecimal<Decimal32>.Quantize(a, b) })
        {
            foreach (var pair in new[] { (q, one), (one, q), (q, q) }) { var r = op(pair.Item1, pair.Item2); Assert.Equal(0xFC000011u, Decimal32.EncodeBinary(r.Value)); Assert.Equal(IeeeDecimalFlags.None, r.Flags); }
            foreach (var pair in new[] { (s, one), (one, s), (q, s), (s, q) }) { var r = op(pair.Item1, pair.Item2); Assert.Equal(0x7C000022u, Decimal32.EncodeBinary(r.Value)); Assert.Equal(IeeeDecimalFlags.Invalid, r.Flags); }
        }
        foreach (int index in new[] { 0, 1, 2 })
        {
            var operands = new[] { one, one, one }; operands[index] = s; var r = IeeeDecimal<Decimal32>.FusedMultiplyAdd(operands[0], operands[1], operands[2]); Assert.Equal(0x7C000022u, Decimal32.EncodeBinary(r.Value)); Assert.Equal(IeeeDecimalFlags.Invalid, r.Flags);
            operands[index] = q; r = IeeeDecimal<Decimal32>.FusedMultiplyAdd(operands[0], operands[1], operands[2]); Assert.Equal(0xFC000011u, Decimal32.EncodeBinary(r.Value)); Assert.Equal(IeeeDecimalFlags.None, r.Flags);
        }
    }
    [Fact]
    public void ExactSquareRootsPreservePreferredQuantumAndFlags()
    {
        foreach (var mode in Enum.GetValues<IeeeDecimalRounding>()) foreach (var pair in new[] { ("4", "2"), ("4.00", "2.0"), ("0.0400", "0.20"), ("0E-3", "0E-2"), ("-0E-3", "-0E-2"), ("4E90", "2E45") })
        {
            var r = IeeeDecimal<Decimal32>.Sqrt(Decimal32.Parse(pair.Item1), new(mode)); Assert.Equal(Decimal32.EncodeBinary(Decimal32.Parse(pair.Item2)), Decimal32.EncodeBinary(r.Value)); Assert.Equal(IeeeDecimalFlags.None, r.Flags);
        }
    }
    [Fact]
    public void QuantizeFlagsPrecisionAndRemainderHalfway()
    {
        foreach (var pair in new[] { ("1.20", "0.1", "1.2", false), ("1.25", "0.1", "1.2", true), ("1234567", "1", "1234567", false), ("1E-101", "1E-100", "0E-100", true) })
        {
            var r = IeeeDecimal<Decimal32>.Quantize(Decimal32.Parse(pair.Item1), Decimal32.Parse(pair.Item2)); Assert.Equal(Decimal32.EncodeBinary(Decimal32.Parse(pair.Item3)), Decimal32.EncodeBinary(r.Value)); Assert.Equal(pair.Item4 ? IeeeDecimalFlags.Inexact : 0, r.Flags);
        }
        foreach (var pair in new[] { ("5", "2", "1"), ("7", "2", "-1"), ("-5", "2", "-1"), ("4", "2", "0"), ("-4", "2", "-0") })
        { var r = IeeeDecimal<Decimal32>.Remainder(Decimal32.Parse(pair.Item1), Decimal32.Parse(pair.Item2)); Assert.Equal(Decimal32.EncodeBinary(Decimal32.Parse(pair.Item3)), Decimal32.EncodeBinary(r.Value)); Assert.Equal(IeeeDecimalFlags.None, r.Flags); }
    }
    [Fact]
    public void FusedSignsAndInfiniteOperands()
    {
        foreach (var pair in new[] { ("-2", "3", "7", "1"), ("2", "-3", "5", "-1"), ("-2", "-3", "-5", "1"), ("2", "3", "-7", "-1") })
        { var r = IeeeDecimal<Decimal32>.FusedMultiplyAdd(Decimal32.Parse(pair.Item1), Decimal32.Parse(pair.Item2), Decimal32.Parse(pair.Item3)); Assert.Equal(Decimal32.Parse(pair.Item4), r.Value); Assert.Equal(IeeeDecimalFlags.None, r.Flags); }
        var inf = Decimal32.PositiveInfinity; var one = Decimal32.One;
        Assert.Equal(inf, IeeeDecimal<Decimal32>.Add(inf, one).Value); Assert.Equal(inf, IeeeDecimal<Decimal32>.Add(inf, inf).Value);
        Assert.Equal(-inf, IeeeDecimal<Decimal32>.Multiply(-inf, one).Value); Assert.Equal(-inf, IeeeDecimal<Decimal32>.Multiply(one, -inf).Value);
        Assert.Equal(inf, IeeeDecimal<Decimal32>.Divide(inf, one).Value); Assert.Equal(inf, IeeeDecimal<Decimal32>.Convert(Decimal64.PositiveInfinity).Value);
        Assert.Equal(inf, IeeeDecimal<Decimal32>.FusedMultiplyAdd(inf, one, one).Value); Assert.Equal(inf, IeeeDecimal<Decimal32>.FusedMultiplyAdd(one, inf, inf).Value);
        Assert.Equal(IeeeDecimalFlags.Invalid, IeeeDecimal<Decimal32>.FusedMultiplyAdd(Decimal32.Zero, inf, one).Flags);
    }
    [Fact]
    public void DirectedSquareRootNormalizesCarry()
    {
        var r = IeeeDecimal<Decimal32>.Sqrt(Decimal32.Parse("99.99999"), new(IeeeDecimalRounding.TowardPositive)); Assert.Equal(Decimal32.EncodeBinary(Decimal32.Parse("10.00000")), Decimal32.EncodeBinary(r.Value)); Assert.Equal(IeeeDecimalFlags.Inexact, r.Flags);
    }
    [Fact]
    public void EveryEntryPointRejectsMalformedContextWithoutStatusOrStorage()
    {
        var invalid = new IeeeDecimalContext(Traps: (IeeeDecimalFlags)32); var one = Decimal32.One;
        foreach (var r in new[] { IeeeDecimal<Decimal32>.Add(one, one, invalid), IeeeDecimal<Decimal32>.FusedMultiplyAdd(one, one, one, invalid), IeeeDecimal<Decimal32>.Sqrt(one, invalid), IeeeDecimal<Decimal32>.Remainder(one, one, invalid), IeeeDecimal<Decimal32>.Quantize(one, one, invalid), IeeeDecimal<Decimal32>.Convert(Decimal64.One, invalid) })
        { Assert.Equal(ArithmeticError.OutOfRange, r.Error); Assert.Equal(IeeeDecimalFlags.None, r.Flags); Assert.False(r.TryGetValue(out var value)); Assert.Equal(default, value); }
    }
    [Fact]
    public void RawCoefficientAndPayloadEqualityBoundaries()
    {
        var boundary = Decimal32.Parse("8388608"); Assert.Equal(Decimal32.EncodeBinary(boundary), Decimal32.EncodeBinary(IeeeDecimal<Decimal32>.Add(boundary, Decimal32.Zero).Value));
        Assert.Equal(0x7C000000u, Decimal32.EncodeBinary(IeeeDecimal<Decimal32>.Add(Decimal32.DecodeBinary(0x7C0F4240), Decimal32.One).Value));
        var source = Decimal64.DecodeBinary(0x7C000000000F4240); Assert.Equal(0x7C000000u, Decimal32.EncodeBinary(IeeeDecimal<Decimal32>.Convert(source).Value));
        var payload64 = Decimal64.DecodeBinary(0x7C00000000000000UL | 1_000_000_000_000_000UL); Assert.Equal(0x7C00000000000000UL, Decimal64.EncodeBinary(IeeeDecimal<Decimal64>.Add(payload64, Decimal64.One).Value));
        UInt128 ten33 = UInt128.Parse("1000000000000000000000000000000000", CultureInfo.InvariantCulture); UInt128 nan128 = (UInt128)0x7C00000000000000UL << 64; Assert.Equal(nan128, Decimal128.EncodeBinary(IeeeDecimal<Decimal128>.Add(Decimal128.DecodeBinary(nan128 | ten33), Decimal128.One).Value));
    }
    [Fact] public void FusedZeroProductsRemainValidAndQuietNaNsDoNotInventInvalid()
    {
        var one=Decimal32.One;var zero=Decimal32.Zero;var inf=Decimal32.PositiveInfinity;var q=Decimal32.DecodeBinary(0x7C000033);
        foreach(var tuple in new[]{(one,zero,one),(zero,one,one),(zero,zero,one)})
        {var r=IeeeDecimal<Decimal32>.FusedMultiplyAdd(tuple.Item1,tuple.Item2,tuple.Item3);Assert.Equal(one,r.Value);Assert.Equal(IeeeDecimalFlags.None,r.Flags);}
        foreach(var tuple in new[]{(inf,q,one),(q,inf,one)})
        {var r=IeeeDecimal<Decimal32>.FusedMultiplyAdd(tuple.Item1,tuple.Item2,tuple.Item3);Assert.Equal(0x7C000033u,Decimal32.EncodeBinary(r.Value));Assert.Equal(IeeeDecimalFlags.None,r.Flags);}
    }}

















