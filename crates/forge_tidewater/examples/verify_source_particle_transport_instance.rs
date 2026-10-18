//! Instantiate governed particle components without claiming GPU/frame parity.
use bevy::{prelude::*, render::storage::ShaderBuffer};
use forge_assets::source_particle_transport::SourceParticleTransport;
use forge_core::{ContentHash, bytes_sha256};
use forge_engine::{
    source_graph::SourceGraphMaterial, source_particle_transport::PreparedSourceParticleOwner,
};
use std::{
    error::Error,
    fs::File,
    io::{Read, Write},
    path::Path,
};
fn bounded(path: &Path, limit: usize) -> Result<Vec<u8>, Box<dyn Error>> {
    let mut bytes = Vec::new();
    File::open(path)?
        .take(u64::try_from(limit)? + 1)
        .read_to_end(&mut bytes)?;
    if bytes.len() > limit {
        return Err("input budget exceeded".into());
    }
    Ok(bytes)
}
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 4 {
        return Err(
            "Usage: verify_source_particle_transport_instance ROOT SHA CARRIER FRESH_REPORT".into(),
        );
    }
    let root = Path::new(&args[0]).canonicalize()?;
    let pin: ContentHash = args[1].parse()?;
    let wire = bounded(&root.join("manifest.json"), 1024 * 1024)?;
    let carrier = bounded(Path::new(&args[2]), 512 * 1024 * 1024)?;
    let resolved = SourceParticleTransport::decode_for_carrier(&wire, pin, &carrier, |name| {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if !path.starts_with(&root) {
            return Err("resource outside package root".into());
        }
        bounded(&path, 64 * 1024 * 1024).map_err(|e| e.to_string())
    })?;
    let mut shaders = Assets::<Shader>::default();
    let mut images = Assets::<Image>::default();
    let mut buffers = Assets::<ShaderBuffer>::default();
    let mut world = World::new();
    let mut rows = Vec::new();
    for (i, decl) in resolved.contract.owners.iter().enumerate() {
        let entity = world.spawn_empty().id();
        let prepared = PreparedSourceParticleOwner::prepare(decl, &resolved.resources)?;
        let slot = u64::try_from(i)?.checked_mul(2).ok_or("slot overflow")?;
        let instance = prepared.instantiate(
            &mut shaders,
            &mut images,
            &mut buffers,
            entity,
            Handle::<SourceGraphMaterial>::default(),
            slot,
            slot + 1,
        )?;
        let source_guid = instance.source_component_guid.clone();
        let file_id = instance.source_component_file_id.clone();
        world.entity_mut(entity).insert(instance.owner);
        world.spawn((instance.projection, ChildOf(entity)));
        let lengths: Vec<_> = instance
            .buffers
            .iter()
            .map(|h| buffers.get(h).unwrap().data.as_ref().unwrap().len())
            .collect();
        rows.push(
            serde_json::json!({"guid":source_guid,"file_id":file_id,"buffer_bytes":lengths,
            "owner_component_attached":true,"projection_child_attached":true}),
        );
    }
    let output = serde_json::json!({"schema":"forge.source-particle.transport-instance","version":1,
        "manifest_sha256":pin,"carrier_sha256":bytes_sha256(&carrier),"owners":rows,
        "shaders":shaders.len(),"images":images.len(),"buffers":buffers.len(),
        "renderer_GPU_executed":false,"source_material_registered":false,"Studio_integrated":false});
    File::create_new(&args[3])?.write_all(&serde_json::to_vec_pretty(&output)?)?;
    println!("{}", serde_json::to_string(&output)?);
    Ok(())
}
