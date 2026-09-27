//! Acquisition adapter and authored height mip preparation for the scene probe.
use bevy::{
    asset::RenderAssetUsages,
    prelude::*,
    render::render_resource::{Extent3d, TextureDimension, TextureFormat, TextureUsages},
};
use forge_engine::tidewater_hill_shadow::Settings;
pub use forge_engine::tidewater_hill_shadow::{Images, Ready};
use std::{collections::BTreeMap, error::Error};
pub type Input = forge_engine::tidewater_hill_shadow::PreparedInput;

pub fn load(files: &BTreeMap<String, Vec<u8>>) -> Result<Option<Input>, Box<dyn Error>> {
    if !files.contains_key("hill-shadow.wgsl") {
        return Ok(None);
    }
    let meta: serde_json::Value =
        serde_json::from_slice(files.get("shadow.json").ok_or("missing shadow metadata")?)?;
    let sun = Vec3::from_array(serde_json::from_value::<[f32; 3]>(
        meta["sun_direction"].clone(),
    )?);
    if !sun.is_finite() || (sun.length_squared() - 1.0).abs() > 0.001 {
        return Err("invalid shadow light direction".into());
    }
    let raw = files
        .get("heights.bin")
        .ok_or("missing shadow source heights")?;
    if raw.len() != 2048 * 2048 * 4
        || meta["heights_sha256"] != forge_core::bytes_sha256(raw).to_string()
    {
        return Err("shadow heights differ".into());
    }
    let heights: Vec<_> = raw
        .as_chunks::<4>()
        .0
        .iter()
        .map(|v| f32::from_le_bytes(*v))
        .collect();
    if heights.iter().any(|v| !v.is_finite()) {
        return Err("nonfinite shadow input".into());
    }
    let mut packed = forge_engine::tidewater_hill_shadow::mip_chain(&heights)?;
    if meta["coarse_sha256"] != forge_core::bytes_sha256(&packed).to_string() {
        return Err("native shadow mip chain differs from source".into());
    }
    info!(sha256 = %forge_core::bytes_sha256(&packed), "Native shadow mip chain equals source");
    let document = forge_project::TerrainDocument::from_json(std::str::from_utf8(
        files
            .get("terrain.json")
            .ok_or("missing shadow terrain document")?,
    )?)?;
    if !document.height_deltas.is_empty() || document.height_sampling.base_elevation_m != 0.0 {
        let plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(&document, raw)?;
        packed = forge_engine::tidewater_hill_shadow::mip_chain(&plan.cell_center_heights())?;
        eprintln!(
            "authored hill shadow: document={}, coarse={}",
            plan.document_hash(),
            forge_core::bytes_sha256(&packed)
        );
    }
    Ok(Some(Input::new(
        std::str::from_utf8(files.get("hill-shadow.wgsl").ok_or("missing hill shader")?)?
            .to_owned(),
        packed,
        Settings {
            origin: -1024.0,
            size: 2048.0,
            sun_bake: sun,
        },
    )?))
}

pub fn setup(mut commands: Commands, probe: Res<super::Probe>, mut images: ResMut<Assets<Image>>) {
    if let Some(input) = &probe.hill_heights {
        let input = input.clone();
        commands.queue(move |world: &mut World| {
            input
                .stage(world)
                .expect("validated shadow inputs and registered renderer");
        });
        return;
    }
    let (size, mips, bytes) = (1, 1, vec![0, 0]);
    commands.insert_resource(Settings {
        origin: -1024.0,
        size: 2048.0,
        sun_bake: Vec3::Y,
    });
    let mut input = Image::new_fill(
        Extent3d {
            width: size,
            height: size,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        &[0, 0],
        TextureFormat::R16Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    input.data = Some(bytes);
    input.texture_descriptor.mip_level_count = mips;
    input.sampler = bevy::image::ImageSampler::linear();
    let mut output = Image::new_fill(
        Extent3d {
            width: size,
            height: size,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        &[0; 8],
        TextureFormat::Rgba16Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    output.texture_descriptor.usage |= TextureUsages::STORAGE_BINDING | TextureUsages::COPY_SRC;
    commands.insert_resource(Images {
        input: images.add(input),
        output: images.add(output),
        ready: std::sync::Arc::new(std::sync::atomic::AtomicBool::new(false)),
    });
}
