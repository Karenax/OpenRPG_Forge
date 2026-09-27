//! Reversible inspection light; never changes the zone or the final game environment.
use bevy::prelude::With;
use bevy::{ecs as bevy_ecs, prelude::*};
use leptos::prelude::*;
use std::cell::Cell;

thread_local! {
    static WORK_LIGHT: Cell<bool> = const { Cell::new(true) };
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
    ),
>;

#[component]
pub(super) fn LightingControls() -> impl IntoView {
    let enabled = RwSignal::new(WORK_LIGHT.with(Cell::get));
    view! {
        <label>
            <input type="checkbox" prop:checked=move || enabled.get()
                on:change=move |event| {
                    let checked = event_target_checked(&event);
                    enabled.set(checked);
                    WORK_LIGHT.with(|state| state.set(checked));
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
    let enabled = WORK_LIGHT.with(Cell::get);
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
