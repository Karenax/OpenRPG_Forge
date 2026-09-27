//! Compare camera-following ocean selection, patch topology and morph uniforms.
use forge_world::cdlod::{CdlodConfig, CdlodRoot, CdlodView, select_nodes};
use serde::Deserialize;
use std::{collections::BTreeMap, error::Error, fs, path::PathBuf};
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
struct Case {
    name: String,
    position: [f64; 3],
    planes: [[f64; 4]; 6],
    nodes: Vec<[f64; 4]>,
}
#[derive(Deserialize)]
struct Oracle {
    schema: String,
    version: u32,
    source_commit: String,
    config: Config,
    bounds: [f64; 2],
    morph_f64_bits: Vec<[String; 4]>,
    cases: Vec<Case>,
    files: BTreeMap<String, String>,
}
#[allow(clippy::float_cmp)] // Exact source oracle comparison.
fn main() -> Result<(), Box<dyn Error>> {
    let root = PathBuf::from(
        std::env::args_os()
            .nth(1)
            .ok_or("OCEAN_LOD_EXPORT required")?,
    );
    let oracle: Oracle = serde_json::from_slice(&fs::read(root.join("ocean-lod.json"))?)?;
    if oracle.schema != "forge.tidewater.ocean-lod"
        || oracle.version != 1
        || oracle.source_commit != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || oracle.cases.is_empty()
    {
        return Err("invalid ocean LOD source".into());
    }
    let c = oracle.config;
    let config = CdlodConfig {
        grid_size: c.grid_size,
        leaf_size_m: c.leaf_size_m,
        levels: c.levels,
        range_factor: c.range_factor,
        morph_start_ratio: c.morph_start_ratio,
        max_nodes: c.max_nodes,
    };
    let checked = |name: &str| -> Result<Vec<u8>, Box<dyn Error>> {
        let bytes = fs::read(root.join(name))?;
        if forge_core::bytes_sha256(&bytes).to_string() != oracle.files[name] {
            return Err("source hash mismatch".into());
        }
        Ok(bytes)
    };
    let grid = config.grid()?;
    let positions: Vec<u8> = grid
        .positions
        .iter()
        .flatten()
        .flat_map(|v| v.to_le_bytes())
        .collect();
    let indices: Vec<u8> = grid.indices.iter().flat_map(|v| v.to_le_bytes()).collect();
    assert_eq!(positions, checked("grid.f32.bin")?, "patch positions");
    assert_eq!(
        indices,
        checked("indices.u32.bin")?,
        "alternating triangle indices"
    );
    let morph = config.morph_rows()?;
    let bits: Vec<_> = morph
        .iter()
        .map(|r| r.map(|v| format!("{:016x}", v.to_bits())))
        .collect();
    assert_eq!(bits, oracle.morph_f64_bits, "morph binary64");
    #[allow(clippy::cast_possible_truncation)]
    let packed: Vec<u8> = morph
        .iter()
        .flatten()
        .flat_map(|v| (*v as f32).to_le_bytes())
        .collect();
    assert_eq!(packed, checked("morph.bin")?, "morph GPU layout");
    let mut total = 0;
    for case in &oracle.cases {
        let nodes = select_nodes(
            config,
            CdlodRoot::CameraFollowing,
            CdlodView {
                position: case.position,
                planes: case.planes,
            },
            |_, _| Ok(oracle.bounds),
        )?;
        let actual: Vec<_> = nodes
            .iter()
            .map(|n| [n.origin_xz[0], n.origin_xz[1], n.size_m, f64::from(n.lod)])
            .collect();
        assert_eq!(actual, case.nodes, "{}", case.name);
        total += nodes.len();
    }
    println!(
        "{}",
        serde_json::json!({"status":"exact_cpu_ocean_lod_match","cases":oracle.cases.len(),"ordered_nodes":total,"grid_vertices":grid.positions.len(),"grid_indices":grid.indices.len(),"morph_rows":morph.len(),"visual_parity":false})
    );
    Ok(())
}
