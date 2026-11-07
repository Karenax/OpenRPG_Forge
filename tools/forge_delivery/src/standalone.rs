//! Read-only admission of a v2 game package and its exact module closure.
use crate::{Result, archive, install, manifest, module_runtime, orpmod};
pub use forge_core::ProjectId;
use forge_core::{AreaId, ContentHash, ModuleId, bytes_sha256};
use forge_gameplay::native_storage::{self, GameStorage};
use forge_project::{
    authoring::playtest::PlaytestSnapshot, module_package::ModulePackageManifest,
    workspace::launcher::DerivedGameManifest, zone_authoring::session::ZoneEditSession,
};
use serde::{Deserialize, Serialize};
use std::{
    collections::BTreeSet,
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    path::{Path, PathBuf},
};

pub struct VerifiedGameApplication {
    descriptor: manifest::Descriptor,
    product: manifest::Product,
    game: DerivedGameManifest,
    presentation: crate::game_presentation::PresentationView,
}

pub struct VerifiedGameModule {
    module: ModulePackageManifest,
    transport_revision: ContentHash,
}

/// Player-facing projection of an admitted game, with no native paths or commands.
#[derive(Clone, Debug, Deserialize, Eq, PartialEq, Serialize)]
pub struct GameView {
    pub game_id: ProjectId,
    pub title: String,
    pub summary: String,
    #[serde(
        default,
        skip_serializing_if = "crate::game_presentation::PresentationView::is_empty"
    )]
    pub presentation: crate::game_presentation::PresentationView,
    pub entry: crate::installed_modules::ModuleSelection,
    pub modules: Vec<GameViewModule>,
}

#[derive(Clone, Debug, Deserialize, Eq, PartialEq, Serialize)]
pub struct GameViewModule {
    pub name: String,
    pub version: String,
    pub zones: Vec<GameViewZone>,
}

#[derive(Clone, Debug, Deserialize, Eq, PartialEq, Serialize)]
pub struct GameViewZone {
    pub name: String,
    pub selection: crate::installed_modules::ModuleSelection,
    pub resume: ResumeAvailability,
    /// None means the pending request could not be inspected safely.
    pub pending_resume: Option<bool>,
}

#[derive(Clone, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(tag = "status", rename_all = "kebab-case")]
pub enum ResumeAvailability {
    Absent,
    Ready { saved_hash: ContentHash },
    Unavailable { reason: String },
}

impl VerifiedGameApplication {
    /// The package and the bytes re-read below must both match the supplied revision.
    pub fn read(root: &Path, revision: &str) -> Result<Self> {
        let descriptor = archive::verify_directory(root, revision)?;
        descriptor.validate_host()?;
        let product = descriptor
            .product
            .as_ref()
            .ok_or("Application product is missing")?;
        if product.schema != manifest::STANDALONE_PRODUCT_SCHEMA {
            return Err("A standalone v2 product is required".into());
        }
        let entry = descriptor
            .files
            .iter()
            .find(|entry| entry.path == manifest::STANDALONE_GAME_MANIFEST)
            .ok_or("Game manifest is missing")?;
        if entry.bytes > forge_project::workspace::MAX_WORKSPACE_BYTES as u64 {
            return Err("Game manifest exceeds its read limit".into());
        }
        let path = manifest::confined(root, manifest::STANDALONE_GAME_MANIFEST)?;
        let mut bytes = Vec::new();
        File::open(path)
            .map_err(|error| error.to_string())?
            .take(forge_project::workspace::MAX_WORKSPACE_BYTES as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|error| error.to_string())?;
        if bytes.len() as u64 != entry.bytes || bytes_sha256(&bytes).to_string() != entry.sha256 {
            return Err("Game manifest differs from its package pin".into());
        }
        let game = DerivedGameManifest::from_bytes(&bytes).map_err(|error| error.to_string())?;
        if game.game_id().to_string() != product.id || game.configuration().title != product.title {
            return Err("Game identity or title differs from the product".into());
        }
        let presentation = crate::game_presentation::read(root, game.configuration())?;
        let product = product.clone();
        Ok(Self {
            descriptor,
            product,
            game,
            presentation,
        })
    }

