use forge_core::{AreaId, ProjectId, Revision, Vec3};
use forge_project::{
    RiverBankProfile, RiverControlPoint, RiverPathAlgorithm, RiverProfile, WaterDocument,
    WaterDocumentError, WaterOpticsProfile, WaterQualityTier, WaterReflectionFallback,
    WaterReflectionProfile, WaterRefractionFallback, WaterRefractionProfile, WaterSrgbColor,
    WaterSurfaceProfile,
};
use serde::Serialize;

pub const VISUAL_VALLEY_WATER_SEED: u64 = 0x5249_5645_525f_5631;
pub const VISUAL_VALLEY_WATER_JSON: &str =
    include_str!("../../../examples/visual_valley/water.v1.json");

/// Original, asset-free recipe for the Visual Valley river, banks, and optical intent.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct VisualValleyWaterRecipe {
    pub seed: u64,
}

impl Default for VisualValleyWaterRecipe {
    fn default() -> Self {
        Self {
            seed: VISUAL_VALLEY_WATER_SEED,
        }
    }
}

impl VisualValleyWaterRecipe {
    pub fn build(self) -> Result<WaterDocument, WaterDocumentError> {
        let document = WaterDocument::new(
            ProjectId::deterministic("visual-valley-project"),
            AreaId::deterministic("visual-valley-area"),
            Revision::INITIAL,
            "Visual Valley river",
            self.seed,
            RiverProfile {
                algorithm: RiverPathAlgorithm::CentripetalCatmullRomV1,
                sample_spacing_m: 2.0,
                control_points: vec![
                    control_point(-232.0, 10.2, -168.0, 12.0, 2.2),
                    control_point(-170.0, 9.8, -132.0, 13.0, 2.5),
                    control_point(-112.0, 9.4, -82.0, 14.5, 2.8),
                    control_point(-48.0, 9.0, -36.0, 16.0, 3.2),
                    control_point(18.0, 8.6, 4.0, 17.0, 3.6),
                    control_point(84.0, 8.2, 52.0, 18.0, 4.0),
                    control_point(154.0, 7.8, 108.0, 17.0, 4.2),
                    control_point(226.0, 7.4, 166.0, 19.0, 4.0),
                ],
            },
            WaterSurfaceProfile {
                shallow_color_srgb: WaterSrgbColor::new(0.12, 0.42, 0.46),
                deep_color_srgb: WaterSrgbColor::new(0.02, 0.13, 0.21),
                opacity: 0.84,
                roughness: 0.16,
                flow_speed_mps: 0.72,
                wave_amplitude_m: 0.12,
                wave_length_m: 5.5,
                normal_strength: 0.62,
            },
            RiverBankProfile {
                shoulder_width_m: 5.0,
                blend_width_m: 9.0,
                wet_margin_m: 1.8,
                horizontal_to_vertical_slope: 2.8,
                erosion_amplitude_m: 0.55,
                erosion_frequency_cycles_per_m: 0.028,
            },
            WaterOpticsProfile {
                quality: WaterQualityTier::High,
                reflection: WaterReflectionProfile::ScreenSpace {
                    intensity: 0.72,
                    max_steps: 48,
                    max_distance_m: 96.0,
                    thickness_m: 0.9,
                },
                refraction: WaterRefractionProfile::ScreenSpace {
                    strength: 0.18,
                    max_steps: 24,
                    max_depth_m: 3.2,
                },
                reflection_fallback: WaterReflectionFallback::EnvironmentThenDisabled,
                refraction_fallback: WaterRefractionFallback::SurfaceTint,
            },
        );
        document.validate()?;
        Ok(document)
    }
}

const fn control_point(
    x_m: f32,
    y_m: f32,
    z_m: f32,
    surface_width_m: f32,
    bed_depth_m: f32,
) -> RiverControlPoint {
    RiverControlPoint {
        center_m: Vec3::new(x_m, y_m, z_m),
        surface_width_m,
        bed_depth_m,
    }
}

