using System.Text.Json.Serialization;

/// <summary>Typed campaign evidence shared by managed and Native AOT serialization.</summary>
/// <remarks>Counts describe actual callbacks/check sites, including shrinking retries on failure.</remarks>
internal sealed class CampaignReport
{
    public bool Passed { get; init; }
    public int Seed { get; init; }
    public string CsCheckSeed { get; init; } = "";
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public double ElapsedSeconds { get; init; }
    public long Samples { get; init; }
    public long Assertions { get; init; }
    public long FixedOperations { get; init; }
    public long DecimalOperations { get; init; }
    public long Conversions { get; init; }
    public long RawCases { get; init; }
    public long Decimal32Cases { get; init; }
    public long Decimal64Cases { get; init; }
    public long Decimal128Cases { get; init; }
    public long[] FixedCaseCounts { get; init; } = [];
    public long[] DecimalCaseCounts { get; init; } = [];
    public string Runtime { get; init; } = "";
    public string Architecture { get; init; } = "";
    public int PointerBits { get; init; }
    public bool ServerGC { get; init; }
    public long AllocatedBytes { get; init; }
    public long LiveHeapBytes { get; init; }
    public int GC0 { get; init; }
    public int GC1 { get; init; }
    public int GC2 { get; init; }
    public string? Failure { get; init; }
}

/// <summary>Build-time BCL JSON metadata avoids runtime reflection and dynamic-code requirements.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CampaignReport))]
internal partial class CampaignJsonContext : JsonSerializerContext;
