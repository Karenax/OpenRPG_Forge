use crate::error::{BridgeError, Result, display_path};
use crate::manifest::{
    AREA_BUNDLE_SCHEMA_VERSION, AreaManifest, AreaRef, Counts, FileRecord, INTEGRITY_SCOPE,
    IdentityDocument, MAX_AREA_BYTES, MAX_AREA_FILES, MAX_AREAS, MAX_MANIFEST_BYTES,
    MAX_MODULE_BYTES, MAX_MODULE_FILES, MODULE_BUNDLE_SCHEMA_VERSION, ModuleManifest,
    ReferencesDocument,
};
use sha2::{Digest, Sha256};
use std::collections::BTreeSet;
use std::fs;
use std::path::{Component, Path, PathBuf};

#[derive(Debug, Clone)]
pub struct ValidatedBundle {
    pub root: PathBuf,
    pub manifest: ModuleManifest,
    pub areas: Vec<(AreaRef, AreaManifest)>,
}

#[allow(clippy::too_many_lines)]
pub fn validate_bundle(bundle: &Path) -> Result<ValidatedBundle> {
    let manifest_path = bundle.join("manifest.json");
    let manifest_bytes = read_bounded(&manifest_path, MAX_MANIFEST_BYTES)?;
    let manifest: ModuleManifest = serde_json::from_slice(&manifest_bytes)
        .map_err(|error| BridgeError::json(display_path(&manifest_path), error))?;
    if manifest.schema_version != MODULE_BUNDLE_SCHEMA_VERSION {
        return Err(BridgeError::UnsupportedVersion(manifest.schema_version));
    }
    if manifest.integrity_scope != INTEGRITY_SCOPE {
        return Err(BridgeError::invalid("unexpected module integrity scope"));
    }
    if manifest.source.game != "nwn1-ee" {
        return Err(BridgeError::invalid(format!(
            "unsupported game {}",
            manifest.source.game
        )));
    }
    validate_sha256(&manifest.source.module_sha256, "source.moduleSha256")?;
    if manifest.rights.classification != "local_only_proprietary"
        && manifest.rights.classification != "redistributable_original"
    {
        return Err(BridgeError::RightsBlocked(format!(
            "unknown classification {}",
            manifest.rights.classification
        )));
    }
    if manifest.readiness.playable {
        return Err(BridgeError::invalid(
            "producer manifest cannot claim playable",
        ));
    }
    if manifest.readiness.exportable
        && (manifest.counts.missing > 0
            || manifest.counts.unsupported > 0
            || manifest.counts.license_blocked > 0)
    {
        return Err(BridgeError::invalid(
            "exportable manifest declares blocking statuses",
        ));
    }
    if manifest.areas.len() > MAX_AREAS {
        return Err(BridgeError::SizeLimit("module areas".to_owned()));
    }
    if manifest.files.len() > MAX_MODULE_FILES {
        return Err(BridgeError::SizeLimit("module files".to_owned()));
    }
    let total_module_bytes = manifest
        .files
        .iter()
        .try_fold(0_u64, |total, file| total.checked_add(file.size_bytes))
        .ok_or_else(|| BridgeError::SizeLimit("module bytes overflow".to_owned()))?;
    if total_module_bytes > MAX_MODULE_BYTES {
        return Err(BridgeError::SizeLimit("module bytes".to_owned()));
    }
    validate_counts(&manifest.counts, &manifest)?;
    validate_files(bundle, &manifest.files)?;
    validate_module_inventory(bundle, &manifest.files)?;
    let payload = manifest
        .files
        .iter()
        .find(|file| file.path == manifest.module.path)
        .ok_or_else(|| BridgeError::Missing(manifest.module.path.clone()))?;
    if payload.sha256 != manifest.module.sha256 || payload.size_bytes != manifest.module.size_bytes
    {
        return Err(BridgeError::HashMismatch(
            "module payload reference".to_owned(),
        ));
    }
    let mut area_resrefs = BTreeSet::new();
    let mut areas = Vec::new();
    for area in &manifest.areas {
        if !area_resrefs.insert(area.resref.clone()) {
            return Err(BridgeError::invalid(format!(
                "duplicate area {}",
                area.resref
            )));
        }
        if area.area_schema_version != AREA_BUNDLE_SCHEMA_VERSION {
            return Err(BridgeError::UnsupportedVersion(
                area.area_schema_version.clone(),
            ));
        }
        if !is_safe_relative_path(&area.path) {
            return Err(BridgeError::UnsafePath(area.path.clone()));
        }
        let path = bundle.join(&area.path);
        let bytes = read_bounded(&path, MAX_MANIFEST_BYTES)?;
        if bytes.len() as u64 != area.size_bytes || sha256(&bytes) != area.sha256 {
            return Err(BridgeError::HashMismatch(format!(
                "area manifest {}",
                area.resref
            )));
        }
        let area_manifest: AreaManifest = serde_json::from_slice(&bytes)
            .map_err(|error| BridgeError::json(display_path(&path), error))?;
        if area_manifest.schema_version != AREA_BUNDLE_SCHEMA_VERSION {
            return Err(BridgeError::UnsupportedVersion(
                area_manifest.schema_version.clone(),
            ));
        }
        if !area_manifest
            .module_sha256
            .eq_ignore_ascii_case(&manifest.source.module_sha256)
        {
            return Err(BridgeError::HashMismatch(format!(
                "area module hash {}",
                area.resref
            )));
        }
        let area_root = path.parent().ok_or_else(|| {
            BridgeError::invalid(format!("area manifest has no parent {}", area.path))
        })?;
        validate_area_files(area_root, &area.resref, &area_manifest.files)?;
        areas.push((area.clone(), area_manifest));
    }
    validate_identity(bundle, &manifest)?;
    validate_references(bundle, &manifest)?;
    validate_diagnostics(bundle, &manifest)?;
    Ok(ValidatedBundle {
        root: bundle.to_path_buf(),
        manifest,
        areas,
    })
}

