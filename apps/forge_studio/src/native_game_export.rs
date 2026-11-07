//! Native kit and module resolution for a private game-package candidate.
use forge_core::{ContentHash, bytes_sha256};
use forge_delivery::game_builder::VerifiedGameKit;
use forge_studio_frontend::game_export_protocol::{GameExportRequest, GameExportResult};

fn load_kit() -> Result<(VerifiedGameKit, ContentHash), String> {
    let bytes =
        super::native_module_export::read_bundled("forge-game-export-v1.json", 8 * 1024 * 1024)?
            .ok_or("Les composants du jeu ne sont pas inclus dans cette version du Studio.")?;
    let executable = std::env::current_exe()
        .map_err(|error| format!("Chemin du Studio indisponible : {error}"))?;
    let root = executable
        .parent()
        .ok_or("Dossier du Studio indisponible")?
        .join("game-export-kit");
    let kit = VerifiedGameKit::read(&root, &bytes)
        .map_err(|error| format!("Composants du jeu indisponibles ou altérés : {error}"))?;
    Ok((kit, bytes_sha256(&bytes)))
}

pub(super) fn execute(request: &GameExportRequest) -> Result<GameExportResult, String> {
    request.validate()?;
    let root = forge_studio_frontend::launch_configuration::authoring_root()?
        .ok_or("Ouvrir un espace auteur avant l’export")?;
    super::with_authoring_host(|host| {
        let candidate = forge_studio_frontend::game_export_native::export_game_with_presentation(
            host,
            &root.join("exports"),
            request,
            load_kit,
            |scratch, selection, snapshot, metadata| {
                super::native_module_export::build_snapshot(
                    &root,
                    scratch,
                    snapshot,
                    selection.origin,
                    metadata,
                )
            },
            |game| {
                let pins: Vec<_> = forge_delivery::game_presentation::selections(game)
                    .into_iter()
                    .map(|pin| (pin.asset_id, pin.content_hash))
                    .collect();
                if pins.is_empty() {
                    return Ok(None);
                }
                super::with_asset_imports(|store| {
                    let snapshot = store.export_module_snapshot(&pins, &[], false)?;
                    Ok(Some(
                        forge_delivery::game_presentation::PresentationBundle {
                            catalog: snapshot.catalog,
                            files: snapshot.files,
                        },
                    ))
                })
                .map_err(|error| format!("Visuels du jeu indisponibles : {error}"))
            },
        )?;
        Ok(GameExportResult {
            directory: candidate
                .directory
                .to_str()
                .ok_or("Chemin d’export non Unicode")?
                .to_owned(),
            application_sha256: candidate.application_sha256,
            module_count: candidate.modules.len(),
            workspace_sha256: candidate.workspace_sha256,
            installation_verified: candidate.installation_verified,
            distribution_qualified: candidate.distribution_qualified,
        })
    })
}

#[cfg(test)]
mod gate7_headless_export {
    use forge_core::{CommandId, ContentHash, EntityId, ModuleId, ProjectId, bytes_sha256};
    use forge_delivery::{
        game_builder::{GameCandidate, VerifiedGameKit, inspect_candidate},
        manifest,
    };
    use forge_project::{
        VisualQualityTier,
        authoring::{
            creation::{NewTerrainSettings, prepare_module_terrain},
            host::AuthoringHost,
            workspace::WorkspaceHostRequest as HostRequest,
        },
        workspace::{
            file_session::FileWorkspaceSession,
            launcher::{
                DisplayMode, GAME_CONFIGURATION_VERSION, GameConfiguration, GameContent,
                GameLocation, PlayerDefaults,
            },
            session::{WorkspaceAction, WorkspaceRequest},
        },
    };
    use forge_studio_frontend::{
        game_export_native::export_game, game_export_protocol::GameExportRequest,
    };
    use std::{
        fs,
        io::Read,
        path::{Path, PathBuf},
    };

    struct Kit {
        root: PathBuf,
        receipt_pin: ContentHash,
        source_pin: ContentHash,
    }

