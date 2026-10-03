//! Playable Tidewater graphics controls. Only controls with a live renderer effect appear.
use bevy::{
    anti_alias::{
        contrast_adaptive_sharpening::ContrastAdaptiveSharpening, fxaa::Fxaa,
        taa::TemporalAntiAliasing,
    },
    camera::Hdr,
    core_pipeline::prepass::{DepthPrepass, MotionVectorPrepass},
    core_pipeline::tonemapping::Tonemapping,
    input::mouse::{MouseScrollUnit, MouseWheel},
    post_process::{
        auto_exposure::{AutoExposure, AutoExposureCompensationCurve, AutoExposurePlugin},
        bloom::Bloom,
        effect_stack::Vignette,
        motion_blur::MotionBlur,
    },
    prelude::*,
    render::{
        camera::{MipBias, TemporalJitter},
        view::ColorGrading,
    },
    text::FontSource,
};
use forge_engine::{
    ForgePlayerCamera, PlayerInputFocus,
    tidewater_air_haze::{TidewaterAirHaze, TidewaterAirHazePlugin},
    tidewater_exposure,
    tidewater_film_grain::{TidewaterFilmGrain, TidewaterFilmGrainPlugin},
    tidewater_gtao::{TidewaterGtao, TidewaterGtaoPlugin},
    tidewater_lens_flare::{TidewaterLensFlare, TidewaterLensFlarePlugin},
    tidewater_material::VillageMaterial,
};
use serde::{Deserialize, Serialize};
use std::path::PathBuf;

#[derive(Clone, Copy, Debug, Deserialize, Serialize, PartialEq)]
#[serde(default)]
#[serde(deny_unknown_fields)]
struct Values {
    auto_exposure: f32,
    ambient_occlusion: f32,
    fxaa: f32,
    temporal_aa: f32,
    exposure_ev: f32,
    bloom: f32,
    saturation: f32,
    contrast: f32,
    sharpen: f32,
    vignette: f32,
    film_grain: f32,
    haze: f32,
    bounce_light: f32,
    motion_blur_degrees: f32,
    lens_flare: f32,
    underwater_shafts: f32,
    underwater_lighting: f32,
    underwater_caustics: f32,
    underwater_shadow_refraction: f32,
    water_refraction_capture: f32,
}

impl Default for Values {
    fn default() -> Self {
        Self {
            auto_exposure: 1.0,
            ambient_occlusion: 1.0,
            fxaa: 1.0,
            temporal_aa: 0.0,
            exposure_ev: 0.4,
            bloom: 0.05,
            saturation: 1.06,
            contrast: 1.04,
            sharpen: 0.2,
            vignette: 0.28,
            film_grain: 0.012,
            haze: 1.6,
            bounce_light: 1.0,
            motion_blur_degrees: 180.0,
            lens_flare: 1.0,
            underwater_shafts: 1.0,
            underwater_lighting: 1.0,
            underwater_caustics: 0.75,
            underwater_shadow_refraction: 1.0,
            water_refraction_capture: 1.0,
        }
    }
}

impl Values {
    fn valid(self) -> bool {
        (0.0..=1.0).contains(&self.auto_exposure)
            && (0.0..=1.5).contains(&self.ambient_occlusion)
            && (0.0..=1.0).contains(&self.fxaa)
            && (0.0..=1.0).contains(&self.temporal_aa)
            && (-3.0..=3.0).contains(&self.exposure_ev)
            && (0.0..=0.3).contains(&self.bloom)
            && (0.5..=1.5).contains(&self.saturation)
            && (0.8..=1.3).contains(&self.contrast)
            && (0.0..=1.0).contains(&self.sharpen)
            && (0.0..=1.0).contains(&self.vignette)
            && (0.0..=0.06).contains(&self.film_grain)
            && (0.0..=4.0).contains(&self.haze)
            && (0.0..=2.0).contains(&self.bounce_light)
            && (0.0..=360.0).contains(&self.motion_blur_degrees)
            && (0.0..=2.0).contains(&self.lens_flare)
            && (0.0..=2.0).contains(&self.underwater_shafts)
            && (0.0..=1.0).contains(&self.underwater_lighting)
            && (0.0..=4.0).contains(&self.underwater_caustics)
            && (0.0..=1.0).contains(&self.underwater_shadow_refraction)
            && (0.0..=1.0).contains(&self.water_refraction_capture)
    }

