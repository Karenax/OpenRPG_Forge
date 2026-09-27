//! Explicit, frozen diagnostic view; never a saved camera edit or a performance benchmark.
use bevy::prelude::*;
use forge_world::{WorldSketchCamera, WorldSketchDocument};

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum FixedProofViewError {
    InvalidDocument(String),
    MissingCamera(String),
    InvalidDirection,
    InvalidTopology,
    NotPerspective,
}

impl std::fmt::Display for FixedProofViewError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::InvalidDocument(error) => write!(f, "Invalid proof view document: {error}"),
            Self::MissingCamera(id) => write!(f, "Unknown authored proof camera: {id}"),
            Self::InvalidDirection => f.write_str("Invalid proof camera direction/up axis"),
            Self::InvalidTopology => {
                f.write_str("Fixed proof view requires exactly one active camera")
            }
            Self::NotPerspective => f.write_str("Fixed proof view requires a perspective camera"),
        }
    }
}
impl std::error::Error for FixedProofViewError {}

#[derive(Clone, Resource)]
struct FixedProofView(WorldSketchCamera);

/// Bounded diagnostic clock, never saved into a document or used for benchmarks.
#[derive(Default, Resource)]
pub struct FixedProofClock {
    pending: bool,
    pub steps: u8,
    pub last_error: Option<&'static str>,
}

impl FixedProofClock {
    /// Coalesces a repeated pending request; at most eight seconds per reload.
    pub fn request_step(&mut self) -> bool {
        if self.pending || self.steps >= 32 {
            return false;
        }
        self.pending = true;
        true
    }
}

/// Observes only the main-world camera installation, not GPU presentation.
#[derive(Clone, Debug, Resource)]
pub struct FixedProofViewStatus {
    pub camera_id: String,
    pub installed: bool,
    pub error: Option<FixedProofViewError>,
    /// Consecutive main-world observations, not `RenderApp` frames or GPU readiness.
    pub stable_main_frames: u32,
    pub current_view_matches: bool,
    pub virtual_elapsed_ns: u128,
    pub fixed_elapsed_ns: u128,
}

/// Freezes virtual time from startup and reuses the sole active camera.
pub struct FixedProofViewPlugin(FixedProofView);

impl FixedProofViewPlugin {
    pub fn new(
        document: &WorldSketchDocument,
        camera_id: &str,
    ) -> Result<Self, FixedProofViewError> {
        document
            .validate()
            .map_err(|error| FixedProofViewError::InvalidDocument(error.to_string()))?;
        let camera = document
            .validation_cameras
            .iter()
            .find(|view| view.id == camera_id)
            .ok_or_else(|| FixedProofViewError::MissingCamera(camera_id.to_owned()))?;
        let direction = Vec3::from_array(camera.target_m) - Vec3::from_array(camera.position_m);
        let direction = direction
            .try_normalize()
            .ok_or(FixedProofViewError::InvalidDirection)?;
        if direction.cross(Vec3::Y).length_squared() < 1.0e-6 {
            return Err(FixedProofViewError::InvalidDirection);
        }
        Ok(Self(FixedProofView(camera.clone())))
    }
}

