//! Input belongs to the playtest canvas only while it owns pointer capture.
use bevy::ecs::system::SystemParam;
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::PlayerInputFocus;
use forge_project::authoring::playtest::{PlaytestExitDiagnostics, PlaytestIdentity};
use leptos::prelude::*;
use serde::{Deserialize, Serialize};
use std::fmt::Write as _;

const GRAPHICS_PREFERENCE_KEY: &str = "forge.studio.tidewater.graphics.v1";

/// Exclusive owner of temporal jitter. FXAA remains an independent spatial pass.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize, Default)]
#[serde(rename_all = "snake_case")]
pub(super) enum TemporalMode {
    #[default]
    Off,
    BevyTaa,
    SourceResolve,
}

impl TemporalMode {
    pub(super) const fn value(self) -> &'static str {
        match self {
            Self::Off => "off",
            Self::BevyTaa => "bevy_taa",
            Self::SourceResolve => "source_resolve",
        }
    }
}

/// Two-state surface lighting preference; stored as a boolean for compatibility.
#[derive(Clone, Copy, PartialEq, Serialize, Deserialize, Default)]
#[serde(from = "bool", into = "bool")]
pub(super) enum UnderwaterLighting {
    #[default]
    Enabled,
    Disabled,
}

impl From<bool> for UnderwaterLighting {
    fn from(enabled: bool) -> Self {
        if enabled {
            Self::Enabled
        } else {
            Self::Disabled
        }
    }
}

impl From<UnderwaterLighting> for bool {
    fn from(value: UnderwaterLighting) -> Self {
        value == UnderwaterLighting::Enabled
    }
}

thread_local! {
    static PLAYING: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
    static METRICS: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
    static COMPARISON_HOLD: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
    static OUTPUT: std::cell::RefCell<Option<WriteSignal<String>>> = const { std::cell::RefCell::new(None) };
    static TEMPORAL_OUTPUT: std::cell::RefCell<Option<WriteSignal<String>>> = const { std::cell::RefCell::new(None) };
    static TEMPORAL_STATUS: std::cell::RefCell<String> = const { std::cell::RefCell::new(String::new()) };
    static LAST_RUNTIME: std::cell::RefCell<Option<RuntimeObservation>> = const { std::cell::RefCell::new(None) };
    static GRAPHICS: std::cell::Cell<PlaytestGraphics> = const { std::cell::Cell::new(PlaytestGraphics::DEFAULT) };
    static GRAPHICS_LOADED: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
}

/// Local presentation preferences for the imported Tidewater playtest.
/// They never mutate the authored zone or its accepted source snapshot.
#[derive(Clone, Copy, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub(super) struct PlaytestGraphics {
    pub(super) auto_exposure: bool,
    pub(super) fxaa: bool,
    #[serde(default)]
    pub(super) temporal_mode: TemporalMode,
    #[serde(default = "immersion_enabled_default")]
    pub(super) underwater: bool,
    #[serde(default = "shaft_strength_default")]
    pub(super) underwater_shafts: f32,
    #[serde(default)]
    pub(super) underwater_lighting: UnderwaterLighting,
    #[serde(default = "caustics_strength_default")]
    pub(super) underwater_caustics: f32,
    #[serde(default)]
    pub(super) underwater_shadow_refraction: UnderwaterLighting,
    pub(super) water_refraction_capture: UnderwaterLighting,
    pub(super) exposure_ev: f32,
    pub(super) saturation: f32,
    pub(super) contrast: f32,
    pub(super) ambient_occlusion: f32,
    pub(super) bounce_light: f32,
    pub(super) lens_flare: f32,
    pub(super) air_haze: f32,
    pub(super) sharpen: f32,
    pub(super) bloom: f32,
    pub(super) vignette: f32,
    pub(super) film_grain: f32,
    pub(super) motion_blur_degrees: f32,
}

