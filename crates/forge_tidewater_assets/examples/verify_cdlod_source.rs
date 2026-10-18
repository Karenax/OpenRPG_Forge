//! Independent source-oracle comparison of terrain bounds and CPU LOD selection.
use forge_core::ContentHash;
use forge_project::TerrainDocument;
use forge_tidewater_assets::cdlod::{CdlodConfig, CdlodRoot, CdlodView, select_nodes};
use serde::Deserialize;
use std::{error::Error, fs};
use {
    forge_tidewater_assets::terrain_bounds::RasterLodBounds,
    forge_world::sampled_terrain::SampledTerrainPlan,
};

#[derive(Deserialize)]
struct Oracle {
    schema: String,
    version: u32,
    source_commit: String,
    heights_sha256: ContentHash,
    config: Config,
    morph_f64_bits: Vec<[String; 4]>,
    bounds_probes: Vec<BoundsProbe>,
    cases: Vec<Case>,
}
#[derive(Deserialize)]
struct Config {
    grid_size: u16,
    leaf_size_m: f64,
    levels: u8,
    range_factor: f64,
    morph_start_ratio: f64,
    max_nodes: usize,
}
#[derive(Deserialize)]
struct BoundsProbe {
    origin: [f64; 2],
    size: f64,
    bounds: [f64; 2],
}
#[derive(Deserialize)]
struct Case {
    name: String,
    position: [f64; 3],
    planes: [[f64; 4]; 6],
    nodes: Vec<[f64; 4]>,
}

#[allow(clippy::float_cmp)] // Exact oracle equality is the purpose of this executable.
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 {
        return Err("expected ORACLE_JSON TERRAIN_JSON HEIGHTS_BIN".into());
    }
    let oracle: Oracle = serde_json::from_slice(&fs::read(&args[0])?)?;
    if oracle.schema != "forge.tidewater.cdlod-oracle"
        || oracle.version != 1
        || oracle.source_commit != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || oracle.cases.is_empty()
        || oracle.bounds_probes.is_empty()
    {
        return Err("invalid or empty pinned oracle".into());
    }
    let document = TerrainDocument::from_json(&fs::read_to_string(&args[1])?)?;
    if document.sampled_source.as_ref().map(|s| &s.samples_sha256) != Some(&oracle.heights_sha256) {
        return Err("oracle and terrain source hashes differ".into());
    }
    let plan = SampledTerrainPlan::from_document(&document, &fs::read(&args[2])?)?;
    let bounds = RasterLodBounds::from_plan(&plan)?;
    let c = oracle.config;
    let config = CdlodConfig {
        grid_size: c.grid_size,
        leaf_size_m: c.leaf_size_m,
        levels: c.levels,
        range_factor: c.range_factor,
        morph_start_ratio: c.morph_start_ratio,
        max_nodes: c.max_nodes,
    };
    let morph_bits: Vec<_> = config
        .morph_rows()?
        .iter()
        .map(|row| row.map(|n| format!("{:016x}", n.to_bits())))
        .collect();
    if morph_bits != oracle.morph_f64_bits {
        return Err("morph uniform binary64 rows differ".into());
    }
    for probe in &oracle.bounds_probes {
        let actual = bounds.bounds_for(probe.origin, probe.size)?;
        if actual != probe.bounds {
            return Err(format!(
                "bounds {:?}/{}: {actual:?} != {:?}",
                probe.origin, probe.size, probe.bounds
            )
            .into());
        }
    }
    let mut total_nodes = 0;
    for case in &oracle.cases {
        let selected = select_nodes(
            config,
            CdlodRoot::Fixed {
                origin_xz: [-1024.0; 2],
                size_m: 2048.0,
            },
            CdlodView {
                position: case.position,
                planes: case.planes,
            },
            |origin, size| bounds.bounds_for(origin, size),
        )?;
        let actual: Vec<_> = selected
            .iter()
            .map(|n| [n.origin_xz[0], n.origin_xz[1], n.size_m, f64::from(n.lod)])
            .collect();
        if actual != case.nodes {
            let first = actual.iter().zip(&case.nodes).position(|(a, b)| a != b);
            return Err(format!(
                "{}: nodes {}/{}, first difference {first:?}",
                case.name,
                actual.len(),
                case.nodes.len()
            )
            .into());
        }
        total_nodes += actual.len();
    }
    println!(
        "{}",
        serde_json::json!({
            "status": "exact_cpu_oracle_match",
            "cases": oracle.cases.len(), "ordered_nodes": total_nodes,
        "bounds_probes": oracle.bounds_probes.len(), "morph_rows": oracle.morph_f64_bits.len(),
            "visual_parity": false,
        })
    );
    Ok(())
}
