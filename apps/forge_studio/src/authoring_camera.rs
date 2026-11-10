//! Transient editor navigation. Never writes a terrain command or controls the player camera.
use bevy::prelude::*;
use leptos::prelude::*;
use std::cell::RefCell;

type EditorCameraFilter = (
    forge_engine::source_sky::preview::SceneCameraFilter,
    Without<forge_engine::ForgePlayerCamera>,
);

#[derive(Default)]
struct Navigation {
    last: Option<(i32, i32)>,
    orbit: Vec2,
    pan: Vec2,
    zoom: f32,
    reset: bool,
    preview: Option<PreviewNavigation>,
}

enum PreviewNavigation {
    Frame(Vec3, f32),
    FrameFrom(Vec3, Vec3),
    Focus(Vec3, Vec3),
    Leave,
}

thread_local! {
    static INPUT: RefCell<Navigation> = RefCell::new(Navigation::default());
}

pub(super) fn cancel() {
    INPUT.with(|input| *input.borrow_mut() = Navigation::default());
}

pub(super) fn frame_preview(pivot: Vec3, distance: f32) {
    INPUT
        .with(|input| input.borrow_mut().preview = Some(PreviewNavigation::Frame(pivot, distance)));
}

pub(super) fn frame_preview_from(pivot: Vec3, eye: Vec3) {
    if pivot.is_finite() && eye.is_finite() && (eye - pivot).xz().length_squared() > 0.0001 {
        INPUT.with(|input| {
            input.borrow_mut().preview = Some(PreviewNavigation::FrameFrom(pivot, eye))
        });
    }
}

pub(super) fn leave_preview() {
    INPUT.with(|input| input.borrow_mut().preview = Some(PreviewNavigation::Leave));
}

/// Focus a real object without entering the disposable-asset preview camera mode.
pub(super) fn focus_object(pivot: Vec3, distance: f32) {
    if pivot.is_finite() && distance.is_finite() && distance > 0.0 {
        focus_from(
            pivot,
            pivot + Vec3::new(0.6, 0.3, 1.0).normalize() * distance,
        );
    }
}

/// Frame an accepted object from its own facade, without changing scene data.
pub(super) fn focus_from(pivot: Vec3, eye: Vec3) {
    let offset = eye - pivot;
    if pivot.is_finite() && eye.is_finite() && offset.xz().length_squared() > 0.0001 {
        INPUT.with(|input| {
            input.borrow_mut().preview = Some(PreviewNavigation::Focus(pivot, eye));
        });
    }
}

#[component]
pub(super) fn CameraControls() -> impl IntoView {
    view! {
        <div aria-label="Navigation de la vue">
            <button on:click=move |_| INPUT.with(|input| input.borrow_mut().zoom -= 0.2)>"Rapprocher"</button>
            <button on:click=move |_| INPUT.with(|input| input.borrow_mut().zoom += 0.2)>"Éloigner"</button>
            <button on:click=move |_| INPUT.with(|input| input.borrow_mut().reset = true)>"Vue d’ensemble"</button>
            <details class="camera-help"><summary>"Déplacer la vue"</summary>
                <p id="forge-camera-help">"Dans la vue 3D : flèches pour tourner, Maj + flèches pour déplacer, + et − pour zoomer, Origine pour la vue d’ensemble. Souris : clic droit pour tourner, clic central pour déplacer ; pavé tactile : défilement pour zoomer."</p>
                <button aria-label="Tourner à gauche" on:click=move |_| step(Vec2::new(-20.0,0.0),false)>"↶ Gauche"</button>
                <button aria-label="Tourner à droite" on:click=move |_| step(Vec2::new(20.0,0.0),false)>"Droite ↷"</button>
                <button aria-label="Incliner vers le haut" on:click=move |_| step(Vec2::new(0.0,-20.0),false)>"Incliner ↑"</button>
                <button aria-label="Incliner vers le bas" on:click=move |_| step(Vec2::new(0.0,20.0),false)>"Incliner ↓"</button>
                <button on:click=move |_| step(Vec2::new(-20.0,0.0),true)>"Déplacer ←"</button>
                <button on:click=move |_| step(Vec2::new(20.0,0.0),true)>"Déplacer →"</button>
                <button on:click=move |_| step(Vec2::new(0.0,-20.0),true)>"Déplacer ↑"</button>
                <button on:click=move |_| step(Vec2::new(0.0,20.0),true)>"Déplacer ↓"</button>
            </details>
        </div>
    }
}

fn step(delta: Vec2, pan: bool) {
    INPUT.with(|input| {
        let mut input = input.borrow_mut();
        if pan {
            input.pan += delta;
        } else {
            input.orbit += delta;
        }
    });
}

