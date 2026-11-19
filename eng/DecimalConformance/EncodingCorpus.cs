// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Numerics;
using PeterO.Numbers;
using SharpSafeNumerics;

/// <summary>Replays the pinned GDA DPD interchange vectors in both directions.</summary>
internal static class EncodingCorpus
{
    internal static int Run(string directory)
    {
        try
        {
            var results = new[]
            {
                Check<Decimal32>(Path.Combine(directory, "dsEncode.decTest"), 8),
                Check<Decimal64>(Path.Combine(directory, "ddEncode.decTest"), 16),
                Check<Decimal128>(Path.Combine(directory, "dqEncode.decTest"), 32)
            };
            return results.Any(result => !result) ? 1 : 0;
        }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static bool Check<T>(string path, int hexDigits) where T : struct, IFloatingPointIeee754<T>
    {
        int passed = 0, failed = 0, nan = 0, skipped = 0;
        Span<char> formatted = stackalloc char[128];
        foreach (string line in File.ReadLines(path))
        {
            var row = line.Split("--", 2)[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (row.Length == 0 || row[0].EndsWith(':')) continue;
            if (row.Length > 1 && row[1] == "multiply") { skipped++; continue; }
            if (row.Length < 5 || row[1] != "apply" || row[3] != "->")
                throw new FormatException($"Malformed DPD row: {path}: {line}");
            if (row[2].StartsWith('#') == row[4].StartsWith('#')) { skipped++; continue; }
            bool decode = row[2].StartsWith('#');
            string hex = row[decode ? 2 : 4];
            string number = row[decode ? 4 : 2].Trim((char)39);
            if (hex.Length != hexDigits + 1 || !BigInteger.TryParse("0" + hex[1..],
                NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var bits))
                throw new FormatException($"Invalid DPD word: {path}: {line}");
            bool match;
            var expected = EDecimal.FromString(number);
            if (expected.IsNaN()) nan++;
            if (decode)
            {
                var value = IeeeDecimal<T>.DecodeDecimal(bits);
                var written = IeeeDecimal<T>.Format(value.Value, formatted);
                var actual = EDecimal.FromString(new string(formatted[..written.Value]));
                match = value.Flags == IeeeDecimalFlags.None && actual.ToString() == expected.ToString();
            }
            else
            {
                var value = IeeeDecimal<T>.Parse(number);
                var encoding = IeeeDecimal<T>.EncodeDecimal(value.Value);
                match = value.Flags == IeeeDecimalFlags.None && encoding.Value == bits;
            }
            if (match) passed++;
            else { failed++; Console.Error.WriteLine($"Mismatch {Path.GetFileName(path)} {row[0]}: {line}"); }
        }
        Console.WriteLine($"{Path.GetFileName(path)}: {passed} passed, {failed} failed, {skipped} non-interchange rows skipped, {nan} NaN rows");
        return passed > 0 && nan > 0 && failed == 0;
    }
}
