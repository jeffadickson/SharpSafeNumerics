// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

//! SN-QUAL-COMPACT: explicit campaign states never become implicit qualification.
use proptest::prelude::*;
use qualcompact::{compact, normalize};
use serde_json::{Value, json};

fn week(status: &str) -> Value {
    json!({"Kind":"weekQualification","SchemaVersion":1,"RunId":"week-test",
        "SourceRevision":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","Status":status,
        "ChunkCount":1,"PassedChunks":1,"FailedChunks":0,"InconclusiveChunks":0,
        "SourceUpdateFailures":0,"Chunks":[{"Index":0,"Seed":731925,"Status":"Passed",
        "ExitCode":0,"SourceRevision":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "SummarySHA256":"raw-hash","SummaryRelativePath":"chunks/0000.json"}]})
}

#[test]
fn week_states_are_explicit_and_running_never_passes() {
    for (status, verdict) in [
        ("Running", "Unknown"),
        ("Passed", "Passed"),
        ("Failed", "Failed"),
        ("Incomplete", "Incomplete"),
    ] {
        let run = normalize(week(status));
        assert_eq!(run["kind"], "weekQualification");
        assert_eq!(run["verdict"], verdict);
    }
}

#[test]
fn failures_dominate_even_contradictory_running_or_passed_reports() {
    for status in ["Running", "Passed"] {
        for evidence in ["Failure", "FailedChunks", "chunk"] {
            let mut v = week(status);
            match evidence {
                "Failure" => v["Failure"] = json!({"Message":"bad result"}),
                "FailedChunks" => v["FailedChunks"] = json!(1),
                _ => v["Chunks"][0]["Status"] = json!("Failed"),
            }
            assert_eq!(normalize(v)["verdict"], "Failed");
        }
    }
}

#[test]
fn terminal_pass_requires_complete_consistent_chunks_and_refreshes() {
    let mut cases = Vec::new();
    let mut v = week("Passed");
    v["Chunks"] = json!([]);
    cases.push(v);
    let mut v = week("Passed");
    v["ChunkCount"] = json!(2);
    cases.push(v);
    let mut v = week("Passed");
    v["Chunks"][0]["Status"] = json!("Inconclusive");
    v["Chunks"][0]["ExitCode"] = json!(3);
    cases.push(v);
    let mut v = week("Passed");
    v["SourceUpdateFailures"] = json!(1);
    cases.push(v);
    let mut v = week("Passed");
    v["Chunks"][0]["Status"] = json!("Interrupted");
    cases.push(v);
    let mut v = week("Passed");
    v["Chunks"][0]["ExitCode"] = Value::Null;
    cases.push(v);
    for v in cases {
        assert_ne!(normalize(v)["verdict"], "Passed");
    }
}

#[test]
fn contradictory_chunk_counts_and_exit_failures_cannot_pass() {
    let mut v = week("Passed");
    v["PassedChunks"] = json!(999);
    assert_eq!(normalize(v)["verdict"], "Incomplete");
    for exit in [1, 2, -1] {
        let mut v = week("Running");
        v["Chunks"][0]["Status"] = json!("Inconclusive");
        v["Chunks"][0]["ExitCode"] = json!(exit);
        assert_eq!(normalize(v)["verdict"], "Failed");
    }
}

#[test]
fn interrupted_recovery_reason_is_not_a_numerical_failure_and_preflight_stays_incomplete() {
    let mut value = week("Incomplete");
    value["Chunks"][0]["Status"] = json!("Interrupted");
    value["Chunks"][0]["ExitCode"] = Value::Null;
    value["Chunks"][0]["Failure"] = json!("Orchestrator restarted before observing completion");
    assert_eq!(normalize(value)["verdict"], "Incomplete");
    let mut value = week("Passed");
    value["PreflightInconclusive"] = json!(1);
    assert_eq!(normalize(value)["verdict"], "Incomplete");
}

fn soak(status: &str) -> Value {
    json!({"Kind":"ieeeDecimalSoak","SchemaVersion":1,"SourceRevision":null,"Status":status,
        "Seed":7542032,"CsCheckSeed":"replay-seed","LastBatchSeed":"batch-seed",
        "Samples":1,"Assertions":3,"PassedChecks":1,"FailedChecks":0,"InconclusiveChecks":0,
        "Failure":null,"Provenance":{"Scope":["decimal32","decimal64","decimal128"],"Limitations":["finite sampling"]}})
}

#[test]
fn ieee_soak_declared_states_and_failures_are_faithful() {
    for (status, expected) in [
        ("Passed", "Passed"),
        ("Failed", "Failed"),
        ("Inconclusive", "Incomplete"),
        ("Running", "Unknown"),
    ] {
        let run = normalize(soak(status));
        assert_eq!(run["kind"], "ieeeDecimalSoak");
        assert_eq!(run["verdict"], expected);
    }
    for key in ["FailedChecks", "Failure"] {
        let mut v = soak("Passed");
        v[key] = if key == "Failure" {
            json!({"Type":"OracleMismatch","FirstCase":{"Input1":"NaN123","ActualFlags":1}})
        } else {
            json!(1)
        };
        assert_eq!(normalize(v)["verdict"], "Failed");
    }
    let mut v = soak("Passed");
    v["InconclusiveChecks"] = json!(1);
    assert_eq!(normalize(v)["verdict"], "Incomplete");
}

