// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

//! Normalization deliberately retains each evidence record independently.
use serde_json::{Value, json};
mod campaigns;

/// Normalize a supported JSON schema. Missing evidence remains Unknown.
pub fn normalize(v: Value) -> Value {
    if let Some(campaign) = campaigns::normalize(&v) {
        return campaign;
    }
    let mut issues = Vec::new();
    let mut identity = json!({});
    for key in [
        "RunId",
        "Repository",
        "SourceRef",
        "SourceRevision",
        "Machine",
        "StartedUtc",
        "FinishedUtc",
        "StartUtc",
        "EndUtc",
    ] {
        if let Some(value) = v.get(key) {
            identity[key] = value.clone();
        }
    }
    let (kind, verdict, metrics) = if v.get("Checks").is_some()
        || v.get("QualificationStatus").is_some()
    {
        let mut failed = false;
        let mut incomplete = false;
        for key in ["FatalError", "SourceChanges"] {
            if v.get(key).is_some_and(nonempty) {
                failed = true;
                issues.push(json!({"code":key,"value":v[key]}));
            }
        }
        let checks = v.get("Checks").and_then(Value::as_array);
        if checks.is_none_or(|a| a.is_empty()) {
            incomplete = true;
            issues.push(json!({"code":"MissingChecks"}));
        }
        if let Some(checks) = checks {
            for (index, c) in checks.iter().enumerate() {
                let status = c.get("Status").and_then(Value::as_str).unwrap_or("Unknown");
                match status {
                    "Passed" | "ProbeRejected" => {}
                    "SkippedBudget" => incomplete = true,
                    "Failed" | "TimedOut" | "Cancelled" | "MissingPrerequisite" => failed = true,
                    _ => incomplete = true,
                }
                if status != "ProbeRejected"
                    && let Some(exit) = c.get("ExitCode")
                {
                    let text = exit.to_string();
                    let digits = text.strip_prefix('-').unwrap_or(&text);
                    if exit.is_number()
                        && !digits.is_empty()
                        && digits.bytes().all(|b| b.is_ascii_digit())
                    {
                        if text != "0" {
                            failed = true;
                        }
                    } else {
                        incomplete = true;
                        issues.push(json!({"code":"InvalidExitCode","index":index,"value":exit}));
                    }
                }
                if status != "Passed" {
                    issues.push(json!({"code":"CheckStatus","index":index,"check":c}));
                }
            }
        }
        let declared = v.get("QualificationStatus").and_then(Value::as_str);
        if v.get("QualificationStatus").is_some() && declared.is_none() {
            incomplete = true;
            issues.push(
                json!({"code":"InvalidQualificationStatus","value":v["QualificationStatus"]}),
            );
        }
        match declared {
            Some("Failed" | "TimedOut" | "Cancelled" | "MissingPrerequisite") => failed = true,
            Some("Incomplete") => incomplete = true,
            Some("Passed") | None => {}
            _ => incomplete = true,
        }
        (
            "workstation",
            if failed {
                "Failed"
            } else if incomplete {
                "Incomplete"
            } else {
                "Passed"
            },
            {
                let mut m = json!({"checkCount":checks.map(Vec::len)});
                if let Some(status) = v.get("QualificationStatus") {
                    m["declaredStatus"] = status.clone();
                }
                if let Some(checks) = checks {
                    m["checkStatuses"] = json!(
                        checks
                            .iter()
                            .map(|c| {
                                let mut o = json!({});
                                for (src, dst) in [
                                    ("Name", "name"),
                                    ("Status", "status"),
                                    ("ExitCode", "exitCode"),
                                ] {
                                    if let Some(value) = c.get(src) {
                                        o[dst] = value.clone();
                                    }
                                }
                                o
                            })
                            .collect::<Vec<_>>()
                    );
                }
                m
            },
        )
    } else if v.get("Passed").is_some() && v.get("Kind").is_none() {
        let verdict = match v["Passed"].as_bool() {
            Some(true) => "Passed",
            Some(false) => "Failed",
            None => "Unknown",
        };
        if verdict != "Passed" || v.get("Failure").is_some_and(nonempty) {
            issues.push(json!({"code":"NumericFailure","value":v.get("Failure")}));
        }
        let verdict = if v.get("Failure").is_some_and(nonempty) {
            "Failed"
        } else {
            verdict
        };
        let mut m = v.clone();
        if let Some(o) = m.as_object_mut() {
            for key in ["Failure", "Passed"] {
                o.remove(key);
            }
        }
        ("numericSoak", verdict, m)
    } else if v.is_array()
        && !v.as_array().unwrap().is_empty()
        && v.as_array()
            .unwrap()
            .iter()
            .all(|e| e.get("Statuses").is_some())
    {
        let entries = v.as_array().unwrap();
        for (i, e) in entries.iter().enumerate() {
            if let Some(a) = e.get("Survivors").and_then(Value::as_array) {
                for s in a {
                    issues.push(json!({"code":"MutationFinding","reportIndex":i,"mutant":s}));
                }
            }
        }
        (
            "mutationSummary",
            "Unknown",
            json!(
                entries
                    .iter()
                    .map(|e| {
                        let mut m = e.clone();
                        m.as_object_mut().unwrap().remove("Survivors");
                        m
                    })
                    .collect::<Vec<_>>()
            ),
        )
    } else if let Some(files) = v.get("files").and_then(Value::as_object) {
        let mut counts = std::collections::BTreeMap::<String, u64>::new();
        for (file, entry) in files {
            if let Some(ms) = entry.get("mutants").and_then(Value::as_array) {
                for m in ms {
                    let status = m.get("status").and_then(Value::as_str).unwrap_or("Unknown");
                    *counts.entry(status.to_owned()).or_default() += 1;
                    if status != "Killed" {
                        issues.push(json!({"code":"MutationFinding","file":file,"mutant":m}));
                    }
                }
            }
        }
        ("stryker", "Unknown", json!({"statusCounts":counts}))
    } else if v.pointer("/coverage/assemblies").is_some()
        || v.get("summary")
            .is_some_and(|s| s.get("linecoverage").is_some())
    {
        ("coverage", "Unknown", {
            let mut m = v.clone();
            if let Some(a) = m
                .pointer_mut("/coverage/assemblies")
                .and_then(Value::as_array_mut)
            {
                for e in a {
                    if let Some(o) = e.as_object_mut() {
                        o.remove("classesinassembly");
                    }
                }
            }
            m
        })
    } else {
        issues.push(json!({"code":"UnsupportedSchema"}));
        ("unknown", "Unknown", json!({}))
    };
    if !identity
        .get("SourceRevision")
        .and_then(Value::as_str)
        .is_some_and(|s| !s.is_empty())
    {
        issues.push(json!({"code":"UnknownSourceRevision"}));
    }

    let mut run = json!({"kind":kind,"identity":identity,"verdict":verdict,"metrics":metrics,"issueCount":issues.len(),"issues":issues,"omittedIssues":0});
    if let Some(checks) = v.get("Checks") {
        run["checkDetails"] = checks.clone();
    }
    run
}
fn nonempty(v: &Value) -> bool {
    match v {
        Value::Null => false,
        Value::String(s) => !s.is_empty(),
        Value::Array(a) => !a.is_empty(),
        _ => true,
    }
}

