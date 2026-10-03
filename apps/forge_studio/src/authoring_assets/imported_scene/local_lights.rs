//! Derived emitters follow the common placement document; no second authoring state.
use super::super::{imported_buildings, placements};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_core::{AssetId, ContentHash, EntityId};
use forge_engine::{
    source_local_lights::{collection::Collection, projection::ResidentSourceLights},
    tidewater_assets::local_lights::BuildingLightBindings,
};
use forge_project::zone_authoring::placements::ManualPlacement;
use std::{cell::RefCell, collections::BTreeMap};

type Receipt = (u64, forge_core::ContentHash, Vec<ManualPlacement>);
thread_local! {
    static READY: RefCell<Result<Option<Receipt>, String>> = const { RefCell::new(Ok(None)) };
}

#[derive(Resource, Default)]
struct State {
    receipt: Option<Receipt>,
    residents: BTreeMap<EntityId, ResidentSourceLights>,
    package: Option<ContentHash>,
    ranges: BTreeMap<EntityId, (AssetId, u64)>,
    next_copy: u64,
}

pub(super) fn install(app: &mut App) {
    READY.with(|slot| *slot.borrow_mut() = Ok(None));
    if let Err(error) = forge_engine::tidewater_material::local_lights::gpu::register(app) {
        READY.with(|slot| *slot.borrow_mut() = Err(error));
        return;
    }
    app.init_resource::<State>()
        .init_resource::<Collection>()
        .add_systems(PostUpdate, reconcile.after(super::update))
        .add_systems(
            PostUpdate,
            collect
                .after(reconcile)
                .after(bevy::transform::TransformSystems::Propagate)
                .after(bevy::camera::CameraUpdateSystems),
        );
}

fn collect(world: &mut World) {
    // A pending or refused projection must never retain an old camera packet.
    world.resource_mut::<Collection>().invalidate();
    let state = world.resource::<State>();
    if state.receipt.is_none()
        || !state
            .residents
            .values()
            .all(|resident| resident.is_synchronized(world))
        || ready() != Ok(true)
    {
        return;
    }
    let mut orders = state
        .residents
        .values()
        .flat_map(ResidentSourceLights::registration_orders)
        .copied()
        .collect::<Vec<_>>();
    orders.sort_unstable();
    let mut query = world.query_filtered::<(Entity, &Camera), (
        With<Camera3d>,
        Without<forge_engine::tidewater_refraction::CaptureCamera>,
        Without<forge_engine::tidewater_sky::preview::PreviewCamera>,
    )>();
    let mut active = query.iter(world).filter(|(_, camera)| camera.is_active);
    let Some((camera, _)) = active.next() else {
        return;
    };
    if active.next().is_some() {
        return;
    }
    // Read the same night value as the live sky shader, not a second solar clock.
    let mut query = world.query::<&MeshMaterial3d<forge_engine::tidewater_sky::SkyMaterial>>();
    let mut skies = query.iter(world);
    let Some(sky) = skies.next() else {
        return;
    };
    if skies.next().is_some() {
        return;
    }
    let Some(material) = world
        .get_resource::<Assets<forge_engine::tidewater_sky::SkyMaterial>>()
        .and_then(|materials| materials.get(&sky.0))
    else {
        return;
    };
    let night = f64::from(material.settings.stars_time_night.z);
    let Some(time) = world.get_resource::<Time>() else {
        return;
    };
    let settings = forge_engine::source_local_lights::Settings {
        delta_seconds: time.delta_secs_f64(),
        night,
        enabled: true,
        strength: 1.0,
        flashlight: forge_engine::source_local_lights::Flashlight::default(),
    };
    world.resource_scope(|world, mut collection: Mut<Collection>| {
        // collect invalidates on every refusal, including foreign or missing lamps.
        // Readiness of a queue write remains separate from geometric readiness.
        let _ = collection.collect(world, camera, settings, &orders);
    });
}

pub(super) fn ready() -> Result<bool, String> {
    // Candidates without a light package retain their existing geometry path.
    let Some(lights) = imported_buildings::local_lights_package() else {
        return Ok(true);
    };
    let desired = placements::desired_imports();
    READY.with(|slot| match &*slot.borrow() {
        Err(error) => Err(error.clone()),
        Ok(None) => Ok(false),
        Ok(Some((generation, pin, sources))) => Ok(*generation
            == imported_buildings::published_generation()
            && *pin == lights.receipt_hash()
            && *sources == desired),
    })
}

