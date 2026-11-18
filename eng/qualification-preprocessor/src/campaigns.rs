//! Explicit campaign schemas. A completed upload or a running checkpoint is not a pass.
use serde_json::{Value, json};
use std::collections::BTreeMap;

/// Recognize versioned campaign envelopes before legacy shape-based detection.
pub(super) fn normalize(v: &Value) -> Option<Value> {
    if v.get("Kind").and_then(Value::as_str) == Some("ieeeDecimalSoak") {
        return Some(normalize_soak(v));
    }
    if v.get("Kind").and_then(Value::as_str) != Some("weekQualification") {
        return None;
    }
    let mut issues = Vec::new();
    let version_valid = v.get("SchemaVersion").and_then(Value::as_u64) == Some(1);
    if !version_valid {
        issues.push(json!({"code":"UnsupportedCampaignSchema","value":v.get("SchemaVersion")}));
    }
    let mut failed = v.get("Status").and_then(Value::as_str) == Some("Failed");
    let mut incomplete = false;
    for key in ["Failure", "FatalError", "SourceChanges"] {
        if let Some(value) = v.get(key).filter(|value| super::nonempty(value)) {
            failed = true;
            issues.push(json!({"code":"CampaignFailure","field":key,"value":value}));
        }
    }
    for key in [
        "FailedChunks",
        "InconclusiveChunks",
        "SourceUpdateFailures",
        "PreflightInconclusive",
    ] {
        if let Some(value) = v.get(key) {
            match unsigned_nonzero(value) {
                Some(false) => {}
                Some(true) => {
                    if key == "FailedChunks" {
                        failed = true;
                    } else {
                        incomplete = true;
                    }
                    issues.push(json!({"code":"CampaignCounter","field":key,"value":value}));
                }
                None => {
                    incomplete = true;
                    issues.push(json!({"code":"InvalidCampaignCounter","field":key,"value":value}));
                }
            }
        }
    }
    let chunks = v.get("Chunks").and_then(Value::as_array);
    // Chunk counts are grouped by their own identity, never by the latest top-level revision.
    // These are record/status counts, not pooled numerical assertions or samples.
    let mut scopes = BTreeMap::<String, BTreeMap<String, u64>>::new();
    if let Some(chunks) = chunks {
        for (index, chunk) in chunks.iter().enumerate() {
            let status = chunk
                .get("Status")
                .and_then(Value::as_str)
                .unwrap_or("Unknown");
            let revision = chunk
                .get("SourceRevision")
                .and_then(Value::as_str)
                .filter(|s| !s.is_empty());
            *scopes
                .entry(revision.unwrap_or("<unknown>").to_owned())
                .or_default()
                .entry(status.to_owned())
                .or_default() += 1;
            if revision.is_none() {
                incomplete = true;
                issues.push(json!({"code":"UnknownChunkSourceRevision","index":index}));
            }
            let exit = chunk.get("ExitCode");
            let inconclusive_exit = exit.and_then(Value::as_i64) == Some(3);
            if exit.is_some_and(|value| integer_nonzero(value) == Some(true))
                && !inconclusive_exit
                && (status != "Interrupted" || matches!(exit.and_then(Value::as_i64), Some(1 | 2)))
            {
                failed = true;
                issues.push(json!({"code":"ChunkExitFailure","index":index,"chunk":chunk}));
            }
            match status {
                "Passed" => {
                    if chunk.get("ExitCode").and_then(Value::as_i64) != Some(0) {
                        incomplete = true;
                        issues.push(json!({"code":"InvalidChunkExit","index":index,"chunk":chunk}));
                    }
                }
                "Failed" => failed = true,
                _ => incomplete = true,
            }
            // Recovery records use Failure for an interruption reason. That is
            // not an observed numerical failure; explicit failed exit codes above
            // still dominate even an Interrupted status.
            if status != "Interrupted" && chunk.get("Failure").is_some_and(super::nonempty) {
                failed = true;
            }
            if status != "Passed" || chunk.get("Failure").is_some_and(super::nonempty) {
                issues.push(json!({"code":"ChunkStatus","index":index,"chunk":chunk}));
            }
        }
        if chunks.is_empty() {
            incomplete = true;
        }
        if let Some(count) = v.get("ChunkCount")
            && count.as_u64() != Some(chunks.len() as u64)
        {
            incomplete = true;
            issues.push(
                json!({"code":"ChunkCountMismatch","declared":count,"observed":chunks.len()}),
            );
        }
        for (field, status) in [
            ("PassedChunks", "Passed"),
            ("FailedChunks", "Failed"),
            ("InconclusiveChunks", "Inconclusive"),
        ] {
            if let Some(count) = v.get(field) {
                let observed = chunks
                    .iter()
                    .filter(|chunk| chunk.get("Status").and_then(Value::as_str) == Some(status))
                    .count();
                if count.as_u64() != Some(observed as u64) {
                    incomplete = true;
                    issues.push(json!({"code":"ChunkStatusCountMismatch","field":field,"declared":count,"observed":observed}));
                }
            }
        }
    } else {
        incomplete = true;
        issues.push(json!({"code":"MissingChunks"}));
    }
    match v.get("SourceUpdates") {
        Some(Value::Array(updates)) => {
            for (index, update) in updates.iter().enumerate() {
                if update.get("Status").and_then(Value::as_str) != Some("Passed") {
                    incomplete = true;
                    issues.push(json!({"code":"SourceUpdateStatus","index":index,"update":update}));
                }
            }
        }
        Some(value) => {
            incomplete = true;
            issues.push(json!({"code":"InvalidSourceUpdates","value":value}));
        }
        None => {}
    }
    let declared = v.get("Status").and_then(Value::as_str);
    if !matches!(
        declared,
        Some("Running" | "Passed" | "Failed" | "Incomplete")
    ) {
        issues.push(json!({"code":"InvalidCampaignStatus","value":v.get("Status")}));
    }
    let verdict = if failed {
        "Failed"
    } else if !version_valid {
        "Unknown"
    } else {
        match declared {
            Some("Passed") if !incomplete => "Passed",
            Some("Passed" | "Incomplete") => "Incomplete",
            _ => "Unknown",
        }
    };
    let mut metrics = v.clone();
    for key in [
        "Chunks",
        "SourceUpdates",
        "Failure",
        "FatalError",
        "SourceChanges",
    ] {
        metrics.as_object_mut().unwrap().remove(key);
    }
    metrics["chunkScopes"] = json!(
        scopes
            .into_iter()
            .map(|(revision, counts)| json!({"SourceRevision":revision,"statusCounts":counts}))
            .collect::<Vec<_>>()
    );
    let mut identity = json!({});
    for key in [
        "RunId",
        "Repository",
        "SourceRef",
        "SourceRevision",
        "BootstrapRevision",
        "StartedUtc",
        "UpdatedUtc",
        "DeadlineUtc",
    ] {
        if let Some(value) = v.get(key) {
            identity[key] = value.clone();
        }
    }
    if !identity
        .get("SourceRevision")
        .and_then(Value::as_str)
        .is_some_and(|s| !s.is_empty())
    {
        issues.push(json!({"code":"UnknownSourceRevision"}));
    }
    let mut run = json!({"kind":if version_valid {"weekQualification"} else {"unknown"},"identity":identity,"verdict":verdict,"metrics":metrics,"issueCount":issues.len(),"issues":issues,"omittedIssues":0});
    if let Some(value) = v.get("Chunks") {
        run["chunkDetails"] = value.clone();
    }
    if let Some(value) = v.get("SourceUpdates") {
        run["sourceUpdateDetails"] = value.clone();
    }
    Some(run)
}

