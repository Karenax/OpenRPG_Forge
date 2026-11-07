//! Playable Tidewater graphics controls. Only controls with a live renderer effect appear.
use bevy::{
    input::mouse::{MouseScrollUnit, MouseWheel},
    prelude::*,
    text::FontSource,
};
use forge_engine::{
    ForgePlayerCamera, PlayerInputFocus,
    source_graphics::{self, Control as Setting, Settings as Values},
    source_surface::SourceSurfaceMaterial,
};
use std::path::PathBuf;

#[derive(Resource)]
pub(crate) struct GraphicsState {
    values: Values,
    pub open: bool,
    path: Option<PathBuf>,
}

impl GraphicsState {
    pub(crate) fn values_receipt(&self) -> serde_json::Value {
        serde_json::json!(self.values)
    }

    fn load() -> Self {
        // The static scene capture can show the actual menu for visual QA.
        // Normal play always starts with the panel closed.
        let proof_open = std::env::var_os("FORGE_TIDEWATER_STATIC_POSTFX").is_some()
            && matches!(
                std::env::var("FORGE_TIDEWATER_GRAPHICS_PROOF").as_deref(),
                Ok("top" | "bottom")
            );
        let path = std::env::var_os("LOCALAPPDATA").map(|root| {
            PathBuf::from(root)
                .join("OpenRPG Forge")
                .join("Tidewater")
                .join("graphics.json")
        });
        Self::load_from(path, proof_open)
    }

    fn load_from(path: Option<PathBuf>, open: bool) -> Self {
        let values = path
            .as_ref()
            .and_then(|path| std::fs::read(path).ok())
            .and_then(|bytes| serde_json::from_slice::<Values>(&bytes).ok())
            .filter(|values| values.valid())
            .unwrap_or_default();
        Self { values, open, path }
    }

    fn save(&self) {
        let Some(path) = &self.path else { return };
        let Some(parent) = path.parent() else { return };
        if let Err(error) = std::fs::create_dir_all(parent).and_then(|()| {
            let bytes = serde_json::to_vec_pretty(&self.values).map_err(std::io::Error::other)?;
            std::fs::write(path, bytes)
        }) {
            warn!("Tidewater graphics settings could not be saved: {error}");
        }
    }
}

#[derive(Component)]
pub(super) struct Panel;
#[derive(Component)]
struct SettingValue(Setting);
#[derive(Component)]
struct Step {
    setting: Setting,
    direction: f32,
}

pub(super) fn install(app: &mut App) -> Result<(), Box<dyn std::error::Error>> {
    if std::env::var("FORGE_TIDEWATER_SOURCE_FINAL_POST").as_deref() == Ok("1")
        && !app.is_plugin_added::<forge_engine::source_final_post::PluginSourceFinal>()
    {
        app.add_plugins(forge_engine::source_final_post::PluginSourceFinal);
    }
    source_graphics::install(app)?;
    app.insert_resource(GraphicsState::load())
        .add_systems(PostStartup, spawn_panel)
        .add_systems(
            Update,
            (toggle, scroll_panel, click, apply, refresh).chain(),
        );
    Ok(())
}

pub(super) fn toggle(
    keys: Res<ButtonInput<KeyCode>>,
    mut state: ResMut<GraphicsState>,
    mut panels: Query<&mut Visibility, With<Panel>>,
    mut focus: ResMut<PlayerInputFocus>,
) {
    if state.open && keys.just_pressed(KeyCode::KeyR) {
        state.values = Values::default();
        state.save();
    }
    if !(keys.just_pressed(KeyCode::KeyH) || keys.just_pressed(KeyCode::F10)) {
        return;
    }
    state.open = !state.open;
    if state.open {
        focus.0 = false;
    }
    for mut visibility in &mut panels {
        *visibility = if state.open {
            Visibility::Visible
        } else {
            Visibility::Hidden
        };
    }
}

