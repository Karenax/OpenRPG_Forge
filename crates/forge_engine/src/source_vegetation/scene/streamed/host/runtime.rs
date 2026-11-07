use super::{Admission, HostStatus, Resident, Selection, WorldEnvelopes, physical};
use crate::source_lighting::frame::Frame;
use crate::source_lighting::frame::MAX_WIND_SPEED;
use crate::source_vegetation::world::WorldPlantMaterial;
use crate::{PlayerSimulationReady, render_inventory};
use bevy::{prelude::*, render::storage::ShaderBuffer};
use bevy_rapier3d::prelude::{KinematicCharacterController, RapierConfiguration};
use forge_core::{ContentHash, EntityId};
use forge_project::VegetationPresentationProfile;
use forge_world::{
    cell_streaming::{CellKey, CellStreamingPolicy, LoadedCell},
    imported_vegetation::{
        ImportedPopulation,
        streaming::{AdmittedPlantCells, ImportedTerrainSnapshot},
    },
    terrain_hlod::TerrainHlodLayout,
};
use std::collections::BTreeMap;

pub(super) struct Partition {
    source: ContentHash,
    policy: CellStreamingPolicy,
    cells: BTreeMap<CellKey, LoadedCell>,
    distant: TerrainHlodLayout,
}

pub(super) struct SimulationAdmission {
    generation: u64,
    bootstrap_frame: u32,
    token: render_inventory::RenderInventoryToken,
    context: render_inventory::GraphicsContextReceipt,
}

#[derive(Resource)]
pub(super) struct State {
    pub species: Option<super::species::State>,
    pub source_lighting: bool,
    pub surface_generation: u64,
    pub grass: Option<crate::source_vegetation::grass::host::Host>,
    pub render: Resident<WorldPlantMaterial>,
    pub physical: Option<physical::Resident>,
    pub physical_source: Option<super::super::bounds::VerifiedCollisionSource>,
    pub bounds: std::sync::Arc<WorldEnvelopes>,
    pub population: std::sync::Arc<ImportedPopulation>,
    pub snapshot: std::sync::Arc<ImportedTerrainSnapshot>,
    pub bound_sources: Option<super::super::bounds::BoundSources>,
    pub owner: EntityId,
    pub head: ContentHash,
    pub terrain: ContentHash,
    pub presentation: VegetationPresentationProfile,
    pub frame: Frame,
    pub frame_handle: Handle<ShaderBuffer>,
    pub direction: bevy::math::DVec2,
    pub clock: crate::source_wind::vegetation::Clock,
    pub partition: Option<Partition>,
    pub admitted: Option<Admission>,
    pub selected_camera: Option<Vec3>,
    pub pending: Option<Selection>,
    pub published: bool,
    pub frame_ready: bool,
    pub wait_started: Option<f64>,
    pub authoring_target: Option<(ContentHash, ContentHash)>,
    pub simulation_admission: Option<SimulationAdmission>,
}

pub(super) use crate::scene_camera::primary as camera;

pub(super) fn update_frame(world: &mut World) {
    let _profile = crate::cpu_timing::Span::new("plants.update_frame");
    if !world.contains_resource::<State>() {
        return;
    }
    if world.resource::<State>().authoring_target.is_some() {
        world.resource_mut::<HostStatus>().admission.frame_ready = false;
        return;
    }
    let result = world.resource_scope(|world, mut state: Mut<State>| -> Result<(), String> {
        let (camera_entity, position) = camera(world)?;
        if state.source_lighting {
            let shared = crate::source_lighting::surfaces::live::snapshot(world)
                .ok_or("Admitted source surface scene absent")?;
            if shared.generation != state.surface_generation {
                renew_source_bindings(world, &mut state)?;
                state.surface_generation = shared.generation;
            }
            state.frame = shared.frame;
            if shared.handle != state.frame_handle {
                return Err("Source scene changed its shared frame identity".into());
            }
            if !shared.frame_ready {
                state.frame_ready = false;
                world.resource_mut::<HostStatus>().admission.frame_ready = false;
                return Ok(());
            }
            let mut materials = state.render.materials();
            if let Some(grass) = &state.grass {
                materials.extend(grass.materials());
            }
            for handle in materials {
                let assets = world.resource::<Assets<WorldPlantMaterial>>();
                if assets.get(&handle).is_none_or(|material| {
                    !material.source_lighting
                        || material.frame != shared.handle
                        || material.local_lights
                            != crate::source_lighting::local_lights::gpu::BUFFER
                }) {
                    return Err(
                        "Owned source surface lost its lighting specialization or shared frame"
                            .into(),
                    );
                }
            }
        } else {
            let dt = world.resource::<Time<Real>>().delta_secs_f64();
            let speed = MAX_WIND_SPEED * state.presentation.wind_strength;
            let selected = world
                .get_resource::<crate::source_wind::SceneWind>()
                .map(|wind| wind.wind());
            let (next, wind, gust) = crate::source_lighting::frame::motion::advance(
                &state.clock,
                dt,
                selected.map_or(state.direction.to_array(), |wind| wind.direction),
                selected.map_or(f64::from(speed), |wind| wind.speed),
                f64::from(state.presentation.wind_speed_hz),
            )?;
            let handle = state.frame_handle.clone();
            state.frame.update(world, &handle, position, wind, gust)?;
            state.clock = next;
        }
        state.frame_ready = true;
        world.resource_mut::<HostStatus>().admission.frame_ready = true;
        if let Some(grass) = state.grass.as_mut() {
            world.resource_mut::<HostStatus>().grass = grass.refresh(world, camera_entity)?;
        }
        super::species::publish_frame(world, &state, position)?;
        Ok(())
    });
    if let Err(error) = result {
        fail(world, error);
    }
}