impl PlaytestGraphics {
    pub(super) const DEFAULT: Self = Self {
        auto_exposure: true,
        fxaa: true,
        temporal_mode: TemporalMode::Off,
        underwater: true,
        underwater_shafts: 1.0,
        underwater_lighting: UnderwaterLighting::Enabled,
        underwater_caustics: 0.75,
        underwater_shadow_refraction: UnderwaterLighting::Enabled,
        water_refraction_capture: UnderwaterLighting::Enabled,
        exposure_ev: 0.1,
        saturation: 1.06,
        contrast: 1.04,
        ambient_occlusion: 1.0,
        bounce_light: 1.0,
        lens_flare: 1.0,
        air_haze: 1.6,
        sharpen: 0.2,
        bloom: 0.05,
        vignette: 0.28,
        film_grain: 0.012,
        motion_blur_degrees: 180.0,
    };

    fn valid(self) -> bool {
        (-2.0..=2.0).contains(&self.exposure_ev)
            && (0.0..=2.0).contains(&self.underwater_shafts)
            && (0.0..=4.0).contains(&self.underwater_caustics)
            && (0.0..=2.0).contains(&self.saturation)
            && (0.5..=1.5).contains(&self.contrast)
            && (0.0..=2.0).contains(&self.ambient_occlusion)
            && (0.0..=2.0).contains(&self.bounce_light)
            && (0.0..=2.0).contains(&self.lens_flare)
            && (0.0..=4.0).contains(&self.air_haze)
            && (0.0..=1.0).contains(&self.sharpen)
            && (0.0..=0.3).contains(&self.bloom)
            && (0.0..=1.0).contains(&self.vignette)
            && (0.0..=0.06).contains(&self.film_grain)
            && (0.0..=360.0).contains(&self.motion_blur_degrees)
    }
}

const fn caustics_strength_default() -> f32 {
    0.75
}

const fn immersion_enabled_default() -> bool {
    true
}

const fn shaft_strength_default() -> f32 {
    1.0
}

pub(super) fn comparison_hold_requested() -> bool {
    COMPARISON_HOLD.with(std::cell::Cell::get)
}

pub(super) fn graphics() -> PlaytestGraphics {
    GRAPHICS_LOADED.with(|loaded| {
        if !loaded.replace(true) {
            let saved = web_sys::window()
                .and_then(|window| window.local_storage().ok().flatten())
                .and_then(|storage| storage.get_item(GRAPHICS_PREFERENCE_KEY).ok().flatten())
                .and_then(|json| {
                    (json.len() <= 4096)
                        .then(|| serde_json::from_str::<PlaytestGraphics>(&json).ok())
                        .flatten()
                })
                .filter(|value| value.valid());
            if let Some(saved) = saved {
                GRAPHICS.with(|slot| slot.set(saved));
            }
        }
    });
    GRAPHICS.with(std::cell::Cell::get)
}

fn change_graphics(value: PlaytestGraphics, signal: RwSignal<PlaytestGraphics>) {
    if !value.valid() {
        return;
    }
    let temporal_changed =
        GRAPHICS.with(|slot| slot.replace(value).temporal_mode != value.temporal_mode);
    signal.set(value);
    if temporal_changed {
        set_temporal_status("Changement temporel demandé · en attente du moteur".into());
    }
    if let Some(storage) =
        web_sys::window().and_then(|window| window.local_storage().ok().flatten())
        && let Ok(json) = serde_json::to_string(&value)
        && storage.set_item(GRAPHICS_PREFERENCE_KEY, &json).is_err()
    {
        web_sys::console::warn_1(
            &"Réglages graphiques non mémorisés : stockage local indisponible".into(),
        );
    }
}

#[derive(Clone)]
struct RuntimeObservation {
    fixed_tick: u64,
    final_position_m: [f32; 3],
    grounded: bool,
    collisions: usize,
    simulation_ready: bool,
    runtime_status: String,
}