    #[must_use]
    pub const fn game(&self) -> &DerivedGameManifest {
        &self.game
    }

    #[must_use]
    pub fn entry(&self) -> crate::installed_modules::ModuleSelection {
        let entry = self.game.configuration().entry;
        crate::installed_modules::ModuleSelection {
            module_id: entry.module_id,
            area_id: entry.area_id,
        }
    }

    pub(crate) const fn descriptor(&self) -> &manifest::Descriptor {
        &self.descriptor
    }

    #[must_use]
    pub fn allows(&self, module: ModuleId, area: AreaId) -> bool {
        self.game
            .configuration()
            .contents
            .iter()
            .any(|content| content.module_id == module && content.areas.contains(&area))
    }

    /// Derive a confined per-game destination without creating user data.
    pub fn save_root(&self, user: &Path) -> Result<PathBuf> {
        manifest::confined(user, self.game.save_namespace())
    }
}

impl VerifiedGameModule {
    /// Verify an archive and its transport revision without extracting it.
    /// Inspection is portable; host compatibility is checked on installation.
    pub fn read_archive(path: &Path) -> Result<Self> {
        let (module, _descriptor, revision) = orpmod::inspect_with_transport(path, false)?;
        Ok(Self {
            module,
            transport_revision: ContentHash::parse(&revision).map_err(|error| error.to_string())?,
        })
    }

    #[must_use]
    pub fn module_id(&self) -> ModuleId {
        self.module.module_id
    }

    #[must_use]
    pub const fn transport_revision(&self) -> ContentHash {
        self.transport_revision
    }

    /// `inspect_installed` verifies the transport, semantic module and governed files.
    pub fn read(root: &Path, revision: &str) -> Result<Self> {
        let transport_revision = ContentHash::parse(revision).map_err(|error| error.to_string())?;
        let module = module_runtime::inspect_installed(root, revision)?;
        // The module reader verified every payload. Re-read only the small
        // descriptor to admit this host and catch replacement of that manifest.
        let descriptor: manifest::Descriptor =
            crate::read_json(&manifest::confined(root, "package-manifest.json")?)?;
        descriptor.validate_host()?;
        if crate::hash(&crate::json(&descriptor)?) != revision {
            return Err("Module transport descriptor changed during admission".into());
        }
        Ok(Self {
            module,
            transport_revision,
        })
    }
}

/// Verify an entire prospective or active installation without changing it.
/// Call again under the installation lock immediately before native spawn.
pub fn verify_snapshot(
    root: &Path,
    snapshot: &install::Snapshot,
) -> Result<VerifiedGameApplication> {
    verified_snapshot(root, snapshot).map(|(application, _)| application)
}

fn verified_snapshot(
    root: &Path,
    snapshot: &install::Snapshot,
) -> Result<(VerifiedGameApplication, Vec<VerifiedGameModule>)> {
    if snapshot.core.kind != manifest::Kind::Applications || snapshot.packs.len() > 1024 {
        return Err("Invalid standalone installation snapshot".into());
    }
    let application = VerifiedGameApplication::read(
        &install::package_root(root, &snapshot.core)?,
        &snapshot.core.revision,
    )?;
    if application.descriptor.id != snapshot.core.id {
        return Err("Standalone application package identity differs from the installation".into());
    }
    let mut modules = Vec::with_capacity(snapshot.packs.len());
    for (id, pack) in &snapshot.packs {
        if id != &pack.id || pack.kind != manifest::Kind::Module {
            return Err("Unexpected package in standalone installation".into());
        }
        modules.push(VerifiedGameModule::read(
            &install::package_root(root, pack)?,
            &pack.revision,
        )?);
    }
    validate_game_closure(&application, &modules)?;
    Ok((application, modules))
}

/// Read one coherent, bounded v2 view while excluding concurrent installer
/// changes. Launch still revalidates the active snapshot under the same lock.
pub fn game_view(root: &Path) -> Result<GameView> {
    game_view_for(root, None)
}

