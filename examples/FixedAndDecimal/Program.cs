using System.Numerics;
using SharpSafeNumerics;

// Simulation tick quantization is explicit; the scale never travels as a double.
var tick = SafeFixed.FromRatio(1, 60, FixedRounding.ToEven);
if (!tick.TryGetValue(out var fixedTick)) return 1;
Console.WriteLine($"One simulation tick: raw {fixedTick.RawValue} / 4294967296 seconds.");
var one = Decimal64.One;
var three = Decimal64.CreateChecked(3);
var exact = SafeDecimal.TryDivide(one, three);
if (exact.Error != ArithmeticError.Inexact) return 2;
var rounded = SafeDecimal.TryDivide(one, three, DecimalRounding.ToEven);
if (!rounded.IsSuccess || !rounded.IsInexact) return 3;
Console.WriteLine($"Decimal64 1/3: exact status {exact.Error}; accepted nearest-even {rounded.Value}, inexact {rounded.IsInexact}.");
// This optional decimal path may allocate for exactness checks. It is deliberately
// outside a StrictNumeric/RealTime scope; the fixed-point arithmetic itself does not.
return 0;
