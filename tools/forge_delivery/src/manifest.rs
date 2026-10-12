use crate::Result;
use serde::{Deserialize, Serialize};
use std::{
    collections::{BTreeMap, BTreeSet},
    path::{Path, PathBuf},
};

pub const MAX_MANIFEST: u64 = 64 * 1024 * 1024;
pub const MAX_FILES: usize = 250_000;
pub const MAX_FILE_BYTES: u64 = 16 * 1024 * 1024 * 1024;
pub const MAX_PACK_BYTES: u64 = 256 * 1024 * 1024 * 1024;

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum Kind {
    Applications,
    Assets,
    Module,
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum Rights {
    PrivateLocalOnly,
    Redistributable,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Entry {
    pub path: String,
    pub bytes: u64,
    pub sha256: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Descriptor {
    pub schema: String,
    pub id: String,
    pub label: String,
    pub version: String,
    pub kind: Kind,
    pub platform: String,
    pub source_revision: String,
    pub rights: Rights,
    pub license_paths: Vec<String>,
    pub files: Vec<Entry>,
    pub product: Option<Product>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PackRef {
    pub id: String,
    pub manifest_sha256: String,
    pub kind: Kind,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Launch {
    pub executable: String,
    pub args: Vec<String>,
    pub env: BTreeMap<String, String>,
    pub clear_env_prefixes: Vec<String>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Seed {
    pub pack_id: String,
    pub path: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Product {
    pub schema: String,
    pub id: String,
    pub title: String,
    pub apps: BTreeMap<String, Launch>,
    pub required_packs: Vec<PackRef>,
    #[serde(default)]
    pub optional_packs: Vec<String>,
    pub authoring_seed: Option<Seed>,
}

pub fn identifier(value: &str) -> Result<()> {
    if value.is_empty()
        || value.len() > 100
        || !value
            .bytes()
            .all(|b| b.is_ascii_lowercase() || b.is_ascii_digit() || b == b'-')
    {
        return Err(format!("Invalid identifier: {value}"));
    }
    Ok(())
}

pub fn valid_hash(value: &str) -> Result<()> {
    if value.len() != 64
        || !value
            .bytes()
            .all(|b| b.is_ascii_digit() || (b'a'..=b'f').contains(&b))
    {
        return Err("Invalid SHA-256".into());
    }
    Ok(())
}

/// Portable relative filenames. Reject Windows aliases even when tested on Linux.
pub fn relative(value: &str) -> Result<()> {
    if value.is_empty()
        || value.len() > 1024
        || value.contains(['\\', ':'])
        || value.chars().any(char::is_control)
    {
        return Err(format!("Unsafe package path: {value}"));
    }
    for part in value.split('/') {
        let stem = part
            .split('.')
            .next()
            .unwrap_or_default()
            .to_ascii_uppercase();
        let device = matches!(
            stem.as_str(),
            "CON" | "PRN" | "AUX" | "NUL" | "CLOCK$" | "CONIN$" | "CONOUT$"
        ) || (stem.len() == 4
            && (stem.starts_with("COM") || stem.starts_with("LPT"))
            && matches!(stem.as_bytes()[3], b'1'..=b'9'));
        if part.is_empty()
            || matches!(part, "." | "..")
            || part.ends_with(['.', ' '])
            || part.contains(['<', '>', '"', '|', '?', '*'])
            || device
        {
            return Err(format!("Unsafe package path: {value}"));
        }
    }
    Ok(())
}

pub fn no_links(path: &Path) -> Result<()> {
    let metadata =
        std::fs::symlink_metadata(path).map_err(|e| format!("{}: {e}", path.display()))?;
    #[cfg(windows)]
    {
        use std::os::windows::fs::MetadataExt;
        if metadata.file_attributes() & 0x400 != 0 {
            return Err("Reparse points are forbidden".into());
        }
    }
    if metadata.file_type().is_symlink() {
        return Err("Symbolic links are forbidden".into());
    }
    Ok(())
}

pub fn confined(root: &Path, value: &str) -> Result<PathBuf> {
    relative(value)?;
    safe_chain(root)?;
    let mut result = root.to_path_buf();
    for part in value.split('/') {
        result.push(part);
        check_existing(&result)?;
    }
    Ok(result)
}

fn check_existing(path: &Path) -> Result<()> {
    match std::fs::symlink_metadata(path) {
        Ok(_) => no_links(path),
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => Ok(()),
        Err(error) => Err(error.to_string()),
    }
}

/// Check ancestor junctions and dangling links before creating any destination.
pub fn safe_chain(path: &Path) -> Result<()> {
    if !path.is_absolute()
        || path.components().any(|c| {
            matches!(
                c,
                std::path::Component::ParentDir | std::path::Component::CurDir
            )
        })
    {
        return Err("Absolute normalized path required".into());
    }
    for ancestor in path.ancestors() {
        check_existing(ancestor)?;
    }
    Ok(())
}

impl Descriptor {
    pub fn validate(&self) -> Result<()> {
        if self.schema != "forge.delivery.package.v1" {
            return Err("Unsupported package schema".into());
        }
        identifier(&self.id)?;
        if self.label.trim().is_empty()
            || self.version.is_empty()
            || self.source_revision.is_empty()
        {
            return Err("Missing provenance or version".into());
        }
        if !matches!(self.platform.as_str(), "windows-x64" | "any") {
            return Err("Unsupported platform".into());
        }
        if self.files.is_empty() || self.files.len() > MAX_FILES {
            return Err("Invalid package file count".into());
        }
        let mut names = BTreeSet::new();
        let mut total = 0_u64;
        for entry in &self.files {
            relative(&entry.path)?;
            valid_hash(&entry.sha256)?;
            if entry.path.eq_ignore_ascii_case("package-manifest.json")
                || !names.insert(entry.path.to_ascii_lowercase())
            {
                return Err("Duplicate or reserved package path".into());
            }
            if entry.bytes > MAX_FILE_BYTES {
                return Err("File exceeds 16 GiB".into());
            }
            total = total
                .checked_add(entry.bytes)
                .ok_or("Package size overflow")?;
            let extension = Path::new(&entry.path)
                .extension()
                .and_then(|s| s.to_str())
                .unwrap_or_default()
                .to_ascii_lowercase();
            if self.kind != Kind::Applications
                && matches!(
                    extension.as_str(),
                    "exe" | "dll" | "ps1" | "bat" | "cmd" | "msi" | "com" | "scr"
                )
            {
                return Err("Asset/module packages cannot contain executable programs".into());
            }
        }
        if total > MAX_PACK_BYTES {
            return Err("Package exceeds 256 GiB".into());
        }
        // Also reject file/directory conflicts independently of archive ordering.
        for name in &names {
            let mut parent = name.as_str();
            while let Some((prefix, _)) = parent.rsplit_once('/') {
                if names.contains(prefix) {
                    return Err("File/directory path conflict".into());
                }
                parent = prefix;
            }
        }
        if self.license_paths.is_empty() {
            return Err("License/provenance files are required".into());
        }
        for license in &self.license_paths {
            relative(license)?;
            if !names.contains(&license.to_ascii_lowercase()) {
                return Err(format!("Missing license file: {license}"));
            }
        }
        match (&self.product, &self.kind) {
            (Some(product), Kind::Applications) => product.validate(&names)?,
            (None, Kind::Assets | Kind::Module) => (),
            _ => return Err("Only application packages must carry a product definition".into()),
        }
        Ok(())
    }
}

impl Product {
    fn validate(&self, names: &BTreeSet<String>) -> Result<()> {
        if self.schema != "forge.delivery.product.v1" {
            return Err("Unsupported product schema".into());
        }
        identifier(&self.id)?;
        if self.title.trim().is_empty()
            || !self.apps.contains_key("game")
            || !self.apps.contains_key("editor")
        {
            return Err("Game and editor entry points are required".into());
        }
        let mut packs = BTreeSet::new();
        for pack in &self.required_packs {
            identifier(&pack.id)?;
            valid_hash(&pack.manifest_sha256)?;
            if pack.kind == Kind::Applications || !packs.insert(pack.id.as_str()) {
                return Err("Invalid pack dependency".into());
            }
        }
        let required = packs.clone();
        for pack in &self.optional_packs {
            identifier(pack)?;
            if !packs.insert(pack.as_str()) {
                return Err("Duplicate optional pack".into());
            }
        }
        for (name, app) in &self.apps {
            identifier(name)?;
            relative(&app.executable)?;
            if !names.contains(&app.executable.to_ascii_lowercase())
                || app.args.len() > 128
                || app.env.len() > 128
            {
                return Err("Invalid application entry point".into());
            }
            for (key, value) in &app.env {
                if key.is_empty() || key.contains(['=', '\0']) || value.contains('\0') {
                    return Err("Invalid environment assignment".into());
                }
            }
            for value in &app.args {
                validate_template(value, &required)?;
            }
            for value in app.env.values() {
                validate_template(value, &packs)?;
            }
            if app.clear_env_prefixes.iter().any(|p| {
                p.is_empty()
                    || p.len() > 128
                    || !p.bytes().all(|b| b.is_ascii_alphanumeric() || b == b'_')
            }) {
                return Err("Invalid environment prefix".into());
            }
        }
        if let Some(seed) = &self.authoring_seed {
            relative(&seed.path)?;
            if !required.contains(seed.pack_id.as_str()) {
                return Err("Unknown authoring seed pack".into());
            }
        }
        Ok(())
    }
}

fn validate_template(value: &str, packs: &BTreeSet<&str>) -> Result<()> {
    if value.contains('\0') || value.len() > 4096 {
        return Err("Invalid launch argument".into());
    }
    let mut rest = value;
    while let Some((before, tail)) = rest.split_once('{') {
        if before.contains('}') {
            return Err("Invalid launch template".into());
        }
        let (token, next) = tail.split_once('}').ok_or("Unclosed launch token")?;
        if !matches!(token, "app" | "install" | "user" | "workspace" | "session")
            && !token
                .strip_prefix("pack:")
                .is_some_and(|id| packs.contains(id))
        {
            return Err(format!("Unknown launch token: {token}"));
        }
        rest = next;
    }
    if rest.contains('}') {
        return Err("Invalid launch template".into());
    }
    Ok(())
}
