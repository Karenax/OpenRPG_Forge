//! Opt-in forest preview. Far prototypes use shared LOD2 instances, not final chunk HLOD batching.
use super::{
    DerivedVegetationMesh, GovernedTreePrimitive, VegetationChunkBatchMarker, VegetationLodRanges,
    VegetationPlacementMarker, VegetationPresentation, VegetationProjectionLod,
    VegetationScatterProjection, VisualValleyVegetationMaterial, build_hlod_chunk_mesh,
    maximum_deformation_m, mesh_tag_for, procedural_placement,
};
use crate::VegetationPrototypeWindCache;
use bevy::{
    camera::{
        primitives::Aabb,
        visibility::{NoAutoAabb, VisibilityRange},
    },
    light::NotShadowCaster,
    mesh::MeshTag,
    prelude::*,
};
use forge_project::{VEGETATION_CHUNK_SIZE_M, VegetationKind, VegetationPresentationProfile};
use forge_world::VegetationInstanceId;
use std::collections::HashMap;

mod bounds;

#[derive(Resource)]
pub struct VegetationPrototypeForestPreview;

pub fn governed_forest_ready(world: &World) -> bool {
    !world.contains_resource::<VegetationPrototypeForestPreview>() || quality_ready(world)
}

/// Explicitly return to the procedural presentation before releasing prototype caches.
pub fn stop_governed_forest(world: &mut World) -> Result<(), String> {
    if world.contains_resource::<PreviewState>() {
        super::quality::restore_procedural(world)?;
        super::far_batches::clear(world);
        if let Some(state) = world.remove_resource::<PreviewState>() {
            for entity in state.entities.into_values() {
                world.despawn(entity);
            }
        }
        world
            .resource_mut::<super::VegetationProjectionStats>()
            .prototype_preview_error = None;
    }
    world.remove_resource::<VegetationPrototypeForestPreview>();
    Ok(())
}

/// The governed path follows the current scatter, profile and resolved catalog.
/// Never reuse a receipt while its asynchronous quality projection is pending.
pub(crate) fn quality_ready(world: &World) -> bool {
    let Some(state) = world.get_resource::<PreviewState>() else {
        return false;
    };
    let Some(source) = world.get_resource::<VegetationScatterProjection>() else {
        return false;
    };
    let Some(profile) = world.get_resource::<VegetationPresentation>() else {
        return false;
    };
    !state.failed
        && state
            .source
            .as_ref()
            .is_some_and(|s| std::sync::Arc::ptr_eq(s, &source.generated))
        && state.profile == Some(profile.0)
        && state.prototypes.as_ref() == world.get_resource::<VegetationPrototypeWindCache>()
        && super::far_batches::quality_ready(world)
}

#[derive(Resource, Default)]
struct PreviewState {
    source: Option<std::sync::Arc<forge_world::GeneratedVegetation>>,
    profile: Option<VegetationPresentationProfile>,
    prototypes: Option<VegetationPrototypeWindCache>,
    entities: HashMap<TreeDrawKey, Entity>,
    failed: bool,
}

#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq)]
struct TreeDrawKey {
    placement: VegetationInstanceId,
    lod: u8,
    primitive: usize,
}

struct TreeDraw {
    placement: VegetationInstanceId,
    lod: u8,
    primitive: usize,
    mesh: Handle<Mesh>,
    material: Handle<VisualValleyVegetationMaterial>,
    transform: Transform,
    tag: MeshTag,
    range: VisibilityRange,
    bounds: Aabb,
}

pub(super) fn project(world: &mut World) {
    crate::quality_cpu_trace::measure(
        world,
        crate::QualityCpuPhase::GovernedForestInclusive,
        project_inner,
    );
}

#[allow(
    clippy::too_many_lines,
    reason = "Keep fallible preparation, atomic projection and explicit recovery together"
)]
fn project_inner(world: &mut World) {
    if !world.contains_resource::<VegetationPrototypeForestPreview>() {
        return;
    }
    let Some(cache) = world.get_resource::<VegetationPrototypeWindCache>() else {
        return;
    };
    let cache_failure = cache.failure.clone();
    if cache.layers.is_empty()
        && cache_failure.is_none()
        && !world.contains_resource::<PreviewState>()
    {
        return;
    }
    let source = world
        .resource::<VegetationScatterProjection>()
        .generated
        .clone();
    let profile = world.resource::<VegetationPresentation>().0;
    let mut state = world.remove_resource::<PreviewState>().unwrap_or_default();
    let same_prototypes =
        state.prototypes.as_ref() == world.get_resource::<VegetationPrototypeWindCache>();
    if same_prototypes
        && (state.failed
            || (state
                .source
                .as_ref()
                .is_some_and(|old| std::sync::Arc::ptr_eq(old, &source))
                && state.profile == Some(profile)
                && cache_failure.is_none()))
    {
        world.insert_resource(state);
        return;
    }
    let prepared = cache_failure
        .map_or_else(|| prepare(world, &source, profile), Err)
        .and_then(|(draws, batches)| {
            let temporal = prepare_temporal(world, &draws, profile)?;
            Ok((draws, batches, temporal))
        });
    match prepared {
        Ok((draws, batches, temporal)) => {
            let mut previous = std::mem::take(&mut state.entities);
            let old_trees: Vec<_> = world
                .query::<(Entity, &VegetationPlacementMarker)>()
                .iter(world)
                .filter(|(_, marker)| marker.kind == VegetationKind::Tree)
                .map(|(entity, _)| entity)
                .collect();
            for entity in &old_trees {
                world.entity_mut(*entity).remove::<Mesh3d>();
            }
            replace_batches(world, batches);
            let count = draws.len();
            world
                .resource_mut::<super::VegetationProjectionStats>()
                .prototype_preview = Some(super::VegetationPrototypePreviewStats {
                trees: source.kind_counts.trees,
                primitive_entities: count,
                procedural_geometry_counts_valid: false,
            });
            for (draw, temporal) in draws.into_iter().zip(temporal) {
                let key = TreeDrawKey {
                    placement: draw.placement,
                    lod: draw.lod,
                    primitive: draw.primitive,
                };
                let id = previous
                    .remove(&key)
                    .filter(|id| world.get_entity(*id).is_ok())
                    .unwrap_or_else(|| world.spawn_empty().id());
                let mut entity = world.entity_mut(id);
                entity.insert((
                    Mesh3d(draw.mesh),
                    MeshMaterial3d(draw.material),
                    draw.transform,
                    draw.tag,
                    draw.range,
                    draw.bounds,
                    NoAutoAabb,
                    GovernedTreePrimitive {
                        placement_id: draw.placement,
                        lod: draw.lod,
                    },
                ));
                if let Some(member) = temporal {
                    entity.insert((MeshTag(member.slot * 4 + member.level), member));
                }
                if draw.lod > 1 {
                    entity.insert(NotShadowCaster);
                } else {
                    entity.remove::<NotShadowCaster>();
                }
                state.entities.insert(key, id);
            }
            for entity in previous.into_values() {
                world.despawn(entity);
            }
            // Existing triangle/visibility telemetry describes procedural recipes, not this preview.
            // Report actual added/removed draw entities separately until unified telemetry is wired.
            let ranges = prototype_lod_ranges(profile);
            info!(
                event_kind = "governed_forest_preview_projected",
                detail_fade_start_m = ranges.near.end_margin.start,
                detail_fade_end_m = ranges.near.end_margin.end,
                trees = source.kind_counts.trees,
                primitive_entities = count,
                replaced_procedural_entities = old_trees.len(),
                procedural_triangle_metrics_valid = false,
                "shared three-LOD forest preview; chunk HLOD/performance qualification pending"
            );
            state.source = Some(source);
            state.profile = Some(profile);
            state.prototypes = Some(world.resource::<VegetationPrototypeWindCache>().clone());
            if let Err(error) = super::far_batches::project(world) {
                fail_preview(world, &mut state, error);
            } else {
                state.failed = false;
                world
                    .resource_mut::<super::VegetationProjectionStats>()
                    .prototype_preview_error = None;
            }
        }
        Err(error) => fail_preview(world, &mut state, error),
    }
    world.insert_resource(state);
}

