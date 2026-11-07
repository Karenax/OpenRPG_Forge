//! Revision-bound selection and the exact, bounded context eligible for sharing.
use super::ScopePolicy;
use crate::zone_authoring::{ZoneAuthoringHead, placements::ManualPlacement};
use forge_core::{
    AreaId, ContentHash, EntityId, Revision, Vec3, canonical_json_bytes, canonical_json_sha256,
};
use serde::{Deserialize, Serialize};
use std::collections::BTreeSet;

pub const MAX_CONTEXT_BYTES: usize = 64 * 1024;
const MAX_SELECTION: usize = 64;

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Region {
    pub minimum: Vec3,
    pub maximum: Vec3,
}

impl Region {
    #[must_use]
    pub fn contains(self, point: Vec3) -> bool {
        point.is_finite()
            && point.x >= self.minimum.x
            && point.x <= self.maximum.x
            && point.y >= self.minimum.y
            && point.y <= self.maximum.y
            && point.z >= self.minimum.z
            && point.z <= self.maximum.z
    }

    fn validate(self, head: &ZoneAuthoringHead) -> Result<(), String> {
        let extent = head.base.terrain.extent_m;
        if !self.minimum.is_finite()
            || !self.maximum.is_finite()
            || !self.contains(self.minimum)
            || !self.contains(self.maximum)
            || self.minimum.x < -extent.size_x_m * 0.5
            || self.maximum.x > extent.size_x_m * 0.5
            || self.minimum.z < -extent.size_z_m * 0.5
            || self.maximum.z > extent.size_z_m * 0.5
        {
            return Err("Assistant region must be finite, ordered and inside the zone".into());
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DialogueLineId {
    pub dialogue_id: EntityId,
    pub node_id: EntityId,
}

/// A user selection request. Only `SelectionScope::new` resolves its authority.
#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Selection {
    pub placements: Vec<EntityId>,
    pub objects: Vec<EntityId>,
    pub dialogue_lines: Vec<DialogueLineId>,
    pub zone_properties: bool,
    #[serde(default)]
    pub create_content: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct SelectedDialogueLine {
    pub id: DialogueLineId,
    pub text: String,
}

/// Derived context, with no local paths, full archives, unselected dialogue or secrets.
#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct PlanningContext {
    pub schema: &'static str,
    pub area_id: AreaId,
    pub base_hash: ContentHash,
    pub base_revision: Revision,
    pub region: Region,
    pub zone_name: String,
    pub zone_properties: bool,
    pub rules_profile: Option<String>,
    pub placements: Vec<ManualPlacement>,
    pub objects: Vec<forge_gameplay::ObjectDefinition>,
    pub dialogue_lines: Vec<SelectedDialogueLine>,
    pub supported_operations: Vec<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub asset_catalog: Option<super::assets::CatalogContext>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub creation: Option<super::content::CreationContext>,
}

impl PlanningContext {
    pub fn content_hash(&self) -> Result<ContentHash, String> {
        canonical_json_sha256(self).map_err(|error| error.to_string())
    }
}

/// Not deserializable from provider output. The native application constructs it
/// from the user's selection and revalidates it against the current head.
#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct SelectionScope {
    context: PlanningContext,
    policy: ScopePolicy,
}

impl SelectionScope {
    pub fn new(
        head: &ZoneAuthoringHead,
        selection: &Selection,
        region: Region,
    ) -> Result<Self, String> {
        Self::build(head, selection, region, None)
    }

    pub fn new_with_assets(
        head: &ZoneAuthoringHead,
        selection: &Selection,
        region: Region,
        catalog: &super::assets::AssetCatalog,
    ) -> Result<Self, String> {
        Self::build(head, selection, region, Some(catalog))
    }

    fn build(
        head: &ZoneAuthoringHead,
        selection: &Selection,
        region: Region,
        catalog: Option<&super::assets::AssetCatalog>,
    ) -> Result<Self, String> {
        head.validate()?;
        region.validate(head)?;
        let count =
            selection.placements.len() + selection.objects.len() + selection.dialogue_lines.len();
        if count > MAX_SELECTION
            || selection.placements.iter().collect::<BTreeSet<_>>().len()
                != selection.placements.len()
            || selection.objects.iter().collect::<BTreeSet<_>>().len() != selection.objects.len()
            || selection
                .dialogue_lines
                .iter()
                .collect::<BTreeSet<_>>()
                .len()
                != selection.dialogue_lines.len()
        {
            return Err("Assistant selection exceeds 64 entries or contains duplicates".into());
        }
        let mut context = PlanningContext {
            schema: "forge.assistant-context.v1",
            area_id: head.base.terrain.area_id,
            base_hash: head.content_hash()?,
            base_revision: head.revision,
            region,
            zone_name: head.effective()?.0.name,
            zone_properties: selection.zone_properties,
            rules_profile: head
                .corrections
                .gameplay
                .as_ref()
                .map(|document| document.profile.id.clone()),
            placements: Vec::new(),
            objects: Vec::new(),
            dialogue_lines: Vec::new(),
            supported_operations: Vec::new(),
            asset_catalog: None,
            creation: None,
        };
        let mut paths = Vec::new();
        if selection.zone_properties {
            paths.extend([
                "/corrections/name".into(),
                "/corrections/base_elevation_m".into(),
            ]);
            context
                .supported_operations
                .extend(["rename_zone".into(), "set_base_elevation".into()]);
        }
        let placements = head.corrections.placements.as_deref().unwrap_or_default();
        for id in &selection.placements {
            let index = placements
                .iter()
                .position(|placement| placement.id == *id)
                .ok_or("Unknown selected placement")?;
            let placement = &placements[index];
            if placement.locked || !region.contains(placement.transform.translation_m) {
                return Err("Selected placement is locked or outside the authorized region".into());
            }
            context.placements.push(placement.clone());
            paths.push(format!(
                "/corrections/placements/{index}/transform/translation_m"
            ));
        }
        if !context.placements.is_empty() {
            context.supported_operations.push("move_placement".into());
        }
        append_gameplay(head, selection, &mut context, &mut paths)?;
        append_creation(head, selection, &mut context, &mut paths)?;
        if let Some(catalog) = catalog {
            context.asset_catalog = Some(catalog.context().clone());
            if catalog.context().allow_placement {
                context.supported_operations.push("place_asset".into());
                paths.push("/corrections/placements".into());
            }
        }
        if canonical_json_bytes(&context)
            .map_err(|error| error.to_string())?
            .len()
            > MAX_CONTEXT_BYTES
        {
            return Err("Assistant context exceeds 64 KiB; narrow the selection".into());
        }
        Ok(Self {
            context,
            policy: ScopePolicy::new(paths, Vec::new())?,
        })
    }