#[test]
fn ieee_soak_malformed_or_missing_counters_cannot_pass() {
    for key in [
        "Samples",
        "Assertions",
        "PassedChecks",
        "FailedChecks",
        "InconclusiveChecks",
    ] {
        for value in [Value::Null, json!("0"), json!(-1), json!(0.5)] {
            let mut v = soak("Passed");
            v[key] = value;
            assert_ne!(normalize(v)["verdict"], "Passed");
        }
        let mut v = soak("Passed");
        v.as_object_mut().unwrap().remove(key);
        assert_ne!(normalize(v)["verdict"], "Passed");
    }
    let mut v = soak("Passed");
    v["SchemaVersion"] = json!(2);
    v["Passed"] = json!(true);
    assert_ne!(normalize(v)["verdict"], "Passed");
}

#[test]
fn checkpoint_or_unknown_kind_does_not_borrow_legacy_passed_and_bad_updates_are_visible() {
    for kind in ["ieeeDecimalSoakCheckpoint", "futureCampaign"] {
        assert_ne!(
            normalize(json!({"Kind":kind,"SchemaVersion":1,"Passed":true}))["verdict"],
            "Passed"
        );
    }
    let mut value = week("Passed");
    value["SourceUpdates"] = json!({"Status":"Failed"});
    assert_eq!(normalize(value)["verdict"], "Incomplete");
}

#[test]
fn ieee_soak_exact_counts_null_source_seeds_and_failure_replay_survive() {
    let mut v = soak("Failed");
    v["Samples"] = serde_json::from_str("1844674407370955161600").unwrap();
    v["Failure"] = json!({"Type":"OracleMismatch","CsCheckMinimizedDiagnostic":"seed replay"});
    let run = normalize(v);
    assert_eq!(
        run["metrics"]["Samples"].to_string(),
        "1844674407370955161600"
    );
    assert!(run["identity"]["SourceRevision"].is_null());
    assert_eq!(run["metrics"]["CsCheckSeed"], "replay-seed");
    assert!(
        run["issues"]
            .as_array()
            .unwrap()
            .iter()
            .any(|i| i["value"]["CsCheckMinimizedDiagnostic"] == "seed replay")
    );
    assert_eq!(
        run["metrics"]["Provenance"]["Limitations"][0],
        "finite sampling"
    );
}

#[test]
fn malformed_campaign_version_or_status_cannot_fall_back_to_legacy_passed() {
    for version in [json!(2), json!("1"), Value::Null] {
        let mut v = week("Passed");
        v["SchemaVersion"] = version;
        v["Passed"] = json!(true);
        assert_ne!(normalize(v)["verdict"], "Passed");
    }
    for status in [json!("Uploaded"), json!(true), Value::Null] {
        let mut v = week("Passed");
        v["Status"] = status;
        assert_ne!(normalize(v)["verdict"], "Passed");
    }
}

#[test]
fn revision_scopes_and_full_chunk_references_are_not_borrowed_or_merged() {
    let mut v = week("Running");
    let mut other = v["Chunks"][0].clone();
    other["SourceRevision"] = json!("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
    v["Chunks"].as_array_mut().unwrap().push(other);
    let run = normalize(v);
    assert_eq!(run["chunkDetails"].as_array().unwrap().len(), 2);
    assert_eq!(run["metrics"]["chunkScopes"].as_array().unwrap().len(), 2);
    assert_eq!(
        run["chunkDetails"][0]["SummaryRelativePath"],
        "chunks/0000.json"
    );
}

#[test]
fn large_campaign_compacts_with_exact_omissions_and_full_failures() {
    let mut v = week("Failed");
    let mut chunk = v["Chunks"][0].clone();
    chunk["Status"] = json!("Failed");
    v["Chunks"] = json!(vec![chunk; 1000]);
    let full = json!({"runs":[normalize(v)]});
    assert_eq!(
        full["runs"][0]["chunkDetails"].as_array().unwrap().len(),
        1000
    );
    let bytes = compact(&full, 8192, 2).unwrap();
    let out: Value = serde_json::from_slice(&bytes).unwrap();
    assert_eq!(out["runs"][0]["omittedChunkDetails"], 1000);
    assert_eq!(out["runs"][0]["verdict"], "Failed");
    assert!(out["runs"][0]["omittedIssues"].as_u64().unwrap() >= 998);
}

proptest! {
    #![proptest_config(ProptestConfig { cases:128, rng_seed:proptest::test_runner::RngSeed::Fixed(7542032), ..ProptestConfig::default() })]
    #[test]
    fn campaign_exact_values_and_failure_precedence(n in any::<u64>(), seed in any::<i32>()) {
        let mut v=week("Running");v["Samples"]=json!(n);v["Seed"]=json!(seed);v["Failure"]=json!("failure");
        let run=normalize(v);prop_assert_eq!(&run["verdict"],"Failed");
        prop_assert_eq!(run["metrics"]["Samples"].as_u64(),Some(n));
        prop_assert_eq!(run["metrics"]["Seed"].as_i64(),Some(i64::from(seed)));
    }
}
