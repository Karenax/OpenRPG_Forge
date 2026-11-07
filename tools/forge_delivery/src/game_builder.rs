//! Private game-package candidates built from an authenticated native kit.
//!
//! The kit manifest must come from the native Studio distribution, never from
//! a frontend path. Hashes establish byte identity, not licence qualification,
//! binary functionality, installation or redistribution rights.
use crate::{
    Result, archive,
    manifest::{self, Descriptor, Entry, Kind, Launch, PackRef, Product, Rights},
    module_builder::ExportedModule,
    standalone::{self, VerifiedGameApplication, VerifiedGameModule},
};
use forge_core::{ContentHash, ModuleId, ProjectId};
use forge_project::workspace::launcher::DerivedGameManifest;
use serde::{Deserialize, Serialize};
use std::{
    collections::{BTreeMap, BTreeSet},
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    path::{Path, PathBuf},
};

const MAX_KIT_MANIFEST_BYTES: usize = 8 * 1024 * 1024;
const MAX_CANDIDATE_BYTES: u64 = 8 * 1024 * 1024;
const MAX_NOTICE_BYTES: u64 = 16 * 1024 * 1024;
const NOTICE_ROOT: &str = "licenses/rust-runtime-candidate/";
const PROJECT_NOTICE: &str = "licenses/THIRD_PARTY_NOTICES.md";
const NOTICE_INDICES: [(&str, &str); 4] = [
    ("INDEX.json", "forge.rust-dependency-notices.v2"),
    (
        "SUPPLEMENT-NOTICES.json",
        "forge.rust-notice-supplements.v1",
    ),
    (
        "FORGE-ORIGINAL-NOTICES.json",
        "forge.original-code-notices.v1",
    ),
    ("ASSET-NOTICES.json", "forge.asset-notice-supplements.v1"),
];

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GameKitManifest {
    pub schema: String,
    pub platform: String,
    pub version: String,
    pub source_revision: String,
    pub launcher_contract: String,
    pub launcher: String,
    pub module_runtime: String,
    pub rights: Rights,
    pub license_paths: Vec<String>,
    pub files: Vec<Entry>,
}

/// All entries were checked against a manifest supplied by the native host.
pub struct VerifiedGameKit {
    root: PathBuf,
    manifest: GameKitManifest,
    manifest_sha256: ContentHash,
}

#[derive(Clone, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(deny_unknown_fields)]
pub struct GameCandidateModule {
    pub module_id: ModuleId,
    pub file: String,
    pub package_sha256: ContentHash,
    pub transport_manifest_sha256: ContentHash,
}

#[derive(Debug, Serialize)]
pub struct GameCandidate {
    pub schema: &'static str,
    pub workspace_sha256: ContentHash,
    pub kit_manifest_sha256: ContentHash,
    pub application_file: &'static str,
    pub application_sha256: ContentHash,
    pub application_manifest_sha256: ContentHash,
    pub installer_file: &'static str,
    pub installer_sha256: ContentHash,
    pub modules: Vec<GameCandidateModule>,
    pub rights: Rights,
    pub runtime_verified: bool,
    pub installation_verified: bool,
    pub distribution_qualified: bool,
    #[serde(skip)]
    pub directory: PathBuf,
}

// This is a reader of the existing export receipt, not a second game manifest.
// v1 candidates did not contain an installer and are deliberately not upgraded.
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct CandidateIndex {
    schema: String,
    workspace_sha256: ContentHash,
    kit_manifest_sha256: ContentHash,
    application_file: String,
    application_sha256: ContentHash,
    application_manifest_sha256: ContentHash,
    installer_file: String,
    installer_sha256: ContentHash,
    modules: Vec<GameCandidateModule>,
    rights: Rights,
    runtime_verified: bool,
    installation_verified: bool,
    distribution_qualified: bool,
}

/// A bounded inspection of the files next to the bootstrap executable.
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct CandidateInspection {
    pub game_id: ProjectId,
    pub title: String,
    source: PathBuf,
    bootstrap: PathBuf,
    index_sha256: String,
    archives: Vec<CandidateArchive>,
}

#[derive(Clone, Debug, Eq, PartialEq)]
struct CandidateArchive {
    path: PathBuf,
    sha256: String,
    revision: String,
    id: String,
}

/// The approved plan stays in the native process and is never supplied by the UI.
pub struct CandidateInstall {
    inspection: CandidateInspection,
    plan: crate::install::Plan,
    installed_core: Option<crate::install::InstalledPack>,
}

impl CandidateInstall {
    #[must_use]
    pub const fn inspection(&self) -> &CandidateInspection {
        &self.inspection
    }

