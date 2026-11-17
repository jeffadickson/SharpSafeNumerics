using SharpSafeNumerics;
using SharpSafeNumerics.Verification;
using System.Runtime.Intrinsics;
using System.Buffers.Binary;
using System.Numerics;
if (SafeInteger.TryAdd(40, 2, out int value) != ArithmeticError.None || value != 42) return 1;
if (SafeInteger.TryAdd(int.MaxValue, 1, out _) != ArithmeticError.Overflow) return 2;
// Sign-changing round trips must reject even when low bits survive both casts.
if (SafeInteger.TryConvert<int, uint>(-1, out var unsigned) != ArithmeticError.OutOfRange || unsigned != 0) return 3;
if (SafeInteger.TryConvert<uint, int>(uint.MaxValue, out var signed) != ArithmeticError.OutOfRange || signed != 0) return 4;
if (SafeInteger.TryConvert<UInt128, Int128>(UInt128.MaxValue, out _) != ArithmeticError.OutOfRange) return 5;
if (SafeInteger.TryConvert<Int128, long>(long.MinValue, out var wide) != ArithmeticError.None || wide != long.MinValue) return 6;
if (SafeInteger.TryConvert<nuint, nint>(nuint.MaxValue, out _) != ArithmeticError.OutOfRange) return 7;
// The reference harness is test-only. No BigInteger oracle is shipped in Core.
WideIntegerOracle.VerifyAll(UInt128.MaxValue, UInt128.One);
WideIntegerOracle.VerifyAll(UInt128.One << 127, UInt128.MaxValue);
var random = new Random(731922);
Span<byte> randomBytes = stackalloc byte[16];
for (int i = 0; i < 256; i++)
{
    random.NextBytes(randomBytes);
    UInt128 a = BinaryPrimitives.ReadUInt128LittleEndian(randomBytes);
    random.NextBytes(randomBytes);
    UInt128 b = BinaryPrimitives.ReadUInt128LittleEndian(randomBytes);
    WideIntegerOracle.VerifyAll(a, b);
}
if (AdcCount.TryCreate(4095, out var adc) != ArithmeticError.None || !default(AdcCount).IsValid) return 8;
Span<byte> encoded = stackalloc byte[2];
if (!adc.TryWrite(encoded, out int bytesWritten) || bytesWritten != 2 || encoded[0] != 15 || encoded[1] != 255) return 9;
if (!AdcCount.TryRead(encoded, out var decoded) || decoded != adc) return 10;
Span<char> text = stackalloc char[40];
if (!adc.TryFormat(text, out int charsWritten) || !text[..charsWritten].SequenceEqual("4095")) return 11;
if (!AdcCount.TryParse(text[..charsWritten], out decoded) || decoded != adc) return 12;
if (adc.Add(1).Value.Value != 4095 || adc.TryAdd(1, out _) != ArithmeticError.OutOfRange) return 13;
if (WideSigned.TryCreate(Int128.MinValue, out var minimum) != ArithmeticError.None) return 14;
Span<byte> wideBytes = stackalloc byte[16];
if (!minimum.TryWrite(wideBytes, out bytesWritten) || bytesWritten != 16 || wideBytes[0] != 128 || wideBytes[1..].IndexOfAnyExcept((byte)0) >= 0) return 15;
if (!WideSigned.TryRead(wideBytes, out var wideDecoded) || wideDecoded != minimum) return 16;
int[] batchLeft = new int[257], batchRight = new int[257], batchValues = new int[260];
var batchErrors = new ArithmeticError[260];
for (int i = 0; i < batchLeft.Length; i++)
{
    batchLeft[i] = i % 3 == 0 ? int.MaxValue : i % 3 == 1 ? int.MinValue : i;
    batchRight[i] = i % 3 == 1 ? -1 : 1;
}
var batchResult = SafeInteger.AddBatch(batchLeft, batchRight, batchValues, batchErrors);
if (!batchResult.IsSuccess) return 17;
int batchWidth = batchResult.VectorWidthUsed;
for (int i = 0; i < batchLeft.Length; i++)
{
    BigInteger exact = (BigInteger)batchLeft[i] + batchRight[i];
    var expected = exact > int.MaxValue ? ArithmeticError.Overflow : exact < int.MinValue ? ArithmeticError.Underflow : ArithmeticError.None;
    if (batchErrors[i] != expected || batchValues[i] != (expected == ArithmeticError.None ? (int)exact : 0)) return 18;
}
if (batchWidth != SafeInteger.BatchAddVectorWidth) return 19;
if (SafeFixed.FromRatio(1, 2).Value.RawValue != 2147483648L) return 20;
if (SafeFixed.Multiply(Fixed64.FromRaw(long.MaxValue), Fixed64.FromRaw(long.MaxValue)).Error != ArithmeticError.Inexact) return 21;
if (SafeFixed.Divide(default, default).Error != ArithmeticError.DivideByZero) return 22;
if (!VerifyDecimal<Decimal32>() || !VerifyDecimal<Decimal64>() || !VerifyDecimal<Decimal128>()) return 23;
Console.WriteLine("Fixed Q32.32 and three safe decimal formats passed Native AOT contract checks.");
Console.WriteLine($"Detected batch width: {batchWidth} bits; 257 BigInteger-checked lanes.");
Console.WriteLine($"AOT oracle: 3,096 type pairs, 52,632 operation/policy outcomes; seed 731922; native width {IntPtr.Size * 8}; Vector256 accelerated {Vector256.IsHardwareAccelerated}.");
Console.WriteLine("AOT smoke passed.");
return 0;

static bool VerifyDecimal<T>() where T : struct, IFloatingPointIeee754<T>
{
    // Independent integer expectations for exact values, plus deliberate precision
    // loss and nonfinite rejection, execute in the native application entry point.
    for (int i = -16; i <= 16; i++)
    {
        var a = T.CreateChecked(i);
        var product = SafeDecimal.TryMultiply(a, T.CreateChecked(3));
        if (!product.IsSuccess || product.IsInexact || product.Value != T.CreateChecked(i * 3)) return false;
    }
    var exact = SafeDecimal.TryDivide(T.One, T.CreateChecked(3));
    var rounded = SafeDecimal.TryDivide(T.One, T.CreateChecked(3), DecimalRounding.ToEven);
    return exact.Error == ArithmeticError.Inexact && rounded.IsSuccess && rounded.IsInexact &&
        SafeDecimal.TryAdd(T.NaN, T.Zero).Error == ArithmeticError.NonFinite &&
        SafeDecimal.TryDivide(T.One, T.Zero).Error == ArithmeticError.DivideByZero &&
        SafeDecimal.TryNegate(T.One, (DecimalRounding)255).Error == ArithmeticError.OutOfRange;
}

[BoundedInteger(typeof(ushort), "0", "4095", OverflowPolicy = ArithmeticPolicy.Saturate,
    DefaultPolicy = DefaultValuePolicy.ZeroValid, Endianness = IntegerEndianness.BigEndian,
    SerializationWidth = 2, Unit = "count")]
public readonly partial struct AdcCount;

[BoundedInteger(typeof(Int128), "-170141183460469231731687303715884105728", "170141183460469231731687303715884105727",
    Endianness = IntegerEndianness.BigEndian)]
public readonly partial struct WideSigned;
