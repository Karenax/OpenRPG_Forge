//! Acquisition-specific material roles, revision checks and collision frames.
//! Disposable instance ownership is implemented once by the shared engine.
use super::{DebrisSurface, PreparedDebris};
use bevy::prelude::*;
#[cfg(test)]
use forge_engine::ManualPlacementCollisionNotRequired;
use forge_engine::source_surface::SourceSurfaceMaterial;
use forge_engine::source_surface::nature::{self, NatureMaterial};
use forge_engine::source_surface::objects::props::{self, MaterialHandle, PreparedPart};
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};
pub use props::{Instance, ResidentScene};
use std::collections::BTreeMap;
#[cfg(test)]
mod tests;

pub struct ResidentMaterials {
    pub wood: Handle<SourceSurfaceMaterial>,
    pub hard: Handle<SourceSurfaceMaterial>,
    pub fabric: Handle<SourceSurfaceMaterial>,
    pub nature: Handle<NatureMaterial>,
}

pub struct PreparedScene(props::PreparedScene);

impl PreparedScene {
    /// Resolve every accepted reference/material before allocating meshes or entities.
    ///
    /// # Errors
    /// Rejects invalid common placements, wrong revisions, absent assets and excess geometry.
    pub fn new(
        geometry: &PreparedDebris,
        placements: &[ManualPlacement],
        materials: &ResidentMaterials,
        world: &World,
    ) -> Result<Self, String> {
        PlacementChanges::between(&[], placements)?;
        let village = world
            .get_resource::<Assets<SourceSurfaceMaterial>>()
            .ok_or("Debris Village materials unavailable")?;
        let nature = world
            .get_resource::<Assets<NatureMaterial>>()
            .ok_or("Debris Nature materials unavailable")?;
        for (handle, kind) in [
            (&materials.wood, 0),
            (&materials.hard, 1),
            (&materials.fabric, 5),
        ] {
            if village
                .get(handle)
                .is_none_or(|material| material.kind != kind)
            {
                return Err("Debris shared material role unresolved".into());
            }
        }
        if nature.get(&materials.nature).is_none() {
            return Err("Debris Nature material unresolved".into());
        }
        let shaders = world
            .get_resource::<Assets<Shader>>()
            .ok_or("Debris shaders unavailable")?;
        if shaders
            .get(&forge_engine::source_surface::SURFACE_SHADER)
            .is_none()
            || shaders.get(&nature::SHADER).is_none()
            || world.get_resource::<Assets<Mesh>>().is_none()
        {
            return Err("Debris renderer resources unresolved".into());
        }
        let mut templates = BTreeMap::new();
        for placement in placements {
            if placement.asset_hash != forge_tidewater_assets::tidewater_debris::package_hash() {
                return Err("Debris placement revision differs".into());
            }
            let object = geometry
                .objects
                .iter()
                .find(|object| object.asset_id == placement.asset_id)
                .ok_or("Debris placement prototype absent")?;
            templates.entry(object.asset_id).or_insert_with(|| {
                object
                    .parts
                    .iter()
                    .map(|part| PreparedPart {
                        mesh: part.mesh.clone(),
                        shadow: part.shadow_mesh.clone(),
                        material: match part.surface {
                            DebrisSurface::Wood => MaterialHandle::Surface(materials.wood.clone()),
                            DebrisSurface::Hard => MaterialHandle::Surface(materials.hard.clone()),
                            DebrisSurface::Fabric => {
                                MaterialHandle::Surface(materials.fabric.clone())
                            }
                            DebrisSurface::Nature => {
                                MaterialHandle::Nature(materials.nature.clone())
                            }
                        },
                    })
                    .collect()
            });
        }
        Ok(Self(props::PreparedScene::new(
            templates, placements, world,
        )?))
    }

    /// Attach hash-verified source primitives before adoption. Empty source owners are explicit.
    ///
    /// # Errors
    /// Rejects a missing owner or a collision frame that differs from the mesh prototype.
    #[allow(
        clippy::float_cmp,
        reason = "Captured collision and mesh frames must match exactly; tolerance would admit different source geometry."
    )]
    pub fn with_collision_frames(
        mut self,
        geometry: &PreparedDebris,
        frames: &forge_tidewater_assets::tidewater_debris::collisions::CollisionFrames,
    ) -> Result<Self, String> {
        let mut collisions = BTreeMap::new();
        for asset in self.0.asset_ids() {
            let object = geometry
                .objects
                .iter()
                .find(|object| object.asset_id == asset)
                .ok_or("Debris collision prototype absent")?;
            let owner = frames
                .owner(&object.source_id)
                .ok_or("Debris collision owner absent")?;
            if owner.category
                != forge_tidewater_assets::tidewater_debris::collisions::Category::Procedural
                || owner.source_frame.translation_m != object.source_translation_m
                || owner.source_frame.rotation_xyzw != [0.0, 0.0, 0.0, 1.0]
                || owner.source_frame.scale != [1.0; 3]
            {
                return Err("Debris collision and mesh frames differ".into());
            }
            collisions.insert(asset, super::collisions::prepare(owner)?);
        }
        self.0 = self.0.with_collisions(collisions)?;
        Ok(self)
    }

    pub fn stage(self, world: &mut World) {
        self.0.stage(world);
    }
}