    #[must_use]
    pub fn root(&self) -> &Path {
        Path::new(&self.plan.root)
    }

    #[must_use]
    pub fn is_update(&self) -> bool {
        self.plan.base_revision > 0
    }

    /// Reuse an already installed identical revision without creating an update.
    pub fn is_current(&self) -> Result<bool> {
        if !self.is_update() {
            return Ok(false);
        }
        let active = crate::install::verify(self.root())?.active;
        Ok(active.core == self.plan.target.core && active.packs == self.plan.target.packs)
    }

    /// Inspect the installed entry without reading the export directory again.
    pub fn player_entry_status(&self) -> Result<crate::install::PlayerEntryStatus> {
        crate::install::player_entry_status(self.root(), &self.inspection.game_id.to_string())
    }

    /// Publish or repair the durable entry from the verified installed package.
    pub fn ensure_player_entry(&self) -> Result<PathBuf> {
        crate::install::ensure_player_entry(
            self.root(),
            &self.inspection.game_id.to_string(),
            self.installed_core
                .as_ref()
                .ok_or("Revalidate the installed game first")?,
        )
    }

    /// The installed closure is sufficient; the export may have been removed.
    pub fn launch_installed(&self) -> Result<std::process::Child> {
        crate::launch::start_standalone_pinned(
            self.root(),
            self.installed_core
                .as_ref()
                .ok_or("Revalidate the installed game first")?,
        )
    }

    pub fn invalidate_installed(&mut self) {
        self.installed_core = None;
    }

    pub fn revalidate_installed(&mut self) -> Result<()> {
        let core = crate::install::verified_player_core(
            self.root(),
            &self.inspection.game_id.to_string(),
        )?;
        self.installed_core = Some(core);
        Ok(())
    }
}

const fn installer_name() -> &'static str {
    if cfg!(windows) {
        "Installer.exe"
    } else {
        "Installer"
    }
}

fn regular_file(root: &Path, name: &str, max: u64) -> Result<PathBuf> {
    let path = manifest::confined(root, name)?;
    manifest::no_links(&path)?;
    let metadata = fs::metadata(&path).map_err(|error| error.to_string())?;
    if !metadata.is_file() || metadata.len() == 0 || metadata.len() > max {
        return Err(format!("Invalid candidate file: {name}"));
    }
    Ok(path)
}

/// Inspect only a v2 export located next to the very executable being run.
/// Hashes provide consistency checks, not authenticity or distribution rights.
pub fn inspect_candidate(source: &Path, bootstrap: &Path) -> Result<CandidateInspection> {
    #[cfg(windows)]
    let source = manifest::normalized_directory(source)?;
    #[cfg(not(windows))]
    manifest::safe_chain(source)?;
    manifest::safe_chain(bootstrap)?;
    let source = fs::canonicalize(source).map_err(|error| error.to_string())?;
    // Windows may preserve a trailing separator at the root of a shared folder.
    #[cfg(windows)]
    let source = manifest::normalized_directory(&source)?;
    let bootstrap = fs::canonicalize(bootstrap).map_err(|error| error.to_string())?;
    let index_path = regular_file(&source, "candidate.json", MAX_CANDIDATE_BYTES)?;
    let mut bytes = Vec::new();
    File::open(index_path)
        .map_err(|error| error.to_string())?
        .take(MAX_CANDIDATE_BYTES + 1)
        .read_to_end(&mut bytes)
        .map_err(|error| error.to_string())?;
    if bytes.len() as u64 > MAX_CANDIDATE_BYTES {
        return Err("Candidate receipt exceeds its read limit".into());
    }
    let index: CandidateIndex =
        serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    if index.schema != "forge.delivery.private-game-candidate.v2"
        || index.application_file != "game.forgepack"
        || index.installer_file != installer_name()
        || index.rights != Rights::PrivateLocalOnly
        || index.runtime_verified
        || index.installation_verified
        || index.distribution_qualified
        || index.modules.is_empty()
        || index.modules.len() > 1023
    {
        return Err("Unsupported or incomplete game candidate".into());
    }
    // Reading this field is intentional: the receipt carries the original kit
    // pin, but it does not claim a signed chain to the player's machine.
    let _kit_manifest_sha256 = index.kit_manifest_sha256;
    let installer = regular_file(&source, &index.installer_file, manifest::MAX_FILE_BYTES)?;
    if fs::canonicalize(&installer).map_err(|error| error.to_string())? != bootstrap
        || crate::file_hash(&installer)? != index.installer_sha256.to_string()
    {
        return Err("The installer differs from the candidate executable".into());
    }
    let (game_id, title, archives) = inspect_candidate_archives(&source, &index)?;
    Ok(CandidateInspection {
        game_id,
        title,
        source,
        bootstrap,
        index_sha256: crate::hash(&bytes),
        archives,
    })
}

