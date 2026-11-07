//! Common immutable-scene producer for saved placement companions.
//! Expected inventory comes from admitted documents, never from harvested emitters.
use super::{PreparedPlacementLights, ResidentPlacementLights};
use crate::{ManualPlacementProjection, source_lighting::local_lights};
use bevy::prelude::*;
use forge_assets::source_local_lights::SourceLocalLightsPackage;
use forge_core::{ContentHash, EntityId};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{collections::BTreeMap, sync::Arc};

pub struct PreparedHost {
    identity: ContentHash,
    pending: BTreeMap<EntityId, PreparedPlacementLights>,
    orders: Vec<u64>,
}

#[derive(Resource)]
struct State {
    pending: BTreeMap<EntityId, PreparedPlacementLights>,
    residents: BTreeMap<EntityId, ResidentPlacementLights>,
    orders: Vec<u64>,
    flashlight_on: bool,
    authoring_hold: bool,
}

/// CPU scene preparation only. Queue writes and GPU execution have separate evidence.
#[derive(Resource, Default, Clone, Debug, PartialEq, Eq)]
pub struct Status {
    pub camera: Option<Entity>,
    pub identity: Option<ContentHash>,
    pub expected_placements: usize,
    pub staged_placements: usize,
    pub expected_emitters: usize,
    pub ready: bool,
    pub error: Option<String>,
}

#[derive(Resource, Clone, Copy)]
pub struct Controls {
    pub enabled: bool,
    pub strength: f64,
    pub flashlight_on: bool,
    pub flashlight: local_lights::Flashlight,
}
impl Default for Controls {
    fn default() -> Self {
        Self {
            enabled: true,
            strength: 1.0,
            flashlight_on: false,
            flashlight: local_lights::Flashlight::default(),
        }
    }
}

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct PlacementLightHostSet;

impl PreparedHost {
    pub fn from_packages(
        placements: &[ManualPlacement],
        packages: &BTreeMap<ContentHash, Arc<SourceLocalLightsPackage>>,
    ) -> Result<Self, String> {
        Self::prepare(placements, |placement, base| {
            let reference = placement
                .local_lights
                .as_ref()
                .ok_or("Saved source-light reference disappeared")?;
            let package = packages
                .get(&reference.manifest_sha256)
                .ok_or("Saved placement source-light package is absent")?;
            PreparedPlacementLights::from_package(package, placement, base)
        })
    }

    fn prepare(
        placements: &[ManualPlacement],
        mut prepare: impl FnMut(&ManualPlacement, u64) -> Result<PreparedPlacementLights, String>,
    ) -> Result<Self, String> {
        let mut selected: Vec<_> = placements
            .iter()
            .filter(|placement| placement.local_lights.is_some())
            .collect();
        selected.sort_by_key(|placement| placement.id);
        if selected.windows(2).any(|pair| pair[0].id == pair[1].id) {
            return Err("Duplicate source-light placement identity".into());
        }
        let identity = forge_core::canonical_json_sha256(&selected).map_err(|e| e.to_string())?;
        let mut pending = BTreeMap::new();
        let mut orders = Vec::new();
        let mut base = 0;
        for placement in selected {
            let prepared = prepare(placement, base)?;
            let source_orders = prepared.lights.registration_orders();
            base = source_orders
                .iter()
                .max()
                .ok_or("Saved light placement has no selected emitters")?
                .checked_add(1)
                .ok_or("Source-light scene registration range overflows")?;
            orders.extend_from_slice(source_orders);
            pending.insert(placement.id, prepared);
        }
        orders.sort_unstable();
        Ok(Self {
            identity,
            pending,
            orders,
        })
    }

    #[must_use]
    pub fn expected(&self) -> Status {
        Status {
            identity: Some(self.identity),
            expected_placements: self.pending.len(),
            expected_emitters: self.orders.len(),
            ..default()
        }
    }

    #[must_use]
    pub fn is_empty(&self) -> bool {
        self.pending.is_empty()
    }

    pub fn install(self, app: &mut App) -> Result<(), String> {
        if app.world().contains_resource::<State>() {
            return Err("Saved source lights require their exclusive scene producer".into());
        }
        local_lights::gpu::register(app)?;
        local_lights::collection::claim_producer(app.world_mut())?;
        self.install_scene(app);
        Ok(())
    }

