//! Untrusted high-level provider plans compile into one existing zone transaction.
use super::selection::{DialogueLineId, SelectionScope};
use crate::{
    terrain_edit::{TerrainAction, TerrainEdit},
    zone_authoring::{
        ZoneAuthoringHead,
        placements::{ManualPlacement, PlacementEdit},
        session::{ZoneAction, ZoneRequest},
    },
};
use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, UnitQuaternion, Vec3};
use serde::{Deserialize, Serialize};

pub const MAX_PROPOSAL_BYTES: usize = 128 * 1024;
pub const MAX_PLAN_OPERATIONS: usize = 32;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "operation", rename_all = "snake_case", deny_unknown_fields)]
pub enum PlannedOperation {
    CreatePlaces {
        places: Vec<super::content::PlaceDraft>,
    },
    CreateFetchQuest {
        quest: Box<super::content::quest::FetchQuestDraft>,
    },
    AddContent {
        content: Box<super::content::ContentAddition>,
    },
    PlaceAsset {
        asset_id: AssetId,
        key: String,
        name: String,
        position: Vec3,
        yaw_degrees: f32,
        scale: f32,
    },
    RenameZone {
        name: String,
    },
    SetBaseElevation {
        metres: f32,
    },
    MovePlacement {
        id: EntityId,
        position: Vec3,
    },
    MoveObject {
        id: EntityId,
        position: Vec3,
    },
    SetDialogueText {
        dialogue_id: EntityId,
        node_id: EntityId,
        text: String,
    },
}

impl PlannedOperation {
    #[must_use]
    pub const fn capability(&self) -> &'static str {
        match self {
            Self::CreatePlaces { .. } => "create_places",
            Self::CreateFetchQuest { .. } => "create_fetch_quest",
            Self::AddContent { .. } => "add_content",
            Self::PlaceAsset { .. } => "place_asset",
            Self::RenameZone { .. } => "rename_zone",
            Self::SetBaseElevation { .. } => "set_base_elevation",
            Self::MovePlacement { .. } => "move_placement",
            Self::MoveObject { .. } => "move_object",
            Self::SetDialogueText { .. } => "set_dialogue_text",
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PlanProposal {
    pub context_hash: ContentHash,
    pub summary: String,
    pub missing_capabilities: Vec<String>,
    pub operations: Vec<PlannedOperation>,
}

impl PlanProposal {
    /// Treat provider text strictly as data; unknown operations/fields fail closed.
    pub fn from_bytes(bytes: &[u8]) -> Result<Self, String> {
        if bytes.len() > MAX_PROPOSAL_BYTES {
            return Err("Assistant proposal exceeds 128 KiB".into());
        }
        let proposal: Self = serde_json::from_slice(bytes).map_err(|error| error.to_string())?;
        proposal.validate()?;
        Ok(proposal)
    }

    pub fn validate(&self) -> Result<(), String> {
        if self.summary.trim().is_empty()
            || self.summary.len() > 4096
            || self.operations.len() > MAX_PLAN_OPERATIONS
            || self.missing_capabilities.len() > 32
            || self
                .missing_capabilities
                .iter()
                .any(|value| value.trim().is_empty() || value.len() > 256)
            || (self.operations.is_empty() && self.missing_capabilities.is_empty())
        {
            return Err(
                "Assistant proposal has invalid summary, capabilities or operation budget".into(),
            );
        }
        Ok(())
    }

    /// Builds only a proposal. Session/native validation and exact approval remain
    /// required. Multi-operation failures cannot partially mutate the active head.
    pub fn compile(
        &self,
        head: &ZoneAuthoringHead,
        scope: &SelectionScope,
        command: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        scope.validate_head(head)?;
        if self.context_hash != scope.context().content_hash()? {
            return Err("Plan targets a different shared context".into());
        }
        if !self.missing_capabilities.is_empty() {
            return Err("Plan requires unavailable capabilities; no mutation proposed".into());
        }
        let mut candidate = head.clone();
        for operation in &self.operations {
            if !scope
                .context()
                .supported_operations
                .iter()
                .any(|name| name == operation.capability())
            {
                return Err(format!(
                    "Operation unavailable in selected context: {}",
                    operation.capability()
                ));
            }
            let action = compile_operation(&candidate, scope, operation, command)?;
            let ZoneAction::Edit(corrections) = action else {
                return Err("Assistant operation must be a typed edit".into());
            };
            candidate.version = corrections.schema_version();
            candidate.corrections = *corrections;
            candidate.validate()?;
        }
        Ok(ZoneRequest {
            command_id: command,
            expected_revision: head.revision,
            expected_hash: head.content_hash()?,
            action: ZoneAction::edit(candidate.corrections),
        })
    }
}

fn compile_operation(
    head: &ZoneAuthoringHead,
    scope: &SelectionScope,
    operation: &PlannedOperation,
    command: CommandId,
) -> Result<ZoneAction, String> {
    let context = scope.context();
    let request = match operation {
        PlannedOperation::CreatePlaces { places } => {
            super::content::expand_places(places, command)?.compile(head, scope, command)?
        }
        PlannedOperation::CreateFetchQuest { quest } => {
            quest.expand(command)?.compile(head, scope, command)?
        }
        PlannedOperation::AddContent { content } => content.compile(head, scope, command)?,
        PlannedOperation::PlaceAsset { .. } => compile_placement(head, scope, operation, command)?,
        PlannedOperation::RenameZone { name } => head.prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::Rename { name: name.clone() }),
            command,
        )?,
        PlannedOperation::SetBaseElevation { metres } => {
            if !metres.is_finite()
                || *metres < context.region.minimum.y
                || *metres > context.region.maximum.y
            {
                return Err("Elevation exceeds the authorized region".into());
            }
            head.prepare_terrain_action(
                TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: *metres }),
                command,
            )?
        }
        PlannedOperation::MovePlacement { id, position } => {
            if !context
                .placements
                .iter()
                .any(|placement| placement.id == *id)
                || !context.region.contains(*position)
            {
                return Err(
                    "Placement or position is outside the authorized selection/region".into(),
                );
            }
            let mut transform = head
                .corrections
                .placements
                .as_ref()
                .and_then(|placements| placements.iter().find(|placement| placement.id == *id))
                .ok_or("Selected placement is absent")?
                .transform;
            transform.translation_m = *position;
            head.prepare_placement(command, PlacementEdit::Transform { id: *id, transform })?
        }
        PlannedOperation::MoveObject { id, position } => {
            if !context.objects.iter().any(|object| object.id == *id)
                || !context.region.contains(*position)
            {
                return Err("Object or position is outside the authorized selection/region".into());
            }
            let mut document = head
                .corrections
                .gameplay
                .as_deref()
                .ok_or("Gameplay is absent")?
                .clone();
            document
                .objects
                .iter_mut()
                .find(|object| object.id == *id)
                .ok_or("Selected object is absent")?
                .position = *position;
            head.prepare_gameplay(Some(document), command)?
        }
        PlannedOperation::SetDialogueText {
            dialogue_id,
            node_id,
            text,
        } => compile_dialogue_text(head, scope, *dialogue_id, *node_id, text, command)?,
    };
    Ok(request.action)
}

