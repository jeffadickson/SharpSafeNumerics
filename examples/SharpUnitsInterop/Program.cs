using System.Numerics;
using SharpSafeNumerics;
using SharpUnits;

// Executed integration assertions use the real SharpUnits assembly. The bridge
// owns exact admission into that library's double-valued coherent-SI interface.
int checks = 0;
Require(WholeVolts.Unit == "V", "declared unit metadata agrees with adapter convention");
Require(SafeInteger.TryAdd(3, 2, out int five) == ArithmeticError.None, "integer arithmetic");
Require(WholeVolts.TryCreate(five, out var voltage) == ArithmeticError.None, "domain admission");
Require(VoltageBridge.TryToQuantity(voltage, out var quantity) == ArithmeticError.None, "unit bridge");
Require(quantity.SiValue == 5 && quantity.Dimension == Dimension.Voltage, "SI volts and dimension");
var current = quantity / Electrical.Ohms(2);
Require(current.SiValue == 2.5 && current.Dimension == Dimension.ElectricCurrent, "actual dimensional division");
Require(VoltageBridge.TryToQuantity(default, out var cleared) == ArithmeticError.OutOfRange && cleared == default,
    "invalid default clears unit output");
Require(WholeVolts.TryCreate(9007199254740993L, out _) == ArithmeticError.OutOfRange, "reject potential double rounding");
Require(WholeVolts.TryCreate(-9007199254740993L, out _) == ArithmeticError.OutOfRange, "reject negative precision boundary");
foreach (long raw in new[] { -9007199254740992L, 9007199254740992L, -1L, 0L, 1L }) CheckExact(raw);
var random = new Random(731931);
for (int i = 0; i < 1000; i++) CheckExact(random.NextInt64(-9007199254740992L, 9007199254740992L));
Require(WholeVolts.TryCreate(9007199254740992L, out var maximum) == ArithmeticError.None &&
    maximum.TryAdd(1, out _) == ArithmeticError.OutOfRange, "bounded arithmetic before unit conversion");
Console.WriteLine($"SharpUnits bridge process: pointer-bits={IntPtr.Size * 8}; architecture={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}.");
Console.WriteLine($"SharpUnits real-source integration passed {checks} assertions; seed731931; units=coherent SI volts; precision=exact integer admission through ±2^53.");
return 0;

void CheckExact(long raw)
{
    Require(WholeVolts.TryCreate(raw, out var value) == ArithmeticError.None, "integer domain");
    Require(VoltageBridge.TryToQuantity(value, out var result) == ArithmeticError.None, "unit admission");
    // Independent BigInteger conversion catches an integer silently rounded by double.
    Require(new BigInteger(result.SiValue) == raw, "exact magnitude");
    Require(result.Dimension == Dimension.Voltage, "dimension preserved");
}
void Require(bool condition, string context)
{
    checks++;
    if (!condition) throw new InvalidOperationException("SharpUnits integration: " + context);
}

/// <summary>Whole coherent-SI volts in the contiguous integer interval exactly representable by double.</summary>
[BoundedInteger(typeof(long), "-9007199254740992", "9007199254740992", Unit = "V")]
public readonly partial struct WholeVolts;