/// Reduce findings without removing run identities or verdicts; reject impossible budgets.
pub fn compact(full: &Value, max_bytes: usize, max_findings: usize) -> Result<Vec<u8>, String> {
    let mut v = full.clone();
    for run in v["runs"].as_array_mut().ok_or("Missing runs")? {
        for (details, omitted) in [
            ("chunkDetails", "omittedChunkDetails"),
            ("sourceUpdateDetails", "omittedSourceUpdateDetails"),
        ] {
            if let Some(records) = run.as_object_mut().unwrap().remove(details) {
                run[omitted] = json!(records.as_array().map(Vec::len));
            }
        }
        if let Some(checks) = run.as_object_mut().unwrap().remove("checkDetails") {
            run["omittedCheckDetails"] = json!(checks.as_array().map(Vec::len));
        }
        let a = run["issues"].as_array_mut().ok_or("Missing issues")?;
        a.truncate(max_findings);
        run["omittedIssues"] = json!(
            run["issueCount"].as_u64().unwrap_or(0)
                - run["issues"].as_array().unwrap().len() as u64
        );
    }
    // Successful gates retain an exact count; raw/full details retain their names.
    for run in v["runs"].as_array_mut().unwrap() {
        if let Some(checks) = run
            .pointer_mut("/metrics/checkStatuses")
            .and_then(Value::as_array_mut)
        {
            let before = checks.len();
            checks.retain(|c| c["status"] != "Passed");
            let omitted = before - checks.len();
            run["metrics"]["omittedPassedCheckStatuses"] = json!(omitted);
        }
    }
    loop {
        let bytes = serde_json::to_vec(&v).map_err(|e| e.to_string())?;
        if bytes.len() <= max_bytes {
            return Ok(bytes);
        };
        let mut removed = false;
        for run in v["runs"].as_array_mut().unwrap().iter_mut().rev() {
            if run["issues"].as_array_mut().unwrap().pop().is_some() {
                run["omittedIssues"] = json!(run["omittedIssues"].as_u64().unwrap() + 1);
                removed = true;
                break;
            }
        }
        if !removed {
            return Err(format!(
                "Identity, verdict and metrics exceed {max_bytes} byte budget"
            ));
        }
    }
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn contradictory_failed_check_dominates_passed() {
        assert_eq!(
            normalize(
                json!({"QualificationStatus":"Passed","Checks":[{"Name":"build","Status":"Failed"}]})
            )["verdict"],
            "Failed"
        );
    }
    #[test]
    fn numeric_exact_large_count() {
        let v: Value =
            serde_json::from_str(r#"{"Passed":true,"Samples":1844674407370955161600}"#).unwrap();
        assert_eq!(
            normalize(v)["metrics"]["Samples"].to_string(),
            "1844674407370955161600"
        );
    }
}
