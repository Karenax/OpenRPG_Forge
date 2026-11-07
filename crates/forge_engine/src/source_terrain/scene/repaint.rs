//! Paint-only updates of the same admitted terrain; geometry and normals stay owned.
use super::{LodMaterial, PreparedPaint, ResidentTerrain};
use bevy::prelude::*;
use forge_assets::source_terrain_package::TerrainPackage;
use forge_core::{ContentHash, Revision};
use forge_project::TerrainDocument;
use forge_world::sampled_terrain::SampledTerrainPlan;

pub(super) fn key(document: &TerrainDocument, package: ContentHash) -> Result<ContentHash, String> {
    // Compare every non-paint field, including future document fields by default.
    // Journal revision changes alone do not alter the derived geometry.
    let mut geometry = document.clone();
    geometry.material_overrides.clear();
    geometry.revision = Revision::INITIAL;
    forge_core::canonical_json_sha256(&(package, geometry)).map_err(|e| e.to_string())
}

pub struct PreparedRepaint {
    expected: ResidentTerrain,
    next: ContentHash,
    paint: PreparedPaint,
}

impl PreparedRepaint {
    /// Return None for any changed source, relief, water level, palette or other non-paint input.
    /// The existing full preparation remains responsible for those transitions.
    pub fn new(
        plan: &SampledTerrainPlan,
        package: &TerrainPackage,
        current: &ResidentTerrain,
        water_level_m: Option<f64>,
    ) -> Result<Option<Self>, String> {
        if water_level_m.is_some_and(|level| !level.is_finite()) {
            return Err("Nonfinite terrain AO water level".into());
        }
        if current.entity.is_none()
            || current.material.is_none()
            || current.water_level_m.map(f64::to_bits) != water_level_m.map(f64::to_bits)
            || current.reuse_key != key(plan.document(), package.identity())?
        {
            return Ok(None);
        }
        Ok(Some(Self {
            expected: current.clone(),
            next: plan.document_hash(),
            paint: PreparedPaint::new(plan)?,
        }))
    }

    /// Retain mesh, material, textures, programs and normal-generation ownership.
    /// Consumers must still acknowledge the new material dependencies and GPU fence.
    pub fn stage(self, world: &mut World) -> Result<ResidentTerrain, String> {
        let current = world
            .get_resource::<ResidentTerrain>()
            .filter(|r| {
                r.document_hash == self.expected.document_hash
                    && r.relief == self.expected.relief
                    && r.water_level_m.map(f64::to_bits)
                        == self.expected.water_level_m.map(f64::to_bits)
                    && r.reuse_key == self.expected.reuse_key
                    && r.entity == self.expected.entity
                    && r.material == self.expected.material
                    && r.normals.image == self.expected.normals.image
                    && std::sync::Arc::ptr_eq(&r.normals.ready, &self.expected.normals.ready)
                    && r.source_linear_detail == self.expected.source_linear_detail
            })
            .ok_or("Terrain paint replacement is stale")?;
        let root = current.entity.ok_or("Terrain paint root absent")?;
        let handle = current
            .material
            .clone()
            .ok_or("Terrain paint material absent")?;
        if world.get::<Mesh3d>(root).is_none()
            || world
                .get::<MeshMaterial3d<LodMaterial>>(root)
                .is_none_or(|m| m.0 != handle)
        {
            return Err("Terrain paint projection was replaced".into());
        }
        let mut changed = world
            .get_resource::<Assets<LodMaterial>>()
            .and_then(|assets| assets.get(&handle))
            .ok_or("Terrain paint material retired")?
            .clone();
        if changed.base.normal != self.expected.normals.image {
            return Err("Terrain paint normal binding was replaced".into());
        }
        if world.get_resource::<Assets<Image>>().is_none_or(|images| {
            images.get(&self.expected.source_linear_detail).is_none()
                || images.get(&self.expected.normals.image).is_none()
        }) {
            return Err("Terrain paint source texture retired".into());
        }
        let inputs = self.paint.stage(world)?;
        changed.base.paint_weights = inputs.weights;
        changed.base.paint_parameters = inputs.parameters;
        changed.base.linear_detail = inputs
            .detail
            .unwrap_or(self.expected.source_linear_detail.clone());
        *world
            .resource_mut::<Assets<LodMaterial>>()
            .get_mut(&handle)
            .ok_or("Terrain paint material retired")? = changed;
        let resident = ResidentTerrain {
            document_hash: self.next,
            ..self.expected
        };
        world.insert_resource(resident.clone());
        Ok(resident)
    }
}

#[cfg(test)]
mod tests;
