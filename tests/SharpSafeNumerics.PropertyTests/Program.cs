using System.Numerics;
using CsCheck;
using Hedgehog;
using Hedgehog.Linq;
using SharpSafeNumerics;
using HRange = Hedgehog.Linq.Range;

static bool AdditionMatchesOracle(int a, int b)
{
    BigInteger exact = (BigInteger)a + b;
    var error = SafeInteger.TryAdd(a, b, out var value);
    if (exact > int.MaxValue) return error == ArithmeticError.Overflow;
    if (exact < int.MinValue) return error == ArithmeticError.Underflow;
    return error == ArithmeticError.None && value == (int)exact;
}

static bool SubtractionMatchesOracle(int a, int b)
{
    BigInteger exact = (BigInteger)a - b;
    var error = SafeInteger.TrySubtract(a, b, out var value);
    if (exact > int.MaxValue) return error == ArithmeticError.Overflow;
    if (exact < int.MinValue) return error == ArithmeticError.Underflow;
    return error == ArithmeticError.None && value == (int)exact;
}

static bool MultiplicationMatchesOracle(int a, int b)
{
    BigInteger exact = (BigInteger)a * b;
    var error = SafeInteger.TryMultiply(a, b, out var value);
    if (exact > int.MaxValue) return error == ArithmeticError.Overflow;
    if (exact < int.MinValue) return error == ArithmeticError.Underflow;
    return error == ArithmeticError.None && value == (int)exact;
}

Console.WriteLine("CsCheck: 30,000 generated arithmetic pairs");
Gen.Select(Gen.Int, Gen.Int).Sample(t => AdditionMatchesOracle(t.Item1, t.Item2), iter: 10_000);
Gen.Select(Gen.Int, Gen.Int).Sample(t => SubtractionMatchesOracle(t.Item1, t.Item2), iter: 10_000);
Gen.Select(Gen.Int, Gen.Int).Sample(t => MultiplicationMatchesOracle(t.Item1, t.Item2), iter: 10_000);

Console.WriteLine("Hedgehog: independently generated/shrunk arithmetic properties");
var addProperty =
    from a in Gen.Int32(HRange.ConstantBoundedInt32()).ForAll()
    from b in Gen.Int32(HRange.ConstantBoundedInt32()).ForAll()
    select AdditionMatchesOracle(a, b);
addProperty.Check();

var mulProperty =
    from a in Gen.Int32(HRange.ConstantBoundedInt32()).ForAll()
    from b in Gen.Int32(HRange.ConstantBoundedInt32()).ForAll()
    select MultiplicationMatchesOracle(a, b);
mulProperty.Check();

Console.WriteLine("Property suites passed.");
