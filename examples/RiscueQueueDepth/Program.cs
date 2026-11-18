// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using SharpSafeNumerics;

if (Bounded<int, QueueDepthRange>.TryCreate(62, out var depth) != ArithmeticError.None)
    return 1;
if (depth.TryAdd(1, out var next) != ArithmeticError.None)
    return 2;
Console.WriteLine("Queue depth: " + next.Value);
if (next.TryAdd(1, out _) != ArithmeticError.OutOfRange)
    return 3;
return 0;

// Example policy only; this does not claim integration with the RISCue repository.
readonly struct QueueDepthRange : IStaticIntegerRange<int>
{
    public static int Minimum => 0;
    public static int Maximum => 63;
}