fn inspect_candidate_archives(
    source: &Path,
    index: &CandidateIndex,
) -> Result<(ProjectId, String, Vec<CandidateArchive>)> {
    let limit = manifest::MAX_PACK_BYTES + manifest::MAX_MANIFEST + 16;
    let application = regular_file(source, &index.application_file, limit)?;
    if crate::file_hash(&application)? != index.application_sha256.to_string() {
        return Err("Game application archive changed".into());
    }
    let (descriptor, revision) = archive::inspect(&application)?;
    descriptor.validate_host()?;
    let product = descriptor.product.as_ref().ok_or("Game product missing")?;
    if descriptor.kind != Kind::Applications
        || descriptor.rights != Rights::PrivateLocalOnly
        || descriptor.source_revision != index.workspace_sha256.to_string()
        || revision != index.application_manifest_sha256.to_string()
        || product.schema != manifest::STANDALONE_PRODUCT_SCHEMA
        || descriptor.id != format!("game-{}", product.id)
        || product.title.len() > forge_project::workspace::MAX_NAME_BYTES
    {
        return Err("Game application identity differs from its candidate".into());
    }
    let game_id: ProjectId = product.id.parse().map_err(|_| "Invalid game identity")?;
    let launcher = product
        .apps
        .get("launcher")
        .ok_or("Game launcher missing")?;
    let launcher_entry = descriptor
        .files
        .iter()
        .find(|entry| entry.path == launcher.executable)
        .ok_or("Game launcher inventory missing")?;
    if launcher_entry.sha256 != index.installer_sha256.to_string() {
        return Err("Installer and packaged launcher differ".into());
    }
    let mut archives = vec![CandidateArchive {
        path: application,
        sha256: index.application_sha256.to_string(),
        revision,
        id: descriptor.id.clone(),
    }];
    let mut ids = BTreeSet::new();
    for item in &index.modules {
        if item.file != format!("module-{}.orpmod", item.module_id) || !ids.insert(item.module_id) {
            return Err("Invalid or duplicate game module".into());
        }
        let path = regular_file(source, &item.file, limit)?;
        if crate::file_hash(&path)? != item.package_sha256.to_string() {
            return Err("Game module archive changed".into());
        }
        let admitted = VerifiedGameModule::read_archive(&path)?;
        if admitted.module_id() != item.module_id
            || admitted.transport_revision() != item.transport_manifest_sha256
            || !product.required_packs.iter().any(|pack| {
                pack.kind == Kind::Module
                    && pack.id == item.module_id.to_string()
                    && pack.manifest_sha256 == item.transport_manifest_sha256.to_string()
            })
        {
            return Err("Game module differs from its required revision".into());
        }
        archives.push(CandidateArchive {
            path,
            sha256: item.package_sha256.to_string(),
            revision: item.transport_manifest_sha256.to_string(),
            id: item.module_id.to_string(),
        });
    }
    if product.required_packs.len() != ids.len() {
        return Err("Game module closure is incomplete".into());
    }
    Ok((game_id, product.title.clone(), archives))
}

fn comparable_path(path: &Path) -> Result<String> {
    manifest::safe_chain(path)?;
    let mut existing = path;
    while !existing.try_exists().map_err(|error| error.to_string())? {
        existing = existing.parent().ok_or("Path has no existing ancestor")?;
    }
    let resolved = fs::canonicalize(existing)
        .map_err(|error| error.to_string())?
        .join(
            path.strip_prefix(existing)
                .map_err(|error| error.to_string())?,
        );
    let value = resolved
        .to_str()
        .ok_or("Non-Unicode path")?
        .replace('\\', "/");
    Ok(if cfg!(windows) {
        value.to_ascii_lowercase()
    } else {
        value
    })
}

fn overlaps(left: &str, right: &str) -> bool {
    let left = left.trim_end_matches('/');
    let right = right.trim_end_matches('/');
    left == right
        || left.starts_with(&format!("{right}/"))
        || right.starts_with(&format!("{left}/"))
}

