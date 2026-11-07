//! Source receivers and casters for the actual playable scene, on its existing device.
use crate::{
    source_environment::scene::ResidentEnvironment,
    source_shadows::{
        casters::{self, graph, live, scene},
        heightfield,
    },
    sun_cascades::CascadeView,
};
use bevy::{diagnostic::FrameCount, light::NotShadowCaster, prelude::*};
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::sync::{Arc, atomic::Ordering};

pub struct Prepared {
    plants: Option<crate::source_vegetation::scene::shadow::Source>,
    heights: heightfield::PreparedHeights,
    terrain_relief: forge_core::ContentHash,
}

impl Prepared {
    pub fn new(terrain: &SampledTerrainPlan) -> Result<Self, String> {
        Ok(Self {
            plants: None,
            heights: heightfield::PreparedHeights::from_plan(terrain)?,
            terrain_relief: crate::source_terrain::sampling::relief_identity(terrain)?,
        })
    }

    #[must_use]
    pub fn with_plants(mut self, plants: crate::source_vegetation::scene::shadow::Source) -> Self {
        self.plants = Some(plants);
        self
    }

    pub(super) fn stage(
        self,
        world: &mut World,
        camera: Entity,
        motion: graph::animation::Motion,
    ) -> Result<Resident, String> {
        let sun = sun(world)?;
        let hill = self.heights.with_sun(sun.as_vec3())?.stage(world)?;
        let parts = collect(world)?.unwrap_or_default();
        let view = view(world, camera)?;
        let (target, plant_batches) =
            stage_casters(world, self.plants.as_ref(), &parts, view, sun, motion)?;
        Ok(Resident {
            plants: self.plants,
            hill,
            terrain_relief: self.terrain_relief,
            target,
            plant_batches,
            parts,
            tracker: live::Tracker::new(sun)?,
            generation: 1,
            encoded_frame: None,
            mesh_events: default(),
        })
    }
}

pub(super) struct Resident {
    plants: Option<crate::source_vegetation::scene::shadow::Source>,
    pub(super) hill: heightfield::Images,
    pub(super) terrain_relief: forge_core::ContentHash,
    pub(super) target: graph::Target,
    plant_batches: usize,
    parts: Vec<Part>,
    tracker: live::Tracker,
    pub(super) generation: u64,
    encoded_frame: Option<u32>,
    mesh_events: bevy::ecs::message::MessageCursor<AssetEvent<Mesh>>,
}

#[derive(Default, Clone, Debug, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
pub struct Status {
    pub world_receivers: usize,
    pub model_receivers: usize,
    pub terrain_receivers: usize,
    pub active: bool,
    pub ready: bool,
    pub generation: u64,
    pub opaque_casters: usize,
    pub plant_batches: usize,
    pub encoded_frames: u64,
    pub cascade_layers: [u64; 3],
    pub encoded_frame: Option<u32>,
    pub gpu_completed: Option<u32>,
    pub motion_matches: bool,
}

#[derive(Clone)]
struct Part {
    entity: Entity,
    mesh: Handle<Mesh>,
    model: Mat4,
}

fn sun(world: &World) -> Result<bevy::math::DVec3, String> {
    let direction = world
        .get_resource::<ResidentEnvironment>()
        .ok_or("Source surfaces require their admitted environment")?
        .scene_lighting
        .sun_direction
        .truncate()
        .as_dvec3()
        .normalize();
    if !direction.is_finite() {
        return Err("Source surface sun is invalid".into());
    }
    Ok(direction)
}

fn view(world: &World, camera: Entity) -> Result<CascadeView, String> {
    let transform = world
        .get::<Transform>(camera)
        .ok_or("Source shadow camera absent")?;
    let Some(Projection::Perspective(projection)) = world.get::<Projection>(camera) else {
        return Err("Source shadows require the real perspective camera".into());
    };
    crate::scene_camera::source_view(transform, projection)?;
    CascadeView::from_camera_matrix(
        transform.to_matrix(),
        projection.near,
        projection.fov,
        projection.aspect_ratio,
    )
    .map_err(str::to_owned)
}

