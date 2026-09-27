//! Original periodic cloud-density texture; generated once, not per rendered frame.
use forge_project::{CloudLayerSettings, CloudLayerSettingsError};
use thiserror::Error;
mod volume;
/// Physical thickness relative to the horizontal repeat scale of the preview volume.
pub const CLOUD_LAYER_DEPTH_RATIO: f32 = 0.12;
pub use volume::{CloudDensityVolume, generate_cloud_density_volume};

#[derive(Clone, Debug, PartialEq)]
pub struct CloudDensityMap {
    pub resolution: u16,
    /// Row-major linear density, not sRGB and not final opacity or lighting.
    pub samples: Vec<u8>,
}

#[derive(Debug, Error)]
pub enum CloudGenerationError {
    #[error(transparent)]
    Settings(#[from] CloudLayerSettingsError),
    #[error("cloud density resolution must be a power of two from 32 to 512")]
    Resolution,
    #[error("cloud volume resolution must be a power of two from 32 to 128")]
    VolumeResolution,
}

/// Coverage controls a monotonic density threshold, not an exact area percentage.
/// Wind, opacity, altitude and world scale do not require regenerating the texture.
pub fn generate_cloud_density(
    settings: CloudLayerSettings,
    resolution: u16,
) -> Result<CloudDensityMap, CloudGenerationError> {
    settings.validate()?;
    if !(32..=512).contains(&resolution) || !resolution.is_power_of_two() {
        return Err(CloudGenerationError::Resolution);
    }
    let mut samples = Vec::with_capacity(usize::from(resolution).pow(2));
    for z in 0..resolution {
        for x in 0..resolution {
            let u = f64::from(x) / f64::from(resolution);
            let v = f64::from(z) / f64::from(resolution);
            let value = [(4, 0.56), (8, 0.27), (16, 0.12), (32, 0.05)]
                .into_iter()
                .map(|(cells, weight)| periodic_noise(settings.seed, u, v, cells) * weight)
                .sum::<f64>();
            let density = if settings.coverage <= 0.0 {
                0.0
            } else if settings.coverage >= 1.0 {
                1.0
            } else {
                smooth((value - (1.0 - f64::from(settings.coverage)) + 0.1) / 0.2)
            };
            #[allow(
                clippy::cast_possible_truncation,
                clippy::cast_sign_loss,
                reason = "density is clamped to [0,1]"
            )]
            samples.push((density * 255.0).round() as u8);
        }
    }
    Ok(CloudDensityMap {
        resolution,
        samples,
    })
}

fn smooth(value: f64) -> f64 {
    let t = value.clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "wrapped coordinates lie within bounded lattice"
)]
fn periodic_noise(seed: u64, u: f64, v: f64, cells: u16) -> f64 {
    let x = u.rem_euclid(1.0) * f64::from(cells);
    let z = v.rem_euclid(1.0) * f64::from(cells);
    let ix = x.floor() as u16;
    let iz = z.floor() as u16;
    let tx = smooth(x - x.floor());
    let tz = smooth(z - z.floor());
    let lattice = |dx, dz| {
        let mut bits = seed
            ^ u64::from((ix + dx) % cells).wrapping_mul(0x9e37_79b9_7f4a_7c15)
            ^ u64::from((iz + dz) % cells).wrapping_mul(0xbf58_476d_1ce4_e5b9)
            ^ u64::from(cells);
        bits = (bits ^ (bits >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
        bits = (bits ^ (bits >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
        bits ^= bits >> 31;
        f64::from((bits >> 48) as u16) / 65_535.0
    };
    let lerp = |a, b, t| a + (b - a) * t;
    lerp(
        lerp(lattice(0, 0), lattice(1, 0), tx),
        lerp(lattice(0, 1), lattice(1, 1), tx),
        tz,
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    fn settings(coverage: f32) -> CloudLayerSettings {
        CloudLayerSettings {
            seed: 1337,
            altitude_m: 1500.0,
            pattern_scale_m: 4000.0,
            coverage,
            opacity: 0.8,
            wind_xz_m_per_second: [10.0, -5.0],
        }
    }

    #[test]
    fn deterministic_seeded_monotonic_and_bounded() {
        let low = generate_cloud_density(settings(0.35), 64).unwrap();
        let high = generate_cloud_density(settings(0.65), 64).unwrap();
        assert_eq!(low, generate_cloud_density(settings(0.35), 64).unwrap());
        assert_eq!(low.samples.len(), 4096);
        assert!(low.samples.iter().zip(&high.samples).all(|(a, b)| a <= b));
        assert!(low.samples.iter().any(|v| *v > 0));
        assert!(low.samples.contains(&0));
        assert_ne!(
            low,
            generate_cloud_density(
                CloudLayerSettings {
                    seed: 42,
                    ..settings(0.35)
                },
                64
            )
            .unwrap()
        );
        for (coverage, expected) in [(0.0, 0), (1.0, 255)] {
            assert!(
                generate_cloud_density(settings(coverage), 32)
                    .unwrap()
                    .samples
                    .iter()
                    .all(|v| *v == expected)
            );
        }
        assert_eq!(
            generate_cloud_density(settings(0.5), 512)
                .unwrap()
                .samples
                .len(),
            262_144
        );
        for resolution in [0, 16, 33, 1024, u16::MAX] {
            assert!(generate_cloud_density(settings(0.5), resolution).is_err());
        }
    }

    #[test]
    fn seam_is_periodic_and_continuous() {
        for cells in [4, 8, 16, 32] {
            for v in [0.0, 0.37, 0.95, 1.0] {
                assert_eq!(
                    periodic_noise(42, 0.0, v, cells).to_bits(),
                    periodic_noise(42, 1.0, v, cells).to_bits()
                );
                assert!(
                    (periodic_noise(42, -0.000_001, v, cells)
                        - periodic_noise(42, 0.000_001, v, cells))
                    .abs()
                        < 0.000_001
                );
                assert_eq!(
                    periodic_noise(42, v, 0.0, cells).to_bits(),
                    periodic_noise(42, v, 1.0, cells).to_bits()
                );
            }
        }
    }

    #[test]
    fn presentation_parameters_do_not_reseed_the_field() {
        let source = settings(0.5);
        let moved = CloudLayerSettings {
            altitude_m: 2000.0,
            opacity: 0.3,
            pattern_scale_m: 5000.0,
            wind_xz_m_per_second: [0.0; 2],
            ..source
        };
        assert_eq!(
            generate_cloud_density(source, 64).unwrap(),
            generate_cloud_density(moved, 64).unwrap()
        );
    }
}
