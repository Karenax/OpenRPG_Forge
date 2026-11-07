//! Live scene ownership for admitted fish profiles, shared by Studio and game.
use super::scene::{PreparedScene, Scene};
use crate::{
    source_fish::{material::FishMaterial, shadow::cache::MaskedFishCache},
    source_lighting::surfaces::live::{self as surfaces, SurfaceHostSet},
    source_shadows::casters::{graph, masked::merge},
    source_surface::{scene, underwater},
};
use bevy::{
    camera::primitives::{Frustum, Sphere},
    prelude::*,
};
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};

#[cfg(test)]
mod tests;

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct FishHostSet;

#[derive(Resource, Default)]
pub struct ClockPolicy {
    pub frozen: bool,
}

#[derive(Resource, Clone, Debug, Default)]
pub struct Status {
    pub placements: usize,
    pub synchronized: bool,
    pub error: Option<String>,
}

#[derive(Clone, Copy)]
enum WaterMode {
    Absent,
    Required,
}

#[derive(Resource)]
struct Pending {
    scene: Option<PreparedScene>,
    water_mode: WaterMode,
}

#[derive(Resource)]
struct State {
    scene: Option<Scene>,
    placements: Vec<ManualPlacement>,
    water_mode: WaterMode,
    water: scene::WaterBindings,
    shadow: MaskedFishCache,
}

struct ShadowOwner;

/// Install a fish owner in an existing live environment. `water` explicitly
/// states whether the admitted scene supplies live water lighting inputs.
///
/// # Errors
/// Refuses missing scene prerequisites, conflicting water mode or another owner.
pub fn install(app: &mut App, initial: Option<PreparedScene>, water: bool) -> Result<(), String> {
    if app.world().contains_resource::<Pending>() || app.world().contains_resource::<State>() {
        return Err("Fish scene is already owned".into());
    }
    if !surfaces::installed(app.world()) {
        return Err("Fish require the admitted live environment and surface host".into());
    }
    let water_mode = if water {
        WaterMode::Required
    } else {
        WaterMode::Absent
    };
    validate_water(app.world(), water_mode)?;
    if !app.is_plugin_added::<MaterialPlugin<FishMaterial>>() {
        app.add_plugins(MaterialPlugin::<FishMaterial>::default());
    }
    app.insert_resource(Pending {
        scene: initial,
        water_mode,
    })
    .init_resource::<Status>()
    .init_resource::<ClockPolicy>()
    .add_systems(PostStartup, start.in_set(FishHostSet).after(SurfaceHostSet))
    .add_systems(
        PostUpdate,
        update
            .in_set(FishHostSet)
            .after(SurfaceHostSet)
            .after(bevy::transform::TransformSystems::Propagate),
    )
    .add_systems(PostUpdate, emit.after(FishHostSet));
    Ok(())
}

fn validate_water(world: &World, mode: WaterMode) -> Result<(), String> {
    let present = world.contains_resource::<underwater::CausticMaterialInputs>();
    match (mode, present) {
        (WaterMode::Absent, true) => Err("Fish cannot ignore installed water inputs".into()),
        (WaterMode::Required, false) => Err("Fish require the admitted water inputs".into()),
        _ => Ok(()),
    }
}

fn current_water(world: &World, state: &State) -> Result<scene::WaterBindings, String> {
    validate_water(world, state.water_mode)?;
    match state.water_mode {
        WaterMode::Absent => Ok(state.water.clone()),
        WaterMode::Required => scene::required_water(world),
    }
}

fn clock(world: &World) -> (f32, u32) {
    if world.resource::<ClockPolicy>().frozen {
        (0.0, 0)
    } else {
        (
            world.resource::<Time>().elapsed_secs(),
            world.resource::<bevy::diagnostic::FrameCount>().0,
        )
    }
}

fn start(world: &mut World) {
    let Some(pending) = world.remove_resource::<Pending>() else {
        return;
    };
    let result = (|| {
        validate_water(world, pending.water_mode)?;
        let water = match pending.water_mode {
            WaterMode::Absent => scene::absent_water(world)?,
            WaterMode::Required => scene::required_water(world)?,
        };
        let placements = pending
            .scene
            .as_ref()
            .map_or_else(Vec::new, |scene| scene.placements().to_vec());
        let resident = if let Some(prepared) = pending.scene {
            let lighting = scene::lighting_material(world, &water)?;
            Some(prepared.stage(world, &lighting, clock(world).0)?)
        } else {
            None
        };
        world.insert_resource(State {
            scene: resident,
            placements,
            water_mode: pending.water_mode,
            water,
            shadow: MaskedFishCache::default(),
        });
        Ok::<_, String>(())
    })();
    if let Err(error) = result {
        world.resource_mut::<Status>().error = Some(error);
    }
}

/// Determine whether a new selected profile must be prepared for a change.
#[must_use]
pub fn needs_rebuild(before: &[ManualPlacement], after: &[ManualPlacement]) -> bool {
    before.len() != after.len()
        || before
            .iter()
            .zip(after)
            .any(|(a, b)| (a.id, a.asset_id, a.asset_hash) != (b.id, b.asset_id, b.asset_hash))
}

/// Current authored roots for selection and editor adapters.
#[must_use]
pub fn authored_roots(world: &World) -> Vec<(Entity, forge_core::EntityId, forge_core::Transform)> {
    world
        .get_resource::<State>()
        .and_then(|state| state.scene.as_ref())
        .map_or_else(Vec::new, |scene| scene.resident().authored_roots().to_vec())
}

