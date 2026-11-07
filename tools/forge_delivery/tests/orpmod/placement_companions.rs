//! Full archive admission for synthetic source companion packages.
use super::source_graph::fixture as source_graph_fixture;
use super::*;
use forge_assets::{
    source_particle_emission::SourceSingleBurstLoopParameters,
    source_particle_package::SourceParticlePackage,
    source_particle_transport::{
        SourceParticleBlob, SourceParticleControlLayout, SourceParticleOwnerTransport,
        SourceParticleProgram, SourceParticleTransport, SourceParticleVertexTransport,
    },
    source_physics_package::SourcePhysicsPackage,
};
use forge_core::{CommandId, ContentHash, EntityId, Transform};
use forge_project::zone_authoring::placements::{
    ManualPlacement, ParticlePackageReference, PhysicsPackageReference, PlacementEdit,
};

#[path = "../support/installed_fixture.rs"]
#[allow(
    dead_code,
    reason = "This test uses only the installation fixture entry point"
)]
mod installed_fixture;

fn physics_package(carrier: &[u8], orphan: bool) -> (ContentHash, Vec<u8>) {
    let carrier_pin = bytes_sha256(carrier);
    let manifest = serde_json::to_vec(&serde_json::json!({
        "schema":"openrpg.source-physics-transport.v1",
        "rows":[{"carrier_sha256":carrier_pin,"components":[]}]
    }))
    .unwrap();
    let manifest_pin = bytes_sha256(&manifest);
    let candidates = if orphan {
        BTreeMap::from([("orphan.bin".into(), vec![1, 2, 3])])
    } else {
        BTreeMap::new()
    };
    let wire = SourcePhysicsPackage {
        carrier_sha256: carrier_pin,
        manifest_sha256: manifest_pin,
        manifest,
        candidates,
    }
    .encode()
    .unwrap();
    (manifest_pin, wire)
}

fn particle_package(carrier: &[u8], orphan: bool) -> (ContentHash, Vec<u8>) {
    let mut resources = BTreeMap::new();
    let mut blob = |path: &str, bytes: Vec<u8>| {
        let pin = SourceParticleBlob {
            path: path.into(),
            sha256: bytes_sha256(&bytes),
            byte_length: bytes.len(),
        };
        resources.insert(path.into(), bytes);
        pin
    };
    let compute = SourceParticleProgram {
        source: blob(
            "compute.wgsl",
            b"@compute @workgroup_size(1) fn advance() {}".to_vec(),
        ),
        entry_point: "advance".into(),
    };
    let vertex = SourceParticleProgram {
        source: blob(
            "vertex.wgsl",
            b"@vertex fn vertex() -> @builtin(position) vec4<f32> { return vec4<f32>(0.0); }"
                .to_vec(),
        ),
        entry_point: "vertex".into(),
    };
    let owner = SourceParticleOwnerTransport {
        source_component_guid: "a".repeat(32),
        source_component_file_id: "11400000".into(),
        source_node: 0,
        source_primitive: 0,
        source_material: 0,
        compute,
        curve_atlas: blob("curve.bin", vec![0; 8]),
        curve_width: 1,
        curve_height: 1,
        curve_bank: blob("bank.bin", vec![0; 1024]),
        initial_state: blob("state.bin", vec![0; 16]),
        state_prefix_bytes: 16,
        workgroups: 1,
        control: SourceParticleControlLayout {
            delta_offset: 1008,
            mode_offset: 1012,
            stamp_offset: 1016,
            count_offset: 1020,
            reset_mode: 3.0,
            allocate_mode: 2.0,
            update_mode: 1.0,
        },
        spawner: SourceSingleBurstLoopParameters {
            loop_duration_seconds: 3.0,
            loop_count: 1,
            burst_delay_seconds: 0.0,
            burst_count: 1.0,
            burst_active: true,
            initial_play: true,
            prewarm_steps: 0,
        },
        projection: SourceParticleVertexTransport {
            frame_layout: None,
            program: vertex,
            bank: blob("projection-bank.bin", vec![0; 1024]),
            native_vertices: blob("vertices.bin", vec![0; 3 * 64]),
            vertex_count: 3,
            input_stride_bytes: 64,
            output_stride_bytes: 112,
        },
    };
    if orphan {
        resources.insert("orphan.bin".into(), vec![1, 2, 3]);
    }
    let contract = SourceParticleTransport {
        schema_version: 1,
        carrier_sha256: bytes_sha256(carrier),
        source_archive_sha256: bytes_sha256(b"synthetic archive"),
        source_graph_sha256: bytes_sha256(b"synthetic graph"),
        licence_ref: "LicenseRef-local-fixture".into(),
        owners: vec![owner],
    };
    let manifest = serde_json::to_vec(&contract).unwrap();
    let manifest_pin = bytes_sha256(&manifest);
    let wire = SourceParticlePackage {
        carrier_sha256: bytes_sha256(carrier),
        manifest_sha256: manifest_pin,
        manifest,
        resources,
    }
    .encode()
    .unwrap();
    (manifest_pin, wire)
}