fn scroll_panel(
    mut wheel: MessageReader<MouseWheel>,
    state: Res<GraphicsState>,
    mut panels: Query<(&mut ScrollPosition, &ComputedNode), With<Panel>>,
) {
    // Consume wheel input while closed so reopening never replays old scrolling.
    let delta: f32 = wheel
        .read()
        .map(|event| {
            event.y
                * match event.unit {
                    MouseScrollUnit::Line => 42.0,
                    MouseScrollUnit::Pixel => 1.0,
                }
        })
        .sum();
    if !state.open || delta == 0.0 {
        return;
    }
    for (mut position, layout) in &mut panels {
        // Bevy clamps only the resolved layout position. Clamp the stored value
        // too, or overscrolling leaves an invisible distance to wheel back.
        let scale = layout.inverse_scale_factor();
        let max_physical =
            (layout.content_size().y - layout.size().y + layout.scrollbar_size.y).max(0.0);
        let current = layout.scroll_position.y * scale;
        position.y = (current - delta).clamp(0.0, max_physical * scale);
    }
}

fn click(
    buttons: Query<(&Interaction, &Step), Changed<Interaction>>,
    mut state: ResMut<GraphicsState>,
) {
    if !state.open {
        return;
    }
    let mut changed = false;
    for (interaction, button) in &buttons {
        if *interaction != Interaction::Pressed {
            continue;
        }
        match button.setting.step(&mut state.values, button.direction) {
            Ok(updated) => changed |= updated,
            Err(error) => warn!(%error, "Source graphics control refused"),
        }
    }
    if changed {
        state.save();
    }
}

// The UI supplies settings; the shared engine owns the rendering behavior.
fn apply(
    state: Res<GraphicsState>,
    cameras: Query<Entity, With<ForgePlayerCamera>>,
    mut commands: Commands,
) {
    if !state.is_changed() {
        return;
    }
    let Ok(camera) = cameras.single() else { return };
    let values = state.values;
    let pipeline = if std::env::var("FORGE_TIDEWATER_SOURCE_FINAL_POST").as_deref() == Ok("1") {
        source_graphics::Pipeline::SourceFinal
    } else {
        source_graphics::Pipeline::Reference
    };
    let quality = forge_project::PostProcessQualitySettings {
        bloom_enabled: true,
        color_grading_enabled: true,
        tonemapping_enabled: true,
    };
    commands.queue(move |world: &mut World| {
        if let Err(error) = source_graphics::apply(world, camera, values, quality, pipeline) {
            error!(%error, "Source graphics settings refused");
        }
    });
}

fn refresh(state: Res<GraphicsState>, mut labels: Query<(&SettingValue, &mut Text)>) {
    if !state.is_changed() {
        return;
    }
    for (setting, mut text) in &mut labels {
        text.0 = setting.0.display(state.values);
    }
}