/// Confirm that a draw belongs to the currently installed fish scene.
#[must_use]
pub fn owns(world: &World, entity: Entity) -> bool {
    world
        .get_resource::<State>()
        .and_then(|state| state.scene.as_ref())
        .is_some_and(|scene| scene.resident().owns_part(world, entity))
}

/// Publish an accepted edit against the exact previous placement set. Pose-only
/// changes reuse meshes; inventory changes require a prepared replacement.
///
/// # Errors
/// Rejects stale edits, invalid placements, missing preparation or resources.
pub fn replace(
    world: &mut World,
    expected: &[ManualPlacement],
    desired: &[ManualPlacement],
    prepared: Option<PreparedScene>,
) -> Result<(), String> {
    PlacementChanges::between(expected, desired)?;
    if world.resource::<Status>().error.is_some() {
        return Err("Fish scene is in an error state".into());
    }
    if !world.contains_resource::<State>() {
        return Err("Fish scene is still starting".into());
    }
    world.resource_scope(|world, mut state: Mut<State>| {
        if state.placements != expected {
            return Err("Fish placements changed before replacement".into());
        }
        if desired.is_empty() {
            if let Some(scene) = state.scene.take() {
                scene.clear(world);
            }
            state.shadow.clear();
        } else if !needs_rebuild(expected, desired) {
            state
                .scene
                .as_mut()
                .ok_or("Fish scene absent")?
                .resident_mut()
                .move_authored(world, desired)?;
        } else {
            let prepared = prepared.ok_or("Prepared fish replacement absent")?;
            if prepared.placements() != desired {
                return Err("Prepared fish placements differ".into());
            }
            let water = current_water(world, &state)?;
            let lighting = scene::lighting_material(world, &water)?;
            let next = prepared.stage(world, &lighting, clock(world).0)?;
            if let Some(previous) = state.scene.replace(next) {
                previous.clear(world);
            }
            state.shadow.clear();
        }
        state.placements = desired.to_vec();
        let mut status = world.resource_mut::<Status>();
        status.placements = desired.len();
        status.synchronized = false;
        Ok(())
    })
}

fn update(world: &mut World) {
    if !world.contains_resource::<State>() || world.resource::<Status>().error.is_some() {
        return;
    }
    let result = world.resource_scope(|world, mut state: Mut<State>| -> Result<bool, String> {
        let water = current_water(world, &state)?;
        if state.scene.is_none() {
            merge::publish::<ShadowOwner>(world, None)?;
            return Ok(state.placements.is_empty());
        }
        let cameras: Vec<_> = world
            .query_filtered::<(&Camera, &GlobalTransform, &Frustum), crate::source_sky::preview::SceneCameraFilter>()
            .iter(world)
            .filter(|(camera, _, _)| camera.is_active)
            .map(|(_, transform, frustum)| (transform.translation(), *frustum))
            .collect();
        if cameras.len() != 1 {
            merge::publish::<ShadowOwner>(world, None)?;
            return Ok(false);
        }
        let (camera, frustum) = cameras[0];
        let (time, frame) = clock(world);
        let lighting = scene::lighting_material(world, &water)?;
        let state = &mut *state;
        let fish = state.scene.as_mut().ok_or("Fish scene absent")?;
        fish.resident_mut().update_view(
            world,
            camera,
            |center, radius| frustum.intersects_sphere(&Sphere { center: center.into(), radius }, true),
            time,
            frame,
        )?;
        fish.resident().update_scene_receivers(world, &lighting)?;
        let ready = fish.resident().authored_roots().iter().all(|(root, id, _)| {
            state.placements.iter().find(|source| source.id == *id).is_some_and(|source| {
                world.get::<crate::ManualPlacementProjection>(*root).is_some_and(|value| value.0 == *source)
                    && world.get::<GlobalTransform>(*root).is_some_and(|global| {
                        world.get::<Transform>(*root).is_some_and(|local| global.to_matrix().abs_diff_eq(local.to_matrix(), 1e-5))
                    })
            })
        });
        let fish = state.scene.as_ref().ok_or("Fish scene absent")?;
        let target = world.resource::<graph::Target>();
        let inventory = if ready && target.settings.enabled >= 0.5 && !fish.resident().is_dormant() {
            fish.resident().shadow_inventory(world)?
        } else {
            Vec::new()
        };
        let shader = fish.shadow_shader();
        let target = target.image.clone();
        let shadow = state.shadow.frame(world, inventory, target, shader, time)?;
        merge::publish::<ShadowOwner>(world, shadow)?;
        Ok(ready)
    });
    match result {
        Ok(ready) => {
            let count = world.resource::<State>().placements.len();
            let mut status = world.resource_mut::<Status>();
            status.placements = count;
            status.synchronized = ready;
        }
        Err(error) => {
            let _ = merge::publish::<ShadowOwner>(world, None);
            let mut status = world.resource_mut::<Status>();
            status.synchronized = false;
            status.error = Some(error);
        }
    }
}

fn emit(world: &mut World) {
    if let Err(error) = merge::emit(world) {
        let mut status = world.resource_mut::<Status>();
        status.synchronized = false;
        status.error = Some(error);
    }
}
