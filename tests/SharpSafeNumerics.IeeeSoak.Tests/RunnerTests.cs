// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Text.Json;
using CsCheck;
using SharpSafeNumerics.IeeeSoak;
using Xunit;

namespace SharpSafeNumerics.IeeeSoak.Tests;

public sealed class RunnerTests
{
    private static string FreshPath()
    {
        string root=Environment.GetEnvironmentVariable("CODEX_ARTIFACT_ROOT")??"E:\\Artifacts";
        if(!Directory.Exists(root))throw new DirectoryNotFoundException(root);
        string path=Path.Combine(root,"SharpSafeNumerics","ieee-soak-tests",Guid.NewGuid().ToString("N"));
        if(!Directory.Exists(path))Directory.CreateDirectory(path);
        return Path.Combine(path,"summary.json");
    }

    [Fact]
    public void MandatoryOptionsAndPinnedRevisionAreRetained()
    {
        string path=FreshPath(),revision=new('a',40);
        Assert.True(RunOptions.TryParse(["--seconds","7200","--seed","2147483647","--summary",path,"--source-revision",revision],out var options,out _));
        Assert.Equal(7200,options!.Seconds);Assert.Equal(int.MaxValue,options.Seed);Assert.Equal(revision,options.SourceRevision);Assert.Equal(path,options.SummaryPath);
    }

    [Theory]
    [InlineData("0")][InlineData("7201")][InlineData("-1")][InlineData("1.5")][InlineData("1e2")][InlineData(" 1")][InlineData("+1")][InlineData("2147483648")]
    public void InvalidDurationIsRejected(string seconds)=>Assert.False(RunOptions.TryParse(["--seconds",seconds,"--seed","1","--summary",FreshPath()],out _,out _));

    [Fact]
    public void MissingDuplicateUnknownAndMalformedArgumentsAreRejected()
    {
        string path=FreshPath();
        foreach(var args in new string[][] {[],["--seconds"],["--seconds","1"],["--seed","0","--seconds","1","--summary",path],["--seconds","1","--seed","1","--summary",path,"--seed","2"],["--seconds","1","--seed","1","--summary",path,"--unknown","2"],["--seconds","1","--seed","1","--summary",path,"--source-revision","bad"]})
            Assert.False(RunOptions.TryParse(args,out _,out _));
        Assert.False(RunOptions.TryParse(["--seconds","1","--seed","1","--summary",path,"--source-revision",new string('g',40)],out _,out _));
        Assert.False(RunOptions.TryParse(["--seconds","1","--seed","1","--summary","\0"],out _,out _));
    }

    [Fact]
    public void ExistingOutputsAndMissingParentsAreRejectedWithoutOverwriting()
    {
        string path=FreshPath();File.WriteAllText(path,"sentinel");
        Assert.False(RunOptions.TryParse(["--seconds","1","--seed","1","--summary",path],out _,out _));Assert.Equal("sentinel",File.ReadAllText(path));
        Assert.False(RunOptions.TryParse(["--seconds","1","--seed","1","--summary",Path.Combine(path,"missing","result.json")],out _,out _));
        Assert.False(RunOptions.TryParse(["--seconds","1","--seed","1","--summary",Path.GetDirectoryName(path)!],out _,out _));
        string fresh=FreshPath();File.WriteAllText(fresh+".events.jsonl","journal");
        Assert.False(RunOptions.TryParse(["--seconds","1","--seed","1","--summary",fresh],out _,out _));
    }

    [Fact]
    public void InconclusiveIsNotReportedAsPassAndFailureDominates()
    {
        Assert.Equal("Passed",CampaignState.FinalStatus(0,0,false));
        Assert.Equal("Inconclusive",CampaignState.FinalStatus(0,1,false));
        Assert.Equal("Inconclusive",CampaignState.FinalStatus(0,0,true));
        Assert.Equal("Failed",CampaignState.FinalStatus(1,10,true));
    }

    [Fact]
    public void GeneratedRecipesAreDeterministicAndExerciseEveryOperationAndFormat()
    {
        var seen=new HashSet<string>();
        foreach(int format in new[]{0,1,2})
        for(int operation=0;operation<CaseVerifier.OperationCount;operation++)
        {
            var first=CaseVerifier.Describe((UInt128)format,(UInt128)operation);
            Assert.Equal(first,CaseVerifier.Describe((UInt128)format,(UInt128)operation));
            seen.Add(first);
            var observations=new List<CaseObservation>();
            CaseVerifier.Verify((UInt128)format,(UInt128)operation,observations.Add);
            Assert.Equal(5,observations.Count);Assert.All(observations,o=>Assert.NotEqual("Failed",o.Outcome));
        }
        Assert.Equal(3*CaseVerifier.OperationCount,seen.Count);
    }

    [Fact]
    public void CsCheckInputCampaignHasReplayableSeedAndNoFalsePass()
    {
        string seed=new PCG(1,7542019).ToString();int count=0;
        Gen.Select(Gen.UInt128,Gen.UInt128).Sample(pair=>
        {
            CaseVerifier.Verify(pair.Item1,pair.Item2,_=>{});count++;
        },seed:seed,iter:100,threads:1,writeLine:_=>{});
        Assert.Equal(100,count);
    }