fn renew_source_bindings(world: &mut World, state: &mut State) -> Result<(), String> {
    let mut materials = state.render.materials();
    if let Some(grass) = &state.grass {
        materials.extend(grass.materials());
    }
    let updates = materials
        .into_iter()
        .map(|handle| {
            let mut material = world
                .resource::<Assets<WorldPlantMaterial>>()
                .get(&handle)
                .ok_or("Owned source material disappeared")?
                .clone();
            crate::source_lighting::surfaces::live::bind_plant(world, &mut material)?;
            Ok((handle, material))
        })
        .collect::<Result<Vec<_>, String>>()?;
    for (handle, material) in updates {
        world
            .resource_mut::<Assets<WorldPlantMaterial>>()
            .insert(handle.id(), material)
            .map_err(|error| error.to_string())?;
    }
    state.render.renew_bindings(world)?;
    if let Some(grass) = &mut state.grass {
        grass.renew_bindings(world)?;
    }
    Ok(())
}

pub(super) fn early(world: &mut World) {
    let _profile = crate::cpu_timing::Span::new("plants.early_guard");
    let result = if world.contains_resource::<State>() {
        world.resource_scope(|world, mut state: Mut<State>| refresh(world, &mut state, true))
    } else {
        Ok(false)
    };
    finish_guard(world, result, true);
}
pub(super) fn final_gate(world: &mut World) {
    let _profile = crate::cpu_timing::Span::new("plants.final_guard");
    let result = if world.contains_resource::<State>() {
        world.resource_scope(|world, mut state: Mut<State>| refresh(world, &mut state, false))
    } else {
        Ok(false)
    };
    finish_guard(world, result, false);
}

fn finish_guard(world: &mut World, result: Result<bool, String>, count: bool) {
    match result {
        Ok(true) => {
            if let Some(mut state) = world.get_resource_mut::<State>() {
                state.wait_started = None;
            }
        }
        Ok(false) => hold(world, count),
        Err(error) => fail(world, error),
    }
}

