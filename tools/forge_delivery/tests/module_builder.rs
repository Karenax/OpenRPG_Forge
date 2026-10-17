use forge_assets::governed_import::*;
use forge_core::{AssetId, CommandId, EntityId, ModuleId, Transform, Vec3, bytes_sha256};
use forge_delivery::{module_builder::*, module_runtime, orpmod};
use forge_project::{
    authoring::new_terrain,
    module_gameplay_visuals::{GameplayVisualBinding, VisualAssetPin},
    module_package::ModuleRights,
    zone_authoring::session::ZoneEditSession,
};
use serde_json::json;
use std::{
    collections::BTreeMap,
    fs,
    path::PathBuf,
    sync::atomic::{AtomicU64, Ordering},
};

static NEXT: AtomicU64 = AtomicU64::new(0);
struct Temp(PathBuf);
impl Temp {
    fn new() -> Self {
        let root = std::env::temp_dir().join(format!(
            "forge-module-builder-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir(&root).unwrap();
        Self(root)
    }
}
impl Drop for Temp {
    fn drop(&mut self) {
        if self.0.is_absolute() && self.0.parent() == Some(std::env::temp_dir().as_path()) {
            let _ = fs::remove_dir_all(&self.0);
        }
    }
}

struct Fixture {
    zone: ZoneEditSession,
    catalog: GovernedImportCatalog,
    payloads: BTreeMap<String, Vec<u8>>,
}
impl Fixture {
    fn new(raster: bool) -> Self {
        let mut terrain = new_terrain("builder-review", "camp", "Le camp").unwrap();
        if raster {
            terrain.schema_version = 4;
            terrain.height_sampling.algorithm =
                forge_project::HeightSamplingAlgorithm::SampledRasterV1;
            terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
            terrain.sampled_source = Some(forge_project::sampled_terrain::SampledTerrainSource {
                schema: forge_project::sampled_terrain::SAMPLED_TERRAIN_SCHEMA.into(),
                schema_version: 1,
                width: 256,
                height: 256,
                origin_xz_m: [-256., -256.],
                sample_spacing_m: 2.,
                outside_height_m: 0.,
                encoding: forge_project::sampled_terrain::HeightEncoding::Float32Le,
                samples_sha256: bytes_sha256(b"not-transported-raster"),
            });
        }
        let mut zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
        let player = EntityId::deterministic("builder/player");
        let npc = EntityId::deterministic("builder/npc");
        let document=serde_json::from_value(json!({
            "schema":"forge.gameplay","version":1,"module_id":ModuleId::deterministic("builder/module"),
            "area_id":zone.head().base.terrain.area_id,"player":player,
            "profile":{"id":"forge.gate3-minimal","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,
                "attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,"max_action_receipts":512,"max_events_per_tick":128},
            "actors":[{"id":player,"name":"Player","role":"player","position":{"x":0.,"y":1.,"z":0.},
                "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false},
                {"id":npc,"name":"Keeper","role":"friendly","position":{"x":0.,"y":1.,"z":-2.},
                "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}],
            "objects":[],"items":[],"places":[],"dialogues":[],"quests":[]
        })).unwrap();
        let request = zone
            .head()
            .prepare_gameplay(Some(document), CommandId::deterministic("builder/gameplay"))
            .unwrap();
        zone.execute(&request).unwrap();
        let mesh = triangle();
        let recipe = br#"{"schema":"authored-triangle","version":1}"#.to_vec();
        let asset = GovernedImportAsset {
            id: AssetId::deterministic("builder/mesh"),
            name: "Authored triangle".into(),
            kind: GovernedAssetKind::Mesh,
            payload: GovernedAssetFile {
                path: "assets/triangle.glb".into(),
                sha256: bytes_sha256(&mesh),
                bytes: mesh.len() as u64,
            },
            provenance: AssetProvenance {
                source: "Original fixture geometry".into(),
                author: "Forge tests".into(),
                license: "CC0-1.0".into(),
                evidence: "licenses/mesh.txt".into(),
                acquired_on: "2026-10-15".into(),
                rights: AssetRights {
                    local_use: true,
                    redistribution: RedistributionRights::Allowed,
                    redistribution_evidence: Some("licenses/mesh.txt".into()),
                },
            },
            derived_from: None,
            transformations: vec![],
            authored_changes: true,
            durable_source: Some(DurableAssetSource::Recipe {
                file: GovernedAssetFile {
                    path: "recipes/triangle.json".into(),
                    sha256: bytes_sha256(&recipe),
                    bytes: recipe.len() as u64,
                },
            }),
            thumbnail: None,
        };
        let request = zone
            .head()
            .prepare_gameplay_visuals(
                Some(vec![GameplayVisualBinding {
                    area_id: zone.head().base.terrain.area_id,
                    entity_id: npc,
                    asset: VisualAssetPin {
                        asset_id: asset.id,
                        sha256: asset.payload.sha256,
                    },
                    local_transform: Transform::from_translation(Vec3::new(0., -0.5, 0.)),
                }]),
                &[VisualAssetPin {
                    asset_id: asset.id,
                    sha256: asset.payload.sha256,
                }],
                CommandId::deterministic("builder/binding"),
            )
            .unwrap();
        zone.execute(&request).unwrap();
        Self {
            zone,
            catalog: GovernedImportCatalog {
                schema: GOVERNED_IMPORT_SCHEMA.into(),
                schema_version: GOVERNED_IMPORT_VERSION,
                assets: vec![asset],
            },
            payloads: BTreeMap::from([
                ("assets/triangle.glb".into(), mesh),
                ("recipes/triangle.json".into(), recipe),
                (
                    "licenses/mesh.txt".into(),
                    b"Original test geometry CC0-1.0".to_vec(),
                ),
            ]),
        }
    }
    fn metadata() -> ExportMetadata {
        ExportMetadata {
            name: "Le camp exporté".into(),
            version: "1.0.0".into(),
            publisher: "Forge tests".into(),
            license_text: "Original adventure authored for local regression.".into(),
            rights: ModuleRights::PrivateLocalOnly,
        }
    }
    fn build(&self, root: &std::path::Path) -> Result<ExportedModule, String> {
        build_zone(
            root,
            &self.zone.archive_bytes().unwrap(),
            self.zone.head().content_hash().unwrap(),
            Self::metadata(),
            self.catalog.clone(),
            self.payloads.clone(),
        )
    }
}

fn triangle() -> Vec<u8> {
    let mut binary = Vec::new();
    for value in [0_f32, 0., 0., 1., 0., 0., 0., 1., 0.] {
        binary.extend(value.to_le_bytes());
    }
    for index in [0_u16, 1, 2] {
        binary.extend(index.to_le_bytes());
    }
    while !binary.len().is_multiple_of(4) {
        binary.push(0);
    }
    let mut document=serde_json::to_vec(&json!({"asset":{"version":"2.0"},"buffers":[{"byteLength":binary.len()}],
        "bufferViews":[{"buffer":0,"byteLength":36,"target":34962},{"buffer":0,"byteOffset":36,"byteLength":6,"target":34963}],
        "accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]},
            {"bufferView":1,"componentType":5123,"count":3,"type":"SCALAR"}],
        "meshes":[{"primitives":[{"attributes":{"POSITION":0},"indices":1}]}],"nodes":[{"mesh":0}],"scenes":[{"nodes":[0]}],"scene":0})).unwrap();
    while !document.len().is_multiple_of(4) {
        document.push(b' ');
    }
    let mut glb = b"glTF".to_vec();
    glb.extend(2_u32.to_le_bytes());
    glb.extend(((28 + document.len() + binary.len()) as u32).to_le_bytes());
    glb.extend((document.len() as u32).to_le_bytes());
    glb.extend(b"JSON");
    glb.extend(document);
    glb.extend((binary.len() as u32).to_le_bytes());
    glb.extend(b"BIN\0");
    glb.extend(binary);
    glb
}

fn origin(exported: &ExportedModule) -> ExportOrigin {
    let bytes: std::sync::Arc<[u8]> = fs::read(&exported.package_path).unwrap().into();
    ExportOrigin {
        expected_package_sha256: bytes_sha256(&bytes),
        package_bytes: bytes,
    }
}

fn build_with_origin(
    fixture: &Fixture,
    root: &std::path::Path,
    metadata: ExportMetadata,
    source: Option<ExportOrigin>,
) -> Result<ExportedModule, String> {
    build_zone_with_origin(
        root,
        &fixture.zone.archive_bytes().unwrap(),
        fixture.zone.head().content_hash().unwrap(),
        metadata,
        fixture.catalog.clone(),
        fixture.payloads.clone(),
        source,
    )
}

#[test]
fn memory_origin_admission_preserves_exact_module_transport_and_rights_documents() {
    let temp = Temp::new();
    let fixture = Fixture::new(false);
    let original = fixture.build(&temp.0.join("Original")).unwrap();
    let input = origin(&original);
    let read = orpmod::read_origin_documents(input.package_bytes.clone()).unwrap();
    assert_eq!(
        read.module,
        orpmod::inspect(&original.package_path, false).unwrap()
    );
    assert_eq!(
        read.module_manifest,
        fs::read(original.source_root.join("module.json")).unwrap()
    );
    assert_eq!(
        forge_delivery::hash(&read.transport_manifest),
        original.transport_manifest_sha256
    );
    for path in read
        .module
        .provenance
        .license_paths
        .iter()
        .chain(&read.module.provenance.proof_paths)
        .chain(std::iter::once(&read.module.provenance.credits_path))
    {
        assert_eq!(
            read.evidence[path],
            fs::read(original.source_root.join(path)).unwrap()
        );
    }
    let mut corrupted = input.package_bytes.to_vec();
    let last = corrupted.len() - 1;
    corrupted[last] ^= 1;
    assert!(orpmod::read_origin_documents(corrupted.into()).is_err());
}

#[test]
fn edited_module_reexport_preserves_origin_documents_byte_for_byte() {
    let temp = Temp::new();
    let mut fixture = Fixture::new(false);
    let original = fixture.build(&temp.0.join("Original")).unwrap();
    let old = orpmod::inspect(&original.package_path, false).unwrap();
    let mut document = (**fixture.zone.head().corrections.gameplay.as_ref().unwrap()).clone();
    document.actors[1].name = "Retouched keeper".into();
    let edit = fixture
        .zone
        .head()
        .prepare_gameplay(Some(document), CommandId::deterministic("origin-retouch"))
        .unwrap();
    fixture.zone.execute(&edit).unwrap();
    let mut metadata = Fixture::metadata();
    metadata.version = "1.1.0".into();
    metadata.publisher = "New author".into();
    metadata.license_text = "Conditions of the new authored changes".into();
    let exported = build_with_origin(
        &fixture,
        &temp.0.join("Reexport"),
        metadata,
        Some(origin(&original)),
    )
    .unwrap();
    let module = orpmod::inspect(&exported.package_path, false).unwrap();
    assert_eq!(module.module_id, old.module_id);
    assert_eq!(module.provenance.rights, ModuleRights::PrivateLocalOnly);
    for path in old
        .provenance
        .license_paths
        .iter()
        .chain(&old.provenance.proof_paths)
        .chain(std::iter::once(&old.provenance.credits_path))
    {
        let preserved = format!("_forge-export/origin/files/{path}");
        assert_eq!(
            fs::read(exported.source_root.join(&preserved)).unwrap(),
            fs::read(original.source_root.join(path)).unwrap()
        );
        assert!(
            module.provenance.license_paths.contains(&preserved)
                || module.provenance.proof_paths.contains(&preserved)
        );
    }
    assert_eq!(
        fs::read(
            exported
                .source_root
                .join("_forge-export/origin/module.json")
        )
        .unwrap(),
        fs::read(original.source_root.join("module.json")).unwrap()
    );
    let proof: serde_json::Value = serde_json::from_slice(
        &fs::read(
            exported
                .source_root
                .join("_forge-export/origin/source-package.json"),
        )
        .unwrap(),
    )
    .unwrap();
    assert_eq!(proof["package_sha256"], original.package_sha256.to_string());
    assert_eq!(proof["origin_rights"], "private_local_only");
    assert!(
        fs::read_to_string(exported.source_root.join("_forge-export/credits.txt"))
            .unwrap()
            .contains("_forge-export/origin/")
    );
}

#[test]
fn private_origin_cannot_be_relabelled_redistributable_by_a_freeform_new_license() {
    let temp = Temp::new();
    let fixture = Fixture::new(false);
    let original = fixture.build(&temp.0.join("Original")).unwrap();
    let mut metadata = Fixture::metadata();
    metadata.rights = ModuleRights::Redistributable;
    metadata.license_text = "CC0-1.0 declared by the importing user".into();
    let output = temp.0.join("Forbidden publication");
    let error =
        build_with_origin(&fixture, &output, metadata, Some(origin(&original))).unwrap_err();
    assert!(error.to_lowercase().contains("origin"), "{error}");
    assert!(!output.exists());
}

#[test]
fn corrupt_wrong_hash_or_foreign_module_origin_fails_before_any_output() {
    let temp = Temp::new();
    let mut fixture = Fixture::new(false);
    let original = fixture.build(&temp.0.join("Original")).unwrap();
    let mut wrong = origin(&original);
    wrong.expected_package_sha256 = bytes_sha256(b"unreviewed origin");
    let output = temp.0.join("Wrong hash");
    assert!(build_with_origin(&fixture, &output, Fixture::metadata(), Some(wrong)).is_err());
    assert!(!output.exists());
    let mut corrupt = origin(&original);
    let mut bytes = corrupt.package_bytes.to_vec();
    let last = bytes.len() - 1;
    bytes[last] ^= 1;
    corrupt.expected_package_sha256 = bytes_sha256(&bytes);
    corrupt.package_bytes = bytes.into();
    let output = temp.0.join("Corrupt payload");
    assert!(build_with_origin(&fixture, &output, Fixture::metadata(), Some(corrupt)).is_err());
    assert!(!output.exists());
    let mut document = (**fixture.zone.head().corrections.gameplay.as_ref().unwrap()).clone();
    document.module_id = ModuleId::deterministic("foreign-export-module");
    let edit = fixture
        .zone
        .head()
        .prepare_gameplay(Some(document), CommandId::deterministic("foreign-origin"))
        .unwrap();
    fixture.zone.execute(&edit).unwrap();
    let output = temp.0.join("Foreign origin");
    assert!(
        build_with_origin(
            &fixture,
            &output,
            Fixture::metadata(),
            Some(origin(&original))
        )
        .is_err()
    );
    assert!(!output.exists());
}

#[test]
fn authored_export_without_an_origin_keeps_the_legacy_builder_contract() {
    let temp = Temp::new();
    let fixture = Fixture::new(false);
    let original = fixture.build(&temp.0.join("Existing API")).unwrap();
    let exported = build_with_origin(
        &fixture,
        &temp.0.join("New API authored"),
        Fixture::metadata(),
        None,
    )
    .unwrap();
    assert_eq!(original.package_sha256, exported.package_sha256);
    assert_eq!(
        orpmod::inspect(&original.package_path, false).unwrap(),
        orpmod::inspect(&exported.package_path, false).unwrap()
    );
}

#[test]
fn reviewed_zone_exports_an_independent_installable_package_with_exact_visuals_and_recipe() {
    let temp = Temp::new();
    let fixture = Fixture::new(false);
    let exported = fixture.build(&temp.0.join("Exports Élan")).unwrap();
    assert!(exported.package_path.is_file() && exported.package_path.is_absolute());
    assert_eq!(
        exported.module_id,
        fixture
            .zone
            .head()
            .corrections
            .gameplay
            .as_ref()
            .unwrap()
            .module_id
    );
    assert_eq!(
        exported.head_hash,
        fixture.zone.head().content_hash().unwrap()
    );
    assert_eq!(
        exported.package_sha256.to_string(),
        forge_delivery::file_hash(&exported.package_path).unwrap()
    );
    let manifest = orpmod::inspect(&exported.package_path, false).unwrap();
    assert_eq!(
        manifest.gameplay_visuals,
        fixture
            .zone
            .head()
            .corrections
            .gameplay_visuals
            .clone()
            .unwrap()
    );
    let moved = temp.0.join("unavailable-authoring");
    assert!(exported.source_root.starts_with(&temp.0) && moved.starts_with(&temp.0));
    fs::rename(&exported.source_root, moved).unwrap();
    let loaded = module_runtime::load(
        &exported.package_path,
        exported.area_id,
        Some(&temp.0.join("Independent Cache")),
    )
    .unwrap();
    assert_eq!(loaded.zone.head(), fixture.zone.head());
    assert_eq!(
        loaded.read_payload("recipes/triangle.json", 4096).unwrap(),
        fixture.payloads["recipes/triangle.json"]
    );
}

#[test]
fn repeated_exports_keep_both_packages_and_never_overwrite_the_first_revision() {
    let temp = Temp::new();
    let fixture = Fixture::new(false);
    let root = temp.0.join("exports");
    let first = fixture.build(&root).unwrap();
    let first_bytes = fs::read(&first.package_path).unwrap();
    let second = fixture.build(&root).unwrap();
    assert_ne!(first.package_path, second.package_path);
    assert_ne!(first.source_root, second.source_root);
    assert_eq!(fs::read(&first.package_path).unwrap(), first_bytes);
    assert_eq!(
        orpmod::inspect(&first.package_path, false).unwrap(),
        orpmod::inspect(&second.package_path, false).unwrap()
    );
}

#[test]
fn stale_source_unclosed_payloads_missing_assets_and_malformed_media_leave_no_output() {
    for fault in 0..5 {
        let temp = Temp::new();
        let mut fixture = Fixture::new(false);
        let root = temp.0.join("exports");
        let mut payloads = fixture.payloads.clone();
        let mut catalog = fixture.catalog.clone();
        let mut expected = fixture.zone.head().content_hash().unwrap();
        match fault {
            0 => expected = bytes_sha256(b"stale source"),
            1 => {
                payloads.insert("extra.bin".into(), b"unreferenced".to_vec());
            }
            2 => catalog.assets[0].payload.sha256 = bytes_sha256(b"missing revision"),
            3 => {
                payloads.remove("recipes/triangle.json");
            }
            _ => {
                let bytes = b"invalid GLB".to_vec();
                catalog.assets[0].payload.bytes = bytes.len() as u64;
                catalog.assets[0].payload.sha256 = bytes_sha256(&bytes);
                payloads.insert("assets/triangle.glb".into(), bytes);
                let pin = VisualAssetPin {
                    asset_id: catalog.assets[0].id,
                    sha256: catalog.assets[0].payload.sha256,
                };
                let mut bindings = fixture
                    .zone
                    .head()
                    .corrections
                    .gameplay_visuals
                    .clone()
                    .unwrap();
                bindings[0].asset = pin;
                let request = fixture
                    .zone
                    .head()
                    .prepare_gameplay_visuals(
                        Some(bindings),
                        &[pin],
                        CommandId::deterministic("builder/corrupt-media-pin"),
                    )
                    .unwrap();
                fixture.zone.execute(&request).unwrap();
                expected = fixture.zone.head().content_hash().unwrap();
            }
        }
        assert!(
            build_zone(
                &root,
                &fixture.zone.archive_bytes().unwrap(),
                expected,
                Fixture::metadata(),
                catalog,
                payloads
            )
            .is_err()
        );
        assert!(
            !root.exists(),
            "fault {fault} created output before validation"
        );
    }
}

#[test]
fn publication_requires_explicit_asset_rights_and_embedded_evidence_before_output() {
    let temp = Temp::new();
    let fixture = Fixture::new(false);
    let root = temp.0.join("exports");
    let mut metadata = Fixture::metadata();
    metadata.rights = ModuleRights::Redistributable;
    let mut catalog = fixture.catalog.clone();
    catalog.assets[0].provenance.rights.redistribution = RedistributionRights::Unknown;
    assert!(
        build_zone(
            &root,
            &fixture.zone.archive_bytes().unwrap(),
            fixture.zone.head().content_hash().unwrap(),
            metadata,
            catalog,
            fixture.payloads.clone()
        )
        .is_err()
    );
    assert!(!root.exists());
    let mut payloads = fixture.payloads.clone();
    payloads.remove("licenses/mesh.txt");
    assert!(
        build_zone(
            &root,
            &fixture.zone.archive_bytes().unwrap(),
            fixture.zone.head().content_hash().unwrap(),
            Fixture::metadata(),
            fixture.catalog.clone(),
            payloads
        )
        .is_err()
    );
    assert!(!root.exists());
}

#[test]
fn unsupported_raster_or_missing_rpg_is_refused_without_creating_an_export() {
    let temp = Temp::new();
    let fixture = Fixture::new(true);
    let root = temp.0.join("exports");
    ZoneEditSession::from_archive_bytes(&fixture.zone.archive_bytes().unwrap()).unwrap();
    assert!(fixture.build(&root).is_err());
    assert!(!root.exists());
    let zone = ZoneEditSession::from_initial_terrain(
        new_terrain("builder-review", "walk", "Walk").unwrap(),
    )
    .unwrap();
    assert!(
        build_zone(
            &root,
            &zone.archive_bytes().unwrap(),
            zone.head().content_hash().unwrap(),
            Fixture::metadata(),
            fixture.catalog.clone(),
            fixture.payloads.clone()
        )
        .is_err()
    );
    assert!(!root.exists());
}
