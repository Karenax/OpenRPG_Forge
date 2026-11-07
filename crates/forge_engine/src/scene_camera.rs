//! Marks temporary offscreen views excluded from interactive scene tools.
use bevy::prelude::*;
use bevy::render::extract_component::{ExtractComponent, ExtractComponentPlugin};

#[derive(Component, Clone, Copy, Debug, Default, ExtractComponent)]
pub struct OffscreenSceneCamera;

/// The Tidewater terrain and ocean view-selection cadence, shared by hosts.
/// Call `record` only after the selected view has been applied successfully.
#[derive(Clone, Copy, Debug, Default)]
pub struct SourceViewCadence {
    previous: Option<(Entity, Transform, [u32; 4])>,
}

impl SourceViewCadence {
    #[must_use]
    pub fn needs_refresh(
        &self,
        camera: Entity,
        transform: &Transform,
        projection: &PerspectiveProjection,
    ) -> bool {
        let lens = [
            projection.fov,
            projection.aspect_ratio,
            projection.near,
            projection.far,
        ]
        .map(f32::to_bits);
        !self
            .previous
            .is_some_and(|(previous_camera, previous, previous_lens)| {
                previous_camera == camera
                    && previous_lens == lens
                    && previous.translation.distance_squared(transform.translation) < 1.0
                    && previous.rotation.dot(transform.rotation).abs() > 0.9999
            })
    }

    pub fn record(
        &mut self,
        camera: Entity,
        transform: &Transform,
        projection: &PerspectiveProjection,
    ) {
        let lens = [
            projection.fov,
            projection.aspect_ratio,
            projection.near,
            projection.far,
        ]
        .map(f32::to_bits);
        self.previous = Some((camera, *transform, lens));
    }

    pub fn invalidate(&mut self) {
        self.previous = None;
    }
}

/// Retain the same offscreen identity in render-world camera selection.
pub fn install_offscreen_extraction(app: &mut App) {
    if !app.is_plugin_added::<ExtractComponentPlugin<OffscreenSceneCamera>>() {
        app.add_plugins(ExtractComponentPlugin::<OffscreenSceneCamera>::default());
    }
}

// SPDX-License-Identifier: MIT
// View adaptation from Tidewater, copyright 2026 DRG Software Solutions LLC.
// Upstream 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
pub fn source_view(
    transform: &Transform,
    projection: &PerspectiveProjection,
) -> Result<forge_world::terrain_lod::CdlodView, String> {
    use bevy::math::{DMat4, DQuat, DVec3, DVec4};
    let near = f64::from(projection.near);
    let far = f64::from(projection.far);
    let aspect = f64::from(projection.aspect_ratio);
    if !(near > 0.0 && far > near && far.is_finite() && aspect > 0.0 && aspect.is_finite()) {
        return Err("Invalid finite source perspective".into());
    }
    if !transform.translation.is_finite()
        || !transform.rotation.is_finite()
        || transform.rotation.length_squared() == 0.0
        // A rigid camera can acquire a few f32 ulps of scale during matrix
        // decomposition. The source view intentionally discards that noise,
        // but must still reject actual camera scaling and non-finite values.
        || !transform.scale.is_finite()
        || (transform.scale - Vec3::ONE).abs().max_element() > 8.0 * f32::EPSILON
        || !projection.fov.is_finite()
        || (projection.fov <= 0.0 || projection.fov >= std::f32::consts::PI)
    {
        return Err("Invalid source camera pose or field of view".into());
    }
    let y = 1.0 / (f64::from(projection.fov) * 0.5).tan();
    let clip = DMat4::from_cols(
        DVec4::new(y / aspect, 0.0, 0.0, 0.0),
        DVec4::new(0.0, y, 0.0, 0.0),
        DVec4::new(0.0, 0.0, near / (far - near), -1.0),
        DVec4::new(0.0, 0.0, near * far / (far - near), 0.0),
    );
    let position = DVec3::from_array(transform.translation.to_array().map(f64::from));
    let world = DMat4::from_rotation_translation(
        DQuat::from_array(transform.rotation.to_array().map(f64::from)).normalize(),
        position,
    );
    let rows = (clip * world.inverse())
        .transpose()
        .to_cols_array_2d()
        .map(DVec4::from_array);
    Ok(forge_world::terrain_lod::CdlodView {
        position: position.to_array(),
        planes: [
            rows[3] + rows[0],
            rows[3] - rows[0],
            rows[3] + rows[1],
            rows[3] - rows[1],
            rows[3] - rows[2],
            rows[2],
        ]
        .map(|p| (p / p.truncate().length()).to_array()),
    })
}

/// Predict source views along the player's current horizontal movement.
/// A stationary player needs no speculative views. The same source camera
/// conversion is used for terrain and water, regardless of the module.
pub fn future_source_views(
    transform: &Transform,
    projection: &PerspectiveProjection,
    velocity: Vec3,
    distances_m: &[f32],
) -> Result<Vec<forge_world::terrain_lod::CdlodView>, String> {
    if !velocity.is_finite()
        || distances_m.is_empty()
        || distances_m.len() > 8
        || distances_m
            .iter()
            .any(|distance| !distance.is_finite() || !(0.0..=64.0).contains(distance))
    {
        return Err("Invalid future source view prediction".into());
    }
    let horizontal = velocity.with_y(0.0);
    let speed_squared = horizontal.length_squared();
    if !speed_squared.is_finite() {
        return Err("Invalid future source view prediction".into());
    }
    if speed_squared < 0.01 {
        return Ok(Vec::new());
    }
    let direction = horizontal.normalize();
    distances_m
        .iter()
        .map(|distance| {
            let mut future = *transform;
            future.translation += direction * *distance;
            source_view(&future, projection)
        })
        .collect()
}

