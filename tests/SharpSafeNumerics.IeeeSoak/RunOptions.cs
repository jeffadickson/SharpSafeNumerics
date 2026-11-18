using System.Globalization;

namespace SharpSafeNumerics.IeeeSoak;

/// <summary>Validated chunk inputs; output paths are fresh and have existing parents.</summary>
internal sealed record RunOptions(int Seconds,int Seed,string SummaryPath,string? SourceRevision)
{
    internal static bool TryParse(string[] args,out RunOptions? options,out string error)
    {
        options=null;error="Usage: --seconds 1..7200 --seed positive-Int32 --summary fresh-file [--source-revision 40-hex]";
        if(args.Length%2!=0)return false;
        var values=new Dictionary<string,string>(StringComparer.Ordinal);
        for(int i=0;i<args.Length;i+=2)
        {
            if(args[i] is not("--seconds" or "--seed" or "--summary" or "--source-revision")||!values.TryAdd(args[i],args[i+1]))return false;
        }
        if(!values.TryGetValue("--seconds",out string? duration)||!int.TryParse(duration,NumberStyles.None,CultureInfo.InvariantCulture,out int seconds)||seconds is <1 or >7200)return false;
        if(!values.TryGetValue("--seed",out string? seedText)||!int.TryParse(seedText,NumberStyles.None,CultureInfo.InvariantCulture,out int seed)||seed<=0)return false;
        string? revision=values.GetValueOrDefault("--source-revision");
        if(revision is not null&&(revision.Length!=40||revision.Any(c=>!char.IsAsciiHexDigit(c))))return false;
        if(!values.TryGetValue("--summary",out string? summary)||string.IsNullOrWhiteSpace(summary))return false;
        try
        {
            summary=Path.GetFullPath(summary);
            if(!Directory.Exists(Path.GetDirectoryName(summary)))return false;
            foreach(string path in new[]{summary,summary+".events.jsonl",summary+".cscheck.log"})
                if(File.Exists(path)||Directory.Exists(path))return false;
        }
        catch(Exception ex)when(ex is ArgumentException or NotSupportedException or PathTooLongException){return false;}
        options=new(seconds,seed,summary,revision);error="";return true;
    }
}
