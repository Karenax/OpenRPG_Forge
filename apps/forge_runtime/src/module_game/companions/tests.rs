use super::*;
use crate::module_game::{installed_fixture, qualification_fixture::source_graph_fixture};
use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, bytes_sha256};
use forge_project::{
    module_package::ModuleFilePin,
    zone_authoring::placements::{
        ParticlePackageReference, PhysicsPackageReference, PlacementEdit,
    },
};
use std::cell::Cell;

struct Fixture {
    area: AreaId,
    placement: ManualPlacement,
    bindings: Vec<ModulePlacementCompanion>,
    files: Vec<ModuleFile>,
    bytes: BTreeMap<String, Vec<u8>>,
}

fn fixture() -> Fixture {
    fixture_for(bytes_sha256(b"exact model"))
}

fn fixture_for(carrier: ContentHash) -> Fixture {
    let area = AreaId::deterministic("runtime/companion/area");
    let asset_id = AssetId::deterministic("runtime/companion/model");
    let manifest = b"{}".to_vec();
    let manifest_sha256 = bytes_sha256(&manifest);
    let physics = forge_assets::source_physics_package::SourcePhysicsPackage {
        carrier_sha256: carrier,
        manifest_sha256,
        manifest: manifest.clone(),
        candidates: BTreeMap::new(),
    }
    .encode()
    .unwrap();
    let particles = forge_assets::source_particle_package::SourceParticlePackage {
        carrier_sha256: carrier,
        manifest_sha256,
        manifest,
        resources: BTreeMap::new(),
    }
    .encode()
    .unwrap();
    let placement = ManualPlacement {
        id: EntityId::deterministic("runtime/companion/placement"),
        name: "Source fixture".into(),
        asset_id,
        asset_hash: carrier,
        physics: Some(Box::new(PhysicsPackageReference {
            schema_version: 1,
            carrier_sha256: carrier,
            manifest_sha256,
            package_sha256: bytes_sha256(&physics),
        })),
        particles: Some(Box::new(ParticlePackageReference {
            schema_version: 1,
            carrier_sha256: carrier,
            manifest_sha256,
            package_sha256: bytes_sha256(&particles),
        })),
        local_lights: None,
        transform: Transform::IDENTITY,
        locked: false,
    };
    let mut bindings = Vec::new();
    let mut files = Vec::new();
    let mut bytes = BTreeMap::new();
    for (kind, path, wire) in [
        (
            ModulePlacementCompanionKind::Physics,
            "companions/a.orpp",
            physics,
        ),
        (
            ModulePlacementCompanionKind::Particles,
            "companions/b.orpe",
            particles,
        ),
    ] {
        let hash = bytes_sha256(&wire);
        bindings.push(ModulePlacementCompanion {
            areas: vec![area],
            kind,
            asset_id,
            carrier_sha256: carrier,
            manifest_sha256,
            package: ModuleFilePin {
                path: path.into(),
                sha256: hash,
            },
            proof: ModuleFilePin {
                path: format!("{path}.proof"),
                sha256: bytes_sha256(b"proof"),
            },
        });
        files.push(ModuleFile {
            path: path.into(),
            bytes: u64::try_from(wire.len()).unwrap(),
            sha256: hash,
            role: ModuleFileRole::PlacementCompanion,
        });
        bytes.insert(path.into(), wire);
    }
    Fixture {
        area,
        placement,
        bindings,
        files,
        bytes,
    }
}

fn read_fixture(fixture: &Fixture, cache: &mut Cache) -> Result<Prepared, String> {
    prepare_with(
        fixture.area,
        &fixture.bindings,
        &fixture.files,
        &fixture.placement,
        cache,
        |path, _| {
            fixture
                .bytes
                .get(path)
                .cloned()
                .ok_or_else(|| "Payload absent".into())
        },
    )
}