// The panel is one governed UI tree whose children must be spawned in order.
#[allow(clippy::too_many_lines)]
fn spawn_panel(
    mut commands: Commands,
    state: Res<GraphicsState>,
    mut fonts: ResMut<Assets<Font>>,
    village_materials: Res<Assets<SourceSurfaceMaterial>>,
) {
    let proof_position = if std::env::var_os("FORGE_TIDEWATER_STATIC_POSTFX").is_some() {
        match std::env::var("FORGE_TIDEWATER_GRAPHICS_PROOF").as_deref() {
            Ok("top") => Some(Vec2::ZERO),
            Ok("bottom") => Some(Vec2::new(0.0, 500.0)),
            _ => None,
        }
    } else {
        None
    };
    let font = std::fs::read(r"C:\Windows\Fonts\segoeui.ttf")
        .ok()
        .map(|bytes| FontSource::Handle(fonts.add(Font::from_bytes(bytes))))
        .unwrap_or_default();
    let has_bounce = village_materials
        .iter()
        .any(|(_, material)| material.ground_bounce.is_some());
    commands
        .spawn((
            Panel,
            if state.open {
                Visibility::Visible
            } else {
                Visibility::Hidden
            },
            Node {
                position_type: PositionType::Absolute,
                top: Val::Px(24.0),
                right: Val::Px(24.0),
                width: Val::Px(400.0),
                max_height: proof_position.map_or(Val::Percent(90.0), |_| Val::Px(560.0)),
                overflow: Overflow::scroll_y(),
                flex_direction: FlexDirection::Column,
                row_gap: Val::Px(12.0),
                padding: UiRect::all(Val::Px(18.0)),
                ..default()
            },
            ScrollPosition(proof_position.unwrap_or(Vec2::ZERO)),
            BackgroundColor(Color::srgba(0.015, 0.028, 0.038, 0.94)),
        ))
        .with_children(|panel| {
            panel.spawn((
                Text::new("GRAPHISMES"),
                TextFont {
                    font_size: FontSize::Px(18.0),
                    font: font.clone(),
                    ..default()
                },
                TextColor(Color::srgb(0.65, 0.93, 0.86)),
            ));
            panel.spawn((
                Text::new(
                    "Échap : retour  |  H/F10 : fermer  |  R : défauts  |  Molette : défiler  |  Sauvegarde auto",
                ),
                TextFont {
                    font_size: FontSize::Px(13.0),
                    font: font.clone(),
                    ..default()
                },
                TextColor(Color::srgb(0.7, 0.75, 0.78)),
            ));
            for setting in Setting::ALL {
                if matches!(setting, Setting::BounceLight) && !has_bounce {
                    continue;
                }
                panel
                    .spawn(Node {
                        width: Val::Percent(100.0),
                        align_items: AlignItems::Center,
                        justify_content: JustifyContent::SpaceBetween,
                        ..default()
                    })
                    .with_children(|row| {
                        row.spawn((
                            Text::new(setting.label()),
                            TextFont {
                                font_size: FontSize::Px(16.0),
                                font: font.clone(),
                                ..default()
                            },
                            TextColor(Color::WHITE),
                        ));
                        row.spawn(Node {
                            align_items: AlignItems::Center,
                            column_gap: Val::Px(8.0),
                            ..default()
                        })
                        .with_children(|controls| {
                            step_button(controls, setting, -1.0, "-", &font);
                            controls.spawn((
                                SettingValue(setting),
                                Text::new(setting.display(state.values)),
                                TextFont {
                                    font_size: FontSize::Px(15.0),
                                    font: font.clone(),
                                    ..default()
                                },
                                TextColor(Color::srgb(0.75, 0.94, 0.90)),
                                Node {
                                    width: Val::Px(66.0),
                                    ..default()
                                },
                            ));
                            step_button(controls, setting, 1.0, "+", &font);
                        });
                    });
            }
        });
}

