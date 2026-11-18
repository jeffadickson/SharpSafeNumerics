// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using SharpSafeNumerics;
using SharpUnits;

/// <summary>Example admission adapter into SharpUnits' coherent-SI, double-valued quantity API.</summary>
public static class VoltageBridge
{
    /// <summary>Converts an initialized whole-volt domain without loss of integer precision; clears invalid input.</summary>
    /// <remarks>
    /// WholeVolts admits only integers in ±2^53, all exactly representable by double.
    /// This adapter does not claim that subsequent SharpUnits arithmetic is exact
    /// or allocation-free. The Unit annotation alone does not implement this bridge.
    /// </remarks>
    public static ArithmeticError TryToQuantity(WholeVolts value, out Quantity quantity)
    {
        quantity = default;
        if (!value.TryGetValue(out long raw)) return ArithmeticError.OutOfRange;
        quantity = Electrical.Volts((double)raw);
        return ArithmeticError.None;
    }
}
