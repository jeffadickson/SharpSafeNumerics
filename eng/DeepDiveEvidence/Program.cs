// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using SharpSafeNumerics;

// Export real package outcomes as decimal strings: JavaScript Numbers cannot
// preserve every Int64/Int128 value. These fixtures qualify the browser model,
// not arbitrary .NET execution inside a browser.
var cases = new List<object>();
Add<byte>("byte"); Add<sbyte>("sbyte"); Add<short>("short"); Add<ushort>("ushort");
Add<int>("int"); Add<uint>("uint"); Add<long>("long"); Add<ulong>("ulong");
Add<Int128>("Int128"); Add<UInt128>("UInt128"); Add<nint>("nint"); Add<nuint>("nuint");
Span<byte> bytes = stackalloc byte[2];
foreach (int current in new[] { 0, 1, 62, 63 })
foreach (int delta in new[] { -64, -1, 0, 1, 2, int.MaxValue })
{
    _ = DemoDepth.TryCreate(current, out var depth);
    var error = depth.TryAdd(delta, out var next);
    var saturated = depth.Add(delta);
    if (!depth.TryWrite(bytes, out int written) || written != 2) throw new InvalidOperationException("Codec fixture failed.");
    cases.Add(new { kind = "domain", current, delta, status = error.ToString(),
        value = next.TryGetValue(out int primitive) ? primitive : 0,
        saturated = saturated.Value.Value, hex = Convert.ToHexString(bytes) });
}
string json = JsonSerializer.Serialize(new { nativeBits = IntPtr.Size * 8, cases });
if (args.Length == 0) Console.WriteLine(json);
else if (args is ["--verify", var document])
{
    if (!File.Exists(document)) throw new FileNotFoundException("Deep-dive document missing.", document);
    var match = Regex.Match(File.ReadAllText(document), "<script id=\"fixtures\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline);
    if (!match.Success || !JsonElement.DeepEquals(JsonDocument.Parse(json).RootElement,
        JsonDocument.Parse(match.Groups[1].Value).RootElement))
        throw new InvalidOperationException("Embedded deep-dive fixtures disagree with the current package.");
    Console.WriteLine($"Verified {cases.Count} embedded fixtures against the current package.");
}
else throw new ArgumentException("Usage: DeepDiveEvidence [--verify <html-document>]");

void Add<T>(string name) where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
{
    T[] points = [T.MinValue, T.MaxValue, T.Zero, T.One];
    foreach (T a in points)
    foreach (T b in points)
    foreach (string op in new[] { "+", "-", "*" })
    foreach (ArithmeticPolicy policy in Enum.GetValues<ArithmeticPolicy>())
    {
        string status; string value = "0";
        try
        {
            var result = op switch { "+" => SafeInteger.Add(a, b, policy), "-" => SafeInteger.Subtract(a, b, policy), _ => SafeInteger.Multiply(a, b, policy) };
            status = result.Error.ToString();
            if (result.TryGetValue(out T output)) value = output.ToString(null, CultureInfo.InvariantCulture);
        }
        catch (OverflowException) { status = "OverflowException"; }
        cases.Add(new { kind = "arithmetic", type = name, a = a.ToString(null, CultureInfo.InvariantCulture),
            b = b.ToString(null, CultureInfo.InvariantCulture), op, policy = policy.ToString(), status, value });
    }
    foreach (Int128 value in new Int128[] { -1, 0, 1, 255, 256, 65535, 65536, Int128.MaxValue })
    {
        var status = SafeInteger.TryConvert<Int128, T>(value, out T output);
        cases.Add(new { kind = "conversion", type = name, input = value.ToString(CultureInfo.InvariantCulture),
            status = status.ToString(), value = output.ToString(null, CultureInfo.InvariantCulture) });
    }
}

/// <summary>Matches the queue example: domain saturation and a two-byte big-endian codec.</summary>
[BoundedInteger(typeof(int), "0", "63", OverflowPolicy = ArithmeticPolicy.Saturate,
    DefaultPolicy = DefaultValuePolicy.ZeroValid, SerializationWidth = 2, Endianness = IntegerEndianness.BigEndian)]
public readonly partial struct DemoDepth;
