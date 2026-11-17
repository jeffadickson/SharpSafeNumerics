using SharpSafeNumerics;
if (SafeInteger.TryAdd(40, 2, out int value) != ArithmeticError.None || value != 42) return 1;
if (SafeInteger.TryAdd(int.MaxValue, 1, out _) != ArithmeticError.Overflow) return 2;
Console.WriteLine("AOT smoke passed.");
return 0;
