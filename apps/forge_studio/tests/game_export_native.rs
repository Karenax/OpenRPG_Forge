#![cfg(not(target_arch = "wasm32"))]
use forge_core::{CommandId, ContentHash, ModuleId, ProjectId, bytes_sha256};
use forge_delivery::{
    game_builder::{GameKitManifest, VerifiedGameKit},
    manifest::{Entry, Rights},
};
use forge_project::{
    VisualQualityTier,
    audit::AuditStamp,
    authoring::{
        creation::{NewTerrainSettings, prepare_module_terrain},
        host::AuthoringHost,
        workspace::WorkspaceHostRequest as HostRequest,
    },
    workspace::{
        launcher::{
            DisplayMode, GAME_CONFIGURATION_VERSION, GameConfiguration, GameContent, GameLocation,
            PlayerDefaults,
        },
        session::{WorkspaceAction, WorkspaceRequest},
    },
};
use forge_studio_frontend::{
    game_export_native::export_game, game_export_protocol::GameExportRequest,
};
use std::{cell::Cell, path::PathBuf};

struct Fixture(PathBuf);
impl Fixture {
    fn new() -> Self {
        let root = std::env::temp_dir().canonicalize().unwrap();
        let path = root.join(format!(
            "forge-studio-game-export-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        std::fs::create_dir(&path).unwrap();
        Self(path.canonicalize().unwrap())
    }
}
impl Drop for Fixture {
    fn drop(&mut self) {
        assert_eq!(
            self.0.parent(),
            Some(std::env::temp_dir().canonicalize().unwrap().as_path())
        );
        std::fs::remove_dir_all(&self.0).unwrap();
    }
}

fn apply(host: &mut AuthoringHost, key: &str, action: WorkspaceAction) {
    let head = host
        .execute_workspace(HostRequest::Inspect)
        .unwrap()
        .head
        .unwrap();
    let request = WorkspaceRequest {
        command_id: CommandId::deterministic(key),
        base_revision: head.revision,
        base_hash: head.content_hash().unwrap(),
        action,
    };
    host.execute_workspace_with_audit(
        HostRequest::Apply {
            request: Box::new(request),
        },
        AuditStamp::human("native-game-export-test", "2026-11-02T20:00:00Z").unwrap(),
    )
    .unwrap();
}

fn kit_file(root: &std::path::Path, path: &str, bytes: &[u8]) -> Entry {
    let file = root.join(path);
    std::fs::create_dir_all(file.parent().unwrap()).unwrap();
    std::fs::write(file, bytes).unwrap();
    Entry {
        path: path.into(),
        bytes: bytes.len() as u64,
        sha256: bytes_sha256(bytes).to_string(),
    }
}

fn kit(root: &std::path::Path) -> Vec<u8> {
    std::fs::create_dir(root).unwrap();
    let mut files = vec![
        kit_file(root, "bin/launcher", b"fixture launcher"),
        kit_file(root, "bin/runtime", b"fixture runtime"),
        kit_file(root, "licenses/THIRD_PARTY_NOTICES.md", b"Fixture notices"),
    ];
    let index =
        serde_json::to_vec(&serde_json::json!({"schema":"forge.rust-dependency-notices.v2"}))
            .unwrap();
    let base = bytes_sha256(&index).to_string();
    files.push(kit_file(
        root,
        "licenses/rust-runtime-candidate/INDEX.json",
        &index,
    ));
    let supplement = serde_json::to_vec(
        &serde_json::json!({"schema":"forge.rust-notice-supplements.v1", "base_index_sha256":base}),
    )
    .unwrap();
    let supplement_hash = bytes_sha256(&supplement).to_string();
    files.push(kit_file(
        root,
        "licenses/rust-runtime-candidate/SUPPLEMENT-NOTICES.json",
        &supplement,
    ));
    let forge = serde_json::to_vec(&serde_json::json!({"schema":"forge.original-code-notices.v1", "base_index_sha256":base, "supplement_index_sha256":supplement_hash})).unwrap();
    files.push(kit_file(
        root,
        "licenses/rust-runtime-candidate/FORGE-ORIGINAL-NOTICES.json",
        &forge,
    ));
    let assets = serde_json::to_vec(&serde_json::json!({"schema":"forge.asset-notice-supplements.v1", "base_index_sha256":base})).unwrap();
    files.push(kit_file(
        root,
        "licenses/rust-runtime-candidate/ASSET-NOTICES.json",
        &assets,
    ));
    let license_paths = [
        "licenses/THIRD_PARTY_NOTICES.md",
        "licenses/rust-runtime-candidate/INDEX.json",
        "licenses/rust-runtime-candidate/SUPPLEMENT-NOTICES.json",
        "licenses/rust-runtime-candidate/FORGE-ORIGINAL-NOTICES.json",
        "licenses/rust-runtime-candidate/ASSET-NOTICES.json",
    ]
    .into_iter()
    .map(str::to_owned)
    .collect();
    serde_json::to_vec(&GameKitManifest {
        schema: "forge.delivery.game-kit.v1".into(),
        platform: if cfg!(target_os = "windows") {
            "windows-x64"
        } else {
            "linux-x64"
        }
        .into(),
        version: "fixture".into(),
        source_revision: bytes_sha256(b"fixture input").to_string(),
        launcher_contract: "forge.game-launcher.v2".into(),
        launcher: "bin/launcher".into(),
        module_runtime: "bin/runtime".into(),
        rights: Rights::PrivateLocalOnly,
        license_paths,
        files,
    })
    .unwrap()
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "one fixture exercises the native export boundary and its cleanup"
)]
fn frontend_cannot_supply_kit_paths_and_missing_kit_creates_no_export() {
    let fixture = Fixture::new();
    let mut host = AuthoringHost::new(&fixture.0).unwrap();
    host.execute_workspace(HostRequest::Create).unwrap();
    let project = ProjectId::deterministic("native-export-project");
    let module = ModuleId::deterministic("native-export-module");
    apply(
        &mut host,
        "native-export-project",
        WorkspaceAction::CreateProject {
            id: project,
            name: "Jeu".into(),
        },
    );
    apply(
        &mut host,
        "native-export-module",
        WorkspaceAction::CreateModule {
            project_id: project,
            id: module,
            name: "Module".into(),
        },
    );
    host.execute_workspace(HostRequest::Save).unwrap();
    let document = prepare_module_terrain(
        project,
        module,
        "native-export-area",
        "Carte",
        NewTerrainSettings::default(),
    )
    .unwrap();
    let area = document.area_id;
    let review = host
        .execute_workspace(HostRequest::ReviewNewZone {
            module_id: module,
            document: Box::new(document.clone()),
        })
        .unwrap()
        .receipt
        .unwrap();
    host.execute_workspace_with_audit(
        HostRequest::CreateNewZone {
            request: Box::new(review.request),
            document: Box::new(document),
        },
        AuditStamp::human("native-game-export-test", "2026-11-02T20:00:00Z").unwrap(),
    )
    .unwrap();
    let game = GameConfiguration {
        version: GAME_CONFIGURATION_VERSION,
        title: "Jeu".into(),
        summary: String::new(),
        icon: None,
        logo: None,
        background: None,
        contents: vec![GameContent {
            module_id: module,
            areas: vec![area],
        }],
        entry: GameLocation {
            module_id: module,
            area_id: area,
        },
        defaults: PlayerDefaults {
            quality: VisualQualityTier::High,
            display_mode: DisplayMode::Borderless,
            master_volume: 80,
            music_volume: 60,
            reduce_motion: false,
            subtitles: true,
        },
    };
    apply(
        &mut host,
        "native-export-game",
        WorkspaceAction::ConfigureGame {
            project_id: project,
            before: None,
            after: Some(Box::new(game)),
        },
    );
    host.execute_workspace(HostRequest::Save).unwrap();
    let hash = host
        .execute_workspace(HostRequest::Inspect)
        .unwrap()
        .head
        .unwrap()
        .content_hash()
        .unwrap();
    let request = GameExportRequest {
        schema_version: 1,
        project_id: project,
        expected_workspace_hash: hash,
        version: "1.0.0".into(),
        publisher: "Auteur".into(),
        license_text: "Usage local".into(),
    };
    let mut untrusted = serde_json::to_value(&request).unwrap();
    untrusted["kit_path"] = "C:/untrusted".into();
    assert!(serde_json::from_value::<GameExportRequest>(untrusted).is_err());
    let exports = fixture.0.join("exports");
    let attempted = Cell::new(false);
    let failure = export_game(
        &host,
        &exports,
        &request,
        || Err("kit indisponible".into()),
        |_, _, _, _| {
            attempted.set(true);
            Err("must not build".into())
        },
    )
    .err()
    .unwrap();
    assert!(failure.contains("kit indisponible"));
    assert!(!attempted.get());
    assert!(!exports.exists());
    let stale = GameExportRequest {
        expected_workspace_hash: ContentHash::from_bytes([42; 32]),
        ..request.clone()
    };
    assert!(
        export_game(
            &host,
            &exports,
            &stale,
            || Err("kit".into()),
            |_, _, _, _| Err("module".into())
        )
        .is_err()
    );
    assert!(!exports.exists());
    std::fs::create_dir(&exports).unwrap();
    let existing = exports.join("preexisting.orpmod");
    std::fs::write(&existing, b"existing export").unwrap();
    let kit_root = fixture.0.join("kit");
    let manifest = kit(&kit_root);
    let attempted = Cell::new(false);
    let failure = export_game(
        &host,
        &exports,
        &request,
        || {
            Ok((
                VerifiedGameKit::read(&kit_root, &manifest)?,
                bytes_sha256(&manifest),
            ))
        },
        |_, _, _, _| {
            attempted.set(true);
            Err("must not build".into())
        },
    )
    .err()
    .unwrap();
    assert!(failure.contains("définitions RPG"));
    assert!(!attempted.get());
    assert_eq!(std::fs::read(&existing).unwrap(), b"existing export");
    assert_eq!(std::fs::read_dir(&exports).unwrap().count(), 1);
}

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "one fixture exercises the saved game through package closure and revalidation"
)]
fn saved_procedural_game_builds_a_private_candidate_and_rejects_a_changed_kit() {
    use forge_assets::governed_import::{
        GOVERNED_IMPORT_SCHEMA, GOVERNED_IMPORT_VERSION, GovernedImportCatalog,
    };
    use forge_core::EntityId;
    use forge_delivery::{archive, module_builder, orpmod, standalone};
    use forge_project::workspace::file_session::FileWorkspaceSession;
    use std::collections::BTreeMap;

    let fixture = Fixture::new();
    let mut workspace = FileWorkspaceSession::create(&fixture.0).unwrap();
    let project = ProjectId::deterministic("native-positive-project");
    let module = ModuleId::deterministic("native-positive-module");
    let execute = |workspace: &mut FileWorkspaceSession, key: &str, action: WorkspaceAction| {
        let head = workspace.session().head();
        let command = WorkspaceRequest {
            command_id: CommandId::deterministic(key),
            base_revision: head.revision,
            base_hash: head.content_hash().unwrap(),
            action,
        };
        workspace.execute(&command).unwrap();
    };
    execute(
        &mut workspace,
        "native-positive-project",
        WorkspaceAction::CreateProject {
            id: project,
            name: "Jeu de test".into(),
        },
    );
    execute(
        &mut workspace,
        "native-positive-module",
        WorkspaceAction::CreateModule {
            project_id: project,
            id: module,
            name: "Module de test".into(),
        },
    );
    let mut areas = Vec::new();
    for key in ["native-positive-entry", "native-positive-rpg"] {
        let document =
            prepare_module_terrain(project, module, key, "Carte", NewTerrainSettings::default())
                .unwrap();
        areas.push(document.area_id);
        let reviewed = workspace.review_new_zone(module, document.clone()).unwrap();
        workspace
            .create_new_zone(&reviewed.request, document)
            .unwrap();
    }
    let player = EntityId::deterministic("native-positive-player");
    let gameplay = serde_json::from_value(serde_json::json!({
        "schema":"forge.gameplay", "version":1, "module_id":module, "area_id":areas[1], "player":player,
        "profile":{"id":"forge.gate3-minimal", "version":1, "interaction_range_m":3.0,
            "attack_range_m":2.0, "attack_damage":20, "attack_stamina_cost":5, "max_stamina":100,
            "max_action_receipts":512, "max_events_per_tick":128},
        "actors":[{"id":player,"name":"Player","role":"player","position":{"x":0.0,"y":0.0,"z":0.0},
            "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}],
        "objects":[],"items":[],"places":[],"dialogues":[],"quests":[]
    })).unwrap();
    let zone = workspace.read_zone(areas[1]).unwrap();
    let edit = zone
        .head()
        .prepare_gameplay(
            Some(gameplay),
            CommandId::deterministic("native-positive-gameplay"),
        )
        .unwrap();
    let (review, _) = workspace
        .preview_zone_edit(
            areas[1],
            &edit,
            CommandId::deterministic("native-positive-attach"),
        )
        .unwrap();
    workspace.apply_zone_edit(&review.request, &edit).unwrap();
    let configuration = GameConfiguration {
        version: GAME_CONFIGURATION_VERSION,
        title: "Jeu procédural".into(),
        summary: String::new(),
        icon: None,
        logo: None,
        background: None,
        contents: vec![GameContent {
            module_id: module,
            areas: areas.clone(),
        }],
        entry: GameLocation {
            module_id: module,
            area_id: areas[0],
        },
        defaults: PlayerDefaults {
            quality: VisualQualityTier::High,
            display_mode: DisplayMode::Borderless,
            master_volume: 80,
            music_volume: 60,
            reduce_motion: false,
            subtitles: true,
        },
    };
    execute(
        &mut workspace,
        "native-positive-config",
        WorkspaceAction::ConfigureGame {
            project_id: project,
            before: None,
            after: Some(Box::new(configuration)),
        },
    );
    workspace.save().unwrap();
    let hash = workspace.session().head().content_hash().unwrap();
    drop(workspace);
    let mut host = AuthoringHost::new(&fixture.0).unwrap();
    host.execute_workspace(HostRequest::Open).unwrap();
    let request = GameExportRequest {
        schema_version: 1,
        project_id: project,
        expected_workspace_hash: hash,
        version: "1.0.0".into(),
        publisher: "Auteur".into(),
        license_text: "Usage local".into(),
    };
    let kit_root = fixture.0.join("kit");
    let manifest = kit(&kit_root);
    let exports = fixture.0.join("exports");
    std::fs::create_dir(&exports).unwrap();
    let existing = exports.join("preexisting.orpmod");
    std::fs::write(&existing, b"previous export").unwrap();
    let loads = Cell::new(0);
    let build = |scratch: &std::path::Path,
                 selection: &forge_project::workspace::export::GameExportModule,
                 snapshot: &forge_project::workspace::export::ModuleExportSnapshot,
                 metadata| {
        assert!(selection.origin.is_none());
        assert_eq!(snapshot.primary_area, areas[1]);
        module_builder::build_module_with_origin(
            scratch,
            snapshot,
            metadata,
            GovernedImportCatalog {
                schema: GOVERNED_IMPORT_SCHEMA.into(),
                schema_version: GOVERNED_IMPORT_VERSION,
                assets: Vec::new(),
            },
            BTreeMap::new(),
            None,
        )
    };
    let candidate = export_game(
        &host,
        &exports,
        &request,
        || {
            loads.set(loads.get() + 1);
            Ok((
                VerifiedGameKit::read(&kit_root, &manifest)?,
                bytes_sha256(&manifest),
            ))
        },
        build,
    )
    .unwrap();
    assert_eq!(loads.get(), 2);
    assert_eq!(candidate.workspace_sha256, hash);
    assert_eq!(candidate.rights, Rights::PrivateLocalOnly);
    assert!(
        !candidate.runtime_verified
            && !candidate.installation_verified
            && !candidate.distribution_qualified
    );
    assert_eq!(candidate.modules.len(), 1);
    let module_path = candidate.directory.join(&candidate.modules[0].file);
    let module_manifest = orpmod::inspect(&module_path, false).unwrap();
    assert!(module_manifest.assets.is_empty());
    assert_eq!(module_manifest.zones.len(), 2);
    let extracted = fixture.0.join("extracted-application");
    archive::extract(
        &candidate.directory.join(candidate.application_file),
        &extracted,
        &candidate.application_manifest_sha256.to_string(),
    )
    .unwrap();
    let application = standalone::VerifiedGameApplication::read(
        &extracted,
        &candidate.application_manifest_sha256.to_string(),
    )
    .unwrap();
    assert_eq!(application.game().game_id(), project);
    assert_eq!(application.game().configuration().entry.area_id, areas[0]);
    let module = standalone::VerifiedGameModule::read_archive(&module_path).unwrap();
    standalone::validate_game_closure(&application, &[module]).unwrap();
    assert_eq!(std::fs::read(&existing).unwrap(), b"previous export");
    assert_eq!(std::fs::read_dir(&exports).unwrap().count(), 2);

    // Change an inert fixture binary only after assembly, during kit revalidation.
    let loads = Cell::new(0);
    let error = export_game(
        &host,
        &exports,
        &request,
        || {
            let count = loads.get();
            loads.set(count + 1);
            if count == 1 {
                std::fs::write(kit_root.join("bin/launcher"), b"altered fixture launcher").unwrap();
            }
            Ok((
                VerifiedGameKit::read(&kit_root, &manifest)?,
                bytes_sha256(&manifest),
            ))
        },
        build,
    )
    .unwrap_err();
    assert_eq!(loads.get(), 2);
    assert!(!error.is_empty());
    assert_eq!(std::fs::read(&existing).unwrap(), b"previous export");
    assert_eq!(std::fs::read_dir(&exports).unwrap().count(), 2);
    assert!(
        candidate
            .directory
            .join(candidate.application_file)
            .is_file()
    );
}