#[expect(
    clippy::too_many_lines,
    reason = "keep source validation, publication and readiness in one atomic refresh"
)]
fn refresh(world: &mut World, state: &mut State, publish: bool) -> Result<bool, String> {
    if state.authoring_target.is_some() {
        return Ok(false);
    }
    if world.resource::<HostStatus>().error.is_some() {
        return Ok(false);
    }
    if !super::species::prepare(world, state)? {
        return Ok(false);
    }
    let Some(terrain) = crate::cell_streaming::render_selection(world) else {
        return Ok(false);
    };
    if terrain.source != state.terrain {
        return Err("Imported vegetation terrain source changed".into());
    }
    let Some(distant) = terrain.distant else {
        return Ok(false);
    };
    // Both simulation guards inspect the live partition. Keep its snapshot
    // only when it changes, rather than copying the cell and HLOD maps per guard.
    if state.partition.as_ref().is_none_or(|partition| {
        partition.source != terrain.source
            || &partition.policy != terrain.policy
            || &partition.cells != terrain.cells
            || &partition.distant != distant
    }) {
        let _profile = crate::cpu_timing::Span::new("plants.partition_change");
        let partition = Partition {
            source: terrain.source,
            policy: terrain.policy.clone(),
            cells: terrain.cells.clone(),
            distant: distant.clone(),
        };
        if state
            .bound_sources
            .as_ref()
            .is_some_and(|bound| !bound.matches(&state.bounds, &state.population, &state.snapshot))
        {
            state.bound_sources = None;
        }
        let admission = AdmittedPlantCells {
            policy: terrain.policy,
            cells: terrain.cells,
            distant,
            terrain_hash: terrain.source,
            head_hash: state.head,
        };
        let selected = {
            let _profile = crate::cpu_timing::Span::new("plants.partition.bounds_select");
            if let Some(bound) = &state.bound_sources {
                bound.select(admission)?
            } else {
                state.bounds.select_snapshot_verified(
                    &state.population,
                    &state.snapshot,
                    admission,
                )?
            }
        };
        let new_bound = selected.bind_sources();
        let admitted = {
            let _profile = crate::cpu_timing::Span::new("plants.partition.render_admit");
            state.render.admit_verified(&selected)?
        };
        let prepared = if let Some(physical) = &state.physical {
            let _profile = crate::cpu_timing::Span::new("plants.partition.physics_verify");
            let already_checked = state.physical_source.as_ref().is_some_and(|source| {
                selected.matches_collision_source(source, physical.identity())
            });
            if !already_checked && !physical.matches(selected.selection()) {
                return Err("Imported plant collision selection changed".into());
            }
            None
        } else {
            let _profile = crate::cpu_timing::Span::new("plants.partition.physics_prepare");
            Some(physical::Prepared::from_selection(selected.selection())?)
        };
        if let Some(prepared) = prepared {
            state.physical = Some(physical::Resident::stage(world, state.owner, prepared)?);
        }
        state.physical_source = Some(
            selected.collision_source(
                state
                    .physical
                    .as_ref()
                    .ok_or("Imported plant collisions absent")?
                    .identity(),
            ),
        );
        state.partition = Some(partition);
        state.admitted = Some(admitted);
        state.selected_camera = None;
        world.resource_mut::<HostStatus>().canonical_instances = selected.selection().plants.len();
        drop(selected);
        if let Some(bound) = new_bound {
            state.bound_sources = Some(bound);
        }
    }
    if state.species.as_ref().is_some_and(|s| !s.committed) {
        let (_, position) = camera(world)?;
        if !publish || !super::species::commit(world, state, position)? {
            return Ok(false);
        }
    }
    update_selection(world, state)?;
    if publish && state.pending.is_some() {
        let token = render_inventory::acquire(
            world,
            state.owner,
            state.render.identity(),
            state.render.generation(),
        );
        if token.is_some() || state.render.entity_count() == 0 {
            let plan = state.pending.take().expect("checked pending plant plan");
            state.render.publish(world, plan, token.as_ref())?;
            state.published = true;
        }
    }
    let collisions_ready = {
        let _profile = crate::cpu_timing::Span::new("plants.physics_ready");
        state
            .physical
            .as_ref()
            .is_some_and(|physical| physical.ready(world))
    };
    observe_ready(world, state, collisions_ready)
}

