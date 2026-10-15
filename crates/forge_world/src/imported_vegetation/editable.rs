//! Common placement identity and transform facade for batched imported instances.
use super::{ImportedEntity, ImportedPopulation};
use forge_core::{AssetId, Transform, UnitQuaternion, Vec3};
use forge_project::zone_authoring::{
    ZoneAuthoringHead, placements::ManualPlacement, source_objects,
};

#[must_use]
pub fn plant_asset_id(head: &ZoneAuthoringHead, layer: &str) -> AssetId {
    AssetId::deterministic(format!(
        "imported-instance:{}:{}:{layer}",
        head.base.terrain.project_id, head.base.terrain.area_id
    ))
}

#[must_use]
pub fn is_plant_asset(head: &ZoneAuthoringHead, id: AssetId) -> bool {
    head.base.vegetation.as_ref().is_some_and(|v| {
        v.layers
            .iter()
            .any(|l| plant_asset_id(head, l.id.as_str()) == id)
    })
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Validated imported coordinates are bounded by the Forge transform range"
)]
pub fn placement(
    head: &ZoneAuthoringHead,
    layer: &str,
    entity: &ImportedEntity,
) -> Result<ManualPlacement, String> {
    let vegetation = head.base.vegetation.as_ref().ok_or("Vegetation absent")?;
    let block = vegetation
        .imported_source
        .as_ref()
        .and_then(|s| s.blocks.iter().find(|b| b.layer_id == layer))
        .ok_or("Imported layer absent")?;
    let [x, y, z, w] = entity.source.rotation_xyzw.map(|v| v as f32);
    let [px, py, pz] = entity.source.position_m.map(|v| v as f32);
    let [sx, sy, sz] = entity.source.scale.map(|v| v as f32);
    let transform = Transform {
        translation_m: Vec3::new(px, py, pz),
        rotation: UnitQuaternion::new(x, y, z, w),
        scale: Vec3::new(sx, sy, sz),
    };
    transform.validate().map_err(|e| e.to_string())?;
    Ok(ManualPlacement {
        particles: None,
        physics: None,
        id: entity.id,
        name: vegetation
            .layers
            .iter()
            .find(|l| l.id.as_str() == layer)
            .map_or_else(|| layer.to_owned(), |l| l.display_name.clone()),
        asset_id: plant_asset_id(head, layer),
        asset_hash: block.instances_sha256,
        transform,
        locked: false,
    })
}

pub(super) fn apply(
    head: &ZoneAuthoringHead,
    layer: &str,
    entities: Vec<ImportedEntity>,
    hidden: Vec<ImportedEntity>,
) -> Result<Vec<ImportedEntity>, String> {
    let mut result = Vec::with_capacity(entities.len());
    let extent = head
        .base
        .vegetation
        .as_ref()
        .ok_or("Vegetation absent")?
        .extent_m;
    for (entity, visible) in entities
        .into_iter()
        .map(|entity| (entity, true))
        .chain(hidden.into_iter().map(|entity| (entity, false)))
    {
        let edits = head
            .corrections
            .source_object_edits
            .as_deref()
            .unwrap_or_default();
        if edits
            .binary_search_by_key(&entity.id, |edit| edit.source.id)
            .is_err()
        {
            if visible {
                result.push(entity);
            }
            continue;
        }
        let source = placement(head, layer, &entity)?;
        for object in source_objects::resolve(head, &source)? {
            if !visible && object.id == source.id {
                continue;
            }
            if object == source {
                result.push(entity.clone());
                continue;
            }
            let t = object.transform;
            if t.translation_m.x.abs() > extent.size_x_m * 0.5
                || t.translation_m.z.abs() > extent.size_z_m * 0.5
            {
                return Err("Edited source object is outside the vegetation extent".into());
            }
            let mut copy = entity.clone();
            copy.id = object.id;
            copy.source.position_m =
                [t.translation_m.x, t.translation_m.y, t.translation_m.z].map(f64::from);
            copy.source.rotation_xyzw =
                [t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w].map(f64::from);
            copy.source.scale = [t.scale.x, t.scale.y, t.scale.z].map(f64::from);
            result.push(copy);
        }
    }
    Ok(result)
}

impl ImportedPopulation {
    /// The GUI can resolve one clicked instance without creating per-instance ECS roots.
    pub fn editable_placement(
        &self,
        head: &ZoneAuthoringHead,
        terrain_bytes: &[u8],
        id: forge_core::EntityId,
    ) -> Result<Option<ManualPlacement>, String> {
        let view = self.terrain_view(head, terrain_bytes)?;
        for layer in &view.layers {
            if let Some(entity) = layer.instances.iter().find(|e| e.id == id) {
                return placement(head, layer.layer_id, entity).map(Some);
            }
        }
        Ok(None)
    }
}