fn validate_counts(counts: &Counts, manifest: &ModuleManifest) -> Result<()> {
    if counts.areas != manifest.areas.len() {
        return Err(BridgeError::invalid("area count mismatch"));
    }
    if counts.blueprints
        != manifest
            .catalogs
            .iter()
            .map(|catalog| catalog.entry_count)
            .sum::<usize>()
    {
        // Catalogs may include tilesets as well; only compare when a blueprints catalog exists.
        if let Some(catalog) = manifest
            .catalogs
            .iter()
            .find(|catalog| catalog.kind == "blueprints")
            && counts.blueprints != catalog.entry_count
        {
            return Err(BridgeError::invalid("blueprint count mismatch"));
        }
    }
    if counts.dialogs != manifest.dialogs.len() {
        return Err(BridgeError::invalid("dialog count mismatch"));
    }
    if counts.factions != manifest.factions.faction_count {
        return Err(BridgeError::invalid("faction count mismatch"));
    }
    if counts.scripts_ncs != manifest.scripts.ncs_count
        || counts.scripts_nss != manifest.scripts.nss_count
    {
        return Err(BridgeError::invalid("script count mismatch"));
    }
    if counts.rules_tables != manifest.rules.table_count
        || counts.tlk_strings != manifest.rules.string_count
    {
        return Err(BridgeError::invalid("rules count mismatch"));
    }
    if counts.references != manifest.references.node_count {
        return Err(BridgeError::invalid("reference count mismatch"));
    }
    Ok(())
}

fn validate_files(root: &Path, files: &[FileRecord]) -> Result<()> {
    let mut seen = BTreeSet::new();
    for file in files {
        if !seen.insert(file.path.clone()) {
            return Err(BridgeError::invalid(format!(
                "duplicate file {}",
                file.path
            )));
        }
        validate_file_record(root, file)?;
    }
    Ok(())
}