fn compile_dialogue_text(
    head: &ZoneAuthoringHead,
    scope: &SelectionScope,
    dialogue_id: EntityId,
    node_id: EntityId,
    text: &str,
    command: CommandId,
) -> Result<ZoneRequest, String> {
    let id = DialogueLineId {
        dialogue_id,
        node_id,
    };
    if !scope
        .context()
        .dialogue_lines
        .iter()
        .any(|line| line.id == id)
    {
        return Err("Dialogue line is outside the authorized selection".into());
    }
    let document = head
        .corrections
        .gameplay
        .as_deref()
        .ok_or("Gameplay is absent")?;
    let mut dialogue = document
        .dialogues
        .iter()
        .find(|dialogue| dialogue.id == dialogue_id)
        .ok_or("Selected dialogue is absent")?
        .clone();
    text.clone_into(
        &mut dialogue
            .nodes
            .iter_mut()
            .find(|node| node.id == node_id)
            .ok_or("Selected dialogue line is absent")?
            .text,
    );
    let updated = document
        .with_dialogue_definition(dialogue)
        .map_err(|error| error.to_string())?;
    head.prepare_gameplay(Some(updated), command)
}

fn compile_placement(
    head: &ZoneAuthoringHead,
    scope: &SelectionScope,
    operation: &PlannedOperation,
    command: CommandId,
) -> Result<ZoneRequest, String> {
    let PlannedOperation::PlaceAsset {
        asset_id,
        key,
        name,
        position,
        yaw_degrees,
        scale,
    } = operation
    else {
        return Err("Expected an asset placement operation".into());
    };
    let catalog = scope
        .context()
        .asset_catalog
        .as_ref()
        .filter(|catalog| catalog.allow_placement)
        .ok_or("Asset placement was not authorized")?;
    let asset = catalog
        .assets
        .iter()
        .find(|asset| asset.asset_id == *asset_id && asset.model_verified)
        .ok_or("Asset is not a verified selected model")?;
    if key.is_empty()
        || key.len() > 64
        || !key
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'_' | b'-'))
        || !scope.context().region.contains(*position)
        || !yaw_degrees.is_finite()
        || !scale.is_finite()
        || *scale <= 0.0
        || *scale > 100.0
    {
        return Err("Invalid placement key, region, rotation or scale".into());
    }
    let half_angle = (yaw_degrees.rem_euclid(360.0) * 0.5).to_radians();
    let transform = Transform {
        translation_m: *position,
        rotation: UnitQuaternion::new(0.0, half_angle.sin(), 0.0, half_angle.cos()),
        scale: Vec3::new(*scale, *scale, *scale),
    };
    head.prepare_placement(
        command,
        PlacementEdit::Insert {
            placement: ManualPlacement {
                local_lights: None,
                id: EntityId::deterministic(format!("forge/assistant/{command}/placement/{key}")),
                name: name.clone(),
                asset_id: *asset_id,
                asset_hash: asset.model_hash.ok_or("Verified asset has no model pin")?,
                physics: asset.physics.clone(),
                particles: asset.particles.clone(),
                transform,
                locked: false,
            },
        },
    )
}
