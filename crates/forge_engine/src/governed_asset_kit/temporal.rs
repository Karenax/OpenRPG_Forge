//! Reconstructible kit adapter for the shared temporal LOD renderer.
//! Source materials and placement documents remain untouched.

use super::{
    GovernedAssetKitPlacementMarker, GovernedAssetKitRenderLod, MAX_GOVERNED_ASSET_HIERARCHY_DEPTH,
    configure_governed_asset_kit_primitives, governed_asset_lod_range,
};
use crate::{
    TemporalLodPolicy,
    temporal_lod::render::{GroupKey, Groups, Member},
    vegetation::{VisualValleyVegetationExtension, VisualValleyVegetationMaterial},
};
use bevy::{asset::AssetId, mesh::MeshTag, prelude::*};
use std::collections::HashMap;

#[derive(Component)]
struct SourceMaterial(Handle<StandardMaterial>);

#[derive(Component)]
struct RegisteredRoot;

#[derive(Default, Resource)]
struct MaterialCache(HashMap<AssetId<StandardMaterial>, Handle<VisualValleyVegetationMaterial>>);

pub(super) fn install(app: &mut App) {
    crate::temporal_lod::render::install(app);
    app.init_resource::<MaterialCache>()
        .add_systems(
            Update,
            attach.after(configure_governed_asset_kit_primitives),
        )
        .add_systems(
            PostUpdate,
            update_roots
                .after(bevy::transform::TransformSystems::Propagate)
                .before(crate::temporal_lod::render::update),
        );
}

fn policy() -> TemporalLodPolicy {
    // Compile-time adapter constants: three visible LODs and an empty cull representation.
    TemporalLodPolicy::new(&[44.0, 128.0, 1_500.0], 2.0, 0.2)
        .expect("kit temporal LOD constants satisfy the shared policy contract")
}

/// Searches only transient ancestry; persistent placement IDs never become Bevy entities.
fn ancestry(world: &World, entity: Entity) -> Option<(Entity, GovernedAssetKitRenderLod)> {
    let mut ancestor = entity;
    let mut level = None;
    for _ in 0..MAX_GOVERNED_ASSET_HIERARCHY_DEPTH {
        if let Some(lod) = world.get::<GovernedAssetKitRenderLod>(ancestor) {
            level = Some(*lod);
        }
        if world
            .get::<GovernedAssetKitPlacementMarker>(ancestor)
            .is_some()
        {
            return level.map(|lod| (ancestor, lod));
        }
        ancestor = world.get::<ChildOf>(ancestor)?.parent();
    }
    None
}

