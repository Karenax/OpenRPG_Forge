//! Reopen a pinned source carrier and all declared geometry/material payloads.
//! Does not register shaders/images or qualify a rendered frame.
use forge_assets::{
    source_graph_attributes::read_source_graph_primitive_geometry,
    source_graph_glb::read_source_graph_glb,
};
use forge_core::{ContentHash, bytes_sha256};
use std::{
    error::Error,
    fs::File,
    io::{Read, Write},
    path::Path,
};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 {
        return Err("Usage: verify_source_graph_carrier GLB SHA256 FRESH_REPORT".into());
    }
    let expected: ContentHash = args[1].parse()?;
    let mut raw = Vec::new();
    File::open(Path::new(&args[0]))?
        .take(512 * 1024 * 1024 + 1)
        .read_to_end(&mut raw)?;
    if raw.len() > 512 * 1024 * 1024 || bytes_sha256(&raw) != expected {
        return Err("carrier budget or SHA differs".into());
    }
    let glb = read_source_graph_glb(&raw)?.ok_or("source graph declaration absent")?;
    let mut geometry = Vec::new();
    for (mesh, m) in glb.document()["meshes"]
        .as_array()
        .ok_or("meshes missing")?
        .iter()
        .enumerate()
    {
        for primitive in 0..m["primitives"]
            .as_array()
            .ok_or("primitives missing")?
            .len()
        {
            let decoded = read_source_graph_primitive_geometry(&glb, mesh, primitive)?;
            geometry.push(serde_json::json!({"mesh":mesh,"primitive":primitive,
                "vertices":decoded.positions.len(),"triangles":decoded.indices.len()/3,
                "material":decoded.source.material_index}));
        }
    }
    let materials: Vec<_>=glb.materials.iter().map(|(index,c)|{
        let scalars=c.read_scalar_parameters(&glb.views).map_err(|e|format!("{e:?}"))?;
        Ok::<_,String>(serde_json::json!({"material":index,"schema_version":c.schema_version,
            "authored_textures":c.textures.len(),"scene_textures":c.scene_textures,
            "total_texture_bindings":c.texture_binding_count(),
            "native_mips":c.textures.iter().filter_map(|t|t.imported_storage.as_ref()).map(|t|t.mips.len()).sum::<usize>(),
            "uniform_bytes":scalars.as_ref().map(|s|s.little_endian_uniform_bytes().len()),
            "binary_word_lanes":c.scalar_parameters.as_ref().map(|s|&s.word_lanes)}))
    }).collect::<Result<_,_>>()?;
    let output = serde_json::json!({"schema":"forge.source-graph.carrier-reopen","version":1,
        "carrier_sha256":expected,"geometry":geometry,"materials":materials,
        "renderer_registration_executed":false,"GPU_executed":false,"Studio_qualified":false});
    File::create_new(&args[2])?.write_all(&serde_json::to_vec_pretty(&output)?)?;
    println!("{}", serde_json::to_string(&output)?);
    Ok(())
}