pub(super) fn exit_diagnostics(identity: PlaytestIdentity) -> Option<PlaytestExitDiagnostics> {
    LAST_RUNTIME.with(|slot| {
        slot.borrow()
            .as_ref()
            .map(|runtime| PlaytestExitDiagnostics {
                identity,
                fixed_tick: runtime.fixed_tick,
                final_position_m: runtime.final_position_m,
                grounded: runtime.grounded,
                collisions: runtime.collisions,
                simulation_ready: runtime.simulation_ready,
                runtime_status: runtime.runtime_status.clone(),
                end_reason: "retour_edition".into(),
            })
    })
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "The Leptos view keeps the playtest graphics controls in one ownership scope"
)]
pub(super) fn PlaytestStatus() -> impl IntoView {
    let metrics = RwSignal::new(false);
    let hold = RwSignal::new(false);
    let graphics = RwSignal::new(self::graphics());
    let (status, output) = signal("Mode édition · aucune simulation joueur".to_owned());
    let (temporal_status, temporal_output) =
        signal(TEMPORAL_STATUS.with(|slot| slot.borrow().clone()));
    OUTPUT.with(|slot| slot.replace(Some(output)));
    TEMPORAL_OUTPUT.with(|slot| slot.replace(Some(temporal_output)));
    on_cleanup(|| {
        METRICS.with(|value| value.set(false));
        COMPARISON_HOLD.with(|value| value.set(false));
        OUTPUT.with(|slot| slot.replace(None));
        TEMPORAL_OUTPUT.with(|slot| slot.replace(None));
    });
    view! {
        <p>"Tidewater : ZQSD / WASD pour avancer, Maj pour accélérer. Dans l’eau : Espace pour remonter, C ou Ctrl pour plonger. Échap libère la souris."</p>
        <details id="authoring-playtest-graphics"><summary>"Paramètres graphiques Tidewater (Playtest)"</summary>
            <p>"Réglages visuels mémorisés sur cet appareil ; ils ne modifient pas la carte."</p>
            <label><input id="authoring-graphics-auto-exposure" type="checkbox" prop:checked=move || graphics.get().auto_exposure
                on:change=move |event| { let mut next = graphics.get_untracked(); next.auto_exposure = event_target_checked(&event); change_graphics(next, graphics); }/>
                " Exposition automatique"</label>
            <label><input id="authoring-graphics-fxaa" type="checkbox" prop:checked=move || graphics.get().fxaa
                on:change=move |event| { let mut next = graphics.get_untracked(); next.fxaa = event_target_checked(&event); change_graphics(next, graphics); }/>
                " Anti-crénelage FXAA"</label>
            <label for="authoring-graphics-temporal">"Anti-crénelage temporel"</label>
            <select id="authoring-graphics-temporal" prop:value=move || graphics.get().temporal_mode.value()
                on:change=move |event| {
                    let mode = match event_target_value(&event).as_str() {
                        "off" => TemporalMode::Off,
                        "bevy_taa" => TemporalMode::BevyTaa,
                        "source_resolve" => TemporalMode::SourceResolve,
                        _ => return,
                    };
                    let mut next = graphics.get_untracked(); next.temporal_mode = mode; change_graphics(next, graphics);
                }>
                <option value="off">"Désactivé"</option>
                <option value="bevy_taa">"TAA standard"</option>
                <option value="source_resolve">"Tidewater · expérimental"</option>
            </select>
            <output id="authoring-graphics-temporal-status" aria-live="polite">{move || temporal_status.get()}</output>
            <label><input id="authoring-graphics-underwater" type="checkbox" prop:checked=move || graphics.get().underwater
                on:change=move |event| { let mut next = graphics.get_untracked(); next.underwater = event_target_checked(&event); change_graphics(next, graphics); }/>
                " Effets sous-marins"</label>
            <label>"Rayons sous-marins "{move || format!("{:.2}", graphics.get().underwater_shafts)}
                <input id="authoring-graphics-shafts" type="range" min="0" max="2" step="0.05" prop:value=move || graphics.get().underwater_shafts.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.underwater_shafts = value.clamp(0.0, 2.0); change_graphics(next, graphics); } }/>
            </label>
            <label><input id="authoring-graphics-underwater-lighting" type="checkbox" prop:checked=move || bool::from(graphics.get().underwater_lighting)
                on:change=move |event| { let mut next = graphics.get_untracked(); next.underwater_lighting = event_target_checked(&event).into(); change_graphics(next, graphics); }/>
                " Éclairage des surfaces immergées"</label>
            <label><input id="authoring-graphics-underwater-shadow-refraction" type="checkbox" prop:checked=move || bool::from(graphics.get().underwater_shadow_refraction)
                on:change=move |event| { let mut next = graphics.get_untracked(); next.underwater_shadow_refraction = event_target_checked(&event).into(); change_graphics(next, graphics); }/>
                " Réfraction des ombres"</label>
            <label><input id="authoring-graphics-water-refraction-capture" type="checkbox" prop:checked=move || bool::from(graphics.get().water_refraction_capture)
                on:change=move |event| { let mut next = graphics.get_untracked(); next.water_refraction_capture = event_target_checked(&event).into(); change_graphics(next, graphics); }/>
                " Fond visible à travers l’eau"</label>
            <label>"Caustiques "{move || format!("{:.2}", graphics.get().underwater_caustics)}
                <input id="authoring-graphics-caustics" type="range" min="0" max="4" step="0.05" prop:value=move || graphics.get().underwater_caustics.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.underwater_caustics = value.clamp(0.0, 4.0); change_graphics(next, graphics); } }/>
            </label>
            <label>"Exposition (EV) "{move || format!("{:.2}", graphics.get().exposure_ev)}
                <input id="authoring-graphics-exposure" type="range" min="-2" max="2" step="0.05" prop:value=move || graphics.get().exposure_ev.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.exposure_ev = value.clamp(-2.0, 2.0); change_graphics(next, graphics); } }/>
            </label>
            <label>"Saturation "{move || format!("{:.2}", graphics.get().saturation)}
                <input type="range" min="0" max="2" step="0.01" prop:value=move || graphics.get().saturation.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.saturation = value.clamp(0.0, 2.0); change_graphics(next, graphics); } }/>
            </label>
            <label>"Contraste "{move || format!("{:.2}", graphics.get().contrast)}
                <input type="range" min="0.5" max="1.5" step="0.01" prop:value=move || graphics.get().contrast.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.contrast = value.clamp(0.5, 1.5); change_graphics(next, graphics); } }/>
            </label>
            <label>"Occlusion ambiante "{move || format!("{:.2}", graphics.get().ambient_occlusion)}
                <input id="authoring-graphics-ao" type="range" min="0" max="2" step="0.05" prop:value=move || graphics.get().ambient_occlusion.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.ambient_occlusion = value.clamp(0.0, 2.0); change_graphics(next, graphics); } }/>
            </label>
            <label>"Lumière rebond "{move || format!("{:.2}", graphics.get().bounce_light)}
                <input id="authoring-graphics-bounce" type="range" min="0" max="2" step="0.05" prop:value=move || graphics.get().bounce_light.to_string()
                    disabled=move || { let _ = status.get(); super::authoring_assets::imported_buildings::ground_bounce_package().is_none() }
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.bounce_light = value.clamp(0.0, 2.0); change_graphics(next, graphics); } }/>
            </label>
            <label>"Netteté "{move || format!("{:.2}", graphics.get().sharpen)}
                <input type="range" min="0" max="1" step="0.01" prop:value=move || graphics.get().sharpen.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.sharpen = value.clamp(0.0, 1.0); change_graphics(next, graphics); } }/>
            </label>
            <label>"Bloom "{move || format!("{:.3}", graphics.get().bloom)}
                <input type="range" min="0" max="0.3" step="0.005" prop:value=move || graphics.get().bloom.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.bloom = value.clamp(0.0, 0.3); change_graphics(next, graphics); } }/>
            </label>
            <label>"Flare solaire "{move || format!("{:.2}", graphics.get().lens_flare)}
                <input id="authoring-graphics-flare" type="range" min="0" max="2" step="0.05" prop:value=move || graphics.get().lens_flare.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.lens_flare = value.clamp(0.0, 2.0); change_graphics(next, graphics); } }/>
            </label>
            <label>"Brume marine "{move || format!("{:.2}", graphics.get().air_haze)}
                <input id="authoring-graphics-haze" type="range" min="0" max="4" step="0.05" prop:value=move || graphics.get().air_haze.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.air_haze = value.clamp(0.0, 4.0); change_graphics(next, graphics); } }/>
            </label>
            <label>"Vignette "{move || format!("{:.2}", graphics.get().vignette)}
                <input type="range" min="0" max="1" step="0.01" prop:value=move || graphics.get().vignette.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.vignette = value.clamp(0.0, 1.0); change_graphics(next, graphics); } }/>
            </label>
            <label>"Grain photo "{move || format!("{:.3}", graphics.get().film_grain)}
                <input id="authoring-graphics-grain" type="range" min="0" max="0.06" step="0.001" prop:value=move || graphics.get().film_grain.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.film_grain = value.clamp(0.0, 0.06); change_graphics(next, graphics); } }/>
            </label>
            <label>"Flou de mouvement "{move || format!("{:.0}°", graphics.get().motion_blur_degrees)}
                <input id="authoring-graphics-motion-blur" type="range" min="0" max="360" step="18" prop:value=move || graphics.get().motion_blur_degrees.to_string()
                    on:input=move |event| { if let Ok(value) = event_target_value(&event).parse::<f32>() { let mut next = graphics.get_untracked(); next.motion_blur_degrees = value.clamp(0.0, 360.0); change_graphics(next, graphics); } }/>
            </label>
            <button type="button" on:click=move |_| change_graphics(PlaytestGraphics::DEFAULT, graphics)>"Réinitialiser les effets"</button>
        </details>
        <details><summary>"Diagnostics du rendu et du playtest"</summary>
            <label><input id="authoring-playtest-hold" type="checkbox" prop:checked=move || hold.get()
                disabled=move || { let _ = status.get(); LAST_RUNTIME.with(|slot| slot.borrow().as_ref().is_none_or(|runtime| !runtime.simulation_ready)) }
                on:change=move |event| { let checked = event_target_checked(&event); hold.set(checked); COMPARISON_HOLD.with(|value| value.set(checked)); }/>
                " Figer le joueur et les animations pour comparer les effets"</label>
            <label><input type="checkbox" prop:checked=move || metrics.get()
                on:change=move |event| { let checked = event_target_checked(&event); metrics.set(checked); METRICS.with(|value| value.set(checked)); }/>
                "Afficher les mesures dans la vue 3D"</label>
            <super::input_capture::InputCaptureStatus/>
            <output id="authoring-playtest-status">{move || status.get()}</output>
        </details>
    }
}