    /// Rebuild accepted placement companions under the existing sole producer.
    /// Retire lights before their model roots are replaced by the object host.
    pub fn replace(self, world: &mut World, expected: ContentHash) -> Result<(), String> {
        if world
            .get_resource::<Status>()
            .and_then(|status| status.identity)
            != Some(expected)
            || !world.contains_resource::<State>()
        {
            return Err("Source light scene changed before authoring replacement".into());
        }
        let status = self.expected();
        world
            .resource_mut::<local_lights::collection::Collection>()
            .invalidate();
        world.resource_mut::<Status>().ready = false;
        let retired = world.resource_scope(|world, mut state: Mut<State>| -> Result<(), String> {
            for resident in state.residents.values_mut() {
                resident.retire(world)?;
            }
            state.residents.clear();
            state.pending = self.pending;
            state.orders = self.orders;
            state.authoring_hold = false;
            Ok(())
        });
        if let Err(error) = retired {
            world.resource_mut::<Status>().error = Some(error.clone());
            return Err(error);
        }
        world.insert_resource(status);
        Ok(())
    }

    fn install_scene(self, app: &mut App) {
        let status = self.expected();
        app.insert_resource(State {
            pending: self.pending,
            residents: BTreeMap::new(),
            orders: self.orders,
            flashlight_on: false,
            authoring_hold: false,
        })
        .insert_resource(status)
        .init_resource::<Controls>()
        .init_resource::<local_lights::collection::Collection>()
        .add_systems(
            PostUpdate,
            update
                .in_set(PlacementLightHostSet)
                .after(bevy::transform::TransformSystems::Propagate)
                .after(bevy::camera::CameraUpdateSystems)
                .after(crate::source_environment::host::EnvironmentHostSet),
        );
    }
}

impl State {
    fn reconcile(&mut self, world: &mut World) -> Result<bool, String> {
        let mut roots: BTreeMap<EntityId, Vec<Entity>> = BTreeMap::new();
        for (entity, placement) in world
            .query::<(Entity, &ManualPlacementProjection)>()
            .iter(world)
        {
            if self.pending.contains_key(&placement.0.id)
                || self.residents.contains_key(&placement.0.id)
            {
                roots.entry(placement.0.id).or_default().push(entity);
            }
        }
        let mut staged = false;
        for (id, roots) in roots {
            if roots.len() != 1 {
                return Err("Saved source lights have ambiguous canonical roots".into());
            }
            let Some(prepared) = self.pending.remove(&id) else {
                continue;
            };
            self.residents.insert(id, prepared.stage(world, roots[0])?);
            staged = true;
        }
        world.resource_mut::<Status>().staged_placements = self.residents.len();
        // New children must pass through the ordinary transform schedule first.
        if staged || !self.pending.is_empty() {
            return Ok(false);
        }
        if !self.residents.values().all(|r| r.is_synchronized(world)) {
            return Err(
                "Saved source-light projection no longer matches its admitted scene".into(),
            );
        }
        Ok(true)
    }
}

/// Hold the existing sole producer before an editor replaces model roots.
pub fn suspend_authoring(world: &mut World, expected: ContentHash) -> Result<(), String> {
    if world
        .get_resource::<Status>()
        .and_then(|status| status.identity)
        != Some(expected)
        || !world.contains_resource::<State>()
    {
        return Err("Source light authoring transition is stale or foreign".into());
    }
    world.resource_mut::<State>().authoring_hold = true;
    world.resource_mut::<Status>().ready = false;
    world
        .resource_mut::<local_lights::collection::Collection>()
        .invalidate();
    Ok(())
}

fn update(world: &mut World) {
    world
        .resource_mut::<local_lights::collection::Collection>()
        .invalidate();
    world.resource_mut::<Status>().ready = false;
    world.resource_mut::<Status>().camera = None;
    if world.resource::<State>().authoring_hold {
        return;
    }
    if world.resource::<Status>().error.is_some() {
        return;
    }
    let controls = *world.resource::<Controls>();
    let result = world.resource_scope(|world, mut state: Mut<State>| -> Result<bool, String> {
        if !state.reconcile(world)? {
            return Ok(false);
        }
        let Some(frame) = local_lights::frame::from_scene(
            world,
            controls.enabled,
            controls.strength,
            controls.flashlight,
        )?
        else {
            return Ok(false);
        };
        world.resource_scope(
            |world, mut collection: Mut<local_lights::collection::Collection>| {
                if controls.flashlight_on != state.flashlight_on {
                    collection.toggle_flashlight(controls.flashlight_on);
                    state.flashlight_on = controls.flashlight_on;
                }
                collection
                    .collect(world, frame.camera, frame.settings, &state.orders)
                    .map_err(|error| error.to_string())
            },
        )?;
        world.resource_mut::<Status>().camera = Some(frame.camera);
        Ok(true)
    });
    match result {
        Ok(ready) => world.resource_mut::<Status>().ready = ready,
        Err(error) => {
            error!(%error, "Saved source-light scene preparation failed");
            world.resource_mut::<Status>().error = Some(error);
        }
    }
}

#[cfg(test)]
mod tests;