fn validate_destination(inspection: &CandidateInspection, root: &Path) -> Result<()> {
    let destination = comparable_path(root)?;
    if overlaps(&destination, &comparable_path(&inspection.source)?)
        || overlaps(
            &destination,
            &comparable_path(&crate::launch::user_root()?)?,
        )
    {
        return Err("Installation must be separate from the candidate and user data".into());
    }
    if root.try_exists().map_err(|error| error.to_string())? {
        if !fs::metadata(root)
            .map_err(|error| error.to_string())?
            .is_dir()
        {
            return Err("Installation destination is not a directory".into());
        }
        if root
            .join("installation.json")
            .try_exists()
            .map_err(|error| error.to_string())?
        {
            let state = crate::install::verify(root)?;
            if crate::install::product(root, &state.active)?.id != inspection.game_id.to_string() {
                return Err("A different game is installed here".into());
            }
        } else {
            for item in fs::read_dir(root).map_err(|error| error.to_string())? {
                let path = item.map_err(|error| error.to_string())?.path();
                manifest::no_links(&path)?;
                if path.file_name().and_then(|name| name.to_str()) != Some("installation.lock")
                    || !fs::metadata(&path)
                        .map_err(|error| error.to_string())?
                        .is_file()
                {
                    return Err("Choose an empty directory for the first installation".into());
                }
            }
        }
    }
    Ok(())
}

/// Prepare the existing transaction after validating the reviewed archives.
pub fn prepare_candidate_install(
    source: &Path,
    bootstrap: &Path,
    root: &Path,
) -> Result<CandidateInstall> {
    let inspection = inspect_candidate(source, bootstrap)?;
    let root = manifest::normalized_directory(root)?;
    validate_destination(&inspection, &root)?;
    let paths: Vec<_> = inspection
        .archives
        .iter()
        .map(|entry| entry.path.clone())
        .collect();
    let plan = crate::install::prepare(&root, &paths)?;
    if plan.inputs.len() != inspection.archives.len()
        || plan
            .inputs
            .iter()
            .zip(&inspection.archives)
            .any(|(input, expected)| {
                Path::new(&input.archive) != expected.path.as_path()
                    || input.archive_sha256 != expected.sha256
                    || input.manifest_sha256 != expected.revision
                    || input.id != expected.id
            })
    {
        return Err("Candidate changed while preparing installation".into());
    }
    let installed_core = Some(plan.target.core.clone());
    Ok(CandidateInstall {
        inspection,
        plan,
        installed_core,
    })
}

/// Recheck the source and apply the existing transaction, which rechecks all
/// archive hashes and the current installation head under its installation lock.
pub fn apply_candidate_install(candidate: &CandidateInstall) -> Result<()> {
    if inspect_candidate(
        &candidate.inspection.source,
        &candidate.inspection.bootstrap,
    )? != candidate.inspection
    {
        return Err("Candidate changed after installation was prepared".into());
    }
    validate_destination(&candidate.inspection, candidate.root())?;
    if overlaps(
        &comparable_path(candidate.root())?,
        &comparable_path(Path::new(&candidate.plan.user_root))?,
    ) {
        return Err("Installation must be separate from user data".into());
    }
    crate::install::apply(&candidate.plan)?;
    Ok(())
}

impl VerifiedGameKit {
    /// `manifest_bytes` must be anchored in the installed Studio's own build.
    pub fn read(root: &Path, manifest_bytes: &[u8]) -> Result<Self> {
        if manifest_bytes.is_empty() || manifest_bytes.len() > MAX_KIT_MANIFEST_BYTES {
            return Err("Game kit manifest is missing or exceeds its budget".into());
        }
        manifest::safe_chain(root)?;
        manifest::no_links(root)?;
        let root = fs::canonicalize(root).map_err(|error| error.to_string())?;
        let kit: GameKitManifest =
            serde_json::from_slice(manifest_bytes).map_err(|error| error.to_string())?;
        validate_kit_shape(&kit)?;
        for entry in &kit.files {
            verify_kit_file(&root, entry)?;
        }
        reject_known_aube_launcher(&root, &kit)?;
        verify_notices(&root, &kit)?;
        Ok(Self {
            root,
            manifest: kit,
            manifest_sha256: ContentHash::parse(&crate::hash(manifest_bytes))
                .map_err(|error| error.to_string())?,
        })
    }
}

fn reject_known_aube_launcher(root: &Path, kit: &GameKitManifest) -> Result<()> {
    const AUBE_MARKER: &[u8] = b"FORGE_LAUNCHER_AUBE_FRONTEND_READY";
    let path = manifest::confined(root, &kit.launcher)?;
    let mut file = File::open(path).map_err(|error| error.to_string())?;
    let mut buffer = vec![0_u8; 64 * 1024 + AUBE_MARKER.len()];
    let mut carry = 0;
    loop {
        let read = file
            .read(&mut buffer[carry..])
            .map_err(|error| error.to_string())?;
        if read == 0 {
            break;
        }
        let size = carry + read;
        if buffer[..size]
            .windows(AUBE_MARKER.len())
            .any(|window| window == AUBE_MARKER)
        {
            return Err("The Aube reference launcher is not a generic game kit".into());
        }
        carry = size.min(AUBE_MARKER.len() - 1);
        buffer.copy_within(size - carry..size, 0);
    }
    Ok(())
}

