//! Reopen a pinned private conversion receipt and decode its collision carriers.
use forge_assets::source_collision_geometry::read_source_collision_geometry;
use forge_assets::source_graph_glb::read_source_graph_glb;
use forge_core::{ContentHash, bytes_sha256};
use serde_json::{Value, json};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 2 {
        return Err("expected RECEIPT_JSON RECEIPT_SHA256".into());
    }
    let raw = std::fs::read(&args[0])?;
    if bytes_sha256(&raw) != ContentHash::parse(&args[1])? {
        return Err("receipt pin differs".into());
    }
    let receipt: Value = serde_json::from_slice(&raw)?;
    let entries = receipt["rows"].as_array().ok_or("receipt rows missing")?;
    if entries.is_empty() {
        return Err("empty conversion receipt".into());
    }
    let mut rows = Vec::new();
    let mut meshes = 0;
    let mut vertices = 0;
    let mut triangles = 0;
    for entry in entries {
        let path = entry["glb"].as_str().ok_or("missing GLB path")?;
        let expected = ContentHash::parse(entry["sha256"].as_str().ok_or("missing GLB pin")?)?;
        let bytes = std::fs::read(path)?;
        if bytes_sha256(&bytes) != expected {
            return Err(format!("GLB pin differs: {path}").into());
        }
        let glb = read_source_graph_glb(&bytes)?.ok_or("source graph carrier absent")?;
        let geometry = read_source_collision_geometry(&glb)?;
        let row_vertices: usize = geometry.iter().map(|g| g.positions.len()).sum();
        let row_triangles: usize = geometry
            .iter()
            .flat_map(|g| &g.triangles)
            .map(Vec::len)
            .sum();
        meshes += geometry.len();
        vertices += row_vertices;
        triangles += row_triangles;
        rows.push(json!({"glb":path,"sha256":expected,"mesh_bindings":geometry.len(),
            "vertices":row_vertices,"triangles":row_triangles,
            "identities":geometry.iter().map(|g| json!({"node_path":g.node_path,"guid":g.mesh_guid,"file_id":g.mesh_file_id})).collect::<Vec<_>>() }));
    }
    println!(
        "{}",
        serde_json::to_string_pretty(&json!({"schema":"forge.source-collision-geometry-check.v1",
        "receipt_sha256":bytes_sha256(&raw),"carriers":rows.len(),"mesh_bindings":meshes,
        "vertices":vertices,"triangles":triangles,"rows":rows,
        "decoded_integrity_verified":true,"physics_admitted":false,"studio_verified":false,"full_import":false}))?
    );
    Ok(())
}
