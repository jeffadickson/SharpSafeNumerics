// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using CsCheck;

namespace SharpSafeNumerics.IeeeSoak;

/// <summary>One-worker bounded chunks, with CsCheck generation/shrinking and durable streamed diagnostics.</summary>
internal static class CampaignRunner
{
    internal static int Run(string[] args)
    {
        if(!RunOptions.TryParse(args,out var parsed,out string error)){Console.Error.WriteLine(error);return 2;}
        RunOptions options=parsed!;
        try{return Execute(options);}catch(IOException ex){Console.Error.WriteLine(ex.Message);return 2;}catch(UnauthorizedAccessException ex){Console.Error.WriteLine(ex.Message);return 2;}
    }
    private static int Execute(RunOptions options)
    {
        // CreateNew closes the validation/write race and never replaces an existing result.
        using var summary=new FileStream(options.SummaryPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        using var journalFile=new FileStream(options.SummaryPath+".events.jsonl",FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        using var journal=new StreamWriter(journalFile){AutoFlush=true};
        using var diagnostics=new StreamWriter(new FileStream(options.SummaryPath+".cscheck.log",FileMode.CreateNew,FileAccess.Write,FileShare.Read)){AutoFlush=true};
        var state=new CampaignState();var clock=Stopwatch.StartNew();DateTimeOffset start=DateTimeOffset.UtcNow;
        string firstSeed=new PCG(1,(ulong)options.Seed).ToString(),lastSeed=firstSeed;string? failure=null;bool cancelled=false;
        using var cancellation=new CancellationTokenSource();
        ConsoleCancelEventHandler cancel=(_,e)=>{e.Cancel=true;cancellation.Cancel();};Console.CancelKeyPress+=cancel;
        void Event(string name,CaseObservation? observation=null,string? detail=null)
        {journal.WriteLine(JsonSerializer.Serialize(new JournalEvent("ieeeDecimalSoakCheckpoint",1,name,DateTimeOffset.UtcNow,state.Samples,state.Assertions,state.InconclusiveChecks,lastSeed,observation,detail),SoakJsonContext.Default.JournalEvent));}
        Event("Started",detail:CaseVerifier.Describe((UInt128)options.Seed,0));double nextCheckpoint=60;
        try
        {
            while(clock.Elapsed.TotalSeconds<options.Seconds&&!cancellation.IsCancellationRequested)
            {
                lastSeed=new PCG(checked((uint)state.Batches+1),(ulong)options.Seed).ToString();state.Batches++;
                Gen.Select(Gen.UInt128,Gen.UInt128).Sample(pair=>
                {
                    if(!state.FailureDetected&&(clock.Elapsed.TotalSeconds>=options.Seconds||cancellation.IsCancellationRequested))return;
                    if(state.FailureDetected)state.ShrinkEvaluations++;else state.Samples++;
                    CaseVerifier.Verify(pair.Item1,pair.Item2,observation=>
                    {
                        state.Record(observation);
                        if(observation.Outcome!="Passed")Event(observation.Outcome,observation);
                    });
                },seed:lastSeed,iter:64,threads:1,writeLine:diagnostics.WriteLine,print:pair=>$"a=0x{pair.Item1:X32}; b=0x{pair.Item2:X32}; {CaseVerifier.Describe(pair.Item1,pair.Item2)}");
                if(clock.Elapsed.TotalSeconds>=nextCheckpoint){Event("Checkpoint");nextCheckpoint+=60;}
            }
            cancelled=cancellation.IsCancellationRequested;
        }
        catch(Exception ex){failure=ex.ToString();diagnostics.WriteLine(failure);Event("Failure",state.LastFailure,failure);}
        finally{Console.CancelKeyPress-=cancel;}
        clock.Stop();
        bool harnessFailure=failure is not null&&!state.FailureDetected;
        string status=CampaignState.FinalStatus(state.FailedChecks+(harnessFailure?1:0),state.InconclusiveChecks,cancelled||state.Samples==0);
        var report=new CampaignReport
        {
            SourceRevision=options.SourceRevision,Status=status,Seed=options.Seed,CsCheckSeed=firstSeed,LastBatchSeed=lastSeed,StartUtc=start,EndUtc=DateTimeOffset.UtcNow,ElapsedSeconds=clock.Elapsed.TotalSeconds,RequestedSeconds=options.Seconds,DeadlineOverrunSeconds=Math.Max(0,clock.Elapsed.TotalSeconds-options.Seconds),CompletionReason=failure is not null?state.FailureDetected?"Counterexample":"HarnessError":cancelled?"Cancelled":"DurationReached",
            Samples=state.Samples,Assertions=state.Assertions,PassedChecks=state.PassedChecks,FailedChecks=state.FailedChecks,InconclusiveChecks=state.InconclusiveChecks,ShrinkEvaluations=state.ShrinkEvaluations,Batches=state.Batches,FormatModeChecks=state.FormatModeChecks,OperationChecks=state.OperationChecks,InconclusiveReasons=state.InconclusiveReasons,
            Failure=failure is null?null:new(state.FailureDetected?"Counterexample":"HarnessError",state.FirstFailure,state.LastFailure,failure),
            Provenance=new(RuntimeInformation.FrameworkDescription,RuntimeInformation.ProcessArchitecture.ToString(),IntPtr.Size*8,"5.0.1","1.8.2",Hash("SharpSafeNumerics.Decimal.dll"),Hash("SharpSafeNumerics.IeeeSoak.dll"),
                ["decimal32/64/128; five rounding directions", "Add/Subtract/Multiply/Divide/FMA/Sqrt/Quantize/IEEE remainder: independent PeterO values and exact-integer product rounding, finite cohorts, five flags", "BigInteger->decimal; decimal->Int64; binary64<->decimal", "Exp/Log/ExpM1/LogP1: adaptive 160-1280-digit precision agreement, then target rounding; input magnitude<=20 (Log positive<=1000), cancellation exponent<=80"],
                ["Finite randomized evidence, not whole-domain proof", "NaN payload/sign propagation alternatives are outside comparison scope; quieting and flags checked", "Adaptive elementary cross-precision agreement is not a rigorous outward enclosure; failure to stabilize through 1280 digits is Inconclusive", "Binary tininess-only policy disagreements are Inconclusive", "No trigonometric oracle or whole-IEEE operation coverage", "Deadline is checked between bounded cases; shrinking can finish later", "Samples count generated executions; ShrinkEvaluations and aggregate comparisons include shrinking separately"])
        };
        Event("Completed",detail:status);journal.Flush();journalFile.Flush(true);
        JsonSerializer.Serialize(summary,report,SoakJsonContext.Default.CampaignReport);summary.Flush(true);
        Console.WriteLine(JsonSerializer.Serialize(report,SoakJsonContext.Default.CampaignReport));
        return status=="Passed"?0:status=="Inconclusive"?3:1;
    }
    private static string? Hash(string file)
    {string path=Path.Combine(AppContext.BaseDirectory,file);if(!File.Exists(path))return null;using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
}