/// One derived river sample. It is a cache input, never a persistent engine entity.
#[derive(Clone, Copy, Debug, PartialEq, Serialize)]
#[serde(deny_unknown_fields)]
pub struct RiverCenterlineSample {
    pub center_m: Vec3,
    pub surface_width_m: f32,
    pub bed_depth_m: f32,
    pub source_segment_index: u16,
    pub segment_fraction: f32,
}

/// Applies the exact version-1 centreline sampling contract declared by `RiverPathAlgorithm`.
///
/// The output count is prevalidated and capped by `forge_project::MAX_RIVER_SAMPLE_COUNT`.
pub fn sample_river_centerline(
    document: &WaterDocument,
) -> Result<Vec<RiverCenterlineSample>, WaterDocumentError> {
    document.validate()?;
    let requested_sample_count = document.river.requested_sample_count()?;

    #[allow(clippy::cast_possible_truncation)]
    let mut samples = Vec::with_capacity(requested_sample_count as usize);
    let first = document.river.control_points[0];
    samples.push(RiverCenterlineSample {
        center_m: first.center_m,
        surface_width_m: first.surface_width_m,
        bed_depth_m: first.bed_depth_m,
        source_segment_index: 0,
        segment_fraction: 0.0,
    });

    for segment_index in 0..document.river.control_points.len() - 1 {
        let start = document.river.control_points[segment_index];
        let end = document.river.control_points[segment_index + 1];
        let previous = if segment_index == 0 {
            extrapolate_before(start.center_m, end.center_m)
        } else {
            document.river.control_points[segment_index - 1].center_m
        };
        let next = if segment_index + 2 == document.river.control_points.len() {
            extrapolate_after(start.center_m, end.center_m)
        } else {
            document.river.control_points[segment_index + 2].center_m
        };
        let delta_x = end.center_m.x - start.center_m.x;
        let delta_z = end.center_m.z - start.center_m.z;

        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        let interval_count =
            (delta_x.hypot(delta_z) / document.river.sample_spacing_m).ceil() as u32;

        for interval_index in 1..=interval_count {
            #[allow(clippy::cast_precision_loss)]
            let segment_fraction = interval_index as f32 / interval_count as f32;
            let curved_center_m = centripetal_catmull_rom(
                previous,
                start.center_m,
                end.center_m,
                next,
                segment_fraction,
            );
            let center_m = if interval_index == interval_count {
                end.center_m
            } else {
                Vec3::new(
                    curved_center_m.x,
                    linear_interpolate(start.center_m.y, end.center_m.y, segment_fraction),
                    curved_center_m.z,
                )
            };
            samples.push(RiverCenterlineSample {
                center_m,
                surface_width_m: if interval_index == interval_count {
                    end.surface_width_m
                } else {
                    linear_interpolate(start.surface_width_m, end.surface_width_m, segment_fraction)
                },
                bed_depth_m: if interval_index == interval_count {
                    end.bed_depth_m
                } else {
                    linear_interpolate(start.bed_depth_m, end.bed_depth_m, segment_fraction)
                },
                #[allow(clippy::cast_possible_truncation)]
                source_segment_index: segment_index as u16,
                segment_fraction,
            });
        }
    }

    debug_assert_eq!(
        samples.len(),
        usize::try_from(requested_sample_count).unwrap_or(usize::MAX)
    );
    Ok(samples)
}

fn extrapolate_before(start: Vec3, end: Vec3) -> Vec3 {
    Vec3::new(
        2.0 * start.x - end.x,
        2.0 * start.y - end.y,
        2.0 * start.z - end.z,
    )
}

fn extrapolate_after(start: Vec3, end: Vec3) -> Vec3 {
    Vec3::new(
        2.0 * end.x - start.x,
        2.0 * end.y - start.y,
        2.0 * end.z - start.z,
    )
}