/// Derive, without creating it, one player's browser profile from the verified
/// game and the installation's protected user root under the delivery lock.
pub fn game_webview_profile_for(
    root: &Path,
    expected: Option<ProjectId>,
) -> Result<Option<(ProjectId, PathBuf, String)>> {
    install::state(root)?;
    install::with_lock(root, || {
        let state = install::state(root)?;
        let product = install::product(root, &state.active)?;
        if product.schema == "forge.delivery.product.v1" && expected.is_none() {
            return Ok(None);
        }
        let application = verify_snapshot(root, &state.active)?;
        let game = application.game().game_id();
        if game.as_uuid().is_nil() || expected.is_some_and(|expected| expected != game) {
            return Err("Le jeu installé a changé. Rouvrez son lanceur.".into());
        }
        let user = Path::new(&state.user_root);
        let profile = manifest::confined(user, &format!("launcher-webview/{game}"))?;
        if profile.try_exists().map_err(|error| error.to_string())? && !profile.is_dir() {
            return Err("Le profil du jeu ne correspond pas à un dossier.".into());
        }
        Ok(Some((game, profile, state.user_root.clone())))
    })
}

/// Keep a window's user-data identity tied to the verified head, under the
/// caller's installation lock. Exact text comparison fails closed on aliases.
pub(crate) fn require_profile_root(state: &install::State, expected: Option<&str>) -> Result<()> {
    if expected.is_some_and(|expected| state.user_root != expected) {
        return Err("Le dossier de données du jeu a changé. Rouvrez son lanceur.".into());
    }
    Ok(())
}

/// Prepare a rollback only for the reviewed game and its verified previous
/// snapshot. `install::apply` subsequently refuses a changed installation head.
pub fn prepare_game_rollback(root: &Path, expected: ProjectId) -> Result<Option<install::Plan>> {
    prepare_game_rollback_for_profile(root, expected, None)
}

pub fn prepare_game_rollback_for_profile(
    root: &Path,
    expected: ProjectId,
    expected_user_root: Option<&str>,
) -> Result<Option<install::Plan>> {
    // As with the game view, do not create a missing installation when inspecting it.
    install::state(root)?;
    install::with_lock(root, || {
        let state = install::state(root)?;
        let (active, _) = verified_snapshot(root, &state.active)?;
        require_profile_root(&state, expected_user_root)?;
        if active.game().game_id() != expected {
            return Err("Le jeu installé a changé. Rouvrez son lanceur.".into());
        }
        let Some(previous) = &state.previous else {
            return Ok(None);
        };
        let (target, _) = verified_snapshot(root, previous)?;
        if target.game().game_id() != expected {
            return Err("La version précédente appartient à un autre jeu.".into());
        }
        install::prepare_rollback(root).map(Some)
    })
}

const UNINSTALL_HELPER_PREFIX: &str = ".forge-game-uninstall-";
const UNINSTALL_HELPER_RECEIPT: &str = "helper.json";
const UNINSTALL_HELPER_RECEIPT_LIMIT: u64 = 16 * 1024;

/// Legacy helpers only bind a game to its physical root, not to one lifetime
/// of that installation. Do not publish another lifetime while a helper can
/// still address it. Inspect names only; ambiguous entries are preserved.
pub(crate) fn require_no_uninstall_helpers(root: &Path) -> Result<()> {
    if !root.try_exists().map_err(|error| error.to_string())? {
        return Ok(());
    }
    for entry in fs::read_dir(root).map_err(|error| error.to_string())? {
        let entry = entry.map_err(|error| error.to_string())?;
        let name = entry.file_name();
        let name = name.to_string_lossy();
        let matches = if cfg!(windows) {
            name.get(..UNINSTALL_HELPER_PREFIX.len())
                .is_some_and(|prefix| prefix.eq_ignore_ascii_case(UNINSTALL_HELPER_PREFIX))
        } else {
            name.starts_with(UNINSTALL_HELPER_PREFIX)
        };
        if matches {
            return Err(
                "Un ancien programme de désinstallation subsiste dans ce dossier. \
                Terminez son nettoyage ou choisissez un autre dossier pour réinstaller le jeu."
                    .into(),
            );
        }
    }
    Ok(())
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
struct UninstallHelperReceipt {
    schema: String,
    installation_root: String,
    game_id: ProjectId,
    title: String,
    bytes: u64,
    sha256: String,
}

/// An operation-owned copy of the common launcher; it is not a signature.
#[derive(Clone, Debug)]
pub struct UninstallHelper {
    pub root: PathBuf,
    pub executable: PathBuf,
    pub game_id: ProjectId,
    pub title: String,
    pub executable_sha256: String,
    pub receipt_sha256: String,
}

fn uninstall_helper_name() -> &'static str {
    if cfg!(windows) {
        "Desinstaller.exe"
    } else {
        "Desinstaller"
    }
}

