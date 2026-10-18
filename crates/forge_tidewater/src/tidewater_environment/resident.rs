//! Scene-owned compute outputs seeded from the acquired environment.
use bevy::{
    asset::RenderAssetUsages,
    prelude::*,
    render::{extract_resource::ExtractResource, render_resource as gpu, storage::ShaderBuffer},
};

/// Keep these handles for the entire generation. Materials sample displayed;
/// the controller copies a completed filtered target there at each swap.
#[derive(Clone, Resource, ExtractResource)]
pub struct Targets {
    pub source: Handle<Image>,
    pub filtered: [Handle<Image>; 2],
    pub displayed: Handle<Image>,
    pub sh: [Handle<ShaderBuffer>; 2],
}

/// Validate all seed data before allocating assets. Both filtered targets and
/// SH buffers retain the acquired lighting until a complete refresh replaces it.
/// No neutral/black substitute is published, and no renderer is created here.
pub fn stage(world: &mut World, acquired: &Image, sh: [Vec4; 9]) -> Result<Targets, String> {
    if !world.contains_resource::<Assets<Image>>()
        || !world.contains_resource::<Assets<ShaderBuffer>>()
    {
        return Err("Environment requires initialized image and buffer assets".into());
    }
    let descriptor = &acquired.texture_descriptor;
    if descriptor.size
        != (gpu::Extent3d {
            width: 128,
            height: 128,
            depth_or_array_layers: 6,
        })
        || descriptor.dimension != gpu::TextureDimension::D2
        || descriptor.format != gpu::TextureFormat::Rgba16Float
        || descriptor.mip_level_count != 6
        || descriptor.sample_count != 1
        || sh.iter().any(|v| !v.is_finite())
    {
        return Err("Environment seed dimensions or SH differ".into());
    }
    let mut filtered = super::cube_rgba16f(
        128,
        6,
        acquired
            .data
            .as_deref()
            .ok_or("Environment seed bytes absent")?,
    )?;
    let usage = gpu::TextureUsages::TEXTURE_BINDING
        | gpu::TextureUsages::STORAGE_BINDING
        | gpu::TextureUsages::COPY_SRC
        | gpu::TextureUsages::COPY_DST;
    filtered.texture_descriptor.usage = usage;
    let mut source = Image::new_uninit(
        gpu::Extent3d {
            width: 128,
            height: 128,
            depth_or_array_layers: 6,
        },
        gpu::TextureDimension::D2,
        gpu::TextureFormat::Rgba16Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    source.texture_descriptor.mip_level_count = 8;
    source.texture_descriptor.usage = usage;
    source.texture_view_descriptor = Some(gpu::TextureViewDescriptor {
        dimension: Some(gpu::TextureViewDimension::Cube),
        ..default()
    });
    source.sampler = filtered.sampler.clone();
    let mut irradiance = ShaderBuffer::from(sh.to_vec());
    irradiance.buffer_description.usage =
        gpu::BufferUsages::STORAGE | gpu::BufferUsages::COPY_SRC | gpu::BufferUsages::COPY_DST;
    let (source, filtered, displayed) = {
        let mut assets = world.resource_mut::<Assets<Image>>();
        (
            assets.add(source),
            [assets.add(filtered.clone()), assets.add(filtered.clone())],
            assets.add(filtered),
        )
    };
    let mut buffers = world.resource_mut::<Assets<ShaderBuffer>>();
    Ok(Targets {
        source,
        filtered,
        displayed,
        sh: [buffers.add(irradiance.clone()), buffers.add(irradiance)],
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    fn seed() -> (Image, [Vec4; 9]) {
        let count: usize = (0..6)
            .map(|level| ((128_u32 >> level).pow(2) * 6 * 8) as usize)
            .sum();
        let bytes: Vec<_> = (0..count / 2)
            .flat_map(|_| 0x3800_u16.to_le_bytes())
            .collect();
        (
            crate::tidewater_environment::cube_rgba16f(128, 6, &bytes).unwrap(),
            [Vec4::new(-0.1, 0.2, 0.3, 0.0); 9],
        )
    }
    #[test]
    fn stages_distinct_compute_outputs_without_changing_acquired_lighting() {
        let mut world = World::new();
        world.init_resource::<Assets<Image>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        let (acquired, sh) = seed();
        let expected = acquired.data.clone();
        let targets = stage(&mut world, &acquired, sh).unwrap();
        assert_ne!(targets.filtered[0], targets.filtered[1]);
        assert_ne!(targets.source, targets.filtered[0]);
        assert!(targets.filtered.iter().all(|h| *h != targets.displayed));
        assert_ne!(targets.sh[0], targets.sh[1]);
        let images = world.resource::<Assets<Image>>();
        let source = images.get(&targets.source).unwrap();
        assert_eq!(source.texture_descriptor.mip_level_count, 8);
        assert!(source.data.is_none());
        for handle in targets.filtered.iter().chain([&targets.displayed]) {
            let image = images.get(handle).unwrap();
            assert_eq!(image.data, expected);
            assert_eq!(image.texture_descriptor.mip_level_count, 6);
            assert!(
                image
                    .texture_descriptor
                    .usage
                    .contains(gpu::TextureUsages::STORAGE_BINDING)
            );
        }
        let buffers = world.resource::<Assets<ShaderBuffer>>();
        for handle in &targets.sh {
            let buffer = buffers.get(handle).unwrap();
            assert_eq!(buffer.data, ShaderBuffer::from(sh.to_vec()).data);
            assert_eq!(buffer.data.as_ref().unwrap().len(), 144);
        }
    }
    #[test]
    fn invalid_seeds_allocate_nothing_and_replacement_preserves_previous_handles() {
        let mut world = World::new();
        world.init_resource::<Assets<Image>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        let (image, sh) = seed();
        let old = stage(&mut world, &image, sh).unwrap();
        let mut bad = image.clone();
        bad.data.as_mut().unwrap().pop();
        assert!(stage(&mut world, &bad, sh).is_err());
        let mut bad_sh = sh;
        bad_sh[0].x = f32::NAN;
        assert!(stage(&mut world, &image, bad_sh).is_err());
        assert_eq!(world.resource::<Assets<Image>>().len(), 4);
        assert_eq!(world.resource::<Assets<ShaderBuffer>>().len(), 2);
        let next = stage(&mut world, &image, sh).unwrap();
        assert_ne!(next.filtered[0], old.filtered[0]);
        assert!(
            world
                .resource::<Assets<Image>>()
                .get(&old.filtered[0])
                .is_some()
        );
        assert!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&old.sh[0])
                .is_some()
        );
    }
}