    fn field_mut(&mut self, setting: Setting) -> &mut f32 {
        match setting {
            Setting::AutoExposure => &mut self.auto_exposure,
            Setting::AmbientOcclusion => &mut self.ambient_occlusion,
            Setting::Fxaa => &mut self.fxaa,
            Setting::TemporalAa => &mut self.temporal_aa,
            Setting::Exposure => &mut self.exposure_ev,
            Setting::Bloom => &mut self.bloom,
            Setting::Saturation => &mut self.saturation,
            Setting::Contrast => &mut self.contrast,
            Setting::Sharpen => &mut self.sharpen,
            Setting::Vignette => &mut self.vignette,
            Setting::FilmGrain => &mut self.film_grain,
            Setting::Haze => &mut self.haze,
            Setting::BounceLight => &mut self.bounce_light,
            Setting::MotionBlur => &mut self.motion_blur_degrees,
            Setting::LensFlare => &mut self.lens_flare,
            Setting::UnderwaterShafts => &mut self.underwater_shafts,
            Setting::UnderwaterLighting => &mut self.underwater_lighting,
            Setting::UnderwaterCaustics => &mut self.underwater_caustics,
            Setting::UnderwaterShadowRefraction => &mut self.underwater_shadow_refraction,
            Setting::WaterRefractionCapture => &mut self.water_refraction_capture,
        }
    }

    fn field(self, setting: Setting) -> f32 {
        match setting {
            Setting::AutoExposure => self.auto_exposure,
            Setting::AmbientOcclusion => self.ambient_occlusion,
            Setting::Fxaa => self.fxaa,
            Setting::TemporalAa => self.temporal_aa,
            Setting::Exposure => self.exposure_ev,
            Setting::Bloom => self.bloom,
            Setting::Saturation => self.saturation,
            Setting::Contrast => self.contrast,
            Setting::Sharpen => self.sharpen,
            Setting::Vignette => self.vignette,
            Setting::FilmGrain => self.film_grain,
            Setting::Haze => self.haze,
            Setting::BounceLight => self.bounce_light,
            Setting::MotionBlur => self.motion_blur_degrees,
            Setting::LensFlare => self.lens_flare,
            Setting::UnderwaterShafts => self.underwater_shafts,
            Setting::UnderwaterLighting => self.underwater_lighting,
            Setting::UnderwaterCaustics => self.underwater_caustics,
            Setting::UnderwaterShadowRefraction => self.underwater_shadow_refraction,
            Setting::WaterRefractionCapture => self.water_refraction_capture,
        }
    }
}

#[derive(Resource)]
pub(super) struct GraphicsState {
    values: Values,
    pub open: bool,
    path: Option<PathBuf>,
}

#[derive(Resource)]
struct SourceExposureCurve(Handle<AutoExposureCompensationCurve>);

#[derive(Resource)]
struct SourceExposureMask(Handle<Image>);

impl GraphicsState {
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

#[derive(Clone, Copy, Component)]
enum Setting {
    AutoExposure,
    AmbientOcclusion,
    Fxaa,
    TemporalAa,
    Exposure,
    Bloom,
    Saturation,
    Contrast,
    Sharpen,
    Vignette,
    FilmGrain,
    Haze,
    BounceLight,
    MotionBlur,
    LensFlare,
    UnderwaterShafts,
    UnderwaterLighting,
    UnderwaterCaustics,
    UnderwaterShadowRefraction,
    WaterRefractionCapture,
}

impl Setting {
    const ALL: [Self; 20] = [
        Self::AutoExposure,
        Self::AmbientOcclusion,
        Self::Fxaa,
        Self::TemporalAa,
        Self::Exposure,
        Self::Bloom,
        Self::Saturation,
        Self::Contrast,
        Self::Sharpen,
        Self::Vignette,
        Self::FilmGrain,
        Self::Haze,
        Self::BounceLight,
        Self::MotionBlur,
        Self::LensFlare,
        Self::UnderwaterShafts,
        Self::UnderwaterLighting,
        Self::UnderwaterCaustics,
        Self::UnderwaterShadowRefraction,
        Self::WaterRefractionCapture,
    ];

    const fn label(self) -> &'static str {
        match self {
            Self::AutoExposure => "Adaptation",
            Self::AmbientOcclusion => "Occlusion ambiante",
            Self::Fxaa => "FXAA",
            Self::TemporalAa => "TAA (expérimental)",
            Self::Exposure => "Exposition",
            Self::Bloom => "Bloom",
            Self::Saturation => "Saturation",
            Self::Contrast => "Contraste",
            Self::Sharpen => "Netteté",
            Self::Vignette => "Vignette",
            Self::FilmGrain => "Grain photo",
            Self::Haze => "Brume marine",
            Self::BounceLight => "Lumière rebond",
            Self::MotionBlur => "Flou de mouvement",
            Self::LensFlare => "Lens flare solaire",
            Self::UnderwaterShafts => "Rayons sous-marins",
            Self::UnderwaterLighting => "Surfaces immergées",
            Self::UnderwaterCaustics => "Caustiques",
            Self::UnderwaterShadowRefraction => "Réfraction des ombres",
            Self::WaterRefractionCapture => "Capture de réfraction",
        }
    }