fn attach_synthetic_files(loaded: &mut LoadedModuleZone, f: &mut Fixture) {
    for binding in &mut f.bindings {
        binding.areas = vec![loaded.zone.head().base.terrain.area_id];
        let proof = b"proof";
        std::fs::create_dir_all(loaded.package_root.join("companions")).unwrap();
        std::fs::write(loaded.package_root.join(&binding.proof.path), proof).unwrap();
        loaded.manifest.files.push(ModuleFile {
            path: binding.proof.path.clone(),
            bytes: u64::try_from(proof.len()).unwrap(),
            sha256: bytes_sha256(proof),
            role: ModuleFileRole::Provenance,
        });
        loaded
            .manifest
            .provenance
            .proof_paths
            .push(binding.proof.path.clone());
    }
    for (path, bytes) in &f.bytes {
        std::fs::write(loaded.package_root.join(path), bytes).unwrap();
    }
    // Supply synthetic bindings after loading solely to exercise the installed
    // reader; full archive admission is tested independently by delivery.
    loaded.manifest.placement_companions = f.bindings.clone();
    loaded.manifest.files.extend(f.files.clone());
    loaded.manifest.compatibility.module_api_version =
        forge_project::module_package::MODULE_PLACEMENT_COMPANIONS_API_VERSION;
}

#[test]
fn exact_saved_packets_are_shared_for_repeated_placements() {
    let f = fixture();
    let reads = Cell::new(0);
    let mut cache = Cache::default();
    let mut read = |path: &str, _: usize| {
        reads.set(reads.get() + 1);
        f.bytes
            .get(path)
            .cloned()
            .ok_or_else(|| "Payload absent".to_owned())
    };
    let first = prepare_with(
        f.area,
        &f.bindings,
        &f.files,
        &f.placement,
        &mut cache,
        &mut read,
    )
    .unwrap();
    let mut copied = f.placement.clone();
    copied.id = EntityId::deterministic("runtime/companion/copied placement");
    let second = prepare_with(
        f.area,
        &f.bindings,
        &f.files,
        &copied,
        &mut cache,
        &mut read,
    )
    .unwrap();
    assert_eq!(reads.get(), 2);
    assert!(Arc::ptr_eq(
        &first.physics.unwrap().wire,
        &second.physics.unwrap().wire
    ));
    assert!(Arc::ptr_eq(
        &first.particles.unwrap().wire,
        &second.particles.unwrap().wire
    ));
    assert_eq!(cache.total, f.bytes.values().map(Vec::len).sum::<usize>());
}

#[test]
fn absence_ambiguity_wrong_area_and_model_pins_are_refused() {
    for case in 0..7 {
        let mut f = fixture();
        match case {
            0 => {
                f.bindings.remove(0);
            }
            1 => f.bindings.push(f.bindings[0].clone()),
            2 => f.bindings[0].areas = vec![AreaId::deterministic("different area")],
            3 => f.placement.physics.as_mut().unwrap().schema_version = 2,
            4 => {
                f.placement.physics.as_mut().unwrap().carrier_sha256 = bytes_sha256(b"other model");
            }
            5 => f.placement.asset_id = AssetId::deterministic("other identity"),
            _ => {
                f.placement.physics.as_mut().unwrap().package_sha256 =
                    bytes_sha256(b"other revision");
            }
        }
        let reads = Cell::new(0);
        let result = prepare_with(
            f.area,
            &f.bindings,
            &f.files,
            &f.placement,
            &mut Cache::default(),
            |_, _| {
                reads.set(reads.get() + 1);
                Err("Unexpected read".into())
            },
        );
        assert!(result.is_err(), "case {case}");
        assert_eq!(reads.get(), 0, "case {case}");
    }
}

#[test]
fn inventory_corruption_or_missing_bytes_are_never_replaced() {
    for case in 0..6 {
        let mut f = fixture();
        let path = f.bindings[0].package.path.clone();
        match case {
            0 => {
                f.files.remove(0);
            }
            1 => f.files[0].role = ModuleFileRole::Asset,
            2 => f.files.push(f.files[0].clone()),
            3 => f.bytes.get_mut(&path).unwrap()[0] ^= 1,
            4 => {
                f.bytes.remove(&path);
            }
            _ => f.files[0].bytes += 1,
        }
        assert!(
            read_fixture(&f, &mut Cache::default()).is_err(),
            "case {case}"
        );
    }
    let mut f = fixture();
    let mut cache = Cache::default();
    assert!(read_fixture(&f, &mut cache).is_ok());
    f.files[0].bytes += 1;
    assert!(
        read_fixture(&f, &mut cache).is_err(),
        "cached bytes must still match the inventory"
    );
}

