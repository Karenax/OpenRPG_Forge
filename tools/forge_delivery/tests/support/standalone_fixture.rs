#![allow(
    dead_code,
    reason = "shared by separate standalone integration and lock tests"
)]
#[path = "installed_fixture.rs"]
pub(crate) mod installed_fixture;

use forge_core::{AreaId, ContentHash, ModuleId, ProjectId};
use forge_delivery::{
    archive, install,
    manifest::{Descriptor, Kind, Launch, PackRef, Product, Rights, STANDALONE_PRODUCT_SCHEMA},
    module_runtime, orpmod,
};
use forge_project::{
    VisualQualityTier,
    workspace::{
        WorkspaceHead, ZoneReference,
        launcher::{
            DisplayMode, GAME_CONFIGURATION_VERSION, GameConfiguration, GameContent, GameLocation,
            PlayerDefaults,
        },
    },
};
use std::{collections::BTreeMap, fs, path::PathBuf};

#[derive(Clone)]
pub struct Game {
    pub project: ProjectId,
    pub module: ModuleId,
    pub area: AreaId,
    pub archive: PathBuf,
    pub revision: String,
    pub archive_hash: ContentHash,
    pub head_hash: ContentHash,
    pub extra_area: Option<GameArea>,
}

#[derive(Clone)]
pub struct GameArea {
    pub area: AreaId,
    pub archive_hash: ContentHash,
    pub head_hash: ContentHash,
}

pub struct Fixture {
    source: installed_fixture::Fixture,
    pub user: PathBuf,
    pub first: Game,
    pub second: Game,
}

impl Fixture {
    pub fn new() -> Self {
        let source = installed_fixture::Fixture::new();
        let state = install::state(&source.install).unwrap();
        let game = |entry: &installed_fixture::ModuleFixture, key: &str| {
            let loaded =
                module_runtime::load(&source.module_root(entry.id), entry.area, None).unwrap();
            Game {
                project: loaded.manifest.project_id,
                module: entry.id,
                area: entry.area,
                archive: source.root.join(format!("{key}.orpmod")),
                revision: state.active.packs[&entry.id.to_string()].revision.clone(),
                archive_hash: loaded.manifest.zones[0].archive_sha256,
                head_hash: loaded.manifest.zones[0].head_hash,
                extra_area: None,
            }
        };
        let first = game(&source.first, "harbor");
        let second = game(&source.second, "mountain");
        let user = source.root.join("shared-user-data");
        Self {
            source,
            user,
            first,
            second,
        }
    }

    pub fn root(&self) -> &std::path::Path {
        &self.source.root
    }

    pub fn legacy_installation(&self) -> &std::path::Path {
        &self.source.install
    }

    pub fn installation(&self, name: &str) -> PathBuf {
        self.root().join(format!("installation-{name}"))
    }

    /// Produce a small app package with a real derived project manifest and
    /// inert native entries. No executable is started by this fixture.
    pub fn application(&self, game: &Game, name: &str, title: &str, malformed: bool) -> PathBuf {
        self.application_at(game, name, title, malformed, game.area)
    }

    pub fn application_at(
        &self,
        game: &Game,
        name: &str,
        title: &str,
        malformed: bool,
        entry: AreaId,
    ) -> PathBuf {
        self.application_at_with_quality(
            game,
            name,
            title,
            malformed,
            entry,
            VisualQualityTier::High,
        )
    }

    pub fn application_at_with_quality(
        &self,
        game: &Game,
        name: &str,
        title: &str,
        malformed: bool,
        entry: AreaId,
        quality: VisualQualityTier,
    ) -> PathBuf {
        self.application_at_with_defaults(
            game,
            name,
            title,
            malformed,
            entry,
            (quality, DisplayMode::Borderless),
        )
    }

