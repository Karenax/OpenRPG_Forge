//! Shared placement of an admitted stall and its attached fish.
use super::super::stall_material::FullMaterial;
use crate::{
    ManualPlacementCollision, ManualPlacementCollisionExpected, ManualPlacementPart,
    ManualPlacementProjection,
    distance_lod::Policy,
    domain_transform_to_bevy, fish_stand_colliders,
    source_fish::resident::{self, Attachment, ResidentFishProps},
    source_shadows::casters::scene::OpaqueCaster,
};
use bevy::{prelude::*, render::storage::ShaderBuffer};
use forge_project::zone_authoring::placements::ManualPlacement;

#[derive(Clone, Copy)]
pub struct PlacementAssets<'a> {
    pub mesh: &'a Handle<Mesh>,
    pub material: &'a Handle<FullMaterial>,
    pub fish: &'a [Attachment],
    pub clock: &'a Handle<ShaderBuffer>,
    pub fish_lod: Policy,
}

/// Spawn the stall and its colliders beneath the authored placement root.
/// Attached fish inherit edits to the root transform and visibility.
///
/// # Errors
/// On a failed fish admission, removes the root and its staged descendants.
pub fn spawn(
    world: &mut World,
    source: &ManualPlacement,
    assets: PlacementAssets<'_>,
) -> Result<(Entity, ResidentFishProps), String> {
    let root = spawn_root(world, source, assets.mesh, assets.material);
    match resident::stage_attachments(
        world,
        root,
        source.id,
        assets.fish,
        assets.clock,
        assets.fish_lod,
    ) {
        Ok(fish) => Ok((root, fish)),
        Err(error) => {
            world.despawn(root);
            Err(error)
        }
    }
}

/// Project the authored base, shared by standalone and edited stalls.
pub fn spawn_root(
    world: &mut World,
    source: &ManualPlacement,
    mesh: &Handle<Mesh>,
    material: &Handle<FullMaterial>,
) -> Entity {
    let root = world
        .spawn((
            Name::new(format!("Placement {} · {}", source.id, source.name)),
            domain_transform_to_bevy(source.transform),
            Visibility::Inherited,
            ManualPlacementProjection(source.clone()),
        ))
        .id();
    world.spawn((
        Mesh3d(mesh.clone()),
        MeshMaterial3d(material.clone()),
        OpaqueCaster,
        Transform::IDENTITY,
        Visibility::Inherited,
        ChildOf(root),
        ManualPlacementPart(source.id),
    ));
    let primitives = fish_stand_colliders()
        .into_iter()
        .map(|(transform, collider)| {
            world
                .spawn((
                    transform,
                    collider,
                    ManualPlacementCollision(source.id),
                    ChildOf(root),
                ))
                .id()
        })
        .collect();
    world
        .entity_mut(root)
        .insert(ManualPlacementCollisionExpected {
            placement: source.id,
            primitives,
        });
    root
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{AssetId, ContentHash, EntityId, Transform as ForgeTransform};

    #[test]
    fn failed_fish_admission_removes_the_staged_stall_and_colliders() {
        let mut world = World::new();
        let unrelated = world.spawn_empty().id();
        let source = ManualPlacement {
            id: EntityId::deterministic("test stall"),
            name: "Test stall".into(),
            asset_id: AssetId::deterministic("test stall mesh"),
            asset_hash: ContentHash::from_bytes([1; 32]),
            physics: None,
            particles: None,
            local_lights: None,
            transform: ForgeTransform::IDENTITY,
            locked: false,
        };
        let error = spawn(
            &mut world,
            &source,
            PlacementAssets {
                mesh: &Handle::default(),
                material: &Handle::default(),
                fish: &[],
                clock: &Handle::default(),
                fish_lod: Policy::new(90.0, 80.0, 22.0, 1.0, 0.9).unwrap(),
            },
        )
        .err();
        assert_eq!(error.as_deref(), Some("Fish meshes absent"));
        assert!(
            world
                .query_filtered::<Entity, Or<(
                    With<ManualPlacementProjection>,
                    With<ManualPlacementPart>,
                    With<ManualPlacementCollision>,
                )>>()
                .iter(&world)
                .next()
                .is_none()
        );
        assert!(world.get_entity(unrelated).is_ok());
    }
}
