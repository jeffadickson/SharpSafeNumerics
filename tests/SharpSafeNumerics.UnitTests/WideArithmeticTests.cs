using SharpSafeNumerics.Verification;
using Xunit;
using Xunit.Abstractions;

namespace SharpSafeNumerics.UnitTests;

/// <summary>SN-010-WIDE: deterministic adversarial and full-width reference campaigns.</summary>
public sealed class WideArithmeticTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public void All_operations_and_policies_match_exact_oracle(int type)
    {
        var result = type switch
        {
            0 => WideIntegerOracle.Campaign<byte>(), 1 => WideIntegerOracle.Campaign<sbyte>(),
            2 => WideIntegerOracle.Campaign<short>(), 3 => WideIntegerOracle.Campaign<ushort>(),
            4 => WideIntegerOracle.Campaign<int>(), 5 => WideIntegerOracle.Campaign<uint>(),
            6 => WideIntegerOracle.Campaign<long>(), 7 => WideIntegerOracle.Campaign<ulong>(),
            8 => WideIntegerOracle.Campaign<Int128>(), 9 => WideIntegerOracle.Campaign<UInt128>(),
            10 => WideIntegerOracle.Campaign<nint>(), 11 => WideIntegerOracle.Campaign<nuint>(),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        output.WriteLine($"Type {type}: boundaries={result.Boundaries}; pairs={result.Pairs}; outcomes={result.Pairs * 17}; seed={WideIntegerOracle.Seed}");
    }
}
