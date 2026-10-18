//! Derived entities for an already accepted canonical placement.
use super::ResidentMeshes;
use bevy::{light::NotShadowCaster, prelude::*};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::collections::BTreeMap;
use {
    crate::tidewater_material::VillageMaterial,
    forge_engine::{ManualPlacementPart, ManualPlacementProjection},
};

pub struct PlacementEntities {
    pub root: Entity,
    pub parts: Vec<Entity>,
}

/// Resolve all dependencies before queuing entities. The caller owns visibility,
/// reconciliation and the editor gizmo; no source document is changed here.
/// Tidewater shadows require the dedicated shadow renderer, not Bevy's caster.
pub fn spawn(
    commands: &mut Commands,
    placement: &ManualPlacement,
    geometry: &ResidentMeshes,
    materials: &BTreeMap<String, Handle<VillageMaterial>>,
) -> Result<PlacementEntities, String> {
    placement.transform.validate().map_err(|e| e.to_string())?;
    let geometry = geometry.resolve(placement.asset_id, placement.asset_hash)?;
    let prepared = geometry
        .iter()
        .map(|part| {
            materials
                .get(&part.material)
                .map(|material| (part.mesh.clone(), material.clone()))
                .ok_or_else(|| format!("Building material is not resident: {}", part.material))
        })
        .collect::<Result<Vec<_>, _>>()?;
    let root = commands
        .spawn((
            Name::new(format!("Placement {} · {}", placement.id, placement.name)),
            forge_engine::domain_transform_to_bevy(placement.transform),
            Visibility::Hidden,
            ManualPlacementProjection(placement.clone()),
        ))
        .id();
    let parts = prepared
        .into_iter()
        .map(|(mesh, material)| {
            commands
                .spawn((
                    Mesh3d(mesh),
                    MeshMaterial3d(material),
                    Transform::IDENTITY,
                    Visibility::Inherited,
                    NotShadowCaster,
                    ManualPlacementPart(placement.id),
                    ChildOf(root),
                ))
                .id()
        })
        .collect();
    Ok(PlacementEntities { root, parts })
}