    #[must_use]
    pub const fn context(&self) -> &PlanningContext {
        &self.context
    }

    pub fn with_assets(mut self, catalog: &super::assets::AssetCatalog) -> Result<Self, String> {
        if self.context.asset_catalog.is_some() {
            return Err("Rebuild the scope to replace asset permissions".into());
        }
        self.context.asset_catalog = Some(catalog.context().clone());
        if catalog.context().allow_placement {
            self.context.supported_operations.push("place_asset".into());
            // Insertion changes sorted array indices; the compiler preserves
            // all existing entries and the review displays the whole array.
            self.policy.allowed.push("/corrections/placements".into());
        }
        if canonical_json_bytes(&self.context)
            .map_err(|error| error.to_string())?
            .len()
            > MAX_CONTEXT_BYTES
        {
            return Err("Assistant context exceeds 64 KiB; narrow the asset selection".into());
        }
        Ok(self)
    }

    #[must_use]
    pub const fn policy(&self) -> &ScopePolicy {
        &self.policy
    }

    pub fn validate_head(&self, head: &ZoneAuthoringHead) -> Result<(), String> {
        if self.context.base_hash != head.content_hash()?
            || self.context.base_revision != head.revision
        {
            return Err("Assistant selection is stale; inspect the current revision".into());
        }
        Ok(())
    }
}

fn append_creation(
    head: &ZoneAuthoringHead,
    selection: &Selection,
    context: &mut PlanningContext,
    paths: &mut Vec<String>,
) -> Result<(), String> {
    if selection.create_content {
        let document =
            head.corrections.gameplay.as_deref().ok_or(
                "Initialiser le contenu RPG dans le Studio avant d’autoriser sa création.",
            )?;
        context.creation = Some(super::content::CreationContext::new(document));
        context.supported_operations.push("add_content".into());
        if document.surface_visible(forge_gameplay::CreatorSurface::Places)
            && context.creation.as_ref().is_some_and(|creation| {
                creation
                    .capabilities
                    .contains(&forge_gameplay::GameplayCapability::Places)
            })
        {
            context.supported_operations.push("create_places".into());
        }
        if document.creator.is_some() {
            context
                .supported_operations
                .push("create_fetch_quest".into());
        }
        for field in [
            "actors",
            "items",
            "objects",
            "places",
            "dialogues",
            "quests",
            "narrative",
        ] {
            paths.push(format!("/corrections/gameplay/{field}"));
        }
    }
    Ok(())
}

fn append_gameplay(
    head: &ZoneAuthoringHead,
    selection: &Selection,
    context: &mut PlanningContext,
    paths: &mut Vec<String>,
) -> Result<(), String> {
    if selection.objects.is_empty() && selection.dialogue_lines.is_empty() {
        return Ok(());
    }
    let document = head
        .corrections
        .gameplay
        .as_ref()
        .ok_or("Selected zone has no gameplay document")?;
    for id in &selection.objects {
        let index = document
            .objects
            .iter()
            .position(|object| object.id == *id)
            .ok_or("Unknown selected gameplay object")?;
        let object = &document.objects[index];
        if !context.region.contains(object.position) {
            return Err("Selected object is outside the authorized region".into());
        }
        context.objects.push(object.clone());
        paths.push(format!("/corrections/gameplay/objects/{index}/position"));
    }
    for id in &selection.dialogue_lines {
        let dialogue = document
            .dialogues
            .iter()
            .position(|dialogue| dialogue.id == id.dialogue_id)
            .ok_or("Unknown selected dialogue")?;
        let node = document.dialogues[dialogue]
            .nodes
            .iter()
            .position(|node| node.id == id.node_id)
            .ok_or("Unknown selected dialogue line")?;
        context.dialogue_lines.push(SelectedDialogueLine {
            id: *id,
            text: document.dialogues[dialogue].nodes[node].text.clone(),
        });
        paths.push(format!(
            "/corrections/gameplay/dialogues/{dialogue}/nodes/{node}/text"
        ));
    }
    if !context.objects.is_empty() {
        context.supported_operations.push("move_object".into());
    }
    if !context.dialogue_lines.is_empty() {
        context
            .supported_operations
            .push("set_dialogue_text".into());
    }
    Ok(())
}
