//! Transient editor navigation. Never writes a terrain command or controls the player camera.
use bevy::prelude::*;
use leptos::prelude::*;
use std::cell::RefCell;

type EditorCameraFilter = (
    forge_engine::tidewater_sky::preview::SceneCameraFilter,
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
            <span>"Clic droit : tourner · clic central : déplacer la vue"</span>
        </div>
    }
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
    player: Option<Res<forge_engine::PlayerRuntimeSnapshot>>,
    mut state: Local<CameraState>,
) {
    if player.is_some() {
        cancel();
        return;
    }
    let Ok(mut camera) = cameras.single_mut() else {
        return;
    };
    super::authoring_compass::publish_camera(&camera);
    if state.initial.is_none() {
        state.initial = Some(*camera);
        state.pivot.y = terrain.as_ref().map_or(0.0, |terrain| {
            terrain.document().height_sampling.base_elevation_m
        });
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
            PreviewNavigation::Focus(pivot, eye) => Some((pivot, eye)),
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
        state.pivot = Vec3::ZERO;
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
    let (half_x, half_z, maximum_distance) =
        sampled.as_ref().map_or((256.0, 256.0, 1000.0), |terrain| {
            let extent = &terrain.plan.document().extent_m;
            (
                extent.size_x_m * 0.5,
                extent.size_z_m * 0.5,
                extent.size_x_m.max(extent.size_z_m) * 2.0,
            )
        });
    state.pivot.x = state.pivot.x.clamp(-half_x, half_x);
    state.pivot.z = state.pivot.z.clamp(-half_z, half_z);
    let distance = (distance * input.zoom.clamp(-1.0, 1.0).exp()).clamp(4.0, maximum_distance);
    camera.translation = state.pivot
        + distance
            * Vec3::new(
                pitch.cos() * yaw.sin(),
                pitch.sin(),
                pitch.cos() * yaw.cos(),
            );
    camera.look_at(state.pivot, Vec3::Y);
}
