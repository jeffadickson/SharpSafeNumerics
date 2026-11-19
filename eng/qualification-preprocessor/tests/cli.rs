// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

//! End-to-end checks exercise the actual native CLI and filesystem contract.
use serde_json::{Value, json};
use std::{
    fs,
    path::PathBuf,
    process::{Command, Output},
    sync::atomic::{AtomicUsize, Ordering},
};
static NEXT: AtomicUsize = AtomicUsize::new(0);
struct Case(PathBuf);
impl Case {
    fn new() -> Self {
        let root = std::env::var_os("CARGO_TARGET_DIR")
            .map(PathBuf::from)
            .unwrap_or_else(std::env::temp_dir);
        assert!(root.is_dir());
        let p = root.join(format!(
            "cli-test-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir(&p).unwrap();
        Self(p)
    }
    fn file(&self, name: &str, bytes: &[u8]) -> PathBuf {
        let p = self.0.join(name);
        fs::write(&p, bytes).unwrap();
        p
    }
    fn run(&self, args: &[&str]) -> Output {
        Command::new(env!("CARGO_BIN_EXE_qualcompact"))
            .current_dir(&self.0)
            .args(args)
            .output()
            .unwrap()
    }
}
impl Drop for Case {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.0);
    }
}
fn report(o: &Output) -> Value {
    serde_json::from_slice(&o.stdout).unwrap()
}
#[test]
fn failed_qualification_exit_zero() {
    let c = Case::new();
    c.file("a.json", br#"{"Passed":false,"Failure":"x"}"#);
    let o = c.run(&["a.json"]);
    assert!(o.status.success());
    assert_eq!(report(&o)["runs"][0]["verdict"], "Failed");
}
#[test]
fn bom_and_exact_count() {
    let c = Case::new();
    let mut b = vec![239, 187, 191];
    b.extend(br#"{"Passed":true,"Samples":1844674407370955161600}"#);
    c.file("a.json", &b);
    let o = c.run(&["a.json"]);
    assert!(o.status.success());
    assert_eq!(
        report(&o)["runs"][0]["metrics"]["Samples"].to_string(),
        "1844674407370955161600"
    );
}
#[test]
fn malformed_artifact_diagnostic() {
    let c = Case::new();
    c.file("a.json", b"{");
    let o = c.run(&["a.json"]);
    assert_eq!(o.status.code(), Some(2));
    assert_eq!(report(&o)["diagnostics"][0]["code"], "InvalidArtifact");
}
#[test]
fn unknown_artifact_never_pass() {
    let c = Case::new();
    c.file("a.json", b"{}");
    let o = c.run(&["a.json"]);
    assert_eq!(o.status.code(), Some(2));
    assert_eq!(report(&o)["runs"][0]["verdict"], "Unknown");
}
#[test]
fn fresh_outputs_only() {
    let c = Case::new();
    c.file("a.json", br#"{"Passed":true}"#);
    c.file("out.json", b"preserve");
    let o = c.run(&["a.json", "--output", "out.json"]);
    assert_eq!(o.status.code(), Some(2));
    assert_eq!(fs::read(c.0.join("out.json")).unwrap(), b"preserve");
}
#[test]
fn input_alias_rejected() {
    let c = Case::new();
    let b = br#"{"Passed":true}"#;
    c.file("a.json", b);
    let o = c.run(&["a.json", "--output", "a.json"]);
    assert_eq!(o.status.code(), Some(2));
    assert_eq!(fs::read(c.0.join("a.json")).unwrap(), b);
}
#[test]
fn output_details_alias_rejected() {
    let c = Case::new();
    c.file("a.json", br#"{"Passed":true}"#);
    let o = c.run(&["a.json", "--output", "out.json", "--details", "./out.json"]);
    assert_eq!(o.status.code(), Some(2));
    assert!(!c.0.join("out.json").exists());
}
#[test]
fn missing_parent_rejected() {
    let c = Case::new();
    c.file("a.json", br#"{"Passed":true}"#);
    let o = c.run(&["a.json", "--output", "missing/out.json"]);
    assert_eq!(o.status.code(), Some(2));
    assert!(!c.0.join("missing").exists());
}
#[test]
fn directory_nonrecursive() {
    let c = Case::new();
    fs::create_dir(c.0.join("nested")).unwrap();
    fs::write(c.0.join("nested/fail.json"), br#"{"Passed":false}"#).unwrap();
    c.file("summary.json", br#"{"Passed":true}"#);
    let o = c.run(&["."]);
    assert!(o.status.success());
    assert_eq!(report(&o)["runs"].as_array().unwrap().len(), 1);
}
#[test]
fn budget_failure_preserves_full_details() {
    let c = Case::new();
    c.file("a.json", br#"{"Passed":false,"Failure":"failure"}"#);
    let o = c.run(&["a.json", "--max-bytes", "1", "--details", "details.json"]);
    assert_eq!(o.status.code(), Some(2));
    let v: Value = serde_json::from_slice(&fs::read(c.0.join("details.json")).unwrap()).unwrap();
    assert_eq!(v["runs"][0]["verdict"], "Failed");
}
#[test]
fn deterministic_output() {
    let c = Case::new();
    c.file("a.json", br#"{"Passed":true}"#);
    assert_eq!(c.run(&["a.json"]).stdout, c.run(&["a.json"]).stdout);
}
const START: &str = "Soak start UTC 2026-11-17T19:01:25.0835564Z; seconds=1; seed=731925/seed; detected-width=256\n";
const PASS: &str = "Soak passed UTC 2026-11-17T19:51:42.1353842Z; elapsed=1.041s; samples=10; type-pairs=120; arithmetic-policy-outcomes=2040; batch-lanes=100; GC0=1\n";
#[test]
fn actual_integer_log_and_error_reference() {
    let c = Case::new();
    c.file(
        "a.log",
        format!("{START}expected error probe\n{PASS}").as_bytes(),
    );
    let o = c.run(&["a.log"]);
    assert!(o.status.success());
    let r = &report(&o)["runs"][0];
    assert_eq!(r["verdict"], "Passed");
    assert_eq!(r["metrics"]["completions"][0]["samples"], 10);
    assert_eq!(r["metrics"]["errorReferenceCount"], 1);
}
#[test]
fn truncated_terminal_never_passes() {
    let c = Case::new();
    c.file("a.log", b"Soak passed UTC x\n");
    let o = c.run(&["a.log"]);
    assert_ne!(report(&o)["runs"][0]["verdict"], "Passed");
}
#[test]
fn later_start_never_passes() {
    let c = Case::new();
    c.file("a.log", format!("{PASS}{START}").as_bytes());
    let o = c.run(&["a.log"]);
    assert_ne!(report(&o)["runs"][0]["verdict"], "Passed");
}
#[test]
fn oversized_unterminated_line_visible() {
    let c = Case::new();
    let mut b = format!("{START}{PASS}").into_bytes();
    b.extend(vec![b'x'; 100_000]);
    c.file("a.log", &b);
    let o = c.run(&["a.log"]);
    assert_ne!(report(&o)["runs"][0]["verdict"], "Passed");
}
#[test]
fn repeated_log_records_bounded() {
    let c = Case::new();
    c.file("a.log", format!("{START}{PASS}").repeat(1000).as_bytes());
    let o = c.run(&["a.log", "--max-bytes", "20000"]);
    let r = &report(&o)["runs"][0];
    assert_eq!(r["metrics"]["completionCount"], 1000);
    assert_eq!(r["metrics"]["omittedCompletions"], 992);
    assert_eq!(r["verdict"], "Unknown");
}
#[test]
fn full_failures_retained() {
    let c = Case::new();
    let checks: Vec<_> = (0..100)
        .map(|n| json!({"Name":n.to_string(),"Status":"Failed"}))
        .collect();
    c.file(
        "a.json",
        &serde_json::to_vec(&json!({"Checks":checks})).unwrap(),
    );
    let o = c.run(&[
        "a.json",
        "--max-bytes",
        "20000",
        "--max-findings",
        "0",
        "--details",
        "details.json",
    ]);
    assert!(o.status.success());
    let v: Value = serde_json::from_slice(&fs::read(c.0.join("details.json")).unwrap()).unwrap();
    assert_eq!(v["runs"][0]["issues"].as_array().unwrap().len(), 101);
    assert_eq!(report(&o)["runs"][0]["omittedIssues"], 101);
}
#[test]
fn negative_fractional_log_counts_never_pass() {
    for value in ["-1", "1.5", "1e2"] {
        let c = Case::new();
        c.file(
            "a.log",
            format!(
                "{START}{}",
                PASS.replace("samples=10", &format!("samples={value}"))
            )
            .as_bytes(),
        );
        let o = c.run(&["a.log"]);
        assert_ne!(report(&o)["runs"][0]["verdict"], "Passed");
    }
}
#[test]
fn windows_case_alias_rejected_before_write() {
    if !cfg!(windows) {
        return;
    }
    let c = Case::new();
    c.file("a.json", br#"{"Passed":true}"#);
    let o = c.run(&["a.json", "--output", "out.json", "--details", "OUT.JSON"]);
    assert_eq!(o.status.code(), Some(2));
    assert!(!c.0.join("out.json").exists());
}
#[test]
fn windows_unicode_case_alias_rejected_before_write() {
    if !cfg!(windows) {
        return;
    }
    let c = Case::new();
    c.file("a.json", br#"{"Passed":true}"#);
    let o = c.run(&["a.json", "--output", "outÁ.json", "--details", "outá.json"]);
    assert_eq!(o.status.code(), Some(2));
    assert!(!c.0.join("outÁ.json").exists());
}
#[test]
fn weekly_directory_names_are_discovered_without_raw_recursive_scan() {
    let c = Case::new();
    c.file(
        "report-0001.json",
        br#"{"Kind":"weekQualification","SchemaVersion":1,"Status":"Running","Chunks":[]}"#,
    );
    c.file(
        "checkpoint.json",
        br#"{"Kind":"weekQualification","SchemaVersion":1,"Status":"Incomplete","Chunks":[]}"#,
    );
    c.file("report-not-a-number.json", b"{");
    let o = c.run(&["."]);
    assert!(o.status.success());
    let value = report(&o);
    let runs = value["runs"].as_array().unwrap();
    assert_eq!(runs.len(), 2);
    assert!(
        runs.iter()
            .all(|run| run["kind"] == "weekQualification" && run["verdict"] != "Passed")
    );
}

#[test]
fn actual_ieee_envelope_and_unfinished_reservation_never_infer_pass() {
    let c = Case::new();
    c.file("ieee-soak-summary.json",br#"{"Kind":"ieeeDecimalSoak","SchemaVersion":1,"Status":"Inconclusive","SourceRevision":null,"Seed":7542032,"Samples":10,"Assertions":30,"PassedChecks":9,"FailedChecks":0,"InconclusiveChecks":1,"Failure":null}"#);
    let output = c.run(&["."]);
    assert!(output.status.success());
    assert_eq!(report(&output)["runs"][0]["verdict"], "Incomplete");
    c.file("reserved.json", b"");
    let output = c.run(&["reserved.json"]);
    assert_eq!(output.status.code(), Some(2));
    assert_eq!(report(&output)["diagnostics"][0]["code"], "InvalidArtifact");
}