/// Eligibility follows real scene visibility and opaque material semantics.
/// Alpha-cutout and skinned standard meshes require their appropriate caster;
/// they cannot silently become solid or undeformed shadows.
fn collect(world: &mut World) -> Result<Option<Vec<Part>>, String> {
    let mut candidates: Vec<_> = world
        .query_filtered::<(
            Entity,
            &Mesh3d,
            Option<&MeshMaterial3d<StandardMaterial>>,
            Option<&MeshMaterial3d<crate::VisualValleyTerrainMaterial>>,
            Option<&MeshMaterial3d<crate::source_lighting::surfaces::ModelMaterial>>,
            Option<&MeshMaterial3d<crate::source_lighting::surfaces::TerrainMaterial>>,
            Option<&MeshMaterial3d<crate::source_surface::SourceSurfaceMaterial>>,
        ), Or<(Without<NotShadowCaster>, With<scene::OpaqueCaster>)>>()
        .iter(world)
        .filter(
            |(_, _, standard, terrain, source_model, source_terrain, source_object)| {
                standard.is_some()
                    || terrain.is_some()
                    || source_model.is_some()
                    || source_terrain.is_some()
                    || source_object.is_some()
            },
        )
        .map(
            |(entity, mesh, standard, terrain, source_model, source_terrain, source_object)| {
                (
                    entity,
                    mesh.0.clone(),
                    standard.map(|v| v.0.clone()),
                    terrain.map(|v| v.0.clone()),
                    source_model.map(|v| v.0.clone()),
                    source_terrain.map(|v| v.0.clone()),
                    source_object.map(|v| v.0.clone()),
                )
            },
        )
        .collect();
    candidates.sort_by_key(|part| part.0);
    let mut parts = Vec::with_capacity(candidates.len());
    for (entity, mesh, standard, terrain, source_model, source_terrain, source_object) in candidates
    {
        let visible =
            crate::static_hlod::source_caster_visibility(world, entity).unwrap_or_else(|| {
                world.get::<Visibility>(entity) != Some(&Visibility::Hidden)
                    && world
                        .get::<InheritedVisibility>(entity)
                        .is_none_or(|v| v.get())
            });
        if !visible {
            continue;
        }
        let alpha = if let Some(material) = standard {
            world
                .resource::<Assets<StandardMaterial>>()
                .get(&material)
                .map(|v| v.alpha_mode)
        } else if let Some(material) = terrain {
            world
                .resource::<Assets<crate::VisualValleyTerrainMaterial>>()
                .get(&material)
                .map(|v| v.base.alpha_mode)
        } else if let Some(material) = source_model {
            world
                .resource::<Assets<crate::source_lighting::surfaces::ModelMaterial>>()
                .get(&material)
                .map(|v| v.base.alpha_mode)
        } else if let Some(material) = source_terrain {
            world
                .resource::<Assets<crate::source_lighting::surfaces::TerrainMaterial>>()
                .get(&material)
                .map(|v| v.base.base.alpha_mode)
        } else if let Some(material) = source_object {
            world
                .resource::<Assets<crate::source_surface::SourceSurfaceMaterial>>()
                .get(&material)
                .map(Material::alpha_mode)
        } else {
            None
        };
        let Some(alpha) = alpha else {
            return Ok(None);
        };
        match alpha {
            AlphaMode::Opaque => {}
            AlphaMode::Blend | AlphaMode::Add | AlphaMode::Multiply | AlphaMode::Premultiplied => {
                continue;
            }
            _ => return Err("Source opaque caster refuses an alpha-cutout standard mesh".into()),
        }
        if world
            .get::<bevy::mesh::skinning::SkinnedMesh>(entity)
            .is_some()
        {
            return Err("Source opaque caster refuses an undeformed skinned mesh".into());
        }
        if !world.resource::<Assets<Mesh>>().contains(&mesh) {
            return Ok(None);
        }
        parts.push(Part {
            entity,
            mesh,
            model: scene::world_pose(world, entity)?,
        });
    }
    Ok(Some(parts))
}