/// Register once per projection, not per frame. Every primitive shares the placement's clock.
fn prepare_temporal(
    world: &mut World,
    draws: &[TreeDraw],
    profile: VegetationPresentationProfile,
) -> Result<Vec<Option<crate::temporal_lod::render::Member>>, String> {
    use crate::temporal_lod::render::{GroupKey, Groups, Member};
    if !world.contains_resource::<Groups>() {
        return Ok(vec![None; draws.len()]);
    }
    let ranges = prototype_lod_ranges(profile);
    let centre = |range: &std::ops::Range<f32>| range.start.midpoint(range.end);
    let policy = crate::TemporalLodPolicy::new(
        &[
            centre(&ranges.near.end_margin),
            centre(&ranges.medium.end_margin),
            profile.cull_distance_m,
        ],
        2.0,
        0.2,
    )
    .map_err(|error| format!("forest temporal LOD policy: {error:?}"))?;
    let mut result = Vec::with_capacity(draws.len());
    for draw in draws {
        let slot = world.resource_mut::<Groups>().register(
            GroupKey::Tree(draw.placement),
            draw.transform.translation,
            draw.tag.0,
            policy,
        )?;
        // Even a same-profile remesh needs a new renderer receipt. Root-only motion does not.
        world.resource_mut::<Groups>().invalidate(slot);
        result.push(Some(Member {
            slot,
            level: u32::from(draw.lod),
            original_tag: draw.tag.0,
            original_range: draw.range.clone(),
        }));
    }
    for draw in draws {
        if let Some(mut material) = world
            .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
            .get_mut(&draw.material)
        {
            material.extension.temporal_parameters.x = 1.0;
        }
    }
    Ok(result)
}

fn fail_preview(world: &mut World, state: &mut PreviewState, error: String) {
    let recovery = super::quality::restore_procedural(world);
    if recovery.is_ok() {
        super::far_batches::clear(world);
        for entity in std::mem::take(&mut state.entities).into_values() {
            world.despawn(entity);
        }
    }
    error!(event_kind = "governed_forest_preview_failed", reason = %error,
        procedural_restored = recovery.is_ok(), recovery_error = ?recovery.err());
    world
        .resource_mut::<super::VegetationProjectionStats>()
        .prototype_preview_error = Some(error);
    state.failed = true;
    state.prototypes = world
        .get_resource::<VegetationPrototypeWindCache>()
        .cloned();
}

type HlodReplacement = (Entity, Handle<Mesh>, Option<DerivedVegetationMesh>, u32);

fn replace_batches(world: &mut World, batches: Vec<HlodReplacement>) {
    for (entity, handle, derived, count) in batches {
        if let Some(derived) = derived {
            *world
                .resource_mut::<Assets<Mesh>>()
                .get_mut(&handle)
                .expect("validated owned HLOD mesh") = derived.mesh;
            world
                .entity_mut(entity)
                .insert((Mesh3d(handle), derived.aabb));
        } else {
            // Retain owned quality bindings, but stop drawing an empty batch.
            world.entity_mut(entity).remove::<Mesh3d>();
        }
        world
            .get_mut::<VegetationChunkBatchMarker>(entity)
            .expect("validated HLOD marker")
            .logical_placements = count;
    }
}

/// Dense governed meshes switch detail earlier than the lightweight procedural trees.
/// Keep the medium/far envelopes and the same complementary eight-metre fade.
fn prototype_lod_ranges(profile: VegetationPresentationProfile) -> VegetationLodRanges {
    VegetationLodRanges::from_governed_profile(profile)
}

