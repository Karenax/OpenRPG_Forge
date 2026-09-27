//! Continuous seeded cloud morphology, independent of GPU upload and ray traversal.
use super::{CloudGenerationError, periodic_noise, smooth};
use forge_project::CloudLayerSettings;

#[derive(Clone, Debug, PartialEq)]
pub struct CloudDensityVolume {
    pub resolution: u16,
    /// Linear R8, x fastest, then y (height), then z. Horizontal axes repeat;
    /// top and bottom faces are empty. This is density, not final opacity.
    pub samples: Vec<u8>,
}

/// Derives an isolated candidate volume without changing the existing 2D field.
/// Maximum allocation is 128³ bytes. Presentation parameters do not alter morphology.
pub fn generate_cloud_density_volume(
    settings: CloudLayerSettings,
    resolution: u16,
) -> Result<CloudDensityVolume, CloudGenerationError> {
    settings.validate()?;
    if !(32..=128).contains(&resolution) || !resolution.is_power_of_two() {
        return Err(CloudGenerationError::VolumeResolution);
    }
    let side = usize::from(resolution);
    let lobes = cloud_lobes(settings.seed);
    let mut samples = Vec::with_capacity(side.pow(3));
    for z in 0..resolution {
        for y in 0..resolution {
            for x in 0..resolution {
                let density = sample_with_lobes(
                    settings,
                    &lobes,
                    f64::from(x) / f64::from(resolution),
                    f64::from(y) / f64::from(resolution - 1),
                    f64::from(z) / f64::from(resolution),
                );
                #[allow(
                    clippy::cast_possible_truncation,
                    clippy::cast_sign_loss,
                    reason = "normalized density is clamped before R8 quantization"
                )]
                samples.push((density * 255.0).round() as u8);
            }
        }
    }
    Ok(CloudDensityVolume {
        resolution,
        samples,
    })
}

#[cfg(test)]
fn sample(settings: CloudLayerSettings, u: f64, height: f64, v: f64) -> f64 {
    sample_with_lobes(settings, &cloud_lobes(settings.seed), u, height, v)
}

fn sample_with_lobes(
    settings: CloudLayerSettings,
    lobes: &[CloudLobe; 64],
    u: f64,
    height: f64,
    v: f64,
) -> f64 {
    if settings.coverage <= 0.0 || !(0.0..1.0).contains(&height) || height == 0.0 {
        return 0.0;
    }
    // Broad coverage is shared vertically. Independent 3D erosion sculpts the
    // interior instead of copying one horizontal field at discrete heights.
    // Periodic domain deformation breaks the lattice-aligned, kilometre-wide
    // smooth plates. This work runs only when deriving the volume, not per frame.
    let warped_u = u + (periodic_noise(settings.seed ^ 0x510e_527f, u, v, 8) - 0.5) * 0.08;
    let warped_v = v + (periodic_noise(settings.seed ^ 0x9b05_688c, u, v, 8) - 0.5) * 0.08;
    let weather = periodic_noise(settings.seed, warped_u, warped_v, 8);
    // Noise is sampled in units of horizontal world scale on all three axes.
    // Using normalized height directly would squash eight times as many lobes
    // into the shallow layer, producing thin horizontal strata.
    let world_height = height * f64::from(super::CLOUD_LAYER_DEPTH_RATIO);
    let erosion = noise3(
        settings.seed ^ 0x6a09_e667,
        warped_u,
        world_height,
        warped_v,
        16,
    );
    let distance = lobe_distance(lobes, [warped_u, world_height, warped_v]);
    let lobes = smooth((1.0 - erosion * 0.15 - distance) / 0.2);
    let bottom = smooth(height / 0.06);
    let top = smooth((1.0 - height) / 0.15);
    let threshold = 1.0 - f64::from(settings.coverage);
    let coverage = smooth((weather - threshold + 0.15) / 0.3);
    lobes * coverage * bottom * top
}

