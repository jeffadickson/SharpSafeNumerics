// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using CsCheck;
using Hedgehog;
using Hedgehog.Linq;
using SharpSafeNumerics;
using HRange = Hedgehog.Linq.Range;
using CGen = CsCheck.Gen;
using HGen = Hedgehog.Linq.Gen;
using SharpSafeNumerics.Verification;

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

Console.WriteLine("CsCheck wide arithmetic/policies: 2,000 UInt128 pairs across 12 types (24,000 type pairs)");
CGen.Select(CGen.UInt128, CGen.UInt128).Sample(pair =>
{
    WideIntegerOracle.VerifyAll(pair.Item1, pair.Item2);
}, seed: seed, iter: 2_000, threads: 1);

// Independent long generators/shrinkers form full 128-bit bit patterns. Destination
// widths consume low bits explicitly in the test harness, never in production paths.
var wideProperty =
    from ah in HGen.Int64(HRange.ConstantBoundedInt64()).ForAll()
    from al in HGen.Int64(HRange.ConstantBoundedInt64()).ForAll()
    from bh in HGen.Int64(HRange.ConstantBoundedInt64()).ForAll()
    from bl in HGen.Int64(HRange.ConstantBoundedInt64()).ForAll()
    select CheckWide(ah, al, bh, bl);
Console.WriteLine("Hedgehog wide arithmetic/policies: 1,000 pairs across 12 types (12,000 type pairs)");
wideProperty.Check(config);

static bool CheckWide(long ah, long al, long bh, long bl)
{
    UInt128 a = ((UInt128)unchecked((ulong)ah) << 64) | unchecked((ulong)al);
    UInt128 b = ((UInt128)unchecked((ulong)bh) << 64) | unchecked((ulong)bl);
    WideIntegerOracle.VerifyAll(a, b);
    return true;
}

Console.WriteLine("CsCheck fixed-point multiply: 10,000 raw pairs; existing replay seed");
CGen.Select(CGen.Long, CGen.Long).Sample(pair => FixedProductMatches(pair.Item1, pair.Item2), seed: seed, iter: 10_000, threads: 1);
var fixedProperty =
    from a in HGen.Int64(HRange.ConstantBoundedInt64()).ForAll()
    from b in HGen.Int64(HRange.ConstantBoundedInt64()).ForAll()
    select FixedProductMatches(a, b);
fixedProperty.Check(config);

Console.WriteLine("CsCheck safe decimal exact arithmetic: 2,000 pairs per format");
CGen.Select(CGen.Int, CGen.Int).Sample(pair =>
    DecimalExactMatches<Decimal32>(pair.Item1, pair.Item2) &&
    DecimalExactMatches<Decimal64>(pair.Item1, pair.Item2) &&
    DecimalExactMatches<Decimal128>(pair.Item1, pair.Item2), seed: seed, iter: 2_000, threads: 1);
var decimalProperty =
    from a in HGen.Int32(HRange.ConstantBoundedInt32()).ForAll()
    from b in HGen.Int32(HRange.ConstantBoundedInt32()).ForAll()
    select DecimalExactMatches<Decimal32>(a, b) && DecimalExactMatches<Decimal64>(a, b) && DecimalExactMatches<Decimal128>(a, b);
decimalProperty.Check(config);
Console.WriteLine("Property suites passed.");

static bool FixedProductMatches(long a, long b)
{
    // Work with positive magnitudes, independently of the implementation's signed
    // quotient/remainder and bitwise parity tests. Round half to an even raw integer.
    BigInteger product = (BigInteger)a * b;
    BigInteger denominator = BigInteger.One << 32;
    BigInteger q = BigInteger.DivRem(BigInteger.Abs(product), denominator, out var remainder);
    int compare = (remainder * 2).CompareTo(denominator);
    if (compare > 0 || (compare == 0 && !q.IsEven)) q++;
    q *= product.Sign;
    var result = SafeFixed.Multiply(Fixed64.FromRaw(a), Fixed64.FromRaw(b), FixedRounding.ToEven);
    var expected = q > long.MaxValue ? ArithmeticError.Overflow : q < long.MinValue ? ArithmeticError.Underflow : ArithmeticError.None;
    return result.Error == expected && (result.TryGetValue(out var value) ? value.RawValue == (long)q : value.RawValue == 0);
}

static bool DecimalExactMatches<T>(int a, int b) where T : struct, IFloatingPointIeee754<T>
{
    // These independent small integer operands keep products below seven digits,
    // allowing the same exact property to exercise all three decimal formats.
    a %= 1000; b %= 1000;
    T left = T.CreateChecked(a), right = T.CreateChecked(b);
    var sum = SafeDecimal.TryAdd(left, right);
    var difference = SafeDecimal.TrySubtract(left, right);
    var product = SafeDecimal.TryMultiply(left, right);
    var division = SafeDecimal.TryDivide(left, T.CreateChecked(2));
    // The BCL decimal types do not implement CreateChecked<BigInteger>. These
    // bounded integer calculations cannot overflow Int32 and give exact references.
    return sum.IsSuccess && !sum.IsInexact && sum.Value == T.CreateChecked(a + b) &&
        difference.IsSuccess && !difference.IsInexact && difference.Value == T.CreateChecked(a - b) &&
        product.IsSuccess && !product.IsInexact && product.Value == T.CreateChecked(a * b) &&
        division.IsSuccess && !division.IsInexact && division.Value * T.CreateChecked(2) == left;
}