/// Copy the admitted launcher under the install lock to an exclusive directory.
/// Incomplete/ambiguous directories are preserved rather than overwritten.
pub fn prepare_game_uninstall_helper(root: &Path, expected: ProjectId) -> Result<UninstallHelper> {
    prepare_game_uninstall_helper_for_profile(root, expected, None)
}

pub fn prepare_game_uninstall_helper_for_profile(
    root: &Path,
    expected: ProjectId,
    expected_user_root: Option<&str>,
) -> Result<UninstallHelper> {
    install::state(root)?;
    install::with_lock(root, || {
        let state = install::state(root)?;
        let (application, _) = verified_snapshot(root, &state.active)?;
        require_profile_root(&state, expected_user_root)?;
        if application.game().game_id() != expected {
            return Err("Le jeu installé a changé. Rouvrez son lanceur.".into());
        }
        let launcher = application
            .product
            .apps
            .get("launcher")
            .ok_or("Launcher missing")?;
        let entry = application
            .descriptor
            .files
            .iter()
            .find(|entry| entry.path == launcher.executable)
            .ok_or("Launcher inventory missing")?;
        let source = manifest::confined(
            &install::package_root(root, &state.active.core)?,
            &entry.path,
        )?;
        manifest::no_links(&source)?;
        let physical_root = fs::canonicalize(root).map_err(|error| error.to_string())?;
        let directory = manifest::confined(
            root,
            &format!("{UNINSTALL_HELPER_PREFIX}{}", crate::attempt()?),
        )?;
        fs::create_dir(&directory).map_err(|error| error.to_string())?;
        let executable = directory.join(uninstall_helper_name());
        let result: Result<UninstallHelper> = (|| {
            let mut input = File::open(source).map_err(|error| error.to_string())?;
            let mut output = OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&executable)
                .map_err(|error| error.to_string())?;
            let count =
                std::io::copy(&mut input, &mut output).map_err(|error| error.to_string())?;
            output.sync_all().map_err(|error| error.to_string())?;
            drop(output);
            if count != entry.bytes || crate::file_hash(&executable)? != entry.sha256 {
                return Err("La copie du lanceur ne correspond pas au jeu.".to_owned());
            }
            #[cfg(unix)]
            {
                use std::os::unix::fs::PermissionsExt;
                fs::set_permissions(&executable, fs::Permissions::from_mode(0o755))
                    .map_err(|error| error.to_string())?;
            }
            let receipt = UninstallHelperReceipt {
                schema: "forge.delivery.game-uninstall-helper.v2".into(),
                installation_root: physical_root
                    .to_str()
                    .ok_or("Non-Unicode installation root")?
                    .to_owned(),
                game_id: expected,
                title: application.game().configuration().title.clone(),
                bytes: entry.bytes,
                sha256: entry.sha256.clone(),
            };
            let bytes = crate::json(&receipt)?;
            if bytes.len() as u64 > UNINSTALL_HELPER_RECEIPT_LIMIT {
                return Err("Reçu du programme temporaire trop long.".into());
            }
            let mut output = OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(directory.join(UNINSTALL_HELPER_RECEIPT))
                .map_err(|error| error.to_string())?;
            output
                .write_all(&bytes)
                .map_err(|error| error.to_string())?;
            output.sync_all().map_err(|error| error.to_string())?;
            Ok(UninstallHelper {
                root: physical_root,
                executable: fs::canonicalize(executable).map_err(|error| error.to_string())?,
                game_id: expected,
                title: receipt.title,
                executable_sha256: receipt.sha256,
                receipt_sha256: crate::hash(&bytes),
            })
        })();
        result.map_err(|error| {
            format!(
                "{error} Dossier temporaire conservé : {}",
                directory.display()
            )
        })
    })
}

