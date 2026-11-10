//! M5 save migration, reproducible trace report and diff-oracle comparison.

use crate::m4::{M4Error, M4Report, M4Save, M4Scenario};
use crate::trace::{NwnTrace, TraceError};
use forge_core::{ContentHash, canonical_json_sha256};
use forge_nwn_rules::{ItemCatalog, TraceComparison, compare_encounters};
use serde::{Deserialize, Serialize};
use std::fmt;
use std::path::Path;

pub const M5_SAVE_SCHEMA: &str = "forge.aurora-m5-save@1";
pub const M5_REPORT_SCHEMA: &str = "forge.aurora-m5-report@1";
pub const M5_DIFF_SCHEMA: &str = "forge.aurora-m5-diff@1";
pub const M5_SEMANTIC_VERSION: u32 = 5;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct M5SaveMetadata {
    pub module: String,
    pub profile_id: String,
    pub source_schema: String,
    pub semantic_version: u32,
    pub created_by: String,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct M5Save {
    pub schema: String,
    pub metadata: M5SaveMetadata,
    pub m4: M4Save,
    pub trace: NwnTrace,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct M5Migration {
    pub from_schema: String,
    pub to_schema: String,
    pub accepted: bool,
    pub restored: bool,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct M5Report {
    pub schema: String,
    pub m4: M4Report,
    pub trace: NwnTrace,
    pub save: M5Save,
    pub save_sha256: ContentHash,
    pub migration: M5Migration,
    pub resume_idempotent: bool,
    pub repeated_transitions: usize,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct M5Divergence {
    pub path: String,
    pub expected: String,
    pub actual: String,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct M5Diff {
    pub schema: String,
    pub matches: bool,
    pub expected_trace_sha256: ContentHash,
    pub actual_trace_sha256: ContentHash,
    pub divergences: Vec<M5Divergence>,
    pub combat: TraceComparison,
}

#[derive(Debug, thiserror::Error)]
pub enum M5Error {
    #[error("M4 failed: {0}")]
    M4(#[from] M4Error),
    #[error("trace failed: {0}")]
    Trace(#[from] TraceError),
    #[error("save schema {0} is not supported")]
    UnsupportedSchema(String),
    #[error("save validation failed: {0}")]
    Save(String),
    #[error("JSON failed: {0}")]
    Json(String),
}

impl M5Save {
    pub fn from_m4(m4: M4Save, trace: NwnTrace, module: impl Into<String>) -> Self {
        Self {
            schema: M5_SAVE_SCHEMA.to_owned(),
            metadata: M5SaveMetadata {
                module: module.into(),
                profile_id: m4.rules.profile_id.clone(),
                source_schema: m4.schema.clone(),
                semantic_version: M5_SEMANTIC_VERSION,
                created_by: "openrpg-forge/aurora-bridge-m5".to_owned(),
            },
            m4,
            trace,
        }
    }

    pub fn validate(&self, catalog: &ItemCatalog) -> Result<(), M5Error> {
        if self.schema != M5_SAVE_SCHEMA {
            return Err(M5Error::UnsupportedSchema(self.schema.clone()));
        }
        if self.metadata.semantic_version != M5_SEMANTIC_VERSION {
            return Err(M5Error::Save(format!(
                "unsupported semantic version {}",
                self.metadata.semantic_version
            )));
        }
        if self.metadata.profile_id != self.m4.rules.profile_id {
            return Err(M5Error::Save(
                "metadata profile id differs from M4".to_owned(),
            ));
        }
        if self.trace.seed != self.m4.rules.seed {
            return Err(M5Error::Save("trace seed differs from M4".to_owned()));
        }
        self.m4
            .validate(catalog)
            .map_err(|error| M5Error::Save(error.to_string()))?;
        self.trace.validate().map_err(M5Error::Trace)?;
        Ok(())
    }

    pub fn from_json(text: &str, catalog: &ItemCatalog) -> Result<Self, M5Error> {
        let value: serde_json::Value =
            serde_json::from_str(text).map_err(|error| M5Error::Json(error.to_string()))?;
        let schema = value["schema"]
            .as_str()
            .ok_or_else(|| M5Error::Save("save has no schema".to_owned()))?
            .to_owned();
        match schema.as_str() {
            M5_SAVE_SCHEMA => {
                let save: Self = serde_json::from_value(value)
                    .map_err(|error| M5Error::Json(error.to_string()))?;
                save.validate(catalog)?;
                Ok(save)
            }
            "forge.aurora-m4-save@1" => {
                let m4: M4Save = serde_json::from_value(value)
                    .map_err(|error| M5Error::Json(error.to_string()))?;
                let trace = NwnTrace::empty("migrated-m4".to_owned(), m4.rules.seed);
                let save = Self::from_m4(m4, trace, "migrated-m4");
                save.validate(catalog)?;
                Ok(save)
            }
            _ => Err(M5Error::UnsupportedSchema(schema)),
        }
    }

    pub fn to_json_pretty(&self) -> Result<String, M5Error> {
        serde_json::to_string_pretty(self).map_err(|error| M5Error::Json(error.to_string()))
    }
}

pub fn run_m5(bundle: &Path, seed: u64) -> Result<M5Report, M5Error> {
    let catalog = ItemCatalog::synthetic().map_err(|error| M5Error::Save(error.to_string()))?;
    let mut scenario = M4Scenario::load(bundle, seed)?;
    let m4 = scenario.run()?;
    let m4_save = scenario.save()?;
    let trace = NwnTrace::from_m4_report(&m4);
    let save = M5Save::from_m4(m4_save.clone(), trace.clone(), m4.module.clone());
    save.validate(&catalog)?;

    let m5_json = save.to_json_pretty()?;
    let restored = M5Save::from_json(&m5_json, &catalog)?;
    let m4_json =
        serde_json::to_string(&m4_save).map_err(|error| M5Error::Json(error.to_string()))?;
    let migrated = M5Save::from_json(&m4_json, &catalog)?;
    let migration = M5Migration {
        from_schema: m4_save.schema.clone(),
        to_schema: M5_SAVE_SCHEMA.to_owned(),
        accepted: true,
        restored: restored == save,
    };
    let save_sha256 =
        canonical_json_sha256(&save).map_err(|error| M5Error::Json(error.to_string()))?;
    let resume_idempotent = m4.resume_idempotent
        && restored == save
        && migrated.validate(&catalog).is_ok()
        && migration.restored;
    Ok(M5Report {
        schema: M5_REPORT_SCHEMA.to_owned(),
        trace,
        m4: m4.clone(),
        save,
        save_sha256,
        migration,
        resume_idempotent,
        repeated_transitions: m4.transitions,
    })
}

#[must_use]
pub fn compare_m5(expected: &M5Report, actual: &M5Report) -> M5Diff {
    let mut divergences = Vec::new();
    if expected.trace.entries_sha256 != actual.trace.entries_sha256 {
        divergences.push(M5Divergence {
            path: "trace.entriesSha256".to_owned(),
            expected: expected.trace.entries_sha256.to_string(),
            actual: actual.trace.entries_sha256.to_string(),
        });
    }
    if expected.m4.gold != actual.m4.gold {
        divergences.push(M5Divergence {
            path: "gold".to_owned(),
            expected: expected.m4.gold.to_string(),
            actual: actual.m4.gold.to_string(),
        });
    }
    if expected.m4.journal_entries != actual.m4.journal_entries {
        divergences.push(M5Divergence {
            path: "journalEntries".to_owned(),
            expected: format!("{:?}", expected.m4.journal_entries),
            actual: format!("{:?}", actual.m4.journal_entries),
        });
    }
    if expected.m4.quest_status != actual.m4.quest_status {
        divergences.push(M5Divergence {
            path: "questStatus".to_owned(),
            expected: expected.m4.quest_status.clone(),
            actual: actual.m4.quest_status.clone(),
        });
    }
    if expected.m4.spells != actual.m4.spells {
        divergences.push(M5Divergence {
            path: "spells".to_owned(),
            expected: format!("{:?}", expected.m4.spells),
            actual: format!("{:?}", actual.m4.spells),
        });
    }
    if expected.save_sha256 != actual.save_sha256 {
        divergences.push(M5Divergence {
            path: "saveSha256".to_owned(),
            expected: expected.save_sha256.to_string(),
            actual: actual.save_sha256.to_string(),
        });
    }
    let combat = compare_encounters(&expected.m4.combat, &actual.m4.combat);
    for divergence in &combat.divergences {
        divergences.push(M5Divergence {
            path: format!("combat.{}", divergence.path),
            expected: divergence.expected.clone(),
            actual: divergence.actual.clone(),
        });
    }
    M5Diff {
        schema: M5_DIFF_SCHEMA.to_owned(),
        matches: divergences.is_empty(),
        expected_trace_sha256: expected.trace.entries_sha256,
        actual_trace_sha256: actual.trace.entries_sha256,
        divergences,
        combat,
    }
}

impl fmt::Display for M5Migration {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            formatter,
            "{} -> {} accepted={} restored={}",
            self.from_schema, self.to_schema, self.accepted, self.restored
        )
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    fn bundle() -> PathBuf {
        PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1")
    }

    #[test]
    fn m5_migrates_m4_and_restores_the_save() {
        let report = run_m5(&bundle(), 2026).expect("m5");
        assert_eq!(report.schema, M5_REPORT_SCHEMA);
        assert_eq!(report.migration.to_schema, M5_SAVE_SCHEMA);
        assert!(report.migration.accepted);
        assert!(report.migration.restored);
        assert!(report.resume_idempotent);
        assert_eq!(report.repeated_transitions, 2);
        assert!(report.trace.validate().is_ok());
    }

    #[test]
    fn unknown_save_schema_is_refused() {
        let catalog = ItemCatalog::synthetic().expect("catalog");
        let error = M5Save::from_json(r#"{"schema":"unknown@9"}"#, &catalog).expect_err("schema");
        assert!(matches!(error, M5Error::UnsupportedSchema(_)));
    }

    #[test]
    fn diff_detects_a_mutated_report() {
        let report = run_m5(&bundle(), 2026).expect("m5");
        assert!(compare_m5(&report, &report).matches);
        let mut mutated = report.clone();
        mutated.m4.gold += 1;
        let diff = compare_m5(&report, &mutated);
        assert!(!diff.matches);
        assert!(diff.divergences.iter().any(|item| item.path == "gold"));
    }
}
