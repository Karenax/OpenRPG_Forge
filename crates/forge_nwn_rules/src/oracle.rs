//! Oracle comparison harness for combat traces.
//!
//! The real NWN local oracle was not executed for M4. This module provides the
//! deterministic comparison primitive and keeps the absence of a real oracle
//! explicit in the report.

use crate::combat::{CombatAction, EncounterReport};
use serde::{Deserialize, Serialize};

pub const ORACLE_SCHEMA: &str = "forge.nwn-oracle-comparison@1";
pub const ORACLE_LOG_SCHEMA: &str = "forge.nwn-oracle-log@1";

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TraceDivergence {
    pub path: String,
    pub expected: String,
    pub actual: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TraceComparison {
    pub schema: String,
    pub matches: bool,
    pub divergences: Vec<TraceDivergence>,
}

#[must_use]
pub fn compare_encounters(expected: &EncounterReport, actual: &EncounterReport) -> TraceComparison {
    let mut divergences = Vec::new();
    if expected.winner != actual.winner {
        divergences.push(TraceDivergence {
            path: "winner".to_owned(),
            expected: format!("{:?}", expected.winner),
            actual: format!("{:?}", actual.winner),
        });
    }
    if expected.rounds != actual.rounds {
        divergences.push(TraceDivergence {
            path: "rounds".to_owned(),
            expected: expected.rounds.to_string(),
            actual: actual.rounds.to_string(),
        });
    }
    if expected.actions.len() != actual.actions.len() {
        divergences.push(TraceDivergence {
            path: "actions.len".to_owned(),
            expected: expected.actions.len().to_string(),
            actual: actual.actions.len().to_string(),
        });
    }
    for (index, (left, right)) in expected
        .actions
        .iter()
        .zip(actual.actions.iter())
        .enumerate()
    {
        match (left, right) {
            (CombatAction::Move { .. }, CombatAction::Move { .. }) => {}
            (CombatAction::Attack { outcome: left }, CombatAction::Attack { outcome: right }) => {
                for (field, expected, actual) in [
                    (
                        "natural",
                        left.natural.to_string(),
                        right.natural.to_string(),
                    ),
                    ("total", left.total.to_string(), right.total.to_string()),
                    ("damage", left.damage.to_string(), right.damage.to_string()),
                    (
                        "target_hp_after",
                        left.target_hp_after.to_string(),
                        right.target_hp_after.to_string(),
                    ),
                    ("note", left.note.clone(), right.note.clone()),
                ] {
                    if expected != actual {
                        divergences.push(TraceDivergence {
                            path: format!("actions[{index}].{field}"),
                            expected,
                            actual,
                        });
                    }
                }
            }
            _ => divergences.push(TraceDivergence {
                path: format!("actions[{index}].kind"),
                expected: action_kind(left).to_owned(),
                actual: action_kind(right).to_owned(),
            }),
        }
    }
    TraceComparison {
        schema: ORACLE_SCHEMA.to_owned(),
        matches: divergences.is_empty(),
        divergences,
    }
}

fn action_kind(action: &CombatAction) -> &'static str {
    match action {
        CombatAction::Move { .. } => "move",
        CombatAction::Attack { .. } => "attack",
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct OracleAttack {
    pub round: u32,
    pub attacker: String,
    pub target: String,
    pub natural: u32,
    pub total: i32,
    pub target_ac: i32,
    pub hit: bool,
    pub damage: i32,
    pub target_hp_after: i32,
    pub critical: bool,
    pub concealment_miss: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct OracleLog {
    pub schema: String,
    pub seed: u64,
    pub winner: Option<String>,
    pub rounds: u32,
    pub attacks: Vec<OracleAttack>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum OracleLogError {
    Schema(String),
    Line { number: usize, detail: String },
    Field(String),
}

impl std::fmt::Display for OracleLogError {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Schema(schema) => write!(formatter, "unsupported oracle log schema {schema}"),
            Self::Line { number, detail } => {
                write!(formatter, "oracle log line {number}: {detail}")
            }
            Self::Field(field) => {
                write!(formatter, "oracle log field {field} is absent or invalid")
            }
        }
    }
}

impl std::error::Error for OracleLogError {}

impl OracleLog {
    /// Parse the documented line-oriented external oracle format. Unknown keys
    /// are refused so a silently wrong log cannot be compared successfully.
    pub fn parse(text: &str) -> Result<Self, OracleLogError> {
        let mut schema = None;
        let mut seed = None;
        let mut winner = None;
        let mut rounds = None;
        let mut attacks = Vec::new();
        for (index, raw) in text.lines().enumerate() {
            let line = raw.trim();
            if line.is_empty() || line.starts_with('#') {
                continue;
            }
            let mut tokens = line.split_whitespace();
            let kind = tokens.next().unwrap_or_default();
            let mut fields = std::collections::BTreeMap::new();
            for token in tokens {
                let Some((key, value)) = token.split_once('=') else {
                    return Err(OracleLogError::Line {
                        number: index + 1,
                        detail: format!("token {token} is not key=value"),
                    });
                };
                fields.insert(key.to_owned(), value.to_owned());
            }
            match kind {
                "schema" => {
                    schema = Some(fields.get("value").cloned().unwrap_or_default());
                }
                "seed" => {
                    seed = Some(
                        fields
                            .get("value")
                            .and_then(|value| value.parse().ok())
                            .ok_or_else(|| OracleLogError::Field("seed".to_owned()))?,
                    );
                }
                "winner" => {
                    winner = match fields.get("value").map(String::as_str) {
                        Some("none") | None => None,
                        Some(value) => Some(value.to_owned()),
                    };
                }
                "rounds" => {
                    rounds = Some(
                        fields
                            .get("value")
                            .and_then(|value| value.parse().ok())
                            .ok_or_else(|| OracleLogError::Field("rounds".to_owned()))?,
                    );
                }
                "attack" => {
                    attacks.push(OracleAttack {
                        round: parse_field(&fields, "round")?,
                        attacker: required(&fields, "attacker")?,
                        target: required(&fields, "target")?,
                        natural: parse_field(&fields, "natural")?,
                        total: parse_field(&fields, "total")?,
                        target_ac: parse_field(&fields, "ac")?,
                        hit: parse_field(&fields, "hit")?,
                        damage: parse_field(&fields, "damage")?,
                        target_hp_after: parse_field(&fields, "hp")?,
                        critical: parse_field(&fields, "critical")?,
                        concealment_miss: parse_field(&fields, "concealment")?,
                    });
                }
                other => {
                    return Err(OracleLogError::Line {
                        number: index + 1,
                        detail: format!("unknown record {other}"),
                    });
                }
            }
        }
        let schema = schema.ok_or_else(|| OracleLogError::Field("schema".to_owned()))?;
        if schema != ORACLE_LOG_SCHEMA {
            return Err(OracleLogError::Schema(schema));
        }
        Ok(Self {
            schema,
            seed: seed.ok_or_else(|| OracleLogError::Field("seed".to_owned()))?,
            winner,
            rounds: rounds.ok_or_else(|| OracleLogError::Field("rounds".to_owned()))?,
            attacks,
        })
    }

    /// Compare an external log with a Forge encounter. Only attacks are
    /// compared; movement is a Forge-side implementation detail.
    #[must_use]
    pub fn compare(&self, report: &EncounterReport) -> TraceComparison {
        let mut divergences = Vec::new();
        if self.winner != report.winner {
            divergences.push(TraceDivergence {
                path: "winner".to_owned(),
                expected: format!("{:?}", self.winner),
                actual: format!("{:?}", report.winner),
            });
        }
        if self.rounds != report.rounds {
            divergences.push(TraceDivergence {
                path: "rounds".to_owned(),
                expected: self.rounds.to_string(),
                actual: report.rounds.to_string(),
            });
        }
        let forge_attacks = report
            .actions
            .iter()
            .filter_map(|action| match action {
                CombatAction::Attack { outcome } => Some(outcome),
                CombatAction::Move { .. } => None,
            })
            .collect::<Vec<_>>();
        if self.attacks.len() != forge_attacks.len() {
            divergences.push(TraceDivergence {
                path: "attacks.len".to_owned(),
                expected: self.attacks.len().to_string(),
                actual: forge_attacks.len().to_string(),
            });
        }
        for (index, (expected, actual)) in self.attacks.iter().zip(forge_attacks).enumerate() {
            for (field, expected, actual) in [
                (
                    "round",
                    expected.round.to_string(),
                    actual.round.to_string(),
                ),
                (
                    "attacker",
                    expected.attacker.clone(),
                    actual.attacker.clone(),
                ),
                ("target", expected.target.clone(), actual.target.clone()),
                (
                    "natural",
                    expected.natural.to_string(),
                    actual.natural.to_string(),
                ),
                (
                    "total",
                    expected.total.to_string(),
                    actual.total.to_string(),
                ),
                (
                    "ac",
                    expected.target_ac.to_string(),
                    actual.target_ac.to_string(),
                ),
                ("hit", expected.hit.to_string(), actual.hit.to_string()),
                (
                    "damage",
                    expected.damage.to_string(),
                    actual.damage.to_string(),
                ),
                (
                    "hp",
                    expected.target_hp_after.to_string(),
                    actual.target_hp_after.to_string(),
                ),
                (
                    "critical",
                    expected.critical.to_string(),
                    actual.critical_confirmed.to_string(),
                ),
                (
                    "concealment",
                    expected.concealment_miss.to_string(),
                    actual.concealment_miss.to_string(),
                ),
            ] {
                if expected != actual {
                    divergences.push(TraceDivergence {
                        path: format!("attacks[{index}].{field}"),
                        expected,
                        actual,
                    });
                }
            }
        }
        TraceComparison {
            schema: ORACLE_SCHEMA.to_owned(),
            matches: divergences.is_empty(),
            divergences,
        }
    }
}

fn required(
    fields: &std::collections::BTreeMap<String, String>,
    key: &str,
) -> Result<String, OracleLogError> {
    fields
        .get(key)
        .cloned()
        .ok_or_else(|| OracleLogError::Field(key.to_owned()))
}

fn parse_field<T: std::str::FromStr>(
    fields: &std::collections::BTreeMap<String, String>,
    key: &str,
) -> Result<T, OracleLogError> {
    fields
        .get(key)
        .and_then(|value| value.parse().ok())
        .ok_or_else(|| OracleLogError::Field(key.to_owned()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::character::{CharacterState, CharacterTemplate, Position};
    use crate::factions::FactionTable;
    use crate::profile::NwnRulesProfile;
    use crate::rng::DeterministicRng;

    fn encounter() -> EncounterReport {
        let profile = NwnRulesProfile::vanilla_subset();
        let factions = FactionTable::default();
        let fighter = CharacterState::new(
            CharacterTemplate::synthetic_fighter(),
            Position::new(0.0, 0.0),
        );
        let mut guardian = CharacterTemplate::synthetic_fighter();
        guardian.id = "guardian".to_owned();
        guardian.faction_id = 1;
        guardian.hit_points = 30;
        let guardian = CharacterState::new(guardian, Position::new(0.0, 3.0));
        let mut rng = DeterministicRng::new(3);
        crate::combat::run_encounter(
            &profile,
            &factions,
            &fighter,
            &guardian,
            crate::combat::CombatModifiers::default(),
            crate::combat::CombatModifiers::default(),
            &mut rng,
        )
        .expect("encounter")
        .report
    }

    #[test]
    fn identical_traces_match() {
        let report = encounter();
        assert!(compare_encounters(&report, &report).matches);
    }

    #[test]
    fn a_mutated_trace_is_reported() {
        let expected = encounter();
        let mut actual = expected.clone();
        actual.rounds += 1;
        let comparison = compare_encounters(&expected, &actual);
        assert!(!comparison.matches);
        assert!(
            comparison
                .divergences
                .iter()
                .any(|divergence| divergence.path == "rounds")
        );
    }

    #[test]
    fn external_oracle_log_is_parsed_and_compared() {
        use std::fmt::Write as _;

        let report = encounter();
        let mut log = format!(
            "schema value={ORACLE_LOG_SCHEMA}\nseed value=2026\nwinner value={}\nrounds value={}\n",
            report.winner.as_deref().unwrap_or("none"),
            report.rounds
        );
        for action in &report.actions {
            if let CombatAction::Attack { outcome } = action {
                writeln!(
                    log,
                    "attack round={} attacker={} target={} natural={} total={} ac={} hit={} damage={} hp={} critical={} concealment={}",
                    outcome.round,
                    outcome.attacker,
                    outcome.target,
                    outcome.natural,
                    outcome.total,
                    outcome.target_ac,
                    outcome.hit,
                    outcome.damage,
                    outcome.target_hp_after,
                    outcome.critical_confirmed,
                    outcome.concealment_miss
                )
                .expect("log write");
            }
        }
        let parsed = OracleLog::parse(&log).expect("log");
        assert!(parsed.compare(&report).matches);
        let mutated = log.replace("damage=", "damage=9");
        let parsed = OracleLog::parse(&mutated).expect("mutated log");
        let comparison = parsed.compare(&report);
        assert!(!comparison.matches);
        assert!(
            comparison
                .divergences
                .iter()
                .any(|divergence| divergence.path.starts_with("attacks[0].damage"))
        );
    }
}