/// Human-readable runtime observation, not a persisted authoring document.
#[derive(Default)]
pub(super) struct MovementObservation {
    last_tick: u64,
    last_movement: Option<(u64, Vec3, Vec3, usize)>,
}

type CameraEffectsQuery<'w, 's> = Query<
    'w,
    's,
    (
        Option<&'static forge_tidewater::ambient_occlusion::AmbientOcclusionSettings>,
        Option<&'static forge_tidewater::lens_flare::LensFlare>,
        Option<&'static forge_engine::atmospheric_haze::AirHazeSettings>,
        Option<&'static bevy::post_process::motion_blur::MotionBlur>,
    ),
    bevy::ecs::query::With<forge_engine::ForgePlayerCamera>,
>;

#[derive(SystemParam)]
pub(super) struct PublishParams<'w, 's> {
    player: Option<Res<'w, forge_engine::PlayerRuntimeSnapshot>>,
    camera_grade: Query<
        'w,
        's,
        &'static bevy::render::view::ColorGrading,
        bevy::ecs::query::With<forge_engine::ForgePlayerCamera>,
    >,
    camera_effects: CameraEffectsQuery<'w, 's>,
    focus: Option<Res<'w, PlayerInputFocus>>,
    ready: Option<Res<'w, forge_engine::PlayerSimulationReady>>,
    imported_clouds: Option<Res<'w, crate::authoring_imported_terrain::LiveCloudTime>>,
    imported_ocean: Option<Res<'w, forge_tidewater::tidewater_ocean::Targets>>,
    ocean_geometry: Option<Res<'w, forge_tidewater::tidewater_ocean_geometry::ResidentGeometry>>,
    ocean_optics: Option<Res<'w, forge_tidewater::tidewater_ocean_optics::ResidentOptics>>,
    shore_sim: Option<Res<'w, forge_tidewater::tidewater_shore_sim::Targets>>,
    locomotion: Option<Res<'w, forge_tidewater::AmphibiousPlayerState>>,
    village_materials: Res<'w, Assets<forge_tidewater::tidewater_material::VillageMaterial>>,
    time: Res<'w, Time<Real>>,
}

