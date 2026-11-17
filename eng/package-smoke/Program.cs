using SharpSafeNumerics;
using SharpSafeNumerics.Unions;
using System.Numerics;

if (PackageDepth.TryCreate(63, out var depth) != ArithmeticError.None) return 1;
if (depth.TryAdd(1, out _) != ArithmeticError.OutOfRange || depth.Add(1).Value.Value != 63) return 2;
Span<byte> buffer = stackalloc byte[1];
if (!depth.TryWrite(buffer, out int written) || written != 1 || buffer[0] != 63) return 3;
if (!PackageDepth.TryRead(buffer, out var restored) || restored != depth) return 4;
if (!PackageDepth.TryParse("63", out restored) || restored != depth) return 5;
_ = ArithmeticOutcome.FromResult(NumericResult<int>.Ok(63));
if (SafeFixed.FromRatio(1, 2).Value.RawValue != 2147483648L) return 6;
if (SafeDecimal.TryDivide(Decimal64.One, Decimal64.CreateChecked(3)).Error != ArithmeticError.Inexact) return 7;
var rounded = SafeDecimal.TryDivide(Decimal128.One, Decimal128.CreateChecked(3), DecimalRounding.ToEven);
if (!rounded.IsSuccess || !rounded.IsInexact) return 8;
Console.WriteLine("All five RC packages consumed; generated domain, fixed point, safe decimal and union adapter passed.");
return 0;

[BoundedInteger(typeof(byte), "0", "63", OverflowPolicy = ArithmeticPolicy.Saturate, SerializationWidth = 1)]
public readonly partial struct PackageDepth;