fn prepare(
    world: &mut World,
    source: &forge_world::GeneratedVegetation,
    profile: VegetationPresentationProfile,
) -> Result<(Vec<TreeDraw>, Vec<HlodReplacement>), String> {
    let cache = world.resource::<VegetationPrototypeWindCache>();
    let meshes = world.resource::<Assets<Mesh>>();
    let mut mesh_bounds = bounds::ProjectionBounds::new(meshes);
    let ranges = prototype_lod_ranges(profile);
    let margin = maximum_deformation_m(profile);
    let mut draws = Vec::new();
    for tree in source
        .chunks
        .iter()
        .flat_map(|chunk| &chunk.instances)
        .filter(|tree| tree.kind == VegetationKind::Tree)
    {
        let lods = cache.for_placement(tree.layer_id.as_str(), tree.id.as_entity_id())?;
        for (lod, primitives) in lods.iter().enumerate() {
            if primitives.is_empty() {
                return Err("empty governed LOD".into());
            }
            for (primitive_index, primitive) in primitives.iter().enumerate() {
                let bounds = mesh_bounds.get(primitive.mesh.id())?;
                draws.push(TreeDraw {
                    placement: tree.id,
                    lod: u8::try_from(lod).map_err(|error| error.to_string())?,
                    primitive: primitive_index,
                    mesh: primitive.mesh.clone(),
                    material: primitive.material.clone(),
                    transform: Transform::from_xyz(
                        tree.position_m.x,
                        tree.position_m.y,
                        tree.position_m.z,
                    )
                    .with_rotation(Quat::from_rotation_y(tree.yaw_radians))
                    .with_scale(Vec3::splat(tree.scale)),
                    tag: mesh_tag_for(tree.wind_phase_radians, tree.wind_response),
                    range: match lod {
                        0 => ranges.near.clone(),
                        1 => ranges.medium.clone(),
                        _ => ranges.hlod.clone(),
                    },
                    bounds: Aabb::from_min_max(
                        Vec3::from(bounds.min()) - Vec3::splat(margin),
                        Vec3::from(bounds.max()) + Vec3::splat(margin),
                    ),
                });
                if draws.len() > 32_000 {
                    return Err("forest preview exceeds 32000 primitive entities".into());
                }
            }
        }
    }
    let targets: Vec<_> = world
        .query::<(Entity, &VegetationChunkBatchMarker, &Mesh3d)>()
        .iter(world)
        .filter(|(_, marker, _)| marker.lod == VegetationProjectionLod::Hlod)
        .map(|(entity, marker, mesh)| (entity, *marker, mesh.0.clone()))
        .collect();
    let mut batches = Vec::new();
    for (entity, marker, mesh) in targets {
        let chunk = source
            .chunks
            .iter()
            .find(|chunk| {
                chunk.coordinate.x == marker.chunk_x && chunk.coordinate.z == marker.chunk_z
            })
            .ok_or("HLOD chunk missing from active scatter")?;
        let center = Vec3::new(
            source.origin_xz_m[0] + (f32::from(marker.chunk_x) + 0.5) * VEGETATION_CHUNK_SIZE_M,
            0.,
            source.origin_xz_m[1] + (f32::from(marker.chunk_z) + 0.5) * VEGETATION_CHUNK_SIZE_M,
        );
        let other: Vec<_> = chunk
            .instances
            .iter()
            .filter(|instance| {
                !matches!(instance.kind, VegetationKind::Tree | VegetationKind::Grass)
            })
            .map(|instance| procedural_placement(instance, center))
            .collect();
        if !world.resource::<Assets<Mesh>>().contains(mesh.id()) {
            return Err("HLOD source mesh missing".into());
        }
        let derived = if other.is_empty() {
            None
        } else {
            Some(build_hlod_chunk_mesh(&other, margin).map_err(|error| error.to_string())?)
        };
        batches.push((
            entity,
            mesh,
            derived,
            u32::try_from(other.len()).map_err(|error| error.to_string())?,
        ));
    }
    Ok((draws, batches))
}

#[cfg(test)]
mod tests {
    use super::super::*;
    use super::*;

    #[test]
    fn governed_shadow_fade_matches_actual_caster_range() {
        let mut profile = forge_world::VisualValleyVegetationRecipe::default()
            .build()
            .unwrap()
            .presentation;
        for near in [12.0, 24.0, 32.0, 48.0] {
            profile.near_lod_distance_m = near;
            let range = prototype_lod_ranges(profile).medium.end_margin;
            let material = VisualValleyVegetationExtension::from_profile_and_interaction(
                profile,
                VegetationInteractionField::default(),
            )
            .with_governed_prototype_height(10.0)
            .unwrap();
            assert_eq!(
                material.shadow_fade_distances.x.to_bits(),
                range.start.to_bits()
            );
            assert_eq!(
                material.shadow_fade_distances.y.to_bits(),
                range.end.to_bits()
            );
        }
    }

    fn preview_app() -> App {
        preview_app_with_collisions(true)
    }

    #[test]
    fn governed_detail_fade_is_bounded_without_changing_procedural_or_far_ranges() {
        let app = preview_app();
        let mut profile = app.world().resource::<VegetationPresentation>().0;
        let procedural = VegetationLodRanges::from_profile(profile);
        let governed = prototype_lod_ranges(profile);
        assert_eq!(procedural.near.end_margin, 64.0..72.0);
        assert_eq!(governed.near.end_margin, 24.0..32.0);
        assert_eq!(governed.medium.start_margin, 24.0..32.0);
        assert_eq!(governed.medium.end_margin, procedural.medium.end_margin);
        assert!(governed.hlod == procedural.hlod);
        assert!(governed.grass == procedural.grass);
        assert!(governed.individual == procedural.individual);
        profile.medium_lod_distance_m = 28.0;
        let narrow = prototype_lod_ranges(profile);
        assert_eq!(narrow.near.end_margin, 24.0..28.0);
        assert_eq!(narrow.medium.start_margin, narrow.near.end_margin);
    }