#[allow(
    clippy::too_many_lines,
    reason = "The callback publishes one coherent public observation of player and render state."
)]
pub(super) fn publish(
    PublishParams {
        player,
        camera_grade,
        camera_effects,
        focus,
        ready,
        imported_clouds,
        imported_ocean,
        ocean_geometry,
        ocean_optics,
        shore_sim,
        locomotion,
        village_materials,
        time,
    }: PublishParams,
    mut last: Local<f64>,
    mut movement: Local<MovementObservation>,
) {
    let Some(player) = player else {
        *movement = MovementObservation::default();
        LAST_RUNTIME.with(|slot| slot.replace(None));
        return;
    };
    let now = time.elapsed_secs_f64();
    if player.fixed_tick < movement.last_tick {
        movement.last_movement = None;
    }
    if player.fixed_tick != movement.last_tick {
        let requested = player.desired_translation;
        if requested.x * requested.x + requested.z * requested.z > 1.0e-8 {
            movement.last_movement = Some((
                player.fixed_tick,
                requested,
                player.effective_translation,
                player.collisions,
            ));
        }
        movement.last_tick = player.fixed_tick;
    }
    if now - *last < 0.25 {
        return;
    }
    *last = now;
    let position = player.player_position;
    let simulation_ready = ready.as_ref().is_some_and(|value| value.0);
    let mut text = format!(
        "{} · Tick {} · commande {:?} · position (m) {:.2}, {:.2}, {:.2} · au sol {} · cap caméra (°) {:.1} · caméra {:?}",
        super::authoring_playtest::status(),
        player.fixed_tick,
        player.last_applied_sequence,
        position.x,
        position.y,
        position.z,
        player.grounded,
        player.camera_yaw_radians.to_degrees().rem_euclid(360.0),
        player.camera_mode
    );
    let _ = write!(
        text,
        " · entrée active {} · simulation prête {}",
        focus.is_some_and(|value| value.0),
        simulation_ready
    );
    if let Some(clouds) = imported_clouds {
        let _ = write!(text, " · nuages dynamiques {} images", clouds.submitted);
        if let Some((hours, reader)) = clouds.solar_readback {
            let _ = write!(
                text,
                " · soleil {hours:.2} h · retour atmosphère GPU terminé {reader:?}"
            );
        } else {
            text.push_str(" · éclairage solaire acquis, premier retour GPU en attente");
        }
    }
    if let Some(ocean) = imported_ocean {
        let steps = ocean
            .completed_steps
            .load(std::sync::atomic::Ordering::Acquire);
        let phase = if ocean.ready.load(std::sync::atomic::Ordering::Acquire) {
            "prête"
        } else {
            "initialisation"
        };
        let _ = write!(text, " · FFT océan {phase}, étape {steps}");
    }
    if let Some(geometry) = ocean_geometry {
        let _ = write!(text, " · géométrie océan {} nœuds", geometry.nodes);
    }
    if let Some(optics) = ocean_optics {
        let _ = write!(
            text,
            " · optique océan source, rive dérivée {}, étape {}",
            optics.shore_derived, optics.live_step
        );
    }
    if let Some(shore) = shore_sim {
        let steps = shore
            .completed_steps
            .load(std::sync::atomic::Ordering::Acquire);
        let phase = if shore.ready.load(std::sync::atomic::Ordering::Acquire) {
            "prêt"
        } else {
            "initialisation"
        };
        let _ = write!(text, " · ressac ShoreSim {phase}, étape {steps}");
    }
    if let Some(player) = locomotion {
        let mode = match player.motion.mode {
            forge_tidewater::amphibious_locomotion::Mode::Walk => "marche",
            forge_tidewater::amphibious_locomotion::Mode::Swim => "nage",
        };
        let _ = write!(text, " · mode Tidewater {mode}");
    }
    if let Some((_, material)) = village_materials
        .iter()
        .find(|(_, material)| material.ground_bounce.is_some())
    {
        let _ = write!(
            text,
            " · lumière rebond source, intensité {:.2}",
            material.bounce_settings.x
        );
    }
    if let Ok((ao, flare, haze, blur)) = camera_effects.single() {
        if let Some(ao) = ao {
            let _ = write!(text, " · occlusion ambiante {:.2}", ao.strength());
        }
        if let Some(flare) = flare {
            let _ = write!(text, " · flare solaire {:.2}", flare.strength());
        }
        if let Some(haze) = haze {
            let _ = write!(text, " · brume marine {:.2}", haze.density());
        }
        if let Some(blur) = blur {
            let _ = write!(
                text,
                " · flou de mouvement {:.0}°",
                blur.shutter_angle * 360.0
            );
        } else {
            text.push_str(" · flou de mouvement arrêté");
        }
    }
    if let Ok(grade) = camera_grade.single() {
        let _ = write!(
            text,
            " · exposition appliquée {:.2} EV",
            grade.global.exposure
        );
    }
    if let Some((tick, requested, effective, contacts)) = movement.last_movement {
        let _ = write!(
            text,
            " · dernier mouvement observé tick {tick} : demandé X/Z ({:.4}, {:.4}) m, résolu ({:.4}, {:.4}) m, {contacts} contact(s) (sol inclus)",
            requested.x, requested.z, effective.x, effective.z
        );
    } else {
        text.push_str(" · aucun mouvement horizontal observé depuis le démarrage");
    }
    LAST_RUNTIME.with(|slot| {
        slot.replace(Some(RuntimeObservation {
            fixed_tick: player.fixed_tick,
            final_position_m: [position.x, position.y, position.z],
            grounded: player.grounded,
            collisions: player.collisions,
            simulation_ready,
            runtime_status: text.clone(),
        }));
    });
    OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(text);
        }
    });
}

