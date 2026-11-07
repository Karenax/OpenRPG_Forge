use crate::{
    Result, archive, file_hash, hash, json,
    manifest::{self, Kind, PackRef, Product},
};
use serde::{Deserialize, Serialize};
use std::{
    collections::{BTreeMap, BTreeSet},
    fs::{self, OpenOptions},
    io::{Read, Write},
    path::{Path, PathBuf},
};

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
pub struct InstalledPack {
    pub id: String,
    pub revision: String,
    pub kind: Kind,
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
pub struct Snapshot {
    pub core: InstalledPack,
    pub packs: BTreeMap<String, InstalledPack>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Receipt {
    pub command_id: String,
    pub base_revision: u64,
    pub revision: u64,
    pub operation: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct State {
    pub schema: String,
    pub revision: u64,
    pub user_root: String,
    pub active: Snapshot,
    pub previous: Option<Snapshot>,
    pub receipts: Vec<Receipt>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Input {
    pub archive: String,
    pub archive_sha256: String,
    pub manifest_sha256: String,
    pub id: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Plan {
    pub schema: String,
    pub root: String,
    pub user_root: String,
    pub expected_head: String,
    pub base_revision: u64,
    pub command_id: String,
    pub operation: String,
    pub inputs: Vec<Input>,
    pub target: Snapshot,
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
pub struct UninstallReport {
    pub root: String,
    pub removed_files: usize,
    pub preserved_files: usize,
    pub preserved_packages: usize,
    #[serde(default, skip_serializing_if = "is_false")]
    pub counts_partial: bool,
}

fn is_false(value: &bool) -> bool {
    !value
}

pub fn package_root(root: &Path, pack: &InstalledPack) -> Result<PathBuf> {
    manifest::identifier(&pack.id)?;
    manifest::valid_hash(&pack.revision)?;
    manifest::confined(root, &format!("packages/{}/{}", pack.id, pack.revision))
}

fn head(root: &Path) -> Result<(Option<State>, String)> {
    if !root.try_exists().map_err(|e| e.to_string())? {
        return Ok((None, hash(&[])));
    }
    let path = manifest::confined(root, "installation.json")?;
    if !path.try_exists().map_err(|e| e.to_string())? {
        return Ok((None, hash(&[])));
    }
    let bytes = read_bounded(
        fs::File::open(&path).map_err(|error| error.to_string())?,
        manifest::MAX_MANIFEST,
        "Installation head",
    )?;
    let state: State = serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    if state.schema != "forge.delivery.installation.v1" || state.receipts.len() > 1024 {
        return Err("Unsupported installation head".into());
    }
    Ok((Some(state), hash(&bytes)))
}

pub fn state(root: &Path) -> Result<State> {
    ensure_no_uninstall(root)?;
    state_unchecked(root)
}

fn state_unchecked(root: &Path) -> Result<State> {
    head(root)?
        .0
        .ok_or_else(|| "Aucune installation active".into())
}

pub fn product(root: &Path, snapshot: &Snapshot) -> Result<Product> {
    let directory = package_root(root, &snapshot.core)?;
    let descriptor: manifest::Descriptor =
        crate::read_json(&manifest::confined(&directory, "package-manifest.json")?)?;
    descriptor.validate_host()?;
    if descriptor.kind != Kind::Applications
        || descriptor.id != snapshot.core.id
        || hash(&json(&descriptor)?) != snapshot.core.revision
    {
        return Err("Invalid installed product".into());
    }
    descriptor
        .product
        .ok_or_else(|| "Missing installed product".into())
}

fn dependencies(product: &Product, snapshot: &Snapshot) -> Result<()> {
    for PackRef {
        id,
        manifest_sha256,
        kind,
    } in &product.required_packs
    {
        let pack = snapshot
            .packs
            .get(id)
            .ok_or_else(|| format!("Pack requis absent : {id}"))?;
        if &pack.revision != manifest_sha256 || &pack.kind != kind {
            return Err(format!("Pack incompatible : {id}"));
        }
    }
    Ok(())
}

fn plan_id(plan: &Plan) -> Result<String> {
    let mut copy = plan.clone();
    copy.command_id.clear();
    Ok(hash(&json(&copy)?))
}

const STUDIO_LIBRARY_ID: &str = "asset-library";
const STUDIO_LIBRARY_RECEIPT: &str = ".forge-studio-library.json";
const STUDIO_ASSETS_SCHEMA: &str = "forge.delivery.retained-studio-assets.v2";
const STUDIO_RETAINED_PACKS: [&str; 2] = [STUDIO_LIBRARY_ID, manifest::STUDIO_REFERENCE_ASSETS];

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct RetainedStudioLibraryV1 {
    schema: String,
    user_root: String,
    pack: Option<InstalledPack>,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct RetainedStudioAssets {
    schema: String,
    user_root: String,
    packs: BTreeMap<String, Option<InstalledPack>>,
}

fn retained_studio_assets(root: &Path) -> Result<Option<RetainedStudioAssets>> {
    let Some(bytes) = read_uninstall_document(root, STUDIO_LIBRARY_RECEIPT)? else {
        return Ok(None);
    };
    let retained = match serde_json::from_slice::<RetainedStudioAssets>(&bytes) {
        Ok(retained) if retained.schema == STUDIO_ASSETS_SCHEMA && json(&retained)? == bytes => {
            retained
        }
        _ => {
            let old: RetainedStudioLibraryV1 =
                serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
            if old.schema != "forge.delivery.retained-studio-library.v1" || json(&old)? != bytes {
                return Err("Reçu de conservation des assets Studio invalide.".into());
            }
            RetainedStudioAssets {
                schema: STUDIO_ASSETS_SCHEMA.into(),
                user_root: old.user_root,
                packs: BTreeMap::from([(STUDIO_LIBRARY_ID.into(), old.pack)]),
            }
        }
    };
    let user = Path::new(&retained.user_root);
    if !user.is_absolute() || retained.packs.is_empty() {
        return Err("Reçu de conservation des assets Studio invalide.".into());
    }
    manifest::safe_chain(user)?;
    if !physically_disjoint(&root.join("packages"), user)? {
        return Err("Le profil recouvre les assets conservés du Studio.".into());
    }
    for (id, pack) in &retained.packs {
        if !STUDIO_RETAINED_PACKS.contains(&id.as_str()) {
            return Err("Paquet conservé du Studio invalide.".into());
        }
        let directory = manifest::confined(root, &format!("packages/{id}"))?;
        if !existing(&directory)?.is_some_and(|metadata| metadata.is_dir()) {
            return Err(format!("Paquet Studio conservé introuvable : {id}"));
        }
        if let Some(pack) = pack {
            if &pack.id != id || pack.kind != Kind::Assets {
                return Err(format!("Révision conservée du Studio invalide : {id}"));
            }
            let package = package_root(root, pack)?;
            let inventory = manifest::confined(&package, "package-manifest.json")?;
            if !existing(&inventory)?.is_some_and(|metadata| metadata.is_file()) {
                return Err(format!("Révision conservée du Studio introuvable : {id}"));
            }
        }
    }
    Ok(Some(retained))
}

/// Admit uninstall residue or the verified first activation of this candidate.
pub fn check_studio_reinstall_root(root: &Path, candidate: Option<&Path>) -> Result<()> {
    let root = manifest::normalized_directory(root)?;
    if existing(&root)?.is_none() {
        return Ok(());
    }
    let retained = retained_studio_assets(&root)?;
    let recovery = if let Some(candidate) = candidate
        && let (Some(state), _) = head(&root)?
    {
        let (descriptor, revision) = archive::inspect(candidate)?;
        if state.revision != 1
            || state.previous.is_some()
            || state.active.packs.values().any(|pack| {
                retained
                    .as_ref()
                    .and_then(|assets| assets.packs.get(&pack.id))
                    .and_then(Option::as_ref)
                    != Some(pack)
            })
            || state.active.core.id != descriptor.id
            || state.active.core.revision != revision
            || state.active.core.kind != Kind::Applications
            || !descriptor
                .product
                .is_some_and(|product| product.schema == manifest::STUDIO_PRODUCT_SCHEMA)
        {
            return Err(
                "Le dossier sans marqueur ne correspond pas à cette installation du Studio.".into(),
            );
        }
        Some(state.active)
    } else {
        None
    };
    let mut allowed_packages: Vec<_> = recovery.iter().map(|state| state.core.id.as_str()).collect();
    if let Some(retained) = &retained {
        allowed_packages.extend(retained.packs.keys().map(String::as_str));
    }
    for entry in fs::read_dir(&root).map_err(|error| error.to_string())? {
        let entry = entry.map_err(|error| error.to_string())?;
        let path = entry.path();
        manifest::no_links(&path)?;
        let metadata = entry.metadata().map_err(|error| error.to_string())?;
        match entry.file_name().to_str() {
            Some("installation.lock") if metadata.is_file() && metadata.len() == 0 => {}
            Some(STUDIO_LIBRARY_RECEIPT) if metadata.is_file() && retained.is_some() => {}
            Some("installation.json") if metadata.is_file() && recovery.is_some() => {}
            Some("packages") if metadata.is_dir() => {
                check_unmarked_package_entries(&path, &allowed_packages)?;
            }
            _ => {
                return Err(
                    "Le dossier sans marqueur Studio contient des fichiers étrangers.".into(),
                );
            }
        }
    }
    if let Some(snapshot) = recovery {
        let package = package_root(&root, &snapshot.core)?;
        let parent = package
            .parent()
            .ok_or("Dossier de paquets Studio absent.")?;
        check_unmarked_package_entries(parent, &[&snapshot.core.revision])?;
        verify_snapshot(&root, &snapshot)?;
    }
    Ok(())
}

fn check_unmarked_package_entries(directory: &Path, allowed: &[&str]) -> Result<()> {
    for entry in fs::read_dir(directory).map_err(|error| error.to_string())? {
        let name = entry.map_err(|error| error.to_string())?.file_name();
        if !allowed.iter().any(|allowed| name == *allowed) {
            return Err(
                "Le dossier de paquets sans marqueur Studio contient des fichiers étrangers."
                    .into(),
            );
        }
    }
    Ok(())
}

pub fn prepare(root: &Path, archives: &[PathBuf]) -> Result<Plan> {
    if !root.is_absolute() || archives.is_empty() || archives.len() > 1024 {
        return Err("Absolute installation root and 1..1024 packages required".into());
    }
    ensure_no_uninstall(root)?;
    let (current, expected_head) = head(root)?;
    manifest::safe_chain(root)?;
    let retained = if current.is_none() {
        crate::standalone::require_no_uninstall_helpers(root)?;
        retained_studio_assets(root)?
    } else {
        None
    };
    let user_root = match current
        .as_ref()
        .map(|state| &state.user_root)
        .or_else(|| retained.as_ref().map(|library| &library.user_root))
    {
        Some(user) => user.clone(),
        None => crate::launch::user_root()?
            .to_str()
            .ok_or("Non-Unicode user root")?
            .to_owned(),
    };
    manifest::safe_chain(Path::new(&user_root))?;
    let mut target = current.as_ref().map(|s| s.active.clone());
    let mut packs = target.as_ref().map(|s| s.packs.clone()).unwrap_or_default();
    let mut core = None;
    let mut inputs = Vec::new();
    let mut ids = std::collections::BTreeSet::new();
    let mut new_product = None;
    for path in archives {
        let path = fs::canonicalize(path).map_err(|e| e.to_string())?;
        let (descriptor, revision) = archive::inspect(&path)?;
        descriptor.validate_host()?;
        if !ids.insert(descriptor.id.clone()) {
            return Err("Duplicate package ID in command".into());
        }
        let pack = InstalledPack {
            id: descriptor.id.clone(),
            revision: revision.clone(),
            kind: descriptor.kind.clone(),
        };
        if descriptor.kind == Kind::Applications {
            if core.is_some() {
                return Err("A command cannot install two application packages".into());
            }
            core = Some(pack);
            new_product = descriptor.product;
        } else {
            packs.insert(pack.id.clone(), pack);
        }
        inputs.push(Input {
            archive: path.to_str().ok_or("Non-Unicode archive path")?.to_owned(),
            archive_sha256: file_hash(&path)?,
            manifest_sha256: revision,
            id: descriptor.id,
        });
    }
    if let Some(core) = core {
        target = Some(Snapshot { core, packs });
    } else if let Some(target) = target.as_mut() {
        target.packs = packs;
    }
    let mut target = target.ok_or("First installation requires an application package")?;
    let definition = match new_product {
        Some(p) => p,
        None => product(root, &target)?,
    };
    if let Some(retained) = retained {
        if definition.schema != manifest::STUDIO_PRODUCT_SCHEMA {
            return Err("Ce dossier conserve des paquets d’assets pour le Studio Forge.".into());
        }
        for pack in retained.packs.into_values().flatten() {
            if !definition.optional_packs.contains(&pack.id)
                || target
                    .packs
                    .get(&pack.id)
                    .is_some_and(|selected| selected != &pack)
            {
                return Err(format!("Ce candidat Studio ne reprend pas le paquet conservé : {}", pack.id));
            }
            target.packs.insert(pack.id.clone(), pack);
        }
    }
    if matches!(
        definition.schema.as_str(),
        manifest::STANDALONE_PRODUCT_SCHEMA | manifest::STUDIO_PRODUCT_SCHEMA
    ) {
        let mut admitted: BTreeSet<_> = definition
            .required_packs
            .iter()
            .map(|pack| pack.id.as_str())
            .collect();
        if definition.schema == manifest::STUDIO_PRODUCT_SCHEMA {
            admitted.extend(definition.optional_packs.iter().map(String::as_str));
        }
        // Only the new head is restricted; the prior snapshot and its immutable
        // package revisions remain available for rollback.
        target.packs.retain(|id, _| admitted.contains(id.as_str()));
        if definition.schema == manifest::STUDIO_PRODUCT_SCHEMA
            && let Some(previous) = current.as_ref().and_then(|state| state.previous.as_ref())
        {
            // A rollback to a Studio that predates the migration can hide the
            // asset packs temporarily. Reuse their pinned revisions on upgrade.
            for id in &definition.optional_packs {
                if let Some(pack) = previous.packs.get(id) {
                    target
                        .packs
                        .entry(id.clone())
                        .or_insert_with(|| pack.clone());
                }
            }
        }
        if definition.schema == manifest::STUDIO_PRODUCT_SCHEMA
            && target.packs.values().any(|pack| pack.kind != Kind::Assets)
        {
            return Err("Studio optional packs must be assets packages".into());
        }
        if inputs.iter().any(|input| {
            !(input.id == target.core.id && input.manifest_sha256 == target.core.revision)
                && !target
                    .packs
                    .get(&input.id)
                    .is_some_and(|pack| pack.revision == input.manifest_sha256)
        }) {
            return Err(
                if definition.schema == manifest::STANDALONE_PRODUCT_SCHEMA {
                    "Standalone installation contains an unselected package"
                } else {
                    "Studio installation contains an unselected package"
                }
                .into(),
            );
        }
    }
    dependencies(&definition, &target)?;
    let mut plan = Plan {
        schema: "forge.delivery.plan.v1".into(),
        root: root
            .to_str()
            .ok_or("Non-Unicode installation root")?
            .to_owned(),
        user_root,
        expected_head,
        base_revision: current.as_ref().map_or(0, |s| s.revision),
        command_id: String::new(),
        operation: "install".into(),
        inputs,
        target,
    };
    plan.command_id = plan_id(&plan)?;
    Ok(plan)
}

/// Reuse historical authoring data on first install and carry its verified
/// assets declared by the Studio candidate into the Studio installation.
pub fn prepare_studio_installer(
    root: &Path,
    archive: &Path,
    legacy_root: Option<&Path>,
) -> Result<Plan> {
    let mut plan = prepare(root, &[archive.to_path_buf()])?;
    let retained_profile = plan.base_revision == 0 && retained_studio_assets(root)?.is_some();
    let Some(legacy_root) = legacy_root else {
        return Ok(plan);
    };
    if std::env::var_os("FORGE_DELIVERY_USER_ROOT").is_some() {
        return Ok(plan);
    }
    let (candidate, _) = archive::inspect(archive)?;
    let missing_packs: Vec<_> = candidate
        .product
        .filter(|product| product.schema == manifest::STUDIO_PRODUCT_SCHEMA)
        .into_iter()
        .flat_map(|product| product.optional_packs)
        .filter(|id| !plan.target.packs.contains_key(id))
        .collect();
    if plan.base_revision != 0 && missing_packs.is_empty() {
        return Ok(plan);
    }
    let legacy_root = manifest::normalized_directory(legacy_root)?;
    ensure_no_uninstall(&legacy_root)?;
    if head(&legacy_root)?.0.is_none() {
        // Windows can retain the registration after the old folder was removed.
        return Ok(plan);
    }
    let mut staged_archives = Vec::new();
    let result = with_lock(&legacy_root, || {
        let legacy = state(&legacy_root)?;
        let product = product(&legacy_root, &legacy.active)?;
        if product.schema != "forge.delivery.product.v1"
            || product.id != manifest::STUDIO_PRODUCT_ID
        {
            return Err(
                "L’installation enregistrée n’est pas une installation Forge historique.".into(),
            );
        }
        let user = manifest::normalized_directory(Path::new(&legacy.user_root))?;
        if !existing(&user)?.is_some_and(|metadata| metadata.is_dir()) {
            return Err(
                "Le dossier utilisateur de l’installation historique est introuvable.".into(),
            );
        }
        if !physically_disjoint(root, &user)?
            || !physically_disjoint(&legacy_root.join("packages"), &user)?
        {
            return Err(
                "Le dossier utilisateur historique recouvre des fichiers d’installation.".into(),
            );
        }
        if plan.base_revision == 0 && !retained_profile {
            plan.user_root = legacy.user_root;
        }
        if manifest::normalized_directory(Path::new(&plan.user_root))? == user {
            let selected: Vec<_> = missing_packs
                .iter()
                .filter_map(|id| legacy.active.packs.get_key_value(id))
                .collect();
            if !selected.is_empty() && !physically_disjoint(root, &legacy_root)? {
                return Err("Dossiers d’installation historique et Studio confondus.".into());
            }
            for (id, pack) in selected {
                if &pack.id != id || pack.kind != Kind::Assets {
                    return Err("Paquet d’assets historique invalide.".into());
                }
                let source = package_root(&legacy_root, pack)?;
                // NSIS owns the payload directory and removes the transient archive.
                let staged = Path::new(&plan.inputs[0].archive)
                    .with_file_name(format!("studio-assets-{}.forgepack", crate::attempt()?));
                archive::export_installed(&source, &pack.revision, &staged)?;
                staged_archives.push(staged.clone());
                plan.inputs.push(Input {
                    archive: staged
                        .to_str()
                        .ok_or("Non-Unicode archive path")?
                        .to_owned(),
                    archive_sha256: file_hash(&staged)?,
                    manifest_sha256: pack.revision.clone(),
                    id: pack.id.clone(),
                });
                plan.target.packs.insert(pack.id.clone(), pack.clone());
            }
        }
        plan.command_id = plan_id(&plan)?;
        Ok(plan)
    });
    if result.is_err() {
        for staged in staged_archives {
            let _ = fs::remove_file(staged);
        }
    }
    result
}

pub fn prepare_rollback(root: &Path) -> Result<Plan> {
    ensure_no_uninstall(root)?;
    let (current, expected_head) = head(root)?;
    let current = current.ok_or("No installation to roll back")?;
    let target = current.previous.ok_or("No previous installation")?;
    let mut plan = Plan {
        schema: "forge.delivery.plan.v1".into(),
        root: root.to_str().ok_or("Non-Unicode root")?.into(),
        user_root: current.user_root.clone(),
        expected_head,
        base_revision: current.revision,
        command_id: String::new(),
        operation: "rollback".into(),
        inputs: Vec::new(),
        target,
    };
    plan.command_id = plan_id(&plan)?;
    Ok(plan)
}

/// Write an approved plan without replacing an existing file.
pub fn write_plan_file(plan: &Plan, path: &Path) -> Result<()> {
    if !path.is_absolute() {
        return Err("Plan output must be an absolute path".into());
    }
    manifest::safe_chain(path)?;
    let bytes = json(plan)?;
    let mut file = OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(path)
        .map_err(|e| e.to_string())?;
    let result = (|| {
        file.write_all(&bytes).map_err(|e| e.to_string())?;
        file.sync_all().map_err(|e| e.to_string())
    })();
    if result.is_err() {
        let _ = fs::remove_file(path);
    }
    result
}

struct RecognizedPackage {
    id: String,
    revision: String,
    directory: PathBuf,
    manifest: PathBuf,
    descriptor: manifest::Descriptor,
    foreign_files: usize,
}

const UNINSTALL_JOURNAL: &str = ".forge-uninstall.pending.json";
const UNINSTALL_DONE: &str = ".forge-uninstall.complete.json";
const GAME_UNINSTALL_COMPLETION: &str = "complete.json";
const MAX_GAME_UNINSTALL_COMPLETION: u64 = 16 * 1024;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct GameUninstallCompletion {
    pub report: UninstallReport,
    pub receipt_sha256: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct GameUninstallReceipt {
    schema: String,
    root: String,
    game_id: forge_core::ProjectId,
    helper_receipt_sha256: String,
    done_sha256: String,
    journal_sha256: String,
    head_sha256: String,
    report: UninstallReport,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct UninstallFilePin {
    bytes: u64,
    sha256: String,
}

#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct UninstallPlayerPins {
    ready_sha256: Option<String>,
    pending_sha256: Option<String>,
    stage_sha256: Option<String>,
    entry: Option<UninstallFilePin>,
}

/// Immutable intent: only a checked manifest may authorize a file removal.
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct UninstallJournal {
    schema: String,
    root: String,
    studio_only: bool,
    head_sha256: String,
    head_json: String,
    stage_name: String,
    packages: Vec<InstalledPack>,
    player: UninstallPlayerPins,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct UninstallDone {
    schema: String,
    journal_sha256: String,
    journal_json: String,
    stage_name: String,
    report: UninstallReport,
}

type ReadUninstallDone = (UninstallDone, Vec<u8>, UninstallJournal, State);

fn ensure_no_uninstall(root: &Path) -> Result<()> {
    for name in [UNINSTALL_JOURNAL, UNINSTALL_DONE] {
        if existing(&manifest::confined(root, name)?)?.is_some() {
            return Err(
                "Une désinstallation est en attente ; reprenez-la avant de continuer.".into(),
            );
        }
    }
    Ok(())
}

fn read_bounded(reader: impl Read, limit: u64, label: &str) -> Result<Vec<u8>> {
    let mut bytes = Vec::new();
    reader
        .take(limit.saturating_add(1))
        .read_to_end(&mut bytes)
        .map_err(|error| error.to_string())?;
    if bytes.len() as u64 > limit {
        return Err(format!("{label} exceeds its read bound"));
    }
    Ok(bytes)
}

fn same_existing_root(left: &Path, right: &Path) -> Result<bool> {
    manifest::safe_chain(left)?;
    manifest::safe_chain(right)?;
    let left = fs::canonicalize(left).map_err(|error| error.to_string())?;
    let right = fs::canonicalize(right).map_err(|error| error.to_string())?;
    Ok(left == right)
}

fn parse_uninstall_journal(root: &Path, bytes: &[u8]) -> Result<(UninstallJournal, State)> {
    let journal: UninstallJournal =
        serde_json::from_slice(bytes).map_err(|error| error.to_string())?;
    if json(&journal)? != bytes
        || journal.schema != "forge.delivery.uninstall.v1"
        || !same_existing_root(Path::new(&journal.root), root)?
        || hash(journal.head_json.as_bytes()) != journal.head_sha256
        || journal.packages.len() > 16384
        || !journal.stage_name.starts_with(".forge-uninstall-stage-")
        || !journal.stage_name.ends_with(".tmp")
        || journal.stage_name.contains(['/', '\\'])
        || manifest::relative(&journal.stage_name).is_err()
    {
        return Err("Uninstall journal is invalid or belongs to another root".into());
    }
    manifest::valid_hash(&journal.head_sha256)?;
    let state: State =
        serde_json::from_str(&journal.head_json).map_err(|error| error.to_string())?;
    if state.schema != "forge.delivery.installation.v1"
        || state.revision == 0
        || state.receipts.len() > 1024
    {
        return Err("Uninstall journal contains an invalid installation head".into());
    }
    let mut kinds = BTreeMap::new();
    for pack in &journal.packages {
        manifest::identifier(&pack.id)?;
        manifest::valid_hash(&pack.revision)?;
        if !snapshot_references(&state.active, pack)
            && !state
                .previous
                .as_ref()
                .is_some_and(|old| snapshot_references(old, pack))
        {
            return Err("Uninstall journal references an unowned package".into());
        }
        if kinds
            .insert((pack.id.clone(), pack.revision.clone()), pack.kind.clone())
            .is_some()
        {
            return Err("Uninstall journal repeats a package".into());
        }
    }
    validate_snapshot_refs(&state.active, &kinds)?;
    if let Some(previous) = &state.previous {
        validate_snapshot_refs(previous, &kinds)?;
    }
    for value in [
        journal.player.ready_sha256.as_ref(),
        journal.player.pending_sha256.as_ref(),
        journal.player.stage_sha256.as_ref(),
        journal.player.entry.as_ref().map(|entry| &entry.sha256),
    ]
    .into_iter()
    .flatten()
    {
        manifest::valid_hash(value)?;
    }
    if (journal.player.stage_sha256.is_some() && journal.player.pending_sha256.is_none())
        || journal
            .player
            .entry
            .as_ref()
            .is_some_and(|entry| entry.bytes == 0 || entry.bytes > manifest::MAX_FILE_BYTES)
    {
        return Err("Uninstall player pins are invalid".into());
    }
    Ok((journal, state))
}

fn read_uninstall_document(root: &Path, name: &str) -> Result<Option<Vec<u8>>> {
    let path = manifest::confined(root, name)?;
    let Some(metadata) = existing(&path)? else {
        return Ok(None);
    };
    if !metadata.is_file() || metadata.len() == 0 || metadata.len() > manifest::MAX_MANIFEST {
        return Err("Uninstall document is not a bounded regular file".into());
    }
    let bytes = read_bounded(
        fs::File::open(&path).map_err(|error| error.to_string())?,
        manifest::MAX_MANIFEST,
        "Uninstall document",
    )?;
    if bytes.len() as u64 != metadata.len() {
        return Err("Uninstall document changed while reading".into());
    }
    Ok(Some(bytes))
}

fn read_uninstall_journal(root: &Path) -> Result<Option<(UninstallJournal, Vec<u8>, State)>> {
    let Some(bytes) = read_uninstall_document(root, UNINSTALL_JOURNAL)? else {
        return Ok(None);
    };
    let (journal, state) = parse_uninstall_journal(root, &bytes)?;
    Ok(Some((journal, bytes, state)))
}

fn read_uninstall_done(root: &Path) -> Result<Option<ReadUninstallDone>> {
    let Some(bytes) = read_uninstall_document(root, UNINSTALL_DONE)? else {
        return Ok(None);
    };
    let done: UninstallDone = serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    if json(&done)? != bytes
        || done.schema != "forge.delivery.uninstall-complete.v1"
        || hash(done.journal_json.as_bytes()) != done.journal_sha256
        || !done.stage_name.starts_with(".forge-uninstall-stage-")
        || !done.stage_name.ends_with(".tmp")
        || done.stage_name.contains(['/', '\\'])
        || manifest::relative(&done.stage_name).is_err()
    {
        return Err("Uninstall completion receipt is invalid".into());
    }
    let (journal, state) = parse_uninstall_journal(root, done.journal_json.as_bytes())?;
    if !same_existing_root(Path::new(&done.report.root), root)? {
        return Err("Uninstall completion report belongs to another root".into());
    }
    Ok(Some((done, bytes, journal, state)))
}

fn publish_uninstall_document(
    root: &Path,
    name: &str,
    stage_name: &str,
    bytes: &[u8],
) -> Result<()> {
    if bytes.len() as u64 > manifest::MAX_MANIFEST {
        return Err("Uninstall document exceeds its read bound".into());
    }
    let stage = manifest::confined(root, stage_name)?;
    let destination = manifest::confined(root, name)?;
    // On an interrupted/failed write, a partial stage is ambiguous. Preserve it:
    // the path alone does not prove that another process did not replace it.
    write_new(&stage, bytes)?;
    let result = forge_project::workspace::publish_no_replace(&stage, &destination)
        .map_err(|error| error.to_string());
    if existing(&stage)?.is_some() {
        // The name was created by this operation; never erase changed bytes.
        remove_exact_document(root, stage_name, bytes)?;
    }
    result?;
    Ok(())
}

fn cleanup_published_uninstall_stage(
    root: &Path,
    marker_name: &str,
    stage_name: &str,
    bytes: &[u8],
) -> Result<()> {
    let stage = manifest::confined(root, stage_name)?;
    let Some(stage_metadata) = existing(&stage)? else {
        return Ok(());
    };
    #[cfg(unix)]
    {
        use std::os::unix::fs::MetadataExt;
        let journal_metadata = fs::metadata(manifest::confined(root, marker_name)?)
            .map_err(|error| error.to_string())?;
        if stage_metadata.dev() == journal_metadata.dev()
            && stage_metadata.ino() == journal_metadata.ino()
        {
            remove_exact_document(root, stage_name, bytes)?;
        }
    }
    #[cfg(not(unix))]
    {
        // Windows no-replace rename consumes the stage; any later path is foreign.
        let _ = (stage_metadata, marker_name, bytes);
    }
    Ok(())
}

fn make_uninstall_done(
    journal_bytes: &[u8],
    stage_name: String,
    report: &UninstallReport,
) -> Result<(UninstallDone, Vec<u8>)> {
    let done = UninstallDone {
        schema: "forge.delivery.uninstall-complete.v1".into(),
        journal_sha256: hash(journal_bytes),
        journal_json: String::from_utf8(journal_bytes.to_vec())
            .map_err(|error| error.to_string())?,
        stage_name,
        report: report.clone(),
    };
    let bytes = json(&done)?;
    if bytes.len() as u64 > manifest::MAX_MANIFEST {
        return Err("Uninstall completion receipt exceeds its read bound".into());
    }
    Ok((done, bytes))
}

struct ExpectedLayout {
    files: BTreeSet<String>,
}

fn path_key(value: &str) -> String {
    value.replace('\\', "/").to_ascii_lowercase()
}

fn containment_key(path: &Path) -> String {
    let value = path_key(&path.to_string_lossy());
    #[cfg(windows)]
    if let Some(disk) = value.strip_prefix("//?/") {
        let bytes = disk.as_bytes();
        if bytes.len() >= 3 && bytes[0].is_ascii_lowercase() && &bytes[1..3] == b":/" {
            return disk.to_owned();
        }
    }
    value
}

fn path_is_within(path: &Path, ancestor: &Path) -> bool {
    let path = containment_key(path);
    let ancestor = containment_key(ancestor).trim_end_matches('/').to_owned();
    path == ancestor || path.starts_with(&(ancestor + "/"))
}

fn existing_protected_overlap(package: &Path, protected: &Path) -> Result<bool> {
    if !path_is_within(package, protected) && !path_is_within(protected, package) {
        return Ok(false);
    }
    // The protected tree belongs to the user: do not enumerate its contents.
    Ok(existing(protected)?.is_some())
}

/// Resolve an existing physical ancestor, retaining a not-yet-created suffix.
fn canonical_with_missing(path: &Path) -> Result<PathBuf> {
    manifest::safe_chain(path)?;
    let mut ancestor = path;
    let mut suffix = Vec::new();
    loop {
        match fs::symlink_metadata(ancestor) {
            Ok(metadata) => {
                if !suffix.is_empty() && !metadata.is_dir() {
                    return Err("Protected path has a non-directory ancestor".into());
                }
                break;
            }
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
                suffix.push(
                    ancestor
                        .file_name()
                        .ok_or("Protected path has no existing ancestor")?
                        .to_owned(),
                );
                ancestor = ancestor
                    .parent()
                    .ok_or("Protected path has no existing ancestor")?;
            }
            Err(error) => return Err(error.to_string()),
        }
    }
    let mut resolved = fs::canonicalize(ancestor).map_err(|error| error.to_string())?;
    for part in suffix.iter().rev() {
        resolved.push(part);
    }
    manifest::safe_chain(path)?;
    Ok(resolved)
}

// ASCII folding is not sufficient for unresolved Windows Unicode names. Until
// those suffixes exist and can be physically canonicalized, refuse comparison.
#[cfg(windows)]
fn reject_ambiguous_missing_unicode(path: &Path) -> Result<()> {
    manifest::safe_chain(path)?;
    let mut ancestor = path;
    loop {
        match fs::symlink_metadata(ancestor) {
            Ok(_) => return Ok(()),
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
                let name = ancestor
                    .file_name()
                    .and_then(|name| name.to_str())
                    .ok_or("Ambiguous protected path")?;
                if !name.is_ascii() {
                    return Err("Le dossier de profil non créé contient un nom Unicode ambigu. Créez d’abord son dossier parent puis réessayez.".into());
                }
                ancestor = ancestor
                    .parent()
                    .ok_or("Protected path has no existing ancestor")?;
            }
            Err(error) => return Err(error.to_string()),
        }
    }
}

/// Compare prospective paths without creating or following linked ancestors.
/// Refuse either path being equal to or inside the other.
pub fn physically_disjoint(first: &Path, second: &Path) -> Result<bool> {
    #[cfg(windows)]
    {
        reject_ambiguous_missing_unicode(first)?;
        reject_ambiguous_missing_unicode(second)?;
    }
    let first = canonical_with_missing(first)?;
    let second = canonical_with_missing(second)?;
    Ok(!path_is_within(&first, &second) && !path_is_within(&second, &first))
}

/// Map a physical protected path into the installation root's own spelling.
/// This keeps ordinary and verbatim Windows paths, and existing short names,
/// from hiding an overlap between package files and user data.
fn protected_path_for_root(root: &Path, protected: &Path) -> Result<PathBuf> {
    manifest::safe_chain(root)?;
    let resolved_root = fs::canonicalize(root).map_err(|error| error.to_string())?;
    let resolved_protected = canonical_with_missing(protected)?;
    let root_components = resolved_root.components().count();
    let protected_components = resolved_protected.components().count();
    if path_is_within(&resolved_protected, &resolved_root) {
        let mut mapped = root.to_path_buf();
        for component in resolved_protected.components().skip(root_components) {
            mapped.push(component.as_os_str());
        }
        Ok(mapped)
    } else if path_is_within(&resolved_root, &resolved_protected) {
        let distance = root_components
            .checked_sub(protected_components)
            .ok_or("Protected path component mismatch")?;
        root.ancestors()
            .nth(distance)
            .map(Path::to_path_buf)
            .ok_or_else(|| "Protected path ancestor mismatch".into())
    } else {
        Ok(protected.to_path_buf())
    }
}

fn existing(path: &Path) -> Result<Option<std::fs::Metadata>> {
    match fs::symlink_metadata(path) {
        Ok(metadata) => Ok(Some(metadata)),
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => Ok(None),
        Err(error) => Err(error.to_string()),
    }
}

fn relative_name(root: &Path, path: &Path) -> Result<String> {
    Ok(path
        .strip_prefix(root)
        .map_err(|e| e.to_string())?
        .to_str()
        .ok_or("Non-Unicode package path")?
        .replace('\\', "/"))
}

fn expected_layout(descriptor: &manifest::Descriptor) -> ExpectedLayout {
    let mut files = BTreeSet::from([path_key("package-manifest.json")]);
    for entry in &descriptor.files {
        files.insert(path_key(&entry.path));
    }
    ExpectedLayout { files }
}

fn is_expected_file(layout: &ExpectedLayout, relative: &str) -> bool {
    layout.files.contains(&path_key(relative))
}

fn scan_no_links(path: &Path, protected: &Path) -> Result<()> {
    if path_is_within(path, protected) {
        return Ok(());
    }
    manifest::no_links(path)?;
    let metadata = fs::symlink_metadata(path).map_err(|e| e.to_string())?;
    if metadata.is_dir() {
        for child in fs::read_dir(path).map_err(|e| e.to_string())? {
            scan_no_links(&child.map_err(|e| e.to_string())?.path(), protected)?;
        }
    } else if !metadata.is_file() {
        return Err(format!("Non-regular installed entry: {}", path.display()));
    }
    Ok(())
}

fn scan_foreign(
    package: &Path,
    folder: &Path,
    protected: &Path,
    layout: &ExpectedLayout,
    foreign_files: &mut usize,
) -> Result<()> {
    if path_is_within(folder, protected) {
        return Ok(());
    }
    for child in fs::read_dir(folder).map_err(|e| e.to_string())? {
        let child = child.map_err(|e| e.to_string())?;
        let path = child.path();
        if path_is_within(&path, protected) {
            continue;
        }
        manifest::no_links(&path)?;
        let metadata = fs::symlink_metadata(&path).map_err(|e| e.to_string())?;
        let relative = relative_name(package, &path)?;
        if metadata.is_dir() {
            scan_foreign(package, &path, protected, layout, foreign_files)?;
        } else if metadata.is_file() {
            if !is_expected_file(layout, &relative) {
                *foreign_files = foreign_files.saturating_add(1);
            }
        } else {
            return Err(format!("Non-regular installed entry: {}", path.display()));
        }
    }
    Ok(())
}

fn inspect_packages(root: &Path, protected: &Path) -> Result<Vec<RecognizedPackage>> {
    let packages = manifest::confined(root, "packages")?;
    let Some(metadata) = existing(&packages)? else {
        return Ok(Vec::new());
    };
    if !metadata.is_dir() {
        return Err("Installed packages path is not a directory".into());
    }
    scan_no_links(&packages, protected)?;
    let mut result = Vec::new();
    for id_entry in fs::read_dir(&packages).map_err(|e| e.to_string())? {
        let id_entry = id_entry.map_err(|e| e.to_string())?;
        let id_path = id_entry.path();
        manifest::no_links(&id_path)?;
        let id_metadata = fs::symlink_metadata(&id_path).map_err(|e| e.to_string())?;
        if !id_metadata.is_dir() {
            continue;
        }
        let id = id_entry
            .file_name()
            .to_str()
            .ok_or("Non-Unicode package identifier directory")?
            .to_owned();
        for revision_entry in fs::read_dir(&id_path).map_err(|e| e.to_string())? {
            let revision_entry = revision_entry.map_err(|e| e.to_string())?;
            let revision_path = revision_entry.path();
            manifest::no_links(&revision_path)?;
            let revision_metadata =
                fs::symlink_metadata(&revision_path).map_err(|e| e.to_string())?;
            if !revision_metadata.is_dir() {
                continue;
            }
            let manifest_path = revision_path.join("package-manifest.json");
            let Some(manifest_metadata) = existing(&manifest_path)? else {
                continue;
            };
            manifest::no_links(&manifest_path)?;
            if !manifest_metadata.is_file() {
                return Err(format!(
                    "Package manifest is not a file: {}",
                    manifest_path.display()
                ));
            }
            manifest::identifier(&id)?;
            let revision = revision_entry
                .file_name()
                .to_str()
                .ok_or("Non-Unicode package revision directory")?
                .to_owned();
            manifest::valid_hash(&revision)?;
            let descriptor: manifest::Descriptor = crate::read_json(&manifest_path)?;
            descriptor.validate()?;
            if descriptor.id != id || hash(&json(&descriptor)?) != revision {
                return Err(format!(
                    "Unrecognized package revision: {}",
                    revision_path.display()
                ));
            }
            for entry in &descriptor.files {
                let path = manifest::confined(&revision_path, &entry.path)?;
                if let Some(metadata) = existing(&path)? {
                    manifest::no_links(&path)?;
                    if !metadata.is_file() {
                        return Err(format!(
                            "Owned package path is not a file: {}",
                            path.display()
                        ));
                    }
                }
            }
            let layout = expected_layout(&descriptor);
            let mut foreign_files = 0;
            scan_foreign(
                &revision_path,
                &revision_path,
                protected,
                &layout,
                &mut foreign_files,
            )?;
            result.push(RecognizedPackage {
                id: id.clone(),
                revision,
                directory: revision_path,
                manifest: manifest_path,
                descriptor,
                foreign_files,
            });
        }
    }
    Ok(result)
}

fn validate_snapshot_refs(
    snapshot: &Snapshot,
    packages: &BTreeMap<(String, String), Kind>,
) -> Result<()> {
    if snapshot.core.kind != Kind::Applications || snapshot.packs.len() > 1024 {
        return Err("Invalid installed snapshot".into());
    }
    manifest::identifier(&snapshot.core.id)?;
    manifest::valid_hash(&snapshot.core.revision)?;
    let core_kind = packages
        .get(&(snapshot.core.id.clone(), snapshot.core.revision.clone()))
        .ok_or("Installation head references an unrecognized application package")?;
    if core_kind != &snapshot.core.kind {
        return Err("Application package kind mismatch".into());
    }
    for (id, pack) in &snapshot.packs {
        if id != &pack.id || pack.kind == Kind::Applications {
            return Err("Invalid installed pack index".into());
        }
        manifest::identifier(&pack.id)?;
        manifest::valid_hash(&pack.revision)?;
        let kind = packages
            .get(&(pack.id.clone(), pack.revision.clone()))
            .ok_or("Installation head references an unrecognized pack")?;
        if kind != &pack.kind {
            return Err("Installed pack kind mismatch".into());
        }
    }
    Ok(())
}

fn remove_if_matching(
    path: &Path,
    protected: &Path,
    expected_bytes: u64,
    expected_hash: &str,
) -> Result<(bool, bool)> {
    if path_is_within(path, protected) {
        return Ok((false, existing(path)?.is_some()));
    }
    let Some(metadata) = existing(path)? else {
        return Ok((false, false));
    };
    manifest::no_links(path)?;
    if !metadata.is_file() {
        return Err(format!(
            "Owned package path is not a file: {}",
            path.display()
        ));
    }
    if metadata.len() != expected_bytes || file_hash(path)? != expected_hash {
        return Ok((false, true));
    }
    fs::remove_file(path).map_err(|e| e.to_string())?;
    Ok((true, false))
}

fn remove_empty_ancestors(directory: &Path, packages: &Path, protected: &Path) -> Result<()> {
    let mut current = directory.to_path_buf();
    loop {
        if current == packages
            || !path_is_within(&current, packages)
            || path_is_within(&current, protected)
        {
            return Ok(());
        }
        let Some(metadata) = existing(&current)? else {
            return Ok(());
        };
        manifest::no_links(&current)?;
        if !metadata.is_dir()
            || fs::read_dir(&current)
                .map_err(|e| e.to_string())?
                .next()
                .is_some()
        {
            return Ok(());
        }
        fs::remove_dir(&current).map_err(|e| e.to_string())?;
        current = current
            .parent()
            .ok_or("Package parent missing")?
            .to_path_buf();
    }
}

fn snapshot_references(snapshot: &Snapshot, pack: &InstalledPack) -> bool {
    (snapshot.core.id == pack.id && snapshot.core.revision == pack.revision)
        || snapshot
            .packs
            .values()
            .any(|entry| entry.id == pack.id && entry.revision == pack.revision)
}

/// Remove only the verified, unchanged files of a package created by this
/// failed transaction. Unknown files, links and protected user roots survive.
fn remove_rejected_package(
    root: &Path,
    pack: &InstalledPack,
    protected_roots: &[PathBuf],
) -> Result<bool> {
    let packages = manifest::confined(root, "packages")?;
    let directory = package_root(root, pack)?;
    if protected_roots.iter().any(|protected| {
        path_is_within(&directory, protected) || path_is_within(protected, &directory)
    }) {
        return Ok(false);
    }
    manifest::safe_chain(&directory)?;
    let resolved_packages = packages.canonicalize().map_err(|e| e.to_string())?;
    let resolved_directory = directory.canonicalize().map_err(|e| e.to_string())?;
    if resolved_directory == resolved_packages
        || !resolved_directory.starts_with(&resolved_packages)
    {
        return Err("Rejected package escaped the installation package root".into());
    }
    let descriptor = archive::verify_directory(&directory, &pack.revision)?;
    let protected = protected_roots
        .first()
        .ok_or("Missing protected user root")?;
    let mut preserved = false;
    for entry in &descriptor.files {
        let path = manifest::confined(&directory, &entry.path)?;
        let (removed, kept) = remove_if_matching(&path, protected, entry.bytes, &entry.sha256)?;
        preserved |= kept;
        if removed && let Some(parent) = path.parent() {
            remove_empty_ancestors(parent, &packages, protected)?;
        }
    }
    if !preserved {
        let manifest_path = manifest::confined(&directory, "package-manifest.json")?;
        let bytes = json(&descriptor)?;
        let (_, kept) = remove_if_matching(
            &manifest_path,
            protected,
            bytes.len() as u64,
            &pack.revision,
        )?;
        preserved |= kept;
    }
    remove_empty_ancestors(&directory, &packages, protected)?;
    Ok(!preserved && existing(&directory)?.is_none())
}

fn cleanup_rejected_packages(
    root: &Path,
    created: &[InstalledPack],
    current: Option<&State>,
    planned_user_root: &Path,
) -> usize {
    let Ok(planned) = protected_path_for_root(root, planned_user_root) else {
        return created.len();
    };
    let mut protected_roots = vec![planned];
    if let Some(state) = current {
        let existing_user_root = PathBuf::from(&state.user_root);
        let Ok(protected) = protected_path_for_root(root, &existing_user_root) else {
            return created.len();
        };
        protected_roots.push(protected);
    }
    let mut retained = 0;
    for pack in created {
        let referenced = current.is_some_and(|state| {
            snapshot_references(&state.active, pack)
                || state
                    .previous
                    .as_ref()
                    .is_some_and(|previous| snapshot_references(previous, pack))
        });
        if referenced || !remove_rejected_package(root, pack, &protected_roots).unwrap_or(false) {
            retained += 1;
        }
    }
    retained
}

fn rejected_transaction_error(
    error: String,
    standalone: bool,
    root: &Path,
    created: &[InstalledPack],
    current: Option<&State>,
    planned_user_root: &Path,
) -> String {
    if !standalone {
        return error;
    }
    let retained = cleanup_rejected_packages(root, created, current, planned_user_root);
    if retained == 0 {
        error
    } else {
        format!("{error}; {retained} new package(s) retained for protection or inspection")
    }
}

fn validate_uninstall_paths(
    root: &Path,
    state_path: &Path,
    lock_path: &Path,
    state: &State,
) -> Result<PathBuf> {
    if state.revision == 0 {
        return Err("Invalid installation revision".into());
    }
    let user_root = PathBuf::from(&state.user_root);
    if !user_root.is_absolute() {
        return Err("User data root must be absolute".into());
    }
    let user_root = protected_path_for_root(root, &user_root)?;
    if path_is_within(root, &user_root) {
        return Err("User data root contains the installation root".into());
    }
    for path in [
        state_path.to_path_buf(),
        lock_path.to_path_buf(),
        manifest::confined(root, UNINSTALL_JOURNAL)?,
        manifest::confined(root, UNINSTALL_DONE)?,
        manifest::confined(root, PLAYER_RECEIPT)?,
        manifest::confined(root, PLAYER_PENDING)?,
    ] {
        if path_is_within(&path, &user_root) {
            return Err("Installation metadata is inside the protected user data root".into());
        }
    }
    Ok(user_root)
}

fn finish_uninstall(
    root: &Path,
    state_path: &Path,
    pending: Option<&[u8]>,
    done_bytes: &[u8],
    journal: &UninstallJournal,
    checkpoint: &mut dyn FnMut(&str) -> Result<()>,
) -> Result<()> {
    if read_uninstall_document(root, UNINSTALL_DONE)?.as_deref() != Some(done_bytes)
        || read_uninstall_document(root, UNINSTALL_JOURNAL)?.as_deref() != pending
    {
        return Err("Uninstall markers changed before finalization; preserve them".into());
    }
    if existing(state_path)?.is_some() {
        manifest::no_links(state_path)?;
        if pending.is_none() || file_hash(state_path)? != journal.head_sha256 {
            return Err("Installation head changed before uninstall completion".into());
        }
        if journal.studio_only {
            record_retained_studio_assets(root, journal)?;
        }
        fs::remove_file(state_path).map_err(|error| error.to_string())?;
        checkpoint("head")?;
    }
    if let Some(bytes) = pending {
        remove_exact_document(root, UNINSTALL_JOURNAL, bytes)?;
        checkpoint("journal_removed")?;
    }
    remove_exact_document(root, UNINSTALL_DONE, done_bytes)?;
    checkpoint("done_removed")?;
    Ok(())
}

fn record_retained_studio_assets(root: &Path, journal: &UninstallJournal) -> Result<()> {
    let state: State =
        serde_json::from_str(&journal.head_json).map_err(|error| error.to_string())?;
    let mut packs = BTreeMap::new();
    for id in STUDIO_RETAINED_PACKS {
        let directory = manifest::confined(root, &format!("packages/{id}"))?;
        if existing(&directory)?.is_some_and(|metadata| metadata.is_dir()) {
            let pack = state.active.packs.get(id).or_else(|| {
                state.previous.as_ref().and_then(|snapshot| snapshot.packs.get(id))
            });
            packs.insert(id.to_owned(), pack.cloned());
        }
    }
    if packs.is_empty() {
        return Ok(());
    }
    let previous = read_uninstall_document(root, STUDIO_LIBRARY_RECEIPT)?;
    let bytes = json(&RetainedStudioAssets {
        schema: STUDIO_ASSETS_SCHEMA.into(),
        user_root: state.user_root,
        packs,
    })?;
    if previous.as_deref() == Some(bytes.as_slice()) {
        return Ok(());
    }
    let stage_name = format!("{}.library", journal.stage_name);
    let stage = manifest::confined(root, &stage_name)?;
    if let Some(staged) = read_uninstall_document(root, &stage_name)? {
        if staged != bytes {
            return Err("Le reçu temporaire de la bibliothèque Studio a changé.".into());
        }
    } else {
        write_new(&stage, &bytes)?;
    }
    if read_uninstall_document(root, STUDIO_LIBRARY_RECEIPT)? != previous {
        return Err("Le reçu de la bibliothèque Studio a changé.".into());
    }
    fs::rename(stage, manifest::confined(root, STUDIO_LIBRARY_RECEIPT)?)
        .map_err(|error| error.to_string())
}

fn uninstall_locked(
    root: &Path,
    state_path: &Path,
    lock_path: &Path,
    studio_only: bool,
) -> Result<UninstallReport> {
    uninstall_locked_with(root, state_path, lock_path, studio_only, &mut |_| Ok(()))
}

fn uninstall_locked_with(
    root: &Path,
    state_path: &Path,
    lock_path: &Path,
    studio_only: bool,
    checkpoint: &mut dyn FnMut(&str) -> Result<()>,
) -> Result<UninstallReport> {
    let existing_journal = read_uninstall_journal(root)?;
    if let Some((done, done_bytes, journal, state)) = read_uninstall_done(root)? {
        if journal.studio_only != studio_only {
            return Err("Uninstall must resume using its original mode".into());
        }
        validate_uninstall_paths(root, state_path, lock_path, &state)?;
        let pending = match &existing_journal {
            Some((_, bytes, _)) if bytes.as_slice() == done.journal_json.as_bytes() => {
                Some(bytes.as_slice())
            }
            Some(_) => {
                return Err("Uninstall completion receipt differs from the pending journal".into());
            }
            None if existing(state_path)?.is_none() => None,
            None => return Err("Uninstall journal disappeared before its head".into()),
        };
        cleanup_published_uninstall_stage(root, UNINSTALL_DONE, &done.stage_name, &done_bytes)?;
        if let Some((pending_journal, bytes, _)) = &existing_journal {
            cleanup_published_uninstall_stage(
                root,
                UNINSTALL_JOURNAL,
                &pending_journal.stage_name,
                bytes,
            )?;
        }
        checkpoint("before_finish")?;
        finish_uninstall(root, state_path, pending, &done_bytes, &journal, checkpoint)?;
        return Ok(done.report);
    }
    if let Some((journal, bytes, _)) = &existing_journal {
        if existing(state_path)?.is_none() {
            return Err(
                "Installation head disappeared before uninstall completion; preserve the journal"
                    .into(),
            );
        }
        cleanup_published_uninstall_stage(root, UNINSTALL_JOURNAL, &journal.stage_name, bytes)?;
    }
    let resumed = existing_journal.is_some();
    let state = match &existing_journal {
        Some((journal, _, state)) => {
            if journal.studio_only != studio_only {
                return Err("Uninstall must resume using its original mode".into());
            }
            if existing(state_path)?.is_some() {
                manifest::no_links(state_path)?;
                if file_hash(state_path)? != journal.head_sha256 {
                    return Err("Installation head changed during uninstall".into());
                }
            }
            state.clone()
        }
        None => state_unchecked(root)?,
    };
    let user_root = validate_uninstall_paths(root, state_path, lock_path, &state)?;
    let packages = manifest::confined(root, "packages")?;
    let recognized = inspect_packages(root, &user_root)?;
    let package_kinds = recognized
        .iter()
        .map(|package| {
            (
                (package.id.clone(), package.revision.clone()),
                package.descriptor.kind.clone(),
            )
        })
        .collect::<BTreeMap<_, _>>();
    if !resumed {
        validate_snapshot_refs(&state.active, &package_kinds)?;
        if let Some(previous) = &state.previous {
            validate_snapshot_refs(previous, &package_kinds)?;
        }
    }
    if studio_only {
        // Every revision is checked while holding the same lock as removal.
        // Unrecognized folders are also refused, rather than silently skipped,
        // so a legacy or foreign package cannot be swept up by this operation.
        let has_foreign_content = |snapshot: &Snapshot| {
            snapshot.packs.keys().any(|id| {
                !matches!(id.as_str(), manifest::STUDIO_REFERENCE_ASSETS | STUDIO_LIBRARY_ID)
            })
        };
        if (!resumed && recognized.is_empty())
            || has_foreign_content(&state.active)
            || state.previous.as_ref().is_some_and(has_foreign_content)
            || recognized.iter().any(|package| {
                if package.id == STUDIO_LIBRARY_ID {
                    return package.descriptor.kind != Kind::Assets;
                }
                if package.id == manifest::STUDIO_REFERENCE_ASSETS {
                    return manifest::reference_catalog(&package.descriptor).is_err();
                }
                package.id != "forge-studio"
                    || package.descriptor.kind != Kind::Applications
                    || package.descriptor.product.as_ref().is_none_or(|product| {
                        product.schema != manifest::STUDIO_PRODUCT_SCHEMA
                            || product.id != manifest::STUDIO_PRODUCT_ID
                    })
            })
        {
            return Err(
                "Studio uninstall refused: a package or revision is outside Studio v3".into(),
            );
        }
        if recognized
            .iter()
            .any(|package| STUDIO_RETAINED_PACKS.contains(&package.id.as_str()))
            && !physically_disjoint(&packages, &user_root)?
        {
            return Err("Les assets installés recouvrent le dossier utilisateur.".into());
        }
        for id_entry in fs::read_dir(&packages).map_err(|e| e.to_string())? {
            let id_path = id_entry.map_err(|e| e.to_string())?.path();
            if !id_path.is_dir()
                || !matches!(
                    id_path.file_name().and_then(|s| s.to_str()),
                    Some("forge-studio" | manifest::STUDIO_REFERENCE_ASSETS | STUDIO_LIBRARY_ID)
                )
            {
                return Err("Studio uninstall refused: unrecognized package path".into());
            }
            for revision in fs::read_dir(&id_path).map_err(|e| e.to_string())? {
                let revision = revision.map_err(|e| e.to_string())?.path();
                let pinned = existing_journal.as_ref().is_some_and(|(journal, _, _)| {
                    journal
                        .packages
                        .iter()
                        .any(|pack| package_root(root, pack).is_ok_and(|path| path == revision))
                });
                if !revision.is_dir()
                    || (!pinned && !recognized.iter().any(|item| item.directory == revision))
                {
                    return Err("Studio uninstall refused: unrecognized package revision".into());
                }
            }
        }
    }

    let (journal, journal_bytes) = if let Some((journal, bytes, _)) = existing_journal {
        (journal, bytes)
    } else {
        let head_bytes = read_bounded(
            fs::File::open(state_path).map_err(|error| error.to_string())?,
            manifest::MAX_MANIFEST,
            "Installation head",
        )?;
        let head_json = String::from_utf8(head_bytes).map_err(|error| error.to_string())?;
        let observed: State =
            serde_json::from_str(&head_json).map_err(|error| error.to_string())?;
        if json(&observed)? != json(&state)? || file_hash(state_path)? != hash(head_json.as_bytes())
        {
            return Err("Installation head changed while preparing uninstall".into());
        }
        let mut pins: Vec<_> = recognized
            .iter()
            .map(|package| InstalledPack {
                id: package.id.clone(),
                revision: package.revision.clone(),
                kind: package.descriptor.kind.clone(),
            })
            .filter(|pack| {
                snapshot_references(&state.active, pack)
                    || state
                        .previous
                        .as_ref()
                        .is_some_and(|old| snapshot_references(old, pack))
            })
            .collect();
        pins.sort_by(|left, right| (&left.id, &left.revision).cmp(&(&right.id, &right.revision)));
        if pins.len() > 16384 {
            return Err("Too many package revisions for an uninstall journal".into());
        }
        let journal = UninstallJournal {
            schema: "forge.delivery.uninstall.v1".into(),
            root: root.to_str().ok_or("Non-Unicode installation root")?.into(),
            studio_only,
            head_sha256: hash(head_json.as_bytes()),
            head_json,
            stage_name: format!(".forge-uninstall-stage-{}.tmp", crate::attempt()?),
            packages: pins,
            player: capture_uninstall_player(root, &state)?,
        };
        let bytes = json(&journal)?;
        // Ensure a complete receipt can be written before any payload changes.
        let worst_report = UninstallReport {
            root: journal.root.clone(),
            removed_files: usize::MAX,
            preserved_files: usize::MAX,
            preserved_packages: usize::MAX,
            counts_partial: true,
        };
        make_uninstall_done(
            &bytes,
            format!(".forge-uninstall-stage-{}.tmp", "0".repeat(128)),
            &worst_report,
        )?;
        publish_uninstall_document(root, UNINSTALL_JOURNAL, &journal.stage_name, &bytes)?;
        checkpoint("journal")?;
        (journal, bytes)
    };
    verify_uninstall_player(root, &journal.player)?;

    // The stable entry and its receipts must be removed before the head or
    // packages. In particular, an executable locked by Windows aborts here.
    let (player_removed, player_preserved) =
        remove_player_entry_before_uninstall(root, &state, &user_root)?;
    checkpoint("player")?;
    let mut report = UninstallReport {
        root: root
            .to_str()
            .ok_or("Non-Unicode installation root")?
            .to_owned(),
        removed_files: player_removed,
        preserved_files: player_preserved,
        preserved_packages: 0,
        counts_partial: resumed,
    };
    for pin in &journal.packages {
        if studio_only && STUDIO_RETAINED_PACKS.contains(&pin.id.as_str()) {
            continue;
        }
        if !recognized
            .iter()
            .any(|package| package.id == pin.id && package.revision == pin.revision)
        {
            let directory = package_root(root, pin)?;
            if existing(&directory)?.is_some() {
                // An interruption may leave the already-emptied revision
                // directory after removing its manifest. Never remove its contents.
                remove_empty_ancestors(&directory, &packages, &user_root)?;
                if existing(&directory)?.is_some() {
                    report.preserved_packages = report.preserved_packages.saturating_add(1);
                }
            }
        }
    }
    for package in recognized {
        if studio_only && STUDIO_RETAINED_PACKS.contains(&package.id.as_str()) {
            report.preserved_packages = report.preserved_packages.saturating_add(1);
            continue;
        }
        if !journal.packages.iter().any(|pin| {
            pin.id == package.id
                && pin.revision == package.revision
                && pin.kind == package.descriptor.kind
        }) {
            report.preserved_packages = report.preserved_packages.saturating_add(1);
            continue;
        }
        if file_hash(&package.manifest)? != package.revision {
            return Err("Package manifest changed during uninstall".into());
        }
        let mut package_preserved = package.foreign_files > 0
            || existing_protected_overlap(&package.directory, &user_root)?;
        report.preserved_files = report.preserved_files.saturating_add(package.foreign_files);
        for entry in &package.descriptor.files {
            let path = manifest::confined(&package.directory, &entry.path)?;
            let (removed, preserved) =
                remove_if_matching(&path, &user_root, entry.bytes, &entry.sha256)?;
            report.removed_files = report.removed_files.saturating_add(usize::from(removed));
            if removed {
                checkpoint("file")?;
            }
            if removed && let Some(parent) = path.parent() {
                remove_empty_ancestors(parent, &packages, &user_root)?;
            }
            if preserved {
                package_preserved = true;
                report.preserved_files = report.preserved_files.saturating_add(1);
            }
        }
        // Recheck for files introduced during this operation before removing
        // the manifest that is needed to recognize a preserved package.
        let mut foreign_now = 0;
        scan_foreign(
            &package.directory,
            &package.directory,
            &user_root,
            &expected_layout(&package.descriptor),
            &mut foreign_now,
        )?;
        package_preserved |=
            foreign_now > 0 || existing_protected_overlap(&package.directory, &user_root)?;
        report.preserved_files = report
            .preserved_files
            .saturating_add(foreign_now.saturating_sub(package.foreign_files));
        let manifest_bytes = json(&package.descriptor)?;
        let manifest_protected = path_is_within(&package.manifest, &user_root);
        let manifest_exists = existing(&package.manifest)?.is_some();
        if manifest_exists {
            manifest::no_links(&package.manifest)?;
            let metadata = fs::symlink_metadata(&package.manifest).map_err(|e| e.to_string())?;
            if !metadata.is_file() {
                return Err(format!(
                    "Package manifest is not a file: {}",
                    package.manifest.display()
                ));
            }
            let exact = fs::read(&package.manifest).map_err(|e| e.to_string())? == manifest_bytes;
            if manifest_protected || package_preserved || !exact {
                package_preserved = true;
                report.preserved_files = report.preserved_files.saturating_add(1);
            } else {
                fs::remove_file(&package.manifest).map_err(|e| e.to_string())?;
                report.removed_files = report.removed_files.saturating_add(1);
                checkpoint("manifest")?;
            }
        }
        remove_empty_ancestors(&package.directory, &packages, &user_root)?;
        if existing(&package.directory)?.is_some() && package_preserved {
            report.preserved_packages = report.preserved_packages.saturating_add(1);
        }
    }
    let (done, done_bytes) = make_uninstall_done(
        &journal_bytes,
        format!(".forge-uninstall-stage-{}.tmp", crate::attempt()?),
        &report,
    )?;
    publish_uninstall_document(root, UNINSTALL_DONE, &done.stage_name, &done_bytes)?;
    checkpoint("done")?;
    checkpoint("before_finish")?;
    finish_uninstall(
        root,
        state_path,
        Some(&journal_bytes),
        &done_bytes,
        &journal,
        checkpoint,
    )?;
    Ok(report)
}

/// Remove only verified package files while preserving user data, edits and foreign entries.
pub fn uninstall(root: &Path) -> Result<UninstallReport> {
    uninstall_mode(root, false, None)
}

/// Remove the Studio and retain its asset packs in the installation directory.
pub fn uninstall_studio(root: &Path) -> Result<UninstallReport> {
    uninstall_mode(root, true, None)
}

/// Remove or resume removal of the one reviewed standalone game. Identity is
/// checked under the uninstall lock, including when only its journal remains.
pub fn uninstall_game(root: &Path, expected: forge_core::ProjectId) -> Result<UninstallReport> {
    if expected.as_uuid().is_nil() {
        return Err("Game identity must not be empty".into());
    }
    uninstall_mode(root, false, Some(expected))
}

fn uninstall_game_identity(root: &Path, expected: forge_core::ProjectId) -> Result<()> {
    let pending = read_uninstall_journal(root)?;
    let done = read_uninstall_done(root)?;
    let state = if let Some((_, _, _, state)) = done {
        state
    } else if let Some((_, _, state)) = pending {
        state
    } else {
        let state = state_unchecked(root)?;
        // The immutable package manifest is pinned by `product`; individual
        // modified files remain for the existing uninstall service to preserve.
        let definition = product(root, &state.active)?;
        if definition.schema != manifest::STANDALONE_PRODUCT_SCHEMA
            || definition.id != expected.to_string()
        {
            return Err("Le jeu installé a changé. Rouvrez son lanceur.".into());
        }
        state
    };
    // The private Studio exporter gives each game's core this exact ID. It
    // remains available in the verified journal even after its manifest was
    // removed; the journal is not a cryptographic signature.
    if state.active.core.kind != Kind::Applications
        || state.active.core.id != format!("game-{expected}")
    {
        return Err("La désinstallation concerne un autre jeu.".into());
    }
    Ok(())
}

fn checked_uninstall_helper(helper: &crate::standalone::UninstallHelper) -> Result<()> {
    let current = crate::standalone::read_game_uninstall_helper(&helper.executable)?
        .ok_or("Programme de désinstallation introuvable")?;
    if current.root != helper.root
        || current.executable != helper.executable
        || current.game_id != helper.game_id
        || current.executable_sha256 != helper.executable_sha256
        || current.receipt_sha256 != helper.receipt_sha256
    {
        return Err("Programme de désinstallation modifié".into());
    }
    Ok(())
}

fn read_game_uninstall_receipt(
    helper: &crate::standalone::UninstallHelper,
) -> Result<Option<(GameUninstallReceipt, String)>> {
    let directory = helper
        .executable
        .parent()
        .ok_or("Dossier temporaire absent")?;
    let path = manifest::confined(directory, GAME_UNINSTALL_COMPLETION)?;
    let Some(metadata) = existing(&path)? else {
        return Ok(None);
    };
    if !metadata.is_file() || metadata.len() == 0 || metadata.len() > MAX_GAME_UNINSTALL_COMPLETION
    {
        return Err("Reçu final du programme temporaire invalide".into());
    }
    manifest::no_links(&path)?;
    let bytes = read_bounded(
        fs::File::open(&path).map_err(|error| error.to_string())?,
        MAX_GAME_UNINSTALL_COMPLETION,
        "Game uninstall receipt",
    )?;
    if bytes.len() as u64 != metadata.len() {
        return Err("Reçu final modifié pendant sa lecture".into());
    }
    let receipt: GameUninstallReceipt =
        serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    for value in [
        &receipt.helper_receipt_sha256,
        &receipt.done_sha256,
        &receipt.journal_sha256,
        &receipt.head_sha256,
    ] {
        manifest::valid_hash(value)?;
    }
    if json(&receipt)? != bytes
        || receipt.schema != "forge.delivery.game-uninstalled.v2"
        || !same_existing_root(Path::new(&receipt.root), &helper.root)?
        || !same_existing_root(Path::new(&receipt.report.root), &helper.root)?
        || receipt.game_id != helper.game_id
        || receipt.helper_receipt_sha256 != helper.receipt_sha256
    {
        return Err("Reçu final modifié ou associé à une autre installation".into());
    }
    Ok(Some((receipt, hash(&bytes))))
}

fn game_uninstall_helper_status_locked(
    helper: &crate::standalone::UninstallHelper,
) -> Result<Option<GameUninstallCompletion>> {
    checked_uninstall_helper(helper)?;
    let root = &helper.root;
    let head_path = manifest::confined(root, "installation.json")?;
    let head_exists = existing(&head_path)?.is_some();
    if head_exists {
        manifest::no_links(&head_path)?;
    }
    let pending = read_uninstall_journal(root)?;
    let done = read_uninstall_done(root)?;
    if let Some((receipt, receipt_sha256)) = read_game_uninstall_receipt(helper)? {
        if let Some((done, bytes, journal, state)) = done {
            if hash(&bytes) != receipt.done_sha256
                || done.journal_sha256 != receipt.journal_sha256
                || journal.head_sha256 != receipt.head_sha256
                || journal.studio_only
                || state.active.core.kind != Kind::Applications
                || state.active.core.id != format!("game-{}", helper.game_id)
                || done.report != receipt.report
                || pending
                    .as_ref()
                    .is_some_and(|(_, bytes, _)| bytes.as_slice() != done.journal_json.as_bytes())
                || (head_exists && pending.is_none())
                || (head_exists && file_hash(&head_path)? != journal.head_sha256)
            {
                return Err("Reçu final et transaction de désinstallation divergent".into());
            }
            return Ok(None);
        }
        if head_exists || pending.is_some() {
            return Err(
                "Installation présente ou transaction finale ambiguë ; reçu conservé".into(),
            );
        }
        return Ok(Some(GameUninstallCompletion {
            report: receipt.report,
            receipt_sha256,
        }));
    }
    if !head_exists && pending.is_none() && done.is_none() {
        return Err("Installation et reçu final absents ; programme temporaire conservé".into());
    }
    uninstall_game_identity(root, helper.game_id)?;
    Ok(None)
}

fn publish_game_uninstall_receipt(helper: &crate::standalone::UninstallHelper) -> Result<()> {
    checked_uninstall_helper(helper)?;
    let root = &helper.root;
    let (done, bytes, journal, state) =
        read_uninstall_done(root)?.ok_or("Reçu de transaction de désinstallation absent")?;
    if journal.studio_only
        || state.active.core.kind != Kind::Applications
        || state.active.core.id != format!("game-{}", helper.game_id)
    {
        return Err("La transaction de désinstallation concerne un autre jeu".into());
    }
    let receipt = GameUninstallReceipt {
        schema: "forge.delivery.game-uninstalled.v2".into(),
        root: root.to_str().ok_or("Dossier non Unicode")?.to_owned(),
        game_id: helper.game_id,
        helper_receipt_sha256: helper.receipt_sha256.clone(),
        done_sha256: hash(&bytes),
        journal_sha256: done.journal_sha256,
        head_sha256: journal.head_sha256,
        report: done.report,
    };
    let expected = json(&receipt)?;
    if expected.len() as u64 > MAX_GAME_UNINSTALL_COMPLETION {
        return Err("Reçu final du programme temporaire trop long".into());
    }
    let directory = helper
        .executable
        .parent()
        .ok_or("Dossier temporaire absent")?;
    let path = manifest::confined(directory, GAME_UNINSTALL_COMPLETION)?;
    if existing(&path)?.is_some() {
        let (actual, _) = read_game_uninstall_receipt(helper)?
            .ok_or("Reçu final du programme temporaire absent")?;
        if json(&actual)? != expected {
            return Err("Reçu final divergent ; fichier conservé".into());
        }
    } else {
        let stage = format!(".forge-uninstall-completion-{}.tmp", crate::attempt()?);
        publish_uninstall_document(directory, GAME_UNINSTALL_COMPLETION, &stage, &expected)?;
    }
    Ok(())
}

/// Check the helper and its transaction while holding the installation lock.
/// A final receipt is historical evidence, not a cryptographic signature.
pub fn game_uninstall_helper_status(
    helper: &crate::standalone::UninstallHelper,
) -> Result<Option<GameUninstallCompletion>> {
    let _lock = acquire_existing_helper_lock(&helper.root)?;
    game_uninstall_helper_status_locked(helper)
}

/// Publish the helper's receipt before removing the service's final markers.
pub fn uninstall_game_with_helper(
    helper: &crate::standalone::UninstallHelper,
) -> Result<GameUninstallCompletion> {
    uninstall_game_with_helper_checked(helper, &mut |_| Ok(()))
}

fn uninstall_game_with_helper_checked(
    helper: &crate::standalone::UninstallHelper,
    checkpoint: &mut dyn FnMut(&str) -> Result<()>,
) -> Result<GameUninstallCompletion> {
    let root = &helper.root;
    let _lock = acquire_existing_helper_lock(root)?;
    if let Some(completion) = game_uninstall_helper_status_locked(helper)? {
        return Ok(completion);
    }
    uninstall_game_identity(root, helper.game_id)?;
    let state_path = manifest::confined(root, "installation.json")?;
    let lock_path = manifest::confined(root, "installation.lock")?;
    let mut transaction_checkpoint = |point: &str| -> Result<()> {
        if point == "before_finish" {
            publish_game_uninstall_receipt(helper)?;
            checkpoint("helper_completion")?;
        }
        checkpoint(point)?;
        if matches!(point, "before_finish" | "head" | "journal_removed")
            && (read_game_uninstall_receipt(helper)?.is_none()
                || game_uninstall_helper_status_locked(helper)?.is_some())
        {
            return Err("Reçu final du programme temporaire absent ou incohérent".into());
        }
        Ok(())
    };
    uninstall_locked_with(
        root,
        &state_path,
        &lock_path,
        false,
        &mut transaction_checkpoint,
    )?;
    game_uninstall_helper_status_locked(helper)?
        .ok_or_else(|| "Désinstallation non terminée ; programme temporaire conservé".into())
}

fn uninstall_mode(
    root: &Path,
    studio_only: bool,
    expected: Option<forge_core::ProjectId>,
) -> Result<UninstallReport> {
    if !root.is_absolute() {
        return Err("Installation root must be absolute".into());
    }
    manifest::safe_chain(root)?;
    let state_path = manifest::confined(root, "installation.json")?;
    let journal_path = manifest::confined(root, UNINSTALL_JOURNAL)?;
    let done_path = manifest::confined(root, UNINSTALL_DONE)?;
    if existing(&state_path)?.is_none()
        && existing(&journal_path)?.is_none()
        && existing(&done_path)?.is_none()
    {
        let retained = if studio_only {
            retained_studio_assets(root)?
        } else {
            None
        };
        if (studio_only && retained.is_none()) || expected.is_some() {
            return Err("Uninstall refused: installation state is missing".into());
        }
        return Ok(UninstallReport {
            root: root
                .to_str()
                .ok_or("Non-Unicode installation root")?
                .to_owned(),
            removed_files: 0,
            preserved_files: 0,
            preserved_packages: retained.as_ref().map_or(0, |assets| assets.packs.len()),
            counts_partial: false,
        });
    }
    let lock_path = manifest::confined(root, "installation.lock")?;
    // A pending journal is the only authority for resuming after the head is gone.
    let lock = acquire_uninstall(root)?;
    if let Some(game) = expected {
        uninstall_game_identity(root, game)?;
    }
    let result = uninstall_locked(root, &state_path, &lock_path, studio_only);
    drop(lock);
    // Keep the lock inode in place. Removing it after releasing the OS lock
    // would let another installer open a replacement inode before this
    // process removes the old path.
    result
}

struct Lock {
    _file: fs::File,
}

const LOCK_BUSY_PREFIX: &str = "Un autre processus modifie cette installation : ";

fn acquire_existing_helper_lock(root: &Path) -> Result<Lock> {
    if !root.is_absolute() {
        return Err("Installation root must be absolute".into());
    }
    manifest::safe_chain(root)?;
    if !existing(root)?.is_some_and(|metadata| metadata.is_dir()) {
        return Err("Dossier d’installation absent ; programme temporaire conservé".into());
    }
    let path = manifest::confined(root, "installation.lock")?;
    manifest::no_links(&path)?;
    let file = OpenOptions::new()
        .read(true)
        .write(true)
        .open(path)
        .map_err(|error| error.to_string())?;
    if !file
        .metadata()
        .map_err(|error| error.to_string())?
        .is_file()
    {
        return Err("Verrou d’installation invalide".into());
    }
    file.try_lock()
        .map_err(|error| format!("{LOCK_BUSY_PREFIX}{error}"))?;
    Ok(Lock { _file: file })
}

/// Identify the existing non-blocking lock failure without interpreting other I/O errors.
pub fn is_lock_contention(error: &str) -> bool {
    error.starts_with(LOCK_BUSY_PREFIX)
}

fn acquire_uninstall(root: &Path) -> Result<Lock> {
    manifest::safe_chain(root)?;
    fs::create_dir_all(root).map_err(|e| e.to_string())?;
    let lock = manifest::confined(root, "installation.lock")?;
    let file = OpenOptions::new()
        .create(true)
        .truncate(false)
        .read(true)
        .write(true)
        .open(lock)
        .map_err(|e| e.to_string())?;
    file.try_lock()
        .map_err(|e| format!("{LOCK_BUSY_PREFIX}{e}"))?;
    Ok(Lock { _file: file })
}

fn acquire(root: &Path) -> Result<Lock> {
    let lock = acquire_uninstall(root)?;
    ensure_no_uninstall(root)?;
    Ok(lock)
}

/// Share the installer lock across a final admission and native spawn boundary.
pub(crate) fn with_lock<T>(root: &Path, operation: impl FnOnce() -> Result<T>) -> Result<T> {
    let _lock = acquire(root)?;
    operation()
}

fn write_new(path: &Path, bytes: &[u8]) -> Result<()> {
    let mut file = OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(path)
        .map_err(|e| e.to_string())?;
    file.write_all(bytes).map_err(|e| e.to_string())?;
    file.sync_all().map_err(|e| e.to_string())
}

pub fn apply(plan: &Plan) -> Result<State> {
    apply_mode(plan, false)
}

/// Apply the Studio installer plan and record ownership before staging files.
pub fn apply_studio_installer(plan: &Plan) -> Result<State> {
    apply_mode(plan, true)
}

fn apply_mode(plan: &Plan, studio_installer: bool) -> Result<State> {
    if plan.schema != "forge.delivery.plan.v1"
        || !matches!(plan.operation.as_str(), "install" | "rollback")
        || plan.command_id != plan_id(plan)?
    {
        return Err("Invalid or modified installation plan".into());
    }
    manifest::valid_hash(&plan.expected_head)?;
    let root = Path::new(&plan.root);
    if !root.is_absolute() {
        return Err("Installation root must be absolute".into());
    }
    manifest::safe_chain(Path::new(&plan.user_root))?;
    // Reject a plan transported from another OS before creating installation
    // state or a staging directory. Full archive hashes are rechecked under lock.
    let mut guarded_input = false;
    for input in &plan.inputs {
        let source = Path::new(&input.archive);
        manifest::no_links(source)?;
        let mut file = fs::File::open(source).map_err(|e| e.to_string())?;
        let descriptor = archive::header(&mut file)?.0;
        descriptor.validate_host()?;
        guarded_input |= descriptor.product.as_ref().is_some_and(|product| {
            matches!(
                product.schema.as_str(),
                manifest::STANDALONE_PRODUCT_SCHEMA | manifest::STUDIO_PRODUCT_SCHEMA
            )
        });
    }
    let _lock = acquire(root)?;
    let (current, current_head) = head(root)?;
    if current.is_none() {
        crate::standalone::require_no_uninstall_helpers(root)?;
    }
    if !studio_installer
        && let Some(state) = &current
        && state
            .receipts
            .last()
            .is_some_and(|r| r.command_id == plan.command_id)
    {
        verify(root)?;
        return Ok(state.clone());
    }
    if current_head != plan.expected_head
        || current.as_ref().map_or(0, |s| s.revision) != plan.base_revision
    {
        return Err("Plan périmé : revalider avant application".into());
    }
    let guarded_transaction = guarded_input
        || current.as_ref().is_some_and(|state| {
            product(root, &state.active).is_ok_and(|product| {
                matches!(
                    product.schema.as_str(),
                    manifest::STANDALONE_PRODUCT_SCHEMA | manifest::STUDIO_PRODUCT_SCHEMA
                )
            })
        });
    // Check all inputs before copying any package. A changed later archive
    // must not leave an earlier package from an already invalidated plan.
    let mut inputs = Vec::with_capacity(plan.inputs.len());
    for input in &plan.inputs {
        manifest::identifier(&input.id)?;
        manifest::valid_hash(&input.manifest_sha256)?;
        manifest::valid_hash(&input.archive_sha256)?;
        let source = Path::new(&input.archive);
        if file_hash(source)? != input.archive_sha256 {
            return Err("Package changed after approval".into());
        }
        let (descriptor, revision) = archive::inspect(source)?;
        descriptor.validate_host()?;
        if descriptor.id != input.id || revision != input.manifest_sha256 {
            return Err("Package identity changed".into());
        }
        inputs.push((input, descriptor, revision));
    }
    if current.is_none()
        && let Some(retained) = retained_studio_assets(root)?
        && (retained.user_root != plan.user_root
            || retained
                .packs
                .values()
                .flatten()
                .any(|pack| plan.target.packs.get(&pack.id) != Some(pack))
            || !inputs.iter().any(|(_, descriptor, revision)| {
                descriptor.id == plan.target.core.id
                    && revision == &plan.target.core.revision
                    && descriptor.product.as_ref().is_some_and(|product| {
                        product.schema == manifest::STUDIO_PRODUCT_SCHEMA
                    })
            }))
    {
        return Err("Le plan ne correspond pas aux paquets Studio conservés.".into());
    }
    if studio_installer {
        let Some((input, descriptor, revision)) = inputs.first() else {
            return Err("L’installateur Studio attend son paquet d’application.".into());
        };
        let Some(product) = descriptor
            .product
            .as_ref()
            .filter(|product| product.schema == manifest::STUDIO_PRODUCT_SCHEMA)
        else {
            return Err("Le plan ne correspond pas à un paquet Studio.".into());
        };
        let mut admitted = BTreeSet::new();
        let valid_inputs = inputs[1..].iter().all(|(_, descriptor, revision)| {
            descriptor.kind == Kind::Assets
                && product.optional_packs.contains(&descriptor.id)
                && admitted.insert(&descriptor.id)
                && plan.target.packs.get(&descriptor.id).is_some_and(|pack| {
                    pack.kind == Kind::Assets && &pack.revision == revision
                })
        });
        if plan.operation != "install"
            || !valid_inputs
            || descriptor.kind != Kind::Applications
            || descriptor.id != plan.target.core.id
            || revision != &plan.target.core.revision
        {
            return Err("Le plan ne correspond pas à un paquet Studio.".into());
        }
        let marker = manifest::confined(root, ".studio-classic-installer-owned")?;
        match existing(&marker)? {
            Some(metadata) if metadata.is_file() => {}
            Some(_) => return Err("Marqueur d’installation Studio invalide.".into()),
            None => {
                check_studio_reinstall_root(root, Some(Path::new(&input.archive)))?;
                write_new(&marker, b"pending\n")?;
            }
        }
    }
    // NSIS may be relaunched after activation but before finishing Windows
    // integration. An identical Studio install must preserve the rollback.
    if let Some(state) = &current
        && state.active == plan.target
        && state.user_root == plan.user_root
        && inputs.iter().any(|(_, descriptor, revision)| {
            descriptor.id == state.active.core.id
                && revision == &state.active.core.revision
                && descriptor
                    .product
                    .as_ref()
                    .is_some_and(|product| product.schema == manifest::STUDIO_PRODUCT_SCHEMA)
        })
    {
        verify_snapshot(root, &state.active)?;
        return Ok(state.clone());
    }
    let mut created = Vec::new();
    for (input, descriptor, revision) in inputs {
        let source = Path::new(&input.archive);
        let pack = InstalledPack {
            id: input.id.clone(),
            revision,
            kind: descriptor.kind,
        };
        let destination = package_root(root, &pack)?;
        if destination.try_exists().map_err(|e| e.to_string())? {
            archive::verify_directory(&destination, &pack.revision)?;
            continue;
        }
        let parent = destination.parent().ok_or("No package parent")?;
        fs::create_dir_all(parent).map_err(|e| e.to_string())?;
        let stage = parent.join(format!(".stage-{}-{}", plan.command_id, crate::attempt()?));
        // Interrupted stages are retained; retries use fresh transaction-owned paths.
        archive::extract(source, &stage, &pack.revision)?;
        archive::verify_directory(&stage, &pack.revision)?;
        fs::rename(&stage, &destination).map_err(|e| e.to_string())?;
        created.push(pack);
    }
    let definition = match product(root, &plan.target) {
        Ok(definition) => definition,
        Err(error) => {
            return Err(rejected_transaction_error(
                error,
                guarded_transaction,
                root,
                &created,
                current.as_ref(),
                Path::new(&plan.user_root),
            ));
        }
    };
    let validation = verify_snapshot(root, &plan.target).and_then(|()| {
        if let Some(previous) = &current
            && definition.id != product(root, &previous.active)?.id
        {
            return Err("Different product: select a separate installation directory".into());
        }
        Ok(())
    });
    if let Err(error) = validation {
        return Err(rejected_transaction_error(
            error,
            guarded_transaction
                || matches!(
                    definition.schema.as_str(),
                    manifest::STANDALONE_PRODUCT_SCHEMA | manifest::STUDIO_PRODUCT_SCHEMA
                ),
            root,
            &created,
            current.as_ref(),
            Path::new(&plan.user_root),
        ));
    }
    if !Path::new(&plan.user_root).is_absolute() {
        return Err("User data root must be absolute".into());
    }
    let workspace_result = crate::launch::seed_workspace(
        root,
        &plan.target,
        Path::new(&plan.user_root),
        &plan.command_id,
    );
    if let Err(error) = workspace_result {
        if definition.schema == manifest::STUDIO_PRODUCT_SCHEMA {
            return Err(rejected_transaction_error(
                error,
                true,
                root,
                &created,
                current.as_ref(),
                Path::new(&plan.user_root),
            ));
        }
        return Err(error);
    }
    let revision = plan
        .base_revision
        .checked_add(1)
        .ok_or("Revision overflow")?;
    let mut receipts = current
        .as_ref()
        .map(|s| s.receipts.clone())
        .unwrap_or_default();
    if receipts.len() >= 1024 {
        return Err("Audit limit reached; export receipts before a new update".into());
    }
    receipts.push(Receipt {
        command_id: plan.command_id.clone(),
        base_revision: plan.base_revision,
        revision,
        operation: plan.operation.clone(),
    });
    let state = State {
        schema: "forge.delivery.installation.v1".into(),
        revision,
        user_root: plan.user_root.clone(),
        active: plan.target.clone(),
        previous: current.map(|s| s.active),
        receipts,
    };
    let temp = manifest::confined(
        root,
        &format!(".head-{}-{}.tmp", plan.command_id, crate::attempt()?),
    )?;
    write_new(&temp, &json(&state)?)?;
    fs::rename(temp, manifest::confined(root, "installation.json")?).map_err(|e| e.to_string())?;
    Ok(state)
}

pub fn verify_snapshot(root: &Path, snapshot: &Snapshot) -> Result<()> {
    if snapshot.core.kind != Kind::Applications || snapshot.packs.len() > 1024 {
        return Err("Invalid installed snapshot".into());
    }
    let definition = product(root, snapshot)?;
    if definition.schema == manifest::STANDALONE_PRODUCT_SCHEMA {
        crate::standalone::verify_snapshot(root, snapshot)?;
        return Ok(());
    }
    if definition.schema == manifest::STUDIO_PRODUCT_SCHEMA
        && (snapshot
            .packs
            .keys()
            .any(|id| !definition.optional_packs.contains(id))
            || snapshot
                .packs
                .values()
                .any(|pack| pack.kind != Kind::Assets))
    {
        return Err("Studio installation contains an unselected package".into());
    }
    archive::verify_directory(
        &package_root(root, &snapshot.core)?,
        &snapshot.core.revision,
    )?
    .validate_host()?;
    for (id, pack) in &snapshot.packs {
        if id != &pack.id || pack.kind == Kind::Applications {
            return Err("Invalid installed pack index".into());
        }
        let descriptor = archive::verify_directory(&package_root(root, pack)?, &pack.revision)?;
        descriptor.validate_host()?;
        if definition.schema == manifest::STUDIO_PRODUCT_SCHEMA
            && id == manifest::STUDIO_REFERENCE_ASSETS
        {
            manifest::reference_catalog(&descriptor)?;
        }
    }
    dependencies(&definition, snapshot)?;
    Ok(())
}

pub fn verify(root: &Path) -> Result<State> {
    let state = state(root)?;
    verify_snapshot(root, &state.active)?;
    Ok(state)
}

const PLAYER_RECEIPT: &str = ".forge-player-launcher.json";
const PLAYER_PENDING: &str = ".forge-player-launcher.pending.json";
const PLAYER_RECEIPT_LIMIT: u64 = 16 * 1024;

/// Whether a separate, durable entry needs an explicit create/repair action.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PlayerEntryStatus {
    Ready,
    NeedsRepair,
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
struct PlayerReceipt {
    schema: String,
    game_id: String,
    file: String,
    bytes: u64,
    sha256: String,
    origin: InstalledPack,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
struct PlayerPending {
    schema: String,
    previous_sha256: Option<String>,
    target: PlayerReceipt,
}

struct PlayerRecords {
    ready: Option<(PlayerReceipt, Vec<u8>)>,
    pending: Option<(PlayerPending, Vec<u8>)>,
    entry: Option<(u64, String)>,
}

pub const fn player_filename() -> &'static str {
    if cfg!(windows) { "Jouer.exe" } else { "Jouer" }
}

pub fn player_entry_path(root: &Path) -> Result<PathBuf> {
    manifest::confined(root, player_filename())
}

#[cfg(unix)]
fn player_executable(path: &Path) -> Result<bool> {
    use std::os::unix::fs::PermissionsExt;
    Ok(fs::metadata(path)
        .map_err(|error| error.to_string())?
        .permissions()
        .mode()
        & 0o100
        != 0)
}

#[cfg(not(unix))]
fn player_executable(path: &Path) -> Result<bool> {
    fs::metadata(path)
        .map(|_| true)
        .map_err(|error| error.to_string())
}

#[cfg(unix)]
fn set_player_executable(path: &Path) -> Result<()> {
    use std::os::unix::fs::PermissionsExt;
    fs::set_permissions(path, fs::Permissions::from_mode(0o755)).map_err(|error| error.to_string())
}

#[cfg(not(unix))]
fn set_player_executable(path: &Path) -> Result<()> {
    fs::metadata(path)
        .map(|_| ())
        .map_err(|error| error.to_string())
}

fn read_player_document<T: serde::de::DeserializeOwned + serde::Serialize>(
    root: &Path,
    name: &str,
) -> Result<Option<(T, Vec<u8>)>> {
    let path = manifest::confined(root, name)?;
    let Some(metadata) = existing(&path)? else {
        return Ok(None);
    };
    manifest::no_links(&path)?;
    if !metadata.is_file() || metadata.len() == 0 || metadata.len() > PLAYER_RECEIPT_LIMIT {
        return Err("Player entry receipt is not a bounded regular file".into());
    }
    let mut bytes = Vec::new();
    fs::File::open(&path)
        .map_err(|error| error.to_string())?
        .take(PLAYER_RECEIPT_LIMIT + 1)
        .read_to_end(&mut bytes)
        .map_err(|error| error.to_string())?;
    if bytes.len() as u64 > PLAYER_RECEIPT_LIMIT {
        return Err("Player entry receipt exceeds its read limit".into());
    }
    let value: T = serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    if json(&value)? != bytes {
        return Err("Player entry receipt is not canonical".into());
    }
    Ok(Some((value, bytes)))
}

fn validate_player_receipt(receipt: &PlayerReceipt, game_id: &str) -> Result<()> {
    if receipt.schema != "forge.delivery.player-entry.v1"
        || receipt.game_id != game_id
        || receipt.file != player_filename()
        || receipt.bytes == 0
        || receipt.bytes > manifest::MAX_FILE_BYTES
        || receipt.origin.id != format!("game-{game_id}")
        || receipt.origin.kind != Kind::Applications
    {
        return Err("Player entry ownership differs from this game".into());
    }
    manifest::valid_hash(&receipt.sha256)?;
    manifest::valid_hash(&receipt.origin.revision)
}

fn validate_pending_origin(root: &Path, receipt: &PlayerReceipt) -> Result<()> {
    let package = package_root(root, &receipt.origin)?;
    let descriptor = archive::verify_directory(&package, &receipt.origin.revision)?;
    descriptor.validate_host()?;
    let product = descriptor
        .product
        .as_ref()
        .ok_or("Player entry source product missing")?;
    if descriptor.id != receipt.origin.id
        || product.schema != manifest::STANDALONE_PRODUCT_SCHEMA
        || product.id != receipt.game_id
    {
        return Err("Player entry transaction source identity differs".into());
    }
    let launcher = product
        .apps
        .get("launcher")
        .ok_or("Player entry source launcher missing")?;
    if !descriptor.files.iter().any(|file| {
        file.path == launcher.executable
            && file.bytes == receipt.bytes
            && file.sha256 == receipt.sha256
    }) {
        return Err("Player entry transaction source pin differs".into());
    }
    Ok(())
}

fn player_records(root: &Path, game_id: &str) -> Result<PlayerRecords> {
    let ready: Option<(PlayerReceipt, Vec<u8>)> = read_player_document(root, PLAYER_RECEIPT)?;
    let pending: Option<(PlayerPending, Vec<u8>)> = read_player_document(root, PLAYER_PENDING)?;
    if let Some((receipt, _)) = &ready {
        validate_player_receipt(receipt, game_id)?;
        validate_pending_origin(root, receipt)?;
    }
    if let Some((transaction, _)) = &pending {
        if transaction.schema != "forge.delivery.player-entry-pending.v1" {
            return Err("Unsupported player entry transaction".into());
        }
        validate_player_receipt(&transaction.target, game_id)?;
        if let Some(previous) = &transaction.previous_sha256 {
            manifest::valid_hash(previous)?;
        }
        let previous = ready.as_ref().map(|(_, bytes)| hash(bytes));
        let already_promoted = ready
            .as_ref()
            .is_some_and(|(receipt, _)| receipt == &transaction.target);
        if !already_promoted && transaction.previous_sha256 != previous {
            return Err("Player entry transaction does not own its preceding receipt".into());
        }
        if !already_promoted {
            validate_pending_origin(root, &transaction.target)?;
        }
    }
    let path = manifest::confined(root, player_filename())?;
    let entry = if let Some(metadata) = existing(&path)? {
        manifest::no_links(&path)?;
        if !metadata.is_file() {
            return Err("Player entry is not a regular file".into());
        }
        if !ready
            .as_ref()
            .is_some_and(|(receipt, _)| receipt.bytes == metadata.len())
            && !pending
                .as_ref()
                .is_some_and(|(transaction, _)| transaction.target.bytes == metadata.len())
        {
            return Err("Player entry is foreign, modified or incomplete; preserve it".into());
        }
        Some((metadata.len(), file_hash(&path)?))
    } else {
        None
    };
    if let Some((bytes, sha256)) = &entry {
        let matches =
            |receipt: &PlayerReceipt| receipt.bytes == *bytes && receipt.sha256 == *sha256;
        if !ready.as_ref().is_some_and(|(receipt, _)| matches(receipt))
            && !pending
                .as_ref()
                .is_some_and(|(transaction, _)| matches(&transaction.target))
        {
            return Err("Player entry is foreign, modified or incomplete; preserve it".into());
        }
    }
    Ok(PlayerRecords {
        ready,
        pending,
        entry,
    })
}

fn capture_uninstall_player(root: &Path, state: &State) -> Result<UninstallPlayerPins> {
    let ready = read_player_document::<PlayerReceipt>(root, PLAYER_RECEIPT)?;
    let pending = read_player_document::<PlayerPending>(root, PLAYER_PENDING)?;
    if ready.is_none() && pending.is_none() {
        return Ok(UninstallPlayerPins::default());
    }
    let product = product(root, &state.active)?;
    if product.schema != manifest::STANDALONE_PRODUCT_SCHEMA {
        return Err("Player entry ownership does not match this installation".into());
    }
    let records = player_records(root, &product.id)?;
    let stage_sha256 = match &records.pending {
        Some((transaction, _)) => {
            inspect_player_stage(root, transaction)?.map(|(_, bytes)| hash(&bytes))
        }
        None => None,
    };
    Ok(UninstallPlayerPins {
        ready_sha256: records.ready.as_ref().map(|(_, bytes)| hash(bytes)),
        pending_sha256: records.pending.as_ref().map(|(_, bytes)| hash(bytes)),
        stage_sha256,
        entry: records
            .entry
            .map(|(bytes, sha256)| UninstallFilePin { bytes, sha256 }),
    })
}

fn verify_uninstall_player(root: &Path, pins: &UninstallPlayerPins) -> Result<()> {
    let ready = read_player_document::<PlayerReceipt>(root, PLAYER_RECEIPT)?;
    let pending = read_player_document::<PlayerPending>(root, PLAYER_PENDING)?;
    for (actual, expected) in [
        (
            ready.as_ref().map(|(_, bytes)| hash(bytes)),
            pins.ready_sha256.as_ref(),
        ),
        (
            pending.as_ref().map(|(_, bytes)| hash(bytes)),
            pins.pending_sha256.as_ref(),
        ),
    ] {
        if actual.as_ref().is_some_and(|value| Some(value) != expected) {
            return Err("Player entry ownership changed during uninstall".into());
        }
    }
    if let Some((transaction, _)) = &pending {
        let stage = inspect_player_stage(root, transaction)?.map(|(_, bytes)| hash(&bytes));
        if stage
            .as_ref()
            .is_some_and(|value| Some(value) != pins.stage_sha256.as_ref())
        {
            return Err("Player entry receipt stage changed during uninstall".into());
        }
    }
    if pins.ready_sha256.is_some() || pins.pending_sha256.is_some() {
        let path = manifest::confined(root, player_filename())?;
        if let Some(metadata) = existing(&path)? {
            let pin = pins
                .entry
                .as_ref()
                .ok_or("A foreign player entry appeared during uninstall")?;
            if !metadata.is_file() || metadata.len() != pin.bytes || file_hash(&path)? != pin.sha256
            {
                return Err("Player entry changed during uninstall; preserve it".into());
            }
        }
    }
    Ok(())
}

fn standalone_identity(root: &Path, state: &State, expected_id: &str) -> Result<()> {
    let application = crate::standalone::verify_snapshot(root, &state.active)?;
    if application.game().game_id().to_string() != expected_id {
        return Err("Installed game identity changed".into());
    }
    Ok(())
}

/// Revalidate the current core for the same game without consulting its export.
pub fn verified_player_core(root: &Path, expected_id: &str) -> Result<InstalledPack> {
    state(root)?;
    with_lock(root, || {
        let state = state(root)?;
        standalone_identity(root, &state, expected_id)?;
        Ok(state.active.core)
    })
}

/// Inspect ownership without publishing or repairing anything.
pub fn player_entry_status(root: &Path, expected_id: &str) -> Result<PlayerEntryStatus> {
    state(root)?;
    with_lock(root, || {
        let state = state(root)?;
        standalone_identity(root, &state, expected_id)?;
        let records = player_records(root, expected_id)?;
        Ok(
            if records.pending.is_none()
                && records.ready.is_some()
                && records.entry.is_some()
                && player_executable(&player_entry_path(root)?)?
            {
                PlayerEntryStatus::Ready
            } else {
                PlayerEntryStatus::NeedsRepair
            },
        )
    })
}

/// Caller holds `installation.lock` until the verified launcher is spawned.
pub(crate) fn check_player_entry_locked(
    root: &Path,
    state: &State,
    executable: &Path,
) -> Result<String> {
    let product = product(root, &state.active)?;
    if product.schema != manifest::STANDALONE_PRODUCT_SCHEMA {
        return Err("A standalone game installation is required".into());
    }
    standalone_identity(root, state, &product.id)?;
    let records = player_records(root, &product.id)?;
    if records.ready.is_none()
        || records.pending.is_some()
        || records.entry.is_none()
        || !player_executable(&player_entry_path(root)?)?
    {
        return Err("Player entry needs repair before opening".into());
    }
    manifest::safe_chain(executable)?;
    let expected = manifest::confined(root, player_filename())?;
    if fs::canonicalize(executable).map_err(|error| error.to_string())?
        != fs::canonicalize(expected).map_err(|error| error.to_string())?
    {
        return Err("Player entry executable differs from its receipt".into());
    }
    Ok(product.id)
}

fn player_source(root: &Path, state: &State, game_id: &str) -> Result<(PlayerReceipt, PathBuf)> {
    let package = package_root(root, &state.active.core)?;
    let descriptor = archive::verify_directory(&package, &state.active.core.revision)?;
    let product = descriptor
        .product
        .as_ref()
        .ok_or("Standalone product missing")?;
    let launcher = product
        .apps
        .get("launcher")
        .ok_or("Player launcher missing")?;
    let entry = descriptor
        .files
        .iter()
        .find(|entry| entry.path == launcher.executable)
        .ok_or("Player launcher inventory missing")?;
    let source = manifest::confined(&package, &entry.path)?;
    let receipt = PlayerReceipt {
        schema: "forge.delivery.player-entry.v1".into(),
        game_id: game_id.into(),
        file: player_filename().into(),
        bytes: entry.bytes,
        sha256: entry.sha256.clone(),
        origin: state.active.core.clone(),
    };
    validate_player_receipt(&receipt, game_id)?;
    Ok((receipt, source))
}

fn remove_exact_document(root: &Path, name: &str, bytes: &[u8]) -> Result<()> {
    let path = manifest::confined(root, name)?;
    manifest::no_links(&path)?;
    let metadata = fs::metadata(&path).map_err(|error| error.to_string())?;
    if !metadata.is_file()
        || metadata.len() != bytes.len() as u64
        || file_hash(&path)? != hash(bytes)
    {
        return Err("Player entry receipt changed during the transaction".into());
    }
    fs::remove_file(path).map_err(|error| error.to_string())?;
    Ok(())
}

fn inspect_player_stage(root: &Path, pending: &PlayerPending) -> Result<Option<(String, Vec<u8>)>> {
    let target = json(&pending.target)?;
    let name = format!(".forge-player-receipt-{}.tmp", hash(&target));
    if existing(&root.join(&name))?.is_none() {
        return Ok(None);
    }
    let staged = read_player_document::<PlayerReceipt>(root, &name)?
        .ok_or("Player entry receipt stage disappeared")?;
    if staged.1 != target {
        return Err("Player entry receipt stage is foreign or modified".into());
    }
    Ok(Some((name, target)))
}

fn remove_player_stage(root: &Path, pending: &PlayerPending) -> Result<bool> {
    let Some((name, target)) = inspect_player_stage(root, pending)? else {
        return Ok(false);
    };
    remove_exact_document(root, &name, &target)?;
    Ok(true)
}

fn promote_player_receipt(root: &Path, records: &PlayerRecords) -> Result<()> {
    let (pending, pending_bytes) = records
        .pending
        .as_ref()
        .ok_or("Player entry transaction missing")?;
    let target = &pending.target;
    if records.entry.as_ref() != Some(&(target.bytes, target.sha256.clone())) {
        return Err("Player entry transaction has no complete verified executable".into());
    }
    let ready_path = manifest::confined(root, PLAYER_RECEIPT)?;
    let new_bytes = json(target)?;
    if records
        .ready
        .as_ref()
        .is_some_and(|(ready, _)| ready == target)
    {
        remove_player_stage(root, pending)?;
        return remove_exact_document(root, PLAYER_PENDING, pending_bytes);
    }
    if let Some((_, old_bytes)) = &records.ready {
        if pending.previous_sha256.as_deref() != Some(hash(old_bytes).as_str())
            || read_player_document::<PlayerReceipt>(root, PLAYER_RECEIPT)?
                .is_none_or(|(_, bytes)| bytes != *old_bytes)
        {
            return Err("Player entry receipt changed before promotion".into());
        }
        let stage_name = format!(".forge-player-receipt-{}.tmp", hash(&new_bytes));
        let stage = manifest::confined(root, &stage_name)?;
        if stage.try_exists().map_err(|error| error.to_string())? {
            if read_player_document::<PlayerReceipt>(root, &stage_name)?
                .is_none_or(|(_, bytes)| bytes != new_bytes)
            {
                return Err("Player entry receipt stage is foreign or modified".into());
            }
        } else {
            write_new(&stage, &new_bytes)?;
        }
        fs::rename(stage, &ready_path).map_err(|error| error.to_string())?;
    } else {
        write_new(&ready_path, &new_bytes)?;
    }
    remove_player_stage(root, pending)?;
    remove_exact_document(root, PLAYER_PENDING, pending_bytes)
}

/// Create/repair only a proven player entry for the same reviewed game head.
pub fn ensure_player_entry(
    root: &Path,
    game_id: &str,
    expected: &InstalledPack,
) -> Result<PathBuf> {
    state(root)?;
    with_lock(root, || {
        let state = state(root)?;
        if &state.active.core != expected {
            return Err("Game installation changed; prepare installation again".into());
        }
        standalone_identity(root, &state, game_id)?;
        let mut records = player_records(root, game_id)?;
        if records.entry.is_some() {
            let path = player_entry_path(root)?;
            if !player_executable(&path)? {
                set_player_executable(&path)?;
            }
            if records.pending.is_some() {
                let pending_target = &records
                    .pending
                    .as_ref()
                    .ok_or("Missing pending receipt")?
                    .0
                    .target;
                if records.entry.as_ref()
                    == Some(&(pending_target.bytes, pending_target.sha256.clone()))
                {
                    promote_player_receipt(root, &records)?;
                } else if let Some((pending, pending_bytes)) = &records.pending {
                    remove_player_stage(root, pending)?;
                    remove_exact_document(root, PLAYER_PENDING, pending_bytes)?;
                }
            }
            return manifest::confined(root, player_filename());
        }
        let (target, source) = player_source(root, &state, game_id)?;
        if let Some((pending, bytes)) = &records.pending {
            remove_player_stage(root, pending)?;
            remove_exact_document(root, PLAYER_PENDING, bytes)?;
            records.pending = None;
        }
        let pending = PlayerPending {
            schema: "forge.delivery.player-entry-pending.v1".into(),
            previous_sha256: records.ready.as_ref().map(|(_, bytes)| hash(bytes)),
            target,
        };
        let pending_bytes = json(&pending)?;
        write_new(&manifest::confined(root, PLAYER_PENDING)?, &pending_bytes)?;
        let path = manifest::confined(root, player_filename())?;
        let mut output = OpenOptions::new()
            .create_new(true)
            .write(true)
            .open(&path)
            .map_err(|error| error.to_string())?;
        let package = package_root(root, &state.active.core)?;
        let copied = crate::vault_storage::copy_verified(
            &package,
            &source,
            pending.target.bytes,
            &pending.target.sha256,
            &mut output,
        )?;
        if copied != pending.target.bytes {
            return Err("Player entry copy changed".into());
        }
        output.sync_all().map_err(|error| error.to_string())?;
        drop(output);
        set_player_executable(&path)?;
        let records = player_records(root, game_id)?;
        promote_player_receipt(root, &records)?;
        Ok(path)
    })
}

fn remove_player_entry_before_uninstall(
    root: &Path,
    state: &State,
    protected: &Path,
) -> Result<(usize, usize)> {
    remove_player_entry_before_uninstall_with(root, state, protected, remove_if_matching)
}

fn remove_player_entry_before_uninstall_with(
    root: &Path,
    state: &State,
    protected: &Path,
    remove: impl FnOnce(&Path, &Path, u64, &str) -> Result<(bool, bool)>,
) -> Result<(usize, usize)> {
    if existing(&manifest::confined(root, PLAYER_RECEIPT)?)?.is_none()
        && existing(&manifest::confined(root, PLAYER_PENDING)?)?.is_none()
    {
        // An unaccompanied name is foreign and must not be touched.
        let foreign = existing(&root.join(player_filename()))?.is_some();
        return Ok((0, usize::from(foreign)));
    }
    let product = product(root, &state.active)?;
    if product.schema != manifest::STANDALONE_PRODUCT_SCHEMA {
        return Err("Player entry ownership does not match this installation".into());
    }
    let records = player_records(root, &product.id)?;
    let staged = match &records.pending {
        Some((pending, _)) => inspect_player_stage(root, pending)?,
        None => None,
    };
    let mut removed = 0;
    if let Some((bytes, sha256)) = &records.entry {
        let path = manifest::confined(root, player_filename())?;
        let (deleted, preserved) = remove(&path, protected, *bytes, sha256)?;
        if !deleted || preserved {
            return Err("Player entry could not be removed; close it and retry".into());
        }
        removed += 1;
    }
    if let Some((name, bytes)) = &staged {
        remove_exact_document(root, name, bytes)?;
        removed += 1;
    }
    if let Some((_, bytes)) = &records.pending {
        remove_exact_document(root, PLAYER_PENDING, bytes)?;
        removed += 1;
    }
    if let Some((_, bytes)) = &records.ready {
        remove_exact_document(root, PLAYER_RECEIPT, bytes)?;
        removed += 1;
    }
    Ok((removed, 0))
}

#[cfg(test)]
mod standalone_cleanup_tests {
    use super::*;
    use crate::standalone_fixture;

    #[test]
    fn rejected_cleanup_preserves_referenced_foreign_and_modified_files() {
        let fixture = standalone_fixture::Fixture::new();
        let root = fixture.installation("cleanup-protection");
        let app = fixture.application(&fixture.first, "cleanup-protection", "Jeu un", true);
        let (descriptor, revision) = archive::inspect(&app).unwrap();
        let pack = InstalledPack {
            id: descriptor.id,
            revision,
            kind: Kind::Applications,
        };
        let destination = package_root(&root, &pack).unwrap();
        std::fs::create_dir_all(destination.parent().unwrap()).unwrap();
        archive::extract(&app, &destination, &pack.revision).unwrap();
        let active = Snapshot {
            core: pack.clone(),
            packs: BTreeMap::new(),
        };
        let state = State {
            schema: "forge.delivery.installation.v1".into(),
            revision: 1,
            user_root: fixture.user.to_str().unwrap().into(),
            active: active.clone(),
            previous: Some(active),
            receipts: Vec::new(),
        };
        assert_eq!(
            cleanup_rejected_packages(
                &root,
                std::slice::from_ref(&pack),
                Some(&state),
                &fixture.user
            ),
            1
        );
        assert!(destination.join("launcher.bin").is_file());
        let foreign = destination.join("foreign.keep");
        std::fs::write(&foreign, b"personal").unwrap();
        assert_eq!(
            cleanup_rejected_packages(&root, std::slice::from_ref(&pack), None, &fixture.user),
            1
        );
        assert_eq!(std::fs::read(&foreign).unwrap(), b"personal");
        std::fs::remove_file(foreign).unwrap();
        std::fs::write(destination.join("launcher.bin"), b"modified").unwrap();
        assert_eq!(
            cleanup_rejected_packages(&root, &[pack], None, &fixture.user),
            1
        );
        assert_eq!(
            std::fs::read(destination.join("launcher.bin")).unwrap(),
            b"modified"
        );
        assert!(destination.join("package-manifest.json").is_file());
    }

    fn player_fixture(name: &str) -> (standalone_fixture::Fixture, PathBuf, State) {
        let fixture = standalone_fixture::Fixture::new();
        let root = fixture.installation(name);
        let state = apply(&fixture.prepare_new(&fixture.first, name, "Jeu autonome")).unwrap();
        (fixture, root, state)
    }

    fn revised_launcher(fixture: &standalone_fixture::Fixture, name: &str) -> PathBuf {
        let original = fixture.application(
            &fixture.first,
            &format!("{name}-revision"),
            "Jeu autonome",
            false,
        );
        let (descriptor, revision) = archive::inspect(&original).unwrap();
        let source = fixture.root().join(format!("player-entry-source-{name}"));
        archive::extract(&original, &source, &revision).unwrap();
        fs::remove_file(source.join("package-manifest.json")).unwrap();
        fs::write(source.join("launcher.bin"), b"updated inert launcher").unwrap();
        let output = fixture
            .root()
            .join(format!("player-entry-update-{name}.forgepack"));
        archive::pack(&source, descriptor, &output).unwrap();
        output
    }

    #[test]
    fn player_entry_survives_updates_and_repairs_missing_old_version() {
        let (fixture, root, first) = player_fixture("entry-update");
        let id = fixture.first.project.to_string();
        assert_eq!(
            player_entry_status(&root, &id).unwrap(),
            PlayerEntryStatus::NeedsRepair
        );
        let entry = ensure_player_entry(&root, &id, &first.active.core).unwrap();
        let original = fs::read(&entry).unwrap();
        assert_eq!(
            player_entry_status(&root, &id).unwrap(),
            PlayerEntryStatus::Ready
        );
        #[cfg(unix)]
        {
            use std::os::unix::fs::PermissionsExt;
            assert_ne!(
                fs::metadata(&entry).unwrap().permissions().mode() & 0o111,
                0
            );
        }
        assert!(
            ensure_player_entry(
                &root,
                &fixture.second.project.to_string(),
                &first.active.core
            )
            .is_err()
        );
        let update = revised_launcher(&fixture, "entry-update");
        let updated = apply(&prepare(&root, &[update]).unwrap()).unwrap();
        assert!(ensure_player_entry(&root, &id, &first.active.core).is_err());
        assert_eq!(fs::read(&entry).unwrap(), original);
        assert_eq!(
            ensure_player_entry(&root, &id, &updated.active.core).unwrap(),
            entry
        );
        assert_eq!(fs::read(&entry).unwrap(), original);
        fs::remove_file(&entry).unwrap();
        let ready_bytes = fs::read(root.join(PLAYER_RECEIPT)).unwrap();
        let old_receipt: PlayerReceipt = serde_json::from_slice(&ready_bytes).unwrap();
        let interrupted = PlayerPending {
            schema: "forge.delivery.player-entry-pending.v1".into(),
            previous_sha256: Some(hash(&ready_bytes)),
            target: old_receipt,
        };
        write_new(&root.join(PLAYER_PENDING), &json(&interrupted).unwrap()).unwrap();
        assert_eq!(
            player_entry_status(&root, &id).unwrap(),
            PlayerEntryStatus::NeedsRepair
        );
        ensure_player_entry(&root, &id, &updated.active.core).unwrap();
        assert_eq!(fs::read(&entry).unwrap(), b"updated inert launcher");
        let rolled_back = apply(&prepare_rollback(&root).unwrap()).unwrap();
        assert_eq!(rolled_back.active.core, first.active.core);
        assert_eq!(
            player_entry_status(&root, &id).unwrap(),
            PlayerEntryStatus::Ready
        );
        assert_eq!(fs::read(&entry).unwrap(), b"updated inert launcher");
        let report = uninstall(&root).unwrap();
        assert!(report.removed_files >= 2);
        assert!(!entry.exists());
        assert!(!root.join(PLAYER_RECEIPT).exists());
    }

    #[test]
    fn pending_complete_entry_recovers_and_partial_entry_is_preserved() {
        let (fixture, root, state) = player_fixture("entry-interruption");
        let id = fixture.first.project.to_string();
        let (target, source) = player_source(&root, &state, &id).unwrap();
        let pending = PlayerPending {
            schema: "forge.delivery.player-entry-pending.v1".into(),
            previous_sha256: None,
            target: target.clone(),
        };
        write_new(&root.join(PLAYER_PENDING), &json(&pending).unwrap()).unwrap();
        let entry = root.join(player_filename());
        fs::write(&entry, b"partial").unwrap();
        let before = fs::read(root.join("installation.json")).unwrap();
        assert!(ensure_player_entry(&root, &id, &state.active.core).is_err());
        assert!(uninstall(&root).is_err());
        assert_eq!(fs::read(&entry).unwrap(), b"partial");
        assert_eq!(fs::read(root.join("installation.json")).unwrap(), before);
        fs::remove_file(&entry).unwrap();
        fs::write(&entry, fs::read(source).unwrap()).unwrap();
        ensure_player_entry(&root, &id, &state.active.core).unwrap();
        assert_eq!(
            player_entry_status(&root, &id).unwrap(),
            PlayerEntryStatus::Ready
        );
        assert!(!root.join(PLAYER_PENDING).exists());
    }

    #[test]
    fn completed_repair_promotes_a_prepared_receipt_after_interruption() {
        let (fixture, root, first) = player_fixture("entry-promotion");
        let id = fixture.first.project.to_string();
        let entry = ensure_player_entry(&root, &id, &first.active.core).unwrap();
        let update = revised_launcher(&fixture, "entry-promotion");
        let updated = apply(&prepare(&root, &[update]).unwrap()).unwrap();
        let old = fs::read(root.join(PLAYER_RECEIPT)).unwrap();
        fs::remove_file(&entry).unwrap();
        let (target, source) = player_source(&root, &updated, &id).unwrap();
        let pending = PlayerPending {
            schema: "forge.delivery.player-entry-pending.v1".into(),
            previous_sha256: Some(hash(&old)),
            target: target.clone(),
        };
        write_new(&root.join(PLAYER_PENDING), &json(&pending).unwrap()).unwrap();
        fs::write(&entry, fs::read(source).unwrap()).unwrap();
        let new_receipt = json(&target).unwrap();
        let stage = root.join(format!(".forge-player-receipt-{}.tmp", hash(&new_receipt)));
        write_new(&stage, &new_receipt).unwrap();
        ensure_player_entry(&root, &id, &updated.active.core).unwrap();
        assert_eq!(fs::read(root.join(PLAYER_RECEIPT)).unwrap(), new_receipt);
        assert!(!root.join(PLAYER_PENDING).exists());
        assert!(!stage.exists());
        assert_eq!(
            player_entry_status(&root, &id).unwrap(),
            PlayerEntryStatus::Ready
        );
    }

    #[test]
    fn foreign_entry_is_preserved_and_owned_remove_failure_changes_nothing() {
        let (fixture, root, state) = player_fixture("entry-foreign");
        let id = fixture.first.project.to_string();
        let entry = root.join(player_filename());
        fs::write(&entry, b"foreign").unwrap();
        assert!(ensure_player_entry(&root, &id, &state.active.core).is_err());
        assert_eq!(fs::read(&entry).unwrap(), b"foreign");
        fs::remove_file(&entry).unwrap();
        ensure_player_entry(&root, &id, &state.active.core).unwrap();
        let head = fs::read(root.join("installation.json")).unwrap();
        let owner = fs::read(root.join(PLAYER_RECEIPT)).unwrap();
        let outcome = remove_player_entry_before_uninstall_with(
            &root,
            &state,
            &fixture.user,
            |_, _, _, _| Err("locked".into()),
        );
        assert!(outcome.is_err());
        assert!(entry.is_file());
        assert_eq!(fs::read(root.join("installation.json")).unwrap(), head);
        assert_eq!(fs::read(root.join(PLAYER_RECEIPT)).unwrap(), owner);
        assert!(package_root(&root, &state.active.core).unwrap().is_dir());
        fs::write(&entry, b"modified personal file").unwrap();
        assert!(uninstall(&root).is_err());
        assert_eq!(fs::read(&entry).unwrap(), b"modified personal file");
        assert_eq!(fs::read(root.join("installation.json")).unwrap(), head);
        assert_eq!(fs::read(root.join(PLAYER_RECEIPT)).unwrap(), owner);

        let (_other, foreign_root, _foreign_state) = player_fixture("entry-unowned");
        let foreign_entry = foreign_root.join(player_filename());
        fs::write(&foreign_entry, b"unowned file").unwrap();
        let report = uninstall(&foreign_root).unwrap();
        assert!(report.preserved_files >= 1);
        assert_eq!(fs::read(foreign_entry).unwrap(), b"unowned file");
    }

    fn interrupt_uninstall(root: &Path, point: &str, studio_only: bool) {
        let state_path = root.join("installation.json");
        let lock_path = root.join("installation.lock");
        let lock = acquire_uninstall(root).unwrap();
        let mut reached = false;
        let result = uninstall_locked_with(root, &state_path, &lock_path, studio_only, &mut |at| {
            if at == point {
                reached = true;
                return Err(format!("interrupted at {at}"));
            }
            Ok(())
        });
        drop(lock);
        assert!(reached, "checkpoint was not reached: {point}");
        assert!(result.is_err());
        assert!(root.join(UNINSTALL_JOURNAL).is_file() || root.join(UNINSTALL_DONE).is_file());
    }

    #[test]
    fn game_uninstall_keeps_its_identity_across_interruption() {
        for point in ["journal", "manifest", "head", "journal_removed"] {
            let (fixture, root, state) = player_fixture(&format!("game-uninstall-{point}"));
            let head = fs::read(root.join("installation.json")).unwrap();
            assert!(uninstall_game(&root, fixture.second.project).is_err());
            assert_eq!(fs::read(root.join("installation.json")).unwrap(), head);
            assert!(package_root(&root, &state.active.core).unwrap().is_dir());
            interrupt_uninstall(&root, point, false);
            assert!(uninstall_game(&root, fixture.second.project).is_err());
            assert!(root.join(UNINSTALL_JOURNAL).exists() || root.join(UNINSTALL_DONE).exists());
            uninstall_game(&root, fixture.first.project).unwrap();
            assert!(!root.join("installation.json").exists());
            assert!(!root.join(UNINSTALL_JOURNAL).exists());
            assert!(!root.join(UNINSTALL_DONE).exists());
        }
    }

    #[test]
    fn old_uninstall_reports_keep_their_canonical_encoding() {
        let legacy =
            br#"{"root":"example","removed_files":2,"preserved_files":1,"preserved_packages":0}"#;
        let mut report: UninstallReport = serde_json::from_slice(legacy).unwrap();
        assert!(!report.counts_partial);
        assert_eq!(json(&report).unwrap().as_slice(), legacy);
        report.counts_partial = true;
        let partial = json(&report).unwrap();
        assert!(
            String::from_utf8(partial.clone())
                .unwrap()
                .contains("\"counts_partial\":true")
        );
        assert!(
            serde_json::from_slice::<UninstallReport>(&partial)
                .unwrap()
                .counts_partial
        );
    }

    #[test]
    fn uninstall_preserves_manifest_for_an_existing_or_new_protected_subtree() {
        for scenario in ["already-present", "appears-during-removal", "absent"] {
            let (fixture, root, mut state) =
                player_fixture(&format!("protected-subtree-{scenario}"));
            let package = package_root(&root, &state.active.core).unwrap();
            let manifest = package.join("package-manifest.json");
            let original_manifest = fs::read(&manifest).unwrap();
            let protected = package.join("userdata");
            let save = protected.join("save.dat");
            state.user_root = protected.to_str().unwrap().to_owned();
            fs::write(root.join("installation.json"), json(&state).unwrap()).unwrap();
            if scenario == "already-present" {
                fs::create_dir(&protected).unwrap();
                fs::write(&save, b"unique user data").unwrap();
            }
            let state_path = root.join("installation.json");
            let lock_path = root.join("installation.lock");
            let lock = acquire_uninstall(&root).unwrap();
            let mut inserted = false;
            let report =
                uninstall_locked_with(&root, &state_path, &lock_path, false, &mut |point| {
                    if scenario == "appears-during-removal" && point == "file" && !inserted {
                        fs::create_dir(&protected).unwrap();
                        fs::write(&save, b"unique user data").unwrap();
                        inserted = true;
                    }
                    Ok(())
                })
                .unwrap();
            drop(lock);
            if scenario == "absent" {
                assert!(!protected.exists());
                assert!(!manifest.exists());
            } else {
                assert!(scenario != "appears-during-removal" || inserted);
                assert_eq!(fs::read(&save).unwrap(), b"unique user data");
                assert_eq!(fs::read(&manifest).unwrap(), original_manifest);
                assert!(report.preserved_packages > 0);
            }
            drop(fixture);
        }
    }

    #[cfg(windows)]
    #[test]
    fn uninstall_resumes_for_the_same_physical_root_in_different_forms() {
        for point in ["journal", "done", "head", "journal_removed"] {
            let (fixture, root, _) = player_fixture(&format!("cli-root-form-{point}"));
            interrupt_uninstall(&root, point, false);
            let alternate = fs::canonicalize(&root).unwrap();
            uninstall_game(&alternate, fixture.first.project).unwrap();
            assert!(!root.join("installation.json").exists());
            assert!(!root.join(UNINSTALL_JOURNAL).exists());
            assert!(!root.join(UNINSTALL_DONE).exists());
        }
    }

    #[test]
    fn helper_receipt_survives_each_uninstall_completion_boundary() {
        for point in [
            "journal",
            "file",
            "manifest",
            "done",
            "helper_completion",
            "head",
            "journal_removed",
            "done_removed",
        ] {
            let name = format!("helper-interruption-{point}");
            let (fixture, root, _) = player_fixture(&name);
            let helper =
                crate::standalone::prepare_game_uninstall_helper(&root, fixture.first.project)
                    .unwrap();
            assert!(game_uninstall_helper_status(&helper).unwrap().is_none());
            let mut reached = false;
            let interrupted = uninstall_game_with_helper_checked(&helper, &mut |at| {
                if at == point {
                    reached = true;
                    return Err("injected interruption".into());
                }
                Ok(())
            });
            assert!(reached, "checkpoint not reached: {point}");
            assert!(interrupted.is_err());
            let after_interrupt = game_uninstall_helper_status(&helper).unwrap();
            assert_eq!(after_interrupt.is_some(), point == "done_removed");
            let completed = uninstall_game_with_helper(&helper).unwrap();
            assert_eq!(
                completed.report.counts_partial,
                matches!(point, "journal" | "file" | "manifest")
            );
            assert_eq!(
                game_uninstall_helper_status(&helper).unwrap(),
                Some(completed.clone())
            );
            assert_eq!(uninstall_game_with_helper(&helper).unwrap(), completed);
            assert!(!root.join("installation.json").exists());
            assert!(!root.join(UNINSTALL_JOURNAL).exists());
            assert!(!root.join(UNINSTALL_DONE).exists());
        }
    }

    #[test]
    fn helper_refuses_foreign_receipts_and_a_later_reinstallation() {
        let name = "helper-foreign-and-reinstallation";
        let (fixture, root, _) = player_fixture(name);
        let helper =
            crate::standalone::prepare_game_uninstall_helper(&root, fixture.first.project).unwrap();
        let receipt = helper
            .executable
            .parent()
            .unwrap()
            .join(GAME_UNINSTALL_COMPLETION);
        fs::write(&receipt, b"foreign incomplete receipt").unwrap();
        let head = fs::read(root.join("installation.json")).unwrap();
        assert!(game_uninstall_helper_status(&helper).is_err());
        assert!(uninstall_game_with_helper(&helper).is_err());
        assert_eq!(fs::read(&receipt).unwrap(), b"foreign incomplete receipt");
        assert_eq!(fs::read(root.join("installation.json")).unwrap(), head);
        fs::remove_file(&receipt).unwrap();
        let completed = uninstall_game_with_helper(&helper).unwrap();
        let original_receipt = fs::read(&receipt).unwrap();
        fs::write(&receipt, b"changed after completion").unwrap();
        assert!(game_uninstall_helper_status(&helper).is_err());
        fs::write(&receipt, &original_receipt).unwrap();
        assert_eq!(
            game_uninstall_helper_status(&helper).unwrap(),
            Some(completed)
        );

        // Simulate a legacy installer restoring a head despite the remaining
        // helper. Current installers refuse that root before publication.
        fs::write(root.join("installation.json"), &head).unwrap();
        let reinstalled_head = fs::read(root.join("installation.json")).unwrap();
        assert!(game_uninstall_helper_status(&helper).is_err());
        assert!(uninstall_game_with_helper(&helper).is_err());
        assert_eq!(
            fs::read(root.join("installation.json")).unwrap(),
            reinstalled_head
        );
        assert_eq!(fs::read(receipt).unwrap(), original_receipt);
    }

    #[test]
    fn remaining_helpers_refuse_root_reuse_before_staging_but_allow_updates() {
        let fixture = standalone_fixture::Fixture::new();
        let name = "helper-root-reuse";
        let root = fixture.installation(name);
        let initial = fixture.prepare_new(&fixture.first, name, "Jeu autonome");
        apply(&initial).unwrap();
        let first =
            crate::standalone::prepare_game_uninstall_helper(&root, fixture.first.project).unwrap();
        let second =
            crate::standalone::prepare_game_uninstall_helper(&root, fixture.first.project).unwrap();
        let archives = initial
            .inputs
            .iter()
            .map(|input| PathBuf::from(&input.archive))
            .collect::<Vec<_>>();
        let update = prepare(&root, &archives).unwrap();
        assert_eq!(apply(&update).unwrap().revision, 2);
        fs::create_dir_all(&fixture.user).unwrap();
        let save = fixture.user.join("personal-save");
        fs::write(&save, b"player progress").unwrap();
        uninstall_game_with_helper(&second).unwrap();
        assert!(first.executable.is_file());
        let first_hash = file_hash(&first.executable).unwrap();
        assert!(
            prepare(&root, &archives)
                .unwrap_err()
                .contains("désinstallation")
        );
        // A valid plan reviewed before the old installation also rechecks the
        // guard under the lock, before extracting or publishing any package.
        assert!(apply(&initial).unwrap_err().contains("désinstallation"));
        assert!(!root.join("installation.json").exists());
        assert!(!package_root(&root, &initial.target.core).unwrap().exists());
        for pack in initial.target.packs.values() {
            assert!(!package_root(&root, pack).unwrap().exists());
        }
        assert_eq!(file_hash(&first.executable).unwrap(), first_hash);
        assert_eq!(fs::read(&save).unwrap(), b"player progress");
        // Model explicit retirement of these inert fixture helpers. Neither
        // the refusal nor the installer removes a real helper automatically.
        fs::remove_dir_all(first.executable.parent().unwrap()).unwrap();
        fs::remove_dir_all(second.executable.parent().unwrap()).unwrap();
        apply(&initial).unwrap();
        assert_eq!(fs::read(save).unwrap(), b"player progress");
    }

    #[test]
    fn helper_preserves_final_markers_if_its_receipt_is_replaced_during_completion() {
        for point in ["helper_completion", "head", "journal_removed"] {
            let (fixture, root, _) = player_fixture(&format!("helper-replaced-{point}"));
            let helper =
                crate::standalone::prepare_game_uninstall_helper(&root, fixture.first.project)
                    .unwrap();
            let path = helper
                .executable
                .parent()
                .unwrap()
                .join(GAME_UNINSTALL_COMPLETION);
            let mut original = None;
            let result = uninstall_game_with_helper_checked(&helper, &mut |at| {
                if at == point {
                    original = Some(fs::read(&path).unwrap());
                    fs::write(&path, b"foreign replacement").unwrap();
                }
                Ok(())
            });
            assert!(result.is_err(), "changed receipt accepted at {point}");
            assert_eq!(fs::read(&path).unwrap(), b"foreign replacement");
            assert!(root.join(UNINSTALL_DONE).is_file());
            fs::write(&path, original.expect("checkpoint not reached")).unwrap();
            uninstall_game_with_helper(&helper).unwrap();
            assert!(game_uninstall_helper_status(&helper).unwrap().is_some());
        }
    }

    #[test]
    fn helper_status_and_uninstall_refuse_a_busy_installation_lock() {
        let (fixture, root, _) = player_fixture("helper-lock");
        let helper =
            crate::standalone::prepare_game_uninstall_helper(&root, fixture.first.project).unwrap();
        let lock = acquire_uninstall(&root).unwrap();
        assert!(game_uninstall_helper_status(&helper).is_err());
        assert!(uninstall_game_with_helper(&helper).is_err());
        drop(lock);
        assert!(game_uninstall_helper_status(&helper).unwrap().is_none());
    }

    #[test]
    fn helper_does_not_recreate_a_missing_root_or_lock() {
        let (fixture, root, _) = player_fixture("helper-missing-root");
        let helper =
            crate::standalone::prepare_game_uninstall_helper(&root, fixture.first.project).unwrap();
        let missing_root = root.join("missing-installation");
        let mut moved = helper.clone();
        moved.root.clone_from(&missing_root);
        assert!(game_uninstall_helper_status(&moved).is_err());
        assert!(uninstall_game_with_helper(&moved).is_err());
        assert!(!missing_root.exists());

        let lock_path = root.join("installation.lock");
        fs::remove_file(&lock_path).unwrap();
        assert!(game_uninstall_helper_status(&helper).is_err());
        assert!(uninstall_game_with_helper(&helper).is_err());
        assert!(!lock_path.exists());
        assert!(root.join("installation.json").is_file());
    }

    #[cfg(windows)]
    #[test]
    fn helper_verbatim_root_preserves_nested_user_data_and_future_paths() {
        let (fixture, root, mut state) = player_fixture("helper-protected-windows");
        let protected = package_root(&root, &state.active.core).unwrap();
        let owned_file = protected.join("launcher.bin");
        let original = fs::read(&owned_file).unwrap();
        state.user_root = protected.to_str().unwrap().to_owned();
        fs::write(root.join("installation.json"), json(&state).unwrap()).unwrap();

        let helper =
            crate::standalone::prepare_game_uninstall_helper(&root, fixture.first.project).unwrap();
        let future = protected.join("future-user").join("saves");
        assert!(!future.exists());
        let mapped = protected_path_for_root(&helper.root, &future).unwrap();
        assert!(!future.exists());
        assert!(path_is_within(&mapped, &helper.root));
        assert!(path_is_within(&mapped.join("save.dat"), &mapped));
        fs::create_dir_all(&future).unwrap();
        let save = future.join("save.dat");
        fs::write(&save, b"progression utilisateur").unwrap();
        let (removed, kept) = remove_if_matching(
            &mapped.join("save.dat"),
            &mapped,
            b"progression utilisateur".len() as u64,
            &hash(b"progression utilisateur"),
        )
        .unwrap();
        assert!(!removed && kept);

        let result = uninstall_game_with_helper(&helper).unwrap();
        assert!(result.report.preserved_files > 0);
        assert_eq!(fs::read(owned_file).unwrap(), original);
        assert_eq!(fs::read(save).unwrap(), b"progression utilisateur");
        assert!(protected.join("package-manifest.json").is_file());
    }

    #[cfg(windows)]
    #[test]
    fn protected_path_mapping_recognizes_windows_prefix_and_case() {
        let (fixture, root, state) = player_fixture("protected-path-windows");
        let protected = package_root(&root, &state.active.core).unwrap();
        let canonical = fs::canonicalize(&protected).unwrap();
        let changed_case = PathBuf::from(protected.to_str().unwrap().to_ascii_uppercase());
        let mapped =
            protected_path_for_root(&fs::canonicalize(&root).unwrap(), &changed_case).unwrap();
        assert_eq!(mapped, canonical);
        assert!(path_is_within(&canonical, &changed_case));

        // A package whose files overlap a protected root must not be swept
        // during cleanup of a failed installation transaction either.
        let pack = state.active.core.clone();
        let file = protected.join("launcher.bin");
        let bytes = fs::read(&file).unwrap();
        assert!(
            !remove_rejected_package(&fs::canonicalize(&root).unwrap(), &pack, &[mapped]).unwrap()
        );
        assert_eq!(fs::read(file).unwrap(), bytes);
        drop(fixture);
    }

    #[test]
    fn game_uninstall_preserves_a_modified_module_file() {
        let (fixture, root, state) = player_fixture("game-uninstall-modified-module");
        let module = &state.active.packs[&fixture.first.module.to_string()];
        let path = package_root(&root, module).unwrap().join("module.json");
        assert!(path.is_file());
        fs::write(&path, b"personal edit").unwrap();
        let report = uninstall_game(&root, fixture.first.project).unwrap();
        assert!(report.preserved_files > 0);
        assert_eq!(fs::read(path).unwrap(), b"personal edit");
    }

    #[test]
    fn uninstall_resumes_at_each_boundary_and_blocks_other_operations() {
        for point in [
            "journal",
            "player",
            "file",
            "manifest",
            "done",
            "head",
            "journal_removed",
        ] {
            let name = format!("uninstall-recovery-{point}");
            let (fixture, root, installed) = player_fixture(&name);
            let entry = ensure_player_entry(
                &root,
                &fixture.first.project.to_string(),
                &installed.active.core,
            )
            .unwrap();
            let update = revised_launcher(&fixture, &format!("{name}-blocked"));
            let plan = prepare(&root, &[update]).unwrap();
            interrupt_uninstall(&root, point, false);
            assert!(state_unchecked(&root).is_ok() || matches!(point, "head" | "journal_removed"));
            assert!(state(&root).is_err());
            assert!(
                prepare_rollback(&root)
                    .unwrap_err()
                    .contains("désinstallation")
            );
            assert!(acquire(&root).is_err());
            assert!(apply(&plan).unwrap_err().contains("désinstallation"));
            uninstall(&root).unwrap();
            assert!(!root.join(UNINSTALL_JOURNAL).exists());
            assert!(!root.join(UNINSTALL_DONE).exists());
            assert!(!root.join("installation.json").exists());
            assert!(!entry.exists());
            uninstall(&root).unwrap();
        }
    }

    #[test]
    fn uninstall_refuses_a_head_removed_before_the_completion_receipt() {
        let (fixture, root, state) = player_fixture("uninstall-head-missing");
        let entry = ensure_player_entry(
            &root,
            &fixture.first.project.to_string(),
            &state.active.core,
        )
        .unwrap();
        interrupt_uninstall(&root, "journal", false);
        let head = root.join("installation.json");
        let original = fs::read(&head).unwrap();
        fs::remove_file(&head).unwrap();
        assert!(uninstall(&root).is_err());
        assert!(root.join(UNINSTALL_JOURNAL).is_file());
        assert!(!root.join(UNINSTALL_DONE).exists());
        assert!(entry.is_file());
        assert!(package_root(&root, &state.active.core).unwrap().is_dir());
        fs::write(&head, original).unwrap();
        uninstall(&root).unwrap();
    }

    #[test]
    fn completed_uninstall_recovery_never_revisits_package_contents() {
        let (_fixture, root, state) = player_fixture("uninstall-done-is-final");
        let package = package_root(&root, &state.active.core).unwrap();
        interrupt_uninstall(&root, "done", false);
        fs::create_dir_all(&package).unwrap();
        let foreign = package.join("personal.keep");
        fs::write(&foreign, b"personal").unwrap();
        uninstall(&root).unwrap();
        assert_eq!(fs::read(foreign).unwrap(), b"personal");
        assert!(!root.join(UNINSTALL_JOURNAL).exists());
        assert!(!root.join(UNINSTALL_DONE).exists());
    }

    #[test]
    fn uninstall_is_complete_if_interrupted_after_its_final_marker() {
        let (_fixture, root, _state) = player_fixture("uninstall-after-completion");
        let lock = acquire_uninstall(&root).unwrap();
        let mut reached = false;
        let result = uninstall_locked_with(
            &root,
            &root.join("installation.json"),
            &root.join("installation.lock"),
            false,
            &mut |point| {
                if point == "done_removed" {
                    reached = true;
                    return Err("interrupted after completion".into());
                }
                Ok(())
            },
        );
        drop(lock);
        assert!(reached && result.is_err());
        assert!(!root.join("installation.json").exists());
        assert!(!root.join(UNINSTALL_JOURNAL).exists());
        assert!(!root.join(UNINSTALL_DONE).exists());
        uninstall(&root).unwrap();
    }

    #[test]
    fn uninstall_preserves_an_altered_completion_receipt() {
        let (_fixture, root, _state) = player_fixture("uninstall-altered-completion");
        interrupt_uninstall(&root, "done", false);
        let done = root.join(UNINSTALL_DONE);
        fs::write(&done, b"personal data").unwrap();
        assert!(uninstall(&root).is_err());
        assert_eq!(fs::read(done).unwrap(), b"personal data");
        assert!(root.join(UNINSTALL_JOURNAL).is_file());
        assert!(root.join("installation.json").is_file());
    }

    #[test]
    fn uninstall_rechecks_both_markers_before_removing_the_head() {
        for name in [UNINSTALL_JOURNAL, UNINSTALL_DONE] {
            let fixture_name = if name == UNINSTALL_JOURNAL {
                "uninstall-replaced-journal"
            } else {
                "uninstall-replaced-completion"
            };
            let (_fixture, root, _state) = player_fixture(fixture_name);
            let before = fs::read(root.join("installation.json")).unwrap();
            let lock = acquire_uninstall(&root).unwrap();
            let mut replaced = false;
            let result = uninstall_locked_with(
                &root,
                &root.join("installation.json"),
                &root.join("installation.lock"),
                false,
                &mut |point| {
                    if point == "done" {
                        fs::write(root.join(name), b"foreign replacement").unwrap();
                        replaced = true;
                    }
                    Ok(())
                },
            );
            drop(lock);
            assert!(replaced && result.is_err());
            assert_eq!(fs::read(root.join("installation.json")).unwrap(), before);
            assert_eq!(fs::read(root.join(name)).unwrap(), b"foreign replacement");
        }
    }

    #[test]
    fn uninstall_resume_preserves_modified_foreign_and_ambiguous_packages() {
        let (fixture, root, state) = player_fixture("uninstall-foreign-resume");
        ensure_player_entry(
            &root,
            &fixture.first.project.to_string(),
            &state.active.core,
        )
        .unwrap();
        interrupt_uninstall(&root, "journal", false);
        let package = package_root(&root, &state.active.core).unwrap();
        let modified = package.join("launcher.bin");
        let foreign = package.join("personal.keep");
        fs::write(&modified, b"changed by user").unwrap();
        fs::write(&foreign, b"personal").unwrap();
        // The pending player entry must still be backed by its complete source.
        assert!(uninstall(&root).is_err());
        assert_eq!(fs::read(&modified).unwrap(), b"changed by user");
        assert_eq!(fs::read(&foreign).unwrap(), b"personal");
        assert!(root.join(UNINSTALL_JOURNAL).is_file());

        let (_other, root, state) = player_fixture("uninstall-modified-package");
        interrupt_uninstall(&root, "journal", false);
        let package = package_root(&root, &state.active.core).unwrap();
        fs::write(package.join("launcher.bin"), b"personal replacement").unwrap();
        fs::write(package.join("personal.keep"), b"personal").unwrap();
        let report = uninstall(&root).unwrap();
        assert!(report.preserved_packages >= 1);
        assert_eq!(
            fs::read(package.join("launcher.bin")).unwrap(),
            b"personal replacement"
        );
        assert_eq!(
            fs::read(package.join("personal.keep")).unwrap(),
            b"personal"
        );
        assert!(package.join("package-manifest.json").is_file());

        let (_other, root, state) = player_fixture("uninstall-missing-manifest");
        interrupt_uninstall(&root, "journal", false);
        let package = package_root(&root, &state.active.core).unwrap();
        fs::remove_file(package.join("package-manifest.json")).unwrap();
        let foreign = package.join("personal.keep");
        fs::write(&foreign, b"personal").unwrap();
        let report = uninstall(&root).unwrap();
        assert!(report.preserved_packages >= 1);
        assert_eq!(fs::read(foreign).unwrap(), b"personal");
        assert!(package.join("launcher.bin").is_file());
    }

    #[test]
    fn uninstall_refuses_modified_head_entry_and_ready_origin() {
        let (fixture, root, state) = player_fixture("uninstall-ready-origin");
        let id = fixture.first.project.to_string();
        let entry = ensure_player_entry(&root, &id, &state.active.core).unwrap();
        let receipt_path = root.join(PLAYER_RECEIPT);
        let original_receipt = fs::read(&receipt_path).unwrap();
        let mut receipt: PlayerReceipt = serde_json::from_slice(&original_receipt).unwrap();
        receipt.origin.revision = "0".repeat(64);
        fs::write(&receipt_path, json(&receipt).unwrap()).unwrap();
        assert!(player_entry_status(&root, &id).is_err());
        assert!(uninstall(&root).is_err());
        assert!(entry.is_file());
        assert!(!root.join(UNINSTALL_JOURNAL).exists());
        fs::write(&receipt_path, original_receipt).unwrap();
        interrupt_uninstall(&root, "journal", false);
        let original_entry = fs::read(&entry).unwrap();
        fs::write(&entry, b"foreign replacement").unwrap();
        assert!(uninstall(&root).is_err());
        assert_eq!(fs::read(&entry).unwrap(), b"foreign replacement");
        fs::write(&entry, original_entry).unwrap();
        let head = root.join("installation.json");
        let original_head = fs::read(&head).unwrap();
        let mut changed = original_head.clone();
        changed.push(b' ');
        fs::write(&head, changed).unwrap();
        assert!(uninstall(&root).is_err());
        assert!(entry.is_file());
        fs::write(&head, original_head).unwrap();
        uninstall(&root).unwrap();
    }

    #[test]
    fn uninstall_preserves_unowned_journal_and_stage() {
        let (_fixture, root, _state) = player_fixture("uninstall-foreign-journal");
        let path = root.join(UNINSTALL_JOURNAL);
        fs::write(&path, b"personal data").unwrap();
        assert!(uninstall(&root).is_err());
        assert_eq!(fs::read(&path).unwrap(), b"personal data");
        assert!(root.join("installation.json").is_file());

        let (_fixture, root, _state) = player_fixture("uninstall-foreign-stage");
        interrupt_uninstall(&root, "journal", false);
        let (journal, _, _) = read_uninstall_journal(&root).unwrap().unwrap();
        let stage = root.join(journal.stage_name);
        fs::write(&stage, b"personal stage").unwrap();
        uninstall(&root).unwrap();
        assert_eq!(fs::read(stage).unwrap(), b"personal stage");
    }

    #[test]
    fn uninstall_preserves_a_valid_but_unreferenced_package() {
        let (fixture, root, state) = player_fixture("uninstall-valid-foreign");
        let archive_path = revised_launcher(&fixture, "uninstall-valid-foreign");
        let (descriptor, revision) = archive::inspect(&archive_path).unwrap();
        let foreign = InstalledPack {
            id: descriptor.id,
            revision,
            kind: descriptor.kind,
        };
        assert_eq!(foreign.id, state.active.core.id);
        assert_ne!(foreign.revision, state.active.core.revision);
        let directory = package_root(&root, &foreign).unwrap();
        archive::extract(&archive_path, &directory, &foreign.revision).unwrap();
        let manifest_before = fs::read(directory.join("package-manifest.json")).unwrap();
        let launcher_before = fs::read(directory.join("launcher.bin")).unwrap();
        let report = uninstall(&root).unwrap();
        assert!(report.preserved_packages >= 1);
        assert_eq!(
            fs::read(directory.join("package-manifest.json")).unwrap(),
            manifest_before
        );
        assert_eq!(
            fs::read(directory.join("launcher.bin")).unwrap(),
            launcher_before
        );
    }

    #[test]
    fn uninstall_reads_are_limited_before_allocation() {
        let mut source = std::io::repeat(b'x').take(1000);
        assert!(read_bounded(&mut source, 32, "test").is_err());
        assert_eq!(source.limit(), 967);
        assert_eq!(read_bounded(&b"ok"[..], 2, "test").unwrap(), b"ok");
    }

    #[cfg(unix)]
    #[test]
    fn uninstall_recovers_its_published_hardlink_stage() {
        let (_fixture, root, _state) = player_fixture("uninstall-owned-stage");
        interrupt_uninstall(&root, "journal", false);
        let (journal, _, _) = read_uninstall_journal(&root).unwrap().unwrap();
        let stage = root.join(journal.stage_name);
        fs::hard_link(root.join(UNINSTALL_JOURNAL), &stage).unwrap();
        uninstall(&root).unwrap();
        assert!(!stage.exists());
    }

    #[cfg(windows)]
    #[test]
    fn profile_comparison_refuses_unresolved_unicode_aliases() {
        let (fixture, _install, _state) = player_fixture("profile-unicode");
        let root = fixture.installation("profile-paths");
        fs::create_dir_all(&root).unwrap();
        let upper = root.join("Équipe");
        let lower = root.join("équipe");
        assert!(physically_disjoint(&upper.join("profile"), &lower).is_err());
        fs::create_dir(&upper).unwrap();
        if lower.exists() {
            assert!(!physically_disjoint(&upper.join("profile"), &lower).unwrap());
        } else {
            assert!(physically_disjoint(&upper.join("profile"), &lower).is_err());
        }
        assert!(physically_disjoint(&upper.join("profile"), &root.join("elsewhere")).unwrap());
    }

    #[cfg(windows)]
    #[test]
    fn locked_player_entry_refuses_uninstall_before_packages_are_touched() {
        use std::os::windows::fs::OpenOptionsExt;
        let (fixture, root, state) = player_fixture("entry-locked");
        let entry = ensure_player_entry(
            &root,
            &fixture.first.project.to_string(),
            &state.active.core,
        )
        .unwrap();
        let handle = OpenOptions::new()
            .read(true)
            .share_mode(0)
            .open(&entry)
            .unwrap();
        assert!(uninstall(&root).is_err());
        assert!(root.join("installation.json").is_file());
        assert!(package_root(&root, &state.active.core).unwrap().is_dir());
        assert!(root.join(PLAYER_RECEIPT).is_file());
        drop(handle);
        uninstall(&root).unwrap();
        assert!(!entry.exists());
    }
}
