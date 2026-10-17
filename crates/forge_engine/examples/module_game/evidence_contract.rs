//! RPG projection evidence. Process exit success alone is never a ready verdict.
use forge_core::{AreaId, ContentHash, EntityId, ModuleId, Transform};
use forge_project::module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ExpectedManual {
    pub entity_id: EntityId,
    pub asset: VisualAssetPin,
    pub transform: Transform,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ExpectedProjection {
    pub module_id: ModuleId,
    pub area_id: AreaId,
    pub source_hash: ContentHash,
    pub functional_hash: ContentHash,
    pub visuals: Vec<GameplayVisualBinding>,
    pub manual: Vec<ExpectedManual>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedVisual {
    pub entity_id: EntityId,
    pub asset: VisualAssetPin,
    pub transform: Transform,
    pub parent_identity: Option<EntityId>,
    pub parent_is_gameplay_body: bool,
    pub parent_has_collider: bool,
    pub scene_ready: bool,
    pub mesh_primitives: usize,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObservedManual {
    pub identity: ExpectedManual,
    pub mesh_primitives: usize,
    pub registered_primitives: usize,
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RendererObservation {
    pub observed: bool,
    pub frame: u32,
    pub pipelines: usize,
    pub pending: usize,
    pub failed: usize,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ProjectionObservation {
    pub pid: u32,
    pub frame: u32,
    pub module_id: Option<ModuleId>,
    pub area_id: Option<AreaId>,
    pub source_hash: Option<ContentHash>,
    pub functional_hash: Option<ContentHash>,
    pub gameplay_ready: bool,
    pub tick: u64,
    pub simulation_error: Option<String>,
    pub visual_error: Option<String>,
    pub visual_expected: usize,
    pub visual_spawned: usize,
    pub visual_ready: usize,
    pub manual_projection_ready: bool,
    pub visuals: Vec<ObservedVisual>,
    pub manual: Vec<ObservedManual>,
    pub renderer: RendererObservation,
}

pub fn verify(
    expected: &ExpectedProjection,
    observed: &ProjectionObservation,
) -> Result<(), String> {
    if observed.pid == 0
        || observed.frame == 0
        || observed.module_id != Some(expected.module_id)
        || observed.area_id != Some(expected.area_id)
        || observed.source_hash != Some(expected.source_hash)
        || observed.functional_hash != Some(expected.functional_hash)
    {
        return Err(
            "The observed native process/document identity differs from the admitted module".into(),
        );
    }
    if !observed.gameplay_ready
        || observed.tick == 0
        || observed.simulation_error.is_some()
        || observed.visual_error.is_some()
        || !observed.manual_projection_ready
    {
        return Err("The native simulation or physical/visual projections are not ready".into());
    }
    let renderer = &observed.renderer;
    if !renderer.observed
        || renderer.pipelines == 0
        || renderer.pending != 0
        || renderer.failed != 0
        || renderer.frame > observed.frame
        || observed.frame - renderer.frame > 8
    {
        return Err("A fresh, complete renderer pipeline observation is required".into());
    }
    let count = expected.visuals.len();
    if observed.visual_expected != count
        || observed.visual_spawned != count
        || observed.visual_ready != count
        || observed.visuals.len() != count
        || observed.manual.len() != expected.manual.len()
    {
        return Err("Native model/placement inventories differ from the canonical module".into());
    }
    let mut seen = std::collections::BTreeSet::new();
    let visual_rows = observed
        .visuals
        .iter()
        .map(|row| (row.entity_id, row))
        .collect::<std::collections::BTreeMap<_, _>>();
    let manual_rows = observed
        .manual
        .iter()
        .map(|row| (row.identity.entity_id, row))
        .collect::<std::collections::BTreeMap<_, _>>();
    if visual_rows.len() != observed.visuals.len() || manual_rows.len() != observed.manual.len() {
        return Err("Duplicate observed model or manual-placement identity".into());
    }
    for expected in &expected.visuals {
        if expected.area_id != observed.area_id.ok_or("Observed area is absent")?
            || !seen.insert(expected.entity_id)
        {
            return Err("Invalid expected visual inventory".into());
        }
        let row = visual_rows
            .get(&expected.entity_id)
            .ok_or("A canonical visual has no observed scene")?;
        if row.asset != expected.asset
            || row.transform != expected.local_transform
            || row.parent_identity != Some(expected.entity_id)
            || !row.parent_is_gameplay_body
            || !row.parent_has_collider
            || !row.scene_ready
            || row.mesh_primitives == 0
        {
            return Err(
                "A governed model's pin, transform, parent, collider or loaded mesh differs".into(),
            );
        }
        row.transform.validate().map_err(|e| e.to_string())?;
    }
    seen.clear();
    for expected in &expected.manual {
        if !seen.insert(expected.entity_id) {
            return Err("Duplicate expected manual placement".into());
        }
        let row = manual_rows
            .get(&expected.entity_id)
            .ok_or("A canonical manual placement has no observed scene")?;
        if row.identity != *expected
            || row.mesh_primitives == 0
            || row.registered_primitives != row.mesh_primitives
        {
            return Err(
                "A manual placement's identity, transform or static colliders differ".into(),
            );
        }
        row.identity
            .transform
            .validate()
            .map_err(|e| e.to_string())?;
    }
    Ok(())
}