fn validate_file_record(root: &Path, file: &FileRecord) -> Result<()> {
    if !is_safe_relative_path(&file.path) {
        return Err(BridgeError::UnsafePath(file.path.clone()));
    }
    let path = root.join(&file.path);
    let metadata =
        fs::symlink_metadata(&path).map_err(|error| BridgeError::io(display_path(&path), error))?;
    if metadata.file_type().is_symlink() {
        return Err(BridgeError::UnsafePath(file.path.clone()));
    }
    if metadata.len() != file.size_bytes {
        return Err(BridgeError::HashMismatch(format!("size {}", file.path)));
    }
    let bytes = fs::read(&path).map_err(|error| BridgeError::io(display_path(&path), error))?;
    if sha256(&bytes) != file.sha256 {
        return Err(BridgeError::HashMismatch(file.path.clone()));
    }
    validate_role(&file.role)?;
    Ok(())
}

fn validate_role(role: &str) -> Result<()> {
    if matches!(
        role,
        "module"
            | "catalog"
            | "dialog"
            | "journal"
            | "faction"
            | "script-ncs"
            | "script-nss"
            | "script-inventory"
            | "rules-table"
            | "tlk"
            | "asset-model"
            | "asset-texture"
            | "asset-audio"
            | "asset-navigation"
            | "references"
            | "identity-map"
            | "diagnostics"
            | "report"
            | "area"
            | "model"
            | "texture"
            | "navigation-source"
    ) {
        Ok(())
    } else {
        Err(BridgeError::invalid(format!("unknown payload role {role}")))
    }
}

fn validate_module_inventory(root: &Path, files: &[FileRecord]) -> Result<()> {
    let declared = files
        .iter()
        .map(|file| file.path.as_str())
        .collect::<BTreeSet<_>>();
    let actual = collect_module_files(root)?;
    let actual_set = actual.iter().map(String::as_str).collect::<BTreeSet<_>>();
    if declared != actual_set {
        let missing = declared
            .difference(&actual_set)
            .copied()
            .collect::<Vec<_>>();
        let extra = actual_set
            .difference(&declared)
            .copied()
            .collect::<Vec<_>>();
        return Err(BridgeError::invalid(format!(
            "module inventory mismatch; missing={missing:?} extra={extra:?}"
        )));
    }
    Ok(())
}

fn validate_area_files(root: &Path, area: &str, files: &[FileRecord]) -> Result<()> {
    if files.len() > MAX_AREA_FILES {
        return Err(BridgeError::SizeLimit(format!("area files {area}")));
    }
    let total = files
        .iter()
        .try_fold(0_u64, |total, file| total.checked_add(file.size_bytes))
        .ok_or_else(|| BridgeError::SizeLimit(format!("area bytes overflow {area}")))?;
    if total > MAX_AREA_BYTES {
        return Err(BridgeError::SizeLimit(format!("area bytes {area}")));
    }
    let mut seen = BTreeSet::new();
    for file in files {
        if !seen.insert(file.path.clone()) {
            return Err(BridgeError::invalid(format!(
                "duplicate area file {area}::{}",
                file.path
            )));
        }
        validate_file_record(root, file)?;
    }
    let declared = files
        .iter()
        .map(|file| file.path.as_str())
        .collect::<BTreeSet<_>>();
    let actual = collect_area_inventory(root)?;
    let actual_set = actual.iter().map(String::as_str).collect::<BTreeSet<_>>();
    if declared != actual_set {
        let missing = declared
            .difference(&actual_set)
            .copied()
            .collect::<Vec<_>>();
        let extra = actual_set
            .difference(&declared)
            .copied()
            .collect::<Vec<_>>();
        return Err(BridgeError::invalid(format!(
            "area inventory mismatch {area}; missing={missing:?} extra={extra:?}"
        )));
    }
    Ok(())
}

fn collect_area_inventory(root: &Path) -> Result<Vec<String>> {
    let mut files = Vec::new();
    collect_area_files_recursive(root, root, &mut files)?;
    files.sort();
    Ok(files)
}

