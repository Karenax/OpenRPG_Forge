//! Development proof: compare the Rust sampler against exported upstream probes.
//! No renderer, authoring or parity claim is made by this executable.
use forge_core::ContentHash;
use forge_project::sampled_terrain::{
    HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource,
};
use forge_project::{HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent};
use forge_world::sampled_terrain::{SampledTerrain, SampledTerrainPlan};
use serde::Deserialize;
use std::{error::Error, fs, path::PathBuf};

#[derive(Deserialize)]
struct Metadata {
    resolution: u32,
    origin_m: f64,
    texel_m: f64,
    maps: Maps,
    probes: Vec<Probe>,
}
#[derive(Deserialize)]
struct Maps {
    heights: HeightMap,
}
#[derive(Deserialize)]
struct HeightMap {
    sha256: ContentHash,
}
#[derive(Deserialize)]
struct Probe {
    x: f64,
    z: f64,
    height: f64,
}

fn main() -> Result<(), Box<dyn Error>> {
    let root = PathBuf::from(std::env::args().nth(1).ok_or("expected export directory")?);
    let metadata: Metadata =
        serde_json::from_slice(&fs::read(root.join("terrain/metadata.json"))?)?;
    let source = SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: metadata.resolution,
        height: metadata.resolution,
        origin_xz_m: [metadata.origin_m; 2],
        sample_spacing_m: metadata.texel_m,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: metadata.maps.heights.sha256,
    };
    let bytes = fs::read(root.join("terrain/heights.bin"))?;
    let raster = SampledTerrain::from_le_bytes(source.clone(), &bytes)?;
    // Canonical Forge document, opt-in v4. Material qualification is separate.
    let mut document =
        forge_project::authoring::new_terrain("tidewater", "island", "Tidewater island")?;
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(2048.0, 2048.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 1.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    document.sampled_source = Some(source);
    let plan = SampledTerrainPlan::from_document(&document, &bytes)?;
    if metadata.probes.is_empty() {
        return Err("no upstream probes".into());
    }
    let mut max_error = 0.0_f64;
    for probe in &metadata.probes {
        let actual = raster.height_at(probe.x, probe.z)?;
        if actual.to_bits() != plan.height_at(probe.x, probe.z)?.to_bits() {
            return Err("canonical document changed source sampling".into());
        }
        let error = (actual - probe.height).abs();
        if !probe.height.is_finite() || error > 1e-9 {
            return Err(format!(
                "probe ({}, {}): source {}, Rust {actual}",
                probe.x, probe.z, probe.height
            )
            .into());
        }
        max_error = max_error.max(error);
    }
    if let Some(path) = std::env::args().nth(2) {
        use std::io::Write;
        let encoded = document.to_pretty_json()?;
        let restored = forge_project::TerrainDocument::from_json(&encoded)?;
        if restored != document {
            return Err("document round-trip differs".into());
        }
        fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(path)?
            .write_all(encoded.as_bytes())?;
    }
    println!(
        "{}",
        serde_json::json!({"status":"pass_cpu_source_sampling_only", "probes":metadata.probes.len(), "max_error_m":max_error, "source_sha256":raster.source().samples_sha256})
    );
    Ok(())
}