    fn bounded_file(path: &Path) -> Result<Vec<u8>, String> {
        const LIMIT: u64 = 8 * 1024 * 1024;
        manifest::safe_chain(path)?;
        manifest::no_links(path)?;
        let metadata = fs::metadata(path).map_err(|error| error.to_string())?;
        if !metadata.is_file() || metadata.len() == 0 || metadata.len() > LIMIT {
            return Err(format!("Invalid qualification input: {}", path.display()));
        }
        let mut bytes = Vec::new();
        fs::File::open(path)
            .map_err(|error| error.to_string())?
            .take(LIMIT + 1)
            .read_to_end(&mut bytes)
            .map_err(|error| error.to_string())?;
        if bytes.len() as u64 > LIMIT {
            return Err("Input exceeds its bound".into());
        }
        Ok(bytes)
    }

    impl Kit {
        // The external game_kit.read_assembled preflight is mandatory.
        fn load(&self) -> Result<(VerifiedGameKit, ContentHash), String> {
            let bytes = bounded_file(&self.root.join("game-kit-assembly-receipt.json"))?;
            if bytes_sha256(&bytes) != self.receipt_pin {
                return Err("Kit receipt changed".into());
            }
            let receipt: serde_json::Value =
                serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
            let source = self.source_pin.to_string();
            if receipt["schema"].as_str() != Some("forge.delivery.private-game-kit-assembly.v1")
                || receipt["status"].as_str() != Some("PRIVATE_STAGED_ONLY")
                || receipt["platform"].as_str() != Some("windows-x64")
                || receipt["source_manifest_sha256"].as_str() != Some(source.as_str())
                || receipt["source_revision"].as_str() != Some(source.as_str())
                || receipt["runtime_verified"].as_bool() != Some(false)
                || receipt["installation_verified"].as_bool() != Some(false)
                || receipt["distribution_qualified"].as_bool() != Some(false)
            {
                return Err("Unexpected private Windows kit receipt".into());
            }
            let manifest_bytes = bounded_file(&self.root.join("forge-kit/game-export-v1.json"))?;
            let hash = bytes_sha256(&manifest_bytes);
            if receipt["kit_manifest_sha256"].as_str() != Some(hash.to_string().as_str()) {
                return Err("Kit manifest differs from its receipt".into());
            }
            Ok((
                VerifiedGameKit::read(&self.root.join("game-export-kit"), &manifest_bytes)?,
                hash,
            ))
        }
    }

    fn isolated_workspace() -> Result<PathBuf, String> {
        let supplied = PathBuf::from(
            std::env::var_os("FORGE_AUTHORING_ROOT")
                .ok_or("FORGE_AUTHORING_ROOT must name a fresh qualification directory")?,
        );
        if !supplied.is_absolute() {
            return Err("Qualification root must be absolute".into());
        }
        manifest::safe_chain(&supplied)?;
        let root = fs::canonicalize(&supplied).map_err(|e| e.to_string())?;
        let workspace = Path::new(env!("CARGO_MANIFEST_DIR"))
            .parent()
            .and_then(Path::parent)
            .and_then(Path::parent)
            .ok_or("Workspace unavailable")?;
        let parent = fs::canonicalize(workspace.join(".build/tmp")).map_err(|e| e.to_string())?;
        let name = root.file_name().and_then(|n| n.to_str()).unwrap_or("");
        if root.parent() != Some(parent.as_path())
            || !name.starts_with("gate7-headless-game-export-")
            || fs::read_dir(&root)
                .map_err(|e| e.to_string())?
                .next()
                .is_some()
        {
            return Err("Use an empty named directory directly under .build/tmp".into());
        }
        Ok(root)
    }

    fn apply(workspace: &mut FileWorkspaceSession, key: &str, action: WorkspaceAction) {
        let head = workspace.session().head();
        let request = WorkspaceRequest {
            command_id: CommandId::deterministic(key),
            base_revision: head.revision,
            base_hash: head.content_hash().unwrap(),
            action,
        };
        workspace.execute(&request).unwrap();
    }

