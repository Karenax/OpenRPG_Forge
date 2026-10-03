//! Conservative blocked volumes from exact installed curved Rapier primitives.
//! No curved primitive becomes a tessellated walkable surface.
use bevy::prelude::*;
use bevy_rapier3d::rapier::{math::Pose, parry::shape::Shape};
use forge_navigation::{AgentProfile, AreaVolume, CurvedObstacleProjection};

const PLANES: u16 = 32;
const NUMERIC_MARGIN_M: f32 = 0.02;

#[allow(
    clippy::cast_possible_truncation,
    reason = "support intersections use f64, then finite world bounds and receipt bounds validate the f32 projection"
)]
pub(super) fn project(
    shape: &dyn Shape,
    pose: &Pose,
    profile: AgentProfile,
) -> Result<(AreaVolume, CurvedObstacleProjection), String> {
    profile.validate()?;
    let (kind, parameters) = if let Some(cylinder) = shape.as_cylinder() {
        ("cylinder", vec![cylinder.half_height, cylinder.radius])
    } else if let Some(capsule) = shape.as_capsule() {
        (
            "capsule",
            capsule
                .segment
                .a
                .to_array()
                .into_iter()
                .chain(capsule.segment.b.to_array())
                .chain([capsule.radius])
                .collect(),
        )
    } else if let Some(ball) = shape.as_ball() {
        ("ball", vec![ball.radius])
    } else {
        return Err("unsupported curved navigation obstacle".into());
    };
    let support = shape
        .as_support_map()
        .ok_or("curved collider has no support map")?;
    let cell_size = profile.radius_m * 0.5;
    let cell_height = cell_size * 0.25;
    // The grid marks cell centres. Half a cell diagonal covers their footprint;
    // agent radius covers its body, and 2 cm covers finite-precision transforms.
    let clearance = profile.radius_m
        + cell_size * (std::f32::consts::FRAC_1_SQRT_2 + forge_navigation::CONTOUR_ERROR_CELLS)
        + 0.01
        + NUMERIC_MARGIN_M;
    let mut planes = Vec::with_capacity(usize::from(PLANES));
    let mut support_points = Vec::with_capacity(usize::from(PLANES));
    for index in 0..PLANES {
        let angle = std::f32::consts::TAU * f32::from(index) / f32::from(PLANES);
        let (z, x) = angle.sin_cos();
        let normal = Vec3::new(x, 0.0, z);
        let point = support.support_point(pose, normal);
        support_points.push([
            f64::from(point.x + normal.x * clearance),
            f64::from(point.z + normal.z * clearance),
        ]);
        planes.push((
            [f64::from(x), f64::from(z)],
            f64::from(normal.dot(point) + clearance),
        ));
    }
    let mut vertices_xz = Vec::with_capacity(usize::from(PLANES));
    for index in 0..planes.len() {
        let (a, ha) = planes[index];
        let (b, hb) = planes[(index + 1) % planes.len()];
        let determinant = a[0] * b[1] - a[1] * b[0];
        vertices_xz.push([
            ((ha * b[1] - a[1] * hb) / determinant) as f32,
            ((a[0] * hb - ha * b[0]) / determinant) as f32,
        ]);
    }
    let bounds = shape.compute_aabb(pose);
    // For every outer polygon vertex choose an actual support point of the
    // clearance-expanded footprint. Convex combinations of those witnesses
    // are still in that footprint; their maximum vertex distance therefore
    // bounds excess everywhere in the polygon, including rotated primitives.
    let excess = vertices_xz
        .iter()
        .map(|vertex| {
            support_points
                .iter()
                .map(|point| {
                    (f64::from(vertex[0]) - point[0]).hypot(f64::from(vertex[1]) - point[1])
                })
                .fold(f64::INFINITY, f64::min)
        })
        .fold(0.0, f64::max) as f32;
    let area = AreaVolume {
        vertices_xz,
        min_y: bounds.mins.y - profile.height_m - cell_height,
        max_y: bounds.maxs.y + cell_height,
        area: 0,
    };
    area.validate()?;
    let transform = bevy_rapier3d::utils::iso_to_transform(pose);
    let receipt = CurvedObstacleProjection {
        algorithm: "rapier-support-planes-v1".into(),
        kind: kind.into(),
        parameters,
        position: transform.translation.to_array(),
        rotation: transform.rotation.to_array(),
        planes: PLANES,
        clearance_m: clearance,
        footprint_excess_bound_m: excess + NUMERIC_MARGIN_M,
    };
    receipt.validate(profile)?;
    Ok((area, receipt))
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy_rapier3d::prelude::Collider;

    #[test]
    fn unsupported_curves_and_invalid_profiles_are_rejected() {
        let cone = Collider::cone(1.0, 0.5);
        let pose = Pose::IDENTITY;
        assert!(project(cone.raw.as_ref(), &pose, AgentProfile::default()).is_err());
        let ball = Collider::ball(0.5);
        let profile = AgentProfile {
            speed_mps: f32::NAN,
            ..AgentProfile::default()
        };
        assert!(project(ball.raw.as_ref(), &pose, profile).is_err());
    }

    fn contains(area: &AreaVolume, point: Vec3) -> bool {
        area.vertices_xz.iter().enumerate().all(|(index, a)| {
            let b = area.vertices_xz[(index + 1) % area.vertices_xz.len()];
            (f64::from(b[0]) - f64::from(a[0])) * (f64::from(point.z) - f64::from(a[1]))
                - (f64::from(b[1]) - f64::from(a[1])) * (f64::from(point.x) - f64::from(a[0]))
                >= -1.0e-4
        })
    }

    #[test]
    fn tilted_prepared_capsule_and_cylinder_supports_stay_inside_conservative_footprints() {
        let mut cylinder = Collider::cylinder(1.8, 0.3);
        let mut capsule = Collider::capsule_y(1.8, 0.3);
        // These are the actual prepared shapes after Rapier scaling, not unscaled authoring parameters.
        cylinder.set_scale(Vec3::new(1.4, 0.8, 1.4), 32);
        capsule.set_scale(Vec3::splat(1.3), 32);
        for collider in [cylinder, capsule, Collider::ball(0.6)] {
            let pose = Pose::from_parts(
                Vec3::new(31.0, 2.0, -12.0),
                Quat::from_rotation_z(0.55) * Quat::from_rotation_y(0.3),
            );
            let (area, receipt) =
                project(collider.raw.as_ref(), &pose, AgentProfile::default()).unwrap();
            assert_eq!(receipt.planes, 32);
            assert!(receipt.footprint_excess_bound_m.is_finite());
            let support = collider.raw.as_support_map().unwrap();
            for index in 0..1024_u16 {
                let angle = std::f32::consts::TAU * f32::from(index) / 1024.0;
                let (z, x) = angle.sin_cos();
                let normal = Vec3::new(x, 0.0, z);
                let point = support.support_point(&pose, normal);
                assert!(
                    contains(&area, point),
                    "exact support outside blocked footprint"
                );
                assert!(
                    contains(&area, point + normal * AgentProfile::default().radius_m),
                    "agent clearance outside blocked footprint"
                );
            }
            let bounds = collider.raw.compute_aabb(&pose);
            assert!(area.min_y < bounds.mins.y - AgentProfile::default().height_m);
            assert!(area.max_y > bounds.maxs.y);
        }
    }
}