fn step_button(
    parent: &mut bevy::ecs::hierarchy::ChildSpawnerCommands,
    setting: Setting,
    direction: f32,
    label: &'static str,
    font: &FontSource,
) {
    parent
        .spawn((
            Button,
            Step { setting, direction },
            Node {
                width: Val::Px(30.0),
                height: Val::Px(30.0),
                align_items: AlignItems::Center,
                justify_content: JustifyContent::Center,
                ..default()
            },
            BackgroundColor(Color::srgb(0.08, 0.25, 0.27)),
        ))
        .with_child((
            Text::new(label),
            TextFont {
                font_size: FontSize::Px(22.0),
                font: font.clone(),
                ..default()
            },
            TextColor(Color::WHITE),
        ));
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::input::touch::TouchPhase;

    #[test]
    fn legacy_graphics_preferences_default_only_the_missing_refraction_capture() {
        let previous = Values {
            exposure_ev: 0.9,
            bloom: 0.08,
            underwater_shadow_refraction: 0.0,
            water_refraction_capture: 0.0,
            ..default()
        };
        let mut json = serde_json::to_value(previous).expect("serialize preferences");
        json.as_object_mut()
            .expect("preferences object")
            .remove("water_refraction_capture");
        let restored: Values = serde_json::from_value(json).expect("restore legacy preferences");
        let expected = Values {
            water_refraction_capture: 1.0,
            ..previous
        };
        assert_eq!(restored, expected);
        assert!(restored.valid());
        for invalid in [-0.1, 1.1, f32::NAN, f32::INFINITY] {
            assert!(
                !Values {
                    water_refraction_capture: invalid,
                    ..default()
                }
                .valid()
            );
        }
    }

    #[test]
    fn refraction_capture_input_applies_persists_and_resets() {
        let path = std::env::temp_dir().join(format!(
            "openrpg-tidewater-refraction-{}-{}.json",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .expect("system clock")
                .as_nanos()
        ));
        let mut app = App::new();
        app.insert_resource(ButtonInput::<KeyCode>::default())
            .insert_resource(PlayerInputFocus(true))
            .insert_resource(GraphicsState::load_from(Some(path.clone()), true))
            .insert_resource(source_graphics::ExposureAssets {
                curve: Handle::default(),
                mask: Handle::default(),
            })
            .insert_resource(Assets::<SourceSurfaceMaterial>::default())
            .insert_resource(forge_engine::source_refraction_capture::Control { enabled: false })
            .add_systems(Update, (toggle, click, apply, refresh).chain());
        app.world_mut().spawn(ForgePlayerCamera);
        let minus = app
            .world_mut()
            .spawn((
                Interaction::None,
                Step {
                    setting: Setting::WaterRefractionCapture,
                    direction: -1.0,
                },
            ))
            .id();
        let label = app
            .world_mut()
            .spawn((
                SettingValue(Setting::WaterRefractionCapture),
                Text::default(),
            ))
            .id();

        app.update();
        assert!(
            app.world()
                .resource::<forge_engine::source_refraction_capture::Control>()
                .enabled
        );
        assert_eq!(app.world().get::<Text>(label).unwrap().0, "Actif");

        *app.world_mut().get_mut::<Interaction>(minus).unwrap() = Interaction::Pressed;
        app.update();
        assert!(
            !app.world()
                .resource::<forge_engine::source_refraction_capture::Control>()
                .enabled
        );
        assert_eq!(app.world().get::<Text>(label).unwrap().0, "Arrêt");
        assert_eq!(
            GraphicsState::load_from(Some(path.clone()), false).values,
            app.world().resource::<GraphicsState>().values
        );
        app.insert_resource(GraphicsState::load_from(Some(path.clone()), true))
            .insert_resource(forge_engine::source_refraction_capture::Control { enabled: true });
        app.update();
        assert!(
            !app.world()
                .resource::<forge_engine::source_refraction_capture::Control>()
                .enabled
        );

        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .press(KeyCode::KeyR);
        app.update();
        assert!(
            app.world()
                .resource::<forge_engine::source_refraction_capture::Control>()
                .enabled
        );
        assert_eq!(app.world().get::<Text>(label).unwrap().0, "Actif");
        assert_eq!(
            GraphicsState::load_from(Some(path.clone()), false).values,
            Values::default()
        );
        std::fs::remove_file(path).expect("remove isolated refraction profile");
    }

    #[test]
    fn graphics_input_round_trip() {
        let path = std::env::temp_dir().join(format!(
            "openrpg-tidewater-graphics-{}-{}.json",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .expect("system clock")
                .as_nanos()
        ));
        let mut app = App::new();
        app.insert_resource(ButtonInput::<KeyCode>::default())
            .insert_resource(PlayerInputFocus(true))
            .insert_resource(GraphicsState::load_from(Some(path.clone()), false))
            .add_message::<MouseWheel>()
            .add_systems(Update, (toggle, scroll_panel, click, refresh).chain());

        let window = app.world_mut().spawn_empty().id();
        let panel = app
            .world_mut()
            .spawn((
                Panel,
                Visibility::Hidden,
                ScrollPosition(Vec2::ZERO),
                ComputedNode {
                    size: Vec2::new(400.0, 300.0),
                    content_size: Vec2::new(400.0, 900.0),
                    ..default()
                },
            ))
            .id();
        let plus = app
            .world_mut()
            .spawn((
                Interaction::None,
                Step {
                    setting: Setting::Bloom,
                    direction: 1.0,
                },
            ))
            .id();
        let minus = app
            .world_mut()
            .spawn((
                Interaction::None,
                Step {
                    setting: Setting::Bloom,
                    direction: -1.0,
                },
            ))
            .id();
        let label = app
            .world_mut()
            .spawn((SettingValue(Setting::Bloom), Text::new("0.050")))
            .id();

        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .press(KeyCode::KeyH);
        app.update();
        assert!(app.world().resource::<GraphicsState>().open);
        assert_eq!(
            app.world().get::<Visibility>(panel),
            Some(&Visibility::Visible)
        );
        assert!(!app.world().resource::<PlayerInputFocus>().0);
        // The minimal test app has no InputPlugin to clear per-frame key edges.
        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .reset_all();

        app.world_mut()
            .resource_mut::<Messages<MouseWheel>>()
            .write(MouseWheel {
                unit: MouseScrollUnit::Line,
                x: 0.0,
                y: -2.0,
                window,
                phase: TouchPhase::Moved,
            });
        app.update();
        assert_eq!(app.world().get::<ScrollPosition>(panel).unwrap().y, 84.0);
        app.world_mut()
            .get_mut::<ComputedNode>(panel)
            .unwrap()
            .scroll_position
            .y = 84.0;
        app.world_mut()
            .resource_mut::<Messages<MouseWheel>>()
            .write(MouseWheel {
                unit: MouseScrollUnit::Line,
                x: 0.0,
                y: 100.0,
                window,
                phase: TouchPhase::Moved,
            });
        app.update();
        assert_eq!(app.world().get::<ScrollPosition>(panel).unwrap().y, 0.0);

        *app.world_mut().get_mut::<Interaction>(plus).unwrap() = Interaction::Pressed;
        app.update();
        assert!((app.world().resource::<GraphicsState>().values.bloom - 0.055).abs() < 1e-6);
        assert_eq!(app.world().get::<Text>(label).unwrap().0, "0.055");
        assert!(
            (GraphicsState::load_from(Some(path.clone()), false)
                .values
                .bloom
                - 0.055)
                .abs()
                < 1e-6
        );

        *app.world_mut().get_mut::<Interaction>(minus).unwrap() = Interaction::Pressed;
        app.update();
        assert!((app.world().resource::<GraphicsState>().values.bloom - 0.05).abs() < 1e-6);
        assert_eq!(app.world().get::<Text>(label).unwrap().0, "0.050");
        assert!(
            (GraphicsState::load_from(Some(path.clone()), false)
                .values
                .bloom
                - 0.05)
                .abs()
                < 1e-6
        );

        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .reset_all();
        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .press(KeyCode::F10);
        app.update();
        assert!(!app.world().resource::<GraphicsState>().open);
        assert_eq!(
            app.world().get::<Visibility>(panel),
            Some(&Visibility::Hidden)
        );
        std::fs::remove_file(path).expect("remove isolated graphics profile");
    }
}