fn stage_casters(
    world: &mut World,
    plants: Option<&crate::source_vegetation::scene::shadow::Source>,
    parts: &[Part],
    view: CascadeView,
    sun: bevy::math::DVec3,
    motion: graph::animation::Motion,
) -> Result<(graph::Target, usize), String> {
    let meshes = world.resource::<Assets<Mesh>>();
    let draws = parts
        .iter()
        .map(|part| {
            casters::OpaqueDraw::from_mesh(
                meshes
                    .get(&part.mesh)
                    .ok_or("Scene caster mesh disappeared")?,
                part.model,
            )
        })
        .collect::<Result<_, _>>()?;
    let mut input = scene::prepare(view, sun, draws, None)?;
    if input.settings.enabled > 0.5
        && let Some(plants) = plants
    {
        let mut input_plants = plants.at(motion.main_camera)?;
        input_plants.initial_motion = Some(motion);
        input.plants = Some(input_plants);
    }
    let plant_batches = input
        .plants
        .as_ref()
        .map_or(0, |plants| plants.batches.len());
    graph::stage(world, input).map(|target| (target, plant_batches))
}

impl Resident {
    #[cfg(feature = "physics-rapier")]
    pub(super) fn validate_plant_replacement(
        &self,
        head: forge_core::ContentHash,
        ids: &std::collections::BTreeSet<forge_core::EntityId>,
    ) -> Result<(), String> {
        self.plants
            .as_ref()
            .ok_or("Source plant casters absent")?
            .validate_replacement(head, ids)
    }

    #[cfg(feature = "physics-rapier")]
    pub(super) fn replace_plant_instances(
        &mut self,
        head: forge_core::ContentHash,
        ids: std::collections::BTreeSet<forge_core::EntityId>,
    ) -> Result<(), String> {
        self.plants
            .as_mut()
            .ok_or("Source plant casters absent")?
            .replace_instances(head, ids)?;
        self.tracker.invalidate();
        Ok(())
    }

    /// Renew only this projection's dependencies when its actual caster set changes.
    pub(super) fn refresh(
        &mut self,
        world: &mut World,
        camera: Entity,
        motion: graph::animation::Motion,
    ) -> Result<bool, String> {
        let source = self;
        let Some(parts) = collect(world)? else {
            return Ok(false);
        };
        let mesh_changed =
            world
                .get_resource::<Messages<AssetEvent<Mesh>>>()
                .is_some_and(|events| {
                    let mut changed = false;
                    for event in source.mesh_events.read(events) {
                        changed |= matches!(event, AssetEvent::Modified { id } | AssetEvent::Removed { id }
                            if source.parts.iter().any(|part| part.mesh.id() == *id));
                    }
                    changed
                });
        let changed = mesh_changed
            || source.parts.len() != parts.len()
            || source
                .parts
                .iter()
                .zip(&parts)
                .any(|(a, b)| a.entity != b.entity || a.mesh != b.mesh);
        if changed {
            let (target, plant_batches) = stage_casters(
                world,
                source.plants.as_ref(),
                &parts,
                view(world, camera)?,
                sun(world)?,
                motion,
            )?;
            source.target = target;
            source.plant_batches = plant_batches;
            source.parts = parts;
            source.tracker.invalidate();
            source.generation = source
                .generation
                .checked_add(1)
                .ok_or("Surface generation overflow")?;
            source.encoded_frame = None;
        } else {
            source.parts = parts;
        }
        if source.target.ready.load(Ordering::Acquire) && source.hill.ready.load(Ordering::Acquire)
        {
            source
                .encoded_frame
                .get_or_insert(world.resource::<FrameCount>().0);
            Ok(world.resource::<graph::animation::State>().can_submit())
        } else {
            Ok(false)
        }
    }

