// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using Xunit;

namespace SharpSafeNumerics.UnitTests;

/// <summary>Public declaration metadata remains explicit and preserves every configured option.</summary>
public sealed class AnnotationContractTests
{
    [Fact]
    public void Unspecified_options_preserve_reject_and_invalid_defaults()
    {
        var attribute = new BoundedIntegerAttribute(typeof(ushort), "0", "4095");
        Assert.Equal(ArithmeticPolicy.Reject, attribute.OverflowPolicy);
        Assert.Equal(DefaultValuePolicy.Invalid, attribute.DefaultPolicy);
        Assert.Equal(IntegerTextPolicy.InvariantDecimal, attribute.TextPolicy);
        Assert.Equal(IntegerEndianness.LittleEndian, attribute.Endianness);
        Assert.Equal(0, attribute.SerializationWidth);
        Assert.Null(attribute.Unit);
    }

    [Fact]
    public void Every_explicit_option_round_trips_as_metadata()
    {
        var attribute = new BoundedIntegerAttribute(typeof(ushort), "0", "4095")
        {
            OverflowPolicy = ArithmeticPolicy.Wrap,
            DefaultPolicy = DefaultValuePolicy.ZeroValid,
            TextPolicy = IntegerTextPolicy.InvariantHexadecimal,
            Endianness = IntegerEndianness.BigEndian,
            SerializationWidth = 2,
            Unit = "mV"
        };
        Assert.Equal(ArithmeticPolicy.Wrap, attribute.OverflowPolicy);
        Assert.Equal(DefaultValuePolicy.ZeroValid, attribute.DefaultPolicy);
        Assert.Equal(IntegerTextPolicy.InvariantHexadecimal, attribute.TextPolicy);
        Assert.Equal(IntegerEndianness.BigEndian, attribute.Endianness);
        Assert.Equal(2, attribute.SerializationWidth);
        Assert.Equal("mV", attribute.Unit);
    }
}
