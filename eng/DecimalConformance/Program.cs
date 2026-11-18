using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using PeterO.Numbers;
using SharpSafeNumerics;

// This executable is an independent corpus/reference probe, not a certification runner.
// Input contexts are retained per file, including exponent bounds and clamp. No production
// dependencies or global IEEE status are introduced by this verification-only executable.
if (args.Length == 1 && args[0] == "--probe")
{
    Probe<Decimal32>(); Probe<Decimal64>(); Probe<Decimal128>();
    return 0;
}
string? target = null;
if (args.Length >= 2 && args[0] == "--production")
{
    target = args[1]; args = args[2..];
    if (target is not ("decimal32" or "decimal64" or "decimal128")) { Console.Error.WriteLine("Unknown production format"); return 2; }
}
if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: DecimalConformance --probe | FILE.decTest [...]; emits JSONL, returns 1 on oracle mismatches, 2 on malformed input.");
    return 2;
}
int passed = 0, failed = 0, skipped = 0, malformed = 0;
foreach (string path in args)
{
    try
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Conformance input is not an existing file", path);
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int lineNumber = 0;
        foreach (string raw in File.ReadLines(path))
        {
            lineNumber++;
            // GDA tokens allow quoted whitespace; comments only begin outside quoted strings.
            var matches = Regex.Matches(raw, "'([^']|'')*'|\"([^\"]|\"\")*\"|[^\\s]+")
                .TakeWhile(m => !m.Value.StartsWith("--", StringComparison.Ordinal)).ToArray();
            // Regex fallback must not turn an unterminated quote or two adjacent
            // tokens into different, valid operands and hence false passing evidence.
            bool framing = matches.All(m => ValidToken(m.Value));
            for (int i = 1; i < matches.Length; i++) framing &= matches[i].Index > matches[i - 1].Index + matches[i - 1].Length;
            if (!framing) { malformed++; Console.WriteLine(JsonSerializer.Serialize(new { file = path, line = lineNumber, malformed = "Invalid quote or token framing" })); continue; }
            var tokens = matches.Select(m => m.Value).ToArray();
            if (tokens.Length == 0) continue;
            if (tokens[0].EndsWith(':'))
            {
                if (tokens.Length != 2) { malformed++; continue; }
                if (tokens[0].Equals("dectest:", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    Console.WriteLine(JsonSerializer.Serialize(new { file = Path.GetFileName(path), line = lineNumber, skipped = "Include not followed; supply the child file explicitly", include = Unquote(tokens[1]) }));
                    continue;
                }
                settings[tokens[0][..^1]] = Unquote(tokens[1]);
                continue;
            }
            int arrow = Array.IndexOf(tokens, "->");
            if (arrow < 3 || arrow + 1 >= tokens.Length) { malformed++; continue; }
            string id = tokens[0], operation = tokens[1].ToLowerInvariant();
            try
            {
                var unsupported = settings.Keys.Where(k => !new[] { "precision", "rounding", "maxexponent", "minexponent", "clamp", "extended", "version", "dectest" }.Contains(k.ToLowerInvariant())).ToArray();
                if (unsupported.Length != 0 || settings.GetValueOrDefault("extended", "1") != "1")
                    throw new NotSupportedException("Unsupported directives or subset arithmetic: " + string.Join(',', unsupported));
                if (settings.GetValueOrDefault("clamp", "0") is not ("0" or "1"))
                    throw new NotSupportedException("Unknown clamp option; only 0 and 1 are defined");
                int precision = int.Parse(settings.GetValueOrDefault("precision", "9"), CultureInfo.InvariantCulture);
                int emin = int.Parse(settings.GetValueOrDefault("minexponent", "-999999999"), CultureInfo.InvariantCulture);
                int emax = int.Parse(settings.GetValueOrDefault("maxexponent", "999999999"), CultureInfo.InvariantCulture);
                ERounding rounding = settings.GetValueOrDefault("rounding", "half_even").ToLowerInvariant() switch
                {
                    "half_even" => ERounding.HalfEven,
                    "half_up" => ERounding.HalfUp,
                    "half_down" => ERounding.HalfDown,
                    "down" => ERounding.Down,
                    "up" => ERounding.Up,
                    "ceiling" => ERounding.Ceiling,
                    "floor" => ERounding.Floor,
                    "05up" => ERounding.OddOrZeroFiveUp,
                    _ => throw new NotSupportedException("Unknown rounding direction")
                };
                EContext context = EContext.ForPrecisionAndRounding(precision, rounding)
                    .WithExponentRange(emin, emax).WithExponentClamp(settings.GetValueOrDefault("clamp", "0") == "1").WithBlankFlags();
                if (tokens[2..arrow].Any(t => Unquote(t) == "#"))
                    throw new NotSupportedException("GDA # missing-operand sentinel requires a separate invalid-input adapter");
                if (tokens[2..].Any(t => Unquote(t).Contains('#', StringComparison.Ordinal)))
                    throw new NotSupportedException("GDA width-qualified/encoded operand or expected result requires a raw-format adapter");
                EDecimal[] a = tokens[2..arrow].Select(t => EDecimal.FromString(Unquote(t))).ToArray();
                EDecimal expected = EDecimal.FromString(Unquote(tokens[arrow + 1]));
                int expectedFlags = 0;
                foreach (string flag in tokens[(arrow + 2)..]) expectedFlags |= Flag(flag);
                if (target != null)
                {
                    var observed = target switch
                    {
                        "decimal32" => Production<Decimal32>(operation, a, settings),
                        "decimal64" => Production<Decimal64>(operation, a, settings),
                        _ => Production<Decimal128>(operation, a, settings)
                    };
                    int ieeeExpected = MapIeee(expectedFlags);
                    bool targetPass = observed.Error == ArithmeticError.None && observed.Value.CompareToTotal(expected) == 0 && observed.Flags == ieeeExpected;
                    if (targetPass) passed++; else failed++;
                    Console.WriteLine(JsonSerializer.Serialize(new { file = Path.GetFileName(path), line = lineNumber, id, operation, settings, target, expected = expected.ToString(), actual = observed.Value.ToString(), expectedFlags = ieeeExpected, actualFlags = observed.Flags, observed.Error, passed = targetPass, scope = "production versus explicit GDA result" }));
                    continue;
                }
                EDecimal actual = Evaluate(operation, a, context);
                // Compare cohorts, signed zero and NaN representations using the oracle's exact tuple
                // comparison. Textual spelling (e.g. Infinity versus Inf) is intentionally immaterial.
                bool valueEqual = actual.CompareToTotal(expected) == 0;
                bool flagsEqual = context.Flags == expectedFlags;
                bool ok = valueEqual && flagsEqual;
                if (ok) passed++; else failed++;
                Console.WriteLine(JsonSerializer.Serialize(new { file = Path.GetFileName(path), line = lineNumber, id, operation, settings, expected = expected.ToString(), actual = actual.ToString(), expectedFlags, actualFlags = context.Flags, passed = ok, scope = "PeterO reference versus GDA; production adapter not executed" }));
            }
            catch (NotSupportedException ex) { skipped++; Console.WriteLine(JsonSerializer.Serialize(new { id, skipped = ex.Message })); }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
            { malformed++; Console.WriteLine(JsonSerializer.Serialize(new { id, malformed = ex.Message, line = lineNumber })); }
        }
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        malformed++;
        Console.WriteLine(JsonSerializer.Serialize(new { file = path, malformed = ex.Message, scope = "unreadable input; not passing evidence" }));
    }
}
Console.Error.WriteLine(JsonSerializer.Serialize(new { passed, failed, skipped, malformed }));
return malformed > 0 ? 2 : failed > 0 ? 1 : 0;

