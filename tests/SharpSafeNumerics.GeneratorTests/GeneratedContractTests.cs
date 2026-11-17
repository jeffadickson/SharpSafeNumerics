using System.Globalization;
using System.Numerics;
using Xunit;

namespace SharpSafeNumerics.GeneratorTests;

// Implemented by the actual generated members. The harness needs no runtime discovery.
internal interface IDomainFixture<T, TStorage> : IEquatable<T>, IComparable<T>
    where T : struct, IDomainFixture<T, TStorage>
    where TStorage : struct, IBinaryInteger<TStorage>, IMinMaxValue<TStorage>
{
    static abstract TStorage Minimum { get; }
    static abstract TStorage Maximum { get; }
    static abstract ArithmeticError TryCreate(TStorage input, out T value);
    static abstract ArithmeticError TryCreateFrom<TFrom>(TFrom input, out T value)
        where TFrom : struct, IBinaryInteger<TFrom>;
    static abstract bool TryParse(ReadOnlySpan<char> text, out T value);
    static abstract bool TryRead(ReadOnlySpan<byte> source, out T value);
    static abstract explicit operator T(TStorage input);
    static abstract explicit operator TStorage(T value);
    static abstract bool operator ==(T left, T right);
    static abstract bool operator !=(T left, T right);
    static abstract bool operator <(T left, T right);
    static abstract bool operator >(T left, T right);
    static abstract bool operator <=(T left, T right);
    static abstract bool operator >=(T left, T right);
    bool IsValid { get; }
    TStorage Value { get; }
    bool TryGetValue(out TStorage value);
    ArithmeticError TryConvertTo<TTo>(out TTo value) where TTo : struct, IBinaryInteger<TTo>;
    ArithmeticError TryAdd(TStorage delta, out T value);
    ArithmeticError TrySubtract(TStorage delta, out T value);
    NumericResult<T> Add(TStorage delta);
    NumericResult<T> Subtract(TStorage delta);
    bool TryFormat(Span<char> destination, out int charsWritten);
    bool TryWrite(Span<byte> destination, out int bytesWritten);
}

public sealed class GeneratedContractTests
{
    [Fact]
    public void Full_range_signed_and_unsigned_fixtures_obey_the_public_contract()
    {
        CheckFixture<FullS8, sbyte>(sbyte.MinValue, sbyte.MaxValue, 1);
        CheckFixture<FullU8, byte>(byte.MinValue, byte.MaxValue, 1);
        CheckFixture<FullS16, short>(short.MinValue, short.MaxValue, 2);
        CheckFixture<FullU16, ushort>(ushort.MinValue, ushort.MaxValue, 2);
        CheckFixture<FullS32, int>(int.MinValue, int.MaxValue, 4);
        CheckFixture<FullU32, uint>(uint.MinValue, uint.MaxValue, 4);
        CheckFixture<FullS64, long>(long.MinValue, long.MaxValue, 8);
        CheckFixture<FullU64, ulong>(ulong.MinValue, ulong.MaxValue, 8);
        CheckFixture<FullS128, Int128>(Int128.MinValue, Int128.MaxValue, 16);
        CheckFixture<FullU128, UInt128>(UInt128.MinValue, UInt128.MaxValue, 16, bigEndian: true);
        CheckFixture<FullNative, nint>((nint)int.MinValue, (nint)int.MaxValue, 4);
        CheckFixture<FullUNative, nuint>(0, (nuint)uint.MaxValue, 4);
    }

    [Fact]
    public void Narrow_and_policy_configured_fixtures_obey_the_public_contract()
    {
        CheckFixture<Slot, int>(0, 10, 4);
        CheckFixture<ZeroCounter, short>(0, 100, 1, zeroValid: true);
        CheckFixture<Clamped, sbyte>(-5, 9, 1, policy: ArithmeticPolicy.Saturate);
        CheckFixture<ClampedUnsigned, byte>(2, 9, 1, policy: ArithmeticPolicy.Saturate);
        CheckFixture<Wrapped, sbyte>(-128, 120, 1, policy: ArithmeticPolicy.Wrap);
        CheckFixture<Throwing, sbyte>(1, 10, 1, policy: ArithmeticPolicy.Throw);
        CheckFixture<HexSigned, sbyte>(-128, 127, 1, hex: true);
        CheckFixture<HexNative, nint>((nint)int.MinValue, (nint)int.MaxValue, 4, hex: true);
        CheckFixture<NarrowBigEndian, int>(-789, 788, 2, bigEndian: true);
        CheckFixture<NarrowLittleEndian, long>(-789, 788, 2);
        CheckFixture<NarrowS8, short>(-128, 127, 1);
        CheckFixture<NarrowU8, ulong>(0, 255, 1);
        CheckFixture<MiddleS128, Int128>(-(Int128.One << 64) - 1, (Int128.One << 64) + 18, 16);
        CheckFixture<MiddleU128, UInt128>((UInt128.One << 96) + 3, (UInt128.One << 127) + 7, 16);
        CheckFixture<FixedCode, ushort>(7, 7, 2);
        CheckFixture<ZeroOnly, byte>(0, 0, 1, zeroValid: true);
    }

