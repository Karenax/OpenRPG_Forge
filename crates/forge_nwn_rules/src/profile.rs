//! Explicit NWN rules profile boundary.
//!
//! The profile is separate from the native `OpenRPG` action rules. It selects a
//! small, sourced NWN:EE rules subset and never modifies the native ruleset.

use serde::{Deserialize, Serialize};
use std::fmt;

pub const PROFILE_SCHEMA: &str = "forge.nwn-rules-profile@1";
pub const DEFAULT_PROFILE_ID: &str = "nwn-ee-vanilla-m4-subset";

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum NwnRuleset {
    Vanilla,
    ShadowsOfUndrentide,
    HordesOfTheUnderdark,
}

impl NwnRuleset {
    #[must_use]
    pub const fn label(self) -> &'static str {
        match self {
            Self::Vanilla => "neverwinter-nights-ee-vanilla",
            Self::ShadowsOfUndrentide => "shadows-of-undrentide",
            Self::HordesOfTheUnderdark => "hordes-of-the-underdark",
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct NwnRulesProfile {
    pub schema: String,
    pub id: String,
    pub ruleset: NwnRuleset,
    pub max_level: u8,
    pub hostile_reputation_max: i32,
    pub critical_confirmation: bool,
    pub natural_one_misses: bool,
    pub natural_twenty_hits: bool,
    pub max_rounds: u32,
    pub move_speed_m_per_round: u32,
    pub melee_reach_m: u32,
    pub source: String,
}

impl NwnRulesProfile {
    #[must_use]
    pub fn vanilla_subset() -> Self {
        Self {
            schema: PROFILE_SCHEMA.to_owned(),
            id: DEFAULT_PROFILE_ID.to_owned(),
            ruleset: NwnRuleset::Vanilla,
            max_level: 40,
            hostile_reputation_max: 0,
            critical_confirmation: true,
            natural_one_misses: true,
            natural_twenty_hits: true,
            max_rounds: 50,
            move_speed_m_per_round: 3,
            melee_reach_m: 2,
            source: "NWN:EE nwscript.nss + synthetic M4 fixture; no 2DA copied".to_owned(),
        }
    }

    pub fn validate(&self) -> Result<(), ProfileError> {
        if self.schema != PROFILE_SCHEMA {
            return Err(ProfileError::Schema(self.schema.clone()));
        }
        if self.id.trim().is_empty() {
            return Err(ProfileError::EmptyId);
        }
        if self.max_level == 0 || self.max_level > 40 {
            return Err(ProfileError::MaxLevel(self.max_level));
        }
        if !(0..=100).contains(&self.hostile_reputation_max) {
            return Err(ProfileError::HostileThreshold(self.hostile_reputation_max));
        }
        if self.max_rounds == 0 || self.max_rounds > 1_000 {
            return Err(ProfileError::MaxRounds(self.max_rounds));
        }
        if self.move_speed_m_per_round == 0 || self.move_speed_m_per_round > 30 {
            return Err(ProfileError::MoveSpeed(self.move_speed_m_per_round));
        }
        if self.melee_reach_m == 0 || self.melee_reach_m > 10 {
            return Err(ProfileError::MeleeReach(self.melee_reach_m));
        }
        if self.source.trim().is_empty() {
            return Err(ProfileError::EmptySource);
        }
        Ok(())
    }

    pub fn from_json(text: &str) -> Result<Self, ProfileError> {
        let profile: Self =
            serde_json::from_str(text).map_err(|error| ProfileError::Json(error.to_string()))?;
        profile.validate()?;
        Ok(profile)
    }

    pub fn to_json_pretty(&self) -> Result<String, ProfileError> {
        serde_json::to_string_pretty(self).map_err(|error| ProfileError::Json(error.to_string()))
    }
}

impl Default for NwnRulesProfile {
    fn default() -> Self {
        Self::vanilla_subset()
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ProfileError {
    Schema(String),
    EmptyId,
    MaxLevel(u8),
    HostileThreshold(i32),
    MaxRounds(u32),
    MoveSpeed(u32),
    MeleeReach(u32),
    EmptySource,
    Json(String),
}

impl fmt::Display for ProfileError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Schema(schema) => write!(formatter, "unsupported rules profile schema {schema}"),
            Self::EmptyId => formatter.write_str("rules profile id must not be empty"),
            Self::MaxLevel(level) => write!(formatter, "invalid max level {level}"),
            Self::HostileThreshold(value) => {
                write!(formatter, "invalid hostile reputation threshold {value}")
            }
            Self::MaxRounds(rounds) => write!(formatter, "invalid max rounds {rounds}"),
            Self::MoveSpeed(speed) => write!(formatter, "invalid move speed {speed}"),
            Self::MeleeReach(reach) => write!(formatter, "invalid melee reach {reach}"),
            Self::EmptySource => formatter.write_str("rules profile source must not be empty"),
            Self::Json(detail) => write!(formatter, "invalid rules profile JSON: {detail}"),
        }
    }
}

impl std::error::Error for ProfileError {}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn default_profile_is_valid_and_separate() {
        let profile = NwnRulesProfile::vanilla_subset();
        assert_eq!(profile.validate(), Ok(()));
        assert_eq!(profile.id, DEFAULT_PROFILE_ID);
        assert_eq!(profile.ruleset.label(), "neverwinter-nights-ee-vanilla");
    }

    #[test]
    fn invalid_profiles_are_refused() {
        let mut profile = NwnRulesProfile::vanilla_subset();
        profile.max_level = 0;
        assert_eq!(profile.validate(), Err(ProfileError::MaxLevel(0)));
        profile = NwnRulesProfile::vanilla_subset();
        profile.schema = "other".to_owned();
        assert!(matches!(profile.validate(), Err(ProfileError::Schema(_))));
    }

    #[test]
    fn profile_round_trips_through_json() {
        let profile = NwnRulesProfile::vanilla_subset();
        let text = profile.to_json_pretty().expect("json");
        assert_eq!(NwnRulesProfile::from_json(&text).expect("parse"), profile);
    }

    #[test]
    fn committed_profile_contract_matches_the_default() {
        let text = include_str!("../../../docs/aurora-bridge/nwn-rules-profile.v1.json");
        assert_eq!(
            NwnRulesProfile::from_json(text).expect("profile"),
            NwnRulesProfile::vanilla_subset()
        );
    }
}