fn validate_kit_shape(kit: &GameKitManifest) -> Result<()> {
    let compatible = (kit.platform == "windows-x64"
        && cfg!(all(target_os = "windows", target_arch = "x86_64")))
        || (kit.platform == "linux-x64" && cfg!(all(target_os = "linux", target_arch = "x86_64")));
    if kit.schema != "forge.delivery.game-kit.v1"
        || !compatible
        || kit.launcher_contract != "forge.game-launcher.v2"
        || kit.rights != Rights::PrivateLocalOnly
        || kit.version.trim().is_empty()
        || kit.version.len() > 128
        || kit.files.is_empty()
        || kit.files.len() > manifest::MAX_FILES
    {
        return Err("Unsupported, incomplete or non-private game kit".into());
    }
    manifest::valid_hash(&kit.source_revision)?;
    manifest::relative(&kit.launcher)?;
    manifest::relative(&kit.module_runtime)?;
    if kit.launcher.eq_ignore_ascii_case(&kit.module_runtime) {
        return Err("Launcher and module runtime must be separate kit files".into());
    }
    let mut names = BTreeSet::new();
    let mut total = 0_u64;
    for entry in &kit.files {
        manifest::relative(&entry.path)?;
        manifest::valid_hash(&entry.sha256)?;
        let key = entry.path.to_ascii_lowercase();
        if entry.bytes == 0
            || entry.bytes > manifest::MAX_FILE_BYTES
            || key == "package-manifest.json"
            || key == manifest::STANDALONE_GAME_MANIFEST
            || key.starts_with(".forge-storage/")
            || !names.insert(key)
        {
            return Err("Invalid or duplicate game kit file".into());
        }
        total = total
            .checked_add(entry.bytes)
            .ok_or("Game kit size overflow")?;
    }
    if total > manifest::MAX_PACK_BYTES
        || !kit.files.iter().any(|entry| entry.path == kit.launcher)
        || !kit
            .files
            .iter()
            .any(|entry| entry.path == kit.module_runtime)
    {
        return Err("Game kit binaries or size are invalid".into());
    }
    for name in &names {
        let mut parent = name.as_str();
        while let Some((prefix, _)) = parent.rsplit_once('/') {
            if names.contains(prefix) {
                return Err("Game kit file/directory conflict".into());
            }
            parent = prefix;
        }
    }
    let mut licenses = BTreeSet::new();
    for path in &kit.license_paths {
        manifest::relative(path)?;
        if !path.starts_with("licenses/")
            || !licenses.insert(path.as_str())
            || !kit.files.iter().any(|entry| entry.path == *path)
        {
            return Err("Game kit licence inventory is incomplete or duplicated".into());
        }
    }
    if !licenses.contains(PROJECT_NOTICE)
        || NOTICE_INDICES
            .iter()
            .any(|(name, _)| !licenses.contains(format!("{NOTICE_ROOT}{name}").as_str()))
    {
        return Err("Game kit project and runtime notice indices are required".into());
    }
    Ok(())
}

fn kit_entry<'a>(kit: &'a GameKitManifest, path: &str) -> Result<&'a Entry> {
    kit.files
        .iter()
        .find(|entry| entry.path == path)
        .ok_or_else(|| format!("Game kit file is missing: {path}"))
}

fn verify_kit_file(root: &Path, entry: &Entry) -> Result<()> {
    let path = manifest::confined(root, &entry.path)?;
    manifest::no_links(&path)?;
    let metadata = fs::metadata(&path).map_err(|error| error.to_string())?;
    if !metadata.is_file()
        || metadata.len() != entry.bytes
        || crate::file_hash(&path)? != entry.sha256
    {
        return Err(format!(
            "Game kit file differs from its pin: {}",
            entry.path
        ));
    }
    Ok(())
}

fn read_notice(root: &Path, entry: &Entry) -> Result<Vec<u8>> {
    if entry.bytes > MAX_NOTICE_BYTES {
        return Err("Game notice exceeds its read budget".into());
    }
    let path = manifest::confined(root, &entry.path)?;
    manifest::no_links(&path)?;
    let mut bytes = Vec::new();
    File::open(path)
        .map_err(|error| error.to_string())?
        .take(MAX_NOTICE_BYTES + 1)
        .read_to_end(&mut bytes)
        .map_err(|error| error.to_string())?;
    if bytes.len() as u64 != entry.bytes || crate::hash(&bytes) != entry.sha256 {
        return Err(format!("Game notice changed: {}", entry.path));
    }
    Ok(bytes)
}