    private static void CheckFixture<T, TStorage>(TStorage minimum, TStorage maximum, int width,
        ArithmeticPolicy policy = ArithmeticPolicy.Reject, bool zeroValid = false, bool bigEndian = false, bool hex = false)
        where T : struct, IDomainFixture<T, TStorage>
        where TStorage : struct, IBinaryInteger<TStorage>, IMinMaxValue<TStorage>
    {
        Assert.Equal(minimum, T.Minimum);
        Assert.Equal(maximum, T.Maximum);
        Assert.Equal(ArithmeticError.None, T.TryCreate(minimum, out var low));
        Assert.Equal(ArithmeticError.None, T.TryCreate(maximum, out var high));
        Assert.Equal(low, (T)minimum);
        Assert.Equal(high, (T)maximum);
        Assert.Equal(minimum, (TStorage)low);
        Assert.Equal(maximum, (TStorage)high);
        int endpointOrder = minimum.CompareTo(maximum);
        Assert.True(low <= high);
        Assert.Equal(endpointOrder < 0, low < high);
        Assert.True(high >= low);
        Assert.Equal(endpointOrder < 0, high > low);
        Assert.False(high < low);
        Assert.False(low > high);
        Assert.True(low == (T)minimum);
        Assert.Equal(endpointOrder != 0, low != high);
        Assert.Equal(low.GetHashCode(), ((T)minimum).GetHashCode());
        Assert.True(low.Equals((object)(T)minimum));
        Assert.False(low.Equals((object)maximum));
        Assert.Equal(endpointOrder < 0, high.CompareTo(low) > 0);

        CheckDefault<T, TStorage>(low, zeroValid);
        var min = BigInteger.CreateChecked(minimum);
        var max = BigInteger.CreateChecked(maximum);
        var storageMin = BigInteger.CreateChecked(TStorage.MinValue);
        var storageMax = BigInteger.CreateChecked(TStorage.MaxValue);
        TStorage[] deltas = [TStorage.MinValue, TStorage.MaxValue, TStorage.Zero, TStorage.One];
        bool signed = storageMin.Sign < 0;

        CheckAdmissionFailure<T, TStorage>(min - 1, storageMin, storageMax);
        CheckAdmissionFailure<T, TStorage>(max + 1, storageMin, storageMax);
        CheckMalformedBuffers<T, TStorage>(min, max, width, signed, bigEndian, hex);

        // Endpoints and nearby values exercise bound transitions independently of the declaration.
        BigInteger[] sources = [min, min + 1, max - 1, max];
        foreach (var source in sources.Where(source => source >= min && source <= max).Distinct())
        {
            var raw = TStorage.CreateChecked(source);
            Assert.Equal(ArithmeticError.None, T.TryCreateFrom(raw, out var value));
            Assert.True(value.IsValid);
            Assert.True(value.TryGetValue(out var extracted));
            Assert.Equal(raw, extracted);
            Assert.Equal(raw, value.Value);
            Assert.Equal(ArithmeticError.None, value.TryConvertTo<TStorage>(out var exact));
            Assert.Equal(raw, exact);

            bool fitsByte = source >= byte.MinValue && source <= byte.MaxValue;
            Assert.Equal(fitsByte ? ArithmeticError.None : ArithmeticError.OutOfRange,
                value.TryConvertTo<byte>(out var small));
            Assert.Equal(fitsByte ? (byte)source : (byte)0, small);

            CheckText<T, TStorage>(value, source, width, hex);
            CheckBinary<T, TStorage>(value, source, width, bigEndian);
            foreach (var delta in deltas)
            {
                // BigInteger models the exact result before either primitive or domain admission.
                var amount = BigInteger.CreateChecked(delta);
                CheckArithmetic<T, TStorage>(value, delta, source + amount,
                    storageMin, storageMax, min, max, policy, subtract: false);
                CheckArithmetic<T, TStorage>(value, delta, source - amount,
                    storageMin, storageMax, min, max, policy, subtract: true);
            }
        }
    }

