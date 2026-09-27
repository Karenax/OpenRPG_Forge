//! Prepare every building from the portable package without scene fixtures.
use forge_assets::tidewater_package::BuildingPackage;
use forge_core::ContentHash;
use forge_engine::tidewater_assets::{
    GeometrySpace, PreparedLibrary, PreparedMeshes, ResidentMeshes, meshes,
};
use std::{error::Error, fs, io::Read, path::PathBuf};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 2 {
        return Err("PACKAGE_ROOT EXPECTED_SHA256 required".into());
    }
    let root = PathBuf::from(&args[0]).canonicalize()?;
    let expected: ContentHash = args[1].parse()?;
    let mut manifest = Vec::new();
    fs::File::open(root.join("package.json"))?
        .take(2 * 1024 * 1024 + 1)
        .read_to_end(&mut manifest)?;
    let package = BuildingPackage::load(&manifest, expected, |name, limit| {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if path.parent() != Some(root.as_path()) {
            return Err("Escaping package payload".into());
        }
        let mut bytes = Vec::new();
        fs::File::open(path)
            .map_err(|e| e.to_string())?
            .take(limit as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        Ok(bytes)
    })?;
    let library = PreparedLibrary::load(&package)?;
    let mut parts = 0;
    let mut vertices = 0;
    for record in &package.catalog().assets {
        let local = meshes(
            &package,
            record.id,
            record.content_hash,
            GeometrySpace::Local,
        )?;
        let reference = meshes(
            &package,
            record.id,
            record.content_hash,
            GeometrySpace::SourceReference,
        )?;
        assert_eq!(local.len(), reference.len());
        for (a, b) in local.iter().zip(&reference) {
            assert_eq!(a.mesh.count_vertices(), b.mesh.count_vertices());
            assert_eq!(a.mesh.indices(), b.mesh.indices());
            assert_eq!(a.material, b.material);
            assert!(library.materials.contains_key(&a.material));
            vertices += a.mesh.count_vertices();
            parts += 1;
        }
    }
    assert_eq!((parts, vertices), (111, 112_569));
    let mut mesh_assets = bevy::prelude::Assets::default();
    let prepared = PreparedMeshes::load(&package)?;
    assert_eq!(mesh_assets.len(), 0);
    let resident = prepared.stage(&mut mesh_assets);
    assert_eq!(mesh_assets.len(), 111);
    for record in &package.catalog().assets {
        let first = resident.resolve(record.id, record.content_hash)?;
        let duplicate = first.to_vec();
        for (a, b) in first.iter().zip(&duplicate) {
            assert_eq!(a.mesh.id(), b.mesh.id());
            assert!(mesh_assets.get(&a.mesh).is_some());
        }
        assert!(
            resident
                .resolve(record.id, forge_core::bytes_sha256(b"wrong revision"))
                .is_err()
        );
    }
    assert_eq!(mesh_assets.len(), 111);
    verify_entities(&package, &resident)?;
    let mut images = bevy::prelude::Assets::default();
    let library = library.stage(&mut images);
    assert_eq!(images.len(), 14);
    for handle in library.textures.values() {
        assert!(images.get(handle).is_some());
    }
    println!(
        "Prepared {} buildings, {parts} parts, {vertices} vertices, {} materials and {} mipmapped textures",
        package.catalog().assets.len(),
        library.materials.len(),
        library.textures.len()
    );
    Ok(())
}

fn verify_entities(package: &BuildingPackage, geometry: &ResidentMeshes) -> Result<(), String> {
    use bevy::prelude::*;
    use forge_engine::{
        ManualPlacementPart, ManualPlacementProjection, tidewater_assets::placements,
    };
    use forge_project::zone_authoring::placements::ManualPlacement;
    let record = &package.catalog().assets[0];
    let mut placement = ManualPlacement {
        id: forge_core::EntityId::deterministic("package placement proof"),
        name: "Building".into(),
        asset_id: record.id,
        asset_hash: record.content_hash,
        transform: forge_core::Transform::IDENTITY,
        locked: false,
    };
    let mut world = World::new();
    let initial_entities = world.entities().len();
    let mut materials = std::collections::BTreeMap::new();
    assert!(placements::spawn(&mut world.commands(), &placement, geometry, &materials).is_err());
    world.flush();
    assert_eq!(world.entities().len(), initial_entities);
    // This verifies entity structure only; GPU material readiness is tested by rendering.
    for name in package.materials()["materials"]
        .as_object()
        .ok_or("materials")?
        .keys()
    {
        materials.insert(name.clone(), Handle::default());
    }
    let first = placements::spawn(&mut world.commands(), &placement, geometry, &materials)?;
    placement.id = forge_core::EntityId::deterministic("package duplicate proof");
    placement.transform.translation_m.x = 12.0;
    let second = placements::spawn(&mut world.commands(), &placement, geometry, &materials)?;
    world.flush();
    assert_ne!(first.root, second.root);
    assert_eq!(
        world
            .get::<ManualPlacementProjection>(second.root)
            .ok_or("root")?
            .0,
        placement
    );
    assert_eq!(
        world
            .get::<Transform>(second.root)
            .ok_or("transform")?
            .translation
            .x
            .to_bits(),
        12.0_f32.to_bits()
    );
    for (&a, &b) in first.parts.iter().zip(&second.parts) {
        assert_eq!(
            world.get::<Mesh3d>(a).ok_or("mesh")?.0,
            world.get::<Mesh3d>(b).ok_or("mesh")?.0
        );
        assert_eq!(
            world.get::<ManualPlacementPart>(b).ok_or("identity")?.0,
            placement.id
        );
        assert_eq!(
            world.get::<ChildOf>(b).ok_or("parent")?.parent(),
            second.root
        );
    }
    world.entity_mut(first.root).despawn();
    assert!(first.parts.iter().all(|&e| world.get_entity(e).is_err()));
    assert!(second.parts.iter().all(|&e| world.get_entity(e).is_ok()));
    Ok(())
}