fn retire(world: &mut World, state: &mut State) {
    world.resource_mut::<Collection>().invalidate();
    for resident in state.residents.values_mut() {
        resident.retire(world);
    }
    state.residents.clear();
    state.receipt = None;
}

fn reconcile(world: &mut World) {
    world.resource_scope(|world, mut state: Mut<State>| {
        if !crate::authoring_viewport::can_project_sources() {
            retire(world, &mut state);
            READY.with(|slot| *slot.borrow_mut() = Ok(None));
            return;
        }
        let Some(package) = world
            .get_resource::<imported_buildings::VerifiedLocalLights>()
            .map(|verified| verified.0.clone())
        else {
            retire(world, &mut state);
            READY.with(|slot| *slot.borrow_mut() = Ok(None));
            return;
        };
        let receipt = (
            imported_buildings::published_generation(),
            package.receipt_hash(),
            placements::desired_imports(),
        );
        if state.package != Some(package.receipt_hash()) {
            state.package = Some(package.receipt_hash());
            state.ranges.clear();
            state.next_copy = 1;
        }
        let synchronized = state
            .residents
            .values()
            .all(|resident| resident.is_synchronized(world));
        if state.receipt.as_ref() == Some(&receipt) && synchronized {
            READY.with(|slot| *slot.borrow_mut() = Ok(Some(receipt)));
            return;
        }
        // Invalidate the old camera publication before removing any emitters.
        // Source copies keep acquired phases; only their registration ranges differ.
        retire(world, &mut state);
        READY.with(|slot| *slot.borrow_mut() = Ok(None));
        if world
            .get_resource::<forge_engine::tidewater_assets::scene::ResidentScene>()
            .is_none_or(|scene| !scene.is_synchronized(world))
        {
            // Geometry may have been rebuilt after this frame's propagation.
            // This is pending readiness, rather than a rejected source package.
            return;
        }
        let result = (|| -> Result<(), String> {
            let buildings = imported_buildings::building_package()
                .ok_or("Building geometry package is unavailable for local lights")?;
            let bindings = BuildingLightBindings::from_packages(&buildings, &package)?;
            let stride = package
                .descriptor()
                .document()
                .lights
                .iter()
                .map(|light| light.registration_order)
                .max()
                .unwrap_or(0)
                .checked_add(1)
                .ok_or("Light registration stride overflows")?;
            let mut sources = receipt.2.clone();
            sources.sort_by_key(|source| source.id);
            if sources.windows(2).any(|pair| pair[0].id == pair[1].id) {
                return Err("Duplicate canonical building placement identity".into());
            }
            // Prepare every asset binding before creating an emitter. A failure
            // clears this adapter's projections while preserving placement roots.
            state.ranges.retain(|id, (asset, _)| {
                sources
                    .iter()
                    .any(|source| source.id == *id && source.asset_id == *asset)
            });
            let plans = sources
                .iter()
                .map(|source| {
                    let base = registration_base(&mut state, source, stride)?;
                    bindings
                        .prepare_optional(&package, source, base)
                        .map(|plan| (source.id, plan))
                })
                .collect::<Result<Vec<_>, String>>()?;
            for (id, plan) in plans {
                if let Some(plan) = plan {
                    state.residents.insert(id, plan.stage(world)?);
                }
            }
            state.receipt = Some(receipt);
            Ok(())
        })();
        if let Err(error) = result {
            retire(world, &mut state);
            READY.with(|slot| *slot.borrow_mut() = Err(error));
        }
        // New children require the next normal transform propagation before
        // readiness. No synthetic GlobalTransform or immediate collection.
    });
}

fn registration_base(
    state: &mut State,
    source: &ManualPlacement,
    stride: u64,
) -> Result<u64, String> {
    let base = if let Some((_, base)) = state.ranges.get(&source.id) {
        *base
    } else {
        // Distinct source assets already own disjoint source orders.
        // Offset only copies; preserve assigned ranges across retouches.
        let base = if state
            .ranges
            .values()
            .any(|(asset, _)| *asset == source.asset_id)
        {
            let base = state
                .next_copy
                .checked_mul(stride)
                .ok_or("Light registration range overflows")?;
            state.next_copy = state
                .next_copy
                .checked_add(1)
                .ok_or("Light copy counter overflows")?;
            base
        } else {
            0
        };
        state.ranges.insert(source.id, (source.asset_id, base));
        base
    };
    Ok(base)
}