fn proof(
    kind: ModulePlacementCompanionKind,
    asset_id: AssetId,
    carrier: ContentHash,
    manifest: ContentHash,
    package: ContentHash,
) -> Vec<u8> {
    let field = match kind {
        ModulePlacementCompanionKind::Physics => "source_physics_package",
        ModulePlacementCompanionKind::Particles => "source_particle_package",
    };
    let mut profile = serde_json::json!({
        "external_id":"synthetic-asset","model_content_sha256":carrier
    });
    profile[field] = serde_json::json!({
        "carrier_sha256":carrier,"manifest_sha256":manifest,"sha256":package
    });
    let details = serde_json::json!({"id":"synthetic-asset","model_sha256":carrier});
    let catalog = serde_json::json!({"id":"synthetic-asset","sha256":carrier});
    let revision = bytes_sha256(b"synthetic-registry-snapshot");
    let metadata = serde_json::json!({
        "schema":"forge.private-vault-package-proof.v1",
        "library_id":"synthetic-asset",
        "model_sha256":carrier,
        "library_revision":revision,
        "metadata":details,
        "functional_profile":profile,
        "source_catalog_entry":catalog
    });
    serde_json::to_vec(&serde_json::json!({
        "schema":"forge.private-vault-companion-proof.v1",
        "kind":kind,"asset_id":asset_id,"carrier_sha256":carrier,
        "manifest_sha256":manifest,"package_sha256":package,
        "registry_snapshot_revision":revision,
        "selected_details_sha256":forge_core::canonical_json_sha256(&metadata["metadata"]).unwrap(),
        "selected_profile_sha256":forge_core::canonical_json_sha256(&metadata["functional_profile"]).unwrap(),
        "selected_catalog_entry_sha256":forge_core::canonical_json_sha256(&metadata["source_catalog_entry"]).unwrap(),
        "metadata":metadata
    })).unwrap()
}

