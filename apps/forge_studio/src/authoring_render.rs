//! Temporary render policy for editing. It never changes authored content.
use bevy::{
    ecs as bevy_ecs,
    pbr::ContactShadows,
    prelude::*,
    winit::{UpdateMode, WinitSettings},
};
use std::time::Duration;

#[derive(Resource)]
pub(crate) struct FastEditing;

pub(crate) fn install(app: &mut App) {
    app.insert_resource(FastEditing)
        .insert_resource(WinitSettings {
            focused_mode: UpdateMode::reactive(Duration::from_millis(16)),
            unfocused_mode: UpdateMode::reactive_low_power(Duration::from_millis(250)),
        })
        .add_systems(Update, ensure_daylight)
        .add_systems(
            PostUpdate,
            (lighten_cameras, lighten_lights)
                .before(bevy::light::SimulationLightSystems::UpdateDirectionalLightCascades)
                .before(bevy::light::SimulationLightSystems::UpdateLightFrusta),
        );
    if let Some(canvas) = web_sys::window()
        .and_then(|window| window.document())
        .and_then(|document| document.get_element_by_id("forge-authoring-canvas"))
    {
        let _ = canvas.set_attribute("data-forge-editing-quality", "fast");
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn ensure_daylight(
    mut commands: Commands,
    lights: Query<(), With<DirectionalLight>>,
    mut initialized: Local<bool>,
) {
    if *initialized {
        return;
    }
    *initialized = true;
    if lights.is_empty() {
        commands.spawn((
            Name::new("Temporary editor daylight"),
            DirectionalLight {
                color: Color::srgb(1.0, 0.96, 0.9),
                illuminance: 30_000.0,
                shadow_maps_enabled: false,
                contact_shadows_enabled: false,
                ..default()
            },
            Transform::IDENTITY.looking_at(Vec3::new(-0.45, -1.0, -0.28), Vec3::Y),
        ));
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn lighten_cameras(mut commands: Commands, cameras: Query<Entity, Added<Camera3d>>) {
    for entity in &cameras {
        commands
            .entity(entity)
            .insert(Msaa::Off)
            .remove::<ContactShadows>();
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn lighten_lights(
    mut directional: Query<&mut DirectionalLight, Changed<DirectionalLight>>,
    mut point: Query<&mut PointLight, Changed<PointLight>>,
    mut spot: Query<&mut SpotLight, Changed<SpotLight>>,
) {
    for mut light in &mut directional {
        if light.shadow_maps_enabled || light.contact_shadows_enabled {
            light.shadow_maps_enabled = false;
            light.contact_shadows_enabled = false;
        }
    }
    for mut light in &mut point {
        if light.shadow_maps_enabled || light.contact_shadows_enabled {
            light.shadow_maps_enabled = false;
            light.contact_shadows_enabled = false;
        }
    }
    for mut light in &mut spot {
        if light.shadow_maps_enabled || light.contact_shadows_enabled {
            light.shadow_maps_enabled = false;
            light.contact_shadows_enabled = false;
        }
    }
}