/// Drive the real contrast button only in the explicit isolated menu proof.
pub(super) fn press_contrast_for_proof(world: &mut World) -> Result<f32, &'static str> {
    let before = world.resource::<GraphicsState>().values.contrast;
    let mut buttons = world.query::<(&Step, &mut Interaction)>();
    for (step, mut interaction) in buttons.iter_mut(world) {
        if matches!(step.setting, Setting::Contrast) && step.direction > 0.0 {
            *interaction = Interaction::Pressed;
            return Ok((before + 0.01).clamp(0.8, 1.3));
        }
    }
    Err("contrast button missing")
}

/// Diagnostic-only transitions through the same renderer settings system as the menu.
pub(super) fn terrain_transition(world: &mut World, index: usize) {
    let changes = [
        (Setting::TemporalAa, 1.0),
        (Setting::TemporalAa, 0.0),
        (Setting::Fxaa, 0.0),
        (Setting::Fxaa, 1.0),
        (Setting::AutoExposure, 0.0),
        (Setting::AutoExposure, 1.0),
        (Setting::AmbientOcclusion, 0.0),
        (Setting::AmbientOcclusion, 1.0),
        (Setting::Haze, 0.0),
        (Setting::Haze, 1.6),
        (Setting::WaterRefractionCapture, 0.0),
        (Setting::WaterRefractionCapture, 1.0),
    ];
    if let Some(&(setting, value)) = changes.get(index)
        && let Err(error) = setting.set(&mut world.resource_mut::<GraphicsState>().values, value)
    {
        error!(%error, "Source graphics diagnostic control refused");
    }
}