pub fn primary(world: &mut World) -> Result<(Entity, Vec3), String> {
    let mut query = world.query::<(Entity, &Camera, &Transform)>();
    let mut active = query.iter(world).filter(|(entity, camera, _)| {
        camera.is_active
            && world.get::<Camera3d>(*entity).is_some()
            && world
                .get::<crate::scene_camera::OffscreenSceneCamera>(*entity)
                .is_none()
    });
    let (entity, _, pose) = active.next().ok_or("Scene requires one active 3D camera")?;
    let value = (entity, pose.translation);
    if active.next().is_some() || !value.1.is_finite() || world.get::<ChildOf>(value.0).is_some() {
        return Err("Invalid primary scene camera topology".into());
    }
    Ok(value)
}

/// Main-world view identity and monotone preparation identity; not an RAF number.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct FrameTag {
    pub view: Entity,
    pub serial: u64,
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn future_source_views_follow_motion_without_map_specific_directions() {
        let camera = Transform::from_xyz(10.0, 4.0, -5.0);
        let lens = PerspectiveProjection::default();
        let views =
            future_source_views(&camera, &lens, Vec3::new(-3.0, 5.0, 4.0), &[5.0, 10.0]).unwrap();
        assert_eq!(views.len(), 2);
        for (view, expected) in views.iter().zip([[7.0, 4.0, -1.0], [4.0, 4.0, 3.0]]) {
            assert!(
                view.position
                    .iter()
                    .zip(expected)
                    .all(|(a, b)| (a - b).abs() < 1e-5)
            );
        }
        assert!(
            future_source_views(&camera, &lens, Vec3::ZERO, &[5.0])
                .unwrap()
                .is_empty()
        );
        assert!(future_source_views(&camera, &lens, Vec3::X, &[65.0]).is_err());
        assert!(future_source_views(&camera, &lens, Vec3::splat(f32::NAN), &[5.0]).is_err());
    }

    #[test]
    fn source_view_cadence_matches_reference_and_revokes_camera_or_lens_changes() {
        let mut cadence = SourceViewCadence::default();
        let first = Entity::from_raw_u32(1).unwrap();
        let second = Entity::from_raw_u32(2).unwrap();
        let pose = Transform::IDENTITY;
        let lens = PerspectiveProjection::default();
        assert!(cadence.needs_refresh(first, &pose, &lens));
        cadence.record(first, &pose, &lens);
        assert!(!cadence.needs_refresh(first, &Transform::from_xyz(0.999, 0.0, 0.0), &lens));
        assert!(cadence.needs_refresh(first, &Transform::from_xyz(1.0, 0.0, 0.0), &lens));
        assert!(cadence.needs_refresh(second, &pose, &lens));
        let rotated = Transform::from_rotation(Quat::from_rotation_y(0.04));
        assert!(cadence.needs_refresh(first, &rotated, &lens));
        let changed_lens = PerspectiveProjection {
            fov: lens.fov + 0.01,
            ..lens.clone()
        };
        assert!(cadence.needs_refresh(first, &pose, &changed_lens));
        cadence.invalidate();
        assert!(cadence.needs_refresh(first, &pose, &lens));
    }
    #[test]
    fn source_view_rejects_invalid_pose_depth_and_lens() {
        let lens = PerspectiveProjection {
            far: 60_000.0,
            ..Default::default()
        };
        assert!(source_view(&Transform::IDENTITY, &lens).is_ok());
        for fov in [0.0, -1.0, std::f32::consts::PI, f32::NAN] {
            assert!(
                source_view(
                    &Transform::IDENTITY,
                    &PerspectiveProjection {
                        fov,
                        far: 60_000.0,
                        ..Default::default()
                    }
                )
                .is_err()
            );
        }
        assert!(
            source_view(
                &Transform::IDENTITY,
                &PerspectiveProjection {
                    far: f32::INFINITY,
                    ..Default::default()
                }
            )
            .is_err()
        );
        assert!(source_view(&Transform::from_scale(Vec3::splat(2.0)), &lens).is_err());
    }

    #[test]
    fn source_view_accepts_rounding_noise_but_rejects_real_camera_scale() {
        let lens = PerspectiveProjection {
            fov: 1.308_996_9,
            aspect_ratio: 16.0 / 9.0,
            near: 0.05,
            far: 60_000.0,
            ..Default::default()
        };
        let rigid = Transform {
            translation: Vec3::new(53.600_25, 4.434_454_4, -76.998_83),
            rotation: Quat::from_xyzw(0.0, 0.999_991_3, 0.0, 0.004_186_812_3),
            scale: Vec3::ONE,
        };
        let mut rounded = rigid;
        rounded.scale = Vec3::new(1.000_000_2, 1.0, 1.000_000_2);
        let expected = source_view(&rigid, &lens).unwrap();
        let actual = source_view(&rounded, &lens).unwrap();
        assert_eq!(
            actual.position.map(f64::to_bits),
            expected.position.map(f64::to_bits)
        );
        assert_eq!(
            actual.planes.map(|plane| plane.map(f64::to_bits)),
            expected.planes.map(|plane| plane.map(f64::to_bits))
        );
        for scale in [
            Vec3::splat(1.000_01),
            Vec3::new(1.0, 0.99, 1.0),
            Vec3::splat(f32::NAN),
        ] {
            rounded.scale = scale;
            assert!(source_view(&rounded, &lens).is_err());
        }
    }
}