    [Fact]
    public void PreserveExactPropertyCounterexampleDiagnostics()
    {
        var rows=new List<CaseObservation>();CaseVerifier.Verify(UInt128.Parse("3209834610072289357"),UInt128.Parse("54444120840264849166139"),rows.Add);
        Assert.Equal(5,rows.Count);
    }

    [Fact]
    public void IeeeRemainderDoesNotInheritGdaQuotientPrecisionLimit()
    {
        UInt128 a=UInt128.Parse("0000000000000000037949EB1586B642",System.Globalization.NumberStyles.HexNumber);
        UInt128 b=UInt128.Parse("000026CFB917E113FE77770BD21254D7",System.Globalization.NumberStyles.HexNumber);
        var rows=new List<CaseObservation>();CaseVerifier.Verify(a,b,rows.Add);
        Assert.Equal(5,rows.Count);Assert.All(rows,row=>{Assert.Equal("Passed",row.Outcome);Assert.Equal("Remainder",row.Operation);});
    }

    [Fact]
    public void Decimal32SubnormalProductUsesExactProductBeforeFinalRounding()
    {
        UInt128 a=UInt128.Parse("00000000000000000000000000006354",System.Globalization.NumberStyles.HexNumber);
        UInt128 b=UInt128.Parse("000000004005ED9697EC90C50345FF72",System.Globalization.NumberStyles.HexNumber);
        var rows=new List<CaseObservation>();

        CaseVerifier.Verify(a,b,rows.Add);

        Assert.Equal(5,rows.Count);
        Assert.All(rows,row=>{Assert.Equal("Passed",row.Outcome);Assert.Equal("Multiply",row.Operation);});
        Assert.Equal("1.0601E-97",rows[0].Expected);
    }

    [Fact]
    public void ElementaryReferenceRaisesPrecisionUntilTargetRoundingStabilizes()
    {
        UInt128 a=UInt128.Parse("000017CB80B803677A2C364C0BEE50E1",System.Globalization.NumberStyles.HexNumber);
        UInt128 b=UInt128.Parse("0000000000000208F481C73C07A0621E",System.Globalization.NumberStyles.HexNumber);
        var rows=new List<CaseObservation>();

        CaseVerifier.Verify(a,b,rows.Add);

        Assert.Equal(5,rows.Count);
        Assert.All(rows,row=>{Assert.Equal("Passed",row.Outcome);Assert.Equal("ExpM1",row.Operation);});
        Assert.Equal("1.000001E-80",rows.Single(row=>row.Rounding==nameof(IeeeDecimalRounding.TowardPositive)).Expected);
    }

    [Fact]
    public void FailureAndAmbiguityAccountingPreservesDiagnostics()
    {
        var state=new CampaignState();
        var pass=new CaseObservation("Passed","Decimal32","Add","ToNearestEven","1","2","","3","3",0,0);
        var ambiguous=pass with{Outcome="Inconclusive",Reason="rounding ambiguity",Assertions=0};
        var first=pass with{Outcome="Failed",Expected="3",Actual="4"};var last=first with{Actual="5"};
        foreach(var observation in new[]{pass,ambiguous,first,last})state.Record(observation);
        Assert.Equal(1,state.PassedChecks);Assert.Equal(1,state.InconclusiveChecks);Assert.Equal(2,state.FailedChecks);Assert.Equal(9,state.Assertions);
        Assert.Equal(first,state.FirstFailure);Assert.Equal(last,state.LastFailure);Assert.True(state.FailureDetected);
        Assert.Equal(4,state.OperationChecks["Add"]);Assert.Equal(1,state.InconclusiveReasons["rounding ambiguity"]);
        var error=new OracleMismatchException(first);Assert.Contains("IEEE_MISMATCH",error.Message);Assert.Equal(first,error.Observation);
    }

    [Fact]
    public void BinaryNaNsAndInfinityInputsAreNotConfusedWithApiErrors()
    {
        foreach(UInt128 format in new UInt128[]{0,1,2})
        foreach(ulong bits in new[]{0x7ff800000000000aUL,0x7ff8fffffffffffaUL,0x7ff0fffffffffffaUL})
        {
            var rows=new List<CaseObservation>();CaseVerifier.Verify(format,bits,rows.Add);
            Assert.Equal(5,rows.Count);Assert.All(rows,row=>Assert.Equal("Passed",row.Outcome));
        }
    }

    [Fact]
    public void AShortCampaignWritesFinalSchemaAndCheckpointJournal()
    {
        string path=FreshPath();int exit=CampaignRunner.Run(["--seconds","1","--seed","7542020","--summary",path,"--source-revision",new string('b',40)]);
        Assert.Contains(exit,new[]{0,3});
        using var report=JsonDocument.Parse(File.ReadAllText(path));var root=report.RootElement;
        Assert.Equal("ieeeDecimalSoak",root.GetProperty("Kind").GetString());Assert.Equal(1,root.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal(new string('b',40),root.GetProperty("SourceRevision").GetString());Assert.True(root.GetProperty("Samples").GetInt64()>0);
        Assert.Equal(exit==0?"Passed":"Inconclusive",root.GetProperty("Status").GetString());
        string[] events=File.ReadAllLines(path+".events.jsonl");Assert.True(events.Length>=2);
        foreach(string line in events)using(JsonDocument.Parse(line)){}
        Assert.Equal(2,CampaignRunner.Run(["--seconds","1","--seed","1","--summary",path]));
    }
}
