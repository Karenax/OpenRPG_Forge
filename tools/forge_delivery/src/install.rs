use crate::{
    Result, archive, file_hash, hash, json,
    manifest::{self, Kind, PackRef, Product},
};
use serde::{Deserialize, Serialize};
use std::{
    collections::{BTreeMap, BTreeSet},
    fs::{self, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
};

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
pub struct InstalledPack {
    pub id: String,
    pub revision: String,
    pub kind: Kind,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
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

#[derive(Clone, Debug, Serialize)]
pub struct UninstallReport {
    pub root: String,
    pub removed_files: usize,
    pub preserved_files: usize,
    pub preserved_packages: usize,
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
    let state: State = crate::read_json(&path)?;
    if state.schema != "forge.delivery.installation.v1" || state.receipts.len() > 1024 {
        return Err("Unsupported installation head".into());
    }
    Ok((Some(state), file_hash(&path)?))
}

pub fn state(root: &Path) -> Result<State> {
    head(root)?
        .0
        .ok_or_else(|| "Aucune installation active".into())
}

pub fn product(root: &Path, snapshot: &Snapshot) -> Result<Product> {
    let directory = package_root(root, &snapshot.core)?;
    let descriptor: manifest::Descriptor =
        crate::read_json(&manifest::confined(&directory, "package-manifest.json")?)?;
    descriptor.validate()?;
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

pub fn prepare(root: &Path, archives: &[PathBuf]) -> Result<Plan> {
    if !root.is_absolute() || archives.is_empty() || archives.len() > 1024 {
        return Err("Absolute installation root and 1..1024 packages required".into());
    }
    let (current, expected_head) = head(root)?;
    manifest::safe_chain(root)?;
    let user_root = current.as_ref().map(|s| s.user_root.clone()).unwrap_or(
        crate::launch::user_root()?
            .to_str()
            .ok_or("Non-Unicode user root")?
            .to_owned(),
    );
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
    let target = target.ok_or("First installation requires an application package")?;
    let definition = match new_product {
        Some(p) => p,
        None => product(root, &target)?,
    };
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

pub fn prepare_rollback(root: &Path) -> Result<Plan> {
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

struct ExpectedLayout {
    files: BTreeSet<String>,
}

fn path_key(value: &str) -> String {
    value.replace('\\', "/").to_ascii_lowercase()
}

fn path_is_within(path: &Path, ancestor: &Path) -> bool {
    let path = path_key(&path.to_string_lossy());
    let ancestor = path_key(&ancestor.to_string_lossy())
        .trim_end_matches('/')
        .to_owned();
    path == ancestor || path.starts_with(&(ancestor + "/"))
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

fn uninstall_locked(root: &Path, state_path: &Path, lock_path: &Path) -> Result<UninstallReport> {
    let state = state(root)?;
    if state.revision == 0 {
        return Err("Invalid installation revision".into());
    }
    let user_root = PathBuf::from(&state.user_root);
    if !user_root.is_absolute() {
        return Err("User data root must be absolute".into());
    }
    manifest::safe_chain(&user_root)?;
    if path_is_within(root, &user_root) {
        return Err("User data root contains the installation root".into());
    }
    if path_is_within(state_path, &user_root) || path_is_within(lock_path, &user_root) {
        return Err("Installation metadata is inside the protected user data root".into());
    }
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
    validate_snapshot_refs(&state.active, &package_kinds)?;
    if let Some(previous) = &state.previous {
        validate_snapshot_refs(previous, &package_kinds)?;
    }

    let mut report = UninstallReport {
        root: root
            .to_str()
            .ok_or("Non-Unicode installation root")?
            .to_owned(),
        removed_files: 0,
        preserved_files: 0,
        preserved_packages: 0,
    };
    for package in recognized {
        let mut package_preserved = package.foreign_files > 0;
        report.preserved_files = report.preserved_files.saturating_add(package.foreign_files);
        for entry in &package.descriptor.files {
            let path = manifest::confined(&package.directory, &entry.path)?;
            let (removed, preserved) =
                remove_if_matching(&path, &user_root, entry.bytes, &entry.sha256)?;
            report.removed_files = report.removed_files.saturating_add(usize::from(removed));
            if removed && let Some(parent) = path.parent() {
                remove_empty_ancestors(parent, &packages, &user_root)?;
            }
            if preserved {
                package_preserved = true;
                report.preserved_files = report.preserved_files.saturating_add(1);
            }
        }
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
            }
        }
        remove_empty_ancestors(&package.directory, &packages, &user_root)?;
        if existing(&package.directory)?.is_some() && package_preserved {
            report.preserved_packages = report.preserved_packages.saturating_add(1);
        }
    }
    fs::remove_file(state_path).map_err(|e| e.to_string())?;
    Ok(report)
}

/// Remove only verified package files while preserving user data, edits and foreign entries.
pub fn uninstall(root: &Path) -> Result<UninstallReport> {
    if !root.is_absolute() {
        return Err("Installation root must be absolute".into());
    }
    manifest::safe_chain(root)?;
    let state_path = manifest::confined(root, "installation.json")?;
    if existing(&state_path)?.is_none() {
        return Ok(UninstallReport {
            root: root
                .to_str()
                .ok_or("Non-Unicode installation root")?
                .to_owned(),
            removed_files: 0,
            preserved_files: 0,
            preserved_packages: 0,
        });
    }
    let lock_path = manifest::confined(root, "installation.lock")?;
    // Read before acquiring so a refused uninstall does not create a lock directory/file.
    state(root)?;
    let lock = acquire(root)?;
    let result = uninstall_locked(root, &state_path, &lock_path);
    drop(lock);
    // Keep the lock inode in place. Removing it after releasing the OS lock
    // would let another installer open a replacement inode before this
    // process removes the old path.
    result
}

struct Lock {
    _file: fs::File,
}

fn acquire(root: &Path) -> Result<Lock> {
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
        .map_err(|e| format!("Un autre processus modifie cette installation : {e}"))?;
    Ok(Lock { _file: file })
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
    let _lock = acquire(root)?;
    let (current, current_head) = head(root)?;
    if let Some(state) = &current
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
    for input in &plan.inputs {
        manifest::identifier(&input.id)?;
        manifest::valid_hash(&input.manifest_sha256)?;
        manifest::valid_hash(&input.archive_sha256)?;
        let source = Path::new(&input.archive);
        if file_hash(source)? != input.archive_sha256 {
            return Err("Package changed after approval".into());
        }
        let (descriptor, revision) = archive::inspect(source)?;
        if descriptor.id != input.id || revision != input.manifest_sha256 {
            return Err("Package identity changed".into());
        }
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
    }
    verify_snapshot(root, &plan.target)?;
    let definition = product(root, &plan.target)?;
    if let Some(previous) = &current
        && definition.id != product(root, &previous.active)?.id
    {
        return Err("Different product: select a separate installation directory".into());
    }
    if !Path::new(&plan.user_root).is_absolute() {
        return Err("User data root must be absolute".into());
    }
    crate::launch::seed_workspace(
        root,
        &plan.target,
        Path::new(&plan.user_root),
        &plan.command_id,
    )?;
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
    archive::verify_directory(
        &package_root(root, &snapshot.core)?,
        &snapshot.core.revision,
    )?;
    for (id, pack) in &snapshot.packs {
        if id != &pack.id || pack.kind == Kind::Applications {
            return Err("Invalid installed pack index".into());
        }
        archive::verify_directory(&package_root(root, pack)?, &pack.revision)?;
    }
    dependencies(&product(root, snapshot)?, snapshot)
}

pub fn verify(root: &Path) -> Result<State> {
    let state = state(root)?;
    verify_snapshot(root, &state.active)?;
    Ok(state)
}