    fn preview_app_with_collisions(collisions: bool) -> App {
        let mut source = forge_world::compile_world_sketch(
            &forge_world::embedded_visual_valley_world_sketch().unwrap(),
        )
        .unwrap();
        if !collisions {
            for layer in &mut source.vegetation.layers {
                layer.collision = None;
            }
        }
        let mut policy = forge_project::VisualValleyQualityProfileDocument::from_json(
            include_str!("../../../../examples/visual_valley/quality-profile.v1.json"),
        )
        .unwrap();
        policy.revision = source.carved_terrain.revision;
        let request = VegetationQualityRequest::new(
            &source.carved_terrain,
            policy,
            forge_project::VisualQualityTier::High,
        )
        .unwrap();
        let prepared = prepare_vegetation_projection_with_quality(
            &source.carved_terrain,
            &source.vegetation,
            Some(&request),
        )
        .unwrap();
        let mut app = App::new();
        app.init_resource::<Assets<Mesh>>()
            .init_resource::<Assets<VisualValleyVegetationMaterial>>()
            .insert_resource(VegetationPresentation(prepared.profile))
            .insert_resource(VegetationInteractionField::disabled())
            .insert_resource(prepared.stats.clone())
            .insert_resource(PreparedVegetation(Some(prepared)))
            .insert_resource(quality::VegetationQualityBindings::new(
                source.carved_terrain.clone(),
                source.vegetation.clone(),
            ))
            .insert_resource(request)
            .add_systems(Startup, spawn_vegetation_projection)
            .add_systems(Update, quality::apply_quality);
        app.update();
        let mesh = app
            .world()
            .resource::<quality::VegetationQualityBindings>()
            .meshes[&VegetationMeshKey::TreeNear]
            .0
            .clone();
        let material = app.world().resource::<VegetationMaterialHandle>().0.clone();
        let primitive = crate::WindVegetationPrimitive { mesh, material };
        app.insert_resource(VegetationPrototypeWindCache {
            selection_catalog: None,
            layers: std::collections::BTreeMap::from([(
                "forge_trees".into(),
                std::array::from_fn(|_| vec![primitive.clone()]),
            )]),
            failure: None,
        })
        .insert_resource(VegetationPrototypeForestPreview);
        app
    }

    type TreePoses = std::collections::HashMap<(VegetationInstanceId, u8), Transform>;

    #[test]
    fn temporal_forest_shares_slots_across_all_lods_and_far_geometry() {
        use crate::temporal_lod::render::{BatchMembers, Groups, Member};
        let mut app = preview_app();
        app.init_resource::<Groups>();
        let material = app.world().resource::<VegetationMaterialHandle>().0.clone();
        app.world_mut()
            .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
            .get_mut(&material)
            .unwrap()
            .extension
            .prototype_parameters = Vec4::new(1., 1. / 6., 0., 0.);
        app.insert_resource(super::super::far_batches::GovernedFarBatchPreview);
        project(app.world_mut());
        let world = app.world_mut();
        assert!(
            world
                .resource::<VegetationProjectionStats>()
                .prototype_preview_error
                .is_none()
        );
        let mut slots = HashMap::new();
        for (tree, member, tag) in world
            .query::<(&GovernedTreePrimitive, &Member, &MeshTag)>()
            .iter(world)
        {
            let slot = slots.entry(tree.placement_id).or_insert(member.slot);
            assert_eq!(*slot, member.slot);
            assert_eq!(member.level, u32::from(tree.lod));
            assert_eq!(tag.0, member.slot * 4 + member.level);
        }
        assert!(!slots.is_empty());
        let all_slots: std::collections::HashSet<_> = slots.values().copied().collect();
        let mut count = 0;
        for (batch, mesh) in world.query::<(&BatchMembers, &Mesh3d)>().iter(world) {
            assert!(batch.slots.iter().all(|slot| all_slots.contains(slot)));
            let mesh = world.resource::<Assets<Mesh>>().get(&mesh.0).unwrap();
            let Some(bevy::mesh::VertexAttributeValues::Float32x4(wind)) =
                mesh.attribute(crate::vegetation_far_geometry::WIND_DATA)
            else {
                panic!()
            };
            assert!(
                wind.iter()
                    .all(|value| value[3] > 0.0 && value[3] <= 32_768.0)
            );
            count += 1;
        }
        assert!(count > 0);
    }

    #[test]
    fn replacing_cached_prototype_updates_draws_without_moving_or_recreating_trees() {
        let mut app = preview_app();
        project(app.world_mut());
        let before = app.world().resource::<PreviewState>().entities.clone();
        let poses: HashMap<_, _> = before
            .values()
            .map(|id| (*id, *app.world().get::<Transform>(*id).unwrap()))
            .collect();
        let old = app
            .world()
            .resource::<VegetationPrototypeWindCache>()
            .layers["forge_trees"][0][0]
            .mesh
            .clone();
        let mesh = app
            .world()
            .resource::<Assets<Mesh>>()
            .get(&old)
            .unwrap()
            .clone();
        let replacement = app.world_mut().resource_mut::<Assets<Mesh>>().add(mesh);
        for lod in app
            .world_mut()
            .resource_mut::<VegetationPrototypeWindCache>()
            .layers
            .values_mut()
            .flatten()
        {
            for primitive in lod {
                primitive.mesh = replacement.clone();
            }
        }
        project(app.world_mut());
        assert_eq!(app.world().resource::<PreviewState>().entities, before);
        for id in before.values() {
            assert_eq!(app.world().get::<Mesh3d>(*id).unwrap().0, replacement);
            assert_eq!(*app.world().get::<Transform>(*id).unwrap(), poses[id]);
        }
    }

    fn variant_catalog() -> forge_assets::VegetationPrototypeCatalog {
        let binding = |asset: &str| forge_assets::VegetationPrototypeBinding {
            layer_id: "forge_trees".into(),
            asset_id: asset.into(),
            glb_sha256: forge_core::ContentHash::from_bytes([1; 32]),
            source_pivot_m: [0.0; 3],
            source_height_m: 6.0,
            target_height_m: 6.0,
        };
        forge_assets::VegetationPrototypeCatalog {
            schema: "forge.vegetation-prototype-catalog".into(),
            schema_version: 2,
            vegetation_sha256: forge_core::ContentHash::from_bytes([2; 32]),
            asset_kit_id: "synthetic-cache-test".into(),
            asset_kit_revision: 1,
            bindings: vec![binding("first"), binding("second")],
        }
    }

