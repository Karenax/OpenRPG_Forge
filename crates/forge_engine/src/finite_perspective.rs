//! Opt-in finite reverse-Z depth for Bevy perspective cameras.
//!
//! `Projection::Perspective` remains the canonical lens description. Only its
//! derived camera matrix and frustum change; ordinary cameras are untouched.

#![allow(clippy::needless_pass_by_value)] // Bevy system parameters.

use bevy::{
    camera::{CameraUpdateSystems, primitives::Frustum, visibility::VisibilitySystems},
    math::primitives::{HalfSpace, ViewFrustum},
    prelude::*,
};

/// Use the perspective lens's finite near/far range in the GPU depth mapping.
/// Invalid ranges retain Bevy's standard projection. Removing this component
/// restores that projection on the same frame, even without a lens change.
#[derive(Component, Clone, Copy, Debug, Default)]
pub struct FinitePerspectiveDepth;

/// Derived matrix/frustum preparation, before visibility and camera consumers.
#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct EffectivePerspectiveSystems;

pub struct FinitePerspectivePlugin;

impl Plugin for FinitePerspectivePlugin {
    fn build(&self, app: &mut App) {
        app.configure_sets(
            PostUpdate,
            EffectivePerspectiveSystems
                .after(CameraUpdateSystems)
                .after(bevy::transform::TransformSystems::Propagate)
                .after(VisibilitySystems::UpdateFrusta)
                .before(VisibilitySystems::CheckVisibility),
        )
        .add_systems(PostUpdate, update.in_set(EffectivePerspectiveSystems));
    }
}

#[derive(Component)]
struct Applied;

#[allow(clippy::cast_possible_truncation)] // GPU matrices are f32; compute ratios before rounding.
fn depth_coefficients(near: f32, far: f32) -> Option<Vec2> {
    let near = f64::from(near);
    let far = f64::from(far);
    if !near.is_finite() || !far.is_finite() || near <= 0.0 || far <= near {
        return None;
    }
    let coefficients = Vec2::new(
        (near / (far - near)) as f32,
        (far * near / (far - near)) as f32,
    );
    coefficients.is_finite().then_some(coefficients)
}

#[allow(clippy::float_cmp)] // The standard clip-plane normal must be exactly (0, 0, -1).
fn finite_matrix(mut clip: Mat4, lens: &PerspectiveProjection) -> Option<Mat4> {
    // Replacing the depth row of an oblique lens would discard its clip plane.
    if !clip.is_finite()
        || !lens.near_clip_plane.is_finite()
        || lens.near_clip_plane.x != 0.0
        || lens.near_clip_plane.y != 0.0
        || lens.near_clip_plane.z != -1.0
    {
        return None;
    }
    let coefficients = depth_coefficients(lens.near, lens.far)?;
    clip.z_axis.z = coefficients.x;
    clip.w_axis.z = coefficients.y;
    clip.is_finite().then_some(clip)
}

/// Validate a centered, non-oblique reverse-Z matrix against its camera range.
/// Accept both Bevy's infinite depth and the exact finite coefficients derived
/// by this module; arbitrary depth rows or angular offsets remain unsupported.
#[allow(clippy::float_cmp)] // Structural zeros and depth coefficients must match exactly.
pub(crate) fn standard_reverse_perspective(m: Mat4, near: f32, far: f32) -> bool {
    let Some(finite) = depth_coefficients(near, far) else {
        return false;
    };
    m.is_finite()
        && m.x_axis.x > 0.0
        && m.y_axis.y > 0.0
        && m.x_axis.y == 0.0
        && m.x_axis.z == 0.0
        && m.x_axis.w == 0.0
        && m.y_axis.x == 0.0
        && m.y_axis.z == 0.0
        && m.y_axis.w == 0.0
        && m.z_axis.x == 0.0
        && m.z_axis.y == 0.0
        && m.z_axis.w == -1.0
        && m.w_axis.x == 0.0
        && m.w_axis.y == 0.0
        && m.w_axis.w == 0.0
        && ((m.z_axis.z == 0.0 && m.w_axis.z == near)
            || (m.z_axis.z == finite.x && m.w_axis.z == finite.y))
}

