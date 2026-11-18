// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using SharpSafeNumerics;

if (QueueDepth.TryCreate(62, out var full) != ArithmeticError.None) return 1;
if (full.TryAdd(1, out full) != ArithmeticError.None || full.Value != 63) return 2;
if (full.TryAdd(1, out _) != ArithmeticError.OutOfRange) return 3;
if (full.Add(1).Value != full) return 4;
if (!default(QueueDepth).IsValid || default(QueueDepth).Value != 0) return 5;

Span<char> text = stackalloc char[2];
if (!full.TryFormat(text, out var charsWritten) ||
    !QueueDepth.TryParse(text[..charsWritten], out var parsed) || parsed != full) return 6;

Span<byte> encoded = stackalloc byte[QueueDepth.SerializationWidth];
if (!parsed.TryWrite(encoded, out var bytesWritten) || bytesWritten != encoded.Length ||
    encoded[0] != 0 || encoded[1] != 63 ||
    !QueueDepth.TryRead(encoded, out var decoded) || decoded != full) return 7;

if (QueueDepth.TryCreateFrom(ushort.MaxValue, out _) != ArithmeticError.OutOfRange ||
    decoded.TryConvertTo(out byte capacity) != ArithmeticError.None || capacity != 63) return 8;

Console.WriteLine($"Generated QueueDepth verified: {decoded} {QueueDepth.Unit}");
return 0;

[BoundedInteger(typeof(int), "0", "63",
    OverflowPolicy = ArithmeticPolicy.Saturate, DefaultPolicy = DefaultValuePolicy.ZeroValid,
    SerializationWidth = 2, Endianness = IntegerEndianness.BigEndian, Unit = "entries")]
public readonly partial struct QueueDepth;