    #[test]
    fn governed_variants_share_models_across_lods_and_quality_without_moving_trees() {
        let mut app = preview_app();
        let prototype = app
            .world()
            .resource::<VegetationPrototypeWindCache>()
            .layers["forge_trees"][0][0]
            .clone();
        let second_mesh = app
            .world()
            .resource::<Assets<Mesh>>()
            .get(&prototype.mesh)
            .unwrap()
            .clone();
        let second = crate::WindVegetationPrimitive {
            mesh: app
                .world_mut()
                .resource_mut::<Assets<Mesh>>()
                .add(second_mesh),
            material: prototype.material.clone(),
        };
        let catalog = variant_catalog();
        let mut layers = std::collections::BTreeMap::new();
        for (binding, primitive) in catalog
            .bindings
            .iter()
            .zip([prototype.clone(), second.clone()])
        {
            layers.insert(
                crate::vegetation_prototypes::prototype_key(2, binding),
                std::array::from_fn(|_| vec![primitive.clone()]),
            );
        }
        app.insert_resource(VegetationPrototypeWindCache {
            selection_catalog: Some(catalog.clone()),
            layers,
            failure: None,
        });
        project(app.world_mut());
        let initial = observed_tree_poses(app.world_mut());
        let mesh_count = app.world().resource::<Assets<Mesh>>().len();
        for tier in [
            forge_project::VisualQualityTier::Low,
            forge_project::VisualQualityTier::High,
        ] {
            app.world_mut()
                .resource_mut::<VegetationQualityRequest>()
                .request(tier);
            app.update();
            project(app.world_mut());
            assert!(!app.world().resource::<PreviewState>().failed);
            assert_eq!(observed_tree_poses(app.world_mut()), initial);
            let mut seen = std::collections::HashSet::new();
            for (marker, mesh) in app
                .world_mut()
                .query::<(&GovernedTreePrimitive, &Mesh3d)>()
                .iter(app.world())
            {
                let selected = catalog
                    .select_binding("forge_trees", marker.placement_id.as_entity_id())
                    .unwrap()
                    .unwrap();
                let expected = if selected.asset_id == "first" {
                    &prototype.mesh
                } else {
                    &second.mesh
                };
                assert_eq!(&mesh.0, expected);
                seen.insert(selected.asset_id.clone());
            }
            assert_eq!(seen.len(), 2);
            assert_eq!(app.world().resource::<Assets<Mesh>>().len(), mesh_count);
            assert_eq!(
                app.world_mut()
                    .query::<&bevy_rapier3d::prelude::Collider>()
                    .iter(app.world())
                    .count(),
                1662
            );
        }
        let mut cache = app
            .world_mut()
            .resource_mut::<VegetationPrototypeWindCache>();
        cache.layers.clear();
        assert!(
            cache
                .for_placement("forge_trees", forge_core::EntityId::deterministic("tree"))
                .is_err()
        );
    }

    fn independent_far_app() -> App {
        let mut app = preview_app();
        let original = app
            .world()
            .resource::<VegetationPrototypeWindCache>()
            .layers["forge_trees"][0][0]
            .clone();
        let mesh = app
            .world()
            .resource::<Assets<Mesh>>()
            .get(&original.mesh)
            .unwrap()
            .clone();
        let mut material = app
            .world()
            .resource::<Assets<VisualValleyVegetationMaterial>>()
            .get(&original.material)
            .unwrap()
            .clone();
        material.extension = material
            .extension
            .with_governed_prototype_height(6.)
            .unwrap();
        let mesh = app.world_mut().resource_mut::<Assets<Mesh>>().add(mesh);
        let material = app
            .world_mut()
            .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
            .add(material);
        let primitive = crate::WindVegetationPrimitive { mesh, material };
        app.world_mut()
            .resource_mut::<VegetationPrototypeWindCache>()
            .layers
            .insert(
                "forge_trees".into(),
                std::array::from_fn(|_| vec![primitive.clone()]),
            );
        app.insert_resource(super::super::far_batches::GovernedFarBatchPreview);
        project(app.world_mut());
        app
    }

    fn far_bindings(
        world: &mut World,
    ) -> std::collections::BTreeMap<Entity, bevy::asset::AssetId<Mesh>> {
        world
            .query_filtered::<(Entity, &Mesh3d), With<super::super::far_batches::GovernedFarBatch>>(
            )
            .iter(world)
            .map(|(entity, mesh)| (entity, mesh.0.id()))
            .collect()
    }

    #[test]
    fn far_quality_cycle_reuses_geometry_and_updates_ranges() {
        use forge_project::VisualQualityTier::{High, Low};
        let mut app = independent_far_app();
        assert!(quality_ready(app.world()));
        let initial = far_bindings(app.world_mut());
        assert!(!initial.is_empty());
        let entity = *initial.keys().next().unwrap();
        let initial_range = app.world().get::<VisibilityRange>(entity).unwrap().clone();
        for tier in [Low, High] {
            app.world_mut()
                .resource_mut::<VegetationQualityRequest>()
                .request(tier);
            app.update();
            project(app.world_mut());
            assert!(quality_ready(app.world()));
            assert_eq!(far_bindings(app.world_mut()), initial);
            let range = app.world().get::<VisibilityRange>(entity).unwrap();
            assert_eq!(*range == initial_range, tier == High);
            let world = app.world_mut();
            assert!(
                world
                    .query::<(&GovernedTreePrimitive, &Mesh3d)>()
                    .iter(world)
                    .all(|(marker, _)| marker.lod < 2)
            );
        }
    }

