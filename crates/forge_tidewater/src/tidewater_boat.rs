//! Tidewater's verified boat data, adapted to the common engine projection.
use bevy::prelude::*;
use forge_core::{AssetId, Transform as ForgeTransform};
use forge_engine::source_boat::{self, profile};
use forge_project::zone_authoring::placements::ManualPlacement;
use forge_tidewater_assets::tidewater_boat::BoatPackage;
use std::collections::BTreeMap;

pub use forge_engine::source_boat::{
    BoatCensus, BoatCollisionVolume, BoatInstanceIdentity, BoatPart, BoatProjectionOwner,
    BoatSourceNode, BoatWaterExclusion, ResidentBoat,
};

/// Compatibility adapter for Tidewater's verified package.
pub struct PreparedBoat(source_boat::PreparedBoat);

impl PreparedBoat {
    /// Supply the package's verified data and the caller's catalog identity.
    pub fn from_package(package: &BoatPackage, asset_id: AssetId) -> Result<Self, String> {
        let description = package.description();
        let nodes = description
            .nodes
            .iter()
            .map(|node| profile::Node {
                id: node.id.clone(),
                parent: node.parent.clone(),
                transform: node.transform.clone(),
                visible: node.visible,
            })
            .collect();
        let parts = description
            .parts
            .iter()
            .map(|part| {
                let resolved = package.resolve_part(&part.id)?;
                Ok(profile::Part {
                    id: part.id.clone(),
                    node: part.node.clone(),
                    material: part.material.clone(),
                    attributes: resolved.attributes,
                    indices: resolved.indices,
                    cast_shadow: part.cast_shadow,
                    receive_shadow: part.receive_shadow,
                    render_order: part.render_order,
                })
            })
            .collect::<Result<Vec<_>, String>>()?;
        let colliders = description
            .colliders
            .iter()
            .map(|collider| profile::CollisionVolume {
                id: collider.id.clone(),
                tag: collider.tag.clone(),
                center_m: collider.center_m,
                half_extents_m: collider.half_extents_m,
                walkable: collider.walkable,
                solid: collider.solid,
            })
            .collect();
        let profile = profile::Profile {
            source_id: description.source_id.clone(),
            canonical_translation_m: description.canonical_placement.translation_m,
            canonical_heading_radians: description.canonical_placement.heading_radians,
            nodes,
            parts,
            colliders,
            water_exclusion: package.water_exclusion()?,
            triangles: description.triangles,
        };
        source_boat::PreparedBoat::from_profile(profile, asset_id, package.receipt_hash()).map(Self)
    }

    #[must_use]
    pub fn identity(&self) -> BoatInstanceIdentity {
        self.0.identity()
    }

    #[must_use]
    pub fn canonical_transform(&self) -> ForgeTransform {
        self.0.canonical_transform()
    }

    pub fn stage<M: Material>(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<M>>,
    ) -> Result<ResidentBoat<M>, String> {
        self.0.stage(world, materials)
    }

    pub fn stage_authored<M: Material>(
        self,
        world: &mut World,
        materials: &BTreeMap<String, Handle<M>>,
        placements: &[ManualPlacement],
    ) -> Result<ResidentBoat<M>, String> {
        self.0.stage_authored(world, materials, placements)
    }
}