#[test]
fn no_references_read_nothing_and_the_shared_budget_is_checked_before_io() {
    let mut f = fixture();
    let mut empty = f.placement.clone();
    empty.physics = None;
    empty.particles = None;
    let reads = Cell::new(0);
    let result = prepare_with(
        f.area,
        &f.bindings,
        &f.files,
        &empty,
        &mut Cache::default(),
        |_, _| {
            reads.set(reads.get() + 1);
            Err("Unexpected read".into())
        },
    )
    .unwrap();
    assert!(result.physics.is_none() && result.particles.is_none());
    assert_eq!(reads.get(), 0);
    f.placement.particles = None;
    let mut cache = Cache {
        total: forge_project::workspace::export::MAX_MODULE_EXPORT_SOURCE_BYTES,
        ..Cache::default()
    };
    assert!(
        prepare_with(
            f.area,
            &f.bindings,
            &f.files,
            &f.placement,
            &mut cache,
            |_, _| {
                reads.set(reads.get() + 1);
                Err("Unexpected read".into())
            }
        )
        .is_err()
    );
    assert_eq!(reads.get(), 0);
}

#[test]
fn production_reader_uses_the_installed_file_and_refuses_later_corruption() {
    let install = installed_fixture::Fixture::new();
    let root = install.module_root(install.first.id);
    let mut loaded = forge_delivery::module_runtime::load(&root, install.first.area, None).unwrap();
    let mut f = fixture();
    attach_synthetic_files(&mut loaded, &mut f);
    let first = prepare(&loaded, &f.placement, &mut Cache::default()).unwrap();
    assert_eq!(
        &*first.physics.unwrap().wire,
        f.bytes["companions/a.orpp"].as_slice()
    );
    let path = root.join("companions/a.orpp");
    let mut damaged = std::fs::read(&path).unwrap();
    damaged[0] ^= 1;
    std::fs::write(path, damaged).unwrap();
    assert!(prepare(&loaded, &f.placement, &mut Cache::default()).is_err());
}

fn replace_fixture_file(loaded: &mut LoadedModuleZone, path: &str, bytes: &[u8]) {
    std::fs::write(loaded.package_root.join(path), bytes).unwrap();
    let file = loaded
        .manifest
        .files
        .iter_mut()
        .find(|file| file.path == path)
        .unwrap();
    file.sha256 = bytes_sha256(bytes);
    file.bytes = u64::try_from(bytes.len()).unwrap();
}

#[test]
fn manual_preparation_keeps_packets_on_the_source_route_and_refuses_corruption() {
    let install = installed_fixture::Fixture::new();
    let root = install.module_root(install.first.id);
    let mut loaded = forge_delivery::module_runtime::load(&root, install.first.area, None).unwrap();
    let model = source_graph_fixture::fixture().unwrap();
    let mut f = fixture_for(bytes_sha256(&model));
    attach_synthetic_files(&mut loaded, &mut f);
    let mut catalog: serde_json::Value =
        serde_json::from_slice(&std::fs::read(root.join("assets/catalog.json")).unwrap()).unwrap();
    catalog["assets"][0]["id"] = serde_json::to_value(f.placement.asset_id).unwrap();
    catalog["assets"][0]["payload"]["sha256"] =
        serde_json::to_value(f.placement.asset_hash).unwrap();
    catalog["assets"][0]["payload"]["bytes"] = serde_json::to_value(model.len()).unwrap();
    replace_fixture_file(&mut loaded, "assets/model.glb", &model);
    replace_fixture_file(
        &mut loaded,
        "assets/catalog.json",
        &serde_json::to_vec(&catalog).unwrap(),
    );
    loaded.manifest.assets[0].asset_id = f.placement.asset_id;
    loaded.manifest.assets[0].sha256 = f.placement.asset_hash;
    loaded.manifest.validate().unwrap();
    let command = loaded
        .zone
        .head()
        .prepare_placement(
            CommandId::deterministic("runtime/source-graph"),
            PlacementEdit::Insert {
                placement: f.placement.clone(),
            },
        )
        .unwrap();
    loaded.zone.execute(&command).unwrap();
    let diagnostic = super::super::manual::prepare(&loaded, true).unwrap();
    let normal = super::super::manual::prepare(&loaded, false).unwrap();
    assert!(
        normal.has_source_graphs(),
        "ordinary glTF cannot replace the source host"
    );
    assert_eq!(normal.accepted(), diagnostic.accepted());
    let path = root.join("companions/a.orpp");
    let mut wire = std::fs::read(&path).unwrap();
    wire[0] ^= 1;
    std::fs::write(path, wire).unwrap();
    assert!(super::super::manual::prepare(&loaded, true).is_err());
    assert!(super::super::manual::prepare(&loaded, false).is_err());
}
