use forge_core::{AreaId, ProjectId, Revision, Vec3};
use forge_project::{
    RiverBankProfile, RiverControlPoint, RiverElevationPolicy, RiverPathAlgorithm, RiverProfile,
    WaterDocument, WaterDocumentError, WaterOpticsProfile, WaterQualityTier,
    WaterReflectionFallback, WaterReflectionProfile, WaterRefractionFallback,
    WaterRefractionProfile, WaterSrgbColor, WaterSurfaceProfile,
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
    #[allow(
        clippy::too_many_lines,
        reason = "the auditable fixture keeps all 62 terrain-following controls together"
    )]
    pub fn build(self) -> Result<WaterDocument, WaterDocumentError> {
        let document = WaterDocument::new(
            ProjectId::deterministic("visual-valley-project"),
            AreaId::deterministic("visual-valley-area"),
            Revision::INITIAL,
            "Visual Valley river",
            self.seed,
            RiverProfile {
                algorithm: RiverPathAlgorithm::CentripetalCatmullRomV1,
                elevation_policy: RiverElevationPolicy::default(),
                sample_spacing_m: 2.0,
                control_points: vec![
                    control_point(96.0, 21.821, 160.0, 3.0, 0.800),
                    control_point(92.0, 21.770, 160.0, 3.5, 0.844),
                    control_point(88.0, 21.770, 160.0, 3.75, 0.889),
                    control_point(84.0, 21.544, 160.0, 4.25, 0.933),
                    control_point(80.0, 20.730, 160.0, 4.75, 0.977),
                    control_point(76.0, 19.563, 160.0, 5.0, 1.021),
                    control_point(72.0, 18.074, 160.0, 5.25, 1.066),
                    control_point(68.0, 16.655, 160.0, 5.25, 1.110),
                    control_point(64.0, 15.628, 160.0, 4.75, 1.154),
                    control_point(60.0, 14.891, 160.0, 4.5, 1.198),
                    control_point(56.0, 14.602, 160.0, 3.75, 1.243),
                    control_point(52.0, 14.306, 160.0, 3.25, 1.287),
                    control_point(48.0, 13.562, 160.0, 3.0, 1.331),
                    control_point(44.0, 12.613, 160.0, 2.5, 1.375),
                    control_point(40.0, 11.480, 160.0, 2.25, 1.420),
                    control_point(36.0, 10.459, 160.0, 2.0, 1.464),
                    control_point(32.0, 9.823, 160.0, 1.75, 1.508),
                    control_point(28.0, 9.216, 160.0, 1.5, 1.552),
                    control_point(24.0, 8.208, 160.0, 1.5, 1.597),
                    control_point(20.0, 7.151, 160.0, 1.25, 1.641),
                    control_point(16.0, 6.465, 160.0, 1.25, 1.685),
                    control_point(12.0, 6.074, 160.0, 1.25, 1.730),
                    control_point(8.0, 6.074, 160.0, 1.25, 1.774),
                    control_point(4.0, 4.254, 156.0, 1.5, 1.818),
                    control_point(0.0, 4.254, 156.0, 1.25, 1.862),
                    control_point(-4.0, 4.254, 156.0, 1.5, 1.907),
                    control_point(-8.0, 2.881, 152.0, 3.25, 1.951),
                    control_point(-12.0, 2.245, 148.0, 3.5, 1.995),
                    control_point(-16.0, 1.838, 144.0, 3.5, 2.039),
                    control_point(-20.0, 1.574, 140.0, 3.0, 2.084),
                    control_point(-24.0, 1.309, 136.0, 2.5, 2.128),
                    control_point(-28.0, 1.309, 132.0, 1.75, 2.172),
                    control_point(-32.0, 1.126, 128.0, 1.5, 2.216),
                    control_point(-36.0, 0.652, 124.0, 1.25, 2.261),
                    control_point(-40.0, 0.058, 120.0, 1.0, 2.305),
                    control_point(-44.0, -0.344, 116.0, 1.0, 2.349),
                    control_point(-48.0, -1.686, 112.0, 1.25, 2.393),
                    control_point(-52.0, -3.975, 108.0, 1.0, 2.438),
                    control_point(-56.0, -4.086, 108.0, 1.0, 2.482),
                    control_point(-60.0, -4.213, 108.0, 1.0, 2.526),
                    control_point(-64.0, -4.213, 108.0, 1.25, 2.570),
                    control_point(-68.0, -4.228, 108.0, 1.25, 2.615),
                    control_point(-72.0, -4.692, 108.0, 1.25, 2.659),
                    control_point(-76.0, -5.143, 108.0, 1.25, 2.703),
                    control_point(-80.0, -5.223, 108.0, 1.5, 2.748),
                    control_point(-84.0, -5.422, 108.0, 1.75, 2.792),
                    control_point(-88.0, -6.120, 108.0, 2.0, 2.836),
                    control_point(-92.0, -7.171, 108.0, 2.25, 2.880),
                    control_point(-96.0, -8.308, 108.0, 2.5, 2.925),
                    control_point(-100.0, -9.245, 108.0, 2.5, 2.969),
                    control_point(-104.0, -9.648, 108.0, 2.25, 3.013),
                    control_point(-108.0, -10.211, 108.0, 1.75, 3.057),
                    control_point(-112.0, -11.506, 108.0, 1.5, 3.102),
                    control_point(-116.0, -12.901, 108.0, 1.5, 3.146),
                    control_point(-120.0, -14.026, 108.0, 1.75, 3.190),
                    control_point(-124.0, -14.729, 108.0, 1.75, 3.234),
                    control_point(-128.0, -15.032, 108.0, 1.75, 3.279),
                    control_point(-132.0, -15.376, 108.0, 2.0, 3.323),
                    control_point(-136.0, -16.113, 108.0, 2.5, 3.367),
                    control_point(-140.0, -16.850, 108.0, 3.0, 3.411),
                    control_point(-144.0, -17.177, 108.0, 4.25, 3.456),
                    control_point(-148.0, -17.441, 104.0, 6.0, 3.500),
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
            let curved_center_m = match document.river.algorithm {
                RiverPathAlgorithm::CentripetalCatmullRomV1 => centripetal_catmull_rom(
                    previous,
                    start.center_m,
                    end.center_m,
                    next,
                    segment_fraction,
                ),
                RiverPathAlgorithm::PiecewiseLinearV1 => Vec3::new(
                    linear_interpolate(start.center_m.x, end.center_m.x, segment_fraction),
                    linear_interpolate(start.center_m.y, end.center_m.y, segment_fraction),
                    linear_interpolate(start.center_m.z, end.center_m.z, segment_fraction),
                ),
            };
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

/// Reuse the exact carved water polyline while retaining the selected optical profile.
pub fn water_from_carved_spline(
    mut document: WaterDocument,
    spline: &crate::WorldSketchCarvedSpline,
) -> Result<WaterDocument, WaterDocumentError> {
    if !matches!(
        spline.kind,
        crate::WorldSketchSplineKind::River | crate::WorldSketchSplineKind::Creek
    ) {
        return Err(WaterDocumentError::InvalidProfileRelation(
            "water requires a river or creek carving",
        ));
    }
    document.river.algorithm = RiverPathAlgorithm::PiecewiseLinearV1;
    document.river.elevation_policy = RiverElevationPolicy::AuthoredSurfaceV1;
    document.river.control_points = spline
        .centerline_samples
        .iter()
        .map(|sample| {
            control_point(
                sample.x_m,
                sample.surface_height_m,
                sample.z_m,
                spline.authored_width_m,
                spline.bed_depth_m,
            )
        })
        .collect();
    document.banks.shoulder_width_m = spline.shoulder_width_m;
    if let WaterRefractionProfile::ScreenSpace {
        ref mut max_depth_m,
        ..
    } = document.optics.refraction
    {
        *max_depth_m = max_depth_m.min(spline.bed_depth_m);
    }
    document.validate()?;
    Ok(document)
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
        TerrainGenerationPlan, embedded_visual_valley_environment_profile,
        embedded_visual_valley_terrain_document, embedded_visual_valley_vegetation_document,
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
            "c581e9ef65ca1bc34d5f94908d5f43df77794da4ad5b97e1b3c4f8c0d025ebce"
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
    fn carved_river_preserves_every_control_and_linear_segment() {
        let compiled =
            crate::compile_world_sketch(&crate::embedded_visual_valley_world_sketch().unwrap())
                .unwrap();
        let spline = compiled
            .spline_carving
            .splines
            .iter()
            .find(|s| s.kind == crate::WorldSketchSplineKind::River)
            .unwrap();
        let water =
            water_from_carved_spline(VisualValleyWaterRecipe::default().build().unwrap(), spline)
                .unwrap();
        assert_eq!(
            water.river.control_points.len(),
            spline.centerline_samples.len()
        );
        let reopened = WaterDocument::from_json(&serde_json::to_string(&water).unwrap()).unwrap();
        assert_eq!(
            water.content_hash().unwrap(),
            reopened.content_hash().unwrap()
        );
        for (control, carved) in water
            .river
            .control_points
            .iter()
            .zip(&spline.centerline_samples)
        {
            assert_eq!(
                control.center_m,
                Vec3::new(carved.x_m, carved.surface_height_m, carved.z_m)
            );
            assert_eq!(
                control.surface_width_m.to_bits(),
                spline.authored_width_m.to_bits()
            );
        }
        for sample in sample_river_centerline(&water).unwrap() {
            let index = usize::from(sample.source_segment_index);
            let start = water.river.control_points[index].center_m;
            let end = water.river.control_points[index + 1].center_m;
            assert!(
                (sample.center_m.x - linear_interpolate(start.x, end.x, sample.segment_fraction))
                    .abs()
                    < 0.0001
            );
            assert!(
                (sample.center_m.z - linear_interpolate(start.z, end.z, sample.segment_fraction))
                    .abs()
                    < 0.0001
            );
        }
        let road = compiled
            .spline_carving
            .splines
            .iter()
            .find(|s| s.kind == crate::WorldSketchSplineKind::Road)
            .unwrap();
        assert!(water_from_carved_spline(water, road).is_err());
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
        assert_eq!(first.river.control_points.len(), 62);
        assert!((267.0..268.0).contains(&first.river.approximate_path_length_m()));
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
            "e3c5c7763fa0773ed867f8644cd17be8a29e881eed1c40eefc5f3d13e83b27f8"
        );
    }

    #[test]
    fn authored_route_tracks_the_rendered_valley_and_requires_bank_projection() {
        let terrain = embedded_visual_valley_terrain_document().unwrap();
        let terrain = TerrainGenerationPlan::from_document(&terrain).unwrap();
        let water = embedded_visual_valley_water_document().unwrap();
        let samples = sample_river_centerline(&water).unwrap();
        let mut minimum_center_clearance_m = f32::INFINITY;
        let mut maximum_center_clearance_m = f32::NEG_INFINITY;
        let mut minimum_raw_edge_clearance_m = f32::INFINITY;
        let mut maximum_raw_edge_clearance_m = f32::NEG_INFINITY;

        for (index, sample) in samples.iter().enumerate() {
            let previous = samples[index.saturating_sub(1)].center_m;
            let next = samples[(index + 1).min(samples.len() - 1)].center_m;
            let delta_x = next.x - previous.x;
            let delta_z = next.z - previous.z;
            let inverse_length = delta_x.hypot(delta_z).recip();
            let lateral_x = delta_z * inverse_length;
            let lateral_z = -delta_x * inverse_length;
            let center_surface = terrain
                .surface_at(sample.center_m.x, sample.center_m.z)
                .unwrap();
            let center_clearance_m = sample.center_m.y - center_surface.height_m;
            minimum_center_clearance_m = minimum_center_clearance_m.min(center_clearance_m);
            maximum_center_clearance_m = maximum_center_clearance_m.max(center_clearance_m);

            for side in [-1.0_f32, 1.0] {
                let offset_m = side * sample.surface_width_m * 0.5;
                let x_m = sample.center_m.x + lateral_x * offset_m;
                let z_m = sample.center_m.z + lateral_z * offset_m;
                let edge_surface = terrain.surface_at(x_m, z_m).unwrap();
                let raw_edge_clearance_m = sample.center_m.y - edge_surface.height_m;
                minimum_raw_edge_clearance_m =
                    minimum_raw_edge_clearance_m.min(raw_edge_clearance_m);
                maximum_raw_edge_clearance_m =
                    maximum_raw_edge_clearance_m.max(raw_edge_clearance_m);
            }
        }

        assert_eq!(samples.len(), 137);
        assert!(minimum_center_clearance_m > 0.03);
        assert!(maximum_center_clearance_m < 0.32);
        assert!(minimum_raw_edge_clearance_m < -0.25);
        assert!(minimum_raw_edge_clearance_m > -0.30);
        assert!(maximum_raw_edge_clearance_m > 0.60);
        assert!(maximum_raw_edge_clearance_m < 0.65);
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