    const fn bounds(self) -> (f32, f32, f32) {
        match self {
            Self::AutoExposure
            | Self::Fxaa
            | Self::TemporalAa
            | Self::UnderwaterLighting
            | Self::UnderwaterShadowRefraction
            | Self::WaterRefractionCapture => (0.0, 1.0, 1.0),
            Self::AmbientOcclusion => (0.0, 1.5, 0.01),
            Self::Exposure => (-3.0, 3.0, 0.1),
            Self::Bloom => (0.0, 0.3, 0.005),
            Self::Saturation => (0.5, 1.5, 0.01),
            Self::Contrast => (0.8, 1.3, 0.01),
            Self::Sharpen | Self::Vignette => (0.0, 1.0, 0.01),
            Self::FilmGrain => (0.0, 0.06, 0.001),
            Self::Haze | Self::UnderwaterCaustics => (0.0, 4.0, 0.05),
            Self::BounceLight => (0.0, 2.0, 0.01),
            Self::LensFlare | Self::UnderwaterShafts => (0.0, 2.0, 0.05),
            Self::MotionBlur => (0.0, 360.0, 18.0),
        }
    }

    fn display(self, values: Values) -> String {
        match self {
            Self::AutoExposure => if values.auto_exposure > 0.5 {
                "Auto"
            } else {
                "Fixe"
            }
            .to_owned(),
            Self::Fxaa => if values.fxaa > 0.5 { "Actif" } else { "Arrêt" }.to_owned(),
            Self::TemporalAa => if values.temporal_aa > 0.5 {
                "Actif"
            } else {
                "Arrêt"
            }
            .to_owned(),
            Self::WaterRefractionCapture => if values.water_refraction_capture > 0.5 {
                "Actif"
            } else {
                "Arrêt"
            }
            .to_owned(),
            Self::Exposure => format!("{:+.1} EV", values.field(self)),
            Self::MotionBlur => format!("{:.0}°", values.field(self)),
            Self::LensFlare => format!("{:.2}", values.lens_flare),
            Self::Bloom => format!("{:.3}", values.bloom),
            Self::FilmGrain => format!("{:.3}", values.film_grain),
            _ => format!("{:.2}", values.field(self)),
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
    app.add_plugins((
        AutoExposurePlugin,
        TidewaterFilmGrainPlugin,
        TidewaterAirHazePlugin,
        TidewaterGtaoPlugin,
        TidewaterLensFlarePlugin,
    ));
    let curve = app
        .world_mut()
        .resource_mut::<Assets<AutoExposureCompensationCurve>>()
        .add(tidewater_exposure::source_compensation_curve()?);
    let mask = app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(tidewater_exposure::source_metering_mask());
    app.insert_resource(SourceExposureCurve(curve))
        .insert_resource(SourceExposureMask(mask))
        .insert_resource(GraphicsState::load())
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
        let (low, high, step) = button.setting.bounds();
        let field = state.values.field_mut(button.setting);
        let next = (*field + button.direction * step).clamp(low, high);
        if next.to_bits() != field.to_bits() {
            *field = next;
            changed = true;
        }
    }
    if changed {
        state.save();
    }
}

// Bevy supplies these independent resources to one render-settings system;
// keeping the owned parameters preserves change tracking and stage ordering.
#[allow(clippy::too_many_arguments, clippy::too_many_lines)]
fn apply(
    state: Res<GraphicsState>,
    curve: Res<SourceExposureCurve>,
    mask: Res<SourceExposureMask>,
    cameras: Query<Entity, With<ForgePlayerCamera>>,
    mut village_materials: ResMut<Assets<VillageMaterial>>,
    underwater: Option<ResMut<forge_engine::tidewater_underwater::TidewaterUnderwaterControl>>,
    lighting: Option<ResMut<forge_engine::tidewater_underwater_lighting::Control>>,
    refraction: Option<ResMut<forge_engine::tidewater_refraction::Control>>,
    mut commands: Commands,
) {
    if !state.is_changed() {
        return;
    }
    let Ok(camera) = cameras.single() else { return };
    if let Some(mut underwater) = underwater {
        underwater.shafts_strength = state.values.underwater_shafts;
    }
    if let Some(mut lighting) = lighting {
        let next = forge_engine::tidewater_underwater_lighting::Control {
            enabled: state.values.underwater_lighting > 0.5,
            caustics_strength: state.values.underwater_caustics,
            shadow_refraction: state.values.underwater_shadow_refraction > 0.5,
        };
        if *lighting != next {
            *lighting = next;
        }
    }
    if let Some(mut refraction) = refraction {
        let enabled = state.values.water_refraction_capture > 0.5;
        if refraction.enabled != enabled {
            refraction.enabled = enabled;
        }
    }
    for (_, material) in village_materials.iter_mut() {
        if material.ground_bounce.is_some() {
            material.bounce_settings.x = state.values.bounce_light;
        }
    }
    let mut grading = ColorGrading::default();
    grading.global.exposure = state.values.exposure_ev;
    grading.global.post_saturation = state.values.saturation;
    grading.midtones.contrast = state.values.contrast;
    commands.entity(camera).insert((
        Hdr,
        Tonemapping::AcesFitted,
        grading,
        ContrastAdaptiveSharpening {
            enabled: state.values.sharpen > 0.0,
            sharpening_strength: state.values.sharpen,
            denoise: false,
        },
        Vignette {
            intensity: state.values.vignette,
            ..default()
        },
    ));
    if state.values.bloom > 0.0 {
        commands.entity(camera).insert(Bloom {
            intensity: state.values.bloom,
            ..Bloom::NATURAL
        });
    } else {
        commands.entity(camera).remove::<Bloom>();
    }
    if state.values.film_grain > 0.0 {
        commands
            .entity(camera)
            .insert(TidewaterFilmGrain::new(state.values.film_grain));
    } else {
        commands.entity(camera).remove::<TidewaterFilmGrain>();
    }
    // Keep the render topology stable when the slider reaches zero: removing
    // a ping-pong pass also changes which target auto exposure sees next frame.
    commands
        .entity(camera)
        .insert(TidewaterAirHaze::new(state.values.haze));
    commands
        .entity(camera)
        .insert(TidewaterGtao::new(state.values.ambient_occlusion));
    // The 1×1 visibility pass branches before depth reads at zero, while the
    // camera keeps its ping-pong render topology for stable auto exposure.
    commands
        .entity(camera)
        .insert(TidewaterLensFlare::new(state.values.lens_flare));
    if state.values.auto_exposure > 0.5 {
        commands.entity(camera).insert(AutoExposure {
            filter: 0.0..=1.0,
            speed_brighten: 1.6,
            speed_darken: 1.1,
            compensation_curve: curve.0.clone(),
            metering_mask: mask.0.clone(),
            ..default()
        });
    } else {
        commands.entity(camera).remove::<AutoExposure>();
    }
    if state.values.temporal_aa > 0.5 {
        commands
            .entity(camera)
            .insert(TemporalAntiAliasing::default());
        commands.entity(camera).remove::<Fxaa>();
    } else {
        commands
            .entity(camera)
            .remove::<(TemporalAntiAliasing, TemporalJitter, MipBias)>();
        if state.values.fxaa > 0.5 {
            commands.entity(camera).insert(Fxaa::default());
        } else {
            commands.entity(camera).remove::<Fxaa>();
        }
    }
    if state.values.motion_blur_degrees > 0.0 {
        commands.entity(camera).insert((
            MotionBlur {
                shutter_angle: state.values.motion_blur_degrees / 360.0,
                samples: 3,
            },
            MotionVectorPrepass,
        ));
    } else {
        commands.entity(camera).remove::<MotionBlur>();
    }
    if state.values.temporal_aa <= 0.5 && state.values.motion_blur_degrees <= 0.0 {
        commands
            .entity(camera)
            .remove::<(DepthPrepass, MotionVectorPrepass)>();
    }
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
    village_materials: Res<Assets<VillageMaterial>>,
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
                    "H/F10 : fermer  |  R : défauts  |  Molette : défiler  |  Sauvegarde auto",
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
            .insert_resource(SourceExposureCurve(Handle::default()))
            .insert_resource(SourceExposureMask(Handle::default()))
            .insert_resource(Assets::<VillageMaterial>::default())
            .insert_resource(forge_engine::tidewater_refraction::Control { enabled: false })
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
                .resource::<forge_engine::tidewater_refraction::Control>()
                .enabled
        );
        assert_eq!(app.world().get::<Text>(label).unwrap().0, "Actif");

        *app.world_mut().get_mut::<Interaction>(minus).unwrap() = Interaction::Pressed;
        app.update();
        assert!(
            !app.world()
                .resource::<forge_engine::tidewater_refraction::Control>()
                .enabled
        );
        assert_eq!(app.world().get::<Text>(label).unwrap().0, "Arrêt");
        assert_eq!(
            GraphicsState::load_from(Some(path.clone()), false).values,
            app.world().resource::<GraphicsState>().values
        );
        app.insert_resource(GraphicsState::load_from(Some(path.clone()), true))
            .insert_resource(forge_engine::tidewater_refraction::Control { enabled: true });
        app.update();
        assert!(
            !app.world()
                .resource::<forge_engine::tidewater_refraction::Control>()
                .enabled
        );

        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .press(KeyCode::KeyR);
        app.update();
        assert!(
            app.world()
                .resource::<forge_engine::tidewater_refraction::Control>()
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