fn static_extension() -> VisualValleyVegetationExtension {
    VisualValleyVegetationExtension {
        prototype_parameters: Vec4::new(1.0, 0.0, 0.0, 0.0),
        temporal_parameters: Vec4::new(1.0, 1.0, 0.0, 0.0),
        wind_parameters: Vec4::ZERO,
        interaction_direction_strength: Vec4::ZERO,
        ..default()
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "Keep material source retention, fallible registration and publication together"
)]
fn attach(world: &mut World) {
    // A material retouch/reload changes the shared derivative, not its source.
    // This runs only after an asset mutation, never on the temporal frame clock.
    if world.is_resource_changed::<Assets<StandardMaterial>>() {
        let updates = world
            .resource::<MaterialCache>()
            .0
            .iter()
            .filter_map(|(source, derivative)| {
                world
                    .resource::<Assets<StandardMaterial>>()
                    .get(*source)
                    .map(|base| (derivative.clone(), base.clone()))
            })
            .collect::<Vec<_>>();
        for (handle, base) in updates {
            if let Some(mut material) = world
                .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
                .get_mut(&handle)
            {
                material.base = base;
            }
        }
    }
    let pending = world.query_filtered::<(Entity, &MeshMaterial3d<StandardMaterial>),
        (With<Mesh3d>, Without<Member>)>()
        .iter(world).map(|(entity, material)| (entity, material.0.clone()))
        .collect::<Vec<_>>();
    for (entity, source) in pending {
        let Some((root, lod)) = ancestry(world, entity) else {
            continue;
        };
        let Some(base) = world
            .resource::<Assets<StandardMaterial>>()
            .get(&source)
            .cloned()
        else {
            // Asset loading is asynchronous. Keep the existing material/range and retry.
            continue;
        };
        let Some(root_transform) = world.get::<GlobalTransform>(root) else {
            continue;
        };
        let position = root_transform.translation();
        let original_tag = world.get::<MeshTag>(entity).map_or(0, |tag| tag.0);
        let slot = match world.resource_mut::<Groups>().register(
            GroupKey::Kit(root),
            position,
            0,
            policy(),
        ) {
            Ok(slot) => slot,
            Err(error) => {
                tracing::error!(target: "forge::assets", ?entity, error,
                    "kit temporal LOD registration refused; spatial rendering retained");
                continue;
            }
        };
        let cached = world
            .resource::<MaterialCache>()
            .0
            .get(&source.id())
            .cloned();
        // A newly attached primitive changes the group's actual GPU inventory,
        // even when its distance policy and source material were already known.
        world.resource_mut::<Groups>().invalidate(slot);
        let material = cached.unwrap_or_else(|| {
            let material = world
                .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
                .add(VisualValleyVegetationMaterial {
                    base,
                    extension: static_extension(),
                });
            world
                .resource_mut::<MaterialCache>()
                .0
                .insert(source.id(), material.clone());
            material
        });
        let level = match lod {
            GovernedAssetKitRenderLod::Lod0 => 0,
            GovernedAssetKitRenderLod::Lod1 => 1,
            GovernedAssetKitRenderLod::Lod2 => 2,
        };
        world
            .entity_mut(entity)
            .remove::<MeshMaterial3d<StandardMaterial>>()
            .insert((
                MeshMaterial3d(material),
                SourceMaterial(source),
                MeshTag(slot * 4 + level),
                Member {
                    slot,
                    level,
                    original_tag,
                    original_range: governed_asset_lod_range(lod),
                },
            ));
        world.entity_mut(root).insert(RegisteredRoot);
    }
    // Drop strong derived handles once their final placement disappears. Materials are
    // shared by source identity, never cloned once per object or once per frame.
    let used = world
        .query::<&SourceMaterial>()
        .iter(world)
        .map(|source| source.0.id())
        .collect::<std::collections::HashSet<_>>();
    world
        .resource_mut::<MaterialCache>()
        .0
        .retain(|id, _| used.contains(id));
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "Bevy system parameters"
)]
fn update_roots(
    roots: Query<(Entity, &GlobalTransform), (With<RegisteredRoot>, Changed<GlobalTransform>)>,
    mut groups: ResMut<Groups>,
) {
    for (root, transform) in &roots {
        if let Err(error) =
            groups.register(GroupKey::Kit(root), transform.translation(), 0, policy())
        {
            tracing::error!(target: "forge::assets", ?root, error,
                "kit temporal LOD root update refused");
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ancestry_requires_one_governed_root_and_a_render_level() {
        let mut world = World::new();
        let root = world
            .spawn(GovernedAssetKitPlacementMarker {
                placement_id: "test-placement".into(),
                asset_id: "test-asset".into(),
            })
            .id();
        let lod = world
            .spawn((GovernedAssetKitRenderLod::Lod1, ChildOf(root)))
            .id();
        let primitive = world.spawn(ChildOf(lod)).id();
        assert_eq!(
            ancestry(&world, primitive),
            Some((root, GovernedAssetKitRenderLod::Lod1))
        );
        let collision = world.spawn(ChildOf(root)).id();
        assert!(ancestry(&world, collision).is_none());
    }

    #[test]
    fn kit_extension_is_static_and_preserves_standard_pbr() {
        let extension = static_extension();
        assert_eq!(
            extension.prototype_parameters,
            Vec4::new(1.0, 0.0, 0.0, 0.0)
        );
        assert_eq!(extension.temporal_parameters, Vec4::new(1.0, 1.0, 0.0, 0.0));
        assert_eq!(extension.wind_parameters, Vec4::ZERO);
        let mut state = crate::TemporalLod::new(policy(), 0).expect("valid kit policy");
        assert!(
            state
                .advance(46.0, 0.1, [true; 4])
                .expect("ready representations")
                .transition_active
        );
    }

    #[test]
    fn primitives_share_one_derivative_and_keep_source_material() {
        let mut world = World::new();
        world.init_resource::<Groups>();
        world.init_resource::<MaterialCache>();
        world.init_resource::<Assets<StandardMaterial>>();
        world.init_resource::<Assets<VisualValleyVegetationMaterial>>();
        let source = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial {
                base_color: Color::srgb(0.2, 0.4, 0.6),
                perceptual_roughness: 0.75,
                ..default()
            });
        let root = world
            .spawn((
                GovernedAssetKitPlacementMarker {
                    placement_id: "placement".into(),
                    asset_id: "asset".into(),
                },
                GlobalTransform::from_translation(Vec3::new(12.0, 3.0, 4.0)),
            ))
            .id();
        let mut primitives = Vec::new();
        for lod in [
            GovernedAssetKitRenderLod::Lod0,
            GovernedAssetKitRenderLod::Lod1,
        ] {
            let node = world.spawn((lod, ChildOf(root))).id();
            primitives.push(
                world
                    .spawn((
                        Mesh3d(Handle::default()),
                        MeshMaterial3d(source.clone()),
                        ChildOf(node),
                    ))
                    .id(),
            );
        }
        attach(&mut world);
        let first = world
            .get::<MeshMaterial3d<VisualValleyVegetationMaterial>>(primitives[0])
            .expect("converted material")
            .0
            .clone();
        for primitive in primitives {
            assert_eq!(
                world
                    .get::<MeshMaterial3d<VisualValleyVegetationMaterial>>(primitive)
                    .expect("shared derivative")
                    .0,
                first
            );
            assert_eq!(
                world
                    .get::<SourceMaterial>(primitive)
                    .expect("source retained")
                    .0,
                source
            );
            assert!(
                world
                    .get::<MeshMaterial3d<StandardMaterial>>(primitive)
                    .is_none()
            );
            assert!(world.get::<Member>(primitive).is_some());
        }
        assert_eq!(
            world
                .resource::<Assets<VisualValleyVegetationMaterial>>()
                .len(),
            1
        );
        assert_eq!(
            world
                .resource::<Assets<StandardMaterial>>()
                .get(&source)
                .expect("source exists")
                .base_color,
            Color::srgb(0.2, 0.4, 0.6)
        );
        world
            .resource_mut::<Assets<StandardMaterial>>()
            .get_mut(&source)
            .expect("source retouch")
            .base_color = Color::WHITE;
        attach(&mut world);
        assert_eq!(
            world
                .resource::<Assets<VisualValleyVegetationMaterial>>()
                .get(&first)
                .expect("same derivative after retouch")
                .base
                .base_color,
            Color::WHITE
        );
        assert_eq!(
            world
                .resource::<Assets<VisualValleyVegetationMaterial>>()
                .len(),
            1
        );
    }
}
