<#
.SYNOPSIS
Run bounded Windows x64 qualification in an isolated checkout and push reports only.
.DESCRIPTION
Uses a unique results branch, preserves other checkouts, keeps bulk artifacts local,
and reserves five minutes for committing/pushing results. No CI settings are changed.
#>
[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$Repository = 'jeffadickson/SharpSafeNumerics',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._/-]*$')][string]$SourceRef = 'wide-arithmetic-policy',
    [ValidateRange(1,2147483647)][int]$IssueNumber = 1,
    [ValidateRange(0.25,24)][double]$Hours = 2,
    [ValidateRange(0,1440)][int]$SoakMinutes = 0,
    [ValidateRange(1,64)][int]$MaxParallelism = 12,
    [string]$CheckoutRoot,
    [string]$ArtifactRoot,
    [switch]$IsolatedNuGetCache,
    [switch]$SkipNativeToolsInstall,
    [switch]$PlanOnly,
    [Parameter(DontShow=$true)][string]$DeadlineUtc
)
$ErrorActionPreference = 'Stop'
$started = [DateTime]::UtcNow
$absoluteDeadline=if($DeadlineUtc){[DateTime]::Parse($DeadlineUtc,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind)}else{$started.AddHours($Hours)}
$documents = [Environment]::GetFolderPath('MyDocuments')
if (-not $CheckoutRoot) { $CheckoutRoot = Join-Path $documents 'GitHub' }
if (-not $ArtifactRoot) {
    if ($env:CODEX_ARTIFACT_ROOT) { $ArtifactRoot = $env:CODEX_ARTIFACT_ROOT }
    elseif (Test-Path -LiteralPath 'E:\' -PathType Container) { $ArtifactRoot = 'E:\Artifacts' }
    else { $ArtifactRoot = Join-Path $documents 'QualificationArtifacts' }
}
$machine = [regex]::Replace($env:COMPUTERNAME, '[^A-Za-z0-9-]', '-').ToLowerInvariant()
$runId = $started.ToString('yyyyMMddTHHmmssZ') + '-' + $machine + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)
$checkout = Join-Path $CheckoutRoot ('SharpSafeNumerics-qualification-' + $runId)
$run = Join-Path $ArtifactRoot ('SharpSafeNumerics\workstation\' + $runId)
$branch = 'qualification/issue-' + $IssueNumber + '/' + $runId
$issueUrl='https://github.com/'+$Repository+'/issues/'+$IssueNumber
$toolRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'SharpSafeNumericsQualification\tools'
if ($PlanOnly) {
    [pscustomobject]@{ Repository=$Repository; SourceRef=$SourceRef; IssueNumber=$IssueNumber; IssueUrl=$issueUrl; Checkout=$checkout; Artifacts=$run; ResultsBranch=$branch; Hours=$Hours; DeadlineUtc=$absoluteDeadline.ToString('O'); SoakMinutesPerMode=$SoakMinutes; MaxParallelism=$MaxParallelism; ToolRoot=$toolRoot; IsolatedNuGetCache=$IsolatedNuGetCache.IsPresent } | ConvertTo-Json
    return
}
if ($env:OS -ne 'Windows_NT' -or $env:PROCESSOR_ARCHITECTURE -ne 'AMD64') { throw 'This runner requires Windows x64.' }
function Ensure-Directory([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Container) { return }
    $parent = Split-Path -Parent $Path
    if (-not $parent -or $parent -eq $Path) { throw "Missing filesystem root: $Path" }
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { Ensure-Directory $parent }
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { New-Item -ItemType Directory -Path $Path | Out-Null }
}
function Get-QualificationStatus([object[]]$Checks, [string]$FatalError, [string]$SourceChanges) {
    # A successful upload is separate from qualification: failed gates must remain
    # visibly failed even when no orchestration exception occurred.
    if ($FatalError -or $SourceChanges) { return 'Failed' }
    if (-not $Checks -or $Checks.Count -eq 0) { return 'Incomplete' }
    if (@($Checks | Where-Object { $_.Status -notin @('Passed','ProbeRejected','SkippedBudget') }).Count) { return 'Failed' }
    if (@($Checks | Where-Object Status -eq 'SkippedBudget').Count) { return 'Incomplete' }
    return 'Passed'
}
Ensure-Directory $run
Ensure-Directory $toolRoot
$downloads = New-Object 'System.Collections.Generic.List[object]'
function Network-Seconds {
    $remaining=($absoluteDeadline.AddMinutes(-5)-[DateTime]::UtcNow).TotalSeconds
    if ($remaining -lt 1) {throw 'Download deadline expired'}
    return [int][Math]::Min(900,[Math]::Floor($remaining))
}
# HttpClient's cancellation token covers the body transfer as well as the headers.
# Stream directly to disk and publish the cached filename only after completion.
function Read-OfficialUrl([string]$Url,[string]$Destination) {
    Add-Type -AssemblyName System.Net.Http
    $client=[Net.Http.HttpClient]::new()
    $client.DefaultRequestHeaders.UserAgent.ParseAdd('SharpSafeNumerics-Qualification/1.0')
    $cts=[Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds((Network-Seconds)))
    $response=$null; $stream=$null; $temp=$null
    try {
        $response=$client.GetAsync($Url,[Net.Http.HttpCompletionOption]::ResponseHeadersRead,$cts.Token).GetAwaiter().GetResult()
        [void]$response.EnsureSuccessStatusCode()
        $inputStream=$response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        if($Destination){$temp=$Destination+'.'+[guid]::NewGuid().ToString('N')+'.partial';$stream=[IO.File]::Create($temp)}
        else{$stream=[IO.MemoryStream]::new()}
        [void]$inputStream.CopyToAsync($stream,81920,$cts.Token).GetAwaiter().GetResult()
        if($Destination){$stream.Dispose();$stream=$null;[IO.File]::Move($temp,$Destination);$temp=$null}
        else{return [Text.Encoding]::UTF8.GetString($stream.ToArray())}
    } finally {
        if($stream){$stream.Dispose()};if($response){$response.Dispose()};$client.Dispose();$cts.Dispose()
        if($temp -and (Test-Path -LiteralPath $temp -PathType Leaf)){Remove-Item -LiteralPath $temp}
    }
}
function Download([string]$Url,[string]$Destination) {
    $parent = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { Ensure-Directory $parent }
    Read-OfficialUrl $Url $Destination
    $downloads.Add([pscustomobject]@{Url=$Url;Sha256=(Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash})
}
function Portable-Tool([string]$Repo,[string]$AssetPattern,[string]$Executable) {
    $existing = Get-Command $Executable -ErrorAction SilentlyContinue
    if ($existing) { return $existing.Source }
    $release = Read-OfficialUrl ('https://api.github.com/repos/' + $Repo + '/releases/latest') | ConvertFrom-Json
    $asset = @($release.assets | Where-Object { $_.name -match $AssetPattern }) | Select-Object -First 1
    if (-not $asset) { throw "No official Windows x64 asset found for $Repo" }
    $directory = Join-Path $toolRoot (($Repo -replace '/','-') + '-' + $release.tag_name)
    $binary=if(Test-Path -LiteralPath $directory -PathType Container){Get-ChildItem -LiteralPath $directory -Filter $Executable -Recurse -File | Select-Object -First 1}
    if (-not $binary) {
        $archive = Join-Path $run $asset.name
        Download $asset.browser_download_url $archive
        Ensure-Directory $directory
        Expand-Archive -LiteralPath $archive -DestinationPath $directory -Force
    }
    $binary = Get-ChildItem -LiteralPath $directory -Filter $Executable -Recurse -File | Select-Object -First 1
    if (-not $binary) { throw "Portable $Executable missing in $directory" }
    return $binary.FullName
}
# Bootstrap PowerShell 7 without a machine-wide installation. Process.ArgumentList
# and async exit notifications below require the modern runtime.
if ($PSVersionTable.PSVersion -lt [Version]'7.2') {
    $pwsh = Portable-Tool 'PowerShell/PowerShell' '^PowerShell-[0-9.]+-win-x64\.zip$' 'pwsh.exe'
    $forward = @('-NoProfile','-File',$PSCommandPath)
    foreach ($entry in $PSBoundParameters.GetEnumerator()) {
        if ($entry.Value -is [Management.Automation.SwitchParameter]) { if ($entry.Value.IsPresent) { $forward += '-' + $entry.Key } }
        else { $forward += '-' + $entry.Key; $forward += [string]$entry.Value }
    }
    if(-not $PSBoundParameters.ContainsKey('DeadlineUtc')){$forward+=@('-DeadlineUtc',$absoluteDeadline.ToString('O'))}
    & $pwsh @forward
    exit $LASTEXITCODE
}
$deadline = $absoluteDeadline
$workDeadline = $deadline.AddMinutes(-5)
$records = [Collections.Generic.List[object]]::new()
$active = [Collections.Generic.List[object]]::new()
$savedEnvironment = @{}
foreach ($key in @('PATH','DOTNET_ROOT','NUGET_PACKAGES','MSBUILD_EXE_PATH','DOTNET_EnableAVX2','DOTNET_EnableHWIntrinsic','GIT_AUTHOR_NAME','GIT_AUTHOR_EMAIL','GIT_COMMITTER_NAME','GIT_COMMITTER_EMAIL','GIT_TERMINAL_PROMPT','GH_PROMPT_DISABLED','GCM_INTERACTIVE')) { $savedEnvironment[$key]=[Environment]::GetEnvironmentVariable($key,'Process') }
$git = $null; $gh = $null; $sourceSha = $null; $fatal = $null; $pushed = $false; $counter = 0; $qualificationStatus = 'Incomplete'
function Initialize-ProcessJob {
    if('QualificationJob' -as [type]){return}
    # Create the child suspended, attach it to a kill-on-close Windows job, then
    # resume. This also contains grandchildren created by very short-lived parents.
    # Real file handles avoid waiting for pipe EOF from a surviving descendant.
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public sealed class QualificationJob : IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct Security {
        public int size; public IntPtr descriptor; public int inherit;
    }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Startup {
        public int size; public string reserved, desktop, title;
        public int x,y,width,height,cols,rows,fill,flags;
        public short show, reservedSize; public IntPtr reservedData,stdin,stdout,stderr;
    }
    [StructLayout(LayoutKind.Sequential)] struct Child {
        public IntPtr process, thread; public int pid, tid;
    }
    [DllImport("kernel32",SetLastError=true,CharSet=CharSet.Unicode)]
    static extern SafeFileHandle CreateJobObject(IntPtr security,string name);
    [DllImport("kernel32",SetLastError=true)] static extern bool SetInformationJobObject(SafeFileHandle job,int kind,IntPtr info,int size);
    [DllImport("kernel32",SetLastError=true)] static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
    [DllImport("kernel32",SetLastError=true)] static extern bool TerminateProcess(IntPtr process,uint code);
    [DllImport("kernel32",SetLastError=true)] static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32",SetLastError=true,CharSet=CharSet.Unicode)] static extern SafeFileHandle CreateFile(string path,uint access,uint share,ref Security sa,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool CreateProcess(string app,StringBuilder cmd,IntPtr sa,IntPtr ta,bool inherit,uint flags,IntPtr env,string cwd,ref Startup startup,out Child child);
    readonly SafeFileHandle job;
    public Process Process {get; private set;}
    QualificationJob(SafeFileHandle handle){job=handle;}
    public void Dispose(){job.Dispose();}
    static void Check(bool ok){if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error());}
    // Windows CRT argv convention. Quote every argument, doubling backslashes
    // before embedded quotes and before the closing quote (including empty args).
    static string Quote(string value){
        var result=new StringBuilder("\""); int slashes=0;
        foreach(char ch in value){
            if(ch=='\\'){slashes++;continue;}
            if(ch=='"')result.Append('\\',slashes*2+1);else result.Append('\\',slashes);
            result.Append(ch);slashes=0;
        }
        return result.Append('\\',slashes*2).Append('"').ToString();
    }
    public static QualificationJob Start(ProcessStartInfo psi,string stdout,string stderr){
        if(IntPtr.Size!=8)throw new PlatformNotSupportedException("x64 required");
        var handle=CreateJobObject(IntPtr.Zero,null); Check(!handle.IsInvalid);
        var job=new QualificationJob(handle); Child child=new Child();
        // x64 JOBOBJECT_EXTENDED_LIMIT_INFORMATION is 144 bytes; LimitFlags at offset 16.
        // KILL_ON_JOB_CLOSE is the only enabled limit. Memory and CPU remain OS-managed.
        IntPtr info=Marshal.AllocHGlobal(144), environment=IntPtr.Zero;
        try{
            Marshal.Copy(new byte[144],0,info,144);Marshal.WriteInt32(info,16,0x2000);
            Check(SetInformationJobObject(handle,9,info,144));
            var sa=new Security{size=Marshal.SizeOf<Security>(),inherit=1};
            using(var input=CreateFile("NUL",0x80000000,3,ref sa,3,0,IntPtr.Zero))
            using(var output=CreateFile(stdout,0x40000000,3,ref sa,2,0,IntPtr.Zero))
            using(var error=CreateFile(stderr,0x40000000,3,ref sa,2,0,IntPtr.Zero)){
                Check(!input.IsInvalid && !output.IsInvalid && !error.IsInvalid);
                var si=new Startup{size=Marshal.SizeOf<Startup>(),flags=0x100,stdin=input.DangerousGetHandle(),stdout=output.DangerousGetHandle(),stderr=error.DangerousGetHandle()};
                var cmd=new StringBuilder(Quote(psi.FileName));
                foreach(string arg in psi.ArgumentList)cmd.Append(' ').Append(Quote(arg));
                string vars=string.Join("\0",psi.Environment.OrderBy(p=>p.Key,StringComparer.OrdinalIgnoreCase).Select(p=>p.Key+"="+p.Value))+"\0\0";
                environment=Marshal.StringToHGlobalUni(vars);
                // CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT | CREATE_SUSPENDED.
                Check(CreateProcess(psi.FileName,cmd,IntPtr.Zero,IntPtr.Zero,true,0x08000404,environment,psi.WorkingDirectory,ref si,out child));
                Check(AssignProcessToJobObject(handle,child.process));
                job.Process=Process.GetProcessById(child.pid);
                // Open a managed process handle prior to resume, even if child exits immediately.
                var processHandle=job.Process.Handle;
                Check(ResumeThread(child.thread)!=uint.MaxValue);
            }
            return job;
        }catch{if(child.process!=IntPtr.Zero)TerminateProcess(child.process,1);job.Dispose();if(job.Process!=null)job.Process.Dispose();throw;}
        finally{if(child.thread!=IntPtr.Zero)CloseHandle(child.thread);if(child.process!=IntPtr.Zero)CloseHandle(child.process);Marshal.FreeHGlobal(info);if(environment!=IntPtr.Zero)Marshal.FreeHGlobal(environment);}
    }
}
'@
}
function Start-Step([string]$Name,[string]$Exe,[string[]]$Arguments,[string]$WorkingDirectory,[int]$LimitSeconds=1800,[int]$Weight=1,[hashtable]$Environment=@{}) {
    if (-not (Test-Path -LiteralPath $WorkingDirectory -PathType Container)) { throw "Missing working directory: $WorkingDirectory" }
    if ([DateTime]::UtcNow -ge $workDeadline) {
        $records.Add([pscustomobject]@{Name=$Name;Status='SkippedBudget';ExitCode=$null;Command=@($Exe)+$Arguments})
        return $null
    }
    $script:counter++
    $prefix = '{0:D2}-{1}' -f $script:counter,($Name -replace '[^A-Za-z0-9-]','-')
    $stdout = Join-Path $run ($prefix + '.stdout.log'); $stderr = Join-Path $run ($prefix + '.stderr.log')
    $psi = [Diagnostics.ProcessStartInfo]::new($Exe)
    $psi.WorkingDirectory=$WorkingDirectory; $psi.UseShellExecute=$false; $psi.CreateNoWindow=$true
    foreach ($argument in $Arguments) { $psi.ArgumentList.Add($argument) }
    foreach ($key in $Environment.Keys) { $psi.Environment[$key]=[string]$Environment[$key] }
    Initialize-ProcessJob
    try{$owner=[QualificationJob]::Start($psi,$stdout,$stderr)}catch{
        $records.Add([pscustomobject]@{Name=$Name;Status='LaunchFailed';Detail=$_.Exception.Message;Command=@($Exe)+$Arguments})
        throw
    }
    $process=$owner.Process
    $begin=[DateTime]::UtcNow
    $context=[pscustomobject]@{Name=$Name;Process=$process;Owner=$owner;ExitTask=$process.WaitForExitAsync();Stdout=$stdout;Stderr=$stderr;Start=$begin;End=$begin.AddSeconds($LimitSeconds);Weight=$Weight;Command=@($Exe)+$Arguments;Environment=$Environment}
    Write-Host "Started $Name; logs: $stdout"
    return $context
}
function Complete-Step($Context,[switch]$Cancel,[switch]$Probe) {
    if (-not $Context) { return }
    $end = if ($Context.End -lt $workDeadline) { $Context.End } else { $workDeadline }
    $milliseconds=[int][Math]::Max(0,($end-[DateTime]::UtcNow).TotalMilliseconds)
    try{$finished=(-not $Cancel) -and $Context.Process.WaitForExit($milliseconds)}
    finally{$Context.Owner.Dispose()}
    $code=if($finished){$Context.Process.ExitCode}else{-1}
    $status=if($Cancel){'Cancelled'}elseif(-not $finished){'TimedOut'}elseif($code -eq 0){'Passed'}elseif($Probe){'ProbeRejected'}else{'Failed'}
    $peak=$null; try{$peak=$Context.Process.PeakWorkingSet64}catch{}
    $records.Add([pscustomobject]@{Name=$Context.Name;Status=$status;ExitCode=$code;StartUtc=$Context.Start.ToString('O');ElapsedSeconds=([DateTime]::UtcNow-$Context.Start).TotalSeconds;Command=$Context.Command;Environment=$Context.Environment;Stdout=$Context.Stdout;Stderr=$Context.Stderr;PeakWorkingSetBytes=$peak})
    Write-Host "$($Context.Name): $status (exit $code)"
    $Context.Process.Dispose()
    return $code
}
function Invoke-Step([string]$Name,[string]$Exe,[string[]]$Arguments,[string]$WorkingDirectory,[int]$LimitSeconds=1800) {
    $context=Start-Step $Name $Exe $Arguments $WorkingDirectory $LimitSeconds
    if ($context) { return Complete-Step $context }
    return -1
}
function Read-Step([string]$Name,[string]$Exe,[string[]]$Arguments,[string]$WorkingDirectory,[int]$LimitSeconds=90,[switch]$Probe,[hashtable]$Environment=@{}){
    $context=Start-Step $Name $Exe $Arguments $WorkingDirectory $LimitSeconds -Environment $Environment
    if(-not $context){throw "$Name exceeded its budget. See local logs under $run."}
    $code=Complete-Step $context -Probe:$Probe
    if($Probe -and $records[-1].Status -eq 'ProbeRejected'){return}
    if($code -ne 0){throw "$Name failed or exceeded its budget. See local logs under $run."}
    # Kill-on-close termination can release a descendant's inherited file handle
    # just after the parent exits. Permit that writer while reading flushed output.
    $stream=[IO.FileStream]::new($context.Stdout,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $reader=[IO.StreamReader]::new($stream)
    try{return $reader.ReadToEnd().TrimEnd()}finally{$reader.Dispose()}
}
function Set-QualificationNuGetCache([string]$ArtifactDirectory,[switch]$Isolated){
    # Preserve NuGet.Config, the caller's override, and NuGet's user cache unless requested.
    if($Isolated){
        $cache=Join-Path $ArtifactDirectory 'nuget'
        Ensure-Directory $cache
        $env:NUGET_PACKAGES=$cache
    }
}
function Select-QualificationDotnet([string[]]$Candidates,[string]$WorkingDirectory){
    foreach($exe in ($Candidates | Where-Object {$_} | Select-Object -Unique)){
        if(-not(Test-Path -LiteralPath $exe -PathType Leaf)){continue}
        # The SDK resolver processes the checkout's global.json, including its patch roll-forward.
        $version=Read-Step 'probe-sdk-version' $exe @('--version') $WorkingDirectory -Probe
        if(-not $version -or $version -notmatch '^\d+\.\d+\.\d+(?:-[\w.-]+)?$'){continue}
        $root=Split-Path -Parent $exe
        $msbuild=Join-Path $root "sdk/$version/MSBuild.dll"
        if(-not(Test-Path -LiteralPath $msbuild -PathType Leaf)){continue}
        $info=Read-Step 'probe-sdk-info' $exe @('--info') $WorkingDirectory -Probe -Environment @{DOTNET_CLI_UI_LANGUAGE='en-US'}
        if($info -notmatch '(?mi)^\s*Architecture:\s*x64\s*$'){continue}
        return [pscustomobject]@{Executable=$exe;Root=$root;Version=$version;MSBuild=$msbuild;Info=$info}
    }
}
function Get-MissingToolRuntimes([string]$RuntimeList){
    # The pinned qualification tools target .NET 8.0 and 10.0 and accept stable patches.
    foreach($channel in @('8.0','10.0')){
        $pattern='(?m)^Microsoft\.NETCore\.App '+[regex]::Escape($channel)+'\.\d+\s+\['
        if($RuntimeList -notmatch $pattern){$channel}
    }
}
function Test-DirectoryWritable([string]$Path){
    if(-not(Test-Path -LiteralPath $Path -PathType Container)){return $false}
    $probe=Join-Path $Path ('.qualification-write-'+[guid]::NewGuid().ToString('N'))
    try{
        $stream=[IO.FileStream]::new($probe,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None,1,[IO.FileOptions]::DeleteOnClose)
        $stream.Dispose()
        return $true
    }catch [UnauthorizedAccessException]{return $false}
    catch [IO.IOException]{return $false}
}
function Install-QualificationDotnet([string[]]$Candidates,[string]$RequiredVersion,[string]$WorkingDirectory,[string]$ToolDirectory,[string]$ArtifactDirectory){
    $sdk=Select-QualificationDotnet $Candidates $WorkingDirectory
    $installer=Join-Path $ArtifactDirectory 'dotnet-install.ps1'
    $pwsh=(Get-Process -Id $PID).Path
    if(-not $sdk){
        $root=Join-Path $ToolDirectory ('dotnet-'+$RequiredVersion)
        Ensure-Directory $root
        Download 'https://dot.net/v1/dotnet-install.ps1' $installer
        if((Invoke-Step 'install-sdk' $pwsh @('-NoProfile','-File',$installer,'-Version',$RequiredVersion,'-Architecture','x64','-InstallDir',$root,'-NoPath') $WorkingDirectory 1500) -ne 0){
            throw "Installation of the checkout's .NET SDK $RequiredVersion failed."
        }
        $sdk=Select-QualificationDotnet @((Join-Path $root 'dotnet.exe')) $WorkingDirectory
        if(-not $sdk){throw "The checkout could not select an x64 SDK from $root after installation."}
    }
    $runtimeList=Read-Step 'installed-runtimes' $sdk.Executable @('--list-runtimes') $WorkingDirectory
    $missing=@(Get-MissingToolRuntimes $runtimeList)
    if($missing.Count){
        if(-not(Test-DirectoryWritable $sdk.Root)){
            throw "The selected .NET SDK $($sdk.Version) is usable. Install missing x64 tool runtimes $($missing -join ', ') into $($sdk.Root) as administrator, or rerun this runner elevated."
        }
        if(-not(Test-Path -LiteralPath $installer -PathType Leaf)){Download 'https://dot.net/v1/dotnet-install.ps1' $installer}
        foreach($channel in $missing){
            # Adding an older runtime must preserve the existing SDK's dotnet host.
            $arguments=@('-NoProfile','-File',$installer,'-Channel',$channel,'-Runtime','dotnet','-Architecture','x64','-InstallDir',$sdk.Root,'-NoPath','-SkipNonVersionedFiles')
            if((Invoke-Step ('install-runtime-'+$channel) $pwsh $arguments $WorkingDirectory 600) -ne 0){throw "Installation of the x64 tool runtime $channel failed."}
        }
        $remaining=@(Get-MissingToolRuntimes (Read-Step 'verified-runtimes' $sdk.Executable @('--list-runtimes') $WorkingDirectory))
        if($remaining.Count){throw "Missing x64 tool runtimes in $($sdk.Root) after installation: $($remaining -join ', ')."}
    }
    return $sdk
}
function Find-VersionedTool([string[]]$Candidates,[string]$Pattern,[string]$ProbeName,[string]$WorkingDirectory){
    foreach($exe in ($Candidates | Where-Object {$_} | Select-Object -Unique)){
        if(-not(Test-Path -LiteralPath $exe -PathType Leaf)){continue}
        $version=Read-Step $ProbeName $exe @('--version') $WorkingDirectory -Probe
        if($version -match $Pattern){return $exe}
    }
}
function Commit-Results([string]$Relative,[string]$Revision,[string]$Message){
    [void](Read-Step 'stage-results' $git @('add','--',$Relative) $checkout)
    [void](Read-Step 'commit-results' $git @('commit','--only','-m',$Message,'--',$Relative) $checkout)
    $parent=Read-Step 'results-parent' $git @('rev-parse','HEAD^') $checkout
    $names=Read-Step 'results-paths' $git @('diff-tree','-r','--no-renames','--no-commit-id','--name-only','-z',$Revision,'HEAD') $checkout
    $paths=@($names.Split([char]0,[StringSplitOptions]::RemoveEmptyEntries))
    if($parent -cne $Revision -or -not $paths.Count -or @($paths|Where-Object {-not $_.StartsWith($Relative+'/',[StringComparison]::Ordinal)}).Count){
        throw 'Results commit parent or paths do not match the qualification scope.'
    }
    $identity=Read-Step 'results-identity' $git @('show','-s','--format=%an%n%ae%n%cn%n%ce','HEAD') $checkout
    $actual=$identity -split '\r?\n'
    if($actual.Count -ne 4 -or $actual[0] -cne $env:GIT_AUTHOR_NAME -or $actual[1] -cne $env:GIT_AUTHOR_EMAIL -or
        $actual[2] -cne $env:GIT_COMMITTER_NAME -or $actual[3] -cne $env:GIT_COMMITTER_EMAIL){throw 'Unexpected results commit identity'}
}
try {
    $git=Portable-Tool 'git-for-windows/git' '^MinGit-[0-9.]+-64-bit\.zip$' 'git.exe'
    $gh=Portable-Tool 'cli/cli' '^gh_[0-9.]+_windows_amd64\.zip$' 'gh.exe'
    $env:PATH=(Split-Path -Parent $git)+';'+(Split-Path -Parent $gh)+';'+$env:PATH
    $env:GH_PROMPT_DISABLED='1';$env:GIT_TERMINAL_PROMPT='0';$env:GCM_INTERACTIVE='never'
    try{$canPush=Read-Step 'repository-permissions' $gh @('api',"repos/$Repository",'--jq','.permissions.push') $run}
    catch{throw "Check network access and authenticate once with '$gh auth login --web --git-protocol https'. $($_.Exception.Message)"}
    if ($canPush -ne 'true') { throw "The authenticated account cannot push results to $Repository." }
    Ensure-Directory $CheckoutRoot
    if (Test-Path -LiteralPath $checkout) { throw "Refusing to reuse a checkout: $checkout" }
    # Git invokes credential helpers in its shell. Single-quote the known executable
    # path, also escaping a literal apostrophe in a user-selected tools directory.
    $shellQuote="'"+$gh.Replace('\','/').Replace("'","'\''")+"'"
    $helper='!'+$shellQuote+' auth git-credential'
    [void](Read-Step 'clone' $git @('clone','--no-checkout','-c','credential.https://github.com.helper=','-c',('credential.https://github.com.helper='+$helper),'--',"https://github.com/$Repository.git",$checkout) $CheckoutRoot 600)
    [void](Read-Step 'fetch-source' $git @('fetch','--','origin',$SourceRef) $checkout 600)
    [void](Read-Step 'checkout-source' $git @('checkout','-b',$branch,'FETCH_HEAD') $checkout)
    $sourceSha=Read-Step 'source-revision' $git @('rev-parse','HEAD') $checkout
    $env:GIT_AUTHOR_NAME='jeffadickson';$env:GIT_COMMITTER_NAME='jeffadickson'
    $env:GIT_AUTHOR_EMAIL='88860296+jeffadickson@users.noreply.github.com';$env:GIT_COMMITTER_EMAIL=$env:GIT_AUTHOR_EMAIL
    Set-QualificationNuGetCache $run -Isolated:$IsolatedNuGetCache
    Get-PSDrive -PSProvider FileSystem | Select-Object Name,Root,Used,Free | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'disk-start.json')
    $global=Get-Content -LiteralPath (Join-Path $checkout 'global.json') -Raw | ConvertFrom-Json
    $requiredSdk=[string]$global.sdk.version
    if($requiredSdk -notmatch '^\d+\.\d+\.\d+(?:-[\w.-]+)?$'){throw 'The checkout global.json does not specify an installable SDK version.'}
    $dotnetCandidates=@((Get-Command dotnet.exe -CommandType Application -All -ErrorAction SilentlyContinue).Source)
    foreach($root in @($env:DOTNET_ROOT,(Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'dotnet'),(Join-Path $toolRoot ('dotnet-'+$requiredSdk)))){
        if($root){$dotnetCandidates+=Join-Path $root 'dotnet.exe'}
    }
    $sdk=Install-QualificationDotnet $dotnetCandidates $requiredSdk $checkout $toolRoot $run
    $dotnetExe=$sdk.Executable
    $env:DOTNET_ROOT=$sdk.Root
    $env:PATH=$sdk.Root+';'+$env:PATH
    $env:MSBUILD_EXE_PATH=$sdk.MSBuild
    $sdk.Info | Set-Content -LiteralPath (Join-Path $run 'dotnet-info.txt')
    Write-Host "Selected .NET SDK $($sdk.Version): $dotnetExe"
    $nugetCache=Read-Step 'nuget-global-packages' $dotnetExe @('nuget','locals','global-packages','--list') $checkout
    $nugetCache | Set-Content -LiteralPath (Join-Path $run 'nuget-cache.txt')
    Write-Host "NuGet $nugetCache"
    if((Invoke-Step 'restore-pinned-tools' $dotnetExe @('tool','restore') $checkout 900) -ne 0){throw 'Pinned tool restore failed'}
    $z3Root=Join-Path $toolRoot 'z3-4.12.1'
    $z3Cached=Join-Path $z3Root 'z3-4.12.1-x64-win\bin\z3.exe'
    $z3Pattern='^Z3 version 4\.12\.1(?:\s|$)'
    $z3=Find-VersionedTool (@((Get-Command z3.exe -CommandType Application -All -ErrorAction SilentlyContinue).Source)+@($z3Cached)) $z3Pattern 'probe-z3' $checkout
    if(-not $z3){
        $archive=Join-Path $run 'z3.zip'
        Download 'https://github.com/Z3Prover/z3/releases/download/z3-4.12.1/z3-4.12.1-x64-win.zip' $archive
        Ensure-Directory $z3Root
        Expand-Archive -LiteralPath $archive -DestinationPath $z3Root -Force
        $z3=Find-VersionedTool @($z3Cached) $z3Pattern 'verify-z3' $checkout
        if(-not $z3){throw 'Pinned Z3 4.12.1 was unavailable after extraction.'}
    }
    $venv=Join-Path $toolRoot 'strictdoc-0.30.4'
    $strictdocCached=Join-Path $venv 'Scripts/strictdoc.exe'
    $strictdocPattern='^(?:StrictDoc\s+)?0\.30\.4$'
    $docCandidates=@((Get-Command strictdoc.exe -CommandType Application -All -ErrorAction SilentlyContinue).Source)+@($strictdocCached)
    foreach($root in @($env:VIRTUAL_ENV,$env:CONDA_PREFIX)){
        if($root){$docCandidates+=Join-Path $root 'Scripts/strictdoc.exe'}
    }
    $strictdoc=Find-VersionedTool $docCandidates $strictdocPattern 'probe-strictdoc' $checkout
    $pythonPath=$null
    if(-not $strictdoc){
        $python=Get-Command python.exe -CommandType Application -All -ErrorAction SilentlyContinue |
            Where-Object Source -NotLike '*WindowsApps*' | Select-Object -First 1
        if($python){$pythonPath=$python.Source}
        else{
            $launcher=Get-Command py.exe -CommandType Application -ErrorAction SilentlyContinue
            if($launcher){$pythonPath=Read-Step 'probe-python' $launcher.Source @('-3','-c','import sys; print(sys.executable)') $checkout -Probe}
        }
        if($pythonPath -and (Test-Path -LiteralPath $pythonPath -PathType Leaf)){
            $scripts=Read-Step 'probe-python-script-directories' $pythonPath @('-c','import sysconfig; print(sysconfig.get_path("scripts")); print(sysconfig.get_path("scripts", scheme="nt_user"))') $checkout -Probe
            $docCandidates=@(foreach($directory in ($scripts -split '\r?\n')){
                if($directory -and (Test-Path -LiteralPath $directory -PathType Container)){Join-Path $directory 'strictdoc.exe'}
            })
            $strictdoc=Find-VersionedTool $docCandidates $strictdocPattern 'probe-python-strictdoc' $checkout
        }else{$pythonPath=$null}
    }
    if(-not $strictdoc -and -not $pythonPath){
        # PSF publishes a full, unprivileged Windows build for automation on NuGet.
        $catalog=Read-OfficialUrl 'https://api.nuget.org/v3-flatcontainer/python/index.json' | ConvertFrom-Json
        $version=$catalog.versions | Where-Object {$_ -match '^3\.13\.\d+$'} | Sort-Object {[version]$_} -Descending | Select-Object -First 1
        if(-not $version){throw 'Could not locate official stable Python 3.13 package'}
        $pythonRoot=Join-Path $toolRoot ('python-'+$version)
        $pythonPath=Join-Path $pythonRoot 'tools/python.exe'
        if(-not(Test-Path -LiteralPath $pythonPath -PathType Leaf)){
            $archive=Join-Path $run 'python.zip'
            Download ("https://api.nuget.org/v3-flatcontainer/python/$version/python.$version.nupkg") $archive
            Ensure-Directory $pythonRoot; Expand-Archive -LiteralPath $archive -DestinationPath $pythonRoot -Force
        }
    }
    if(-not $strictdoc){
        $venvPython=Join-Path $venv 'Scripts/python.exe'
        if(-not(Test-Path -LiteralPath $venvPython -PathType Leaf)){
            if(-not(Test-Path -LiteralPath $pythonPath -PathType Leaf)){throw 'Python installation unavailable'}
            [void](Read-Step 'create-doc-environment' $pythonPath @('-m','venv',$venv) $checkout 300)
        }
        [void](Read-Step 'install-strictdoc' $venvPython @('-m','pip','install','--no-compile','strictdoc==0.30.4') $checkout 1200)
        $strictdoc=Find-VersionedTool @($strictdocCached) $strictdocPattern 'verify-strictdoc' $checkout
        if(-not $strictdoc){throw 'Pinned StrictDoc 0.30.4 was unavailable after installation.'}
    }
    Write-Host "Selected StrictDoc 0.30.4: $strictdoc"
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    function Test-NativePrerequisites {
        if(-not(Test-Path -LiteralPath $vswhere -PathType Leaf)){return $false}
        $vs=Read-Step 'probe-vc-tools' $vswhere @('-latest','-products','*','-version','[17.0,)','-requires','Microsoft.VisualStudio.Component.VC.Tools.x86.x64','-property','installationPath') $checkout
        if(-not $vs){return $false}
        $kits=(Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -Name KitsRoot10 -ErrorAction SilentlyContinue).KitsRoot10
        if(-not $kits){$kits=Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Windows Kits\10'}
        $lib=Join-Path $kits 'Lib'
        if(-not(Test-Path -LiteralPath $lib -PathType Container)){return $false}
        foreach($sdk in Get-ChildItem -LiteralPath $lib -Directory){
            if((Test-Path -LiteralPath (Join-Path $sdk.FullName 'um/x64/kernel32.lib') -PathType Leaf) -and
                (Test-Path -LiteralPath (Join-Path $sdk.FullName 'ucrt/x64/ucrt.lib') -PathType Leaf) -and
                (Test-Path -LiteralPath (Join-Path $kits ('bin/'+$sdk.Name+'/x64/rc.exe')) -PathType Leaf)){return $true}
        }
        return $false
    }
    if(-not(Test-NativePrerequisites)){
        if(-not $SkipNativeToolsInstall){
            $existing=if(Test-Path -LiteralPath $vswhere -PathType Leaf){Read-Step 'locate-visual-studio' $vswhere @('-latest','-products','*','-version','[17.0,)','-property','installationPath') $checkout}
            $components=@('--quiet','--norestart','--add','Microsoft.VisualStudio.Component.VC.Tools.x86.x64','--add','Microsoft.VisualStudio.Component.Windows11SDK.26100')
            if($existing){
                $vsSetup=Join-Path (Split-Path -Parent $vswhere) 'setup.exe'
                [void](Invoke-Step 'modify-visual-studio' $vsSetup (@('modify','--installPath',$existing)+$components) $checkout 2100)
            }else{
                $vsSetup=Join-Path $run 'vs_BuildTools.exe'
                Download 'https://aka.ms/vs/17/release/vs_BuildTools.exe' $vsSetup
                [void](Invoke-Step 'install-native-build-tools' $vsSetup (@('--wait')+$components) $checkout 2100)
            }
        }
        if(-not(Test-NativePrerequisites)){$records.Add([pscustomobject]@{Name='native-prerequisites';Status='MissingPrerequisite';Detail='Visual Studio C++ x64 tools and complete Windows SDK required; consult installer log/administrator permissions.'})}
    }
    $pester=Get-Module -ListAvailable Pester|Where-Object {$_.Version.Major -eq 5}|Sort-Object Version -Descending|Select-Object -First 1
    if($pester){$pesterManifest=$pester.Path}else{
        $pesterRoot=Join-Path $toolRoot 'Pester/5.7.1';$pesterManifest=Join-Path $pesterRoot 'Pester.psd1'
        if(-not(Test-Path -LiteralPath $pesterManifest -PathType Leaf)){
            $archive=Join-Path $run 'pester.zip';Download 'https://www.powershellgallery.com/api/v2/package/Pester/5.7.1' $archive
            Ensure-Directory $pesterRoot; Expand-Archive -LiteralPath $archive -DestinationPath $pesterRoot -Force
        }
    }
    $pesterCommand='Import-Module -Name $env:QUALIFICATION_PESTER -ErrorAction Stop; $r=Invoke-Pester -Path tests/WorkstationRunner.Tests.ps1 -PassThru; if(-not $r -or $r.Result -ne "Passed" -or $r.PassedCount -eq 0){exit 1}'
    $psTest=Start-Step 'pester-runner' (Get-Process -Id $PID).Path @('-NoProfile','-Command',$pesterCommand) $checkout 300 1 @{QUALIFICATION_PESTER=$pesterManifest}
    [void](Complete-Step $psTest)
    $build=Join-Path $run 'build'; $coverage=Join-Path $run 'coverage'
    if((Invoke-Step 'build' $dotnetExe @('build','-c','Release','--artifacts-path',$build) $checkout 1800) -ne 0){throw 'Build failed; preserving failure evidence'}
    [void](Invoke-Step 'unit-generator-analyzer-tests' $dotnetExe @('test','-c','Release','--no-build','--artifacts-path',$build,'--collect:XPlat Code Coverage','--results-directory',$coverage) $checkout 1800)
    foreach($project in @('tests/SharpSafeNumerics.Tests','tests/SharpSafeNumerics.PropertyTests','tests/SharpSafeNumerics.AnalyzerTests','examples/GeneratedQueueDepth','examples/RiscueQueueDepth')){
        [void](Invoke-Step ($project -replace '/','-') $dotnetExe @('run','--project',$project,'-c','Release','--no-build','--artifacts-path',$build) $checkout 900)
    }
    [void](Invoke-Step 'dafny' $dotnetExe @('tool','run','dafny','--','verify','formal/SafeInteger.dfy','--solver-path',$z3) $checkout 600)
    [void](Invoke-Step 'strictdoc' $strictdoc @('export','docs/requirements.sdoc','docs/generator-requirements.sdoc','docs/analyzer-requirements.sdoc','--output-dir',(Join-Path $run 'requirements')) $checkout 900)
    [void](Invoke-Step 'coverage-summary' $dotnetExe @('tool','run','reportgenerator','--',('-reports:'+(Join-Path $coverage '**/coverage.cobertura.xml')),('-targetdir:'+(Join-Path $run 'coverage-report')),'-reporttypes:JsonSummary;Html') $checkout 600)
    $native=Join-Path $run 'aot'
    if((Invoke-Step 'aot-publish' $dotnetExe @('publish','tests/SharpSafeNumerics.AotSmoke','-c','Release','-r','win-x64','-o',$native,'--artifacts-path',(Join-Path $run 'aot-build')) $checkout 1800) -eq 0){[void](Invoke-Step 'aot-execute' (Join-Path $native 'SharpSafeNumerics.AotSmoke.exe') @() $checkout 600)}
    $packages=Join-Path $run 'packages'
    if((Invoke-Step 'pack' $dotnetExe @('pack','-c','Release','-o',$packages,'--artifacts-path',$build) $checkout 900) -eq 0){[void](Invoke-Step 'package-consumer' (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $checkout 'eng\VerifyPackages.ps1'),'-PackageDirectory',$packages,'-WorkDirectory',(Join-Path $run 'package-consumer')) $checkout 900)}
    # Finish all rebuilds before starting mutation workers or the immutable soak.
    [void](Invoke-Step 'package-reproducibility' (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $checkout 'eng\VerifyPackageReproducibility.ps1'),'-ArtifactDirectory',(Join-Path $run 'package-reproducibility'),'-BuildArtifactsPath',$build,'-RebuildBetweenPasses') $checkout 1800)
    $soakBin=Join-Path $run 'soak-bin'
    [void](Invoke-Step 'prepare-soak' $dotnetExe @('publish','tests/SharpSafeNumerics.Soak','-c','Release','--no-self-contained','-p:UseAppHost=false','-o',$soakBin,'--artifacts-path',(Join-Path $run 'soak-build')) $checkout 900)
    $capacity=[Math]::Max(1,[Math]::Min($MaxParallelism,[Environment]::ProcessorCount))
    $mutationWorkers=if($capacity -ge 6){[Math]::Max(1,[Math]::Floor(($capacity-3)/3))}else{1}
    $jobs=[Collections.Generic.List[object]]::new()
    foreach($mode in @('auto','128','scalar')){
        $environment=@{DOTNET_EnableHWIntrinsic='1';DOTNET_EnableAVX2='1'}; if($mode -eq '128'){$environment['DOTNET_EnableAVX2']='0'}elseif($mode -eq 'scalar'){$environment['DOTNET_EnableHWIntrinsic']='0'}
        $requestedSeconds=if($SoakMinutes -gt 0){$SoakMinutes*60}else{[int][Math]::Max(1,($workDeadline-[DateTime]::UtcNow).TotalSeconds-10)}
        $arguments=@((Join-Path $soakBin 'SharpSafeNumerics.Soak.dll'),'--seconds',[string]$requestedSeconds,'--seed','731925')
        if($mode -ne 'auto'){$arguments+=@('--expected-width',$(if($mode -eq '128'){'128'}else{'0'}))}
        $jobs.Add([pscustomobject]@{Name=('soak-'+$mode);Exe=$dotnetExe;Arguments=$arguments;WorkingDirectory=$checkout;LimitSeconds=$requestedSeconds+120;Weight=1;Environment=$environment})
    }
    foreach($scope in @('Core','Generator','Analyzers')){
        $config=if($scope -eq 'Core'){'stryker-config.json'}elseif($scope -eq 'Generator'){'stryker-generator-config.json'}else{'stryker-analyzers-config.json'}
        $jobs.Add([pscustomobject]@{Name=('mutation-'+$scope);Exe=$dotnetExe;Arguments=@('stryker','--config-file',('../../'+$config),'--target-framework','net11.0','--concurrency',[string]$mutationWorkers,'--output',(Join-Path $run ('mutation-'+$scope)));WorkingDirectory=(Join-Path $checkout ('src/SharpSafeNumerics.'+$scope));LimitSeconds=5400;Weight=[int]$mutationWorkers;Environment=@{}})
    }
    # Await process completion or the next deadline. There is no fixed-interval
    # polling loop and no memory-triggered termination policy.
    while($jobs.Count -gt 0 -or $active.Count -gt 0){
        $used=0; foreach($context in $active){$used+=$context.Weight}
        for($i=$jobs.Count-1;$i -ge 0;$i--){
            $job=$jobs[$i]
            if($used+$job.Weight -le $capacity){
                if($job.Name -like 'soak-*'){
                    $queued=@($jobs|Where-Object Name -like 'soak-*').Count
                    $waves=[Math]::Ceiling($queued/[double]($capacity-$used))
                    $seconds=([Math]::Max(0,($workDeadline-[DateTime]::UtcNow).TotalSeconds)/$waves)-10
                    $job.Arguments[2]=[string][int][Math]::Max(1,[Math]::Min([int]$job.Arguments[2],$seconds))
                }
                $context=Start-Step -Name $job.Name -Exe $job.Exe -Arguments $job.Arguments -WorkingDirectory $job.WorkingDirectory -LimitSeconds $job.LimitSeconds -Weight $job.Weight -Environment $job.Environment
                $jobs.RemoveAt($i);if($context){$active.Add($context);$used+=$context.Weight}
            }
        }
        if($active.Count -eq 0){break}
        $next=$workDeadline;foreach($context in $active){if($context.End -lt $next){$next=$context.End}}
        $cts=[Threading.CancellationTokenSource]::new()
        $delay=[Threading.Tasks.Task]::Delay([int][Math]::Max(1,($next-[DateTime]::UtcNow).TotalMilliseconds),$cts.Token)
        $waits=[Collections.Generic.List[Threading.Tasks.Task]]::new();$waits.Add($delay);foreach($context in $active){$waits.Add($context.ExitTask)}
        [void][Threading.Tasks.Task]::WhenAny($waits.ToArray()).GetAwaiter().GetResult()
        $cts.Cancel();try{[void]$delay.GetAwaiter().GetResult()}catch [OperationCanceledException]{}finally{$cts.Dispose()}
        for($i=$active.Count-1;$i -ge 0;$i--){$context=$active[$i];if($context.Process.HasExited -or [DateTime]::UtcNow -ge $context.End -or [DateTime]::UtcNow -ge $workDeadline){[void](Complete-Step $context);$active.RemoveAt($i)}}
    }
} catch { $fatal=$_.ToString(); Write-Error -Message $fatal -ErrorAction Continue }
finally {
    foreach($context in $active.ToArray()){try{[void](Complete-Step $context -Cancel)}catch{Write-Warning $_}}
    # Publishing has the remaining absolute budget, including all git network calls.
    $workDeadline=$deadline
    if($sourceSha -and (Test-Path -LiteralPath $checkout -PathType Container)){
        try{
            $relative='qualification-results/workstation/'+$runId; $result=Join-Path $checkout $relative;Ensure-Directory $result
            $sourceChanges=Read-Step 'source-changes' $git @('status','--porcelain=v1','--untracked-files=normal') $checkout
            $qualificationStatus=Get-QualificationStatus $records.ToArray() $fatal $sourceChanges
            $summary=[ordered]@{RunId=$runId;Repository=$Repository;SourceRef=$SourceRef;SourceRevision=$sourceSha;IssueNumber=$IssueNumber;IssueUrl=$issueUrl;ResultsBranch=$branch;Machine=$env:COMPUTERNAME;ProcessorCount=[Environment]::ProcessorCount;StartedUtc=$started.ToString('O');FinishedUtc=[DateTime]::UtcNow.ToString('O');Hours=$Hours;DeadlineUtc=$deadline.ToString('O');ArtifactDirectory=$run;QualificationStatus=$qualificationStatus;FatalError=$fatal;Checks=$records;Downloads=$downloads;SourceChanges=$sourceChanges}
            $summary | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $result 'summary.json') -Encoding utf8
            [pscustomobject]@{Identifier=$env:PROCESSOR_IDENTIFIER;LogicalProcessors=[Environment]::ProcessorCount}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $result 'cpu.json')
            foreach($file in @('dotnet-info.txt','nuget-cache.txt','disk-start.json')){ $path=Join-Path $run $file;if(Test-Path -LiteralPath $path -PathType Leaf){Copy-Item -LiteralPath $path -Destination (Join-Path $result $file)} }
            $coverageSummary=Join-Path $run 'coverage-report/Summary.json';if(Test-Path -LiteralPath $coverageSummary -PathType Leaf){Copy-Item -LiteralPath $coverageSummary -Destination (Join-Path $result 'coverage-summary.json')}
            $packageHashes=Join-Path $run 'package-reproducibility/sha256.json';if(Test-Path -LiteralPath $packageHashes -PathType Leaf){Copy-Item -LiteralPath $packageHashes -Destination (Join-Path $result 'package-sha256.json')}
            foreach($record in $records){foreach($kind in @('Stdout','Stderr')){ $path=$record.$kind;if($path -and (Test-Path -LiteralPath $path -PathType Leaf)){Get-Content -LiteralPath $path -Tail 120 | Set-Content -LiteralPath (Join-Path $result ($record.Name+'.'+$kind+'.tail.txt')) -Encoding utf8}}}
            $mutations=foreach($report in (Get-ChildItem -Path (Join-Path $run 'mutation-*/reports/mutation-report.json') -File -ErrorAction SilentlyContinue)){
                $data=Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json
                $mutants=@(foreach($file in $data.files.PSObject.Properties){foreach($mutant in $file.Value.mutants){$mutant}})
                [pscustomobject]@{Report=$report.FullName;Total=$mutants.Count;Statuses=@($mutants|Group-Object status|Select-Object Name,Count);Survivors=@(foreach($file in $data.files.PSObject.Properties){foreach($m in $file.Value.mutants){if($m.status -in @('Survived','NoCoverage')){[pscustomobject]@{File=$file.Name;Id=$m.id;Status=$m.status;Location=$m.location;Replacement=$m.replacement}}}})}
            }
            @($mutations)|ConvertTo-Json -Depth 9|Set-Content -LiteralPath (Join-Path $result 'mutation-summary.json') -Encoding utf8
            Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $result 'runner.ps1')
            @("# Workstation qualification $runId","","Source: $sourceSha","Qualification status: $qualificationStatus","","Results are bounded evidence, including failures, timeouts and skipped work. See summary.json for exact commands, exit codes, timings and local artifact paths. Raw logs, coverage, packages and full mutation reports remain local. No CI configuration was changed.") | Set-Content -LiteralPath (Join-Path $result 'README.md') -Encoding utf8
            Commit-Results $relative $sourceSha "Record qualification for issue #$IssueNumber ($runId, $($sourceSha.Substring(0,7)))"
            [void](Read-Step 'push-results' $git @('push','-u','origin',("HEAD:refs/heads/"+$branch)) $checkout 180)
            $pushed=$true
            Write-Host "RESULTS BRANCH: $branch"
            Write-Host "TRACKING ISSUE: $issueUrl"
            Write-Host "https://github.com/$Repository/tree/$branch/$relative"
            Write-Host "RAW ARTIFACTS: $run"
        }catch{Write-Error -Message ("Results publishing failed: "+$_) -ErrorAction Continue}
    }
    foreach($key in $savedEnvironment.Keys){[Environment]::SetEnvironmentVariable($key,$savedEnvironment[$key],'Process')}
}
if(-not $pushed -or $qualificationStatus -ne 'Passed' -or $fatal -or @($records|Where-Object {$_.Status -notin @('Passed','ProbeRejected')}).Count -gt 0){exit 1}
exit 0