    pub fn application_at_with_defaults(
        &self,
        game: &Game,
        name: &str,
        title: &str,
        malformed: bool,
        entry: AreaId,
        defaults: (VisualQualityTier, DisplayMode),
    ) -> PathBuf {
        let mut head = WorkspaceHead::default()
            .with_project(game.project, format!("Projet {name}"))
            .unwrap()
            .with_module(game.project, game.module, format!("Module {name}"))
            .unwrap();
        head.projects[0].modules[0].zones.push(ZoneReference {
            area_id: game.area,
            archive_hash: game.archive_hash,
            head_hash: game.head_hash,
        });
        let mut areas = vec![game.area];
        if let Some(extra) = &game.extra_area {
            head.projects[0].modules[0].zones.push(ZoneReference {
                area_id: extra.area,
                archive_hash: extra.archive_hash,
                head_hash: extra.head_hash,
            });
            areas.push(extra.area);
        }
        let configuration = GameConfiguration {
            version: GAME_CONFIGURATION_VERSION,
            title: title.into(),
            summary: String::new(),
            icon: None,
            logo: None,
            background: None,
            contents: vec![GameContent {
                module_id: game.module,
                areas,
            }],
            entry: GameLocation {
                module_id: game.module,
                area_id: entry,
            },
            defaults: PlayerDefaults {
                quality: defaults.0,
                display_mode: defaults.1,
                master_volume: 80,
                music_volume: 60,
                reduce_motion: false,
                subtitles: true,
            },
        };
        let head = head
            .with_game_configuration(game.project, None, Some(configuration))
            .unwrap();
        let manifest = if malformed {
            b"{}".to_vec()
        } else {
            serde_json::to_vec(&head.derive_game_manifest(game.project).unwrap()).unwrap()
        };
        let source = self.root().join(format!("standalone-source-{name}"));
        fs::create_dir_all(source.join("game")).unwrap();
        fs::write(source.join("game/launcher.json"), manifest).unwrap();
        fs::write(source.join("launcher.bin"), b"inert launcher fixture").unwrap();
        fs::write(source.join("module.bin"), b"inert runtime fixture").unwrap();
        fs::write(source.join("LICENSE.txt"), b"Original fixture CC0-1.0").unwrap();
        let recipe = |executable: &str| Launch {
            executable: executable.into(),
            args: if executable == "launcher.bin" {
                vec!["--root".into(), "{install}".into()]
            } else {
                Vec::new()
            },
            env: BTreeMap::new(),
            clear_env_prefixes: Vec::new(),
        };
        let product = Product {
            schema: STANDALONE_PRODUCT_SCHEMA.into(),
            id: game.project.to_string(),
            title: title.into(),
            apps: BTreeMap::from([
                ("launcher".into(), recipe("launcher.bin")),
                ("module-game".into(), recipe("module.bin")),
            ]),
            required_packs: vec![PackRef {
                id: game.module.to_string(),
                manifest_sha256: game.revision.clone(),
                kind: Kind::Module,
            }],
            optional_packs: Vec::new(),
            authoring_seed: None,
        };
        let descriptor = Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: format!("game-{}", game.project),
            label: title.into(),
            version: format!("fixture-{name}"),
            kind: Kind::Applications,
            platform: "any".into(),
            source_revision: "workspace-fixture".into(),
            rights: Rights::PrivateLocalOnly,
            license_paths: vec!["LICENSE.txt".into()],
            files: Vec::new(),
            product: Some(product),
        };
        let output = self.root().join(format!("standalone-{name}.forgepack"));
        archive::pack(&source, descriptor, &output).unwrap();
        output
    }

    /// Build a genuine second area under the same project and module.
    pub fn with_extra_area(&self, game: &Game) -> Game {
        use forge_project::{
            authoring::new_terrain,
            module_package::{ModuleFile, ModuleFileRole, ModulePackageManifest, ModuleZone},
            zone_authoring::session::ZoneEditSession,
        };
        let stem = game.archive.file_stem().unwrap().to_str().unwrap();
        let source = self.root().join(stem);
        let name = format!("{}Crique supérieure{}", " ".repeat(4096), " ".repeat(4096));
        let terrain = new_terrain(stem, "upper", &name).unwrap();
        assert_eq!(terrain.project_id, game.project);
        let area = terrain.area_id;
        let zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
        let bytes = zone.archive_bytes().unwrap();
        let archive_hash = forge_core::bytes_sha256(&bytes);
        let head_hash = zone.head().content_hash().unwrap();
        let path = "zones/upper.json";
        fs::write(source.join(path), &bytes).unwrap();
        let manifest_path = source.join("module.json");
        let mut manifest =
            ModulePackageManifest::from_bytes(&fs::read(&manifest_path).unwrap()).unwrap();
        manifest.module_version = "2.0.0-two-areas".into();
        manifest.files.push(ModuleFile {
            path: path.into(),
            bytes: bytes.len() as u64,
            sha256: archive_hash,
            role: ModuleFileRole::ZoneArchive,
        });
        manifest.zones.push(ModuleZone {
            area_id: area,
            archive_path: path.into(),
            archive_sha256: archive_hash,
            head_hash,
            gameplay_sha256: None,
            source_blobs: vec![],
        });
        fs::write(&manifest_path, manifest.to_bytes().unwrap()).unwrap();
        let (mut descriptor, _) = archive::inspect(&game.archive).unwrap();
        descriptor.version.clone_from(&manifest.module_version);
        let archive = self.root().join("harbor-two-areas.orpmod");
        let (_, revision) = orpmod::pack(&source, descriptor, &archive, false).unwrap();
        let mut game = game.clone();
        game.archive = archive;
        game.revision = revision;
        game.extra_area = Some(GameArea {
            area,
            archive_hash,
            head_hash,
        });
        game
    }

    /// A real authored gameplay document for end-to-end save/resume tests.
    pub fn with_gameplay(&self, game: &Game) -> Game {
        use forge_core::{CommandId, EntityId, Vec3, canonical_json_sha256};
        use forge_gameplay::{ActorDefinition, ActorRole, GameplayDocument, RulesProfile};
        use forge_project::{
            module_package::ModulePackageManifest,
            zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
        };
        assert!(game.extra_area.is_none());
        let stem = game.archive.file_stem().unwrap().to_str().unwrap();
        let source = self.root().join(stem);
        let zone_path = source.join("zones/start.json");
        let mut zone = ZoneEditSession::from_archive_bytes(&fs::read(&zone_path).unwrap()).unwrap();
        let player = EntityId::deterministic("standalone/fixture-player");
        let document = GameplayDocument {
            narrative: None,
            factions: None,
            simulation: None,
            scripting: None,
            cinematics: None,
            creator: None,
            schema: forge_gameplay::GAMEPLAY_SCHEMA.into(),
            version: forge_gameplay::GAMEPLAY_VERSION,
            module_id: game.module,
            area_id: game.area,
            player,
            profile: RulesProfile {
                id: "forge.gate3-minimal".into(),
                version: 1,
                interaction_range_m: 3.0,
                attack_range_m: 2.0,
                attack_damage: 20,
                attack_stamina_cost: 5,
                max_stamina: 100,
                max_action_receipts: 512,
                max_events_per_tick: 128,
            },
            actors: vec![ActorDefinition {
                id: player,
                name: "Joueur".into(),
                role: ActorRole::Player,
                position: Vec3::new(0.0, 1.0, 0.0),
                max_vitality: 100,
                protection: 0,
                inventory_capacity: 12,
                dialogue: None,
                patrol: vec![],
                flees_danger: false,
            }],
            objects: vec![],
            items: vec![],
            places: vec![],
            dialogues: vec![],
            quests: vec![],
        };
        let mut corrections = zone.head().corrections.clone();
        corrections.gameplay = Some(Box::new(document.clone()));
        zone.execute(&ZoneRequest {
            command_id: CommandId::deterministic("standalone/fixture-gameplay"),
            expected_revision: zone.head().revision,
            expected_hash: zone.head().content_hash().unwrap(),
            action: ZoneAction::edit(corrections),
        })
        .unwrap();
        let bytes = zone.archive_bytes().unwrap();
        let archive_hash = forge_core::bytes_sha256(&bytes);
        let head_hash = zone.head().content_hash().unwrap();
        fs::write(&zone_path, &bytes).unwrap();
        let manifest_path = source.join("module.json");
        let mut manifest =
            ModulePackageManifest::from_bytes(&fs::read(&manifest_path).unwrap()).unwrap();
        manifest.module_version = "2.0.0-gameplay".into();
        let zone_entry = manifest
            .zones
            .iter_mut()
            .find(|entry| entry.area_id == game.area)
            .unwrap();
        zone_entry.archive_sha256 = archive_hash;
        zone_entry.head_hash = head_hash;
        zone_entry.gameplay_sha256 = Some(canonical_json_sha256(&document).unwrap());
        let file = manifest
            .files
            .iter_mut()
            .find(|file| file.path == "zones/start.json")
            .unwrap();
        file.bytes = bytes.len() as u64;
        file.sha256 = archive_hash;
        fs::write(&manifest_path, manifest.to_bytes().unwrap()).unwrap();
        let (mut descriptor, _) = archive::inspect(&game.archive).unwrap();
        descriptor.version.clone_from(&manifest.module_version);
        let archive = self.root().join("harbor-gameplay.orpmod");
        let (_, revision) = orpmod::pack(&source, descriptor, &archive, false).unwrap();
        let mut game = game.clone();
        game.archive = archive;
        game.revision = revision;
        game.archive_hash = archive_hash;
        game.head_hash = head_hash;
        game
    }

    pub fn prepare_new(&self, game: &Game, name: &str, title: &str) -> install::Plan {
        let application = self.application(game, name, title, false);
        let mut plan = install::prepare(
            &self.installation(name),
            &[application, game.archive.clone()],
        )
        .unwrap();
        // Match the existing isolated-plan convention without mutating process environment.
        plan.user_root = self.user.to_str().unwrap().into();
        plan.command_id.clear();
        plan.command_id = forge_delivery::hash(&forge_delivery::json(&plan).unwrap());
        plan
    }

    /// Build another valid immutable revision of the same module for upgrade/rollback.
    pub fn upgraded_module(&self, game: &Game, name: &str) -> Game {
        let stem = game.archive.file_stem().unwrap().to_str().unwrap();
        let source = self.root().join(stem);
        let path = source.join("module.json");
        let mut document = forge_project::module_package::ModulePackageManifest::from_bytes(
            &fs::read(&path).unwrap(),
        )
        .unwrap();
        document.module_version = format!("2.0.0-{name}");
        fs::write(&path, document.to_bytes().unwrap()).unwrap();
        let (mut descriptor, _) = archive::inspect(&game.archive).unwrap();
        descriptor.version.clone_from(&document.module_version);
        let archive = self.root().join(format!("upgrade-{name}.orpmod"));
        let (_, revision) = orpmod::pack(&source, descriptor, &archive, false).unwrap();
        let mut upgraded = game.clone();
        upgraded.archive = archive;
        upgraded.revision = revision;
        upgraded
    }
}