/// Soak results have independent pass/fail/inconclusive check counters. Samples
/// are generated inputs, not checks: never equate those two scopes or invent totals.
fn normalize_soak(v: &Value) -> Value {
    let mut issues = Vec::new();
    let version_valid = v.get("SchemaVersion").and_then(Value::as_u64) == Some(1);
    if !version_valid {
        issues.push(json!({"code":"UnsupportedCampaignSchema","value":v.get("SchemaVersion")}));
    }
    let declared = v.get("Status").and_then(Value::as_str);
    let mut failed = declared == Some("Failed");
    let mut incomplete = false;
    for key in [
        "Samples",
        "Assertions",
        "PassedChecks",
        "FailedChecks",
        "InconclusiveChecks",
    ] {
        match v.get(key).and_then(unsigned_nonzero) {
            Some(nonzero) => {
                if key == "FailedChecks" && nonzero {
                    failed = true;
                    issues.push(json!({"code":"FailedChecks","value":v[key]}));
                }
                if key == "InconclusiveChecks" && nonzero {
                    incomplete = true;
                    issues.push(json!({"code":"InconclusiveChecks","value":v[key]}));
                }
                if matches!(key, "Samples" | "Assertions" | "PassedChecks") && !nonzero {
                    incomplete = true;
                    issues.push(json!({"code":"EmptySoakEvidence","field":key,"value":v[key]}));
                }
            }
            None => {
                incomplete = true;
                let mut issue = json!({"code":"InvalidSoakCounter","field":key});
                if let Some(value) = v.get(key) {
                    issue["value"] = value.clone();
                }
                issues.push(issue);
            }
        }
    }
    for key in ["Failure", "FatalError", "SourceChanges"] {
        if let Some(value) = v.get(key).filter(|value| super::nonempty(value)) {
            failed = true;
            issues.push(json!({"code":"CampaignFailure","field":key,"value":value}));
        }
    }
    if !matches!(declared, Some("Passed" | "Failed" | "Inconclusive")) {
        issues.push(json!({"code":"InvalidCampaignStatus","value":v.get("Status")}));
    }
    let verdict = if failed {
        "Failed"
    } else if !version_valid {
        "Unknown"
    } else {
        match declared {
            Some("Passed") if !incomplete => "Passed",
            Some("Passed" | "Inconclusive") => "Incomplete",
            _ => "Unknown",
        }
    };
    let mut identity = json!({});
    for key in ["RunId", "SourceRevision", "StartUtc", "EndUtc"] {
        if let Some(value) = v.get(key) {
            identity[key] = value.clone();
        }
    }
    if !identity
        .get("SourceRevision")
        .and_then(Value::as_str)
        .is_some_and(|s| !s.is_empty())
    {
        issues.push(json!({"code":"UnknownSourceRevision"}));
    }
    let mut metrics = v.clone();
    // Nonempty failure payloads are normalized issues, eligible for explicit compact
    // omission. A null failure remains null, distinguishable from an absent field.
    for key in ["Failure", "FatalError", "SourceChanges"] {
        if v.get(key).is_some_and(super::nonempty) {
            metrics.as_object_mut().unwrap().remove(key);
        }
    }
    json!({"kind":if version_valid {"ieeeDecimalSoak"} else {"unknown"},"identity":identity,"verdict":verdict,"metrics":metrics,"issueCount":issues.len(),"issues":issues,"omittedIssues":0})
}

/// Check arbitrary-precision JSON counters without narrowing or accepting fractions/strings.
fn unsigned_nonzero(value: &Value) -> Option<bool> {
    let text = value.to_string();
    (value.is_number() && !text.is_empty() && text.bytes().all(|b| b.is_ascii_digit()))
        .then(|| text.bytes().any(|b| b != b'0'))
}

fn integer_nonzero(value: &Value) -> Option<bool> {
    let text = value.to_string();
    let digits = text.strip_prefix('-').unwrap_or(&text);
    (value.is_number() && !digits.is_empty() && digits.bytes().all(|b| b.is_ascii_digit()))
        .then(|| digits.bytes().any(|b| b != b'0'))
}