/// Reopen a helper independently of the installation head, which may already
/// have been removed by a resumable uninstall. The service checks the game ID.
pub fn read_game_uninstall_helper(executable: &Path) -> Result<Option<UninstallHelper>> {
    let file_name = executable
        .file_name()
        .and_then(|name| name.to_str())
        .unwrap_or_default();
    let name_matches = if cfg!(windows) {
        file_name.eq_ignore_ascii_case(uninstall_helper_name())
    } else {
        file_name == uninstall_helper_name()
    };
    if !name_matches {
        return Ok(None);
    }
    let directory = executable
        .parent()
        .ok_or("Dossier du programme temporaire absent")?;
    let name = directory
        .file_name()
        .and_then(|name| name.to_str())
        .ok_or("Dossier temporaire invalide")?;
    let prefix_matches = if cfg!(windows) {
        name.get(..UNINSTALL_HELPER_PREFIX.len())
            .is_some_and(|prefix| prefix.eq_ignore_ascii_case(UNINSTALL_HELPER_PREFIX))
    } else {
        name.starts_with(UNINSTALL_HELPER_PREFIX)
    };
    if !prefix_matches {
        return Ok(None);
    }
    let root = directory.parent().ok_or("Dossier d’installation absent")?;
    if !root.is_absolute() {
        return Err("Dossier d’installation invalide".into());
    }
    manifest::safe_chain(directory)?;
    manifest::no_links(executable)?;
    let path = manifest::confined(directory, UNINSTALL_HELPER_RECEIPT)?;
    manifest::no_links(&path)?;
    let mut bytes = Vec::new();
    File::open(path)
        .map_err(|error| error.to_string())?
        .take(UNINSTALL_HELPER_RECEIPT_LIMIT + 1)
        .read_to_end(&mut bytes)
        .map_err(|error| error.to_string())?;
    if bytes.is_empty() || bytes.len() as u64 > UNINSTALL_HELPER_RECEIPT_LIMIT {
        return Err("Reçu du programme temporaire invalide.".into());
    }
    let receipt: UninstallHelperReceipt =
        serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    manifest::valid_hash(&receipt.sha256)?;
    let metadata = fs::metadata(executable).map_err(|error| error.to_string())?;
    let physical_root = fs::canonicalize(root).map_err(|error| error.to_string())?;
    if crate::json(&receipt)? != bytes
        || receipt.schema != "forge.delivery.game-uninstall-helper.v2"
        || Path::new(&receipt.installation_root) != physical_root
        || receipt.game_id.as_uuid().is_nil()
        || receipt.bytes == 0
        || receipt.bytes > manifest::MAX_FILE_BYTES
        || !metadata.is_file()
        || metadata.len() != receipt.bytes
        || crate::file_hash(executable)? != receipt.sha256
    {
        return Err("Programme temporaire modifié ou incomplet.".into());
    }
    Ok(Some(UninstallHelper {
        root: physical_root,
        executable: fs::canonicalize(executable).map_err(|error| error.to_string())?,
        game_id: receipt.game_id,
        title: receipt.title,
        executable_sha256: receipt.sha256,
        receipt_sha256: crate::hash(&bytes),
    }))
}

/// When supplied, the launcher's reviewed identity is checked under the same
/// lock as the complete view; legacy callers can continue without a pin.
pub fn game_view_for(root: &Path, expected: Option<ProjectId>) -> Result<GameView> {
    game_view_for_profile(root, expected, None)
}