static string Unquote(string s) => s.Length > 1 && s[0] is '\'' or '"' ? s[1..^1].Replace("''", "'", StringComparison.Ordinal).Replace("\"\"", "\"", StringComparison.Ordinal) : s;

static bool ValidToken(string value)
{
    if (value[0] is not ('\'' or '"')) return !value.Contains('\'') && !value.Contains('"');
    char quote = value[0];
    if (value.Length < 2 || value[^1] != quote) return false;
    for (int i = 1; i < value.Length - 1; i++)
        if (value[i] == quote) { if (i + 1 >= value.Length - 1 || value[++i] != quote) return false; }
    return true;
}

// Only explicitly mapped operations execute; unknown operations cannot become passes.
static EDecimal Evaluate(string op, EDecimal[] a, EContext c) => (op, a.Length) switch
{
    ("add", 2) => a[0].Add(a[1], c),
    ("subtract", 2) => a[0].Subtract(a[1], c),
    ("multiply", 2) => a[0].Multiply(a[1], c),
    ("divide", 2) => a[0].Divide(a[1], c),
    ("fma", 3) => a[0].MultiplyAndAdd(a[1], a[2], c),
    ("squareroot", 1) => a[0].Sqrt(c),
    ("quantize", 2) => a[0].Quantize(a[1], c),
    ("exp", 1) => a[0].Exp(c),
    ("ln", 1) => a[0].Log(c),
    ("log10", 1) => a[0].Log10(c),
    ("power", 2) => a[0].Pow(a[1], c),
    _ => throw new NotSupportedException("Operation/arity not mapped: " + op + "/" + a.Length)
};

