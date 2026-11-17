using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using CsCheck;
using SharpSafeNumerics;
using SharpSafeNumerics.Verification;

int seconds = 900, seedNumber = 731925, expectedWidth = -1;
if (args.Length % 2 != 0) return InvalidArguments();
var seen = new HashSet<string>(StringComparer.Ordinal);
for (int i = 0; i < args.Length; i += 2)
{
    if (!seen.Add(args[i]) || !int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int value)) return InvalidArguments();
    switch (args[i])
    {
        case "--seconds" when value is >= 1 and <= 86400: seconds = value; break;
        case "--seed" when value > 0: seedNumber = value; break;
        case "--expected-width" when value is 0 or 128 or 256: expectedWidth = value; break;
        default: return InvalidArguments();
    }
}
if (expectedWidth >= 0 && SafeInteger.BatchAddVectorWidth != expectedWidth)
{
    Console.Error.WriteLine($"Expected SIMD width {expectedWidth}, detected {SafeInteger.BatchAddVectorWidth}.");
    return 3;
}

string seed = new PCG(1, (ulong)seedNumber).ToString();
long samples = 0, lanes = 0;
int[] left = new int[267], right = new int[267], values = new int[270];
var errors = new ArithmeticError[270];
long initialHeap = GC.GetTotalMemory(forceFullCollection: true);
int[] initialCollections = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
var timer = Stopwatch.StartNew();
Console.WriteLine($"Soak start UTC {DateTime.UtcNow:O}; seconds={seconds}; seed={seedNumber}/{seed}; workers=1; detected-width={SafeInteger.BatchAddVectorWidth}; native-width={IntPtr.Size * 8}");

Gen.Select(Gen.UInt128, Gen.UInt128).Sample(pair =>
{
    UInt128 a = pair.Item1, b = pair.Item2;
    WideIntegerOracle.VerifyAll(a, b);
    int count = (int)(a % 268);
    for (int i = 0; i < count; i++)
    {
        left[i] = int.CreateTruncating(a >> ((i % 4) * 32));
        right[i] = int.CreateTruncating(b >> ((i % 4) * 32));
    }
    Array.Fill(values, 0x13579BDF); Array.Fill(errors, (ArithmeticError)255);
    var batch = SafeInteger.AddBatch(left.AsSpan(0, count), right.AsSpan(0, count), values, errors);
    Require(batch.IsSuccess, "batch-contract");
    int expected = count >= 8 && SafeInteger.BatchAddVectorWidth == 256 ? 256 : count >= 4 && SafeInteger.BatchAddVectorWidth >= 128 ? 128 : 0;
    Require(batch.VectorWidthUsed == expected, "batch-dispatch");
    for (int i = 0; i < count; i++)
    {
        BigInteger exact = (BigInteger)left[i] + right[i];
        var status = exact > int.MaxValue ? ArithmeticError.Overflow : exact < int.MinValue ? ArithmeticError.Underflow : ArithmeticError.None;
        Require(errors[i] == status && values[i] == (status == ArithmeticError.None ? (int)exact : 0), $"batch-lane {i}, operands {left[i]},{right[i]}");
    }
    Require(values.AsSpan(count).IndexOfAnyExcept(0x13579BDF) < 0 && errors.AsSpan(count).IndexOfAnyExcept((ArithmeticError)255) < 0, "batch-tail-write");

    ushort raw = (ushort)(a % 4096);
    Require(SoakCount.TryCreate(raw, out var domain) == ArithmeticError.None, "domain-create");
    Span<char> text = stackalloc char[16];
    Require(domain.TryFormat(text, out int chars) && SoakCount.TryParse(text[..chars], out var parsed) && parsed == domain, "text-roundtrip");
    Require(!SoakCount.TryParse(raw.ToString(CultureInfo.InvariantCulture) + "x", out _), "malformed-text");
    Span<byte> bytes = stackalloc byte[2];
    Require(domain.TryWrite(bytes, out int written) && written == 2 && bytes[0] == raw >> 8 && bytes[1] == (raw & 255), "binary-oracle");
    Require(SoakCount.TryRead(bytes, out var decoded) && decoded == domain, "binary-roundtrip");
    bytes[0] = (byte)(b >> 8); bytes[1] = (byte)b;
    int decodedRaw = bytes[0] * 256 + bytes[1];
    bool admitted = SoakCount.TryRead(bytes, out decoded);
    Require(admitted == (decodedRaw <= 4095) && (!admitted || decoded.Value == decodedRaw), "binary-admission");
    Require(domain.Add(1).Value.Value == Math.Min(raw + 1, 4095), "domain-saturation");
    Require(domain.TryAdd(1, out _) == (raw == 4095 ? ArithmeticError.OutOfRange : ArithmeticError.None), "domain-reject");
    samples++; lanes += count;
}, seed: seed, iter: long.MaxValue, time: seconds, threads: 1);

timer.Stop();
long finalHeap = GC.GetTotalMemory(forceFullCollection: true);
Console.WriteLine($"Soak passed UTC {DateTime.UtcNow:O}; elapsed={timer.Elapsed.TotalSeconds:F3}s; samples={samples}; type-pairs={samples * 12}; arithmetic-policy-outcomes={samples * 204}; batch-lanes={lanes}; managed-live-heap-before={initialHeap}; after={finalHeap}; GC0={GC.CollectionCount(0) - initialCollections[0]}; GC1={GC.CollectionCount(1) - initialCollections[1]}; GC2={GC.CollectionCount(2) - initialCollections[2]}");
return 0;

static int InvalidArguments()
{
    Console.Error.WriteLine("Usage: --seconds 1..86400 --seed positive-integer --expected-width 0|128|256 (all optional, no duplicates).");
    return 2;
}
static void Require(bool condition, string detail)
{
    if (!condition) throw new InvalidOperationException("Soak oracle mismatch: " + detail);
}

[BoundedInteger(typeof(ushort), "0", "4095", OverflowPolicy = ArithmeticPolicy.Saturate,
    DefaultPolicy = DefaultValuePolicy.ZeroValid, SerializationWidth = 2, Endianness = IntegerEndianness.BigEndian)]
public readonly partial struct SoakCount;