fn centripetal_catmull_rom(
    point_0: Vec3,
    point_1: Vec3,
    point_2: Vec3,
    point_3: Vec3,
    segment_fraction: f32,
) -> Vec3 {
    let time_0 = 0.0;
    let time_1 = centripetal_knot(time_0, point_0, point_1);
    let time_2 = centripetal_knot(time_1, point_1, point_2);
    let time_3 = centripetal_knot(time_2, point_2, point_3);
    let time = linear_interpolate(time_1, time_2, segment_fraction);

    let first = interpolate_at_time(point_0, point_1, time_0, time_1, time);
    let second = interpolate_at_time(point_1, point_2, time_1, time_2, time);
    let third = interpolate_at_time(point_2, point_3, time_2, time_3, time);
    let left = interpolate_at_time(first, second, time_0, time_2, time);
    let right = interpolate_at_time(second, third, time_1, time_3, time);
    interpolate_at_time(left, right, time_1, time_2, time)
}

fn centripetal_knot(previous_time: f32, first: Vec3, second: Vec3) -> f32 {
    let delta_x = second.x - first.x;
    let delta_y = second.y - first.y;
    let delta_z = second.z - first.z;
    let squared_distance = delta_x * delta_x + delta_y * delta_y + delta_z * delta_z;
    previous_time + squared_distance.sqrt().sqrt()
}

fn interpolate_at_time(
    first: Vec3,
    second: Vec3,
    first_time: f32,
    second_time: f32,
    time: f32,
) -> Vec3 {
    let fraction = (time - first_time) / (second_time - first_time);
    Vec3::new(
        linear_interpolate(first.x, second.x, fraction),
        linear_interpolate(first.y, second.y, fraction),
        linear_interpolate(first.z, second.z, fraction),
    )
}

fn linear_interpolate(first: f32, second: f32, fraction: f32) -> f32 {
    first + (second - first) * fraction
}