    private static void CheckDefault<T, TStorage>(T low, bool zeroValid)
        where T : struct, IDomainFixture<T, TStorage>
        where TStorage : struct, IBinaryInteger<TStorage>, IMinMaxValue<TStorage>
    {
        T initial = default;
        Assert.Equal(zeroValid, initial.IsValid);
        Assert.Equal(zeroValid, initial.TryGetValue(out var value));
        Assert.Equal(TStorage.Zero, value);
        if (zeroValid)
        {
            Assert.Equal(TStorage.Zero, initial.Value);
            Assert.Equal(ArithmeticError.None, initial.TryAdd(TStorage.Zero, out var zero));
            Assert.Equal(initial, zero);
            return;
        }

        Assert.Throws<InvalidOperationException>(() => initial.Value);
        Assert.Throws<InvalidOperationException>(() => (TStorage)initial);
        Assert.Equal("<invalid>", initial.ToString());
        Assert.Equal(0, initial.GetHashCode());
        Assert.NotEqual(initial, low);
        Assert.True(initial < low);
        Assert.True(low > initial);
        Assert.Equal(ArithmeticError.Uninitialized, initial.TryConvertTo<TStorage>(out var unavailable));
        Assert.Equal(TStorage.Zero, unavailable);
        Assert.Equal(ArithmeticError.Uninitialized, initial.TryAdd(TStorage.Zero, out var added));
        Assert.Equal(default, added);
        Assert.Equal(ArithmeticError.Uninitialized, initial.TrySubtract(TStorage.Zero, out var subtracted));
        Assert.Equal(default, subtracted);
        Assert.Equal(ArithmeticError.Uninitialized, initial.Add(TStorage.Zero).Error);
        Assert.Equal(ArithmeticError.Uninitialized, initial.Subtract(TStorage.Zero).Error);

        Span<char> text = ['#'];
        Assert.False(initial.TryFormat(text, out var charsWritten));
        Assert.Equal(0, charsWritten);
        Span<byte> bytes = stackalloc byte[16];
        bytes.Fill(0x5A);
        Assert.False(initial.TryWrite(bytes, out var bytesWritten));
        Assert.Equal(0, bytesWritten);
        foreach (byte sentinel in bytes)
            Assert.Equal((byte)0x5A, sentinel);
    }

    private static void CheckAdmissionFailure<T, TStorage>(BigInteger outside, BigInteger storageMin, BigInteger storageMax)
        where T : struct, IDomainFixture<T, TStorage>
        where TStorage : struct, IBinaryInteger<TStorage>, IMinMaxValue<TStorage>
    {
        if (outside >= storageMin && outside <= storageMax)
        {
            var raw = TStorage.CreateChecked(outside);
            Assert.Equal(ArithmeticError.OutOfRange, T.TryCreate(raw, out var rejected));
            Assert.Equal(default, rejected);
            Assert.Throws<OverflowException>(() => (T)raw);
        }

        // An endpoint may exhaust the largest built-in integer types on one side.
        if (outside < (BigInteger)Int128.MinValue || outside > (BigInteger)UInt128.MaxValue)
            return;

        ArithmeticError status = outside.Sign < 0
            ? T.TryCreateFrom((Int128)outside, out var value)
            : T.TryCreateFrom((UInt128)outside, out value);
        Assert.Equal(ArithmeticError.OutOfRange, status);
        Assert.Equal(default, value);
    }

    private static void CheckMalformedBuffers<T, TStorage>(BigInteger min, BigInteger max,
        int width, bool signed, bool bigEndian, bool hex)
        where T : struct, IDomainFixture<T, TStorage>
        where TStorage : struct, IBinaryInteger<TStorage>, IMinMaxValue<TStorage>
    {
        Assert.False(T.TryParse("0x10", out var invalid));
        Assert.Equal(default, invalid);
        Assert.False(T.TryParse("1\0", out var terminated));
        Assert.Equal(default, terminated);
        if (!hex)
        {
            Assert.False(T.TryParse((max + 1).ToString(CultureInfo.InvariantCulture), out var outside));
            Assert.Equal(default, outside);
        }

        Span<byte> bytes = stackalloc byte[width + 1];
        Assert.False(T.TryRead(bytes, out var extra));
        Assert.Equal(default, extra);
        Assert.False(T.TryRead(bytes[..(width - 1)], out var shortInput));
        Assert.Equal(default, shortInput);

        var largest = (BigInteger.One << (width * 8 - (signed ? 1 : 0))) - 1;
        var smallest = signed ? -largest - 1 : BigInteger.Zero;
        foreach (var candidate in new[] { min - 1, max + 1 })
        {
            if (candidate < smallest || candidate > largest)
                continue;

            var wire = bytes[..width];
            WriteOracle(candidate, wire, bigEndian);
            Assert.False(T.TryRead(wire, out var outside));
            Assert.Equal(default, outside);
        }
    }