#[allow(
    clippy::too_many_lines,
    reason = "Keep the synthetic module, source packets and their proofs in one fixture"
)]
fn fixture(orphan_physics: bool, orphan_particles: bool) -> Fixture {
    let mut module = Fixture::new("placement-companions");
    let state = forge_assets::source_graph_bundle::SOURCE_GRAPH_STATE_BUFFER_DECLARATION;
    let forward = format!("{state}\n{}", source_graph_fixture::FORWARD);
    let prepass = format!(
        "{state}\n@vertex fn vertex(@location(0) p: vec3<f32>) -> @builtin(position) vec4<f32> {{ return vec4<f32>(p, 1.0); }}\n@fragment fn fragment() {{}}\n"
    );
    let model = source_graph_fixture::fixture_with_stages(&forward, &prepass).unwrap();
    super::source_graph::set_model(&mut module, &model);
    let id = forge_assets::governed_import::vault_asset_id("synthetic-asset");
    let catalog_path = module.source.join("assets/catalog.json");
    let mut catalog: GovernedImportCatalog =
        serde_json::from_slice(&fs::read(&catalog_path).unwrap()).unwrap();
    catalog.assets[0].id = id;
    module.manifest.assets[0].asset_id = id;
    module.replace(
        "assets/catalog.json",
        &serde_json::to_vec(&catalog).unwrap(),
    );
    let carrier = bytes_sha256(&model);
    let (physics_manifest, physics) = physics_package(&model, orphan_physics);
    let (particle_manifest, particles) = particle_package(&model, orphan_particles);
    let mut zone = ZoneEditSession::from_archive_bytes(
        &fs::read(module.source.join("zones/start.json")).unwrap(),
    )
    .unwrap();
    let placement = ManualPlacement {
        id: EntityId::deterministic("companion/placement"),
        name: "Fixture".into(),
        asset_id: module.manifest.assets[0].asset_id,
        asset_hash: carrier,
        physics: Some(Box::new(PhysicsPackageReference {
            schema_version: 1,
            carrier_sha256: carrier,
            manifest_sha256: physics_manifest,
            package_sha256: bytes_sha256(&physics),
        })),
        particles: Some(Box::new(ParticlePackageReference {
            schema_version: 1,
            carrier_sha256: carrier,
            manifest_sha256: particle_manifest,
            package_sha256: bytes_sha256(&particles),
        })),
        local_lights: None,
        transform: Transform::IDENTITY,
        locked: false,
    };
    for (name, edit) in [
        (
            "insert",
            PlacementEdit::Insert {
                placement: placement.clone(),
            },
        ),
        ("remove", PlacementEdit::Remove { id: placement.id }),
    ] {
        let request = zone
            .head()
            .prepare_placement(CommandId::deterministic(name), edit)
            .unwrap();
        zone.execute(&request).unwrap();
    }
    let archive = zone.archive_bytes().unwrap();
    module.manifest.zones[0].head_hash = zone.head().content_hash().unwrap();
    module.manifest.zones[0].archive_sha256 = bytes_sha256(&archive);
    module.replace("zones/start.json", &archive);
    fs::create_dir(module.source.join("companions")).unwrap();
    module.manifest.compatibility.module_api_version = MODULE_PLACEMENT_COMPANIONS_API_VERSION;
    for (kind, path, manifest_sha256, wire) in [
        (
            ModulePlacementCompanionKind::Physics,
            "companions/physics.orpp",
            physics_manifest,
            physics,
        ),
        (
            ModulePlacementCompanionKind::Particles,
            "companions/particles.orpe",
            particle_manifest,
            particles,
        ),
    ] {
        fs::write(module.source.join(path), &wire).unwrap();
        let pin = bytes_sha256(&wire);
        let proof_path = format!("{path}.proof.json");
        let evidence = proof(
            kind,
            module.manifest.assets[0].asset_id,
            carrier,
            manifest_sha256,
            pin,
        );
        fs::write(module.source.join(&proof_path), &evidence).unwrap();
        let proof_hash = bytes_sha256(&evidence);
        module.manifest.files.push(ModuleFile {
            path: proof_path.clone(),
            bytes: evidence.len() as u64,
            sha256: proof_hash,
            role: ModuleFileRole::Provenance,
        });
        module
            .manifest
            .provenance
            .proof_paths
            .push(proof_path.clone());
        module.manifest.files.push(ModuleFile {
            path: path.into(),
            bytes: wire.len() as u64,
            sha256: pin,
            role: ModuleFileRole::PlacementCompanion,
        });
        module
            .manifest
            .placement_companions
            .push(ModulePlacementCompanion {
                areas: vec![module.manifest.zones[0].area_id],
                kind,
                asset_id: module.manifest.assets[0].asset_id,
                carrier_sha256: carrier,
                manifest_sha256,
                package: ModuleFilePin {
                    path: path.into(),
                    sha256: pin,
                },
                proof: ModuleFilePin {
                    path: proof_path,
                    sha256: proof_hash,
                },
            });
    }
    module.save();
    module
}

