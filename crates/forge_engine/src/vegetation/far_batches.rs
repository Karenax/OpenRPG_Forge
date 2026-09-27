//! Experimental chunk/material batching, consuming the existing governed LOD2 projection.
use super::{
    GovernedTreePrimitive, VegetationLodRanges, VegetationPresentation,
    VegetationScatterProjection, VisualValleyVegetationMaterial, maximum_deformation_m,
};
use crate::temporal_lod::render::{BatchMembers, Member};
use crate::vegetation_far_geometry::{FarPrimitive, build_far_geometry};
use bevy::{
    asset::AssetId,
    camera::{
        primitives::{Aabb, MeshAabb},
        visibility::{NoAutoAabb, VisibilityRange},
    },
    light::NotShadowCaster,
    mesh::MeshTag,
    prelude::*,
};
use forge_project::VEGETATION_CHUNK_SIZE_M;
use std::collections::{BTreeMap, HashMap};

/// Explicit development opt-in; not a qualified HLOD capability.
#[derive(Resource)]
pub struct GovernedFarBatchPreview;

/// Current chunk projection receipt. Missing/replaced batches invalidate it;
/// this is not a visible-triangle count or a GPU acknowledgment.
pub(super) fn quality_ready(world: &World) -> bool {
    if !world.contains_resource::<GovernedFarBatchPreview>() {
        return true;
    }
    world.get_resource::<BatchState>().is_some_and(|state| {
        state.inputs.is_some()
            && state.entities.len() == state.meshes.len()
            && state
                .entities
                .iter()
                .zip(&state.meshes)
                .all(|(entity, mesh)| {
                    world
                        .get::<Mesh3d>(*entity)
                        .is_some_and(|draw| draw.0 == *mesh)
                })
    })
}

#[cfg(test)]
mod readiness_tests {
    use super::*;

    #[test]
    fn inactive_far_batching_does_not_block_individual_tree_projection() {
        let mut world = World::new();
        assert!(quality_ready(&world));
        world.insert_resource(GovernedFarBatchPreview);
        assert!(!quality_ready(&world));
        world.insert_resource(BatchState::default());
        assert!(!quality_ready(&world));
        world.remove_resource::<GovernedFarBatchPreview>();
        assert!(quality_ready(&world));
    }
}

pub(super) use super::GovernedFarBatch;

#[derive(Resource, Default)]
struct BatchState {
    entities: Vec<Entity>,
    meshes: Vec<Handle<Mesh>>,
    inputs: Option<Inputs>,
    bounds: Vec<(Aabb, f32, f32)>,
}

type Groups = BTreeMap<(u16, u16, AssetId<VisualValleyVegetationMaterial>), Vec<Part>>;

struct Inputs {
    groups: Groups,
    origin: [f32; 2],
    // One snapshot per unique source, not per tree or merged batch. Exact comparison
    // detects in-place edits too, without relying on delayed asset events or handle identity.
    meshes: HashMap<AssetId<Mesh>, Mesh>,
}

#[derive(Clone, PartialEq)]
struct Part {
    entity: Entity,
    mesh: Handle<Mesh>,
    material: Handle<VisualValleyVegetationMaterial>,
    pose: Transform,
    tag: u32,
    height_m: f32,
    temporal_slot: u16,
}

struct Prepared {
    mesh: Mesh,
    material: Handle<VisualValleyVegetationMaterial>,
    center: Vec3,
    bounds: Aabb,
    radius: f32,
    maximum_scale: f32,
    originals: Vec<Entity>,
    temporal_slots: Vec<u32>,
}

fn padded_bounds(bounds: Aabb, padding: f32) -> Aabb {
    Aabb::from_min_max(
        Vec3::from(bounds.min()) - Vec3::splat(padding),
        Vec3::from(bounds.max()) + Vec3::splat(padding),
    )
}

fn batch_range(nominal: &VisibilityRange, radius: f32) -> VisibilityRange {
    let mut range = nominal.clone();
    let start = (nominal.start_margin.start - radius).max(0.);
    // Preserve the non-abrupt range that enables Bevy's dither varying.
    range.start_margin = start..start + (nominal.start_margin.end - nominal.start_margin.start);
    range.use_aabb = false;
    range.end_margin = (nominal.end_margin.end + radius)..(nominal.end_margin.end + radius);
    range
}

