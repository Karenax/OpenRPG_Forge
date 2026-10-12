//! CPU mip admission for PNG textures loaded by the standard glTF adapter.
use bevy::{
    image::{ImageFilterMode, ImageSampler, ImageSamplerDescriptor},
    prelude::Image,
    render::render_resource::TextureFormat,
};

pub fn prepare(image: &mut Image, normal: bool) -> Result<(), &'static str> {
    if image.texture_descriptor.mip_level_count > 1 {
        return Ok(());
    }
    let srgb = match image.texture_descriptor.format {
        TextureFormat::Rgba8UnormSrgb => true,
        TextureFormat::Rgba8Unorm => false,
        _ => return Err("static texture requires RGBA8"),
    };
    let mut w = image.texture_descriptor.size.width as usize;
    let mut h = image.texture_descriptor.size.height as usize;
    if w == 0
        || h == 0
        || w > 2048
        || h > 2048
        || !w.is_power_of_two()
        || !h.is_power_of_two()
        || image.texture_descriptor.size.depth_or_array_layers != 1
    {
        return Err("unsupported static texture layout");
    }
    let Some(data) = image.data.as_mut() else {
        return Err("static texture source pixels unavailable");
    };
    if data.len() != w * h * 4 {
        return Err("static texture pixel length mismatch");
    }
    let (to_linear, to_srgb) = transfer_tables();
    let mut previous = data.clone();
    let mut levels = 1;
    while w > 1 || h > 1 {
        let nw = (w / 2).max(1);
        let nh = (h / 2).max(1);
        let mut next = vec![0; 4 * nw * nh];
        for y in 0..nh {
            for x in 0..nw {
                let samples = [
                    (x * 2, y * 2),
                    ((x * 2 + 1).min(w - 1), y * 2),
                    (x * 2, (y * 2 + 1).min(h - 1)),
                    ((x * 2 + 1).min(w - 1), (y * 2 + 1).min(h - 1)),
                ]
                .map(|(sx, sy)| &previous[4 * (sy * w + sx)..4 * (sy * w + sx) + 4]);
                let dest = &mut next[4 * (y * nw + x)..4 * (y * nw + x) + 4];
                if normal {
                    let n: [f32; 3] = std::array::from_fn(|c| {
                        samples
                            .iter()
                            .map(|p| f32::from(p[c]) / 127.5 - 1.)
                            .sum::<f32>()
                            * 0.25
                    });
                    let length = (n[0] * n[0] + n[1] * n[1] + n[2] * n[2]).sqrt();
                    let n = if length > 1e-6 {
                        n.map(|v| v / length)
                    } else {
                        [0., 0., 1.]
                    };
                    for c in 0..3 {
                        dest[c] = rounded_byte((n[c] + 1.) * 127.5);
                    }
                } else {
                    for c in 0..3 {
                        dest[c] = if srgb {
                            let v = samples
                                .iter()
                                .map(|p| to_linear[p[c] as usize])
                                .sum::<f32>()
                                * 0.25;
                            to_srgb[srgb_index(v)]
                        } else {
                            mean_byte(&samples, c)
                        };
                    }
                }
                dest[3] = mean_byte(&samples, 3);
            }
        }
        data.extend_from_slice(&next);
        previous = next;
        w = nw;
        h = nh;
        levels += 1;
    }
    image.texture_descriptor.mip_level_count = levels;
    let mut sampler = match &image.sampler {
        ImageSampler::Descriptor(d) => d.clone(),
        ImageSampler::Default => ImageSamplerDescriptor::default(),
    };
    sampler.mag_filter = ImageFilterMode::Linear;
    sampler.min_filter = ImageFilterMode::Linear;
    sampler.mipmap_filter = ImageFilterMode::Linear;
    sampler.anisotropy_clamp = 8;
    image.sampler = ImageSampler::Descriptor(sampler);
    Ok(())
}

fn transfer_tables() -> (Vec<f32>, Vec<u8>) {
    let to_linear = (0u16..256)
        .map(|v| {
            let x = f32::from(v) / 255.;
            if x <= 0.04045 {
                x / 12.92
            } else {
                ((x + 0.055) / 1.055).powf(2.4)
            }
        })
        .collect();
    let to_srgb = (0u16..=4096)
        .map(|v| {
            let x = f32::from(v) / 4096.;
            let x = if x <= 0.003_130_8 {
                x * 12.92
            } else {
                1.055 * x.powf(1. / 2.4) - 0.055
            };
            rounded_byte(x * 255.)
        })
        .collect();
    (to_linear, to_srgb)
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "Round and clamp to the byte range before pixel quantization"
)]
fn rounded_byte(value: f32) -> u8 {
    value.round().clamp(0., 255.) as u8
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "Round and clamp to the inclusive 0..=4096 transfer-table index range"
)]
fn srgb_index(value: f32) -> usize {
    (value * 4096.).round().clamp(0., 4096.) as usize
}

fn mean_byte(samples: &[&[u8]; 4], channel: usize) -> u8 {
    let rounded = (samples.iter().map(|p| u32::from(p[channel])).sum::<u32>() + 2) / 4;
    u8::try_from(rounded).expect("The rounded mean of four bytes fits in a byte")
}
