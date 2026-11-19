// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Xunit;

namespace SharpSafeNumerics.GeneratorTests;

public sealed class GeneratedDomainTests
{
    [Fact]
    public void Invalid_default_is_distinct_from_admitted_zero()
    {
        Slot invalid = default;
        var zero = (Slot)0;
        var one = (Slot)1;

        Assert.False(invalid.IsValid);
        Assert.False(invalid.TryGetValue(out var raw));
        Assert.Equal(0, raw);
        Assert.Throws<InvalidOperationException>(() => invalid.Value);
        Assert.Throws<InvalidOperationException>(() => (int)invalid);
        Assert.Equal("<invalid>", invalid.ToString());
        Assert.Equal(default, invalid);
        Assert.Equal(0, invalid.GetHashCode());
        Assert.NotEqual(invalid, zero);
        Assert.True(invalid != zero);
        Assert.True(invalid < zero);
        Assert.True(zero > invalid);
        Assert.False(zero < invalid);
        Assert.True(zero <= one);
        Assert.True(one >= zero);
        Assert.True(zero == (Slot)0);
        Assert.Equal(zero.GetHashCode(), ((Slot)0).GetHashCode());
        Assert.True(zero.Equals((object)(Slot)0));
        Assert.False(zero.Equals((object)0));
        Assert.False(zero.Equals(null));
        Assert.Equal(0, Compare(zero, (Slot)0));
        Assert.True(Compare(zero, one) < 0);

        Assert.Equal(ArithmeticError.Uninitialized, invalid.TryAdd(3, out var added));
        Assert.Equal(default, added);
        Assert.Equal(ArithmeticError.Uninitialized, invalid.TrySubtract(3, out var subtracted));
        Assert.Equal(default, subtracted);
        Assert.Equal(ArithmeticError.Uninitialized, invalid.Add(3).Error);
        Assert.Equal(ArithmeticError.Uninitialized, invalid.Subtract(3).Error);
    }

    [Fact]
    public void Zero_valid_default_admits_zero_and_failures_clear_to_default()
    {
        ZeroCounter zero = default;
        Assert.Equal(DefaultValuePolicy.ZeroValid, ZeroCounter.DefaultPolicy);
        Assert.True(zero.IsValid);
        Assert.True(zero.TryGetValue(out var raw));
        Assert.Equal(0, raw);
        Assert.Equal(0, zero.Value);
        Assert.Equal(default, (ZeroCounter)0);

        Assert.Equal(ArithmeticError.OutOfRange, ZeroCounter.TryCreate(-1, out var rejected));
        Assert.Equal(default, rejected);
        Assert.Equal(ArithmeticError.OutOfRange, zero.TrySubtract(1, out var subtracted));
        Assert.Equal(default, subtracted);
        Assert.False(ZeroCounter.TryParse("-1", out var parsed));
        Assert.Equal(default, parsed);
        Assert.False(ZeroCounter.TryRead([255], out var decoded));
        Assert.Equal(default, decoded);
    }

    [Fact]
    public void Reject_reports_primitive_and_domain_failures()
    {
        var nine = (Slot)9;
        Assert.Equal(ArithmeticPolicy.Reject, Slot.OverflowPolicy);
        Assert.Equal(ArithmeticError.None, nine.TryAdd(1, out var ten));
        Assert.Equal(10, ten.Value);
        Assert.Equal(8, nine.Subtract(1).Value.Value);
        Assert.Equal(ArithmeticError.OutOfRange, nine.Add(2).Error);
        Assert.Equal(ArithmeticError.OutOfRange, nine.Subtract(10).Error);
        Assert.Equal(ArithmeticError.Overflow, nine.Add(int.MaxValue).Error);
        Assert.Equal(ArithmeticError.OutOfRange, nine.TryAdd(2, out var rejected));
        Assert.Equal(default, rejected);
        Assert.Throws<OverflowException>(() => (Slot)11);
    }