fn observe_ready(
    world: &mut World,
    state: &mut State,
    collisions_ready: bool,
) -> Result<bool, String> {
    let (camera_entity, _) = camera(world)?;
    let grass = state
        .grass
        .as_mut()
        .map(|grass| grass.refresh(world, camera_entity))
        .transpose()?;
    let render_ready = {
        let _profile = crate::cpu_timing::Span::new("plants.render_ready");
        state.published && state.render.ready(world)
    };
    let ready = render_ready
        && collisions_ready
        && state.frame_ready
        && grass.as_ref().is_none_or(|grass| grass.ready);
    let token = render_inventory::acquire(
        world,
        state.owner,
        state.render.identity(),
        state.render.generation(),
    );
    let surfaces = if state.source_lighting {
        let mut observed = crate::source_lighting::surfaces::live::observe(world, token.as_ref())
            .ok_or("Admitted source surface scene absent")?;
        // Keep the subscriber's visible pose in the invariant, not only the producer's.
        observed.motion_matches &= world
            .resource::<crate::source_shadows::casters::graph::animation::State>()
            .matches_motion(state.frame.shadow_motion());
        Some(observed)
    } else {
        None
    };
    let ready = ready
        && surfaces
            .as_ref()
            .is_none_or(|surface| surface.ready && surface.motion_matches);
    let (species, species_resources) =
        super::species::observe_with_resources(world, state, collisions_ready);
    let ready = ready && species.as_ref().is_none_or(|species| species.ready);
    let resources = simulation_resources_ready(world, state, ready, render_ready);
    let simulation_ready = resources
        && collisions_ready
        && state.frame_ready
        && grass.as_ref().is_none_or(|grass| grass.ready)
        && surfaces
            .as_ref()
            .is_none_or(|surface| surface.ready && surface.motion_matches)
        && species_resources;
    let colliders = state
        .physical
        .as_ref()
        .map_or(0, |physical| physical.resident_count(world));
    let canonical_colliders = state
        .physical
        .as_ref()
        .map_or(0, physical::Resident::source_count);
    let mut status = world.resource_mut::<HostStatus>();
    status.surfaces = surfaces.unwrap_or_default();
    status.grass = grass.unwrap_or_default();
    status.species = species;
    status.admission.render_ready = render_ready;
    status.admission.collisions_ready = collisions_ready;
    status.ready = ready;
    status.simulation_ready = simulation_ready;
    status.replacement_primitives = state
        .species
        .as_ref()
        .and_then(|s| s.candidate.as_ref())
        .map_or(0, crate::ImportedTreePresentation::entity_count);
    status.entities = state.render.entity_count() + status.replacement_primitives;
    status.colliders = colliders;
    status.canonical_colliders = canonical_colliders;
    status.generation = state.render.generation();
    status.sequence = state.render.selection_sequence();
    Ok(simulation_ready)
}

fn simulation_resources_ready(
    world: &World,
    state: &mut State,
    strict_ready: bool,
    render_ready: bool,
) -> bool {
    let valid = state.simulation_admission.as_ref().is_some_and(|admitted| {
        admitted.generation == state.render.generation()
            // A ready render observation already audited this projection after
            // the last world mutation in this guard.
            && (render_ready || state.render.audited_projection_intact(world))
            && admitted.token.is_current(
                world,
                state.owner,
                state.render.identity(),
                state.render.generation(),
            )
            && admitted.token.covers_frame(world, admitted.bootstrap_frame)
            && admitted.context.is_current(world)
    });
    if valid {
        return true;
    }
    state.simulation_admission = None;
    if !strict_ready {
        return false;
    }
    // Empty source projections require no fabricated inventory. Their strict
    // readiness remains the authority; populated projections retain a real one.
    if let Some(token) = render_inventory::acquire(
        world,
        state.owner,
        state.render.identity(),
        state.render.generation(),
    ) && let Some(context) = token.graphics_context(world)
        && let Some(bootstrap_frame) = token.completed_frame(world)
    {
        state.simulation_admission = Some(SimulationAdmission {
            generation: state.render.generation(),
            bootstrap_frame,
            token,
            context,
        });
    }
    strict_ready
}

fn update_selection(world: &mut World, state: &mut State) -> Result<(), String> {
    let _profile = crate::cpu_timing::Span::new("plants.selection");
    let (_, position) = camera(world)?;
    if crate::source_vegetation_selection::needs_refresh(
        state.selected_camera.map(|position| position.to_array()),
        position.to_array(),
    ) {
        let admitted = state
            .admitted
            .as_ref()
            .ok_or("Plant source admission is absent")?;
        let plan = {
            let _profile = crate::cpu_timing::Span::new("plants.selection.plan");
            if let Some(candidate) = state.species.as_mut().and_then(|s| s.candidate.as_mut()) {
                let selected = candidate.selection_for_camera(position)?;
                candidate.select(world, state.head, &selected)?;
                state.render.select_excluding(
                    admitted,
                    position,
                    &candidate.replacement_identities(),
                )?
            } else {
                state.render.select(admitted, position)?
            }
        };
        let mut status = world.resource_mut::<HostStatus>();
        status.detail = plan.detail_instances;
        status.grouped = plan.group_instances;
        status.selected_triangles = plan.cpu_selected_triangles
            + state
                .species
                .as_ref()
                .and_then(|s| s.candidate.as_ref())
                .map_or(0, crate::ImportedTreePresentation::selected_triangles);
        state.selected_camera = Some(position);
        if state.render.matches(&plan) {
            // A camera can return to the live selection before a pending plan
            // is published. Cancel that obsolete plan rather than showing it.
            state.pending = None;
            state.published = true;
        } else {
            state.pending = Some(plan);
            state.published = false;
        }
    }
    Ok(())
}

