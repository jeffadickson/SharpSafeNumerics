using SharpSafeNumerics;

if (QueueDepth.TryCreate(62, out var depth) != ArithmeticError.None) return 1;
if (depth.TryAdd(1, out var full) != ArithmeticError.None || full.Value != 63) return 2;
if (full.TryAdd(1, out _) != ArithmeticError.OutOfRange) return 3;
if (default(QueueDepth).IsValid) return 4;
Console.WriteLine("Generated QueueDepth verified: " + full);
return 0;

[BoundedInteger(typeof(int), "0", "63")]
public readonly partial struct QueueDepth;