fn collect_area_files_recursive(
    root: &Path,
    directory: &Path,
    files: &mut Vec<String>,
) -> Result<()> {
    let mut entries = fs::read_dir(directory)
        .map_err(|error| BridgeError::io(display_path(directory), error))?
        .collect::<std::result::Result<Vec<_>, _>>()
        .map_err(|error| BridgeError::io(display_path(directory), error))?;
    entries.sort_by_key(std::fs::DirEntry::file_name);
    for entry in entries {
        let path = entry.path();
        let metadata = fs::symlink_metadata(&path)
            .map_err(|error| BridgeError::io(display_path(&path), error))?;
        if metadata.file_type().is_symlink() {
            return Err(BridgeError::UnsafePath(display_path(&path)));
        }
        let relative = path
            .strip_prefix(root)
            .map_err(|_| BridgeError::UnsafePath(display_path(&path)))?
            .to_string_lossy()
            .replace('\\', "/");
        if relative == "manifest.json" {
            continue;
        }
        if metadata.is_dir() {
            collect_area_files_recursive(root, &path, files)?;
        } else {
            files.push(relative);
        }
    }
    Ok(())
}

fn validate_identity(bundle: &Path, manifest: &ModuleManifest) -> Result<()> {
    let path = bundle.join(&manifest.identity_map.path);
    let bytes = read_bounded(&path, MAX_MANIFEST_BYTES)?;
    if sha256(&bytes) != manifest.identity_map.sha256
        || bytes.len() as u64 != manifest.identity_map.size_bytes
    {
        return Err(BridgeError::HashMismatch("identity-map.json".to_owned()));
    }
    let identity: IdentityDocument = serde_json::from_slice(&bytes)
        .map_err(|error| BridgeError::json(display_path(&path), error))?;
    if identity.schema_version != "module-identity-map@1" {
        return Err(BridgeError::UnsupportedVersion(identity.schema_version));
    }
    if identity.module_namespace != manifest.identity_map.module_namespace {
        return Err(BridgeError::invalid("identity namespace mismatch"));
    }
    if identity.entries.len() != manifest.identity_map.entry_count {
        return Err(BridgeError::invalid("identity entry count mismatch"));
    }
    let mut ids = BTreeSet::new();
    for entry in &identity.entries {
        if !ids.insert(entry.semantic_id.clone()) {
            return Err(BridgeError::invalid(format!(
                "duplicate semantic id {}",
                entry.semantic_id
            )));
        }
        if entry.stability != "stable" && entry.stability != "provisional" {
            return Err(BridgeError::invalid(format!(
                "unstable identity {}",
                entry.semantic_id
            )));
        }
    }
    Ok(())
}

fn validate_references(bundle: &Path, manifest: &ModuleManifest) -> Result<()> {
    let path = bundle.join(&manifest.references.path);
    let bytes = read_bounded(&path, MAX_MANIFEST_BYTES)?;
    if sha256(&bytes) != manifest.references.sha256
        || bytes.len() as u64 != manifest.references.size_bytes
    {
        return Err(BridgeError::HashMismatch("references.json".to_owned()));
    }
    let references: ReferencesDocument = serde_json::from_slice(&bytes)
        .map_err(|error| BridgeError::json(display_path(&path), error))?;
    if references.schema_version != "module-references@1" {
        return Err(BridgeError::UnsupportedVersion(references.schema_version));
    }
    if references.nodes.len() != manifest.references.node_count
        || references.edges.len() != manifest.references.edge_count
    {
        return Err(BridgeError::invalid("reference count mismatch"));
    }
    let ids = references
        .nodes
        .iter()
        .map(|node| node.id.as_str())
        .collect::<BTreeSet<_>>();
    if ids.len() != references.nodes.len() {
        return Err(BridgeError::invalid("duplicate reference node"));
    }
    for edge in &references.edges {
        if !ids.contains(edge.from.as_str()) || !ids.contains(edge.to.as_str()) {
            return Err(BridgeError::invalid(format!(
                "reference edge target missing {} -> {}",
                edge.from, edge.to
            )));
        }
    }
    Ok(())
}

