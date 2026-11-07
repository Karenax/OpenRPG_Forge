//! Synthetic source-profile transport fixtures; no native shader or public-rights claim.
use super::*;
use forge_core::CommandId;
use forge_project::{
    VegetationDocument, VegetationKind,
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
};
#[path = "../../../../crates/forge_assets/tests/support/plant_package.rs"]
mod synthetic;

#[test]
fn authoring_imported_profiles_reuse_exact_resources_and_keep_installed_admission_strict() {
    let fixture = fixture();
    let mut zone = ZoneEditSession::from_archive_bytes(
        &fs::read(fixture.source.join("zones/start.json")).unwrap(),
    )
    .unwrap();
    let original = zone.head().clone();
    let manifest = fixture.manifest.clone();
    let governed: GovernedImportCatalog =
        serde_json::from_slice(&fs::read(fixture.source.join("assets/catalog.json")).unwrap())
            .unwrap();
    let mut base = zone.head().base.clone();
    base.terrain.name.push_str(" edited");
    let request = zone
        .head()
        .prepare_regeneration(base, CommandId::deterministic("imported-authoring-retouch"))
        .unwrap();
    zone.execute(&request).unwrap();
    let read = |path: &str, _: usize| {
        fs::read(fixture.source.join(path)).map_err(|error| error.to_string())
    };
    assert!(
        forge_delivery::module_vegetation::resolve_imported(&manifest, zone.head(), read).is_err()
    );
    let restored = forge_delivery::module_vegetation::restore_authoring(
        &manifest,
        &original,
        zone.head(),
        &governed,
        read,
    )
    .unwrap();
    assert!(restored.species.is_none());
    let imported = restored.imported.unwrap();
    assert_eq!(imported.prototypes.len(), 1);
    assert_eq!(imported.geometry.groups.len(), 6);
    assert_eq!(
        restored.binding.unwrap().sources,
        manifest.vegetation[0].sources
    );
    assert_ne!(
        imported.vegetation_sha256,
        manifest.vegetation[0].vegetation_sha256
    );
    assert_eq!(fixture.manifest, manifest);
    assert!(
        forge_delivery::module_vegetation::restore_authoring(
            &manifest,
            zone.head(),
            zone.head(),
            &governed,
            |_, _| panic!("stale origin read payloads")
        )
        .is_err()
    );
}

