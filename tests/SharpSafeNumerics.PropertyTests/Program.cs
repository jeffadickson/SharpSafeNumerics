using System.Numerics;
using CsCheck;
using Hedgehog;
using Hedgehog.Linq;
using SharpSafeNumerics;
using HRange = Hedgehog.Linq.Range;
using CGen = CsCheck.Gen;
using HGen = Hedgehog.Linq.Gen;

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

// Narrowing is admitted by independent mathematical bounds, not by reproducing
// the implementation's candidate/round-trip decision.
static bool ConversionMatchesOracle(int input)
{
    BigInteger exact = input;
    var error = SafeInteger.TryConvert<int, sbyte>(input, out var value);
    bool admitted = exact >= sbyte.MinValue && exact <= sbyte.MaxValue;
    return error == (admitted ? ArithmeticError.None : ArithmeticError.OutOfRange) &&
        value == (admitted ? (sbyte)exact : (sbyte)0);
}

string seed = new PCG(1, 731921).ToString();
Console.WriteLine("CsCheck replay seed: " + seed);

Console.WriteLine("CsCheck: 30,000 generated arithmetic pairs");
CGen.Select(CGen.Int, CGen.Int).Sample(t => AdditionMatchesOracle(t.Item1, t.Item2), seed: seed, iter: 10_000, threads: 1);
CGen.Select(CGen.Int, CGen.Int).Sample(t => SubtractionMatchesOracle(t.Item1, t.Item2), seed: seed, iter: 10_000, threads: 1);
CGen.Select(CGen.Int, CGen.Int).Sample(t => MultiplicationMatchesOracle(t.Item1, t.Item2), seed: seed, iter: 10_000, threads: 1);
Console.WriteLine("CsCheck: 10,000 narrowing conversions");
CGen.Int.Sample(ConversionMatchesOracle, seed: seed, iter: 10_000, threads: 1);

Console.WriteLine("Hedgehog: independently generated/shrunk arithmetic properties");
var addProperty =
    from a in HGen.Int32(HRange.ConstantBoundedInt32()).ForAll()
    from b in HGen.Int32(HRange.ConstantBoundedInt32()).ForAll()
    select AdditionMatchesOracle(a, b);
var config = PropertyConfig.withSeed(SeedModule.from(731921UL), PropertyConfig.Default.WithTests(1_000));
Console.WriteLine("Hedgehog seed: 731921; 1,000 cases per property");
addProperty.Check(config);

var mulProperty =
    from a in HGen.Int32(HRange.ConstantBoundedInt32()).ForAll()
    from b in HGen.Int32(HRange.ConstantBoundedInt32()).ForAll()
    select MultiplicationMatchesOracle(a, b);
mulProperty.Check(config);

// Generate near and inside the destination range independently of CsCheck's
// full-width integer distribution, improving acceptance and boundary coverage.
var conversionProperty =
    from input in HGen.Int32(HRange.Constant(-256, 256)).ForAll()
    select ConversionMatchesOracle(input);
conversionProperty.Check(config);

Console.WriteLine("Property suites passed.");