#[test]
fn dedicated_runtime_plans_pinned_companions_while_legacy_hosts_and_publication_refuse() {
    let module = fixture(false, false);
    assert_eq!(
        orpmod::validate_directory(&module.source, false).unwrap(),
        module.manifest
    );
    let output = module.root.join("companions.orpmod");
    let (_, transport) = orpmod::pack(&module.source, module.descriptor(), &output, false).unwrap();
    let loaded = forge_delivery::module_runtime::load(
        &output,
        module.manifest.zones[0].area_id,
        Some(&module.root.join("cache")),
    )
    .unwrap();
    assert!(loaded.reject_unhandled_placement_companions().is_err());

    let installed = installed_fixture::Fixture::new();
    install::apply(&install::prepare(&installed.install, std::slice::from_ref(&output)).unwrap())
        .unwrap();
    let selection = forge_delivery::installed_modules::ModuleSelection {
        module_id: module.manifest.module_id,
        area_id: module.manifest.zones[0].area_id,
    };
    let error = forge_delivery::installed_modules::plan(&installed.install, selection).unwrap_err();
    assert!(
        error.contains("pas encore pris en charge"),
        "unexpected refusal: {error}"
    );

    let dedicated = installed_fixture::Fixture::with_module_runtime();
    install::apply(&install::prepare(&dedicated.install, &[output]).unwrap()).unwrap();
    let plan = forge_delivery::installed_modules::plan(&dedicated.install, selection).unwrap();
    assert_eq!(plan.executable, dedicated.core_root().join("module.bin"));
    for (flag, pin) in [
        (
            "--expected-head",
            loaded.zone.head().content_hash().unwrap().to_string(),
        ),
        (
            "--expected-archive-sha256",
            bytes_sha256(&loaded.archive).to_string(),
        ),
        ("--expected-package-manifest-sha256", transport),
    ] {
        assert!(plan.arguments.windows(2).any(|pair| pair == [flag, &pin]));
    }

    let mut public = module.manifest.clone();
    public.provenance.rights = ModuleRights::Redistributable;
    fs::write(
        module.source.join("module.json"),
        public.to_bytes().unwrap(),
    )
    .unwrap();
    let error = orpmod::validate_directory(&module.source, false).unwrap_err();
    assert!(
        error.contains("redistribution rights"),
        "unexpected refusal: {error}"
    );
}

#[test]
fn semantic_validation_rejects_orphan_buffers_despite_matching_outer_hashes() {
    for (orphan_physics, orphan_particles) in [(true, false), (false, true)] {
        let module = fixture(orphan_physics, orphan_particles);
        let error = orpmod::validate_directory(&module.source, false).unwrap_err();
        assert!(
            error.contains("without declared owner"),
            "unexpected refusal: {error}"
        );
    }
}

#[test]
fn companion_proof_must_match_subject_and_selected_profile_even_when_repinned() {
    for case in 0..5 {
        let mut module = fixture(false, false);
        let path = module.manifest.placement_companions[0].proof.path.clone();
        let mut proof: serde_json::Value =
            serde_json::from_slice(&fs::read(module.source.join(&path)).unwrap()).unwrap();
        match case {
            0 => proof["package_sha256"] = bytes_sha256(b"another package").to_string().into(),
            1 => {
                proof["metadata"]["functional_profile"]["source_physics_package"]["sha256"] =
                    bytes_sha256(b"another package").to_string().into()
            }
            2 => {
                proof["metadata"]["metadata"]["rights"]["local_use"] = false.into();
                proof["selected_details_sha256"] =
                    forge_core::canonical_json_sha256(&proof["metadata"]["metadata"])
                        .unwrap()
                        .to_string()
                        .into();
            }
            3 => {
                proof["metadata"]["library_id"] = "other-asset".into();
                proof["metadata"]["metadata"]["id"] = "other-asset".into();
                proof["metadata"]["functional_profile"]["external_id"] = "other-asset".into();
                proof["metadata"]["source_catalog_entry"]["id"] = "other-asset".into();
                for (target, source) in [
                    ("selected_details_sha256", "metadata"),
                    ("selected_profile_sha256", "functional_profile"),
                    ("selected_catalog_entry_sha256", "source_catalog_entry"),
                ] {
                    proof[target] = forge_core::canonical_json_sha256(&proof["metadata"][source])
                        .unwrap()
                        .to_string()
                        .into();
                }
            }
            _ => {
                proof["metadata"]["functional_profile"]["source_physics_package"]["sha256"] =
                    bytes_sha256(b"another package").to_string().into();
                proof["selected_profile_sha256"] =
                    forge_core::canonical_json_sha256(&proof["metadata"]["functional_profile"])
                        .unwrap()
                        .to_string()
                        .into();
            }
        }
        let bytes = serde_json::to_vec(&proof).unwrap();
        let pin = bytes_sha256(&bytes);
        module.manifest.placement_companions[0].proof.sha256 = pin;
        module.replace(&path, &bytes);
        let error = orpmod::validate_directory(&module.source, false).unwrap_err();
        assert!(
            error.contains("Companion proof"),
            "unexpected refusal: {error}"
        );
    }
}

