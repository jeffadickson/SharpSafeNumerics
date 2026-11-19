// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

//! Requirement-oriented negative and randomized regression checks.
use proptest::prelude::*;
use qualcompact::{compact, normalize};
use serde_json::{Value, json};
fn workstation(status: &str) -> Value {
    json!({"RunId":"test","SourceRevision":"abc","QualificationStatus":"Passed","Checks":[{"Name":"gate","Status":status,"ExitCode":0}]})
}
#[test]
fn all_failed_statuses_dominate() {
    for s in ["Failed", "TimedOut", "Cancelled", "MissingPrerequisite"] {
        assert_eq!(normalize(workstation(s))["verdict"], "Failed");
    }
}
#[test]
fn declared_failure_dominates_successful_checks() {
    for status in ["Failed", "TimedOut", "Cancelled", "MissingPrerequisite"] {
        let mut value = workstation("Passed");
        value["QualificationStatus"] = json!(status);
        assert_eq!(normalize(value)["verdict"], "Failed");
    }
}
#[test]
fn unsupported_schemas_are_not_mutation_reports() {
    for value in [json!([]), json!([{}]), json!({"other": true})] {
        let normalized = normalize(value);
        assert_eq!(normalized["kind"], "unknown");
        assert_eq!(normalized["verdict"], "Unknown");
    }
}
#[test]
fn expected_rejection_is_pass() {
    let mut v = workstation("ProbeRejected");
    v["Checks"][0]["ExitCode"] = json!(1);
    assert_eq!(normalize(v)["verdict"], "Passed");
}
#[test]
fn skips_are_incomplete() {
    assert_eq!(
        normalize(workstation("SkippedBudget"))["verdict"],
        "Incomplete"
    );
}
#[test]
fn unknown_status_is_incomplete() {
    assert_eq!(normalize(workstation("Mystery"))["verdict"], "Incomplete");
}
#[test]
fn missing_checks_never_pass() {
    for v in [
        json!({}),
        json!({"QualificationStatus":"Passed"}),
        json!({"Checks":[]}),
        json!({"Checks":null}),
    ] {
        assert_ne!(normalize(v)["verdict"], "Passed");
    }
}
#[test]
fn nonzero_pass_exit_fails() {
    let mut v = workstation("Passed");
    v["Checks"][0]["ExitCode"] = json!(8);
    assert_eq!(normalize(v)["verdict"], "Failed");
}
#[test]
fn fatal_and_source_changes_fail() {
    for k in ["FatalError", "SourceChanges"] {
        let mut v = workstation("Passed");
        v[k] = json!("changed");
        assert_eq!(normalize(v)["verdict"], "Failed");
    }
}
#[test]
fn legacy_derives() {
    let mut v = workstation("Passed");
    v.as_object_mut().unwrap().remove("QualificationStatus");
    assert_eq!(normalize(v)["verdict"], "Passed");
}
#[test]
fn numeric_failure_overrides() {
    assert_eq!(
        normalize(json!({"Passed":true,"Failure":"broken"}))["verdict"],
        "Failed"
    );
}
#[test]
fn numeric_missing_counts_stay_missing() {
    assert!(
        normalize(json!({"Passed":true}))["metrics"]
            .get("Samples")
            .is_none()
    );
}
#[test]
fn null_branch_coverage_stays_null() {
    let n = normalize(json!({"coverage":{"assemblies":[{"name":"A","branchcoverage":null}]}}));
    assert!(n["metrics"]["coverage"]["assemblies"][0]["branchcoverage"].is_null());
}
#[test]
fn independent_mutation_reports() {
    let n = normalize(
        json!([{"Report":"a","Total":1,"Statuses":[{"Name":"Survived","Count":1}],"Survivors":[{"Id":1,"Status":"Survived"}]},{"Report":"b","Total":2,"Statuses":[{"Name":"Killed","Count":2}],"Survivors":[]}]),
    );
    assert_eq!(n["metrics"].as_array().unwrap().len(), 2);
    assert_eq!(n["issues"][0]["reportIndex"], 0);
}
#[test]
fn stryker_keeps_unknown_and_findings() {
    let n = normalize(
        json!({"files":{"f":{"mutants":[{"id":"1","status":"Killed"},{"id":"2","status":"Survived"},{"id":"3"}]}}}),
    );
    assert_eq!(n["metrics"]["statusCounts"]["Unknown"], 1);
    assert_eq!(n["issues"].as_array().unwrap().len(), 3);
}
#[test]
fn zero_budget_rejects() {
    assert!(compact(&json!({"runs":[normalize(workstation("Failed"))]}), 0, 12).is_err());
}
#[test]
fn omission_retains_failure() {
    let full = json!({"runs":[normalize(workstation("Failed"))]});
    let b = compact(&full, 8192, 0).unwrap();
    let v: Value = serde_json::from_slice(&b).unwrap();
    assert_eq!(v["runs"][0]["verdict"], "Failed");
    assert_eq!(v["runs"][0]["omittedIssues"], 1);
}
#[test]
fn empty_mutation_is_unknown() {
    assert_eq!(normalize(json!([]))["kind"], "unknown");
}
proptest! {
 #![proptest_config(ProptestConfig {cases:128, rng_seed: proptest::test_runner::RngSeed::Fixed(731925), ..ProptestConfig::default()})]
 #[test]fn arbitrary_integer_counts_exact(n in any::<u64>()){let v=normalize(json!({"Passed":true,"Samples":n}));prop_assert_eq!(v["metrics"]["Samples"].as_u64(),Some(n));}
 #[test]fn compact_never_drops_runs(n in 1usize..20, findings in 0usize..12){let runs:Vec<_>=(0..n).map(|_|normalize(workstation("Failed"))).collect();let full=json!({"runs":runs});if let Ok(bytes)=compact(&full,8192,findings){prop_assert!(bytes.len()<=8192);let v:Value=serde_json::from_slice(&bytes).unwrap();prop_assert_eq!(v["runs"].as_array().unwrap().len(),n);for r in v["runs"].as_array().unwrap(){prop_assert_eq!(&r["verdict"],"Failed");}}}
}
#[test]
fn huge_nonzero_exit_is_failed() {
    let mut v = workstation("Passed");
    v["Checks"][0]["ExitCode"] = serde_json::from_str("18446744073709551615").unwrap();
    assert_eq!(normalize(v)["verdict"], "Failed");
}
#[test]
fn malformed_exit_is_incomplete() {
    for exit in [json!("0"), json!(0.5), Value::Null] {
        let mut v = workstation("Passed");
        v["Checks"][0]["ExitCode"] = exit;
        assert_eq!(normalize(v)["verdict"], "Incomplete");
    }
}
#[test]
fn absent_exit_not_null() {
    let mut v = workstation("Passed");
    v["Checks"][0].as_object_mut().unwrap().remove("ExitCode");
    assert!(
        normalize(v)["metrics"]["checkStatuses"][0]
            .get("exitCode")
            .is_none()
    );
}
#[test]
fn full_check_details_retained() {
    let mut v = workstation("Passed");
    v["Checks"][0]["Stdout"] = json!("raw.txt");
    assert_eq!(normalize(v)["checkDetails"][0]["Stdout"], "raw.txt");
}
#[test]
fn timestamp_is_not_source_revision() {
    assert!(
        normalize(json!({"Passed":true,"StartUtc":"today"}))["issues"]
            .as_array()
            .unwrap()
            .iter()
            .any(|i| i["code"] == "UnknownSourceRevision")
    );
}
#[test]
fn explicit_malformed_declaration_is_incomplete() {
    for status in [Value::Null, json!(2)] {
        let mut v = workstation("Passed");
        v["QualificationStatus"] = status;
        assert_eq!(normalize(v)["verdict"], "Incomplete");
    }
}