    #[test]
    fn governed_quality_receipt_rejects_missing_batches_and_stale_profile_or_catalog() {
        let mut app = preview_app();
        assert!(!quality_ready(app.world()));
        project(app.world_mut());
        assert!(!quality_ready(app.world())); // Individual far LODs are not chunk batching.
        let mut app = independent_far_app();
        assert!(quality_ready(app.world()));
        let profile = app.world().resource::<VegetationPresentation>().0;
        app.world_mut()
            .resource_mut::<VegetationPresentation>()
            .0
            .cull_distance_m += 1.0;
        assert!(!quality_ready(app.world()));
        app.world_mut().resource_mut::<VegetationPresentation>().0 = profile;
        assert!(quality_ready(app.world()));
        app.world_mut()
            .resource_mut::<VegetationPrototypeWindCache>()
            .failure = Some("lost catalog".into());
        assert!(!quality_ready(app.world()));
        app.world_mut()
            .resource_mut::<VegetationPrototypeWindCache>()
            .failure = None;
        assert!(quality_ready(app.world()));
        let entity = *far_bindings(app.world_mut()).keys().next().unwrap();
        app.world_mut().entity_mut(entity).remove::<Mesh3d>();
        assert!(!quality_ready(app.world()));
    }

    #[test]
    fn far_cache_invalidates_in_place_source_mesh_and_height_changes() {
        use forge_project::VisualQualityTier::{High, Low};
        let mut app = independent_far_app();
        let mut before = far_bindings(app.world_mut());
        let primitive = app
            .world()
            .resource::<VegetationPrototypeWindCache>()
            .layers["forge_trees"][2][0]
            .clone();
        {
            let mut assets = app.world_mut().resource_mut::<Assets<Mesh>>();
            let mut mesh = assets.get_mut(&primitive.mesh).unwrap();
            let Some(bevy::mesh::VertexAttributeValues::Float32x3(positions)) =
                mesh.attribute_mut(Mesh::ATTRIBUTE_POSITION)
            else {
                panic!()
            };
            positions[0][1] += 0.1;
        }
        for tier in [Low, High] {
            if tier == High {
                app.world_mut()
                    .resource_mut::<Assets<VisualValleyVegetationMaterial>>()
                    .get_mut(&primitive.material)
                    .unwrap()
                    .extension
                    .prototype_parameters
                    .y = 1. / 7.;
            }
            app.world_mut()
                .resource_mut::<VegetationQualityRequest>()
                .request(tier);
            app.update();
            project(app.world_mut());
            let after = far_bindings(app.world_mut());
            assert!(!after.is_empty());
            assert_ne!(after, before);
            for (entity, mesh) in before {
                assert!(app.world().get_entity(entity).is_err());
                assert!(!app.world().resource::<Assets<Mesh>>().contains(mesh));
            }
            before = after;
        }
    }

    #[test]
    fn far_reuse_cpu_comparison_preserves_the_same_scene() {
        use forge_project::VisualQualityTier::{High, Low};
        let mut app = independent_far_app();
        let mut reused_ms = Vec::new();
        let mut rebuilt_ms = Vec::new();
        // Same synthetic forest, input meshes and profile sequence in both paths.
        // Alternating order; no timing assertion or GPU/frame-rate conclusion.
        for round in 0..4 {
            for rebuild in [round % 2 == 0, round % 2 != 0] {
                let before = far_bindings(app.world_mut());
                app.world_mut()
                    .resource_mut::<VegetationQualityRequest>()
                    .request(if round % 2 == 0 { Low } else { High });
                app.update();
                app.world_mut().resource_mut::<PreviewState>().profile = None;
                if rebuild {
                    super::super::far_batches::discard_input_snapshot(app.world_mut());
                }
                let started = std::time::Instant::now();
                project(app.world_mut());
                let elapsed = started.elapsed().as_secs_f64() * 1000.;
                assert_eq!(far_bindings(app.world_mut()) == before, !rebuild);
                if rebuild {
                    rebuilt_ms.push(elapsed);
                } else {
                    reused_ms.push(elapsed);
                }
            }
        }
        println!("synthetic far projection CPU ms: rebuild={rebuilt_ms:?}; reuse={reused_ms:?}");
    }

    #[test]
    fn far_batch_opt_in_replaces_only_far_draws_and_restores_on_failure() {
        let mut app = preview_app();
        let material = app.world().resource::<VegetationMaterialHandle>().0.clone();
        {
            let materials = &mut app
                .world_mut()
                .resource_mut::<Assets<VisualValleyVegetationMaterial>>();
            let mut value = materials.get_mut(&material).unwrap();
            value.extension = value
                .extension
                .clone()
                .with_governed_prototype_height(6.)
                .unwrap();
        }
        app.insert_resource(super::super::far_batches::GovernedFarBatchPreview);
        project(app.world_mut());
        let world = app.world_mut();
        assert!(
            world
                .resource::<VegetationProjectionStats>()
                .prototype_preview_error
                .is_none()
        );
        let batches = world
            .query_filtered::<Entity, With<super::super::far_batches::GovernedFarBatch>>()
            .iter(world)
            .count();
        assert!(batches > 0 && batches <= 64);
        let live = world
            .query::<(&GovernedTreePrimitive, &Mesh3d)>()
            .iter(world)
            .map(|(marker, _)| marker.lod)
            .collect::<Vec<_>>();
        assert_eq!(live.len(), 1662 * 2);
        assert!(live.iter().all(|lod| *lod < 2));
        observed_tree_poses(world);
        world.resource_mut::<VegetationPrototypeWindCache>().failure =
            Some("injected far failure".into());
        project(world);
        assert_eq!(
            world
                .query_filtered::<Entity, With<super::super::far_batches::GovernedFarBatch>>()
                .iter(world)
                .count(),
            0
        );
        assert_eq!(
            world.query::<&GovernedTreePrimitive>().iter(world).count(),
            0
        );
        assert!(
            world
                .resource::<VegetationProjectionStats>()
                .prototype_preview_error
                .is_some()
        );
    }

