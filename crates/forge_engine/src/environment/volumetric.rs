//! Bounded components for the explicit, unqualified volumetric preview.
mod density;
pub(super) mod projection;
use bevy::{
    light::{FogVolume, VolumetricFog},
    prelude::{Transform, Vec3},
};
use forge_project::{EnvironmentQualitySettings, VolumetricFogProfile};

/// Derived components only; source volume and camera coordinates are never rewritten.
pub(super) struct PreparedVolumetric {
    pub fog: VolumetricFog,
    pub volume: FogVolume,
    pub transform: Transform,
}

/// Conservative inscribed cube: every retained point is within the maximum distance.
/// This is not a spherical ray-march cutoff, and truncates corners/edges of the authored volume.
pub(super) fn bounded_volume(
    center: Vec3,
    size: Vec3,
    camera: Vec3,
    maximum: f32,
) -> Option<Transform> {
    if !center.is_finite()
        || !size.is_finite()
        || !camera.is_finite()
        || size.min_element() <= 0.0
        || !maximum.is_finite()
        || maximum <= 0.0
    {
        return None;
    }
    // Tiny conservative margin accommodates f32 arithmetic at scene-scale coordinates.
    let half_budget = Vec3::splat(maximum / 3.0_f32.sqrt() * 0.999_99);
    let low = (center - size * 0.5).max(camera - half_budget);
    let high = (center + size * 0.5).min(camera + half_budget);
    let extent = high - low;
    if extent.min_element() <= 0.0 {
        return None;
    }
    Some(Transform::from_translation((low + high) * 0.5).with_scale(extent))
}

/// Requires a validated authored profile and policy; disabled/empty/non-finite projections return None.
pub(super) fn prepare(
    source: VolumetricFogProfile,
    quality: EnvironmentQualitySettings,
    camera: Vec3,
) -> Option<PreparedVolumetric> {
    if !source.enabled || !quality.volumetric_fog_enabled || quality.volumetric_fog_step_count == 0
    {
        return None;
    }
    let transform = bounded_volume(
        Vec3::new(
            source.volume_center_m.x,
            source.volume_center_m.y,
            source.volume_center_m.z,
        ),
        Vec3::new(
            source.volume_size_m.x,
            source.volume_size_m.y,
            source.volume_size_m.z,
        ),
        camera,
        source
            .maximum_distance_m
            .min(quality.volumetric_fog_max_distance_m),
    )?;
    Some(PreparedVolumetric {
        fog: VolumetricFog {
            ambient_color: super::srgb_color(source.ambient_color_srgb, 1.0),
            // Bevy's ambient term bypasses the density texture and creates a box silhouette.
            // Preview uses sun scattering only; authored ambient intent stays in the document.
            ambient_intensity: 0.0,
            step_count: u32::from(quality.volumetric_fog_step_count),
            // No temporal noise before the native/WASM joint path is qualified.
            jitter: 0.0,
        },
        volume: FogVolume {
            density_factor: source.density_factor,
            absorption: source.absorption,
            scattering: source.scattering,
            scattering_asymmetry: source.scattering_asymmetry,
            ..Default::default()
        },
        transform,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn intersection_is_inside_both_authored_volume_and_distance_budget() {
        let center = Vec3::new(0.0, 20.0, 0.0);
        let size = Vec3::new(1024.0, 100.0, 1024.0);
        for maximum in [240.0, 320.0, 480.0] {
            for camera in [
                Vec3::ZERO,
                Vec3::new(-35.0, 5.4, -205.0),
                Vec3::new(500.0, 60.0, 500.0),
            ] {
                let volume = bounded_volume(center, size, camera, maximum).unwrap();
                for x in [-0.5, 0.5] {
                    for y in [-0.5, 0.5] {
                        for z in [-0.5, 0.5] {
                            let point = volume.translation + volume.scale * Vec3::new(x, y, z);
                            assert!(point.distance(camera) <= maximum);
                            assert!(
                                (point - center)
                                    .abs()
                                    .cmple(size * 0.5 + Vec3::splat(0.001))
                                    .all()
                            );
                        }
                    }
                }
            }
        }
        assert!(bounded_volume(center, size, Vec3::splat(4000.0), 100.0).is_none());
        assert!(bounded_volume(center, size, Vec3::ZERO, f32::NAN).is_none());
        assert!(bounded_volume(center, Vec3::ZERO, Vec3::ZERO, 100.0).is_none());
    }

    #[test]
    fn profile_projects_real_bevy_fields_without_rewriting_source() {
        let source = forge_world::embedded_visual_valley_environment_profile().unwrap();
        let before = source.content_hash().unwrap();
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        assert!(
            prepare(
                source.volumetric_fog,
                policy.presets.low.environment,
                Vec3::ZERO
            )
            .is_none()
        );
        for (quality, steps) in [
            (policy.presets.medium.environment, 32),
            (policy.presets.high.environment, 64),
            (policy.presets.ultra.environment, 96),
        ] {
            let result = prepare(source.volumetric_fog, quality, Vec3::ZERO).unwrap();
            assert_eq!(result.fog.step_count, steps);
            assert_eq!(result.fog.ambient_intensity.to_bits(), 0.0_f32.to_bits());
            assert_eq!(
                result.volume.density_factor.to_bits(),
                source.volumetric_fog.density_factor.to_bits()
            );
            assert_eq!(
                result.volume.absorption.to_bits(),
                source.volumetric_fog.absorption.to_bits()
            );
            assert_eq!(
                result.volume.scattering.to_bits(),
                source.volumetric_fog.scattering.to_bits()
            );
            assert!(result.transform.scale.min_element() > 0.0);
        }
        assert_eq!(source.content_hash().unwrap(), before);
    }
}