fn hold(world: &mut World, count: bool) {
    world.resource_mut::<PlayerSimulationReady>().0 = false;
    for mut config in world.query::<&mut RapierConfiguration>().iter_mut(world) {
        config.physics_pipeline_active = false;
    }
    for mut controller in world
        .query::<&mut KinematicCharacterController>()
        .iter_mut(world)
    {
        controller.translation = None;
    }
    if let Some(mut runtime) = world.get_resource_mut::<crate::gameplay::GameplayRuntime>() {
        runtime.set_enabled(false);
    }
    let mut status = world.resource_mut::<HostStatus>();
    status.ready = false;
    status.simulation_ready = false;
    if count {
        status.blocked_ticks = status.blocked_ticks.saturating_add(1);
    }
    let seconds = world.resource::<Time<Real>>().elapsed_secs_f64();
    if world.contains_resource::<State>() && world.resource::<HostStatus>().error.is_none() {
        let timed_out = {
            let mut state = world.resource_mut::<State>();
            seconds - *state.wait_started.get_or_insert(seconds) >= 120.0
        };
        if timed_out {
            log_readiness_timeout(world);
            fail(
                world,
                "Imported vegetation did not become ready within 120 seconds".into(),
            );
        }
    }
}

fn log_readiness_timeout(world: &World) {
    let state = world.resource::<State>();
    let candidate = state
        .species
        .as_ref()
        .and_then(|species| species.candidate.as_ref());
    let species = super::species::observe(world, state, false);
    error!(
        event_kind = "imported_plant_readiness_timeout",
        status = ?world.resource::<HostStatus>(),
        species = ?species,
        inventory = ?world.get_resource::<render_inventory::RenderInventoryEvidence>(),
        models_gpu_ready = candidate.is_none_or(|model| model.gpu_ready(world)),
        models_shadows_ready = candidate.is_none_or(|model| model.source_shadows_ready(world)),
        "Imported vegetation readiness barriers at timeout"
    );
}

pub(super) fn fail(world: &mut World, error: String) {
    error!(event_kind="imported_plant_host_failed",reason=%error,"Imported vegetation is held");
    world.resource_mut::<HostStatus>().error = Some(error);
    hold(world, false);
    world.write_message(bevy::app::AppExit::error());
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn shared_offscreen_views_do_not_displace_the_module_camera() {
        let mut world = World::new();
        let primary = world
            .spawn((Camera3d::default(), Transform::from_xyz(4.0, 5.0, 6.0)))
            .id();
        world.spawn((
            Camera3d::default(),
            Transform::from_xyz(f32::NAN, 0.0, 0.0),
            crate::scene_camera::OffscreenSceneCamera,
        ));
        assert_eq!(
            camera(&mut world).unwrap(),
            (primary, Vec3::new(4.0, 5.0, 6.0))
        );
        let second = world.spawn((Camera3d::default(), Transform::IDENTITY)).id();
        assert!(camera(&mut world).is_err());
        world.despawn(second);
        assert_eq!(camera(&mut world).unwrap().0, primary);
    }

    #[test]
    fn final_gate_removes_late_motion_and_cannot_promote_another_hosts_hold() {
        let mut world = World::new();
        world.insert_resource(Time::<Real>::default());
        world.insert_resource(HostStatus::default());
        world.insert_resource(PlayerSimulationReady(true));
        let entity = world
            .spawn((
                KinematicCharacterController {
                    translation: Some(Vec3::X),
                    ..default()
                },
                RapierConfiguration::new(1.0),
            ))
            .id();
        // A late actuator has already submitted motion. No plant state/admission exists.
        final_gate(&mut world);
        assert!(!world.resource::<PlayerSimulationReady>().0);
        assert!(
            !world
                .get::<RapierConfiguration>(entity)
                .unwrap()
                .physics_pipeline_active
        );
        assert_eq!(
            world
                .get::<KinematicCharacterController>(entity)
                .unwrap()
                .translation,
            None
        );
        assert!(!world.resource::<HostStatus>().ready);
        // Even a successful plant check must preserve a hold owned by terrain or gameplay.
        finish_guard(&mut world, Ok(true), false);
        assert!(!world.resource::<PlayerSimulationReady>().0);
        assert!(
            !world
                .get::<RapierConfiguration>(entity)
                .unwrap()
                .physics_pipeline_active
        );
    }
}
