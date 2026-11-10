//! Inspect the renderer-ready preparation records in an admitted decal GLB.
//! This is a diagnostic probe, not a Studio/runtime qualification.
use forge_assets::source_decal_glb::read_source_decal_glb;
use forge_engine::source_decal_prepare::{prepare_source_decal_draws, prepare_source_decals};
use std::io::Read;

fn check_shader(label: &str, text: &str, render_pass: bool) -> Result<(), String> {
    let module = naga::front::wgsl::parse_str(text)
        .map_err(|error| format!("{label}: {}", error.emit_to_string(text)))?;
    naga::valid::Validator::new(
        naga::valid::ValidationFlags::all(),
        naga::valid::Capabilities::empty(),
    )
    .validate(&module)
    .map_err(|error| format!("{label}: {error}"))?;
    let entries: Vec<_> = module.entry_points.iter().map(|ep| ep.stage).collect();
    let valid = if render_pass {
        entries.len() == 2
            && entries.contains(&naga::ShaderStage::Vertex)
            && entries.contains(&naga::ShaderStage::Fragment)
    } else {
        entries.is_empty()
    };
    if !valid {
        return Err(format!(
            "{label}: unexpected shader entry points: {entries:?}"
        ));
    }
    Ok(())
}

fn inspect(path: &std::path::Path) -> Result<usize, Box<dyn std::error::Error>> {
    let input = std::fs::File::open(path)?;
    const MAX_BYTES: u64 = 512 * 1024 * 1024;
    let mut bytes = Vec::new();
    input.take(MAX_BYTES + 1).read_to_end(&mut bytes)?;
    let carrier = read_source_decal_glb(&bytes)?.ok_or("GLB has no source decal extension")?;
    let prepared = prepare_source_decals(&carrier)?;
    let draws = prepare_source_decal_draws(&carrier)?;
    for decal in &prepared {
        check_shader("surface", &decal.programs.surface, false)?;
        check_shader("generation", &decal.programs.dbuffer_generation, true)?;
        check_shader("color composition", &decal.programs.color_composition, true)?;
        check_shader(
            "normal composition",
            &decal.programs.normal_composition,
            true,
        )?;
        let draw = draws
            .iter()
            .find(|draw| draw.material_index == decal.material_index)
            .ok_or("Prepared decal material has no draw")?;
        println!(
            "path={} material={} format={:?} texture={}x{} mips={} storage_bytes={} validated_programs=4 vertices={} triangles={} normals={} uv={} node_determinant={}",
            path.display(),
            decal.material_index,
            decal.texture.format,
            decal.texture.width,
            decal.texture.height,
            decal.texture.mips.len(),
            decal.texture.storage.len(),
            draw.positions.len(),
            draw.indices.len() / 3,
            draw.normals.len(),
            draw.uv.len(),
            draw.root_from_mesh.determinant(),
        );
    }
    Ok(prepared.len())
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let paths: Vec<_> = std::env::args_os().skip(1).collect();
    if paths.is_empty() {
        return Err("usage: source_decal_prepare_probe <source-decal.glb>...".into());
    }
    let mut prepared = 0;
    for path in &paths {
        prepared += inspect(std::path::Path::new(path))?;
    }
    println!("carriers={} prepared_source_decals={prepared}", paths.len());
    Ok(())
}