fn reuse(world: &mut World, groups: &Groups, origin: [f32; 2]) -> bool {
    let Some(state) = world.remove_resource::<BatchState>() else {
        return false;
    };
    let assets = world.resource::<Assets<Mesh>>();
    let valid = state.inputs.as_ref().is_some_and(|inputs| {
        inputs.origin.map(f32::to_bits) == origin.map(f32::to_bits)
            && inputs.groups == *groups
            && inputs
                .meshes
                .iter()
                .all(|(id, mesh)| assets.get(*id) == Some(mesh))
    }) && state
        .entities
        .iter()
        .zip(&state.meshes)
        .all(|(entity, mesh)| {
            assets.contains(mesh.id())
                && world
                    .get::<Mesh3d>(*entity)
                    .is_some_and(|draw| draw.0 == *mesh)
        });
    if valid {
        let profile = world.resource::<VegetationPresentation>().0;
        let nominal = VegetationLodRanges::from_profile(profile).hlod;
        for (entity, (bounds, radius, scale)) in state.entities.iter().zip(&state.bounds) {
            if let Some(mut members) = world.get_mut::<BatchMembers>(*entity) {
                members.original_range = batch_range(&nominal, *radius);
            }
            world.entity_mut(*entity).insert((
                batch_range(&nominal, *radius),
                padded_bounds(*bounds, maximum_deformation_m(profile) * scale),
            ));
        }
        for part in groups.values().flatten() {
            world.entity_mut(part.entity).remove::<Mesh3d>();
        }
        info!(
            event_kind = "governed_far_batches_reused",
            batches = state.entities.len(),
            "unchanged far geometry retained; profile ranges and deformation bounds updated"
        );
    }
    world.insert_resource(state);
    valid
}

pub(super) fn clear(world: &mut World) {
    if let Some(state) = world.remove_resource::<BatchState>() {
        for entity in state.entities {
            world.despawn(entity);
        }
        for mesh in state.meshes {
            world.resource_mut::<Assets<Mesh>>().remove(mesh.id());
        }
    }
}

#[cfg(test)]
pub(super) fn discard_input_snapshot(world: &mut World) {
    world.resource_mut::<BatchState>().inputs = None;
}

pub(super) fn project(world: &mut World) -> Result<(), String> {
    crate::quality_cpu_trace::measure(
        world,
        crate::QualityCpuPhase::GovernedFarBatches,
        project_inner,
    )
}

fn project_inner(world: &mut World) -> Result<(), String> {
    if !world.contains_resource::<GovernedFarBatchPreview>() {
        return Ok(());
    }
    let groups = collect(world)?;
    let origin = world
        .resource::<VegetationScatterProjection>()
        .generated
        .origin_xz_m;
    if reuse(world, &groups, origin) {
        return Ok(());
    }
    let prepared = prepare(world, &groups, origin)?;
    let assets = world.resource::<Assets<Mesh>>();
    let mut sources = HashMap::new();
    for part in groups.values().flatten() {
        sources.entry(part.mesh.id()).or_insert_with(|| {
            assets
                .get(&part.mesh)
                .expect("prepared far source exists")
                .clone()
        });
    }
    // All fallible geometry work precedes removal of the individual draw components.
    clear(world);
    let profile = world.resource::<VegetationPresentation>().0;
    let nominal = VegetationLodRanges::from_profile(profile).hlod;
    let mut state = BatchState {
        inputs: Some(Inputs {
            groups,
            origin,
            meshes: sources,
        }),
        ..default()
    };
    for batch in prepared {
        let mesh = world.resource_mut::<Assets<Mesh>>().add(batch.mesh);
        state.meshes.push(mesh.clone());
        state
            .bounds
            .push((batch.bounds, batch.radius, batch.maximum_scale));
        let entity = world
            .spawn((
                Mesh3d(mesh),
                MeshMaterial3d(batch.material),
                Transform::from_translation(batch.center),
                padded_bounds(
                    batch.bounds,
                    maximum_deformation_m(profile) * batch.maximum_scale,
                ),
                batch_range(&nominal, batch.radius),
                NoAutoAabb,
                NotShadowCaster,
                GovernedFarBatch,
            ))
            .id();
        state.entities.push(entity);
        if !batch.temporal_slots.is_empty() {
            world.entity_mut(entity).insert(BatchMembers {
                slots: batch.temporal_slots,
                original_range: batch_range(&nominal, batch.radius),
            });
        }
        for original in batch.originals {
            world.entity_mut(original).remove::<Mesh3d>();
        }
    }
    info!(
        event_kind = "governed_far_batches_projected",
        batches = state.entities.len(),
        "experimental geometry batching, no simplification or HLOD qualification"
    );
    world.insert_resource(state);
    Ok(())
}

