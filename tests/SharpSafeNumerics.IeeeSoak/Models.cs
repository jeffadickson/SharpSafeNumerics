// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("SharpSafeNumerics.IeeeSoak.Tests")]

namespace SharpSafeNumerics.IeeeSoak;

/// <summary>A single format/mode comparison; inconclusive observations never count as numerical passes.</summary>
internal sealed record CaseObservation(string Outcome,string Format,string Operation,string Rounding,string Input1,string Input2,string Input3,string Expected,string Actual,int ExpectedFlags,int ActualFlags,string? Reason=null,int Assertions=3);
internal sealed class OracleMismatchException(CaseObservation observation):Exception("IEEE_MISMATCH "+System.Text.Json.JsonSerializer.Serialize(observation,SoakJsonContext.Default.CaseObservation))
{internal CaseObservation Observation{get;}=observation;}

internal sealed class CampaignState
{
    internal long Samples,Assertions,PassedChecks,FailedChecks,InconclusiveChecks,ShrinkEvaluations,Batches;
    internal bool FailureDetected;
    internal CaseObservation? FirstFailure,LastFailure;
    internal readonly Dictionary<string,long> FormatModeChecks=new(StringComparer.Ordinal),OperationChecks=new(StringComparer.Ordinal),InconclusiveReasons=new(StringComparer.Ordinal);
    internal void Record(CaseObservation observation)
    {
        Assertions+=observation.Assertions;
        Increment(FormatModeChecks,observation.Format+"/"+observation.Rounding);Increment(OperationChecks,observation.Operation);
        if(observation.Outcome=="Passed")PassedChecks++;
        else if(observation.Outcome=="Inconclusive"){InconclusiveChecks++;Increment(InconclusiveReasons,observation.Reason??"unspecified");}
        else{FailedChecks++;FailureDetected=true;FirstFailure??=observation;LastFailure=observation;}
    }
    private static void Increment(Dictionary<string,long> counts,string key)=>counts[key]=counts.GetValueOrDefault(key)+1;
    internal static string FinalStatus(long failed,long inconclusive,bool cancelled)=>failed>0?"Failed":inconclusive>0||cancelled?"Inconclusive":"Passed";
}

internal sealed record FailureReport(string Type,CaseObservation? FirstCase,CaseObservation? LastObservedShrinkingFailure,string? CsCheckMinimizedDiagnostic);
internal sealed record ProvenanceReport(string Runtime,string Architecture,int PointerBits,string CsCheckVersion,string PeterOVersion,string? DecimalAssemblySha256,string? RunnerAssemblySha256,string[] Scope,string[] Limitations);
internal sealed record CampaignReport
{
    public string Kind{get;init;}="ieeeDecimalSoak";
    public int SchemaVersion{get;init;}=1;
    public string? SourceRevision{get;init;}
    public string Status{get;init;}="Inconclusive";
    public int Seed{get;init;}
    public string CsCheckSeed{get;init;}="";
    public string LastBatchSeed{get;init;}="";
    public DateTimeOffset StartUtc{get;init;}
    public DateTimeOffset EndUtc{get;init;}
    public double ElapsedSeconds{get;init;}
    public int RequestedSeconds{get;init;}
    public double DeadlineOverrunSeconds{get;init;}
    public int Workers{get;init;}=1;
    public string CompletionReason{get;init;}="";
    public long Samples{get;init;}
    public long Assertions{get;init;}
    public long PassedChecks{get;init;}
    public long FailedChecks{get;init;}
    public long InconclusiveChecks{get;init;}
    public long ShrinkEvaluations{get;init;}
    public long Batches{get;init;}
    public Dictionary<string,long> FormatModeChecks{get;init;}=[];
    public Dictionary<string,long> OperationChecks{get;init;}=[];
    public Dictionary<string,long> InconclusiveReasons{get;init;}=[];
    public FailureReport? Failure{get;init;}
    public ProvenanceReport? Provenance{get;init;}
}
internal sealed record JournalEvent(string Kind,int SchemaVersion,string Event,DateTimeOffset Utc,long Samples,long Assertions,long InconclusiveChecks,string? BatchSeed,CaseObservation? Case=null,string? Detail=null);

[JsonSerializable(typeof(CampaignReport))]
[JsonSerializable(typeof(JournalEvent))]
[JsonSerializable(typeof(CaseObservation))]
internal partial class SoakJsonContext:JsonSerializerContext;