fn fixture() -> Fixture {
    let mut f = Fixture::new("imported-transport");
    let mut zone =
        ZoneEditSession::from_archive_bytes(&fs::read(f.source.join("zones/start.json")).unwrap())
            .unwrap();
    let mut vegetation = VegetationDocument::from_json(include_str!(
        "../../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = f.manifest.project_id;
    vegetation.area_id = f.manifest.zones[0].area_id;
    vegetation
        .layers
        .retain(|layer| layer.kind == VegetationKind::Tree);
    vegetation.layers.truncate(1);
    vegetation.layers[0].material_filter = None;
    vegetation.layers[0].density_mask = None;
    vegetation.exclusions.clear();
    let mut blob = bytes_sha256(b"one authored imported tree")
        .as_bytes()
        .to_vec();
    for value in [
        0.0_f64, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, 0.0, 0.0, 1.0, 0.25, 1.0, 0.0,
    ] {
        blob.extend_from_slice(&value.to_le_bytes());
    }
    let hash = bytes_sha256(&blob);
    let mut names: Vec<String> = forge_assets::source_vegetation_package::profile::PARAMETERS
        .iter()
        .map(|s| (*s).into())
        .collect();
    names.push("source_order".into());
    vegetation.schema_version = 2;
    vegetation.imported_source = Some(ImportedVegetationSource {
        grass: None,
        schema: "forge.imported-vegetation".into(),
        version: 1,
        blocks: vec![ImportedVegetationBlock {
            layer_id: vegetation.layers[0].id.to_string(),
            prototype_key: "tidewater_trees".into(),
            count: 1,
            encoding: ImportedInstanceEncoding::TransformF64LeV1,
            instances_sha256: hash,
            parameter_names: names,
        }],
    });
    let mut base = zone.head().base.clone();
    base.vegetation = Some(vegetation);
    zone.execute(
        &zone
            .head()
            .prepare_regeneration(base, CommandId::deterministic("imported-base"))
            .unwrap(),
    )
    .unwrap();
    let archive = zone.archive_bytes().unwrap();
    f.manifest.zones[0].archive_sha256 = bytes_sha256(&archive);
    f.manifest.zones[0].head_hash = zone.head().content_hash().unwrap();
    f.manifest.zones[0].source_blobs = vec![hash];
    f.replace("zones/start.json", &archive);
    fs::create_dir(f.source.join("sources")).unwrap();
    let path = format!("sources/{hash}.bin");
    fs::write(f.source.join(&path), &blob).unwrap();
    f.manifest.files.push(ModuleFile {
        path,
        bytes: blob.len() as u64,
        sha256: hash,
        role: ModuleFileRole::SourceBlob,
    });
    let package = synthetic::Fixture::new();
    let index = package.index();
    fs::create_dir_all(f.source.join("plants/imported")).unwrap();
    let index_pin = ModuleFilePin {
        path: "plants/imported/package.json".into(),
        sha256: bytes_sha256(&index),
    };
    let mut resources = Vec::new();
    for (name, bytes) in package
        .files
        .into_iter()
        .chain(std::iter::once(("package.json".into(), index)))
    {
        let path = format!("plants/imported/{name}");
        let sha256 = bytes_sha256(&bytes);
        let role = if name == "LICENSE-Tidewater.txt" {
            f.manifest.provenance.license_paths.push(path.clone());
            ModuleFileRole::License
        } else {
            ModuleFileRole::Vegetation
        };
        fs::write(f.source.join(&path), &bytes).unwrap();
        f.manifest.files.push(ModuleFile {
            path: path.clone(),
            bytes: bytes.len() as u64,
            sha256,
            role,
        });
        if name != "package.json" {
            resources.push(ModuleFilePin { path, sha256 });
        }
    }
    f.manifest.vegetation = vec![ModuleVegetationBinding {
        area_id: f.manifest.zones[0].area_id,
        vegetation_sha256: zone
            .head()
            .effective()
            .unwrap()
            .1
            .unwrap()
            .content_hash()
            .unwrap(),
        sources: vec![ModuleVegetationSource::ImportedPrototypes {
            index: index_pin,
            resources,
        }],
    }];
    f.manifest.compatibility.module_api_version = MODULE_VEGETATION_API_VERSION;
    f.save();
    f
}

#[test]
fn imported_source_resources_roundtrip_and_installed_reader_has_no_original_fallback() {
    let f = fixture();
    orpmod::validate_directory(&f.source, false).unwrap();
    let package = f.root.join("imported.orpmod");
    let mut descriptor = f.descriptor();
    descriptor.license_paths = f.manifest.provenance.license_paths.clone();
    orpmod::pack(&f.source, descriptor, &package, false).unwrap();
    let loaded = forge_delivery::module_runtime::load(
        &package,
        f.manifest.zones[0].area_id,
        Some(&f.root.join("cache")),
    )
    .unwrap();
    assert!(loaded.vegetation_species().unwrap().is_none());
    let resolved = loaded.vegetation_imported().unwrap().unwrap();
    assert_eq!(resolved.prototypes.len(), 1);
    assert_eq!(resolved.geometry.groups.len(), 6);
    assert_eq!(resolved.geometry.grass.len(), 3);
    assert_eq!(
        resolved.prototypes.values().next().unwrap().profile.group(),
        "canopy"
    );
    fs::remove_file(loaded.package_root.join("plants/imported/normal.bin")).unwrap();
    assert!(f.source.join("plants/imported/normal.bin").is_file());
    assert!(loaded.vegetation_imported().is_err());
}

#[test]
fn resource_repins_and_surplus_are_refused_before_binary_reads_or_package_output() {
    let mut f = fixture();
    let archive = fs::read(f.source.join("zones/start.json")).unwrap();
    let zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let original = f.manifest.clone();
    let path = "plants/imported/normal.bin";
    let original_bytes = fs::read(f.source.join(path)).unwrap();
    f.replace(path, &[0; 36]);
    let ModuleVegetationSource::ImportedPrototypes { resources, .. } =
        &mut f.manifest.vegetation[0].sources[0]
    else {
        unreachable!()
    };
    resources
        .iter_mut()
        .find(|pin| pin.path == path)
        .unwrap()
        .sha256 = bytes_sha256(&[0; 36]);
    f.save();
    let mut reads = Vec::new();
    assert!(
        forge_delivery::module_vegetation::resolve_imported(&f.manifest, zone.head(), |path, _| {
            reads.push(path.to_owned());
            fs::read(f.source.join(path)).map_err(|e| e.to_string())
        })
        .is_err()
    );
    assert_eq!(reads, ["plants/imported/package.json"]);
    let output = f.root.join("refused.orpmod");
    let mut descriptor = f.descriptor();
    descriptor.license_paths = f.manifest.provenance.license_paths.clone();
    assert!(orpmod::pack(&f.source, descriptor, &output, false).is_err());
    assert!(!output.exists());
    f.manifest = original;
    f.replace(path, &original_bytes);
    let path = "plants/imported/surplus.bin";
    let sha256 = bytes_sha256(b"surplus");
    fs::write(f.source.join(path), b"surplus").unwrap();
    f.manifest.files.push(ModuleFile {
        path: path.into(),
        bytes: 7,
        sha256,
        role: ModuleFileRole::Vegetation,
    });
    let ModuleVegetationSource::ImportedPrototypes { resources, .. } =
        &mut f.manifest.vegetation[0].sources[0]
    else {
        unreachable!()
    };
    resources.push(ModuleFilePin {
        path: path.into(),
        sha256,
    });
    f.save();
    reads.clear();
    assert!(
        forge_delivery::module_vegetation::resolve_imported(&f.manifest, zone.head(), |path, _| {
            reads.push(path.to_owned());
            fs::read(f.source.join(path)).map_err(|e| e.to_string())
        })
        .is_err()
    );
    assert_eq!(reads, ["plants/imported/package.json"]);
}

#[test]
fn explicit_grass_requires_the_same_closed_profile_index() {
    let mut f = fixture();
    let archive = fs::read(f.source.join("zones/start.json")).unwrap();
    let zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let mut head = zone.head().clone();
    let ModuleVegetationSource::ImportedPrototypes { index, .. } =
        &f.manifest.vegetation[0].sources[0]
    else {
        unreachable!()
    };
    let correct = index.sha256;
    for (profile, admitted) in [
        (correct, true),
        (bytes_sha256(b"different field profile"), false),
    ] {
        let source = head
            .base
            .vegetation
            .as_mut()
            .unwrap()
            .imported_source
            .as_mut()
            .unwrap();
        source.version = 2;
        source.grass = Some(forge_project::imported_vegetation::ImportedGrassField {
            profile_manifest_sha256: profile,
        });
        f.manifest.zones[0].head_hash = head.content_hash().unwrap();
        f.manifest.vegetation[0].vegetation_sha256 =
            head.effective().unwrap().1.unwrap().content_hash().unwrap();
        let mut reads = Vec::new();
        let result =
            forge_delivery::module_vegetation::resolve_imported(&f.manifest, &head, |path, _| {
                reads.push(path.to_owned());
                fs::read(f.source.join(path)).map_err(|e| e.to_string())
            });
        if admitted {
            assert!(result.is_ok());
        } else {
            assert!(
                result
                    .err()
                    .unwrap()
                    .to_string()
                    .contains("Authored grass field differs")
            );
            assert_eq!(reads, ["plants/imported/package.json"]);
        }
    }
}

#[test]
fn missing_license_role_and_wrong_authored_head_are_explicit_refusals() {
    let mut f = fixture();
    let archive = fs::read(f.source.join("zones/start.json")).unwrap();
    let zone = ZoneEditSession::from_archive_bytes(&archive).unwrap();
    let path = "plants/imported/LICENSE-Tidewater.txt";
    f.manifest
        .files
        .iter_mut()
        .find(|file| file.path == path)
        .unwrap()
        .role = ModuleFileRole::Vegetation;
    f.manifest
        .provenance
        .license_paths
        .retain(|name| name != path);
    assert!(
        forge_delivery::module_vegetation::resolve_imported(&f.manifest, zone.head(), |path, _| {
            fs::read(f.source.join(path)).map_err(|e| e.to_string())
        })
        .is_err()
    );
    f.manifest.zones[0].head_hash = bytes_sha256(b"other head");
    assert!(
        forge_delivery::module_vegetation::resolve_imported(
            &f.manifest,
            zone.head(),
            |_, _| panic!("must refuse before reading")
        )
        .is_err()
    );
}

#[test]
fn unknown_authored_prototype_and_changed_parameter_order_refuse_before_binary_transport() {
    let mut f = fixture();
    let mut zone =
        ZoneEditSession::from_archive_bytes(&fs::read(f.source.join("zones/start.json")).unwrap())
            .unwrap();
    let original = zone.head().base.clone();
    for unknown in [true, false] {
        let mut base = original.clone();
        let block = &mut base
            .vegetation
            .as_mut()
            .unwrap()
            .imported_source
            .as_mut()
            .unwrap()
            .blocks[0];
        if unknown {
            block.prototype_key = "unknown_tree".into();
        } else {
            block.parameter_names.swap(0, 1);
        }
        zone.execute(
            &zone
                .head()
                .prepare_regeneration(
                    base,
                    CommandId::deterministic(format!("bad-import-abi-{unknown}")),
                )
                .unwrap(),
        )
        .unwrap();
        let archive = zone.archive_bytes().unwrap();
        f.manifest.zones[0].head_hash = zone.head().content_hash().unwrap();
        f.manifest.zones[0].archive_sha256 = bytes_sha256(&archive);
        f.manifest.vegetation[0].vegetation_sha256 = zone
            .head()
            .effective()
            .unwrap()
            .1
            .unwrap()
            .content_hash()
            .unwrap();
        f.replace("zones/start.json", &archive);
        let mut reads = Vec::new();
        assert!(
            forge_delivery::module_vegetation::resolve_imported(
                &f.manifest,
                zone.head(),
                |path, _| {
                    reads.push(path.to_owned());
                    fs::read(f.source.join(path)).map_err(|e| e.to_string())
                }
            )
            .is_err()
        );
        assert_eq!(reads, ["plants/imported/package.json"]);
    }
}
