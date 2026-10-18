//! Imported playtest immersion and transient comparison hold; no authored mutations.
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_tidewater::{tidewater_ocean, tidewater_ocean_optics, tidewater_underwater};

#[derive(Resource, Default)]
struct ComparisonHold(bool);

pub(super) fn install(app: &mut App) -> Result<(), String> {
    let package = crate::authoring_assets::imported_buildings::water_surface_package()
        .ok_or("Effets sous-marins : paquet de surface vérifié absent")?;
    tidewater_ocean_optics::register_underwater(app, &package)?;
    app.init_resource::<ComparisonHold>()
        .add_systems(PreUpdate, apply_hold)
        .add_systems(Update, apply_graphics);
    Ok(())
}

pub(super) fn held(world: &World) -> bool {
    world
        .get_resource::<ComparisonHold>()
        .is_some_and(|hold| hold.0)
}

pub(super) fn camera_signature(world: &World) -> Option<serde_json::Value> {
    let (_, camera) = crate::authoring_imported_terrain::shadow_source(world)?;
    let transform = world.get::<GlobalTransform>(camera)?;
    let Projection::Perspective(projection) = world.get::<Projection>(camera)? else {
        return None;
    };
    Some(serde_json::json!({
        "world_from_view": transform.to_matrix().to_cols_array(),
        "projection": [projection.fov, projection.aspect_ratio, projection.near, projection.far],
        "fixed_tick": world.get_resource::<forge_engine::PlayerRuntimeSnapshot>().map(|player| player.fixed_tick),
    }))
}

fn apply_hold(world: &mut World) {
    let requested = crate::authoring_input::comparison_hold_requested()
        && world
            .get_resource::<forge_engine::PlayerSimulationReady>()
            .is_some_and(|ready| ready.0);
    if requested == held(world) {
        return;
    }
    let Some(clock) = tidewater_ocean::live_clock(world) else {
        return;
    };
    if requested {
        clock.pause();
        world.resource_mut::<Time<Virtual>>().pause();
    } else {
        if clock.snapshot().pending.is_some() {
            return;
        }
        if let Err(error) = clock.resume_at(world.resource::<Time<Real>>().elapsed_secs_f64()) {
            crate::authoring_viewport::report(format!(
                "Reprise des animations impossible : {error}"
            ));
            return;
        }
        world.resource_mut::<Time<Virtual>>().unpause();
    }
    world.resource_mut::<ComparisonHold>().0 = requested;
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
fn apply_graphics(
    mut control: ResMut<tidewater_underwater::TidewaterUnderwaterControl>,
    mut lighting: ResMut<forge_tidewater::tidewater_underwater_lighting::Control>,
    mut refraction: ResMut<forge_tidewater::tidewater_refraction::Control>,
) {
    let graphics = crate::authoring_input::graphics();
    control.enabled = graphics.underwater;
    control.shafts_strength = graphics.underwater_shafts;
    refraction.enabled = graphics.water_refraction_capture.into();
    let next = forge_tidewater::tidewater_underwater_lighting::Control {
        enabled: graphics.underwater_lighting.into(),
        caustics_strength: graphics.underwater_caustics,
        shadow_refraction: graphics.underwater_shadow_refraction.into(),
    };
    if *lighting != next {
        *lighting = next;
    }
}