impl Plugin for FixedProofViewPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(self.0.clone())
            .init_resource::<FixedProofClock>()
            .insert_resource(FixedProofViewStatus {
                camera_id: self.0.0.id.clone(),
                installed: false,
                error: None,
                stable_main_frames: 0,
                current_view_matches: false,
                virtual_elapsed_ns: 0,
                fixed_elapsed_ns: 0,
            })
            .add_systems(Startup, freeze_virtual_time)
            .add_systems(PostStartup, install_fixed_view)
            .add_systems(Last, observe_fixed_view_stability)
            .add_systems(First, step_proof_clock.after(bevy::time::TimeSystems));
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observation parameters")]
fn observe_fixed_view_stability(
    view: Res<FixedProofView>,
    mut status: ResMut<FixedProofViewStatus>,
    virtual_time: Res<Time<Virtual>>,
    fixed_time: Res<Time<Fixed>>,
    cameras: Query<(&Camera, &Transform, &Projection), With<Camera3d>>,
) {
    let expected = Transform::from_translation(Vec3::from_array(view.0.position_m))
        .looking_at(Vec3::from_array(view.0.target_m), Vec3::Y);
    let mut active = cameras.iter().filter(|(camera, _, _)| camera.is_active);
    let matches = active.next().is_some_and(|(_, transform, projection)| {
        *transform == expected
            && matches!(projection, Projection::Perspective(p)
                if p.fov.to_bits() == view.0.vertical_fov_degrees.to_radians().to_bits()
                && p.near.to_bits() == view.0.near_m.to_bits()
                && p.far.to_bits() == view.0.far_m.to_bits())
    }) && active.next().is_none();
    let virtual_ns = virtual_time.elapsed().as_nanos();
    let fixed_ns = fixed_time.elapsed().as_nanos();
    let valid = matches && status.installed && status.error.is_none() && virtual_time.is_paused();
    let previous_stable_frames = status.stable_main_frames;
    status.stable_main_frames = if !valid {
        0
    } else if status.current_view_matches
        && status.virtual_elapsed_ns == virtual_ns
        && status.fixed_elapsed_ns == fixed_ns
    {
        status.stable_main_frames.saturating_add(1)
    } else {
        1
    };
    status.current_view_matches = valid;
    status.virtual_elapsed_ns = virtual_ns;
    status.fixed_elapsed_ns = fixed_ns;
    if status.stable_main_frames == 30
        || (previous_stable_frames >= 30 && status.stable_main_frames < 30)
    {
        info!(target: "forge::proof", event_kind = "fixed_view_stability_observed",
            camera_id = %status.camera_id, current_view_matches = status.current_view_matches,
            stable_main_frames = status.stable_main_frames,
            virtual_elapsed_ns = %virtual_ns, fixed_elapsed_ns = %fixed_ns,
            gpu_readiness_verified = false, "Main-world frozen view observation; not a GPU receipt");
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn step_proof_clock(
    status: Res<FixedProofViewStatus>,
    view: Res<FixedProofView>,
    mut clock: ResMut<FixedProofClock>,
    mut virtual_time: ResMut<Time<Virtual>>,
    mut time: ResMut<Time>,
    cameras: Query<(&Camera, &Transform, &Projection), With<Camera3d>>,
) {
    if !std::mem::take(&mut clock.pending) {
        return;
    }
    let mut active = cameras.iter().filter(|(camera, _, _)| camera.is_active);
    let camera = active.next();
    let expected = Transform::from_translation(Vec3::from_array(view.0.position_m))
        .looking_at(Vec3::from_array(view.0.target_m), Vec3::Y);
    if !status.installed
        || status.error.is_some()
        || !virtual_time.is_paused()
        || camera.is_none_or(|(_, transform, projection)| {
            *transform != expected
                || !matches!(projection, Projection::Perspective(p)
                if p.fov.to_bits() == view.0.vertical_fov_degrees.to_radians().to_bits()
                && p.near.to_bits() == view.0.near_m.to_bits()
                && p.far.to_bits() == view.0.far_m.to_bits())
        })
        || active.next().is_some()
        || clock.steps >= 32
    {
        clock.last_error = Some("Pas refusé : vue fixe unique et temps en pause requis");
        return;
    }
    virtual_time.advance_by(std::time::Duration::from_millis(250));
    *time = virtual_time.as_generic();
    clock.steps += 1;
    clock.last_error = None;
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
fn freeze_virtual_time(mut time: ResMut<Time<Virtual>>) {
    time.pause();
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
fn install_fixed_view(
    view: Res<FixedProofView>,
    mut status: ResMut<FixedProofViewStatus>,
    mut cameras: Query<(&Camera, &mut Transform, &mut Projection), With<Camera3d>>,
) {
    if cameras
        .iter()
        .filter(|(camera, _, _)| camera.is_active)
        .count()
        != 1
    {
        status.error = Some(FixedProofViewError::InvalidTopology);
        return;
    }
    for (camera, mut transform, mut projection) in &mut cameras {
        if !camera.is_active {
            continue;
        }
        let Projection::Perspective(perspective) = &mut *projection else {
            status.error = Some(FixedProofViewError::NotPerspective);
            return;
        };
        let source = &view.0;
        *transform = Transform::from_translation(Vec3::from_array(source.position_m))
            .looking_at(Vec3::from_array(source.target_m), Vec3::Y);
        perspective.fov = source.vertical_fov_degrees.to_radians();
        perspective.near = source.near_m;
        perspective.far = source.far_m;
        status.installed = true;
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn document() -> WorldSketchDocument {
        forge_world::embedded_visual_valley_world_sketch().unwrap()
    }
    #[test]
    fn clock_steps_exactly_once_stays_paused_and_refuses_moved_camera() {
        let document = document();
        let mut app = App::new();
        app.add_plugins(MinimalPlugins).add_plugins(
            FixedProofViewPlugin::new(&document, &document.validation_cameras[0].id).unwrap(),
        );
        app.insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
            std::time::Duration::from_millis(16),
        ));
        let entity = app
            .world_mut()
            .spawn((
                Camera3d::default(),
                Camera::default(),
                Transform::default(),
                Projection::default(),
            ))
            .id();
        app.update();
        let original = *app.world().get::<Transform>(entity).unwrap();
        assert!(
            app.world_mut()
                .resource_mut::<FixedProofClock>()
                .request_step()
        );
        assert!(
            !app.world_mut()
                .resource_mut::<FixedProofClock>()
                .request_step()
        );
        app.update();
        for _ in 0..4 {
            app.update();
        }
        assert_eq!(
            app.world().resource::<Time<Virtual>>().elapsed(),
            std::time::Duration::from_millis(250)
        );
        assert_eq!(
            app.world().resource::<Time>().elapsed(),
            std::time::Duration::from_millis(250)
        );
        assert!(app.world().resource::<Time<Virtual>>().is_paused());
        app.world_mut()
            .get_mut::<Transform>(entity)
            .unwrap()
            .translation
            .x += 1.;
        assert!(
            app.world_mut()
                .resource_mut::<FixedProofClock>()
                .request_step()
        );
        app.update();
        assert_eq!(app.world().resource::<FixedProofClock>().steps, 1);
        assert!(
            app.world()
                .resource::<FixedProofClock>()
                .last_error
                .is_some()
        );
        *app.world_mut().get_mut::<Transform>(entity).unwrap() = original;
        for _ in 1..32 {
            assert!(
                app.world_mut()
                    .resource_mut::<FixedProofClock>()
                    .request_step()
            );
            app.update();
        }
        assert!(
            !app.world_mut()
                .resource_mut::<FixedProofClock>()
                .request_step()
        );
        assert_eq!(
            app.world().resource::<Time<Virtual>>().elapsed(),
            std::time::Duration::from_secs(8)
        );
        assert_eq!(*app.world().get::<Transform>(entity).unwrap(), original);
    }
    #[test]
    fn fixed_view_reuses_camera_freezes_animation_and_preserves_source() {
        let document = document();
        let original = document.clone();
        let view = &document.validation_cameras[0];
        let mut app = App::new();
        app.add_plugins(MinimalPlugins)
            .add_plugins(FixedProofViewPlugin::new(&document, &view.id).unwrap());
        app.insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
            std::time::Duration::from_millis(16),
        ));
        let entity = app
            .world_mut()
            .spawn((
                Camera3d::default(),
                Camera::default(),
                Transform::default(),
                Projection::Perspective(PerspectiveProjection::default()),
            ))
            .id();
        for _ in 0..4 {
            app.update();
        }
        assert!(app.world().resource::<FixedProofViewStatus>().installed);
        assert!(app.world().resource::<Time<Virtual>>().is_paused());
        assert_eq!(
            app.world().resource::<Time<Virtual>>().elapsed(),
            std::time::Duration::ZERO
        );
        assert_eq!(
            app.world().resource::<Time>().elapsed(),
            std::time::Duration::ZERO
        );
        assert!(
            app.world().resource::<Time<Real>>().elapsed() >= std::time::Duration::from_millis(48)
        );
        // Freezing simulation must not freeze renderer frame identity or real-time deadlines.
        assert_eq!(app.world().resource::<bevy::diagnostic::FrameCount>().0, 4);
        assert_eq!(
            app.world().get::<Transform>(entity).unwrap().translation,
            Vec3::from_array(view.position_m)
        );
        let Projection::Perspective(projection) = app.world().get::<Projection>(entity).unwrap()
        else {
            panic!()
        };
        assert_eq!(
            projection.fov.to_bits(),
            view.vertical_fov_degrees.to_radians().to_bits()
        );
        assert_eq!((projection.near, projection.far), (view.near_m, view.far_m));
        assert_eq!(
            app.world_mut()
                .query::<&Camera3d>()
                .iter(app.world())
                .count(),
            1
        );
        assert_eq!(document, original);
        assert_eq!(
            app.world()
                .resource::<FixedProofViewStatus>()
                .stable_main_frames,
            4
        );
        app.world_mut()
            .get_mut::<Transform>(entity)
            .unwrap()
            .translation
            .x += 1.0;
        app.update();
        let status = app.world().resource::<FixedProofViewStatus>();
        assert!(!status.current_view_matches);
        assert_eq!(status.stable_main_frames, 0);
        assert_eq!(status.virtual_elapsed_ns, 0);
        assert_eq!(status.fixed_elapsed_ns, 0);
    }
    #[test]
    fn fixed_view_rejects_missing_camera_and_ambiguous_topology() {
        let document = document();
        assert!(FixedProofViewPlugin::new(&document, "not-authored").is_err());
        for count in [0, 2] {
            let mut app = App::new();
            app.add_plugins(MinimalPlugins).add_plugins(
                FixedProofViewPlugin::new(&document, &document.validation_cameras[0].id).unwrap(),
            );
            for _ in 0..count {
                app.world_mut().spawn((
                    Camera3d::default(),
                    Camera::default(),
                    Transform::default(),
                    Projection::default(),
                ));
            }
            app.update();
            let status = app.world().resource::<FixedProofViewStatus>();
            assert!(!status.installed);
            assert!(status.error.is_some());
        }
    }
}
