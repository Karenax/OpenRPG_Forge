use forge_core::{AreaId, ModuleId, bytes_sha256};
use forge_project::module_package::{ModuleFile, ModuleFileRole, ModuleVegetationBinding};
use forge_studio_frontend::module_resources_protocol::{Assembly, CHUNK_BYTES, Chunk, Snapshot};

fn snapshot(bytes: &[u8]) -> Snapshot {
    Snapshot {
        head_hash: bytes_sha256(b"head"),
        package_sha256: bytes_sha256(b"package"),
        legacy_reference: false,
        module_id: ModuleId::deterministic("module"),
        area_id: AreaId::deterministic("area"),
        vegetation: Some(ModuleVegetationBinding {
            area_id: AreaId::deterministic("area"),
            vegetation_sha256: bytes_sha256(b"vegetation"),
            sources: Vec::new(),
        }),
        objects: None,
        fish_profiles: Vec::new(),
        environment: None,
        terrain: None,
        ocean: None,
        local_lights: Vec::new(),
        placement_companions: Vec::new(),
        species: None,
        files: vec![ModuleFile {
            path: "plants/resource.bin".into(),
            sha256: bytes_sha256(bytes),
            bytes: bytes.len() as u64,
            role: ModuleFileRole::Vegetation,
        }],
    }
}

fn chunk(snapshot: &Snapshot, bytes: &[u8], offset: usize) -> Chunk {
    Chunk {
        head_hash: snapshot.head_hash,
        package_sha256: snapshot.package_sha256,
        resource_index: 0,
        resource_sha256: snapshot.files[0].sha256,
        total_bytes: bytes.len(),
        offset,
        bytes: bytes[offset..(offset + CHUNK_BYTES).min(bytes.len())].to_vec(),
    }
}

#[test]
fn module_resources_assemble_only_complete_ordered_exact_pins() {
    assert_eq!(
        Assembly::new(&snapshot(&[]), 0).unwrap().finish().unwrap(),
        Vec::<u8>::new()
    );
    let bytes = vec![27; CHUNK_BYTES + 19];
    let snapshot = snapshot(&bytes);
    let mut assembly = Assembly::new(&snapshot, 0).unwrap();
    assert!(
        assembly
            .accept(chunk(&snapshot, &bytes, CHUNK_BYTES))
            .is_err()
    );
    assembly.accept(chunk(&snapshot, &bytes, 0)).unwrap();
    assert!(assembly.accept(chunk(&snapshot, &bytes, 0)).is_err());
    assembly
        .accept(chunk(&snapshot, &bytes, CHUNK_BYTES))
        .unwrap();
    assert!(assembly.next_offset().is_none());
    assert_eq!(assembly.finish().unwrap(), bytes);
    assert!(Assembly::new(&snapshot, 0).unwrap().finish().is_err());
    assert!(Assembly::new(&snapshot, 1).is_err());
}

#[test]
fn binary_resource_wire_preserves_admission_and_rejects_invalid_envelopes() {
    let bytes = vec![255; CHUNK_BYTES];
    let snapshot = snapshot(&bytes);
    let original = chunk(&snapshot, &bytes, 0);
    let wire = original.clone().into_wire().unwrap();
    assert_eq!(&wire[wire.len() - bytes.len()..], &bytes);
    assert!(wire.len() < serde_json::to_vec(&original).unwrap().len());
    let mut assembly = Assembly::new(&snapshot, 0).unwrap();
    assembly
        .accept(Chunk::from_wire(wire.clone()).unwrap())
        .unwrap();
    assert_eq!(assembly.finish().unwrap(), bytes);
    let mut invalid_length = wire.clone();
    invalid_length[8..12].copy_from_slice(&u32::MAX.to_le_bytes());
    assert!(Chunk::from_wire(invalid_length).is_err());
    let mut invalid_magic = wire;
    invalid_magic[0] ^= 1;
    assert!(Chunk::from_wire(invalid_magic).is_err());
}

#[test]
fn module_resources_reject_cross_module_stale_short_surplus_and_tampered_chunks() {
    let bytes = vec![31; CHUNK_BYTES + 7];
    let snapshot = snapshot(&bytes);
    for case in 0..8 {
        let mut assembly = Assembly::new(&snapshot, 0).unwrap();
        let mut invalid = chunk(&snapshot, &bytes, 0);
        match case {
            0 => invalid.head_hash = bytes_sha256(b"other head"),
            1 => invalid.package_sha256 = bytes_sha256(b"other module"),
            2 => invalid.resource_index = 1,
            3 => invalid.resource_sha256 = bytes_sha256(b"other resource"),
            4 => invalid.total_bytes += 1,
            5 => invalid.offset = 1,
            6 => {
                invalid.bytes.pop();
            }
            _ => invalid.bytes.push(0),
        }
        assert!(
            assembly.accept(invalid).is_err(),
            "accepted invalid chunk {case}"
        );
        assert_eq!(assembly.next_offset(), Some(0));
    }
    let mut assembly = Assembly::new(&snapshot, 0).unwrap();
    let mut corrupt = chunk(&snapshot, &bytes, 0);
    corrupt.bytes[0] ^= 1;
    assembly.accept(corrupt).unwrap();
    assembly
        .accept(chunk(&snapshot, &bytes, CHUNK_BYTES))
        .unwrap();
    assert!(assembly.finish().is_err());
}

#[cfg(not(target_arch = "wasm32"))]
#[path = "module_vegetation_resources/native.rs"]
mod native;