    [Fact]
    public void Saturate_clamps_primitive_and_domain_failures_to_domain_endpoints()
    {
        var one = (Clamped)1;
        Assert.Equal(ArithmeticPolicy.Saturate, Clamped.OverflowPolicy);
        Assert.Equal(2, one.Add(1).Value.Value);
        Assert.Equal(0, one.Subtract(1).Value.Value);
        Assert.Equal(9, one.Add(10).Value.Value);
        Assert.Equal(-5, one.Subtract(10).Value.Value);
        Assert.Equal(9, one.Add(sbyte.MaxValue).Value.Value);
        Assert.Equal(-5, ((Clamped)(-5)).Add(sbyte.MinValue).Value.Value);
        Assert.Equal(-5, ((Clamped)(-5)).Subtract(sbyte.MaxValue).Value.Value);
        Assert.Equal(9, ((Clamped)9).Subtract(sbyte.MinValue).Value.Value);

        Assert.Equal(ArithmeticError.Overflow, one.TryAdd(sbyte.MaxValue, out var overflow));
        Assert.Equal(default, overflow);
        Assert.Equal(ArithmeticError.OutOfRange, one.TrySubtract(7, out var outside));
        Assert.Equal(default, outside);

        var unsigned = (ClampedUnsigned)4;
        Assert.Equal((byte)2, unsigned.Subtract(10).Value.Value);
        Assert.Equal((byte)9, unsigned.Add(byte.MaxValue).Value.Value);
    }

    [Fact]
    public void Wrap_uses_primitive_modulo_then_domain_admission()
    {
        var high = (Wrapped)100;
        var minimum = (Wrapped)sbyte.MinValue;
        Assert.Equal(ArithmeticPolicy.Wrap, Wrapped.OverflowPolicy);
        Assert.Equal(-106, high.Add(50).Value.Value);
        Assert.Equal(118, minimum.Subtract(10).Value.Value);
        Assert.Equal(ArithmeticError.OutOfRange, minimum.Subtract(1).Error);
        Assert.Equal(ArithmeticError.OutOfRange, high.Add(21).Error);
        Assert.Equal(ArithmeticError.Overflow, high.TryAdd(50, out var overflow));
        Assert.Equal(default, overflow);
        Assert.Equal(ArithmeticError.Underflow, minimum.TrySubtract(10, out var underflow));
        Assert.Equal(default, underflow);
    }

    [Fact]
    public void Throw_raises_for_primitive_and_domain_failures_after_validity_check()
    {
        var ten = (Throwing)10;
        Assert.Equal(ArithmeticPolicy.Throw, Throwing.OverflowPolicy);
        Assert.Equal(9, ten.Add(-1).Value.Value);
        Assert.Equal(9, ten.Subtract(1).Value.Value);
        Assert.Throws<OverflowException>(() => ten.Add(sbyte.MaxValue));
        Assert.Throws<OverflowException>(() => ten.Subtract(sbyte.MinValue));
        Assert.Throws<OverflowException>(() => ten.Add(1));
        Assert.Throws<OverflowException>(() => ten.Subtract(11));
        Assert.Equal(ArithmeticError.Uninitialized, default(Throwing).Add(1).Error);
        Assert.Equal(ArithmeticError.OutOfRange, ten.TryAdd(1, out var result));
        Assert.Equal(default, result);
    }

    [Fact]
    public void Exact_conversions_check_primitive_fit_sign_and_domain()
    {
        Assert.Equal(ArithmeticError.None, Slot.TryCreateFrom((UInt128)9, out var nine));
        Assert.Equal(9, (int)nine);
        Assert.Equal(ArithmeticError.None, nine.TryConvertTo(out byte narrow));
        Assert.Equal((byte)9, narrow);
        Assert.Equal(ArithmeticError.OutOfRange, Slot.TryCreateFrom(UInt128.MaxValue, out var overflow));
        Assert.Equal(default, overflow);
        Assert.Equal(ArithmeticError.OutOfRange, Slot.TryCreateFrom((ushort)11, out var outside));
        Assert.Equal(default, outside);
        Assert.Equal(ArithmeticError.Uninitialized, default(Slot).TryConvertTo(out UInt128 uninitialized));
        Assert.Equal(UInt128.Zero, uninitialized);

        var negative = (Clamped)(-1);
        Assert.Equal(ArithmeticError.OutOfRange, negative.TryConvertTo(out uint unsigned));
        Assert.Equal(0U, unsigned);
        Assert.Equal(ArithmeticError.OutOfRange, ((FullU16)256).TryConvertTo(out byte small));
        Assert.Equal((byte)0, small);
        Assert.Equal(ArithmeticError.None, ((FullS128)Int128.MinValue).TryConvertTo(out Int128 signed));
        Assert.Equal(Int128.MinValue, signed);
    }