pub fn game_view_for_profile(
    root: &Path,
    expected: Option<ProjectId>,
    expected_user_root: Option<&str>,
) -> Result<GameView> {
    // A read of a missing installation must not create an installation root.
    install::state(root)?;
    install::with_lock(root, || {
        let state = install::state(root)?;
        let (application, modules) = verified_snapshot(root, &state.active)?;
        require_profile_root(&state, expected_user_root)?;
        if expected.is_some_and(|id| application.game().game_id() != id) {
            return Err("Le jeu installé a changé. Rouvrez son lanceur.".into());
        }
        let configuration = application.game.configuration();
        let save_root = application.save_root(Path::new(&state.user_root))?;
        let mut projected = Vec::with_capacity(configuration.contents.len());
        for content in &configuration.contents {
            let module = modules
                .iter()
                .find(|module| module.module.module_id == content.module_id)
                .ok_or("Game module is absent after admission")?;
            let pack = state
                .active
                .packs
                .get(&content.module_id.to_string())
                .ok_or("Game module is absent from the active installation")?;
            let root = install::package_root(root, pack)?;
            let mut zones = Vec::with_capacity(content.areas.len());
            for area_id in &content.areas {
                let (name, zone) = module_runtime::admitted_zone(&root, &module.module, *area_id)?;
                let (pending_resume, resume) =
                    inspect_resume(&save_root, content.module_id, *area_id, &zone)?;
                zones.push(GameViewZone {
                    name,
                    selection: crate::installed_modules::ModuleSelection {
                        module_id: content.module_id,
                        area_id: *area_id,
                    },
                    resume,
                    pending_resume,
                });
            }
            projected.push(GameViewModule {
                name: module.module.name.clone(),
                version: module.module.module_version.clone(),
                zones,
            });
        }
        Ok(GameView {
            game_id: application.game.game_id(),
            title: configuration.title.clone(),
            summary: configuration.summary.clone(),
            presentation: application.presentation.clone(),
            entry: application.entry(),
            modules: projected,
        })
    })
}

fn inspect_resume(
    save_root: &Path,
    module: ModuleId,
    area: AreaId,
    zone: &ZoneEditSession,
) -> Result<(Option<bool>, ResumeAvailability)> {
    let directory = save_root.join(module.to_string()).join(area.to_string());
    if let Err(error) = manifest::safe_chain(&directory) {
        eprintln!("Cannot inspect saved-game directory: {error}");
        return Ok((
            None,
            ResumeAvailability::Unavailable {
                reason: "Impossible de vérifier les sauvegardes de cette carte.".into(),
            },
        ));
    }
    let pending = match native_storage::has_pending_resume(save_root, module, area) {
        Ok(value) => Some(value),
        Err(error) => {
            eprintln!("Cannot inspect saved-game request: {error}");
            return Ok((
                None,
                ResumeAvailability::Unavailable {
                    reason: "Impossible de vérifier la demande de reprise.".into(),
                },
            ));
        }
    };
    let source = zone.head().content_hash()?;
    let snapshot = PlaytestSnapshot::from_zone(zone.head(), source)?;
    let Some(document) = snapshot.gameplay() else {
        return Ok((pending, ResumeAvailability::Absent));
    };
    let checked = (|| {
        let Some(storage) = GameStorage::open_existing(save_root, document)? else {
            return Ok(None);
        };
        storage.resume_candidate(document, source, false)
    })();
    let resume = match checked {
        Ok(Some(saved_hash)) => ResumeAvailability::Ready { saved_hash },
        Ok(None) => ResumeAvailability::Absent,
        Err(error) => {
            eprintln!("Cannot validate saved game: {error}");
            ResumeAvailability::Unavailable {
                reason: "Impossible de vérifier la sauvegarde.".into(),
            }
        }
    };
    Ok((pending, resume))
}

/// Import one committed save from an installed v1 module into its exported game.
pub fn import_legacy_save(
    root: &Path,
    legacy_root: &Path,
    expected: ProjectId,
    selection: crate::installed_modules::ModuleSelection,
) -> Result<ContentHash> {
    import_legacy_save_for_profile(root, legacy_root, expected, None, selection, None)
}

pub fn import_legacy_save_for_profile(
    root: &Path,
    legacy_root: &Path,
    expected: ProjectId,
    expected_user_root: Option<&str>,
    selection: crate::installed_modules::ModuleSelection,
    reviewed_save: Option<ContentHash>,
) -> Result<ContentHash> {
    migrate_legacy_save_for_profile(
        root,
        LegacySaveSource::Installed(legacy_root),
        expected,
        expected_user_root,
        selection,
        LegacySaveAction::Import { reviewed_save },
    )
}

pub fn review_legacy_save_for_profile(
    root: &Path,
    legacy_root: &Path,
    expected: ProjectId,
    expected_user_root: Option<&str>,
    selection: crate::installed_modules::ModuleSelection,
) -> Result<ContentHash> {
    migrate_legacy_save_for_profile(
        root,
        LegacySaveSource::Installed(legacy_root),
        expected,
        expected_user_root,
        selection,
        LegacySaveAction::Review,
    )
}