fn collect(world: &mut World) -> Result<Groups, String> {
    let source = world
        .resource::<VegetationScatterProjection>()
        .generated
        .clone();
    let locations: HashMap<_, _> = source
        .chunks
        .iter()
        .flat_map(|chunk| {
            chunk
                .instances
                .iter()
                .map(move |i| (i.id, (chunk.coordinate.x, chunk.coordinate.z)))
        })
        .collect();
    let mut groups = Groups::new();
    for (entity, marker, mesh, material, pose, tag, temporal) in world
        .query::<(
            Entity,
            &GovernedTreePrimitive,
            &Mesh3d,
            &MeshMaterial3d<VisualValleyVegetationMaterial>,
            &Transform,
            &MeshTag,
            Option<&Member>,
        )>()
        .iter(world)
    {
        if marker.lod != 2 {
            continue;
        }
        let &(x, z) = locations
            .get(&marker.placement_id)
            .ok_or("far placement missing from source")?;
        let value = world
            .resource::<Assets<VisualValleyVegetationMaterial>>()
            .get(&material.0)
            .ok_or("far material absent")?;
        if value.extension.prototype_parameters.x <= 0.5 {
            return Err("far material is not governed PBR".into());
        }
        groups
            .entry((x, z, material.0.id()))
            .or_default()
            .push(Part {
                entity,
                mesh: mesh.0.clone(),
                material: material.0.clone(),
                pose: *pose,
                tag: temporal.map_or(tag.0, |member| member.original_tag),
                height_m: value.extension.prototype_parameters.y.recip(),
                temporal_slot: temporal
                    .map_or(Ok(0), |member| u16::try_from(member.slot))
                    .map_err(|_| "far temporal slot exceeds exact transport budget")?,
            });
    }
    for parts in groups.values_mut() {
        parts.sort_by_key(|part| part.entity);
    }
    Ok(groups)
}

fn prepare(world: &World, groups: &Groups, origin: [f32; 2]) -> Result<Vec<Prepared>, String> {
    let assets = world.resource::<Assets<Mesh>>();
    let mut result = Vec::new();
    for ((x, z, _), parts) in groups {
        let center = Vec3::new(
            origin[0] + (f32::from(*x) + 0.5) * VEGETATION_CHUNK_SIZE_M,
            0.,
            origin[1] + (f32::from(*z) + 0.5) * VEGETATION_CHUNK_SIZE_M,
        );
        let mut inputs = Vec::new();
        let mut radius = 0.0_f32;
        let mut maximum_scale = 0.0_f32;
        for part in parts {
            let mut pose = part.pose;
            pose.translation -= center;
            radius = radius.max(pose.translation.length());
            maximum_scale = maximum_scale.max(pose.scale.x);
            inputs.push(FarPrimitive {
                mesh: assets.get(&part.mesh).ok_or("far mesh absent")?,
                pose,
                height_m: part.height_m,
                tag: part.tag,
                temporal_slot: part.temporal_slot,
            });
        }
        let mesh = build_far_geometry(&inputs).map_err(|e| e.to_string())?;
        let aabb = mesh.compute_aabb().ok_or("far batch has no bounds")?;
        result.push(Prepared {
            mesh,
            material: parts[0].material.clone(),
            center,
            bounds: aabb,
            radius,
            maximum_scale,
            originals: parts.iter().map(|p| p.entity).collect(),
            temporal_slots: parts
                .iter()
                .filter(|p| p.temporal_slot != 0)
                .map(|p| u32::from(p.temporal_slot))
                .collect(),
        });
    }
    Ok(result)
}