    [Fact]
    public void Text_policy_is_invariant_and_does_not_include_unit_metadata()
    {
        var oldCulture = CultureInfo.CurrentCulture;
        var alternate = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        alternate.NumberFormat.NegativeSign = "~";
        CultureInfo.CurrentCulture = alternate;
        try
        {
            Assert.Equal("metre\" / second µ", Clamped.Unit);
            Assert.Null(Slot.Unit);
            Assert.Equal(IntegerTextPolicy.InvariantDecimal, Clamped.TextPolicy);
            Assert.True(Clamped.TryParse("  -5  ", out var signed));
            Assert.Equal(-5, signed.Value);
            Assert.Equal("-5", signed.ToString());
            Assert.False(Clamped.TryParse("~5", out _));
            Assert.False(Clamped.TryParse("-5 m/s", out _));
            Assert.True(Slot.TryParse("+0009", out var decimalValue));
            Assert.Equal("9", decimalValue.ToString());

            Assert.Equal(IntegerTextPolicy.InvariantHexadecimal, HexSigned.TextPolicy);
            Assert.True(HexSigned.TryParse("ff", out var minusOne));
            Assert.Equal(-1, minusOne.Value);
            Assert.Equal("FF", minusOne.ToString());
            Assert.True(HexSigned.TryParse("7f", out var positive));
            Assert.Equal(sbyte.MaxValue, positive.Value);
            Assert.False(HexSigned.TryParse("0x7F", out _));
            Assert.False(HexSigned.TryParse(" FF ", out _));
            Assert.False(HexSigned.TryParse("-1", out _));

            Span<char> output = stackalloc char[2];
            Assert.True(minusOne.TryFormat(output, out var count));
            Assert.Equal(2, count);
            Assert.True(output.SequenceEqual("FF"));
            Assert.False(minusOne.TryFormat(output[..1], out var shortCount));
            Assert.Equal(0, shortCount);
            Assert.False(default(HexSigned).TryFormat(output, out var invalidCount));
            Assert.Equal(0, invalidCount);

            Assert.True(HexNative.TryParse("FFFFFFFF", out var nativeNegative));
            Assert.Equal((nint)(-1), nativeNegative.Value);
            Assert.Equal("FFFFFFFF", nativeNegative.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("1\0")]
    [InlineData("1\0x")]
    [InlineData("1_0")]
    [InlineData("1,0")]
    [InlineData("1.0")]
    [InlineData("1 0")]
    [InlineData("1e0")]
    [InlineData("١")]
    [InlineData("11")]
    [InlineData("2147483648")]
    public void Invalid_decimal_text_clears_output(string text)
    {
        Assert.False(Slot.TryParse(text, out var rejected));
        Assert.Equal(default, rejected);
    }

    [Fact]
    public void Exhaustive_signed_hex_byte_round_trips_follow_twos_complement()
    {
        Span<char> formatted = stackalloc char[2];
        for (int bits = 0; bits <= byte.MaxValue; bits++)
        {
            // Use an unsigned format as the reference so the parser is tested at the sign bit.
            string input = bits.ToString("X2", CultureInfo.InvariantCulture);
            int expected = bits < 128 ? bits : bits - 256;
            Assert.True(HexSigned.TryParse(input, out var value), input);
            Assert.Equal(expected, value.Value);
            Assert.True(value.TryFormat(formatted, out var count), input);
            Assert.True(formatted[..count].SequenceEqual(bits.ToString("X", CultureInfo.InvariantCulture)), input);
        }
    }

    [Fact]
    public void Decimal_input_fuzz_rejects_non_digit_suffixes_without_partial_admission()
    {
        const int seed = 418721;
        var random = new Random(seed);
        for (int index = 0; index < 1000; index++)
        {
            // Printable punctuation, non-ASCII UTF-16 code units and NUL may follow valid prefixes.
            char suffix = (index % 3) switch
            {
                0 => (char)random.Next(0, 9),
                1 => (char)random.Next(0x21, 0x30),
                _ => (char)random.Next(0xC0, char.MaxValue + 1)
            };
            string text = random.Next(0, 11).ToString(CultureInfo.InvariantCulture) + suffix;
            Assert.False(Slot.TryParse(text, out var rejected), $"seed {seed} case {index} suffix U+{(int)suffix:X4}");
            Assert.Equal(default, rejected);
        }
    }

    [Fact]
    public void Binary_codec_uses_declared_width_and_endianness()
    {
        Assert.Equal(2, NarrowBigEndian.SerializationWidth);
        Assert.Equal(IntegerEndianness.BigEndian, NarrowBigEndian.SerializationEndianness);
        Assert.Equal(IntegerEndianness.LittleEndian, NarrowLittleEndian.SerializationEndianness);
        Span<byte> destination = stackalloc byte[4];
        destination.Fill(0x5A);

        var bigNegative = (NarrowBigEndian)(-257);
        Assert.True(bigNegative.TryWrite(destination, out var count));
        Assert.Equal(2, count);
        Assert.True(destination.SequenceEqual<byte>([0xFE, 0xFF, 0x5A, 0x5A]));
        Assert.True(NarrowBigEndian.TryRead(destination[..count], out var bigRoundTrip));
        Assert.Equal(bigNegative, bigRoundTrip);

        var littleNegative = (NarrowLittleEndian)(-257);
        Assert.True(littleNegative.TryWrite(destination, out count));
        Assert.Equal(2, count);
        Assert.True(destination.SequenceEqual<byte>([0xFF, 0xFE, 0x5A, 0x5A]));
        Assert.True(NarrowLittleEndian.TryRead(destination[..count], out var littleRoundTrip));
        Assert.Equal(littleNegative, littleRoundTrip);

        Assert.False(NarrowBigEndian.TryRead(destination, out var withSuffix));
        Assert.Equal(default, withSuffix);
        Assert.False(NarrowBigEndian.TryRead(destination[..1], out var truncated));
        Assert.Equal(default, truncated);
        Assert.False(NarrowBigEndian.TryRead([0x03, 0x15], out var outOfRange));
        Assert.Equal(default, outOfRange);
        Assert.False(bigNegative.TryWrite(destination[..1], out count));
        Assert.Equal(0, count);
        Assert.False(default(NarrowBigEndian).TryWrite(destination, out count));
        Assert.Equal(0, count);
        Assert.True(destination.SequenceEqual<byte>([0xFF, 0xFE, 0x5A, 0x5A]));

        Assert.True(((NarrowS8)(-128)).TryWrite(destination, out count));
        Assert.Equal(1, count);
        Assert.Equal(0x80, destination[0]);
        Assert.True(NarrowS8.TryRead(destination[..count], out var signedByte));
        Assert.Equal(-128, signedByte.Value);
        Assert.True(((NarrowU8)255).TryWrite(destination, out count));
        Assert.Equal(1, count);
        Assert.Equal(0xFF, destination[0]);
        Assert.True(NarrowU8.TryRead(destination[..count], out var unsignedByte));
        Assert.Equal(255UL, unsignedByte.Value);
    }

    [Fact]
    public void Parsing_consumes_text_before_publishing_into_overlapping_storage()
    {
        Span<Slot> slots = stackalloc Slot[1];
        Span<char> text = MemoryMarshal.Cast<Slot, char>(slots);
        "09".CopyTo(text);

        Assert.True(Slot.TryParse(text[..2], out slots[0]));
        Assert.Equal(9, slots[0].Value);

        text[0] = 'x';
        Assert.False(Slot.TryParse(text[..1], out slots[0]));
        Assert.Equal(default, slots[0]);
    }

    [Fact]
    public void Decoding_consumes_bytes_before_publishing_into_overlapping_storage()
    {
        Span<NarrowBigEndian> slots = stackalloc NarrowBigEndian[1];
        Span<byte> bytes = MemoryMarshal.AsBytes(slots);
        bytes[0] = 0x00;
        bytes[1] = 0x29;

        Assert.True(NarrowBigEndian.TryRead(bytes[..2], out slots[0]));
        Assert.Equal(41, slots[0].Value);

        bytes[0] = 0x7F;
        bytes[1] = 0xFF;
        Assert.False(NarrowBigEndian.TryRead(bytes[..2], out slots[0]));
        Assert.Equal(default, slots[0]);
    }

    [Fact]
    public void Extraction_reads_validity_before_publishing_into_overlapping_receiver()
    {
        Span<Slot> slots = stackalloc Slot[1];
        slots[0] = (Slot)0;
        // The generated int payload precedes the validity byte in the sequential layout.
        Span<int> storage = MemoryMarshal.Cast<Slot, int>(slots);
        Assert.True(slots[0].TryGetValue(out storage[1]));
        Assert.Equal(0, storage[1]);
    }

    [Fact]
    public void Conversion_reads_value_before_publishing_into_overlapping_receiver()
    {
        Span<Slot> slots = stackalloc Slot[1];
        slots[0] = (Slot)9;
        Span<short> storage = MemoryMarshal.Cast<Slot, short>(slots);
        Assert.Equal(ArithmeticError.None, slots[0].TryConvertTo(out storage[0]));
        Assert.Equal(9, storage[0]);
    }

    [Fact]
    public void Encoding_reads_value_before_publishing_count_into_overlapping_receiver()
    {
        Span<Slot> slots = stackalloc Slot[1];
        slots[0] = (Slot)9;
        Span<int> storage = MemoryMarshal.Cast<Slot, int>(slots);
        Span<byte> destination = stackalloc byte[Slot.SerializationWidth];
        Assert.True(slots[0].TryWrite(destination, out storage[0]));
        Assert.Equal(Slot.SerializationWidth, storage[0]);
        Assert.True(destination.SequenceEqual<byte>([9, 0, 0, 0]));
    }

    [Fact]
    public void Formatting_reads_value_before_publishing_count_into_overlapping_receiver()
    {
        Span<Slot> slots = stackalloc Slot[1];
        slots[0] = (Slot)9;
        Span<int> storage = MemoryMarshal.Cast<Slot, int>(slots);
        Span<char> destination = stackalloc char[1];
        Assert.True(slots[0].TryFormat(destination, out storage[0]));
        Assert.Equal(1, storage[0]);
        Assert.True(destination.SequenceEqual("9"));
    }

    [Fact]
    public void Nonterminal_128_bit_bounds_preserve_both_halves()
    {
        Assert.Equal(-(Int128.One << 64) - 1, MiddleS128.Minimum);
        Assert.Equal((Int128.One << 64) + 18, MiddleS128.Maximum);
        Assert.Equal((UInt128.One << 96) + 3, MiddleU128.Minimum);
        Assert.Equal((UInt128.One << 127) + 7, MiddleU128.Maximum);
        Assert.Equal(ArithmeticError.None, MiddleS128.TryCreate(MiddleS128.Minimum, out var signed));
        Assert.Equal(MiddleS128.Minimum, signed.Value);
        Assert.Equal(ArithmeticError.OutOfRange, MiddleS128.TryCreate(MiddleS128.Minimum - 1, out _));
        Assert.Equal(ArithmeticError.None, MiddleU128.TryCreate(MiddleU128.Maximum, out var unsigned));
        Assert.Equal(MiddleU128.Maximum, unsigned.Value);
        Assert.Equal(ArithmeticError.OutOfRange, MiddleU128.TryCreate(MiddleU128.Maximum + 1, out _));
    }

    [Fact]
    public void Full_width_codecs_and_invariant_text_match_independent_big_integer_oracle()
    {
        CheckRoundTrips<FullS8, sbyte>(FullS8.SerializationWidth);
        CheckRoundTrips<FullU8, byte>(FullU8.SerializationWidth);
        CheckRoundTrips<FullS16, short>(FullS16.SerializationWidth);
        CheckRoundTrips<FullU16, ushort>(FullU16.SerializationWidth);
        CheckRoundTrips<FullS32, int>(FullS32.SerializationWidth);
        CheckRoundTrips<FullU32, uint>(FullU32.SerializationWidth);
        CheckRoundTrips<FullS64, long>(FullS64.SerializationWidth);
        CheckRoundTrips<FullU64, ulong>(FullU64.SerializationWidth);
        CheckRoundTrips<FullS128, Int128>(FullS128.SerializationWidth);
        CheckRoundTrips<FullU128, UInt128>(FullU128.SerializationWidth, bigEndian: true);
        Assert.Equal(4, FullNative.SerializationWidth);
        Assert.Equal(4, FullUNative.SerializationWidth);
        CheckRoundTrips<FullNative, nint>(FullNative.SerializationWidth);
        CheckRoundTrips<FullUNative, nuint>(FullUNative.SerializationWidth);
    }

    private static int Compare<T>(T left, T right) where T : IComparable<T> => left.CompareTo(right);

    private static void CheckRoundTrips<TDomain, TStorage>(int width, bool bigEndian = false)
        where TDomain : struct, IDomainFixture<TDomain, TStorage>
        where TStorage : struct, IBinaryInteger<TStorage>, IMinMaxValue<TStorage>
    {
        bool signed = TStorage.IsNegative(TStorage.MinValue);
        var expectedMax = (BigInteger.One << (width * 8 - (signed ? 1 : 0))) - 1;
        var expectedMin = signed ? -expectedMax - 1 : BigInteger.Zero;
        Assert.Equal(expectedMin, BigInteger.CreateChecked(TDomain.Minimum));
        Assert.Equal(expectedMax, BigInteger.CreateChecked(TDomain.Maximum));

        Span<byte> input = stackalloc byte[width];
        Span<byte> output = stackalloc byte[width];
        Span<char> chars = stackalloc char[40];
        const int seed = 81334108;
        var random = new Random(seed + width);

        for (int index = 0; index < 1004; index++)
        {
            switch (index)
            {
                case 0: input.Clear(); break;
                case 1: input.Fill(0xFF); break;
                // Explicit sign transition byte patterns give an endpoint regardless of the PRNG.
                case 2: input.Clear(); input[bigEndian ? 0 : width - 1] = 0x80; break;
                case 3: input.Fill(0xFF); input[bigEndian ? 0 : width - 1] = 0x7F; break;
                default: random.NextBytes(input); break;
            }

            var expected = new BigInteger(input, isUnsigned: !signed, isBigEndian: bigEndian);
            string context = $"{typeof(TDomain).Name} seed {seed + width} case {index} bytes {Convert.ToHexString(input)}";
            Assert.True(TDomain.TryRead(input, out var decoded), context);
            Assert.Equal(expected, BigInteger.CreateChecked(decoded.Value));
            Assert.True(decoded.TryWrite(output, out int byteCount), context);
            Assert.Equal(width, byteCount);
            Assert.True(input.SequenceEqual(output), context);

            string text = expected.ToString(CultureInfo.InvariantCulture);
            Assert.True(TDomain.TryParse(text, out var parsed), context);
            Assert.Equal(expected, BigInteger.CreateChecked(parsed.Value));
            Assert.True(parsed.TryFormat(chars, out int charCount), context);
            Assert.True(chars[..charCount].SequenceEqual(text), context);
        }
    }
}

// Public and internal declarations exercise the standard build-time generator path.
[BoundedInteger(typeof(int), "0", "10")]
public readonly partial struct Slot : IDomainFixture<Slot, int>;

[BoundedInteger(typeof(short), "0", "100", DefaultPolicy = DefaultValuePolicy.ZeroValid, SerializationWidth = 1)]
internal partial struct ZeroCounter : IDomainFixture<ZeroCounter, short>;

[BoundedInteger(typeof(sbyte), "-5", "9", OverflowPolicy = ArithmeticPolicy.Saturate, Unit = "metre\" / second µ")]
internal readonly partial struct Clamped : IDomainFixture<Clamped, sbyte>;

[BoundedInteger(typeof(byte), "2", "9", OverflowPolicy = ArithmeticPolicy.Saturate)]
internal readonly partial struct ClampedUnsigned : IDomainFixture<ClampedUnsigned, byte>;

[BoundedInteger(typeof(sbyte), "-128", "120", OverflowPolicy = ArithmeticPolicy.Wrap)]
internal readonly partial struct Wrapped : IDomainFixture<Wrapped, sbyte>;

[BoundedInteger(typeof(sbyte), "1", "10", OverflowPolicy = ArithmeticPolicy.Throw)]
internal readonly partial struct Throwing : IDomainFixture<Throwing, sbyte>;

[BoundedInteger(typeof(sbyte), "-128", "127", TextPolicy = IntegerTextPolicy.InvariantHexadecimal)]
internal readonly partial struct HexSigned : IDomainFixture<HexSigned, sbyte>;

[BoundedInteger(typeof(nint), "-2147483648", "2147483647", TextPolicy = IntegerTextPolicy.InvariantHexadecimal)]
internal readonly partial struct HexNative : IDomainFixture<HexNative, nint>;

[BoundedInteger(typeof(int), "-789", "788", SerializationWidth = 2, Endianness = IntegerEndianness.BigEndian)]
internal readonly partial struct NarrowBigEndian : IDomainFixture<NarrowBigEndian, int>;

[BoundedInteger(typeof(long), "-789", "788", SerializationWidth = 2)]
internal readonly partial struct NarrowLittleEndian : IDomainFixture<NarrowLittleEndian, long>;

[BoundedInteger(typeof(short), "-128", "127", SerializationWidth = 1)]
internal readonly partial struct NarrowS8 : IDomainFixture<NarrowS8, short>;

[BoundedInteger(typeof(ulong), "0", "255", SerializationWidth = 1)]
internal readonly partial struct NarrowU8 : IDomainFixture<NarrowU8, ulong>;

[BoundedInteger(typeof(Int128), "-18446744073709551617", "18446744073709551634")]
internal readonly partial struct MiddleS128 : IDomainFixture<MiddleS128, Int128>;

[BoundedInteger(typeof(UInt128), "79228162514264337593543950339", "170141183460469231731687303715884105735")]
internal readonly partial struct MiddleU128 : IDomainFixture<MiddleU128, UInt128>;

[BoundedInteger(typeof(sbyte), "-128", "127")]
internal readonly partial struct FullS8 : IDomainFixture<FullS8, sbyte>;

[BoundedInteger(typeof(byte), "0", "255")]
internal readonly partial struct FullU8 : IDomainFixture<FullU8, byte>;

[BoundedInteger(typeof(short), "-32768", "32767")]
internal readonly partial struct FullS16 : IDomainFixture<FullS16, short>;

[BoundedInteger(typeof(ushort), "0", "65535")]
internal readonly partial struct FullU16 : IDomainFixture<FullU16, ushort>;

[BoundedInteger(typeof(int), "-2147483648", "2147483647")]
internal readonly partial struct FullS32 : IDomainFixture<FullS32, int>;

[BoundedInteger(typeof(uint), "0", "4294967295")]
internal readonly partial struct FullU32 : IDomainFixture<FullU32, uint>;

[BoundedInteger(typeof(long), "-9223372036854775808", "9223372036854775807")]
internal readonly partial struct FullS64 : IDomainFixture<FullS64, long>;

[BoundedInteger(typeof(ulong), "0", "18446744073709551615")]
internal readonly partial struct FullU64 : IDomainFixture<FullU64, ulong>;

[BoundedInteger(typeof(Int128), "-170141183460469231731687303715884105728", "170141183460469231731687303715884105727")]
internal readonly partial struct FullS128 : IDomainFixture<FullS128, Int128>;

[BoundedInteger(typeof(UInt128), "0", "340282366920938463463374607431768211455", Endianness = IntegerEndianness.BigEndian)]
internal readonly partial struct FullU128 : IDomainFixture<FullU128, UInt128>;

[BoundedInteger(typeof(nint), "-2147483648", "2147483647")]
internal readonly partial struct FullNative : IDomainFixture<FullNative, nint>;

[BoundedInteger(typeof(nuint), "0", "4294967295")]
internal readonly partial struct FullUNative : IDomainFixture<FullUNative, nuint>;
