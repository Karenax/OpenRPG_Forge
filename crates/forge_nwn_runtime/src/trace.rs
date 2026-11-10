//! Reproducible M5 trace: event -> script -> API -> mutation -> receipt.
//!
//! The trace is built only when explicitly requested (M5 command/report), so the
//! normal M3/M4 path does not pay for it. It contains no bundle path and no asset
//! bytes; messages come from the synthetic fixture.

use crate::m4::M4Report;
use forge_core::{ContentHash, canonical_json_sha256};
use serde::{Deserialize, Serialize};
use std::fmt;

pub const TRACE_SCHEMA: &str = "forge.aurora-m5-trace@1";
pub const MAX_TRACE_ENTRIES: usize = 4096;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TraceEntry {
    pub sequence: u64,
    pub phase: String,
    pub tick: u64,
    pub script: String,
    pub api: String,
    pub detail: String,
    pub receipt: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct NwnTrace {
    pub schema: String,
    pub module: String,
    pub seed: u64,
    pub redacted: bool,
    pub entries: Vec<TraceEntry>,
    pub entries_sha256: ContentHash,
}

impl NwnTrace {
    #[must_use]
    pub fn from_m4_report(report: &M4Report) -> Self {
        let mut entries = Vec::new();
        let mut sequence = 0_u64;

        for (index, event) in report.events.iter().enumerate() {
            push(
                &mut entries,
                &mut sequence,
                "event",
                u64::try_from(index).unwrap_or(0),
                "",
                "scenario_event",
                format!("{}: {}", event.step, event.detail),
                format!("event:{index}"),
            );
        }
        for (index, script) in report.executed_scripts.iter().enumerate() {
            push(
                &mut entries,
                &mut sequence,
                "script",
                0,
                script,
                "execute_ncs",
                "script executed".to_owned(),
                format!("script:{index}"),
            );
        }
        for (index, spell) in report.spells.iter().enumerate() {
            push(
                &mut entries,
                &mut sequence,
                "spell",
                u64::try_from(index).unwrap_or(0),
                &spell.spell,
                "cast_spell",
                format!(
                    "damage={} healing={} effect={:?} note={}",
                    spell.damage, spell.healing, spell.effect_added, spell.note
                ),
                format!("spell:{index}"),
            );
        }
        for (index, action) in report.combat.actions.iter().enumerate() {
            let (tick, script, api, detail) = match action {
                forge_nwn_rules::CombatAction::Move {
                    round,
                    actor,
                    distance_after,
                    ..
                } => (
                    u64::from(*round),
                    actor.clone(),
                    "move",
                    format!("distance_after={distance_after:.2}"),
                ),
                forge_nwn_rules::CombatAction::Attack { outcome } => (
                    u64::from(outcome.round),
                    outcome.attacker.clone(),
                    "resolve_attack",
                    format!(
                        "target={} natural={} total={} ac={} hit={} critical={} damage={} hp_after={} note={}",
                        outcome.target,
                        outcome.natural,
                        outcome.total,
                        outcome.target_ac,
                        outcome.hit,
                        outcome.critical_confirmed,
                        outcome.damage,
                        outcome.target_hp_after,
                        outcome.note
                    ),
                ),
            };
            push(
                &mut entries,
                &mut sequence,
                "combat",
                tick,
                &script,
                api,
                detail,
                format!("combat:{index}"),
            );
        }
        Self::from_entries(report.module.clone(), report.seed, entries)
    }

    #[must_use]
    pub fn from_entries(module: String, seed: u64, entries: Vec<TraceEntry>) -> Self {
        let entries_sha256 =
            canonical_json_sha256(&entries).unwrap_or_else(|_| ContentHash::from_bytes([0; 32]));
        Self {
            schema: TRACE_SCHEMA.to_owned(),
            module,
            seed,
            redacted: true,
            entries,
            entries_sha256,
        }
    }

    #[must_use]
    pub fn empty(module: String, seed: u64) -> Self {
        Self::from_entries(module, seed, Vec::new())
    }

    pub fn validate(&self) -> Result<(), TraceError> {
        if self.schema != TRACE_SCHEMA {
            return Err(TraceError::Schema(self.schema.clone()));
        }
        if !self.redacted {
            return Err(TraceError::NotRedacted);
        }
        if self.entries.len() > MAX_TRACE_ENTRIES {
            return Err(TraceError::TooMany(self.entries.len()));
        }
        for (index, entry) in self.entries.iter().enumerate() {
            if entry.sequence != u64::try_from(index).unwrap_or(u64::MAX) {
                return Err(TraceError::Sequence {
                    index,
                    sequence: entry.sequence,
                });
            }
            if entry.phase.is_empty() || entry.api.is_empty() || entry.receipt.is_empty() {
                return Err(TraceError::EmptyField(index));
            }
            if entry.detail.contains(":\\") || entry.detail.contains("/home/") {
                return Err(TraceError::UnredactedPath(index));
            }
        }
        Ok(())
    }
}

#[allow(clippy::too_many_arguments)]
fn push(
    entries: &mut Vec<TraceEntry>,
    sequence: &mut u64,
    phase: &str,
    tick: u64,
    script: &str,
    api: &str,
    detail: String,
    receipt: String,
) {
    entries.push(TraceEntry {
        sequence: *sequence,
        phase: phase.to_owned(),
        tick,
        script: script.to_owned(),
        api: api.to_owned(),
        detail,
        receipt,
    });
    *sequence += 1;
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum TraceError {
    Schema(String),
    NotRedacted,
    TooMany(usize),
    Sequence { index: usize, sequence: u64 },
    EmptyField(usize),
    UnredactedPath(usize),
}

impl fmt::Display for TraceError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Schema(schema) => write!(formatter, "unsupported trace schema {schema}"),
            Self::NotRedacted => formatter.write_str("trace must be marked redacted"),
            Self::TooMany(count) => write!(formatter, "trace entry limit exceeded ({count})"),
            Self::Sequence { index, sequence } => {
                write!(formatter, "trace entry {index} has sequence {sequence}")
            }
            Self::EmptyField(index) => write!(formatter, "trace entry {index} has an empty field"),
            Self::UnredactedPath(index) => {
                write!(formatter, "trace entry {index} contains an absolute path")
            }
        }
    }
}