    pub(super) fn animate(
        &mut self,
        world: &mut World,
        camera: Entity,
        frame: &crate::source_lighting::frame::Frame,
        frame_index: u32,
    ) -> Result<(), String> {
        let motion = frame.shadow_motion();
        self.tracker.set_sun(sun(world)?)?;
        let mut shadow = self.tracker.next(view(world, camera)?, frame_index)?;
        if shadow.settings.enabled > 0.5 {
            shadow.plants = self
                .plants
                .as_mut()
                .map(|plants| plants.select_resident(motion.main_camera))
                .transpose()?
                .unwrap_or_default();
            shadow.opaque_models = Some(self.parts.iter().map(|part| part.model).collect());
        }
        if let Err(error) = graph::animation::submit_frame(world, motion, shadow) {
            self.tracker.invalidate();
            return Err(error);
        }
        Ok(())
    }

    pub(super) fn observe(
        &self,
        world: &World,
        token: Option<&crate::render_inventory::RenderInventoryToken>,
        motion: graph::animation::Motion,
    ) -> Status {
        let current = world
            .get_resource::<graph::Target>()
            .is_some_and(|target| Arc::ptr_eq(&target.ready, &self.target.ready));
        let completed = token.and_then(|token| token.completed_frame(world));
        let statistics = world.resource::<graph::animation::State>().statistics();
        let receivers = world.get_resource::<crate::source_lighting::surfaces::host::Status>();
        Status {
            world_receivers: receivers
                .filter(|status| status.pending == 0)
                .map_or(0, |status| status.models + status.terrain),
            model_receivers: receivers.map_or(0, |status| status.models),
            terrain_receivers: receivers.map_or(0, |status| status.terrain),
            active: current,
            generation: self.generation,
            opaque_casters: self.parts.len(),
            plant_batches: self.plant_batches,
            encoded_frames: statistics.submissions,
            cascade_layers: statistics.layers,
            encoded_frame: self.encoded_frame,
            gpu_completed: completed,
            ready: current
                && receivers.is_some_and(|status| status.pending == 0)
                && self.encoded_frame.is_some_and(|frame| {
                    token.is_some_and(|token| token.covers_frame(world, frame))
                })
                && statistics.submissions > 0,
            motion_matches: world
                .resource::<graph::animation::State>()
                .matches_motion(motion),
        }
    }
}

#[cfg(test)]
mod object_tests {
    use super::*;
    use crate::source_surface::SourceSurfaceMaterial;

    #[test]
    fn source_object_casters_require_explicit_coverage_and_respect_transparency_and_visibility() {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<SourceSurfaceMaterial>>();
        let mesh = world.resource_mut::<Assets<Mesh>>().add(Cuboid::default());
        let material = world
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .add(crate::source_surface::tests::lighting());
        let entity = world
            .spawn((
                Mesh3d(mesh),
                MeshMaterial3d(material.clone()),
                Transform::IDENTITY,
                GlobalTransform::IDENTITY,
                Visibility::Visible,
                NotShadowCaster,
            ))
            .id();
        assert!(collect(&mut world).unwrap().unwrap().is_empty());
        world.entity_mut(entity).insert(scene::OpaqueCaster);
        world
            .entity_mut(entity)
            .insert(InheritedVisibility::VISIBLE);
        assert_eq!(collect(&mut world).unwrap().unwrap().len(), 1);
        world
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .get_mut(&material)
            .unwrap()
            .kind = 6;
        assert!(collect(&mut world).unwrap().unwrap().is_empty());
        world
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .get_mut(&material)
            .unwrap()
            .kind = 0;
        world.entity_mut(entity).insert(Visibility::Hidden);
        assert!(collect(&mut world).unwrap().unwrap().is_empty());
    }
}
