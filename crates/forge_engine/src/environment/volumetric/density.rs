use bevy::{
    asset::RenderAssetUsages,
    image::ImageSampler,
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat},
};

const SIDE: u32 = 32;

/// Small deterministic derived texture; no external asset or source mutation.
pub(super) fn image() -> Image {
    let mut bytes = Vec::with_capacity((SIDE * SIDE * SIDE) as usize);
    for z in 0..SIDE {
        for y in 0..SIDE {
            for x in 0..SIDE {
                let density = edge(x) * edge(y) * edge(z);
                #[allow(
                    clippy::cast_possible_truncation,
                    clippy::cast_sign_loss,
                    reason = "bounded normalized density rounded into R8"
                )]
                bytes.push((density * 255.0).round() as u8);
            }
        }
    }
    let mut image = Image::new(
        Extent3d {
            width: SIDE,
            height: SIDE,
            depth_or_array_layers: SIDE,
        },
        TextureDimension::D3,
        bytes,
        TextureFormat::R8Unorm,
        RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
    );
    image.sampler = ImageSampler::linear();
    image
}

fn edge(index: u32) -> f32 {
    #[allow(clippy::cast_precision_loss, reason = "indices are at most 31")]
    let distance = index.min(SIDE - 1 - index) as f32 / (SIDE - 1) as f32;
    let t = (distance / 0.25).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

#[derive(Resource)]
struct DensityTexture(Handle<Image>);

pub(super) fn handle(world: &mut World) -> Handle<Image> {
    if let Some(cached) = world.get_resource::<DensityTexture>() {
        return cached.0.clone();
    }
    let handle = world.resource_mut::<Assets<Image>>().add(image());
    world.insert_resource(DensityTexture(handle.clone()));
    handle
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn density_is_deterministic_zero_on_all_faces_and_full_at_center() {
        let first = image();
        let bytes = first.data.as_ref().unwrap();
        assert_eq!(bytes.len(), 32 * 32 * 32);
        assert_eq!(first.data, image().data);
        let mut world = World::new();
        world.init_resource::<Assets<Image>>();
        let original = handle(&mut world);
        assert_eq!(original, handle(&mut world));
        assert_eq!(world.resource::<Assets<Image>>().len(), 1);
        for z in 0..32usize {
            for y in 0..32usize {
                for x in 0..32usize {
                    let value = bytes[x + y * 32 + z * 32 * 32];
                    if [x, y, z].iter().any(|v| *v == 0 || *v == 31) {
                        assert_eq!(value, 0);
                    }
                }
            }
        }
        assert_eq!(bytes[16 + 16 * 32 + 16 * 32 * 32], 255);
        for x in 0..16usize {
            assert!(bytes[x + 16 * 32 + 16 * 32 * 32] <= bytes[x + 1 + 16 * 32 + 16 * 32 * 32]);
        }
    }
}