/// Only called by the focused canvas: text fields retain all editing keys.
pub(super) fn key(event: &web_sys::KeyboardEvent) -> bool {
    if event.ctrl_key()
        || event.alt_key()
        || event.meta_key()
        || event.is_composing()
        || !super::authoring_viewport::can_preview_asset()
    {
        return false;
    }
    let delta = match event.key().as_str() {
        "ArrowLeft" => Some(Vec2::new(-20.0, 0.0)),
        "ArrowRight" => Some(Vec2::new(20.0, 0.0)),
        "ArrowUp" => Some(Vec2::new(0.0, -20.0)),
        "ArrowDown" => Some(Vec2::new(0.0, 20.0)),
        "+" | "=" => {
            INPUT.with(|i| i.borrow_mut().zoom -= 0.1);
            None
        }
        "-" | "−" => {
            INPUT.with(|i| i.borrow_mut().zoom += 0.1);
            None
        }
        "Home" => {
            INPUT.with(|i| i.borrow_mut().reset = true);
            None
        }
        _ => return false,
    };
    if let Some(delta) = delta {
        step(delta, event.shift_key());
    }
    event.prevent_default();
    event.stop_propagation();
    true
}

pub(super) fn press(event: &web_sys::MouseEvent) {
    if !matches!(event.button(), 1 | 2) {
        return;
    }
    super::authoring_brush_pointer::suspend();
    INPUT.with(|input| input.borrow_mut().last = Some((event.client_x(), event.client_y())));
    event.prevent_default();
}

#[allow(clippy::cast_possible_truncation, reason = "bounded wheel delta")]
pub(super) fn wheel(event: &web_sys::WheelEvent) {
    if !super::authoring_viewport::can_preview_asset() {
        return;
    }
    let delta = event.delta_y();
    if delta.is_finite() {
        INPUT.with(|input| input.borrow_mut().zoom += (delta * 0.001).clamp(-0.3, 0.3) as f32);
        event.prevent_default();
    }
}

#[allow(clippy::cast_precision_loss, reason = "bounded browser pixel deltas")]
pub(super) fn motion(event: &web_sys::MouseEvent) {
    INPUT.with(|input| {
        let mut input = input.borrow_mut();
        let point = (event.client_x(), event.client_y());
        let last = input.last.replace(point);
        if let Some((x, y)) = last {
            let delta = Vec2::new(
                (point.0.saturating_sub(x)).clamp(-100, 100) as f32,
                (point.1.saturating_sub(y)).clamp(-100, 100) as f32,
            );
            if event.buttons() & 2 != 0 {
                input.orbit += delta;
            } else if event.buttons() & 4 != 0 {
                input.pan += delta;
            }
        }
    });
}

#[derive(Default)]
pub(super) struct CameraState {
    initial: Option<Transform>,
    pivot: Vec3,
    before_preview: Option<(Transform, Vec3)>,
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn navigate(
    mut cameras: Query<&mut Transform, EditorCameraFilter>,
    terrain: Option<Res<forge_engine::TerrainRuntimeState>>,
    sampled: Option<Res<forge_engine::sampled_terrain_physics::SampledTerrainPhysicsState>>,
    streamed: Option<Res<forge_engine::cell_streaming::ResidentSampledTerrain>>,
    player: Option<Res<forge_engine::PlayerRuntimeSnapshot>>,
    mut observer: Option<ResMut<forge_engine::cell_streaming::CellStreamingObserver>>,
    mut state: Local<CameraState>,
) {
    if player.is_some() {
        cancel();
        return;
    }
    let Ok(mut camera) = cameras.single_mut() else {
        return;
    };
    let plan = sampled
        .as_ref()
        .map(|source| source.plan.as_ref())
        .or_else(|| streamed.as_ref().map(|source| source.plan().as_ref()));
    let bounds = match CameraBounds::new(terrain.as_deref(), plan) {
        Ok(bounds) => bounds,
        Err(error) => {
            super::authoring_viewport::report(format!(
                "Déplacement de la vue indisponible : {error}"
            ));
            return;
        }
    };
    navigate_camera(&mut camera, &mut state, bounds);
    if let Some(observer) = observer.as_mut() {
        observer.position_xz_m = [f64::from(state.pivot.x), f64::from(state.pivot.z)];
        for (index, coordinate) in observer.position_xz_m.iter_mut().enumerate() {
            let minimum = f64::from(bounds.minimum[index]);
            let maximum = f64::from(bounds.maximum[index]);
            *coordinate = coordinate.clamp(minimum, maximum - (maximum - minimum) * 0.000_001);
        }
    }
    super::authoring_compass::publish_camera(&camera);
}

#[derive(Clone, Copy)]
struct CameraBounds {
    minimum: Vec2,
    maximum: Vec2,
    maximum_distance: f32,
    pivot: Vec3,
}

impl CameraBounds {
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Validated finite raster and cell-policy coordinates"
    )]
    fn new(
        procedural: Option<&forge_engine::TerrainRuntimeState>,
        sampled: Option<&forge_world::sampled_terrain::SampledTerrainPlan>,
    ) -> Result<Self, String> {
        if let Some(plan) = sampled {
            let source = plan.raster().source();
            let size = Vec2::new(
                plan.document().extent_m.size_x_m,
                plan.document().extent_m.size_z_m,
            );
            let minimum = Vec2::new(source.origin_xz_m[0] as f32, source.origin_xz_m[1] as f32);
            let center = minimum + size * 0.5;
            let height = plan
                .height_at(f64::from(center.x), f64::from(center.y))
                .map_err(|error| error.to_string())? as f32;
            Ok(Self {
                minimum,
                maximum: minimum + size,
                maximum_distance: size.max_element() * 2.0,
                pivot: Vec3::new(center.x, height, center.y),
            })
        } else {
            Ok(Self {
                minimum: Vec2::splat(-256.0),
                maximum: Vec2::splat(256.0),
                maximum_distance: 1000.0,
                pivot: Vec3::Y
                    * procedural.map_or(0.0, |source| {
                        source.document().height_sampling.base_elevation_m
                    }),
            })
        }
    }
}