fn verify_notices(root: &Path, kit: &GameKitManifest) -> Result<()> {
    let notice = String::from_utf8(read_notice(root, kit_entry(kit, PROJECT_NOTICE)?)?)
        .map_err(|error| error.to_string())?;
    for target in markdown_local_links(&notice)? {
        kit_entry(kit, &format!("licenses/{target}"))?;
    }
    let mut values = Vec::new();
    for (name, schema) in NOTICE_INDICES {
        let path = format!("{NOTICE_ROOT}{name}");
        let data = read_notice(root, kit_entry(kit, &path)?)?;
        let index: serde_json::Value =
            serde_json::from_slice(&data).map_err(|error| error.to_string())?;
        if index.get("schema").and_then(serde_json::Value::as_str) != Some(schema) {
            return Err(format!("Unsupported notice index: {name}"));
        }
        verify_notice_texts(kit, &index)?;
        values.push(index);
    }
    let base = &kit_entry(kit, &format!("{NOTICE_ROOT}INDEX.json"))?.sha256;
    if values[1..].iter().any(|index| {
        index
            .get("base_index_sha256")
            .and_then(serde_json::Value::as_str)
            != Some(base.as_str())
    }) || values[2]
        .get("supplement_index_sha256")
        .and_then(serde_json::Value::as_str)
        != Some(
            kit_entry(kit, &format!("{NOTICE_ROOT}SUPPLEMENT-NOTICES.json"))?
                .sha256
                .as_str(),
        )
    {
        return Err("Runtime notice indices do not match".into());
    }
    Ok(())
}

fn markdown_local_links(text: &str) -> Result<Vec<&str>> {
    let mut rest = text;
    let mut links = Vec::new();
    while let Some((_, after)) = rest.split_once("](") {
        let (target, tail) = after
            .split_once(')')
            .ok_or("Incomplete project notice link")?;
        if !target.starts_with("https://")
            && !target.starts_with("http://")
            && !target.starts_with('#')
        {
            manifest::relative(target)?;
            links.push(target);
        }
        rest = tail;
    }
    Ok(links)
}

fn verify_notice_texts(kit: &GameKitManifest, value: &serde_json::Value) -> Result<()> {
    match value {
        serde_json::Value::String(text) if text.starts_with("texts/") => {
            let entry = kit_entry(kit, &format!("{NOTICE_ROOT}{text}"))?;
            let file = text
                .strip_prefix("texts/")
                .and_then(|name| name.strip_suffix(".txt"))
                .ok_or("Invalid content-addressed notice path")?;
            if file != entry.sha256 {
                return Err("Notice text path/hash mismatch".into());
            }
        }
        serde_json::Value::Array(values) => {
            for value in values {
                verify_notice_texts(kit, value)?;
            }
        }
        serde_json::Value::Object(values) => {
            for value in values.values() {
                verify_notice_texts(kit, value)?;
            }
        }
        _ => (),
    }
    Ok(())
}

/// Assemble and read back private candidates. The caller must derive `game`
/// from its locked saved workspace and provide only native-resolved modules.
pub fn build_candidate(
    output_root: &Path,
    kit: &VerifiedGameKit,
    game: &DerivedGameManifest,
    expected_workspace: ContentHash,
    modules: &[ExportedModule],
) -> Result<GameCandidate> {
    build_candidate_with_presentation(output_root, kit, game, expected_workspace, modules, None)
}

