//! Read-only qualification exports, not project saves or gate acceptance.
use serde_json::{Value, json};

const MAX_LEDGER_BYTES: usize = 1_048_576;

// Shared with the browser collector so tests cover the actual exported ledger registration.
const VOLUMETRIC_LEDGER: (&str, &str) = ("volumetric", "studio-volumetric-ledger");

pub(crate) fn diagnostic_report(
    recorded_at: &str,
    ledgers: &[(&str, Option<String>)],
    statuses: &[(&str, Option<String>)],
) -> Result<Vec<u8>, String> {
    let mut observations = serde_json::Map::new();
    let mut total = 0_usize;
    for (name, text) in ledgers {
        total = total.saturating_add(text.as_ref().map_or(0, String::len));
        if total > MAX_LEDGER_BYTES {
            return Err("Diagnostics trop volumineux (limite 1 Mio)".to_owned());
        }
        let value = if let Some(text) = text.as_deref().filter(|text| !text.trim().is_empty()) {
            serde_json::from_str(text)
                .map_err(|error| format!("Diagnostic {name} invalide : {error}"))?
        } else {
            Value::Null
        };
        if observations.insert((*name).to_owned(), value).is_some() {
            return Err(format!("Diagnostic répété : {name}"));
        }
    }
    let mut text_statuses = serde_json::Map::new();
    for (name, text) in statuses {
        total = total.saturating_add(text.as_ref().map_or(0, String::len));
        if total > MAX_LEDGER_BYTES {
            return Err("Diagnostics trop volumineux (limite 1 Mio)".to_owned());
        }
        if text_statuses
            .insert((*name).to_owned(), json!(text))
            .is_some()
        {
            return Err(format!("Statut répété : {name}"));
        }
    }
    let bytes = serde_json::to_vec(&json!({
        "schema": "forge.studio-diagnostic-export",
        "schema_version": 1,
        "recorded_at_utc": recorded_at,
        "evidence_source": "snapshot_of_existing_studio_observation_ledgers",
        "scope": "read_only_diagnostics_not_project_save",
        "gpu_capture_pair_atomic": false,
        "human_acceptance": false,
        "gate_passed": false,
        "observations": observations,
        "displayed_statuses": text_statuses
    }))
    .map_err(|error| error.to_string())?;
    if bytes.len() > MAX_LEDGER_BYTES {
        return Err("Export sérialisé trop volumineux (limite 1 Mio)".to_owned());
    }
    Ok(bytes)
}

pub(crate) fn supported_capture_size(width: u32, height: u32) -> bool {
    width > 0 && height > 0 && width <= 3840 && height <= 2160
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod browser;
#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub(crate) use browser::{ProofExportControls, install};

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn volumetric_observation_preserves_fallback_and_absence() {
        assert_eq!(VOLUMETRIC_LEDGER.1, "studio-volumetric-ledger");
        for observation in [
            Some(
                r#"{"applied":true,"requested_step_count":64,"resolved_path":"volumetric_preview_plus_distance_fog_unqualified","effective_maximum_distance_m":128.0}"#,
            ),
            Some(
                r#"{"applied":false,"requested_step_count":0,"resolved_path":"distance_fog_fallback"}"#,
            ),
            None,
        ] {
            let report: Value = serde_json::from_slice(
                &diagnostic_report(
                    "test",
                    &[(VOLUMETRIC_LEDGER.0, observation.map(str::to_owned))],
                    &[],
                )
                .unwrap(),
            )
            .unwrap();
            let expected =
                observation.map_or(Value::Null, |text| serde_json::from_str(text).unwrap());
            assert_eq!(report["observations"]["volumetric"], expected);
            assert_eq!(report["gate_passed"], false);
            assert_eq!(report["gpu_capture_pair_atomic"], false);
        }
    }

    #[test]
    fn exported_observations_do_not_promote_partial_results() {
        let bytes = diagnostic_report(
            "2026-09-06T00:00:00Z",
            &[(
                "quality",
                Some(r#"{"full_preset_applied":false,"requested_tier":"high"}"#.to_owned()),
            )],
            &[("probe", Some("failed: watchdog".to_owned()))],
        )
        .unwrap();
        let value: Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(value["schema_version"], 1);
        assert_eq!(
            value["observations"]["quality"]["full_preset_applied"],
            false
        );
        assert_eq!(value["displayed_statuses"]["probe"], "failed: watchdog");
        assert_eq!(value["gate_passed"], false);
        assert_eq!(value["human_acceptance"], false);
        assert_eq!(value["gpu_capture_pair_atomic"], false);
    }

    #[test]
    fn absent_ledger_is_explicit_not_an_invented_success() {
        let value: Value = serde_json::from_slice(
            &diagnostic_report(
                "test",
                &[("scene", None), ("quality", Some(String::new()))],
                &[],
            )
            .unwrap(),
        )
        .unwrap();
        assert!(value["observations"]["scene"].is_null());
        assert!(value["observations"]["quality"].is_null());
    }

    #[test]
    fn malformed_duplicate_and_oversized_diagnostics_are_rejected() {
        assert!(diagnostic_report("test", &[("scene", Some("invalid".to_owned()))], &[]).is_err());
        assert!(diagnostic_report("test", &[("scene", None), ("scene", None)], &[]).is_err());
        assert!(diagnostic_report("test", &[], &[("probe", None), ("probe", None)]).is_err());
        assert!(
            diagnostic_report(
                "test",
                &[("scene", Some(" ".repeat(MAX_LEDGER_BYTES) + "{}"))],
                &[]
            )
            .is_err()
        );
        assert!(
            diagnostic_report(
                "test",
                &[],
                &[("probe", Some("x".repeat(MAX_LEDGER_BYTES + 1)))]
            )
            .is_err()
        );
    }

    #[test]
    fn capture_allocation_is_bounded_but_small_views_are_not_called_full_hd() {
        assert!(supported_capture_size(1408, 797));
        assert!(supported_capture_size(3840, 2160));
        for (width, height) in [
            (0, 797),
            (1408, 0),
            (3841, 2160),
            (3840, 2161),
            (u32::MAX, u32::MAX),
        ] {
            assert!(!supported_capture_size(width, height));
        }
    }

    #[test]
    fn whitespace_and_json_escaping_cannot_evade_the_export_limit() {
        assert!(
            diagnostic_report(
                "test",
                &[("scene", Some(" ".repeat(MAX_LEDGER_BYTES + 1)))],
                &[]
            )
            .is_err()
        );
        assert!(
            diagnostic_report(
                "test",
                &[],
                &[("probe", Some("\0".repeat(MAX_LEDGER_BYTES / 4)))]
            )
            .is_err()
        );
    }
}
