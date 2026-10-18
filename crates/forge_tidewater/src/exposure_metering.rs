//! Scene-independent exposure metering shared by native play and Studio WebGPU.
//! Initial default tuning derives from Tidewater PostFX.js; it is not a scene ID.
use bevy::{
    asset::RenderAssetUsages,
    math::cubic_splines::{CubicCurve, CubicGenerator, CubicSegment},
    post_process::auto_exposure::AutoExposureCompensationCurve,
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};

/// Renderer inputs. Project documents remain the authority for persisted settings.
#[derive(Clone, Copy, Debug)]
pub struct ExposureProfile {
    pub target_luminance: f32,
    pub dark_adaptation: f32,
    pub min_multiplier: f32,
    pub max_multiplier: f32,
}

impl Default for ExposureProfile {
    fn default() -> Self {
        Self {
            target_luminance: 0.25,
            dark_adaptation: 0.8,
            min_multiplier: 0.6,
            max_multiplier: 6.0,
        }
    }
}

impl ExposureProfile {
    pub fn compensation_curve(
        self,
    ) -> Result<AutoExposureCompensationCurve, Box<dyn std::error::Error>> {
        if !self.target_luminance.is_finite()
            || self.target_luminance <= 0.0
            || !self.dark_adaptation.is_finite()
            || self.dark_adaptation <= 0.0
            || self.dark_adaptation > 1.0
            || !self.min_multiplier.is_finite()
            || self.min_multiplier <= 0.0
            || self.min_multiplier >= 1.0
            || !self.max_multiplier.is_finite()
            || self.max_multiplier <= 1.0
        {
            return Err("Invalid exposure metering profile".into());
        }
        let target_ev = self.target_luminance.log2();
        let lower = target_ev - self.max_multiplier.log2() / self.dark_adaptation;
        let upper = target_ev - self.min_multiplier.log2();
        if !lower.is_finite() || !upper.is_finite() || lower >= target_ev || upper <= target_ev {
            return Err("Exposure metering stops are not finite and ordered".into());
        }
        let stops = [
            (-8.0_f32).min(lower - 1.0),
            lower,
            target_ev,
            upper,
            8.0_f32.max(upper + 1.0),
        ];
        // Bevy adds the curve's compensation to -log_luminance.
        let points = stops.map(|stop| Vec2::new(stop, self.target_ev(stop) + stop));
        Ok(AutoExposureCompensationCurve::from_curve(
            ContinuousMeteringSpline(points),
        )?)
    }

    fn target_ev(self, log_luminance: f32) -> f32 {
        let ratio = self.target_luminance * 2.0_f32.powf(-log_luminance);
        let partial = if ratio > 1.0 {
            ratio.powf(self.dark_adaptation)
        } else {
            ratio
        };
        partial
            .clamp(self.min_multiplier, self.max_multiplier)
            .log2()
    }
}

// Bevy requires exact equality between adjacent polynomial endpoints. Building
// each segment from its predecessor's evaluated endpoint avoids an f32 ULP gap
// for valid profiles whose target EV is not the original default.
struct ContinuousMeteringSpline([Vec2; 5]);

impl CubicGenerator<Vec2> for ContinuousMeteringSpline {
    type Error = &'static str;

    fn to_curve(&self) -> Result<CubicCurve<Vec2>, Self::Error> {
        let mut start = self.0[0];
        let segments = self.0[1..].iter().map(|&end| {
            let segment = CubicSegment {
                coeff: [start, end - start, Vec2::ZERO, Vec2::ZERO],
            };
            start = segment.position(1.0);
            segment
        });
        CubicCurve::from_segments(segments).ok_or("Exposure metering requires curve segments")
    }
}

#[must_use]
pub fn center_weighted_metering_mask() -> Image {
    const SIZE: u16 = 64;
    let mut image = Image::new_fill(
        Extent3d {
            width: u32::from(SIZE),
            height: u32::from(SIZE),
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        &[0],
        TextureFormat::R8Unorm,
        RenderAssetUsages::default(),
    );
    let mut pixels = Vec::with_capacity(usize::from(SIZE) * usize::from(SIZE));
    for y in 0..SIZE {
        for x in 0..SIZE {
            let dx = (f32::from(x) + 0.5) / f32::from(SIZE) - 0.5;
            let dy = ((f32::from(y) + 0.5) / f32::from(SIZE) - 0.5) * 1.4;
            let weight = (1.0 - dx.hypot(dy) * 1.2).max(0.15);
            #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
            pixels.push((weight * 255.0).round() as u8);
        }
    }
    image.data = Some(pixels);
    image
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::math::cubic_splines::LinearSpline;

    #[test]
    fn default_curve_keeps_the_original_polynomial_coefficients() {
        let profile = ExposureProfile::default();
        let stops = [
            -8.0,
            -2.0 - 6.0_f32.log2() / 0.8,
            -2.0,
            -2.0 - 0.6_f32.log2(),
            8.0,
        ];
        let points = stops.map(|stop| Vec2::new(stop, profile.target_ev(stop) + stop));
        let original = LinearSpline::new(points).to_curve().unwrap();
        let shared = ContinuousMeteringSpline(points).to_curve().unwrap();
        for (before, after) in original.segments().iter().zip(shared.segments()) {
            let bits = |values: [Vec2; 4]| values.map(|v| v.to_array().map(f32::to_bits));
            assert_eq!(bits(before.coeff), bits(after.coeff));
        }
    }

    #[test]
    fn default_profile_preserves_the_original_exposure_response() {
        let profile = ExposureProfile::default();
        for index in -256..=256 {
            let luminance = f32::from(i16::try_from(index).unwrap()) / 16.0;
            let ratio = 0.25 * 2.0_f32.powf(-luminance);
            let partial = if ratio > 1.0 { ratio.powf(0.8) } else { ratio };
            assert!((profile.target_ev(luminance) - partial.clamp(0.6, 6.0).log2()).abs() < 1e-6);
        }
    }

    #[test]
    fn metering_inputs_are_finite_and_centre_weighted() {
        assert!(ExposureProfile::default().compensation_curve().is_ok());
        let image = center_weighted_metering_mask();
        let data = image.data.as_ref().unwrap();
        assert_eq!(data.len(), 64 * 64);
        assert!(data[32 * 64 + 32] > data[0]);
    }
}
