//! Typed, caller-supplied authorship metadata for durable command receipts.
//!
//! Clocks and identity providers live at adapters. The domain stores the value
//! supplied with the accepted command and never reads a system clock implicitly.

use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum AuditActor {
    Legacy,
    Human { subject: String },
    Ai { subject: String },
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AuditStamp {
    pub actor: AuditActor,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub recorded_at_utc: Option<String>,
}

impl Default for AuditStamp {
    fn default() -> Self {
        Self::legacy()
    }
}

impl AuditStamp {
    #[must_use]
    pub fn legacy() -> Self {
        Self {
            actor: AuditActor::Legacy,
            recorded_at_utc: None,
        }
    }

    pub fn human(
        subject: impl Into<String>,
        recorded_at_utc: impl Into<String>,
    ) -> Result<Self, String> {
        Self::new(
            AuditActor::Human {
                subject: subject.into(),
            },
            recorded_at_utc.into(),
        )
    }

    pub fn ai(
        subject: impl Into<String>,
        recorded_at_utc: impl Into<String>,
    ) -> Result<Self, String> {
        Self::new(
            AuditActor::Ai {
                subject: subject.into(),
            },
            recorded_at_utc.into(),
        )
    }

    fn new(actor: AuditActor, recorded_at_utc: String) -> Result<Self, String> {
        let stamp = Self {
            actor,
            recorded_at_utc: Some(recorded_at_utc),
        };
        stamp.validate()?;
        Ok(stamp)
    }

    pub fn validate(&self) -> Result<(), String> {
        match &self.actor {
            AuditActor::Legacy if self.recorded_at_utc.is_none() => Ok(()),
            AuditActor::Legacy => Err("legacy audit stamp cannot carry a timestamp".into()),
            AuditActor::Human { subject } | AuditActor::Ai { subject } => {
                if subject.is_empty() || subject.trim() != subject || subject.len() > 256 {
                    return Err(
                        "audit actor subject must be trimmed, nonempty and at most 256 bytes"
                            .into(),
                    );
                }
                let timestamp = self
                    .recorded_at_utc
                    .as_deref()
                    .ok_or("audit actor requires an explicit UTC timestamp")?;
                if is_rfc3339_utc(timestamp) {
                    Ok(())
                } else {
                    Err("audit timestamp must be RFC3339 UTC".into())
                }
            }
        }
    }

    #[must_use]
    pub fn is_legacy(&self) -> bool {
        matches!(self.actor, AuditActor::Legacy)
    }
}

fn is_rfc3339_utc(value: &str) -> bool {
    let bytes = value.as_bytes();
    if bytes.len() < 20 || !value.ends_with('Z') {
        return false;
    }
    for index in [0, 1, 2, 3, 5, 6, 8, 9, 11, 12, 14, 15, 17, 18] {
        if !bytes[index].is_ascii_digit() {
            return false;
        }
    }
    if !(bytes[4] == b'-'
        && bytes[7] == b'-'
        && bytes[10] == b'T'
        && bytes[13] == b':'
        && bytes[16] == b':')
    {
        return false;
    }
    let digits = |range: std::ops::Range<usize>| -> u32 {
        bytes[range]
            .iter()
            .fold(0, |value, byte| value * 10 + u32::from(byte - b'0'))
    };
    let year = digits(0..4);
    let month = digits(5..7);
    let day = digits(8..10);
    let hour = digits(11..13);
    let minute = digits(14..16);
    let second = digits(17..19);
    if year == 0 || month == 0 || month > 12 || hour > 23 || minute > 59 || second > 59 {
        return false;
    }
    let leap = year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
    let days = match month {
        1 | 3 | 5 | 7 | 8 | 10 | 12 => 31,
        4 | 6 | 9 | 11 => 30,
        2 if leap => 29,
        2 => 28,
        _ => return false,
    };
    if day == 0 || day > days {
        return false;
    }
    let fraction = &bytes[19..bytes.len() - 1];
    fraction.is_empty()
        || (fraction[0] == b'.'
            && fraction.len() > 1
            && fraction[1..].iter().all(u8::is_ascii_digit))
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn requires_injected_utc_time_for_human_and_ai() {
        assert!(AuditStamp::human("author", "2026-09-22T12:34:56Z").is_ok());
        assert!(AuditStamp::ai("assistant", "2026-09-22T12:34:56.125Z").is_ok());
        assert!(AuditStamp::human("author", "not-utc").is_err());
        assert!(AuditStamp::legacy().is_legacy());
    }

    #[test]
    fn rejects_out_of_range_calendar_and_clock_values() {
        for invalid in [
            "2026-00-01T00:00:00Z",
            "2026-13-01T00:00:00Z",
            "2026-04-31T00:00:00Z",
            "2025-02-29T00:00:00Z",
            "2026-01-01T24:00:00Z",
            "2026-01-01T23:60:00Z",
            "2026-01-01T23:59:60Z",
            "2026-01-01T23:59:abZ",
            "2026-01-01T23:59:59.Z",
            "2026-01-01T23:59:59.1.2Z",
        ] {
            assert!(AuditStamp::human("author", invalid).is_err(), "{invalid}");
        }
        assert!(AuditStamp::human("author", "2024-02-29T23:59:59.01Z").is_ok());
    }
}
