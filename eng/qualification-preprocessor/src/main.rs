//! Native CLI: read-only inputs, deterministic output, fresh-file publication.
use clap::Parser;
use serde_json::{Value, json};
use sha2::{Digest, Sha256};
use std::{
    fs::{self, File, OpenOptions},
    io::{self, BufRead, BufReader, Read, Write},
    path::{Path, PathBuf},
    process::ExitCode,
};
#[derive(Parser)]
#[command(
    version,
    about = "Compact qualification evidence; process success is not qualification success"
)]
struct Args {
    #[arg(required = true)]
    inputs: Vec<PathBuf>,
    #[arg(long)]
    output: Option<PathBuf>,
    #[arg(long)]
    details: Option<PathBuf>,
    #[arg(long, default_value_t = 8192)]
    max_bytes: usize,
    #[arg(long, default_value_t = 12)]
    max_findings: usize,
}
fn main() -> ExitCode {
    match execute(Args::parse()) {
        Ok(valid) => {
            if valid {
                ExitCode::SUCCESS
            } else {
                ExitCode::from(2)
            }
        }
        Err(e) => {
            eprintln!("qualcompact: {e}");
            ExitCode::from(2)
        }
    }
}
fn destination(p: &Path) -> Result<PathBuf, String> {
    let name = p.file_name().ok_or("Output needs a file name")?;
    let parent = p
        .parent()
        .filter(|p| !p.as_os_str().is_empty())
        .unwrap_or(Path::new("."));
    let parent = parent
        .canonicalize()
        .map_err(|e| format!("Output parent: {e}"))?;
    if !parent.is_dir() {
        return Err("Output parent is not directory".into());
    }
    let result = parent.join(name);
    if result.exists() {
        return Err(format!("Output already exists: {}", result.display()));
    }
    Ok(result)
}
fn execute(args: Args) -> Result<bool, String> {
    let output = args.output.as_deref().map(destination).transpose()?;
    let details = args.details.as_deref().map(destination).transpose()?;
    if output.is_some()
        && output.as_ref().zip(details.as_ref()).is_some_and(|(a, b)| {
            if cfg!(windows) {
                a.to_string_lossy().to_lowercase() == b.to_string_lossy().to_lowercase()
            } else {
                a == b
            }
        })
    {
        return Err("Output and details paths alias".into());
    }
    let mut paths = std::collections::BTreeSet::new();
    let mut diagnostics = Vec::new();
    for p in args.inputs {
        if p.is_dir() {
            for e in fs::read_dir(&p).map_err(|e| e.to_string())? {
                let p = e.map_err(|e| e.to_string())?.path();
                if p.is_file()
                    && discoverable(&p)
                    && matches!(
                        p.extension().and_then(|s| s.to_str()),
                        Some("json" | "txt" | "log")
                    )
                {
                    paths.insert(p.canonicalize().map_err(|e| e.to_string())?);
                }
            }
        } else {
            paths.insert(p.canonicalize().map_err(|e| e.to_string())?);
        }
    }
    let mut runs = Vec::new();
    for p in paths {
        let raw_source = source(&p)?;
        let extension = p.extension().and_then(|s| s.to_str()).unwrap_or("");
        let source = raw_source;
        let parsed = if extension == "json" {
            read_json(&p)
        } else if matches!(extension, "txt" | "log") {
            read_log(&p)
        } else {
            Err("Unsupported extension".into())
        };
        match parsed {
            Ok(mut run) => {
                if run["kind"] == "unknown" {
                    diagnostics.push(json!({"code":"UnsupportedSchema","source":source}));
                }
                if run["issues"].as_array().is_some_and(|a| {
                    a.iter().any(|i| {
                        matches!(
                            i["code"].as_str(),
                            Some("InvalidSoakRecords" | "OversizedLogLines")
                        )
                    })
                }) {
                    diagnostics.push(json!({"code":"InvalidLogEvidence","source":source}));
                }
                let after = crate::source(&p)?;
                if after["sha256"] != source["sha256"] || after["bytes"] != source["bytes"] {
                    diagnostics.push(
                        json!({"code":"InputChanged","sourceBefore":source,"sourceAfter":after}),
                    );
                    run["verdict"] = json!("Unknown");
                }
                run["source"] = source;
                runs.push(run)
            }
            Err(e) => {
                diagnostics.push(json!({"code":"InvalidArtifact","source":source,"message":e}))
            }
        }
    }
    if runs.is_empty() {
        diagnostics.push(json!({"code":"NoQualifyingArtifact"}));
    }
    let valid = diagnostics.is_empty();
    let full = json!({"schemaVersion":1,"runs":runs,"diagnosticCount":diagnostics.len(),"diagnostics":diagnostics,"omittedDiagnostics":0});
    let compact = qualcompact::compact(&full, args.max_bytes, args.max_findings);
    // Publish full details even when the compact budget cannot preserve identities.
    if let Some(path) = details {
        write_new(
            &path,
            &serde_json::to_vec(&full).map_err(|e| e.to_string())?,
        )?;
    }
    let compact = compact?;
    if let Some(path) = output {
        write_new(&path, &compact)?
    } else {
        io::stdout()
            .lock()
            .write_all(&compact)
            .map_err(|e| e.to_string())?;
    }
    Ok(valid)
}
fn write_new(p: &Path, bytes: &[u8]) -> Result<(), String> {
    let mut f = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(p)
        .map_err(|e| e.to_string())?;
    f.write_all(bytes).map_err(|e| e.to_string())?;
    f.sync_all().map_err(|e| e.to_string())
}
fn source(p: &Path) -> Result<Value, String> {
    let mut f = File::open(p).map_err(|e| e.to_string())?;
    let mut hash = Sha256::new();
    let mut bytes = 0u64;
    let mut b = [0u8; 65536];
    loop {
        let n = f.read(&mut b).map_err(|e| e.to_string())?;
        if n == 0 {
            break;
        }
        hash.update(&b[..n]);
        bytes += n as u64;
    }
    Ok(
        json!({"path":p,"sha256":hash.finalize().iter().map(|b|format!("{b:02x}")).collect::<String>(),"bytes":bytes}),
    )
}
fn read_json(p: &Path) -> Result<Value, String> {
    let mut f = BufReader::new(File::open(p).map_err(|e| e.to_string())?);
    let prefix = f.fill_buf().map_err(|e| e.to_string())?;
    if prefix.starts_with(&[0xef, 0xbb, 0xbf]) {
        f.consume(3)
    }
    let v = serde_json::from_reader(f).map_err(|e| format!("Invalid JSON: {e}"))?;
    Ok(qualcompact::normalize(v))
}
/// Log processing keeps bounded records; omitted line counts remain explicit.
#[derive(Default)]
struct LogRecords {
    starts: Vec<Value>,
    passes: Vec<Value>,
    start_count: u64,
    pass_count: u64,
    errors: Vec<Value>,
    error_count: u64,
    oversized: u64,
    invalid: u64,
    last_start: u64,
    last_pass: u64,
}
impl LogRecords {
    fn line(&mut self, bytes: &[u8], line: u64) -> Result<(), String> {
        let s = std::str::from_utf8(bytes)
            .map_err(|e| e.to_string())?
            .trim()
            .trim_start_matches('\u{feff}');
        let start = s.starts_with("Soak start UTC ");
        let pass = s.starts_with("Soak passed UTC ");
        if !start && !pass {
            if s.to_ascii_lowercase().contains("error") {
                self.error_count += 1;
                if self.errors.len() < 12 {
                    self.errors
                        .push(json!({"code":"NonAuthoritativeErrorReference","line":line}));
                }
            }
            return Ok(());
        }
        let prefix = if start {
            "Soak start UTC "
        } else {
            "Soak passed UTC "
        };
        let mut fields = json!({"line":line});
        let mut parts = s[prefix.len()..].split(';');
        let utc = parts.next().unwrap_or("").trim();
        fields["utc"] = json!(utc);
        for p in parts {
            if let Some((key, value)) = p.trim().split_once('=') {
                fields[key] = serde_json::from_str::<Value>(value)
                    .ok()
                    .filter(Value::is_number)
                    .unwrap_or_else(|| json!(value));
            }
        }
        let utc_valid = utc.len() >= 20
            && utc.as_bytes().get(4) == Some(&b'-')
            && utc.as_bytes().get(10) == Some(&b'T')
            && utc.ends_with('Z');
        let valid = utc_valid
            && if pass {
                [
                    "samples",
                    "type-pairs",
                    "arithmetic-policy-outcomes",
                    "batch-lanes",
                ]
                .iter()
                .all(|k| {
                    fields[*k].is_number()
                        && fields[*k].to_string().bytes().all(|b| b.is_ascii_digit())
                }) && fields["elapsed"]
                    .as_str()
                    .and_then(|s| s.strip_suffix('s'))
                    .and_then(|s| s.parse::<f64>().ok())
                    .is_some_and(|n| n.is_finite() && n >= 0.0)
            } else {
                fields.get("seed").is_some() && fields.get("detected-width").is_some()
            };
        if !valid {
            self.invalid += 1;
            return Ok(());
        }
        if start {
            self.start_count += 1;
            self.last_start = line;
            if self.starts.len() < 8 {
                self.starts.push(fields)
            }
        } else {
            self.pass_count += 1;
            self.last_pass = line;
            if self.passes.len() < 8 {
                self.passes.push(fields)
            }
        }
        Ok(())
    }
}
/// Stream bounded chunks; even an oversized unterminated final line is reported.
fn read_log(p: &Path) -> Result<Value, String> {
    let mut reader = BufReader::new(File::open(p).map_err(|e| e.to_string())?);
    let mut line = Vec::new();
    let mut oversized = false;
    let mut count = 0u64;
    let mut r = LogRecords::default();
    loop {
        let b = reader.fill_buf().map_err(|e| e.to_string())?;
        if b.is_empty() {
            if oversized {
                r.oversized += 1
            } else if !line.is_empty() {
                r.line(&line, count + 1)?
            }
            break;
        }
        let take = b
            .iter()
            .position(|c| *c == b'\n')
            .map(|n| n + 1)
            .unwrap_or(b.len());
        let end = b[take - 1] == b'\n';
        if line.len() + take <= 65536 && !oversized {
            line.extend_from_slice(&b[..take]);
        } else {
            oversized = true;
        }
        reader.consume(take);
        if end {
            count += 1;
            if oversized {
                r.oversized += 1
            } else {
                r.line(&line, count)?
            }
            line.clear();
            oversized = false;
        }
    }
    if r.start_count == 0 && r.pass_count == 0 && r.invalid == 0 {
        return Err("No recognized integer soak record".into());
    }
    let verdict = if r.pass_count == 1
        && r.start_count <= 1
        && r.last_pass > r.last_start
        && r.invalid == 0
        && r.oversized == 0
    {
        "Passed"
    } else if r.pass_count == 0 {
        "Incomplete"
    } else {
        "Unknown"
    };
    if r.invalid > 0 {
        r.errors
            .push(json!({"code":"InvalidSoakRecords","count":r.invalid}));
    }
    if r.oversized > 0 {
        r.errors
            .push(json!({"code":"OversizedLogLines","count":r.oversized}));
    }
    r.errors.push(json!({"code":"UnknownSourceRevision"}));
    Ok(
        json!({"kind":"integerSoakLog","identity":{},"verdict":verdict,"metrics":{"starts":r.starts,"completions":r.passes,"startCount":r.start_count,"completionCount":r.pass_count,"omittedStarts":r.start_count.saturating_sub(8),"omittedCompletions":r.pass_count.saturating_sub(8),"errorReferenceCount":r.error_count,"omittedErrorReferences":r.error_count.saturating_sub(12)},"issueCount":r.errors.len(),"issues":r.errors,"omittedIssues":0}),
    )
}

/// Published evidence names only: arbitrary build tails and metadata are companions.
fn discoverable(p: &Path) -> bool {
    let name = p
        .file_name()
        .unwrap_or_default()
        .to_string_lossy()
        .to_ascii_lowercase();
    matches!(
        name.as_str(),
        "summary.json"
            | "mutation-summary.json"
            | "mutation-report.json"
            | "coverage-summary.json"
            | "soak-auto.stdout.tail.txt"
            | "soak-128.stdout.tail.txt"
            | "soak-scalar.stdout.tail.txt"
    ) || (name.ends_with(".json")
        && name.contains("numeric")
        && (name.contains("summary") || name.contains("soak")))
}