    fn observed_tree_poses(world: &mut World) -> TreePoses {
        let draws: Vec<_> = world
            .query::<(&GovernedTreePrimitive, &Transform)>()
            .iter(world)
            .map(|(draw, transform)| ((draw.placement_id, draw.lod), *transform))
            .collect();
        let poses: std::collections::HashMap<_, _> = draws.iter().copied().collect();
        assert_eq!(poses.len(), draws.len(), "no duplicate placement/LOD");
        for tree in world.resource::<VegetationScatterProjection>().trees() {
            let expected =
                Transform::from_xyz(tree.position_m.x, tree.position_m.y, tree.position_m.z)
                    .with_rotation(Quat::from_rotation_y(tree.yaw_radians))
                    .with_scale(Vec3::splat(tree.scale));
            for lod in 0..3 {
                assert_eq!(poses[&(tree.id, lod)], expected);
            }
        }
        poses
    }

    #[test]
    fn quality_cycle_preserves_live_governed_bindings_for_every_surviving_primitive() {
        use forge_project::VisualQualityTier::{High, Low, Medium, Ultra};
        let mut app = preview_app();
        // Multiple primitives per LOD exercise the key beyond placement/LOD alone.
        for lods in app
            .world_mut()
            .resource_mut::<VegetationPrototypeWindCache>()
            .layers
            .values_mut()
        {
            for primitives in lods {
                primitives.push(primitives[0].clone());
            }
        }
        project(app.world_mut());
        let initial = app.world().resource::<PreviewState>().entities.clone();
        assert_eq!(initial.len(), 1662 * 3 * 2);
        for tier in [Low, Medium, Ultra, High] {
            app.world_mut()
                .resource_mut::<VegetationQualityRequest>()
                .request(tier);
            app.update();
            project(app.world_mut());
            assert_eq!(app.world().resource::<PreviewState>().entities, initial);
            let profile = app.world().resource::<VegetationPresentation>().0;
            let ranges = prototype_lod_ranges(profile);
            for (key, entity) in &initial {
                let range = app.world().get::<VisibilityRange>(*entity).unwrap();
                let expected = match key.lod {
                    0 => &ranges.near,
                    1 => &ranges.medium,
                    _ => &ranges.hlod,
                };
                assert!(
                    range == expected,
                    "LOD range does not match current profile"
                );
                assert_eq!(
                    app.world()
                        .get::<GovernedTreePrimitive>(*entity)
                        .unwrap()
                        .placement_id,
                    key.placement
                );
                assert_eq!(
                    app.world().get::<NotShadowCaster>(*entity).is_some(),
                    key.lod > 1
                );
            }
        }
    }

    #[test]
    fn decorative_forest_density_changes_without_moving_survivors_or_sources() {
        use forge_project::VisualQualityTier::{High, Low, Medium, Ultra};
        // Synthetic recipe variant only: unlike obstacles, decorative trees may thin out.
        let mut app = preview_app_with_collisions(false);
        project(app.world_mut());
        let initial = observed_tree_poses(app.world_mut());
        let stats = app.world().resource::<VegetationProjectionStats>();
        let source_hashes = (
            stats.terrain_document_hash.clone(),
            stats.vegetation_document_hash.clone(),
        );
        let mut previous: Option<TreePoses> = None;
        let mut previous_entities = app.world().resource::<PreviewState>().entities.clone();
        for tier in [Low, Medium, High, Ultra, High] {
            app.world_mut()
                .resource_mut::<VegetationQualityRequest>()
                .request(tier);
            app.update();
            project(app.world_mut());
            assert!(!app.world().resource::<PreviewState>().failed);
            let current = observed_tree_poses(app.world_mut());
            let current_entities = &app.world().resource::<PreviewState>().entities;
            for (key, entity) in current_entities {
                if let Some(previous) = previous_entities.get(key) {
                    assert_eq!(entity, previous, "surviving draw recreated");
                }
            }
            for entity in previous_entities.values() {
                if !current_entities.values().any(|current| current == entity) {
                    assert!(
                        app.world().get_entity(*entity).is_err(),
                        "removed draw leaked"
                    );
                }
            }
            previous_entities = current_entities.clone();
            assert!(!current.is_empty());
            if tier == Low {
                assert!(current.len() < initial.len());
            } else if tier == High {
                assert_eq!(current, initial, "High restores identical IDs and poses");
            } else if tier == Ultra {
                assert!(current.len() > initial.len());
            }
            if let Some(previous) = previous.as_ref() {
                let (smaller, larger) = if tier == High && previous.len() > current.len() {
                    (&current, previous)
                } else {
                    (previous, &current)
                };
                for (key, pose) in smaller {
                    assert_eq!(larger.get(key), Some(pose), "surviving pose changed");
                }
            }
            let stats = app.world().resource::<VegetationProjectionStats>();
            assert_eq!(
                (
                    &stats.terrain_document_hash,
                    &stats.vegetation_document_hash
                ),
                (&source_hashes.0, &source_hashes.1)
            );
            assert_eq!(
                stats.prototype_preview.as_ref().unwrap().primitive_entities,
                current.len()
            );
            assert_eq!(
                app.world_mut()
                    .query::<&bevy_rapier3d::prelude::Collider>()
                    .iter(app.world())
                    .count(),
                0
            );
            previous = Some(current);
        }
    }

    #[test]
    fn forest_preview_replaces_all_three_lods_without_changing_tree_colliders() {
        let mut app = preview_app();
        let original_colliders = app
            .world_mut()
            .query::<&bevy_rapier3d::prelude::Collider>()
            .iter(app.world())
            .count();
        for tier in [
            forge_project::VisualQualityTier::High,
            forge_project::VisualQualityTier::Low,
            forge_project::VisualQualityTier::High,
        ] {
            app.world_mut()
                .resource_mut::<VegetationQualityRequest>()
                .request(tier);
            app.update();
            project(app.world_mut());
            assert!(!app.world().resource::<PreviewState>().failed);
            assert_eq!(
                app.world_mut()
                    .query::<&GovernedTreePrimitive>()
                    .iter(app.world())
                    .count(),
                1662 * 3
            );
            assert_eq!(
                app.world_mut()
                    .query::<&bevy_rapier3d::prelude::Collider>()
                    .iter(app.world())
                    .count(),
                original_colliders
            );
            assert!(
                app.world_mut()
                    .query::<(&VegetationPlacementMarker, Option<&Mesh3d>)>()
                    .iter(app.world())
                    .filter(|(marker, _)| marker.kind == VegetationKind::Tree)
                    .all(|(_, mesh)| mesh.is_none())
            );
            let keys: std::collections::HashSet<_> = app
                .world_mut()
                .query::<&GovernedTreePrimitive>()
                .iter(app.world())
                .map(|draw| (draw.placement_id, draw.lod))
                .collect();
            for tree in app
                .world()
                .resource::<VegetationScatterProjection>()
                .trees()
            {
                for lod in 0..3 {
                    assert!(keys.contains(&(tree.id, lod)));
                }
            }
        }
        assert_eq!(original_colliders, 1662);
    }