/// Loads and validates the exact R1-022/023 fixture embedded in Forge.
pub fn embedded_visual_valley_water_document() -> Result<WaterDocument, WaterDocumentError> {
    WaterDocument::from_json(VISUAL_VALLEY_WATER_JSON)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        embedded_visual_valley_environment_profile, embedded_visual_valley_terrain_document,
        embedded_visual_valley_vegetation_document,
    };
    use forge_core::canonical_json_sha256;
    use forge_project::{
        ResolvedWaterReflectionPath, ResolvedWaterRefractionPath, WaterOpticsCapabilities,
        WaterOpticsCapabilityStatus,
    };

    #[test]
    fn embedded_fixture_matches_recipe_and_serializes_exactly() {
        let generated = VisualValleyWaterRecipe::default().build().unwrap();
        let embedded = embedded_visual_valley_water_document().unwrap();

        assert_eq!(embedded, generated);
        assert_eq!(
            VISUAL_VALLEY_WATER_JSON.trim_end(),
            embedded.to_pretty_json().unwrap()
        );
        assert_eq!(
            embedded.content_hash().unwrap(),
            generated.content_hash().unwrap()
        );
        assert_eq!(
            embedded.content_hash().unwrap().to_string(),
            "d9689b9c4cad040877144f1d1db7c4451a13bb9572d6db14e542be7ea6c0128c"
        );
    }

    #[test]
    fn identity_matches_the_other_visual_valley_documents() {
        let water = embedded_visual_valley_water_document().unwrap();
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let vegetation = embedded_visual_valley_vegetation_document().unwrap();
        let environment = embedded_visual_valley_environment_profile().unwrap();

        water
            .validate_identity(terrain.project_id, terrain.area_id, terrain.revision)
            .unwrap();
        assert_eq!(vegetation.project_id, terrain.project_id);
        assert_eq!(vegetation.area_id, terrain.area_id);
        assert_eq!(vegetation.revision, terrain.revision);
        environment
            .validate_identity(terrain.project_id, terrain.area_id, terrain.revision)
            .unwrap();
    }

    #[test]
    fn recipe_replays_exactly_and_seed_is_part_of_the_source_hash() {
        let first = VisualValleyWaterRecipe::default().build().unwrap();
        let replay = VisualValleyWaterRecipe::default().build().unwrap();
        let changed = VisualValleyWaterRecipe {
            seed: VISUAL_VALLEY_WATER_SEED + 1,
        }
        .build()
        .unwrap();

        assert_eq!(first, replay);
        assert_eq!(
            first.content_hash().unwrap(),
            replay.content_hash().unwrap()
        );
        assert_ne!(
            first.content_hash().unwrap(),
            changed.content_hash().unwrap()
        );
        assert_eq!(first.river.control_points.len(), 8);
        assert!(first.river.approximate_path_length_m() < 700.0);
        assert!(
            first.river.control_points.iter().all(|point| {
                point.center_m.x.abs() <= 256.0 && point.center_m.z.abs() <= 256.0
            })
        );
    }

    #[test]
    fn canonical_sampler_is_deterministic_bounded_and_endpoint_exact() {
        let water = embedded_visual_valley_water_document().unwrap();
        let first = sample_river_centerline(&water).unwrap();
        let replay = sample_river_centerline(&water).unwrap();

        assert_eq!(first, replay);
        assert_eq!(
            first.len(),
            usize::try_from(water.river.requested_sample_count().unwrap()).unwrap()
        );
        assert!(
            water.river.requested_sample_count().unwrap() <= forge_project::MAX_RIVER_SAMPLE_COUNT
        );
        assert_eq!(
            first.first().unwrap().center_m,
            water.river.control_points[0].center_m
        );
        assert_eq!(
            first.last().unwrap().center_m,
            water.river.control_points.last().unwrap().center_m
        );
        assert_eq!(
            canonical_json_sha256(&first).unwrap().to_string(),
            "26d94d3be8eaa14bb4374250f28e98fbb7c8de76eec17c4e725d6635a3248f2b"
        );
    }

    #[test]
    fn requested_and_fallback_optics_are_observable() {
        let water = embedded_visual_valley_water_document().unwrap();
        let requested = water.resolve_optics_path(WaterOpticsCapabilities {
            screen_space_reflections: WaterOpticsCapabilityStatus::Available,
            screen_space_refraction: WaterOpticsCapabilityStatus::Available,
            environment_reflections: WaterOpticsCapabilityStatus::Available,
        });
        assert_eq!(
            requested.reflection,
            ResolvedWaterReflectionPath::ScreenSpace { max_steps: 48 }
        );
        assert_eq!(
            requested.refraction,
            ResolvedWaterRefractionPath::ScreenSpace { max_steps: 24 }
        );
        assert!(!requested.reflection_fallback_used);
        assert!(!requested.refraction_fallback_used);

        let fallback = water.resolve_optics_path(WaterOpticsCapabilities {
            environment_reflections: WaterOpticsCapabilityStatus::Available,
            ..WaterOpticsCapabilities::default()
        });
        assert_eq!(
            fallback.reflection,
            ResolvedWaterReflectionPath::Environment
        );
        assert_eq!(
            fallback.refraction,
            ResolvedWaterRefractionPath::SurfaceTint
        );
        assert!(fallback.reflection_fallback_used);
        assert!(fallback.refraction_fallback_used);
    }

    #[test]
    fn fixture_contains_no_asset_or_upstream_identity_reference() {
        let lowercase = VISUAL_VALLEY_WATER_JSON.to_ascii_lowercase();
        let upstream_identity = ["ca", "dle"].concat();
        for forbidden in [
            "asset",
            upstream_identity.as_str(),
            "the vale",
            "whisperwood",
            "upstream",
            "three.js",
            "bevy",
            "rapier",
            "texture",
        ] {
            assert!(!lowercase.contains(forbidden), "found `{forbidden}`");
        }
    }
}
