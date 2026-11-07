//! Synthetic metadata/transport fixtures; these do not claim native prototype rendering.
use super::*;
use forge_assets::{
    GovernedAssetKitBuildReport, VegetationPrototypeBinding, VegetationPrototypeCatalog,
    asset_browser::AssetBrowser,
};
use forge_core::{CommandId, canonical_json_bytes, canonical_json_sha256};
use forge_project::{
    VegetationDocument, VegetationKind, zone_authoring::species::SpeciesReference,
};

#[test]
fn authoring_species_reuse_admitted_resources_without_rewriting_the_installed_head() {
    let fixture = fixture("authoring-species");
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
        .prepare_regeneration(base, CommandId::deterministic("species-authoring-retouch"))
        .unwrap();
    zone.execute(&request).unwrap();
    let read = |path: &str, _: usize| {
        fs::read(fixture.source.join(path)).map_err(|error| error.to_string())
    };
    assert!(
        forge_delivery::module_vegetation::resolve_species(
            &fixture.manifest,
            zone.head(),
            &governed,
            read
        )
        .is_err()
    );
    let restored = forge_delivery::module_vegetation::restore_authoring(
        &fixture.manifest,
        &original,
        zone.head(),
        &governed,
        read,
    )
    .unwrap();
    assert!(restored.imported.is_none());
    let species = restored.species.unwrap();
    assert_eq!(species.assets["test-tree"].path, "assets/mesh.glb");
    assert_eq!(
        species.assets["test-tree"].sha256,
        fixture.manifest.assets[0].sha256
    );
    assert_eq!(
        restored.binding.unwrap().sources,
        manifest.vegetation[0].sources
    );
    assert_eq!(
        species.catalog.vegetation_sha256,
        zone.head()
            .effective()
            .unwrap()
            .1
            .unwrap()
            .content_hash()
            .unwrap()
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

fn fixture(key: &str) -> Fixture {
    let mut fixture = Fixture::new(key);
    let pin = fixture.manifest.assets[0].sha256;
    let kit: GovernedAssetKitBuildReport = serde_json::from_value(serde_json::json!({
        "schema":"forge.blender-asset-kit-build", "schema_version":1,
        "asset_kit_id":"portable-species", "asset_kit_revision":1,
        "generator":{"input_manifest_sha256":bytes_sha256(b"synthetic-generator")},
        "provenance":{"source_kind":"procedural_original", "license":"LicenseRef-OpenRPG-Forge-Project-Generated",
            "cadle_content_used":false,"commercial_game_content_used":false},
        "qualification":{"technical_export_validated":true},
        "assets":[{"id":"test-tree","kind":"tree", "glb":{"path":"original-unavailable-kit/tree.glb","sha256":pin}}]
    })).unwrap();
    let browser = AssetBrowser::new(kit.clone()).unwrap();
    let (id, _) = browser.reference("test-tree").unwrap();
    fixture.manifest.assets[0].asset_id = id;
    let mut governed: GovernedImportCatalog =
        serde_json::from_slice(&fs::read(fixture.source.join("assets/catalog.json")).unwrap())
            .unwrap();
    governed.assets[0].id = id;
    fixture.replace(
        "assets/catalog.json",
        &serde_json::to_vec(&governed).unwrap(),
    );
    let mut zone = ZoneEditSession::from_archive_bytes(
        &fs::read(fixture.source.join("zones/start.json")).unwrap(),
    )
    .unwrap();
    let mut vegetation = VegetationDocument::from_json(include_str!(
        "../../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = fixture.manifest.project_id;
    vegetation.area_id = fixture.manifest.zones[0].area_id;
    vegetation
        .layers
        .retain(|layer| layer.kind == VegetationKind::Tree);
    vegetation.layers[0].material_filter = None;
    vegetation.layers[0].density_mask = None;
    vegetation.exclusions.clear();
    let layer = vegetation.layers[0].id.clone();
    let source = VegetationPrototypeCatalog {
        schema: "forge.vegetation-prototype-catalog".into(),
        schema_version: 1,
        vegetation_sha256: bytes_sha256(b"original-library-vegetation"),
        asset_kit_id: kit.asset_kit_id.clone(),
        asset_kit_revision: 1,
        bindings: vec![VegetationPrototypeBinding {
            layer_id: "library_trees".into(),
            asset_id: "test-tree".into(),
            glb_sha256: pin,
            source_pivot_m: [0.0; 3],
            source_height_m: 1.0,
            target_height_m: 5.0,
        }],
    };
    let source_hash = canonical_json_sha256(&source).unwrap();
    let mut base = zone.head().base.clone();
    base.vegetation = Some(vegetation);
    let request = zone
        .head()
        .prepare_regeneration(base, CommandId::deterministic("portable-species-base"))
        .unwrap();
    zone.execute(&request).unwrap();
    let request = zone
        .head()
        .prepare_species(
            layer,
            Some(vec![SpeciesReference {
                catalog_sha256: source_hash,
                source_layer_id: forge_project::VegetationLayerId::new("library_trees"),
                asset_id: "test-tree".into(),
            }]),
            CommandId::deterministic("portable-species-choice"),
        )
        .unwrap();
    zone.execute(&request).unwrap();
    let archive = zone.archive_bytes().unwrap();
    fixture.manifest.zones[0].archive_sha256 = bytes_sha256(&archive);
    fixture.manifest.zones[0].head_hash = zone.head().content_hash().unwrap();
    fixture.replace("zones/start.json", &archive);
    fs::create_dir(fixture.source.join("plants")).unwrap();
    let mut pins = Vec::new();
    for (path, bytes) in [
        (
            "plants/species.json",
            canonical_json_bytes(&source).unwrap(),
        ),
        ("plants/kit.json", serde_json::to_vec(&kit).unwrap()),
    ] {
        fs::write(fixture.source.join(path), &bytes).unwrap();
        let sha256 = bytes_sha256(&bytes);
        fixture.manifest.files.push(ModuleFile {
            path: path.into(),
            bytes: bytes.len() as u64,
            sha256,
            role: ModuleFileRole::Vegetation,
        });
        pins.push(ModuleFilePin {
            path: path.into(),
            sha256,
        });
    }
    fixture.manifest.vegetation = vec![ModuleVegetationBinding {
        area_id: fixture.manifest.zones[0].area_id,
        vegetation_sha256: zone
            .head()
            .effective()
            .unwrap()
            .1
            .unwrap()
            .content_hash()
            .unwrap(),
        sources: vec![ModuleVegetationSource::GovernedSpecies {
            catalogs: vec![pins[0].clone()],
            asset_kit: pins[1].clone(),
        }],
    }];
    fixture.manifest.compatibility.module_api_version = MODULE_VEGETATION_API_VERSION;
    fixture.save();
    fixture
}

#[test]
fn api3_species_metadata_roundtrips_through_transport_and_the_installed_reader() {
    let fixture = fixture("species-transport");
    orpmod::validate_directory(&fixture.source, false).unwrap();
    let package = fixture.root.join("species.orpmod");
    orpmod::pack(&fixture.source, fixture.descriptor(), &package, false).unwrap();
    let loaded = forge_delivery::module_runtime::load(
        &package,
        fixture.manifest.zones[0].area_id,
        Some(&fixture.root.join("cache")),
    )
    .unwrap();
    assert_ne!(loaded.package_root, fixture.source);
    let species = loaded.vegetation_species().unwrap().unwrap();
    assert_eq!(species.assets["test-tree"].path, "assets/mesh.glb");
    assert_ne!(
        species.assets["test-tree"].path,
        species.asset_kit.assets[0].glb.path
    );
    assert_eq!(
        bytes_sha256(
            &loaded
                .read_payload(&species.assets["test-tree"].path, 1024 * 1024)
                .unwrap()
        ),
        species.assets["test-tree"].sha256
    );
    assert_eq!(
        species.catalog.bindings[0].target_height_m.to_bits(),
        5.0_f32.to_bits()
    );
    // Fault injection into this isolated installed copy: the original still exists,
    // but the loader must not use it to repair a missing installed companion.
    fs::remove_file(loaded.package_root.join("plants/species.json")).unwrap();
    assert!(fixture.source.join("plants/species.json").is_file());
    assert!(loaded.vegetation_species().is_err());
}

#[test]
fn exact_transport_repins_cannot_replace_the_authored_species_catalog() {
    let mut fixture = fixture("species-repin");
    let path = "plants/species.json";
    let mut source: VegetationPrototypeCatalog =
        serde_json::from_slice(&fs::read(fixture.source.join(path)).unwrap()).unwrap();
    source.bindings[0].target_height_m = 25.0;
    let bytes = canonical_json_bytes(&source).unwrap();
    fixture.replace(path, &bytes);
    let ModuleVegetationSource::GovernedSpecies { catalogs, .. } =
        &mut fixture.manifest.vegetation[0].sources[0]
    else {
        unreachable!()
    };
    catalogs[0].sha256 = bytes_sha256(&bytes);
    fixture.save();
    assert!(orpmod::validate_directory(&fixture.source, false).is_err());
    let output = fixture.root.join("refused.orpmod");
    assert!(orpmod::pack(&fixture.source, fixture.descriptor(), &output, false).is_err());
    assert!(!output.exists());
}

#[test]
fn missing_companion_and_kit_model_pin_mismatch_refuse_before_output() {
    let mut fixture = fixture("species-kit-mismatch");
    let path = "plants/kit.json";
    let mut kit: GovernedAssetKitBuildReport =
        serde_json::from_slice(&fs::read(fixture.source.join(path)).unwrap()).unwrap();
    kit.assets[0].glb.sha256 = bytes_sha256(b"different-model");
    let bytes = serde_json::to_vec(&kit).unwrap();
    fixture.replace(path, &bytes);
    let ModuleVegetationSource::GovernedSpecies { asset_kit, .. } =
        &mut fixture.manifest.vegetation[0].sources[0]
    else {
        unreachable!()
    };
    asset_kit.sha256 = bytes_sha256(&bytes);
    fixture.save();
    assert!(orpmod::validate_directory(&fixture.source, false).is_err());
    fs::remove_file(fixture.source.join(path)).unwrap();
    assert!(orpmod::validate_directory(&fixture.source, false).is_err());
}