/// Native source authority, selected without paths from the launcher frontend.
pub enum LegacySaveSource<'a> {
    Installed(&'a Path),
    RecoveredProfile(&'a Path),
}

pub enum LegacySaveAction {
    Review,
    Import { reviewed_save: Option<ContentHash> },
}

pub fn migrate_legacy_save_for_profile(
    root: &Path,
    legacy: LegacySaveSource<'_>,
    expected: ProjectId,
    expected_user_root: Option<&str>,
    selection: crate::installed_modules::ModuleSelection,
    action: LegacySaveAction,
) -> Result<ContentHash> {
    install::state(root)?;
    install::with_lock(root, || {
        let state = install::state(root)?;
        require_profile_root(&state, expected_user_root)?;
        let (application, modules) = verified_snapshot(root, &state.active)?;
        if application.game().game_id() != expected
            || !application.allows(selection.module_id, selection.area_id)
        {
            return Err("Cette carte n’appartient pas au jeu sélectionné.".into());
        }
        let key = selection.module_id.to_string();
        let module = modules
            .iter()
            .find(|module| module.module.module_id == selection.module_id)
            .ok_or("Le module du jeu n’est pas installé.")?;
        let package = install::package_root(root, &state.active.packs[&key])?;
        let (_, zone) = module_runtime::admitted_zone(&package, &module.module, selection.area_id)?;
        let source = zone.head().content_hash()?;
        let matches_zone = |module: &ModulePackageManifest| {
            module.project_id == expected
                && module.module_id == selection.module_id
                && module
                    .zones
                    .iter()
                    .any(|zone| zone.area_id == selection.area_id && zone.head_hash == source)
        };
        let snapshot = PlaytestSnapshot::from_zone(zone.head(), source)?;
        let document = snapshot
            .gameplay()
            .ok_or("Cette carte ne contient pas de partie.")?;
        let target_root = application.save_root(Path::new(&state.user_root))?;
        let transfer = |legacy_user: &Path, legacy_packages: &Path| {
            let source_root = manifest::confined(legacy_user, "saves")?;
            if !install::physically_disjoint(&source_root, &target_root)? {
                return Err(
                    "Les dossiers de sauvegarde et de paquets doivent rester distincts.".into(),
                );
            }
            let slot = format!("{}/{}", selection.module_id, selection.area_id);
            for saves in [&source_root, &target_root] {
                if !install::physically_disjoint(&root.join("packages"), saves)?
                    || !install::physically_disjoint(legacy_packages, saves)?
                {
                    return Err(
                        "Les dossiers de sauvegarde et de paquets doivent rester distincts.".into(),
                    );
                }
                manifest::confined(saves, &format!("{slot}/authoring.json"))?;
                manifest::confined(saves, &format!("{slot}/authoring.lock"))?;
                manifest::confined(saves, &format!("{slot}/resume.json"))?;
            }
            match action {
                LegacySaveAction::Import { reviewed_save } => GameStorage::import_exact_save(
                    &source_root,
                    &target_root,
                    document,
                    source,
                    reviewed_save,
                ),
                LegacySaveAction::Review => {
                    if GameStorage::open_existing(&target_root, document)?.is_some()
                        || native_storage::has_pending_resume(
                            &target_root,
                            document.module_id,
                            document.area_id,
                        )?
                    {
                        return Err(
                            "Cette carte possède déjà une sauvegarde ou une reprise en attente."
                                .into(),
                        );
                    }
                    GameStorage::open_existing(&source_root, document)?
                        .ok_or("Aucune ancienne sauvegarde pour cette carte.")?
                        .resume_candidate(document, source, false)?
                        .ok_or_else(|| "Aucune ancienne sauvegarde pour cette carte.".into())
                }
            }
        };
        match legacy {
            LegacySaveSource::Installed(legacy_root) => {
                install::state(legacy_root)?;
                if !install::physically_disjoint(root, legacy_root)? {
                    return Err("Les deux installations doivent être distinctes.".into());
                }
                install::with_lock(legacy_root, || {
                    let legacy = install::state(legacy_root)?;
                    if install::product(legacy_root, &legacy.active)?.schema
                        != "forge.delivery.product.v1"
                    {
                        return Err("La source doit être une installation Forge historique.".into());
                    }
                    let pack = legacy
                        .active
                        .packs
                        .get(&key)
                        .filter(|pack| pack.kind == manifest::Kind::Module)
                        .ok_or("Le module historique n’est pas installé.")?;
                    let module = VerifiedGameModule::read(
                        &install::package_root(legacy_root, pack)?,
                        &pack.revision,
                    )?;
                    if !matches_zone(&module.module) {
                        return Err(
                            "La carte historique ne correspond pas à la version du jeu.".into()
                        );
                    }
                    transfer(Path::new(&legacy.user_root), &legacy_root.join("packages"))
                })
            }
            LegacySaveSource::RecoveredProfile(user) => {
                let recovery = manifest::confined(
                    user,
                    crate::installed_modules::RECOVERED_MODULES_DIRECTORY,
                )?;
                require_recovered_module(&recovery, &key, matches_zone)?;
                transfer(user, &recovery)
            }
        }
    })
}

fn require_recovered_module(
    directory: &Path,
    module_id: &str,
    matches_zone: impl Fn(&ModulePackageManifest) -> bool,
) -> Result<()> {
    let entries = fs::read_dir(directory).map_err(|_| "Aucun module historique récupéré.")?;
    let prefix = format!("{module_id}-");
    for (index, entry) in entries.enumerate() {
        if index >= 2048 {
            return Err("Le dossier de récupération dépasse la limite de lecture.".into());
        }
        let entry = entry.map_err(|error| error.to_string())?;
        let name = entry.file_name();
        let Some(name) = name.to_str() else { continue };
        let Some(revision) = name
            .strip_prefix(&prefix)
            .and_then(|name| name.strip_suffix(".orpmod"))
        else {
            continue;
        };
        manifest::valid_hash(revision)?;
        let path = manifest::confined(directory, name)?;
        if !path.is_file() {
            return Err("Le module récupéré doit être un fichier.".into());
        }
        let module = VerifiedGameModule::read_archive(&path)?;
        if module.transport_revision().to_string() != revision
            || module.module_id().to_string() != module_id
        {
            return Err("Le module récupéré diffère de son empreinte ou de son identité.".into());
        }
        if matches_zone(&module.module) {
            return Ok(());
        }
    }
    Err("Aucun module historique récupéré ne correspond à cette carte.".into())
}

/// A closed set of already verified module packages, without filesystem paths from the frontend.
/// Images are admitted with the application; extra asset packs remain refused.
pub fn validate_game_closure(
    application: &VerifiedGameApplication,
    modules: &[VerifiedGameModule],
) -> Result<()> {
    let game = application.game.configuration();
    if application
        .product
        .required_packs
        .iter()
        .any(|pack| pack.kind != manifest::Kind::Module)
        || modules.len() != game.contents.len()
        || application.product.required_packs.len() != game.contents.len()
    {
        return Err("Standalone module and pack closure differs".into());
    }
    let mut unique = BTreeSet::new();
    for verified in modules {
        let module = &verified.module;
        if module.project_id != application.game.game_id() || !unique.insert(module.module_id) {
            return Err("Foreign or duplicate module".into());
        }
        let selected = game
            .contents
            .iter()
            .find(|content| content.module_id == module.module_id)
            .ok_or("Module is not selected by this game")?;
        let pack = application
            .product
            .required_packs
            .iter()
            .find(|pack| pack.id == module.module_id.to_string())
            .ok_or("Selected module has no required pack")?;
        if pack.manifest_sha256 != verified.transport_revision.to_string() {
            return Err("Selected module transport revision differs".into());
        }
        let actual: BTreeSet<_> = module.zones.iter().map(|zone| zone.area_id).collect();
        let selected_areas: BTreeSet<_> = selected.areas.iter().copied().collect();
        if actual.len() != module.zones.len() || actual != selected_areas {
            return Err("Selected and delivered module areas differ".into());
        }
    }
    Ok(())
}