pub(super) fn set_playing(playing: bool) {
    let changed = PLAYING.with(|value| value.replace(playing) != playing);
    if changed {
        COMPARISON_HOLD.with(|value| value.set(false));
        // A runtime observation belongs to exactly one playtest lifetime.  In
        // particular, never let a quick stop after a new start relabel the
        // previous session's position and contacts with the new identity.
        LAST_RUNTIME.with(|slot| slot.replace(None));
        set_temporal_status(if playing {
            "Rendu temporel · en attente de la caméra joueur".into()
        } else {
            "Mode édition · rendu temporel du playtest arrêté".into()
        });
    }
}

pub(super) fn is_playing() -> bool {
    PLAYING.with(std::cell::Cell::get)
}

pub(super) fn set_temporal_status(value: String) {
    let changed = TEMPORAL_STATUS.with(|slot| {
        if *slot.borrow() == value {
            return false;
        }
        slot.replace(value.clone());
        true
    });
    if changed {
        TEMPORAL_OUTPUT.with(|slot| {
            if let Some(output) = *slot.borrow() {
                output.set(value);
            }
        });
    }
}

#[cfg(test)]
pub(crate) fn reset_for_test() {
    PLAYING.with(|slot| slot.set(false));
    GRAPHICS.with(|slot| slot.set(PlaytestGraphics::DEFAULT));
    GRAPHICS_LOADED.with(|slot| slot.set(true));
    TEMPORAL_STATUS.with(|slot| slot.borrow_mut().clear());
}