fn validate_diagnostics(bundle: &Path, manifest: &ModuleManifest) -> Result<()> {
    let path = bundle.join("diagnostics.jsonl");
    let bytes = read_bounded(&path, MAX_MANIFEST_BYTES)?;
    for (index, line) in bytes.split(|byte| *byte == b'\n').enumerate() {
        if line.is_empty() {
            continue;
        }
        serde_json::from_slice::<serde_json::Value>(line).map_err(|error| {
            BridgeError::invalid(format!(
                "diagnostics.jsonl line {} invalid: {error}",
                index + 1
            ))
        })?;
    }
    if manifest.readiness.exportable
        && manifest
            .files
            .iter()
            .all(|file| file.path != "diagnostics.jsonl")
    {
        return Err(BridgeError::Missing("diagnostics.jsonl".to_owned()));
    }
    Ok(())
}

fn collect_module_files(root: &Path) -> Result<Vec<String>> {
    let mut files = Vec::new();
    collect_files_recursive(root, root, &mut files)?;
    files.sort();
    Ok(files)
}

fn collect_files_recursive(root: &Path, directory: &Path, files: &mut Vec<String>) -> Result<()> {
    let mut entries = fs::read_dir(directory)
        .map_err(|error| BridgeError::io(display_path(directory), error))?
        .collect::<std::result::Result<Vec<_>, _>>()
        .map_err(|error| BridgeError::io(display_path(directory), error))?;
    entries.sort_by_key(std::fs::DirEntry::file_name);
    for entry in entries {
        let path = entry.path();
        let metadata = fs::symlink_metadata(&path)
            .map_err(|error| BridgeError::io(display_path(&path), error))?;
        if metadata.file_type().is_symlink() {
            return Err(BridgeError::UnsafePath(display_path(&path)));
        }
        let relative = path
            .strip_prefix(root)
            .map_err(|_| BridgeError::UnsafePath(display_path(&path)))?
            .to_string_lossy()
            .replace('\\', "/");
        if relative == "manifest.json" {
            continue;
        }
        if relative == "areas" {
            continue;
        }
        if relative.starts_with("areas/") {
            return Err(BridgeError::invalid(format!(
                "nested area payload outside area manifest {relative}"
            )));
        }
        if metadata.is_dir() {
            collect_files_recursive(root, &path, files)?;
        } else {
            files.push(relative);
        }
    }
    Ok(())
}

fn read_bounded(path: &Path, limit: u64) -> Result<Vec<u8>> {
    let metadata =
        fs::symlink_metadata(path).map_err(|error| BridgeError::io(display_path(path), error))?;
    if metadata.file_type().is_symlink() {
        return Err(BridgeError::UnsafePath(display_path(path)));
    }
    if metadata.len() > limit {
        return Err(BridgeError::SizeLimit(display_path(path)));
    }
    fs::read(path).map_err(|error| BridgeError::io(display_path(path), error))
}

fn is_safe_relative_path(value: &str) -> bool {
    if value.is_empty()
        || value.starts_with('/')
        || value.contains('\\')
        || value.contains(':')
        || value.contains('\0')
    {
        return false;
    }
    let path = Path::new(value);
    let components = path.components().collect::<Vec<_>>();
    !components.is_empty()
        && components
            .iter()
            .all(|component| matches!(component, Component::Normal(_)))
}

fn validate_sha256(value: &str, label: &str) -> Result<()> {
    if value.len() == 64 && value.bytes().all(|byte| byte.is_ascii_hexdigit()) {
        Ok(())
    } else {
        Err(BridgeError::invalid(format!("invalid SHA-256 {label}")))
    }
}

fn sha256(bytes: &[u8]) -> String {
    let mut digest = String::with_capacity(64);
    for byte in Sha256::digest(bytes) {
        use std::fmt::Write as _;
        let _ = write!(digest, "{byte:02x}");
    }
    digest
}
