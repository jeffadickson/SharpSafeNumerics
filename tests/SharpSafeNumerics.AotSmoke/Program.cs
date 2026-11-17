using SharpSafeNumerics;
if (SafeInteger.TryAdd(40, 2, out int value) != ArithmeticError.None || value != 42) return 1;
if (SafeInteger.TryAdd(int.MaxValue, 1, out _) != ArithmeticError.Overflow) return 2;
// Sign-changing round trips must reject even when low bits survive both casts.
if (SafeInteger.TryConvert<int, uint>(-1, out var unsigned) != ArithmeticError.OutOfRange || unsigned != 0) return 3;
if (SafeInteger.TryConvert<uint, int>(uint.MaxValue, out var signed) != ArithmeticError.OutOfRange || signed != 0) return 4;
if (SafeInteger.TryConvert<UInt128, Int128>(UInt128.MaxValue, out _) != ArithmeticError.OutOfRange) return 5;
if (SafeInteger.TryConvert<Int128, long>(long.MinValue, out var wide) != ArithmeticError.None || wide != long.MinValue) return 6;
if (SafeInteger.TryConvert<nuint, nint>(nuint.MaxValue, out _) != ArithmeticError.OutOfRange) return 7;
Console.WriteLine("AOT smoke passed.");
return 0;
