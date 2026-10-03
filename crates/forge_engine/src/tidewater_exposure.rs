//! Tidewater's metering curve and mask shared by native play and Studio WebGPU.
use bevy::{
    asset::RenderAssetUsages,
    math::cubic_splines::LinearSpline,
    post_process::auto_exposure::AutoExposureCompensationCurve,
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};

// Tidewater PostFX.js targets 0.25 scene luminance, compensates dark views by
// only 80%, and bounds the adaptation multiplier to [0.6, 6.0]. Bevy meters
// log luminance and adds the curve's compensation to -log_luminance.
fn source_target_ev(log_luminance: f32) -> f32 {
    let ratio = 0.25 * 2.0_f32.powf(-log_luminance);
    let partial = if ratio > 1.0 { ratio.powf(0.8) } else { ratio };
    partial.clamp(0.6, 6.0).log2()
}

pub fn source_compensation_curve()
-> Result<AutoExposureCompensationCurve, Box<dyn std::error::Error>> {
    let stops = [
        -8.0,
        -2.0 - 6.0_f32.log2() / 0.8,
        -2.0,
        -2.0 - 0.6_f32.log2(),
        8.0,
    ];
    let points = stops.map(|stop| Vec2::new(stop, source_target_ev(stop) + stop));
    Ok(AutoExposureCompensationCurve::from_curve(
        LinearSpline::new(points),
    )?)
}

#[must_use]
pub fn source_metering_mask() -> Image {
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

    #[test]
    fn metering_inputs_are_finite_and_centre_weighted() {
        assert!(source_compensation_curve().is_ok());
        let image = source_metering_mask();
        let data = image.data.as_ref().unwrap();
        assert_eq!(data.len(), 64 * 64);
        assert!(data[32 * 64 + 32] > data[0]);
    }
}