    #[test]
    fn tree_only_batches_stop_drawing_without_removing_owned_bindings() {
        let mut app = preview_app();
        // Synthetic derived projection exercises empty residual HLOD batches. No document mutation.
        {
            let mut scatter = app
                .world_mut()
                .resource_mut::<VegetationScatterProjection>();
            let generated = std::sync::Arc::make_mut(&mut scatter.generated);
            for chunk in &mut generated.chunks {
                chunk.instances.retain(|instance| {
                    matches!(instance.kind, VegetationKind::Tree | VegetationKind::Grass)
                });
            }
        }
        let bindings = app
            .world()
            .resource::<quality::VegetationQualityBindings>()
            .meshes
            .clone();
        project(app.world_mut());
        assert!(!app.world().resource::<PreviewState>().failed);
        let batches: Vec<_> = app
            .world_mut()
            .query::<(&VegetationChunkBatchMarker, Option<&Mesh3d>)>()
            .iter(app.world())
            .filter(|(marker, _)| marker.lod == VegetationProjectionLod::Hlod)
            .collect();
        assert_eq!(batches.len(), 64);
        assert!(
            batches
                .iter()
                .all(|(marker, mesh)| marker.logical_placements == 0 && mesh.is_none())
        );
        for (handle, _) in bindings.values() {
            assert!(app.world().resource::<Assets<Mesh>>().contains(handle.id()));
        }
        assert_eq!(
            app.world_mut()
                .query::<&GovernedTreePrimitive>()
                .iter(app.world())
                .count(),
            1662 * 3
        );
        assert_eq!(
            app.world_mut()
                .query::<&bevy_rapier3d::prelude::Collider>()
                .iter(app.world())
                .count(),
            1662
        );
    }

    #[test]
    fn corrected_prototype_cache_recovers_after_failure_without_duplicate_colliders() {
        let mut app = preview_app();
        project(app.world_mut());
        app.world_mut()
            .resource_mut::<VegetationPrototypeWindCache>()
            .failure = Some("bad prototype".into());
        project(app.world_mut());
        assert!(app.world().resource::<PreviewState>().failed);
        let source = app
            .world()
            .resource::<VegetationScatterProjection>()
            .generated
            .clone();
        project(app.world_mut());
        assert!(std::sync::Arc::ptr_eq(
            &source,
            &app.world()
                .resource::<VegetationScatterProjection>()
                .generated
        ));
        assert!(app.world().resource::<PreviewState>().entities.is_empty());
        app.world_mut()
            .resource_mut::<VegetationPrototypeWindCache>()
            .failure = None;
        project(app.world_mut());
        assert!(!app.world().resource::<PreviewState>().failed);
        assert_eq!(
            app.world().resource::<PreviewState>().entities.len(),
            1662 * 3
        );
        assert!(
            app.world()
                .resource::<VegetationProjectionStats>()
                .prototype_preview_error
                .is_none()
        );
        assert_eq!(
            app.world_mut()
                .query::<&bevy_rapier3d::prelude::Collider>()
                .iter(app.world())
                .count(),
            1662
        );
        let entities = app.world().resource::<PreviewState>().entities.clone();
        project(app.world_mut());
        assert_eq!(app.world().resource::<PreviewState>().entities, entities);
    }

    #[test]
    fn failed_preview_restores_procedural_without_mixing_draws_or_colliders() {
        for change_quality in [false, true] {
            let mut app = preview_app();
            project(app.world_mut());
            assert_eq!(
                app.world().resource::<PreviewState>().entities.len(),
                1662 * 3
            );
            app.world_mut()
                .resource_mut::<VegetationPrototypeWindCache>()
                .failure = Some("injected material failure".into());
            if change_quality {
                app.world_mut()
                    .resource_mut::<VegetationQualityRequest>()
                    .request(forge_project::VisualQualityTier::Low);
                app.update();
            }
            project(app.world_mut());
            assert!(app.world().resource::<PreviewState>().failed);
            assert!(app.world().resource::<PreviewState>().entities.is_empty());
            assert_eq!(
                app.world_mut()
                    .query::<&GovernedTreePrimitive>()
                    .iter(app.world())
                    .count(),
                0
            );
            let trees: Vec<_> = app
                .world_mut()
                .query::<(&VegetationPlacementMarker, Option<&Mesh3d>)>()
                .iter(app.world())
                .filter(|(marker, _)| marker.kind == VegetationKind::Tree)
                .collect();
            assert_eq!(trees.len(), 3324);
            assert!(trees.iter().all(|(_, mesh)| mesh.is_some()));
            assert_eq!(
                app.world_mut()
                    .query::<&bevy_rapier3d::prelude::Collider>()
                    .iter(app.world())
                    .count(),
                1662
            );
            let stats = app.world().resource::<VegetationProjectionStats>();
            assert!(stats.prototype_preview.is_none());
            assert_eq!(
                stats.prototype_preview_error.as_deref(),
                Some("injected material failure")
            );
            assert!(stats.overlay_details().contains("en échec"));
            app.world_mut()
                .resource_mut::<VegetationQualityRequest>()
                .request(forge_project::VisualQualityTier::Medium);
            app.update();
            project(app.world_mut());
            assert_eq!(
                app.world()
                    .resource::<VegetationProjectionStats>()
                    .prototype_preview_error
                    .as_deref(),
                Some("injected material failure")
            );
        }
    }
}
