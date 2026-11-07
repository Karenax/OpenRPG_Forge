//! Typed Studio protocol. No provider URL/key, scope policy or approval token from AI tools.
use forge_core::{CommandId, ContentHash};
use forge_project::{
    assistant::{
        SourceChange,
        jobs::{JobLimits, JobSnapshot},
        selection::{Region, Selection},
    },
    authoring::AuthoringState,
};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssistantDraft {
    pub expected_head: ContentHash,
    pub brief: String,
    pub selection: Selection,
    pub region: Region,
    pub resume_job: Option<CommandId>,
    #[serde(default)]
    pub assets: Option<crate::assistant_asset_protocol::AssetSelection>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
pub enum AssistantRequest {
    Configuration,
    InspectRecoveries {
        offset: usize,
        current_job: Option<CommandId>,
    },
    RecoverJob {
        job_id: CommandId,
        expected_saved: Option<ContentHash>,
        expected_pending: ContentHash,
        promote: bool,
    },
    Prepare {
        draft: AssistantDraft,
    },
    Generate {
        job_id: CommandId,
        expected_outbound_hash: ContentHash,
    },
    Inspect {
        job_id: CommandId,
    },
    Review {
        job_id: CommandId,
    },
    Apply {
        job_id: CommandId,
        expected_review_hash: ContentHash,
    },
    Interrupt {
        job_id: CommandId,
        attempt: u8,
    },
    Cancel {
        job_id: CommandId,
    },
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ProviderDisclosure {
    pub destination: String,
    pub model: String,
    pub reasoning: bool,
    pub maximum_request_seconds: u64,
    pub limits: JobLimits,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssistantView {
    pub provider: ProviderDisclosure,
    pub job: Option<JobSnapshot>,
    pub outbound_hash: Option<ContentHash>,
    pub outbound_body: Option<serde_json::Value>,
    pub review_hash: Option<ContentHash>,
    pub changes: Vec<SourceChange>,
    pub zone_name_before: Option<String>,
    pub authoring: Option<Box<AuthoringState>>,
    #[serde(default)]
    pub recovery_page: Option<JobRecoveryPage>,
    #[serde(default)]
    pub recovered_job: Option<CommandId>,
    pub message: String,
}

/// Presentation only; decisions re-read the native files and their exact hashes.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct JobRecoveryView {
    pub job_id: CommandId,
    pub saved_hash: Option<ContentHash>,
    pub pending_hash: ContentHash,
    pub saved: Option<JobSnapshot>,
    pub candidate: Option<JobSnapshot>,
    pub saved_error: Option<String>,
    pub candidate_error: Option<String>,
    pub can_promote: bool,
    pub can_keep_saved: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct JobRecoveryPage {
    pub offset: usize,
    pub next_offset: Option<usize>,
    pub jobs: Vec<JobRecoveryView>,
}

/// A concise presentation is safe only when every old value is exactly preserved.
/// All other diffs keep the complete before/after presentation.
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
pub(crate) fn placement_additions(
    change: &SourceChange,
) -> Option<(
    usize,
    Vec<forge_project::zone_authoring::placements::ManualPlacement>,
)> {
    use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};
    if change.path != "/corrections/placements" {
        return None;
    }
    let empty = Vec::new();
    let before = match change.before.as_ref() {
        None | Some(serde_json::Value::Null) => &empty,
        Some(value) => value.as_array()?,
    };
    let after = change.after.as_ref()?.as_array()?;
    let parse = |values: &[serde_json::Value]| {
        values
            .iter()
            .cloned()
            .map(serde_json::from_value::<ManualPlacement>)
            .collect::<Result<Vec<_>, _>>()
            .ok()
    };
    let old = parse(before)?;
    let new = parse(after)?;
    let counts = PlacementChanges::between(&old, &new).ok()?;
    if counts.added == 0 || counts.unchanged != old.len() || counts.removed != 0 {
        return None;
    }
    for (index, placement) in old.iter().enumerate() {
        let next = new
            .binary_search_by_key(&placement.id, |value| value.id)
            .ok()?;
        // Typed f32 equality alone could hide a small JSON number change.
        if before[index] != after[next] {
            return None;
        }
    }
    Some((
        old.len(),
        new.into_iter()
            .filter(|value| old.binary_search_by_key(&value.id, |old| old.id).is_err())
            .collect(),
    ))
}

#[cfg(test)]
mod presentation_tests {
    use super::*;
    use forge_core::{AssetId, EntityId, Transform};
    use forge_project::zone_authoring::placements::ManualPlacement;

    fn placement(name: &str) -> ManualPlacement {
        ManualPlacement {
            local_lights: None,
            id: EntityId::deterministic(name),
            name: name.into(),
            asset_id: AssetId::deterministic("tree"),
            asset_hash: forge_core::canonical_json_sha256(&"tree").unwrap(),
            physics: None,
            particles: None,
            transform: Transform::IDENTITY,
            locked: false,
        }
    }

    fn change(old: Vec<ManualPlacement>, mut new: Vec<ManualPlacement>) -> SourceChange {
        new.sort_by_key(|value| value.id);
        SourceChange {
            path: "/corrections/placements".into(),
            before: Some(serde_json::to_value(old).unwrap()),
            after: Some(serde_json::to_value(new).unwrap()),
        }
    }

    #[test]
    fn additions_preserve_the_old_locked_value_and_list_only_new_models() {
        let mut old = placement("old");
        old.locked = true;
        let added = placement("new");
        let diff = change(vec![old.clone()], vec![added.clone(), old]);
        assert_eq!(placement_additions(&diff), Some((1, vec![added])));
        let mut diff = change(vec![], vec![placement("first")]);
        diff.before = None;
        assert_eq!(placement_additions(&diff).unwrap().0, 0);
    }

    #[test]
    fn removals_modifications_duplicates_and_unknown_fields_keep_full_diff() {
        let old = placement("old");
        let added = placement("new");
        assert!(placement_additions(&change(vec![old.clone()], vec![added.clone()])).is_none());
        let mut moved = old.clone();
        moved.transform.translation_m.x = 1.0;
        assert!(
            placement_additions(&change(vec![old.clone()], vec![moved, added.clone()])).is_none()
        );
        assert!(
            placement_additions(&change(
                vec![old.clone()],
                vec![old.clone(), old.clone(), added.clone()]
            ))
            .is_none()
        );
        let mut diff = change(vec![old.clone()], vec![old, added]);
        diff.after.as_mut().unwrap()[0]["unexpected"] = true.into();
        assert!(placement_additions(&diff).is_none());
    }

    #[test]
    fn tiny_json_changes_are_not_hidden_by_f32_rounding() {
        let mut old = placement("old");
        old.transform.translation_m.x = 1.0;
        let mut diff = change(vec![old.clone()], vec![old.clone(), placement("new")]);
        let after = diff.after.as_mut().unwrap().as_array_mut().unwrap();
        let index = after
            .iter()
            .position(|value| value["id"] == serde_json::to_value(old.id).unwrap())
            .unwrap();
        after[index]["transform"]["translation_m"]["x"] = serde_json::json!(1.000_000_001);
        assert!(placement_additions(&diff).is_none());
    }
}