pub fn build_candidate_with_presentation(
    output_root: &Path,
    kit: &VerifiedGameKit,
    game: &DerivedGameManifest,
    expected_workspace: ContentHash,
    modules: &[ExportedModule],
    presentation: Option<&crate::game_presentation::PresentationBundle>,
) -> Result<GameCandidate> {
    game.validate().map_err(|error| error.to_string())?;
    if game.source_workspace_hash() != expected_workspace {
        return Err("Game manifest differs from the reviewed workspace".into());
    }
    let configuration = game.configuration();
    if crate::game_presentation::selections(configuration).is_empty() != presentation.is_none() {
        return Err("Presentation selection and resolved assets differ".into());
    }
    if let Some(bundle) = presentation {
        bundle.validate(configuration)?;
    }
    let selected: BTreeSet<_> = configuration
        .contents
        .iter()
        .map(|content| content.module_id)
        .collect();
    if selected.len() != modules.len() {
        return Err("Selected game modules are incomplete".into());
    }
    let mut ids = BTreeSet::new();
    for module in modules {
        manifest::no_links(&module.package_path)?;
        if !selected.contains(&module.module_id)
            || !ids.insert(module.module_id)
            || crate::file_hash(&module.package_path)? != module.package_sha256.to_string()
        {
            return Err("Game module selection or package pin differs".into());
        }
        let verified = VerifiedGameModule::read_archive(&module.package_path)?;
        if verified.module_id() != module.module_id
            || verified.transport_revision().to_string() != module.transport_manifest_sha256
        {
            return Err("Game module transport revision differs".into());
        }
    }
    manifest::safe_chain(output_root)?;
    fs::create_dir_all(output_root).map_err(|error| error.to_string())?;
    let output_root = fs::canonicalize(output_root).map_err(|error| error.to_string())?;
    let attempt = crate::attempt()?;
    let temporary = output_root.join(format!(".game-{attempt}.staging"));
    let final_dir = output_root.join(format!("game-{attempt}"));
    fs::create_dir(&temporary).map_err(|error| error.to_string())?;
    let result = assemble(
        &temporary,
        kit,
        game,
        expected_workspace,
        modules,
        presentation,
    );
    match result {
        Ok(mut candidate) => {
            if final_dir.try_exists().map_err(|error| error.to_string())? {
                return Err("Game candidate destination already exists".into());
            }
            fs::rename(&temporary, &final_dir).map_err(|error| error.to_string())?;
            candidate.directory = final_dir;
            Ok(candidate)
        }
        Err(error) => match remove_owned_tree(&temporary) {
            Ok(()) => Err(error),
            Err(cleanup) => Err(format!(
                "{error}; temporary candidate retained at {}: {cleanup}",
                temporary.display()
            )),
        },
    }
}

fn assemble(
    temporary: &Path,
    kit: &VerifiedGameKit,
    game: &DerivedGameManifest,
    expected_workspace: ContentHash,
    modules: &[ExportedModule],
    presentation: Option<&crate::game_presentation::PresentationBundle>,
) -> Result<GameCandidate> {
    let source = temporary.join(".application-source");
    fs::create_dir(&source).map_err(|error| error.to_string())?;
    for entry in &kit.manifest.files {
        copy_kit_file(kit, entry, &source)?;
    }
    let game_path = manifest::confined(&source, manifest::STANDALONE_GAME_MANIFEST)?;
    fs::create_dir_all(game_path.parent().ok_or("Game manifest parent missing")?)
        .map_err(|error| error.to_string())?;
    write_new(&game_path, &crate::json(game)?)?;
    let mut license_paths = kit.manifest.license_paths.clone();
    if let Some(bundle) = presentation {
        license_paths.extend(bundle.write(&source, game.configuration())?);
    }
    let mut refs = Vec::with_capacity(modules.len());
    for module in modules {
        refs.push(PackRef {
            id: module.module_id.to_string(),
            manifest_sha256: module.transport_manifest_sha256.clone(),
            kind: Kind::Module,
        });
    }
    let recipe = |executable: String, args: Vec<String>| Launch {
        executable,
        args,
        env: BTreeMap::new(),
        clear_env_prefixes: Vec::new(),
    };
    let product = Product {
        schema: manifest::STANDALONE_PRODUCT_SCHEMA.into(),
        id: game.game_id().to_string(),
        title: game.configuration().title.clone(),
        apps: BTreeMap::from([
            (
                "launcher".into(),
                recipe(
                    kit.manifest.launcher.clone(),
                    vec!["--root".into(), "{install}".into()],
                ),
            ),
            (
                "module-game".into(),
                recipe(kit.manifest.module_runtime.clone(), Vec::new()),
            ),
        ]),
        required_packs: refs,
        optional_packs: Vec::new(),
        authoring_seed: None,
    };
    let descriptor = Descriptor {
        schema: "forge.delivery.package.v1".into(),
        id: format!("game-{}", game.game_id()),
        label: game.configuration().title.clone(),
        version: kit.manifest.version.clone(),
        kind: Kind::Applications,
        platform: kit.manifest.platform.clone(),
        source_revision: expected_workspace.to_string(),
        rights: Rights::PrivateLocalOnly,
        license_paths,
        files: Vec::new(),
        product: Some(product),
    };
    let application = temporary.join("game.forgepack");
    let (_, revision) = archive::pack(&source, descriptor, &application)?;
    let extracted = temporary.join(".application-verification");
    archive::extract(&application, &extracted, &revision)?;
    let verified_application = VerifiedGameApplication::read(&extracted, &revision)?;
    let (receipts, verified_modules) = copy_modules(temporary, modules)?;
    standalone::validate_game_closure(&verified_application, &verified_modules)?;
    remove_owned_tree(&source)?;
    remove_owned_tree(&extracted)?;
    let installer_file = installer_name();
    let launcher_entry = kit_entry(&kit.manifest, &kit.manifest.launcher)?;
    copy_kit_file_as(kit, launcher_entry, temporary, installer_file)?;
    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        fs::set_permissions(
            temporary.join(installer_file),
            fs::Permissions::from_mode(0o755),
        )
        .map_err(|error| error.to_string())?;
    }
    let candidate = GameCandidate {
        schema: "forge.delivery.private-game-candidate.v2",
        workspace_sha256: expected_workspace,
        kit_manifest_sha256: kit.manifest_sha256,
        application_file: "game.forgepack",
        application_sha256: ContentHash::parse(&crate::file_hash(&application)?)
            .map_err(|error| error.to_string())?,
        application_manifest_sha256: ContentHash::parse(&revision)
            .map_err(|error| error.to_string())?,
        installer_file,
        installer_sha256: ContentHash::parse(&launcher_entry.sha256)
            .map_err(|error| error.to_string())?,
        modules: receipts,
        rights: Rights::PrivateLocalOnly,
        runtime_verified: false,
        installation_verified: false,
        distribution_qualified: false,
        directory: PathBuf::new(),
    };
    write_new(&temporary.join("candidate.json"), &crate::json(&candidate)?)?;
    Ok(candidate)
}