struct HiddenFixtureSource {
    original: PathBuf,
    hidden: PathBuf,
}

impl HiddenFixtureSource {
    fn new(module: &Fixture) -> Self {
        let metadata = fs::symlink_metadata(&module.source).unwrap();
        assert!(metadata.is_dir() && !metadata.file_type().is_symlink());
        #[cfg(windows)]
        {
            use std::os::windows::fs::MetadataExt;
            assert_eq!(
                metadata.file_attributes() & 0x400,
                0,
                "fixture source is a reparse point"
            );
        }
        let root = fs::canonicalize(&module.root).unwrap();
        let original = fs::canonicalize(&module.source).unwrap();
        assert_eq!(original.parent(), Some(root.as_path()));
        let hidden = root.join("temporarily-hidden-source");
        assert!(
            matches!(fs::symlink_metadata(&hidden), Err(error) if error.kind() == std::io::ErrorKind::NotFound)
        );
        fs::rename(&original, &hidden).unwrap();
        Self { original, hidden }
    }
}

impl Drop for HiddenFixtureSource {
    fn drop(&mut self) {
        match fs::symlink_metadata(&self.original) {
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
                if let Err(error) = fs::rename(&self.hidden, &self.original) {
                    eprintln!(
                        "Cannot restore fixture source {}: {error}",
                        self.original.display()
                    );
                }
            }
            _ => eprintln!(
                "Fixture source was replaced; cannot restore {}",
                self.original.display()
            ),
        }
    }
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "Keep the complete two-area export and fail-closed checks together"
)]
fn builder_exports_history_companions_for_multiple_areas_without_vault_access() {
    use forge_delivery::module_builder::{self, ExportMetadata};
    use forge_project::workspace::export::{ModuleAreaArchive, ModuleExportSnapshot};

    let module = fixture(false, false);
    let mut first = ZoneEditSession::from_archive_bytes(
        &fs::read(module.source.join("zones/start.json")).unwrap(),
    )
    .unwrap();
    let player = EntityId::deterministic("companion-game/player");
    let npc = EntityId::deterministic("companion-game/npc");
    let gameplay = serde_json::from_value(serde_json::json!({
        "schema":"forge.gameplay","version":1,"module_id":module.manifest.module_id,
        "area_id":first.head().base.terrain.area_id,"player":player,
        "profile":{"id":"forge.gate3-minimal","version":1,"interaction_range_m":3.0,
            "attack_range_m":2.0,"attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,
            "max_action_receipts":512,"max_events_per_tick":128},
        "actors":[{"id":player,"name":"Player","role":"player","position":{"x":0.,"y":1.,"z":0.},
            "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false},
            {"id":npc,"name":"Keeper","role":"friendly","position":{"x":0.,"y":1.,"z":-2.},
            "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}],
        "objects":[],"items":[],"places":[],"dialogues":[],"quests":[]
    })).unwrap();
    let request = first
        .head()
        .prepare_gameplay(
            Some(gameplay),
            CommandId::deterministic("companion-game/gameplay"),
        )
        .unwrap();
    first.execute(&request).unwrap();
    let mut retained = None;
    first
        .visit_referenced_placements(|placement| {
            if placement.physics.is_some() {
                retained = Some(placement.clone());
            }
            Ok(())
        })
        .unwrap();
    let mut retained = retained.unwrap();
    retained.id = EntityId::deterministic("companion-game/other-area");
    let mut second = ZoneEditSession::from_initial_terrain(
        new_terrain("placement-companions", "other", "Other area").unwrap(),
    )
    .unwrap();
    for (key, edit) in [
        (
            "insert",
            PlacementEdit::Insert {
                placement: retained.clone(),
            },
        ),
        ("remove", PlacementEdit::Remove { id: retained.id }),
    ] {
        let request = second
            .head()
            .prepare_placement(CommandId::deterministic(key), edit)
            .unwrap();
        second.execute(&request).unwrap();
    }
    let primary = first.head().base.terrain.area_id;
    let zones = [first, second]
        .into_iter()
        .map(|zone| ModuleAreaArchive {
            area_id: zone.head().base.terrain.area_id,
            head_hash: zone.head().content_hash().unwrap(),
            bytes: zone.archive_bytes().unwrap(),
        })
        .collect::<Vec<_>>();
    let snapshot = ModuleExportSnapshot {
        module_id: module.manifest.module_id,
        project_id: module.manifest.project_id,
        primary_area: primary,
        primary_head: zones[0].head_hash,
        workspace_hash: None,
        creator: None,
        zones,
        source_blobs: BTreeMap::new(),
    };
    let catalog: GovernedImportCatalog =
        serde_json::from_slice(&fs::read(module.source.join("assets/catalog.json")).unwrap())
            .unwrap();
    let mut payloads = BTreeMap::from([
        (
            "assets/mesh.glb".into(),
            fs::read(module.source.join("assets/mesh.glb")).unwrap(),
        ),
        (
            "LICENSE.txt".into(),
            fs::read(module.source.join("LICENSE.txt")).unwrap(),
        ),
    ]);
    for companion in &module.manifest.placement_companions {
        let (package, proof) = forge_delivery::module_placement_companions::export_paths(
            companion.kind,
            companion.pin(),
        );
        payloads.insert(
            package,
            fs::read(module.source.join(&companion.package.path)).unwrap(),
        );
        payloads.insert(
            proof,
            fs::read(module.source.join(&companion.proof.path)).unwrap(),
        );
    }
    let metadata = ExportMetadata {
        name: "Companion fixture".into(),
        version: "1.0.0".into(),
        publisher: "Forge tests".into(),
        license_text: "Original synthetic fixture".into(),
        rights: ModuleRights::PrivateLocalOnly,
    };
    let output = module.root.join("built");
    let exported = module_builder::build_module_with_origin(
        &output,
        &snapshot,
        metadata.clone(),
        catalog.clone(),
        payloads.clone(),
        None,
    )
    .unwrap();
    let admitted = orpmod::inspect(&exported.package_path, false).unwrap();
    assert_eq!(
        admitted.compatibility.module_api_version,
        MODULE_PLACEMENT_COMPANIONS_API_VERSION
    );
    assert_eq!(admitted.placement_companions.len(), 2);
    let mut expected_areas = snapshot
        .zones
        .iter()
        .map(|zone| zone.area_id)
        .collect::<Vec<_>>();
    expected_areas.sort_unstable();
    for companion in &admitted.placement_companions {
        assert_eq!(companion.areas, expected_areas);
    }
    let refused: Result<(), &str> = {
        let _hidden = HiddenFixtureSource::new(&module);
        Err("synthetic failure")
    };
    assert!(refused.is_err());
    assert!(module.source.is_dir());
    {
        let _hidden = HiddenFixtureSource::new(&module);
        assert!(!module.source.exists());
        assert!(
            forge_delivery::module_runtime::load(
                &exported.package_path,
                expected_areas[1],
                Some(&module.root.join("builder-cache"))
            )
            .unwrap()
            .reject_unhandled_placement_companions()
            .is_err()
        );
    }
    assert!(module.source.is_dir());

    let mut invalid = payloads.clone();
    let proof = invalid
        .keys()
        .find(|path| path.ends_with("/proof.json"))
        .unwrap()
        .clone();
    invalid.insert(proof, b"invalid proof".to_vec());
    let rejected = module.root.join("invalid");
    assert!(
        module_builder::build_module_with_origin(
            &rejected,
            &snapshot,
            metadata.clone(),
            catalog.clone(),
            invalid,
            None
        )
        .is_err()
    );
    assert!(!rejected.exists());
    let mut public = metadata;
    public.rights = ModuleRights::Redistributable;
    let rejected = module.root.join("public");
    assert!(
        module_builder::build_module_with_origin(
            &rejected, &snapshot, public, catalog, payloads, None
        )
        .is_err()
    );
    assert!(!rejected.exists());
}