#[derive(Clone, Copy, Debug, PartialEq)]
struct CloudLobe {
    center: [f64; 3],
    inverse_radius: f64,
}

/// Fixed-size table computed once per texture; no grid or per-voxel randomness.
fn cloud_lobes(seed: u64) -> [CloudLobe; 64] {
    let mut state = seed;
    let mut random = || {
        state = state.wrapping_add(0x9e37_79b9_7f4a_7c15);
        let mut bits = state;
        bits = (bits ^ (bits >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
        bits = (bits ^ (bits >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
        bits ^= bits >> 31;
        f64::from(u32::try_from(bits >> 32).expect("upper half fits u32")) / f64::from(u32::MAX)
    };
    std::array::from_fn(|_| CloudLobe {
        center: [
            random(),
            (0.2 + random() * 0.6) * f64::from(super::CLOUD_LAYER_DEPTH_RATIO),
            random(),
        ],
        inverse_radius: (0.035 + random() * 0.04).recip(),
    })
}

fn lobe_distance(lobes: &[CloudLobe; 64], position: [f64; 3]) -> f64 {
    let mut nearest_squared = f64::INFINITY;
    for lobe in lobes {
        let squared = (0..3)
            .map(|axis| {
                let difference = position[axis] - lobe.center[axis];
                let delta = if axis == 1 {
                    difference
                } else {
                    (difference + 0.5).rem_euclid(1.0) - 0.5
                };
                (delta * lobe.inverse_radius).powi(2)
            })
            .sum::<f64>();
        nearest_squared = nearest_squared.min(squared);
    }
    nearest_squared.sqrt()
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "coordinates wrap into the bounded periodic lattice"
)]
fn noise3(seed: u64, u: f64, h: f64, v: f64, cells: u16) -> f64 {
    let coords = [u, h, v].map(|c| c.rem_euclid(1.0) * f64::from(cells));
    let base = coords.map(|c| c.floor() as u16);
    let blend = coords.map(|c| smooth(c - c.floor()));
    let mut value = 0.0;
    for z in 0..=1_u16 {
        for y in 0..=1_u16 {
            for x in 0..=1_u16 {
                let offsets = [x, y, z];
                let mut bits = seed ^ u64::from(cells);
                let mut weight = 1.0;
                for axis in 0..3 {
                    bits ^= u64::from((base[axis] + offsets[axis]) % cells).wrapping_mul(
                        [
                            0x9e37_79b9_7f4a_7c15,
                            0xbf58_476d_1ce4_e5b9,
                            0x94d0_49bb_1331_11eb,
                        ][axis],
                    );
                    weight *= if offsets[axis] == 0 {
                        1.0 - blend[axis]
                    } else {
                        blend[axis]
                    };
                }
                bits = (bits ^ (bits >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
                bits = (bits ^ (bits >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
                bits ^= bits >> 31;
                value += weight * f64::from((bits >> 48) as u16) / 65_535.0;
            }
        }
    }
    value
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn lobes_repeat_without_a_seam_at_horizontal_volume_boundaries() {
        for seed in [0, 42, 1337, u64::MAX] {
            let lobes = cloud_lobes(seed);
            assert_eq!(lobes, cloud_lobes(seed));
            assert_ne!(lobes, cloud_lobes(seed.wrapping_add(1)));
            assert!(
                lobes.windows(2).any(
                    |pair| pair[0].inverse_radius.to_bits() != pair[1].inverse_radius.to_bits()
                )
            );
            for axis in [0, 2] {
                for edge in [0.0, 0.125, 0.5, 1.0] {
                    let mut left = [0.23, 0.37, 0.61];
                    left[axis] = edge - 1e-7;
                    let mut right = left;
                    right[axis] = edge + 1e-7;
                    // Distance is now normalized by radius, whose minimum is 0.035.
                    assert!(
                        (lobe_distance(&lobes, left) - lobe_distance(&lobes, right)).abs()
                            < 2e-7 / 0.035 + 1e-12
                    );
                    let mut repeated = left;
                    repeated[axis] += 1.0;
                    assert!(
                        (lobe_distance(&lobes, left) - lobe_distance(&lobes, repeated)).abs()
                            < 1e-12
                    );
                }
            }
        }
    }

    fn settings() -> CloudLayerSettings {
        CloudLayerSettings {
            seed: 1337,
            altitude_m: 1500.0,
            pattern_scale_m: 4000.0,
            coverage: 0.52,
            opacity: 0.85,
            wind_xz_m_per_second: [10.0, -5.0],
        }
    }

    #[test]
    fn volume_is_repeatable_bounded_and_not_repeated_slices() {
        let source = settings();
        let volume = generate_cloud_density_volume(source, 32).unwrap();
        assert_eq!(volume, generate_cloud_density_volume(source, 32).unwrap());
        assert_eq!(volume.samples.len(), 32usize.pow(3));
        assert!(volume.samples.iter().any(|v| *v > 0));
        for z in 0..32 {
            for x in 0..32 {
                assert_eq!(volume.samples[x + z * 1024], 0);
                assert_eq!(volume.samples[x + 31 * 32 + z * 1024], 0);
            }
        }
        assert!((0..32).any(|z| {
            (0..32).any(|x| {
                volume.samples[x + 12 * 32 + z * 1024] != volume.samples[x + 20 * 32 + z * 1024]
            })
        }));
        assert_ne!(
            volume,
            generate_cloud_density_volume(CloudLayerSettings { seed: 42, ..source }, 32).unwrap()
        );
        for resolution in [0, 16, 33, 256, u16::MAX] {
            assert!(generate_cloud_density_volume(source, resolution).is_err());
        }
    }

    #[test]
    fn field_is_continuous_periodic_and_monotonic_in_coverage() {
        let source = settings();
        for height in [0.1, 0.37, 0.75, 0.95] {
            for other in [0.0, 0.23, 0.91] {
                assert!(
                    (sample(source, 0.0, height, other) - sample(source, 1.0, height, other)).abs()
                        < 1e-12
                );
                assert!(
                    (sample(source, other, height, 0.0) - sample(source, other, height, 1.0)).abs()
                        < 1e-12
                );
                assert!(
                    (sample(source, other, height - 1e-6, 0.37)
                        - sample(source, other, height + 1e-6, 0.37))
                    .abs()
                        < 0.001
                );
                let low = sample(
                    CloudLayerSettings {
                        coverage: 0.3,
                        ..source
                    },
                    other,
                    height,
                    0.37,
                );
                let high = sample(
                    CloudLayerSettings {
                        coverage: 0.7,
                        ..source
                    },
                    other,
                    height,
                    0.37,
                );
                assert!(low <= high);
            }
        }
        let empty = generate_cloud_density_volume(
            CloudLayerSettings {
                coverage: 0.0,
                ..source
            },
            32,
        )
        .unwrap();
        assert!(empty.samples.iter().all(|v| *v == 0));
        let moved = CloudLayerSettings {
            altitude_m: 2000.0,
            pattern_scale_m: 5000.0,
            opacity: 0.2,
            wind_xz_m_per_second: [0.0; 2],
            ..source
        };
        assert_eq!(
            generate_cloud_density_volume(source, 32).unwrap(),
            generate_cloud_density_volume(moved, 32).unwrap()
        );
    }

    #[test]
    fn maximum_volume_and_invalid_settings_respect_the_budget() {
        let source = settings();
        let maximum = generate_cloud_density_volume(source, 128).unwrap();
        assert_eq!(maximum.samples.len(), 2_097_152);
        assert!(
            generate_cloud_density_volume(
                CloudLayerSettings {
                    coverage: f32::NAN,
                    ..source
                },
                32
            )
            .is_err()
        );
        assert!(
            generate_cloud_density_volume(
                CloudLayerSettings {
                    altitude_m: 0.0,
                    ..source
                },
                32
            )
            .is_err()
        );
    }
}
