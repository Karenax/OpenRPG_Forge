//! One-shot texture readbacks for held comparisons; no live gameplay cost.
use super::{Observation, Targets};
use bevy::prelude::*;
use bevy::render::gpu_readback::{Readback, ReadbackComplete};

#[derive(Resource, Default, serde::Serialize)]
pub struct GpuObservation {
    pub color: Option<Sample>,
    pub depth: Option<Sample>,
}

#[derive(Clone, serde::Serialize)]
pub struct Sample {
    pub size: [u32; 2],
    pub pixels: u64,
    pub covered_pixels: u64,
    pub nonfinite_pixels: u64,
    pub mean_rgb: [f64; 3],
    pub min: f32,
    pub max: f32,
    pub checksum64: u64,
}

#[derive(Component)]
struct Probe {
    size: UVec2,
    color: bool,
}
#[derive(Resource, Default)]
struct HoldStarted(Option<u32>);

/// A held camera is required; observations are asynchronous, not a render fence.
pub fn observe_while_held(world: &mut World, held: bool) {
    world.init_resource::<HoldStarted>();
    let frame = world.resource::<bevy::diagnostic::FrameCount>().0;
    let probes = world
        .query_filtered::<Entity, With<Probe>>()
        .iter(world)
        .collect::<Vec<_>>();
    if !held {
        world.resource_mut::<HoldStarted>().0 = None;
        for entity in probes {
            world.despawn(entity);
        }
        if world.contains_resource::<GpuObservation>() {
            world.insert_resource(GpuObservation::default());
        }
        return;
    }
    let started = *world.resource_mut::<HoldStarted>().0.get_or_insert(frame);
    if frame.saturating_sub(started) < 40 {
        return;
    }
    if !probes.is_empty() || !world.get_resource::<Observation>().is_some_and(|o| o.ready) {
        return;
    }
    let targets = world.resource::<Targets>().clone();
    let size = UVec2::from_array(world.resource::<Observation>().size);
    world.init_resource::<GpuObservation>();
    for (image, color) in [(targets.color, true), (targets.depth, false)] {
        world
            .spawn((Readback::texture(image), Probe { size, color }))
            .observe(receive);
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn receive(
    event: On<ReadbackComplete>,
    probes: Query<&Probe>,
    mut observation: ResMut<GpuObservation>,
    mut commands: Commands,
) {
    let Ok(probe) = probes.get(event.entity) else {
        return;
    };
    let Some(sample) = decode(&event.data, probe.size, probe.color) else {
        return;
    };
    if probe.color {
        observation.color = Some(sample);
    } else {
        observation.depth = Some(sample);
    }
    commands.entity(event.entity).remove::<Readback>();
}

fn decode(bytes: &[u8], size: UVec2, color: bool) -> Option<Sample> {
    let pixel_size = if color { 8_usize } else { 4 };
    let row_bytes = usize::try_from(size.x).ok()?.checked_mul(pixel_size)?;
    let stride = row_bytes.div_ceil(256) * 256;
    let height = usize::try_from(size.y).ok()?;
    if size.x == 0 || size.y == 0 || bytes.len() != stride.checked_mul(height)? {
        return None;
    }
    let pixels = u64::from(size.x) * u64::from(size.y);
    let mut result = Sample {
        size: size.to_array(),
        pixels,
        covered_pixels: 0,
        nonfinite_pixels: 0,
        mean_rgb: [0.0; 3],
        min: f32::INFINITY,
        max: f32::NEG_INFINITY,
        checksum64: 0xcbf2_9ce4_8422_2325,
    };
    for row in bytes.chunks_exact(stride) {
        for pixel in row[..row_bytes].chunks_exact(pixel_size) {
            // Ignore aligned row padding. This is a diagnostic checksum, not
            // a cryptographic provenance hash or a substitute for RGB evidence.
            for byte in pixel {
                result.checksum64 =
                    (result.checksum64 ^ u64::from(*byte)).wrapping_mul(0x0100_0000_01b3);
            }
            let values = if color {
                std::array::from_fn(|channel| {
                    half(u16::from_le_bytes([
                        pixel[channel * 2],
                        pixel[channel * 2 + 1],
                    ]))
                })
            } else {
                [f32::from_le_bytes(pixel.try_into().ok()?), 0.0, 0.0, 0.0]
            };
            if values.iter().any(|value| !value.is_finite()) {
                result.nonfinite_pixels += 1;
                continue;
            }
            let coverage = if color { values[3] } else { values[0] };
            result.covered_pixels += u64::from(coverage > if color { 0.5 } else { 0.0 });
            result.min = result.min.min(coverage);
            result.max = result.max.max(coverage);
            for (sum, value) in result.mean_rgb.iter_mut().zip(values) {
                *sum += f64::from(value);
            }
        }
    }
    if result.nonfinite_pixels == pixels {
        result.min = 0.0;
        result.max = 0.0;
    }
    // Canvas dimensions are u32, so the pixel count fits the exact f64 range.
    #[allow(clippy::cast_precision_loss)]
    for sum in &mut result.mean_rgb {
        *sum /= pixels as f64;
    }
    Some(result)
}

fn half(bits: u16) -> f32 {
    let sign = if bits & 0x8000 == 0 { 1.0 } else { -1.0 };
    let exponent = (bits >> 10) & 31;
    let fraction = bits & 1023;
    match exponent {
        0 => sign * f32::from(fraction) * 2.0_f32.powi(-24),
        31 => {
            if fraction == 0 {
                sign * f32::INFINITY
            } else {
                f32::NAN
            }
        }
        _ => sign * (1.0 + f32::from(fraction) / 1024.0) * 2.0_f32.powi(i32::from(exponent) - 15),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Known binary16 powers of two and decoded channel means have exact fixture values."
    )]
    fn half_depth_and_row_padding_are_decoded_without_counting_padding() {
        let mut color = vec![255; 512];
        color[..8].copy_from_slice(&[0, 60, 0, 56, 0, 52, 0, 60]);
        color[256..264].fill(0);
        let sample = decode(&color, UVec2::new(1, 2), true).unwrap();
        assert_eq!(sample.covered_pixels, 1);
        assert_eq!(sample.nonfinite_pixels, 0);
        assert_eq!(sample.mean_rgb, [0.5, 0.25, 0.125]);
        let mut depth = vec![0; 256];
        depth[..4].copy_from_slice(&0.5_f32.to_le_bytes());
        assert_eq!(decode(&depth, UVec2::ONE, false).unwrap().covered_pixels, 1);
        assert!(decode(&depth[..255], UVec2::ONE, false).is_none());
        assert_eq!(half(1), 2.0_f32.powi(-24));
        assert!(half(0x7c00).is_infinite());
        assert!(half(0x7e00).is_nan());
    }
}