#[cfg(test)]
pub(crate) fn set_playing_for_test(playing: bool) {
    PLAYING.with(|slot| slot.set(playing));
}

#[cfg(test)]
pub(crate) fn set_mode_for_test(mode: TemporalMode) {
    GRAPHICS.with(|slot| {
        let mut profile = slot.get();
        profile.temporal_mode = mode;
        slot.set(profile);
    });
}

pub(super) fn capture() {
    if !PLAYING.with(std::cell::Cell::get) {
        return;
    }
    if let Some(canvas) = web_sys::window()
        .and_then(|window| window.document())
        .and_then(|document| document.get_element_by_id("forge-authoring-canvas"))
    {
        super::input_capture::begin_request();
        super::input_capture::focus_then_request_pointer_lock(&canvas);
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
pub(super) fn update_focus(
    mut focus: ResMut<PlayerInputFocus>,
    mut overlay: ResMut<forge_engine::DiagnosticsOverlaySettings>,
    ready: Option<Res<forge_engine::PlayerSimulationReady>>,
) {
    let visible = METRICS.with(std::cell::Cell::get);
    if overlay.visible != visible {
        overlay.visible = visible;
    }
    focus.0 = ready.is_some_and(|ready| ready.0)
        && web_sys::window()
            .and_then(|window| window.document())
            .is_some_and(|document| {
                document.has_focus().unwrap_or(false)
                    && document
                        .pointer_lock_element()
                        .is_some_and(|element| element.id() == "forge-authoring-canvas")
            });
    let focused = web_sys::window()
        .and_then(|window| window.document())
        .is_some_and(|document| document.has_focus().unwrap_or(false));
    super::input_capture::observe(focus.0, focused);
}