#[allow(clippy::type_complexity)] // Optional lifecycle marker and frustum in one camera query.
fn update(
    mut commands: Commands,
    mut cameras: Query<
        (
            Entity,
            &Projection,
            &GlobalTransform,
            &mut Camera,
            Option<&FinitePerspectiveDepth>,
            Option<&Applied>,
            Option<&mut Frustum>,
        ),
        Or<(With<FinitePerspectiveDepth>, With<Applied>)>,
    >,
) {
    for (entity, projection, transform, mut camera, marker, applied, frustum) in &mut cameras {
        // Preserve sub-camera angular offsets. Only the reverse-Z coefficients
        // differ from the standard perspective matrix.
        let base = camera.sub_camera_view.as_ref().map_or_else(
            || projection.get_clip_from_view(),
            |sub| projection.get_clip_from_view_for_sub(sub),
        );
        let finite = match (marker, projection) {
            (Some(_), Projection::Perspective(lens)) => finite_matrix(base, lens),
            _ => None,
        };
        camera.computed.clip_from_view = finite.unwrap_or(base);
        if let Some(mut frustum) = frustum {
            let clip_from_world = camera.clip_from_view() * transform.affine().inverse();
            *frustum = Frustum(ViewFrustum::from_clip_from_world_custom_far(
                &clip_from_world,
                &transform.translation(),
                &transform.back(),
                projection.far(),
            ));
            if finite.is_some() {
                // Reverse-Z's near bound is clip.z <= clip.w. Bevy's helper
                // uses row3 + row2, the conventional -w <= z bound.
                frustum.0.half_spaces[ViewFrustum::NEAR_PLANE_IDX] =
                    HalfSpace::new(clip_from_world.row(3) - clip_from_world.row(2));
            }
        }
        if finite.is_some() && applied.is_none() {
            commands.entity(entity).insert(Applied);
        } else if finite.is_none() && applied.is_some() {
            commands.entity(entity).remove::<Applied>();
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::camera::CameraProjection;
    use bevy::math::Vec3A;

    fn lens() -> PerspectiveProjection {
        PerspectiveProjection {
            near: 0.1,
            far: 60_000.0,
            aspect_ratio: 16.0 / 9.0,
            ..default()
        }
    }

    #[test]
    fn finite_depth_respects_clip_limits_and_f32_lens_precision() {
        let p = lens();
        let clip = finite_matrix(p.get_clip_from_view(), &p).unwrap();
        // Source computes with decimal 0.1 in f64 before its GPU conversion.
        // PerspectiveProjection's canonical near is already rounded to f32.
        // Record the resulting one-ULP bound rather than claiming bit parity.
        assert!(
            clip.z_axis
                .z
                .to_bits()
                .abs_diff(0.000_001_666_669_4_f32.to_bits())
                <= 1
        );
        assert!(
            clip.w_axis
                .z
                .to_bits()
                .abs_diff(0.100_000_165_f32.to_bits())
                <= 1
        );
        let near = clip.project_point3(Vec3::new(0.0, 0.0, -p.near)).z;
        let far = clip.project_point3(Vec3::new(0.0, 0.0, -p.far)).z;
        assert!((near - 1.0).abs() < 1e-6);
        assert!(far.abs() < 1e-12);
        assert!(clip.project_point3(Vec3::new(0.0, 0.0, -0.08)).z > 1.0);
        assert!(clip.project_point3(Vec3::new(0.0, 0.0, -70_000.0)).z < 0.0);
    }

    #[test]
    fn invalid_ranges_are_rejected_and_angular_offsets_preserved() {
        let mut p = lens();
        let mut base = p.get_clip_from_view();
        base.z_axis.x = 0.17;
        base.z_axis.y = -0.21;
        let result = finite_matrix(base, &p).unwrap();
        assert_eq!(result.z_axis.x.to_bits(), base.z_axis.x.to_bits());
        assert_eq!(result.z_axis.y.to_bits(), base.z_axis.y.to_bits());
        assert_eq!(result.x_axis, base.x_axis);
        assert_eq!(result.y_axis, base.y_axis);
        for (near, far) in [
            (0.0, 60000.0),
            (-1.0, 60000.0),
            (1.0, 1.0),
            (2.0, 1.0),
            (0.1, f32::INFINITY),
            (f32::NAN, 60000.0),
        ] {
            p.near = near;
            p.far = far;
            assert!(finite_matrix(base, &p).is_none());
        }
    }

    #[test]
    fn resolve_guard_accepts_only_matching_centered_depth_mappings() {
        let mut p = lens();
        let infinite = p.get_clip_from_view();
        let finite = finite_matrix(infinite, &p).unwrap();
        assert!(standard_reverse_perspective(infinite, p.near, p.far));
        assert!(standard_reverse_perspective(finite, p.near, p.far));
        assert!(!standard_reverse_perspective(finite, 0.06, p.far));
        assert!(!standard_reverse_perspective(finite, p.near, 30_000.0));
        for (column, row, value) in [
            (2, 0, 0.1),
            (2, 1, 0.1),
            (2, 2, 0.001),
            (3, 2, 0.11),
            (3, 3, 1.0),
            (0, 0, f32::NAN),
        ] {
            let mut data = finite.to_cols_array_2d();
            data[column][row] = value;
            assert!(!standard_reverse_perspective(
                Mat4::from_cols_array_2d(&data),
                p.near,
                p.far
            ));
        }
        p.near_clip_plane.x = 0.1;
        assert!(finite_matrix(p.get_clip_from_view(), &p).is_none());
    }

    fn overwrite_standard(
        mut views: Query<(&Projection, &mut Camera)>,
        mut initialized: Local<bool>,
    ) {
        if *initialized {
            return;
        }
        *initialized = true;
        for (projection, mut camera) in &mut views {
            camera.computed.clip_from_view = projection.get_clip_from_view();
        }
    }

    #[test]
    fn schedule_updates_frustum_and_restores_unmarked_camera() {
        let mut app = App::new();
        app.add_plugins((
            bevy::camera::CameraProjectionPlugin,
            FinitePerspectivePlugin,
        ));
        // CPU camera-update fixture; the real Bevy frustum system runs below.
        app.add_systems(PostUpdate, overwrite_standard.in_set(CameraUpdateSystems));
        let mut p = lens();
        let entity = app
            .world_mut()
            .spawn((
                Camera::default(),
                Projection::Perspective(p.clone()),
                GlobalTransform::IDENTITY,
                Frustum::default(),
                FinitePerspectiveDepth,
            ))
            .id();
        app.update();
        let clip = app.world().get::<Camera>(entity).unwrap().clip_from_view();
        assert!(clip.z_axis.z > 0.0);
        let frustum = app.world().get::<Frustum>(entity).unwrap();
        let near = frustum.0.half_spaces[ViewFrustum::NEAR_PLANE_IDX];
        assert!(near.normal().dot(Vec3A::new(0.0, 0.0, -0.08)) + near.d() < 0.0);
        assert!(near.normal().dot(Vec3A::new(0.0, 0.0, -0.2)) + near.d() > 0.0);
        p.near = 0.2;
        p.aspect_ratio = 1.0;
        app.world_mut()
            .entity_mut(entity)
            .insert(Projection::Perspective(p.clone()));
        app.update();
        let resized = app.world().get::<Camera>(entity).unwrap().clip_from_view();
        assert_eq!(resized.x_axis, p.get_clip_from_view().x_axis);
        assert!((resized.project_point3(Vec3::new(0.0, 0.0, -0.2)).z - 1.0).abs() < 1e-6);
        app.world_mut()
            .entity_mut(entity)
            .remove::<FinitePerspectiveDepth>();
        app.update();
        assert_eq!(
            app.world().get::<Camera>(entity).unwrap().clip_from_view(),
            p.get_clip_from_view()
        );
        assert!(app.world().get::<Applied>(entity).is_none());
    }
}