fn navigate_camera(camera: &mut Transform, state: &mut CameraState, bounds: CameraBounds) {
    if state.initial.is_none() {
        state.initial = Some(*camera);
        state.pivot = bounds.pivot;
    }
    let input = INPUT.with(|input| {
        let mut input = input.borrow_mut();
        let last = input.last;
        let pending = std::mem::take(&mut *input);
        input.last = last;
        pending
    });
    if let Some(preview) = input.preview {
        let focus = matches!(preview, PreviewNavigation::Focus(..));
        let framing = match preview {
            PreviewNavigation::Frame(pivot, distance) => Some((
                pivot,
                pivot + Vec3::new(0.6, 0.3, 1.0).normalize() * distance,
            )),
            PreviewNavigation::FrameFrom(pivot, eye) | PreviewNavigation::Focus(pivot, eye) => {
                Some((pivot, eye))
            }
            PreviewNavigation::Leave => None,
        };
        if let Some((pivot, eye)) = framing {
            if focus {
                state.before_preview = None;
            } else if state.before_preview.is_none() {
                state.before_preview = Some((*camera, state.pivot));
            }
            state.pivot = pivot;
            *camera = Transform::from_translation(eye).looking_at(pivot, Vec3::Y);
        } else if let Some((previous, pivot)) = state.before_preview.take() {
            *camera = previous;
            state.pivot = pivot;
        }
        return;
    }
    if input.reset {
        state.before_preview = None;
        if let Some(initial) = state.initial {
            *camera = initial;
        }
        state.initial = None;
        state.pivot = bounds.pivot;
        return;
    }
    if input.orbit == Vec2::ZERO && input.pan == Vec2::ZERO && input.zoom == 0.0 {
        return;
    }
    let offset = camera.translation - state.pivot;
    let distance = offset.length().max(4.0);
    let yaw = offset.x.atan2(offset.z) - input.orbit.x.clamp(-200.0, 200.0) * 0.005;
    let pitch = ((offset.y / distance).clamp(-1.0, 1.0).asin()
        + input.orbit.y.clamp(-200.0, 200.0) * 0.005)
        .clamp(0.05, 1.5);
    let right = Vec3::new(yaw.cos(), 0.0, -yaw.sin());
    let forward = Vec3::new(-yaw.sin(), 0.0, -yaw.cos());
    state.pivot += (right * -input.pan.x.clamp(-200.0, 200.0)
        + forward * input.pan.y.clamp(-200.0, 200.0))
        * distance
        * 0.002;
    state.pivot.x = state.pivot.x.clamp(bounds.minimum.x, bounds.maximum.x);
    state.pivot.z = state.pivot.z.clamp(bounds.minimum.y, bounds.maximum.y);
    let distance =
        (distance * input.zoom.clamp(-1.0, 1.0).exp()).clamp(4.0, bounds.maximum_distance.max(4.0));
    camera.translation = state.pivot
        + distance
            * Vec3::new(
                pitch.cos() * yaw.sin(),
                pitch.sin(),
                pitch.cos() * yaw.cos(),
            );
    camera.look_at(state.pivot, Vec3::Y);
}
