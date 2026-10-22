//! Presentation adapter; authored coordinates never contain engine handles.
use super::GameplayRuntime;
use crate::ForgePlayerCamera;
use bevy::prelude::*;

type SavedCamera = Option<(Entity, Transform, Projection)>;

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn project(
    runtime: Res<GameplayRuntime>,
    mut cameras: Query<(Entity, &mut Transform, &mut Projection), With<ForgePlayerCamera>>,
    mut previous: Local<SavedCamera>,
) {
    let presentation = runtime.session().cinematic_presentation();
    let authored = presentation
        .as_ref()
        .ok()
        .and_then(|view| view.as_ref())
        .and_then(|view| view.camera.as_ref());
    let Ok((entity, mut transform, mut projection)) = cameras.single_mut() else {
        // A replacement camera must obtain its own baseline; never reuse an
        // unrelated camera's pose or projection after a scene transition.
        *previous = None;
        return;
    };
    if let Some(camera) = authored {
        if previous.as_ref().is_none_or(|saved| saved.0 != entity) {
            *previous = Some((entity, *transform, projection.clone()));
        }
        let eye = Vec3::new(camera.eye.x, camera.eye.y, camera.eye.z);
        let target = Vec3::new(camera.target.x, camera.target.y, camera.target.z);
        // Validation guarantees distinct positions. Use a stable alternative
        // up vector for vertical shots instead of producing a singular basis.
        let up = if (target - eye).normalize().dot(Vec3::Y).abs() > 0.999 {
            Vec3::Z
        } else {
            Vec3::Y
        };
        *transform = Transform::from_translation(eye).looking_at(target, up);
        if let Projection::Perspective(settings) = &mut *projection {
            settings.fov = camera.vertical_fov_degrees.to_radians();
        }
    } else if let Some((saved_entity, saved_pose, saved_projection)) = previous.take()
        && saved_entity == entity
    {
        *transform = saved_pose;
        *projection = saved_projection;
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn control(
    runtime: Res<GameplayRuntime>,
    mut pending: ResMut<crate::PendingPlayerControlCommand>,
) {
    if runtime.session().cinematic_controls_player() {
        pending.cancel_input();
    }
}
