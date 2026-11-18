// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

namespace SharpSafeNumerics;

/// <summary>Immutable decimal/binary conversion modes and trap selection; defaults are nearest-even without traps.</summary>
/// <param name="DecimalRounding">Rounding used when a decimal format is the destination.</param>
/// <param name="BinaryRounding">Rounding used when a binary format is the destination.</param>
/// <param name="Traps">IEEE flags that prevent publishing an arithmetic value.</param>
public readonly record struct IeeeDecimalModes(IeeeDecimalRounding DecimalRounding = default, IeeeDecimalRounding BinaryRounding = default, IeeeDecimalFlags Traps = default);

/// <summary>Explicit immutable status and modes. No process, thread or hardware floating-point environment is changed.</summary>
/// <param name="Modes">Rounding and trap configuration.</param>
/// <param name="Flags">Accumulated IEEE exception flags.</param>
public readonly record struct IeeeDecimalState(IeeeDecimalModes Modes = default, IeeeDecimalFlags Flags = default)
{
    /// <summary>The context to pass to an operation whose destination is decimal.</summary>
    public IeeeDecimalContext DecimalContext => new(Modes.DecimalRounding, Modes.Traps);
    /// <summary>The context to pass to a decimal-to-binary conversion.</summary>
    public IeeeDecimalContext BinaryContext => new(Modes.BinaryRounding, Modes.Traps);
}

/// <summary>Explicit-state bindings for IEEE flag and mode management; callers retain and compose returned snapshots.</summary>
public static class IeeeDecimalEnvironment
{
    private static bool ValidFlags(IeeeDecimalFlags flags) => (flags & ~(IeeeDecimalFlags)31) == 0;
    private static bool ValidModes(IeeeDecimalModes modes) => (uint)modes.DecimalRounding <= 4 && (uint)modes.BinaryRounding <= 4 && ValidFlags(modes.Traps);
    private static bool Valid(IeeeDecimalState state) => ValidModes(state.Modes) && ValidFlags(state.Flags);
    private static IeeeDecimalResult<U> Result<U>(bool valid, U value) where U : struct => valid ? IeeeDecimalResult<U>.Ok(value) : IeeeDecimalResult<U>.Fail(ArithmeticError.OutOfRange);
    /// <summary>Returns the decimal destination rounding direction from a validated snapshot.</summary>
    public static IeeeDecimalResult<IeeeDecimalRounding> GetDecimalRoundingDirection(IeeeDecimalState state) => Result(Valid(state), state.Modes.DecimalRounding);
    /// <summary>Returns the binary destination rounding direction from a validated snapshot.</summary>
    public static IeeeDecimalResult<IeeeDecimalRounding> GetBinaryRoundingDirection(IeeeDecimalState state) => Result(Valid(state), state.Modes.BinaryRounding);
    /// <summary>Changes decimal rounding in a new snapshot, preserving all other modes and flags.</summary>
    public static IeeeDecimalResult<IeeeDecimalState> SetDecimalRoundingDirection(IeeeDecimalState state, IeeeDecimalRounding rounding) => Result(Valid(state) && (uint)rounding <= 4, state with { Modes = state.Modes with { DecimalRounding = rounding } });
    /// <summary>Changes binary destination rounding in a new snapshot, preserving other modes and flags.</summary>
    public static IeeeDecimalResult<IeeeDecimalState> SetBinaryRoundingDirection(IeeeDecimalState state, IeeeDecimalRounding rounding) => Result(Valid(state) && (uint)rounding <= 4, state with { Modes = state.Modes with { BinaryRounding = rounding } });
    /// <summary>Saves modes independently of accumulated flags.</summary>
    public static IeeeDecimalResult<IeeeDecimalModes> SaveModes(IeeeDecimalState state) => Result(Valid(state), state.Modes);
    /// <summary>Restores validated modes without altering the snapshot's flags.</summary>
    public static IeeeDecimalResult<IeeeDecimalState> RestoreModes(IeeeDecimalState state, IeeeDecimalModes saved) => Result(Valid(state) && ValidModes(saved), state with { Modes = saved });
    /// <summary>Restores default rounding and disables traps while preserving accumulated flags.</summary>
    public static IeeeDecimalResult<IeeeDecimalState> DefaultModes(IeeeDecimalState state) => Result(Valid(state), state with { Modes = default });
    /// <summary>Sets selected sticky flags without triggering an arithmetic trap or changing modes.</summary>
    public static IeeeDecimalResult<IeeeDecimalState> RaiseFlags(IeeeDecimalState state, IeeeDecimalFlags flags) => Result(Valid(state) && ValidFlags(flags), state with { Flags = state.Flags | flags });
    /// <summary>Clears selected sticky flags while preserving modes and other flags.</summary>
    public static IeeeDecimalResult<IeeeDecimalState> LowerFlags(IeeeDecimalState state, IeeeDecimalFlags flags) => Result(Valid(state) && ValidFlags(flags), state with { Flags = state.Flags & ~flags });
    /// <summary>Tests whether any selected sticky flag is raised.</summary>
    public static IeeeDecimalResult<bool> TestFlags(IeeeDecimalState state, IeeeDecimalFlags flags) => Result(Valid(state) && ValidFlags(flags), (state.Flags & flags) != 0);
    /// <summary>Saves the complete sticky-flag snapshot.</summary>
    public static IeeeDecimalResult<IeeeDecimalFlags> SaveFlags(IeeeDecimalState state) => Result(Valid(state), state.Flags);
    /// <summary>Replaces sticky flags with a validated saved snapshot.</summary>
    public static IeeeDecimalResult<IeeeDecimalState> RestoreFlags(IeeeDecimalState state, IeeeDecimalFlags saved) => Result(Valid(state) && ValidFlags(saved), state with { Flags = saved });
    /// <summary>Tests selected bits in a saved flag snapshot without requiring an environment instance.</summary>
    public static IeeeDecimalResult<bool> TestSavedFlags(IeeeDecimalFlags saved, IeeeDecimalFlags flags) => Result(ValidFlags(saved) && ValidFlags(flags), (saved & flags) != 0);
}