    private static void CheckText<T, TStorage>(T value, BigInteger number, int width, bool hex)
        where T : struct, IDomainFixture<T, TStorage>
        where TStorage : struct, IBinaryInteger<TStorage>, IMinMaxValue<TStorage>
    {
        // The hex fixtures use the storage width. Remove BigInteger's positive sign nibble.
        var bits = number.Sign < 0 ? number + (BigInteger.One << (width * 8)) : number;
        string expected = hex && !bits.IsZero
            ? bits.ToString("X", CultureInfo.InvariantCulture).TrimStart('0')
            : number.ToString(CultureInfo.InvariantCulture);
        Span<char> text = stackalloc char[41];

        Assert.True(value.TryFormat(text, out var charsWritten));
        Assert.Equal(expected, new string(text[..charsWritten]));
        Assert.Equal(expected, value.ToString());
        Assert.True(T.TryParse(expected, out var parsed));
        Assert.Equal(value, parsed);
        Assert.False(value.TryFormat(text[..(charsWritten - 1)], out var insufficient));
        Assert.Equal(0, insufficient);
    }

    private static void CheckBinary<T, TStorage>(T value, BigInteger number, int width, bool bigEndian)
        where T : struct, IDomainFixture<T, TStorage>
        where TStorage : struct, IBinaryInteger<TStorage>, IMinMaxValue<TStorage>
    {
        Span<byte> expected = stackalloc byte[width];
        WriteOracle(number, expected, bigEndian);
        Span<byte> destination = stackalloc byte[width + 1];
        destination.Fill(0x5A);

        Assert.False(value.TryWrite(destination[..(width - 1)], out var insufficient));
        Assert.Equal(0, insufficient);
        for (int index = 0; index < destination.Length; index++)
            Assert.Equal((byte)0x5A, destination[index]);

        Assert.True(value.TryWrite(destination, out var bytesWritten));
        Assert.Equal(width, bytesWritten);
        Assert.Equal((byte)0x5A, destination[width]);
        Assert.True(destination[..width].SequenceEqual(expected));
        Assert.True(T.TryRead(expected, out var decoded));
        Assert.Equal(value, decoded);
    }

    private static void WriteOracle(BigInteger raw, Span<byte> wire, bool bigEndian)
    {
        // Arithmetic right shift supplies signed extension, with no dependency on BinaryPrimitives.
        for (int byteIndex = 0; byteIndex < wire.Length; byteIndex++)
        {
            int destination = bigEndian ? wire.Length - byteIndex - 1 : byteIndex;
            wire[destination] = (byte)((raw >> (8 * byteIndex)) & byte.MaxValue);
        }
    }

    private static void CheckArithmetic<T, TStorage>(T input, TStorage delta, BigInteger exact,
        BigInteger storageMin, BigInteger storageMax, BigInteger min, BigInteger max,
        ArithmeticPolicy policy, bool subtract)
        where T : struct, IDomainFixture<T, TStorage>
        where TStorage : struct, IBinaryInteger<TStorage>, IMinMaxValue<TStorage>
    {
        ArithmeticError expected = exact < storageMin ? ArithmeticError.Underflow
            : exact > storageMax ? ArithmeticError.Overflow
            : exact < min || exact > max ? ArithmeticError.OutOfRange
            : ArithmeticError.None;
        ArithmeticError status = subtract ? input.TrySubtract(delta, out var output) : input.TryAdd(delta, out output);
        Assert.Equal(expected, status);
        if (expected == ArithmeticError.None)
            Assert.Equal(exact, BigInteger.CreateChecked(output.Value));
        else
            Assert.Equal(default, output);

        NumericResult<T> Apply() => subtract ? input.Subtract(delta) : input.Add(delta);
        if (policy == ArithmeticPolicy.Throw && expected != ArithmeticError.None)
        {
            Assert.Throws<OverflowException>(() => Apply());
            return;
        }

        BigInteger candidate = exact;
        if (policy == ArithmeticPolicy.Saturate)
        {
            candidate = BigInteger.Min(BigInteger.Max(exact, min), max);
            expected = ArithmeticError.None;
        }
        else if (policy == ArithmeticPolicy.Wrap)
        {
            var modulus = storageMax - storageMin + 1;
            candidate = ((exact - storageMin) % modulus + modulus) % modulus + storageMin;
            expected = candidate < min || candidate > max ? ArithmeticError.OutOfRange : ArithmeticError.None;
        }

        var result = Apply();
        Assert.Equal(expected, result.Error);
        if (expected == ArithmeticError.None)
            Assert.Equal(candidate, BigInteger.CreateChecked(result.Value.Value));
        else
        {
            Assert.False(result.TryGetValue(out var rejected));
            Assert.Equal(default, rejected);
        }
    }
}

[BoundedInteger(typeof(ushort), "7", "7")]
internal readonly partial struct FixedCode : IDomainFixture<FixedCode, ushort>;

[BoundedInteger(typeof(byte), "0", "0", DefaultPolicy = DefaultValuePolicy.ZeroValid)]
internal readonly partial struct ZeroOnly : IDomainFixture<ZeroOnly, byte>;