static int Flag(string s) => s.ToLowerInvariant() switch
{
    "invalid_operation" => EContext.FlagInvalid,
    "division_by_zero" => EContext.FlagDivideByZero,
    "division_impossible" or "division_undefined" => EContext.FlagInvalid,
    "overflow" => EContext.FlagOverflow,
    "underflow" => EContext.FlagUnderflow,
    "inexact" => EContext.FlagInexact,
    "rounded" => EContext.FlagRounded,
    "subnormal" => EContext.FlagSubnormal,
    "clamped" => EContext.FlagClamped,
    _ => throw new NotSupportedException("Condition not mapped: " + s)
};

static int MapIeee(int flags) =>
    ((flags & EContext.FlagInvalid) != 0 ? (int)IeeeDecimalFlags.Invalid : 0) |
    ((flags & EContext.FlagDivideByZero) != 0 ? (int)IeeeDecimalFlags.DivisionByZero : 0) |
    ((flags & EContext.FlagOverflow) != 0 ? (int)IeeeDecimalFlags.Overflow : 0) |
    ((flags & EContext.FlagUnderflow) != 0 ? (int)IeeeDecimalFlags.Underflow : 0) |
    ((flags & EContext.FlagInexact) != 0 ? (int)IeeeDecimalFlags.Inexact : 0);

static (EDecimal Value, int Flags, ArithmeticError Error) Production<T>(string op, EDecimal[] operands, Dictionary<string, string> settings)
    where T : struct, IFloatingPointIeee754<T>
{
    int precision = typeof(T) == typeof(Decimal32) ? 7 : typeof(T) == typeof(Decimal64) ? 16 : 34;
    int emax = precision == 7 ? 96 : precision == 16 ? 384 : 6144;
    if (settings.GetValueOrDefault("precision") != precision.ToString(CultureInfo.InvariantCulture) ||
        settings.GetValueOrDefault("maxexponent") != emax.ToString(CultureInfo.InvariantCulture) ||
        settings.GetValueOrDefault("minexponent") != (1 - emax).ToString(CultureInfo.InvariantCulture) ||
        settings.GetValueOrDefault("clamp", "0") != "1")
        throw new NotSupportedException("GDA context is not the selected fixed IEEE format");
    var direction = settings.GetValueOrDefault("rounding", "half_even").ToLowerInvariant() switch
    {
        "half_even" => IeeeDecimalRounding.ToNearestEven,
        "half_up" => IeeeDecimalRounding.ToNearestAway,
        "down" => IeeeDecimalRounding.TowardZero,
        "floor" => IeeeDecimalRounding.TowardNegative,
        "ceiling" => IeeeDecimalRounding.TowardPositive,
        _ => throw new NotSupportedException("GDA rounding not among the five IEEE decimal directions")
    };
    T[] a = new T[operands.Length];
    for (int i = 0; i < a.Length; i++)
    {
        // Raw NaN propagation needs a separate bit-level adapter. Never claim text qualifies it.
        if (operands[i].IsNaN()) throw new NotSupportedException("Raw NaN input requires payload/signaling adapter");
        a[i] = operands[i].IsInfinity() ? operands[i].IsNegative ? T.NegativeInfinity : T.PositiveInfinity : T.Parse(operands[i].ToString(), CultureInfo.InvariantCulture);
        if (ToOracle(a[i]).CompareToTotal(operands[i]) != 0)
            throw new NotSupportedException("Operand is not exactly representable with identical cohort in the selected format");
    }
    var c = new IeeeDecimalContext(direction);
    IeeeDecimalResult<T> result = (op, a.Length) switch
    {
        ("add", 2) => IeeeDecimal<T>.Add(a[0], a[1], c),
        ("subtract", 2) => IeeeDecimal<T>.Subtract(a[0], a[1], c),
        ("multiply", 2) => IeeeDecimal<T>.Multiply(a[0], a[1], c),
        ("divide", 2) => IeeeDecimal<T>.Divide(a[0], a[1], c),
        ("quantize", 2) => IeeeDecimal<T>.Quantize(a[0], a[1], c),
        ("squareroot", 1) => IeeeDecimal<T>.Sqrt(a[0], c),
        ("fma", 3) => IeeeDecimal<T>.FusedMultiplyAdd(a[0], a[1], a[2], c),
        ("exp", 1) => IeeeDecimal<T>.Exp(a[0], c),
        ("ln", 1) => IeeeDecimal<T>.Log(a[0], c),
        ("log10", 1) => IeeeDecimal<T>.Log10(a[0], c),
        ("power", 2) => IeeeDecimal<T>.Pow(a[0], a[1], c),
        _ => throw new NotSupportedException("Production operation not mapped")
    };
    return (result.IsSuccess ? ToOracle(result.Value) : EDecimal.NaN, (int)result.Flags, result.Error);
}

