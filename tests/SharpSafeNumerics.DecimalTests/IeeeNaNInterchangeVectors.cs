// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Numerics;
using Xunit;

namespace SharpSafeNumerics.DecimalTests;

/// <summary>Known IEEE interchange fields checked independently of package round trips.</summary>
public sealed class IeeeNaNInterchangeVectors
{
    [Fact]
    public void NaNSignKindAndPayloadMatchBothInterchangeEncodings()
    {
        Check<Decimal32>(32, 6);
        Check<Decimal64>(64, 15);
        Check<Decimal128>(128, 33);
    }

    private static void Check<T>(int width, int digits) where T : struct, IFloatingPointIeee754<T>
    {
        // IEEE DPD encodes 123 as 0010100011 and 999 as 0011111111.
        // Repeat the 999 declet to independently construct the maximum payload.
        var maxDpd = ((BigInteger.One << (digits / 3 * 10)) - 1) / 1023 * 255;
        foreach (bool negative in new[] { false, true })
        foreach (bool signaling in new[] { false, true })
        foreach (var pair in new (BigInteger Bid, BigInteger Dpd)[]
        {
            (0, 0), (123, 0xa3), (BigInteger.Pow(10, digits) - 1, maxDpd)
        })
        {
            var header = (BigInteger)31 << (width - 6);
            if (negative) header |= BigInteger.One << (width - 1);
            if (signaling) header |= BigInteger.One << (width - 7);
            var bid = IeeeDecimal<T>.DecodeBinary(header | pair.Bid);
            var dpd = IeeeDecimal<T>.DecodeDecimal(header | pair.Dpd);
            Assert.Equal(IeeeDecimalFlags.None, bid.Flags);
            Assert.Equal(IeeeDecimalFlags.None, dpd.Flags);
            foreach (T value in new[] { bid.Value, dpd.Value })
            {
                Assert.Equal(negative, IeeeDecimal<T>.IsSignMinus(value).Value);
                Assert.Equal(signaling, IeeeDecimal<T>.IsSignaling(value).Value);
                Assert.Equal(pair.Bid, IeeeDecimal<T>.GetPayload(value).Value);
                Assert.Equal(header | pair.Bid, IeeeDecimal<T>.EncodeBinary(value).Value);
                Assert.Equal(header | pair.Dpd, IeeeDecimal<T>.EncodeDecimal(value).Value);
            }
        }
    }
}