impl std::error::Error for TraceError {}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::m4::M4Scenario;
    use std::path::PathBuf;

    fn bundle() -> PathBuf {
        PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1")
    }

    #[test]
    fn trace_is_deterministic_and_redacted() {
        let first = M4Scenario::load(&bundle(), 2026)
            .expect("load")
            .run()
            .expect("run");
        let second = M4Scenario::load(&bundle(), 2026)
            .expect("load")
            .run()
            .expect("run");
        let left = NwnTrace::from_m4_report(&first);
        let right = NwnTrace::from_m4_report(&second);
        assert_eq!(left, right);
        assert_eq!(left.validate(), Ok(()));
        assert!(left.redacted);
        assert!(left.entries.iter().any(|entry| entry.phase == "spell"));
        assert!(left.entries.iter().any(|entry| entry.phase == "combat"));
    }

    #[test]
    fn trace_refuses_unredacted_paths_and_bad_sequence() {
        let mut trace = NwnTrace::empty("ab_proving".to_owned(), 1);
        trace.entries.push(TraceEntry {
            sequence: 0,
            phase: "event".to_owned(),
            tick: 0,
            script: String::new(),
            api: "test".to_owned(),
            detail: "E:\\secret\\module".to_owned(),
            receipt: "event:0".to_owned(),
        });
        assert!(matches!(
            trace.validate(),
            Err(TraceError::UnredactedPath(0))
        ));
        trace.entries[0].detail = "safe".to_owned();
        trace.entries[0].sequence = 3;
        assert!(matches!(trace.validate(), Err(TraceError::Sequence { .. })));
    }
}