fn copy_kit_file(kit: &VerifiedGameKit, entry: &Entry, source: &Path) -> Result<()> {
    copy_kit_file_as(kit, entry, source, &entry.path)
}

fn copy_kit_file_as(kit: &VerifiedGameKit, entry: &Entry, source: &Path, name: &str) -> Result<()> {
    let input = manifest::confined(&kit.root, &entry.path)?;
    let output = manifest::confined(source, name)?;
    fs::create_dir_all(output.parent().ok_or("Kit file parent missing")?)
        .map_err(|error| error.to_string())?;
    let mut file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output)
        .map_err(|error| error.to_string())?;
    let count = crate::vault_storage::copy_verified(
        &kit.root,
        &input,
        entry.bytes,
        &entry.sha256,
        &mut file,
    )?;
    if count != entry.bytes {
        return Err("Game kit changed while copying".into());
    }
    file.sync_all().map_err(|error| error.to_string())
}

fn copy_modules(
    temporary: &Path,
    modules: &[ExportedModule],
) -> Result<(Vec<GameCandidateModule>, Vec<VerifiedGameModule>)> {
    let mut receipts = Vec::with_capacity(modules.len());
    let mut verified = Vec::with_capacity(modules.len());
    for module in modules {
        let file = format!("module-{}.orpmod", module.module_id);
        let destination = manifest::confined(temporary, &file)?;
        let mut target = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&destination)
            .map_err(|error| error.to_string())?;
        let size = fs::metadata(&module.package_path)
            .map_err(|error| error.to_string())?
            .len();
        let root = module
            .package_path
            .parent()
            .ok_or("Module package parent missing")?;
        let count = crate::vault_storage::copy_verified(
            root,
            &module.package_path,
            size,
            &module.package_sha256.to_string(),
            &mut target,
        )?;
        if count != size {
            return Err("Module package changed while copying".into());
        }
        target.sync_all().map_err(|error| error.to_string())?;
        // The semantic reader denies concurrent write handles on Windows.
        drop(target);
        let admitted = VerifiedGameModule::read_archive(&destination)?;
        if admitted.module_id() != module.module_id
            || admitted.transport_revision().to_string() != module.transport_manifest_sha256
        {
            return Err("Copied module differs from its reviewed transport".into());
        }
        receipts.push(GameCandidateModule {
            module_id: module.module_id,
            file,
            package_sha256: module.package_sha256,
            transport_manifest_sha256: admitted.transport_revision(),
        });
        verified.push(admitted);
    }
    Ok((receipts, verified))
}

fn write_new(path: &Path, bytes: &[u8]) -> Result<()> {
    let mut file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(|error| error.to_string())?;
    file.write_all(bytes).map_err(|error| error.to_string())?;
    file.sync_all().map_err(|error| error.to_string())
}

/// Only removes a directory created by this transaction, refusing every link.
fn remove_owned_tree(root: &Path) -> Result<()> {
    manifest::no_links(root)?;
    for child in fs::read_dir(root).map_err(|error| error.to_string())? {
        let path = child.map_err(|error| error.to_string())?.path();
        manifest::no_links(&path)?;
        if fs::metadata(&path)
            .map_err(|error| error.to_string())?
            .is_dir()
        {
            remove_owned_tree(&path)?;
        } else {
            fs::remove_file(path).map_err(|error| error.to_string())?;
        }
    }
    fs::remove_dir(root).map_err(|error| error.to_string())
}