// Generic numeric formatting uses platform infinity symbols; map specials explicitly.
static EDecimal ToOracle<T>(T value) where T : struct, IFloatingPointIeee754<T> =>
    T.IsNaN(value) ? EDecimal.NaN : T.IsPositiveInfinity(value) ? EDecimal.PositiveInfinity :
    T.IsNegativeInfinity(value) ? EDecimal.NegativeInfinity :
    EDecimal.FromString(value.ToString(null, CultureInfo.InvariantCulture));

static void Probe<T>() where T : struct, IFloatingPointIeee754<T>
{
    foreach (string input in new[] { "0", "-0", "0.1", "1", "2", "1e-30", "1e30" })
    {
        T x = T.Parse(input, CultureInfo.InvariantCulture);
        Console.WriteLine(JsonSerializer.Serialize(new { format = typeof(T).Name, input, exp = T.Exp(x).ToString(), log = T.Log(x).ToString(), sqrt = T.Sqrt(x).ToString(), sin = T.Sin(x).ToString(), scope = "BCL observed values only; no flags or rounding-direction claim" }));
    }
    int precision = typeof(T) == typeof(Decimal32) ? 7 : typeof(T) == typeof(Decimal64) ? 16 : 34;
    foreach (string input in new[] { "0.1", "1", "2", "0.9999999", "1.000001", "10", "-0.1" })
    {
        T x = T.Parse(input, CultureInfo.InvariantCulture);
        EDecimal exactInput = EDecimal.FromString(x.ToString()!);
        foreach (string op in new[] { "exp", "ln", "squareroot" })
        {
            // A higher-precision independent computation is rounded once to the target precision.
            // This is a finite empirical check, not a proof of correctly rounded elementary functions.
            EDecimal reference = Evaluate(op, [exactInput], EContext.ForPrecisionAndRounding(160, ERounding.HalfEven))
                .RoundToPrecision(EContext.ForPrecisionAndRounding(precision, ERounding.HalfEven));
            T actual = op switch { "exp" => T.Exp(x), "ln" => T.Log(x), _ => T.Sqrt(x) };
            EDecimal observed = EDecimal.FromString(actual.ToString()!);
            bool matches = reference.IsNaN() ? observed.IsNaN() : reference.CompareTo(observed) == 0;
            Console.WriteLine(JsonSerializer.Serialize(new { format = typeof(T).Name, input, op, reference = reference.ToString(), actual = actual.ToString(), matches, oraclePrecision = 160 }));
        }
    }
}