    fn make_workspace(root: &Path) -> (ProjectId, ModuleId, GameConfiguration, ContentHash) {
        let mut workspace = FileWorkspaceSession::create(root).unwrap();
        let project = ProjectId::deterministic("gate7-headless-real-kit-project");
        let module = ModuleId::deterministic("gate7-headless-real-kit-module");
        apply(
            &mut workspace,
            "gate7-headless-project",
            WorkspaceAction::CreateProject {
                id: project,
                name: "Jeu de qualification".into(),
            },
        );
        apply(
            &mut workspace,
            "gate7-headless-module",
            WorkspaceAction::CreateModule {
                project_id: project,
                id: module,
                name: "Module de qualification".into(),
            },
        );
        let terrain = prepare_module_terrain(
            project,
            module,
            "gate7-headless-area",
            "Carte de qualification",
            NewTerrainSettings::default(),
        )
        .unwrap();
        let area = terrain.area_id;
        let reviewed = workspace.review_new_zone(module, terrain.clone()).unwrap();
        workspace
            .create_new_zone(&reviewed.request, terrain)
            .unwrap();
        let player = EntityId::deterministic("gate7-headless-player");
        let gameplay = serde_json::from_value(serde_json::json!({
            "schema":"forge.gameplay", "version":1, "module_id":module, "area_id":area, "player":player,
            "profile":{"id":"forge.gate3-minimal", "version":1, "interaction_range_m":3.0,
                "attack_range_m":2.0, "attack_damage":20, "attack_stamina_cost":5, "max_stamina":100,
                "max_action_receipts":512, "max_events_per_tick":128},
            "actors":[{"id":player,"name":"Player","role":"player","position":{"x":0.0,"y":0.0,"z":0.0},
                "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}],
            "objects":[],"items":[],"places":[],"dialogues":[],"quests":[]
        })).unwrap();
        let zone = workspace.read_zone(area).unwrap();
        let edit = zone
            .head()
            .prepare_gameplay(
                Some(gameplay),
                CommandId::deterministic("gate7-headless-gameplay"),
            )
            .unwrap();
        let (review, _) = workspace
            .preview_zone_edit(
                area,
                &edit,
                CommandId::deterministic("gate7-headless-attach"),
            )
            .unwrap();
        workspace.apply_zone_edit(&review.request, &edit).unwrap();
        let configuration = GameConfiguration {
            version: GAME_CONFIGURATION_VERSION,
            title: "Jeu de qualification".into(),
            summary: "Première révision de qualification".into(),
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
            &mut workspace,
            "gate7-headless-game",
            WorkspaceAction::ConfigureGame {
                project_id: project,
                before: None,
                after: Some(Box::new(configuration.clone())),
            },
        );
        workspace.save().unwrap();
        let hash = workspace.session().head().content_hash().unwrap();
        (project, module, configuration, hash)
    }

    fn export_revision(
        root: &Path,
        kit: &Kit,
        project: ProjectId,
        module: ModuleId,
        hash: ContentHash,
        version: &str,
    ) -> GameCandidate {
        let mut host = AuthoringHost::new(root)
            .unwrap()
            .with_zone_validator(crate::validate_studio_zone);
        host.execute_workspace(HostRequest::Open).unwrap();
        let request = GameExportRequest {
            schema_version: 1,
            project_id: project,
            expected_workspace_hash: hash,
            version: version.into(),
            publisher: "Qualification locale".into(),
            license_text: "Usage privé de qualification.".into(),
        };
        let candidate = export_game(&host, &root.join("exports"), &request, || kit.load(),
            |scratch, selection, snapshot, metadata| {
                if selection.module_id != module || selection.origin.is_some() {
                    return Err("Qualification expects only its new procedural module".into());
                }
                for source in &snapshot.zones {
                    let zone = forge_project::zone_authoring::session::ZoneEditSession::from_archive_bytes(&source.bytes)?;
                    let mut has_placements = false;
                    zone.visit_referenced_placements(|_| { has_placements = true; Ok(()) })?;
                    if has_placements
                        || zone.head().corrections.gameplay_visuals.as_deref().is_some_and(|v| !v.is_empty())
                        || !zone.required_source_blobs()?.is_empty()
                    { return Err("Qualification module must not reference external assets".into()); }
                }
                crate::native_module_export::build_snapshot(root, scratch, snapshot, selection.origin, metadata)
            }).unwrap();
        let inspected = inspect_candidate(
            &candidate.directory,
            &candidate.directory.join(candidate.installer_file),
        )
        .unwrap();
        assert_eq!(inspected.game_id, project);
        assert_eq!(candidate.workspace_sha256, hash);
        assert_eq!(candidate.modules.len(), 1);
        assert_eq!(candidate.modules[0].module_id, module);
        assert!(
            !candidate.runtime_verified
                && !candidate.installation_verified
                && !candidate.distribution_qualified
        );
        candidate
    }

    #[test]
    #[ignore = "requires a separately verified real game kit and an isolated empty workspace"]
    fn exports_two_saved_revisions_with_real_kit_without_a_window() {
        let root = isolated_workspace().unwrap();
        let assembly = PathBuf::from(
            std::env::var_os("FORGE_GATE7_GAME_KIT_ASSEMBLY")
                .expect("Set the kit assembly after the external preflight"),
        );
        assert!(assembly.is_absolute());
        manifest::safe_chain(&assembly).unwrap();
        let assembly = fs::canonicalize(assembly).unwrap();
        assert!(!assembly.starts_with(&root) && !root.starts_with(&assembly));
        let kit = Kit {
            root: assembly,
            receipt_pin: ContentHash::parse(
                &std::env::var("FORGE_GATE7_GAME_KIT_RECEIPT_SHA256").unwrap(),
            )
            .unwrap(),
            source_pin: ContentHash::parse(
                &std::env::var("FORGE_GATE7_GAME_KIT_SOURCE_SHA256").unwrap(),
            )
            .unwrap(),
        };
        // Reject a missing or modified kit before creating any workspace.
        kit.load().unwrap();
        let (project, module, original, first_hash) = make_workspace(&root);
        let first = export_revision(&root, &kit, project, module, first_hash, "1.0.0");
        let mut workspace = FileWorkspaceSession::open(&root).unwrap();
        let mut updated = original.clone();
        updated.summary = "Seconde révision auteur de qualification".into();
        apply(
            &mut workspace,
            "gate7-headless-update",
            WorkspaceAction::ConfigureGame {
                project_id: project,
                before: Some(Box::new(original)),
                after: Some(Box::new(updated)),
            },
        );
        workspace.save().unwrap();
        let second_hash = workspace.session().head().content_hash().unwrap();
        drop(workspace);
        assert_ne!(first_hash, second_hash);
        let second = export_revision(&root, &kit, project, module, second_hash, "1.0.1");
        assert_ne!(first.directory, second.directory);
        assert_ne!(first.application_sha256, second.application_sha256);
        assert_ne!(
            first.application_manifest_sha256,
            second.application_manifest_sha256
        );
        assert_ne!(
            first.modules[0].package_sha256,
            second.modules[0].package_sha256
        );
        assert_ne!(
            first.modules[0].transport_manifest_sha256,
            second.modules[0].transport_manifest_sha256
        );
        assert_eq!(first.installer_sha256, second.installer_sha256);
        for candidate in [&first, &second] {
            let reopened = inspect_candidate(
                &candidate.directory,
                &candidate.directory.join(candidate.installer_file),
            )
            .unwrap();
            assert_eq!(reopened.game_id, project);
        }
        println!(
            "{}",
            serde_json::json!({
                "scope":"studio-native-backend-real-kit-isolated-workspace",
                "game_id":project, "module_id":module,
                "first":{"directory":first.directory,"workspace_sha256":first.workspace_sha256,
                    "application_sha256":first.application_sha256,"module_sha256":first.modules[0].package_sha256},
                "second":{"directory":second.directory,"workspace_sha256":second.workspace_sha256,
                    "application_sha256":second.application_sha256,"module_sha256":second.modules[0].package_sha256},
                "launcher_sha256":first.installer_sha256,
                "gui_verified":false,"installation_verified":false,"distribution_qualified":false
            })
        );
    }
}
