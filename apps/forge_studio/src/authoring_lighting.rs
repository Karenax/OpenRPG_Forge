//! Reversible inspection light; never changes the zone or the final game environment.
use bevy::prelude::With;
use bevy::{ecs as bevy_ecs, prelude::*};
use leptos::prelude::*;
use std::cell::Cell;

thread_local! {
    static WORK_LIGHT: Cell<Option<bool>> = const { Cell::new(None) };
}

const PREFERENCE_KEY: &str = "forge.studio.work-light.v1";

// Switching between editing and playtest reloads the WebGPU viewport. Keep
// this inspection preference in the window session, never in authored data.
fn enabled() -> bool {
    WORK_LIGHT.with(|state| {
        if let Some(value) = state.get() {
            return value;
        }
        let value = web_sys::window()
            .and_then(|window| window.session_storage().ok().flatten())
            .and_then(|storage| storage.get_item(PREFERENCE_KEY).ok().flatten())
            .and_then(|value| match value.as_str() {
                "true" => Some(true),
                "false" => Some(false),
                _ => None,
            })
            .unwrap_or(true);
        state.set(Some(value));
        value
    })
}

fn set_enabled(value: bool) {
    WORK_LIGHT.with(|state| state.set(Some(value)));
    if let Some(storage) =
        web_sys::window().and_then(|window| window.session_storage().ok().flatten())
        && storage
            .set_item(PREFERENCE_KEY, if value { "true" } else { "false" })
            .is_err()
    {
        web_sys::console::warn_1(
            &"Le choix d’éclairage de travail ne peut pas être conservé après rechargement.".into(),
        );
    }
}

#[derive(Component)]
struct OriginalAmbient {
    light: Option<AmbientLight>,
    work_enabled: bool,
    inspection_light: Option<Entity>,
}

type CameraLighting<'w, 's> = Query<
    'w,
    's,
    (
        Entity,
        &'static Camera,
        Option<&'static AmbientLight>,
        Option<&'static OriginalAmbient>,
    ),
    (
        With<Camera3d>,
        Without<forge_engine::tidewater_sky::preview::PreviewCamera>,
        Without<forge_engine::tidewater_refraction::CaptureCamera>,
    ),
>;

#[component]
pub(super) fn LightingControls() -> impl IntoView {
    let enabled = RwSignal::new(enabled());
    view! {
        <label>
            <input type="checkbox" prop:checked=move || enabled.get()
                on:change=move |event| {
                    let checked = event_target_checked(&event);
                    enabled.set(checked);
                    set_enabled(checked);
                }/>
            "Éclairage de travail — temporaire, actif aussi en playtest"
        </label>
    }
}

pub(super) fn install(app: &mut App) {
    app.add_systems(Update, apply);
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn apply(mut commands: Commands, cameras: CameraLighting<'_, '_>) {
    let enabled = enabled();
    for (entity, view, ambient, original) in &cameras {
        // The hidden overview camera must not light the scene during playtest.
        let enabled = enabled && view.is_active;
        if original.is_some_and(|original| original.work_enabled == enabled) {
            continue;
        }
        if let Some(light) = original.and_then(|original| original.inspection_light) {
            commands.entity(light).despawn();
        }
        let original = original.map_or_else(|| ambient.cloned(), |original| original.light.clone());
        // Inverse-square falloff distinguishes the nearby facade from the back
        // wall through the opening. No shadow-map allocation for this tool.
        let inspection_light = enabled.then(|| {
            commands
                .spawn((
                    Name::new("Temporary camera inspection light"),
                    PointLight {
                        // Daylight exposure: 100 klm gives about 500 lx at 4 m,
                        // versus 80 lx at 10 m (before range attenuation).
                        intensity: 100_000.0,
                        range: 24.0,
                        shadow_maps_enabled: false,
                        ..default()
                    },
                    Transform::from_xyz(0.25, 0.35, 0.0),
                    ChildOf(entity),
                ))
                .id()
        });
        let mut camera = commands.entity(entity);
        if enabled {
            camera.insert(AmbientLight {
                color: Color::srgb(0.8, 0.86, 1.0),
                brightness: 250.0,
                ..default()
            });
        } else if let Some(light) = &original {
            camera.insert(light.clone());
        } else {
            camera.remove::<AmbientLight>();
        }
        camera.insert(OriginalAmbient {
            light: original,
            work_enabled: enabled,
            inspection_light,
        });
    }
}
