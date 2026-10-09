use crate::{
    Result, archive, file_hash, hash, json,
    manifest::{self, Kind, PackRef, Product},
};
use serde::{Deserialize, Serialize};
use std::{
    collections::BTreeMap,
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
