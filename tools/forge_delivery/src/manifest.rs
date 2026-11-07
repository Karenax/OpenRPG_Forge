use crate::Result;
use serde::{Deserialize, Serialize};
use std::{
    collections::{BTreeMap, BTreeSet},
    path::{Path, PathBuf},
};

// The native no-reparse adapter is the only local exception; see ADR-0024.
#[cfg(windows)]
#[allow(unsafe_code)]
mod windows_path_guard;

pub const MAX_MANIFEST: u64 = 64 * 1024 * 1024;
pub const MAX_FILES: usize = 250_000;
pub const MAX_FILE_BYTES: u64 = 16 * 1024 * 1024 * 1024;
pub const MAX_PACK_BYTES: u64 = 256 * 1024 * 1024 * 1024;
pub const STANDALONE_PRODUCT_SCHEMA: &str = "forge.delivery.product.v2";
pub const STANDALONE_GAME_MANIFEST: &str = "game/launcher.json";
pub const STUDIO_PRODUCT_SCHEMA: &str = "forge.delivery.product.v3";
pub const STUDIO_PRODUCT_ID: &str = "openrpg-forge";
pub(crate) const STUDIO_REFERENCE_ASSETS: &str = "reference-assets";
pub(crate) const REFERENCE_SCENE: &str = "data/scene";
pub(crate) const REFERENCE_CATALOG: &str = "static-assets.json";

pub(crate) fn reference_catalog(descriptor: &Descriptor) -> Result<&Entry> {
    let path = format!("{REFERENCE_SCENE}/{REFERENCE_CATALOG}");
    if descriptor.id == STUDIO_REFERENCE_ASSETS && descriptor.kind == Kind::Assets {
        return descriptor
            .files
            .iter()
            .find(|entry| entry.path == path)
            .ok_or_else(|| {
                "Le catalogue des modèles de référence est absent du paquet vérifié.".into()
            });
    }
    Err("Paquet de modèles de référence invalide.".into())
}

fn studio_executables(platform: &str) -> Option<(&'static str, &'static str, &'static str)> {
    match platform {
        "windows-x64" => Some((
            "bin/forge-studio.exe",
            "bin/game-export-kit/bin/forge-module.exe",
            "bin/game-export-kit/bin/forge-game-launcher.exe",
        )),
        "linux-x64" => Some((
            "bin/forge-studio",
            "bin/game-export-kit/bin/forge-module",
            "bin/game-export-kit/bin/forge-game-launcher",
        )),
        _ => None,
    }
}

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
        Err(error) => Err(format!(
            "Inspect package ancestor {}: {error}",
            path.display()
        )),
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
    #[cfg(windows)]
    {
        windows_path_guard::safe_chain(path)
    }
    #[cfg(not(windows))]
    {
        for ancestor in path.ancestors() {
            check_existing(ancestor)?;
        }
        Ok(())
    }
}

/// Accept a pasted trailing separator without changing any interior path
/// component, then apply the usual absolute/no-link guard unchanged.
pub fn normalized_directory(path: &Path) -> Result<PathBuf> {
    let value = path.to_str().ok_or("Non-Unicode directory path")?;
    #[cfg(windows)]
    if value
        .split(['/', '\\'])
        .any(|part| matches!(part, "." | ".."))
    {
        return Err("Absolute normalized path required".into());
    }
    #[cfg(not(windows))]
    if value.split('/').any(|part| matches!(part, "." | "..")) {
        return Err("Absolute normalized path required".into());
    }
    #[cfg(windows)]
    let trimmed = value.trim_end_matches(['/', '\\']);
    #[cfg(not(windows))]
    let trimmed = value.trim_end_matches('/');
    let stripped = PathBuf::from(trimmed);
    #[cfg(windows)]
    let result = if path.is_absolute() && !stripped.is_absolute() {
        let mut root = trimmed.to_owned();
        root.push('\\');
        PathBuf::from(root)
    } else {
        stripped
    };
    #[cfg(not(windows))]
    let result = if trimmed.is_empty() && path.is_absolute() {
        PathBuf::from("/")
    } else {
        stripped
    };
    safe_chain(&result)?;
    Ok(result)
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
        if !matches!(self.platform.as_str(), "windows-x64" | "linux-x64" | "any") {
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
            (Some(product), Kind::Applications) => product.validate(&names, &self.platform)?,
            (None, Kind::Assets | Kind::Module) => (),
            _ => return Err("Only application packages must carry a product definition".into()),
        }
        if self.platform == "linux-x64"
            && self
                .product
                .as_ref()
                .is_some_and(|product| product.schema == STUDIO_PRODUCT_SCHEMA)
        {
            let (_, runtime, launcher) =
                studio_executables(&self.platform).ok_or("Unsupported Studio platform")?;
            if [runtime, launcher]
                .iter()
                .any(|path| !self.files.iter().any(|entry| entry.path == *path))
            {
                return Err("Linux Studio internal executable paths must match exactly".into());
            }
        }
        if self
            .product
            .as_ref()
            .is_some_and(|product| product.schema == STANDALONE_PRODUCT_SCHEMA)
            && !self
                .files
                .iter()
                .any(|entry| entry.path == STANDALONE_GAME_MANIFEST)
        {
            return Err("Standalone game manifest path must match exactly".into());
        }
        if self.platform == "linux-x64" && !self.has_exact_entry_points() {
            return Err("Linux application paths must match the inventory exactly".into());
        }
        Ok(())
    }

    fn has_exact_entry_points(&self) -> bool {
        self.product.as_ref().is_none_or(|product| {
            product
                .apps
                .values()
                .all(|app| self.files.iter().any(|file| file.path == app.executable))
        })
    }

    #[cfg(unix)]
    pub(crate) fn requires_execution_permission(&self, path: &str) -> bool {
        self.product.as_ref().is_some_and(|product| {
            product.apps.values().any(|app| app.executable == path)
                || (self.platform == "linux-x64"
                    && product.schema == STUDIO_PRODUCT_SCHEMA
                    && studio_executables(&self.platform)
                        .is_some_and(|(_, runtime, launcher)| path == runtime || path == launcher))
        })
    }

    /// Inspection/packing remains cross-platform; installation and launch must
    /// admit the actual OS and architecture separately from archive integrity.
    pub fn validate_host(&self) -> Result<()> {
        self.validate()?;
        let compatible = self.platform == "any"
            || (self.platform == "windows-x64"
                && cfg!(all(target_os = "windows", target_arch = "x86_64")))
            || (self.platform == "linux-x64"
                && cfg!(all(target_os = "linux", target_arch = "x86_64")));
        if !compatible {
            return Err(format!(
                "Package platform {} is incompatible with this host",
                self.platform
            ));
        }
        if cfg!(unix) && !self.has_exact_entry_points() {
            return Err("Native application paths must match the inventory exactly".into());
        }
        Ok(())
    }
}

impl Product {
    fn validate(&self, names: &BTreeSet<String>, platform: &str) -> Result<()> {
        identifier(&self.id)?;
        match self.schema.as_str() {
            "forge.delivery.product.v1" => {
                if self.title.trim().is_empty()
                    || !self.apps.contains_key("game")
                    || !self.apps.contains_key("editor")
                {
                    return Err("Game and editor entry points are required".into());
                }
            }
            STANDALONE_PRODUCT_SCHEMA => {
                let project = self
                    .id
                    .parse::<forge_core::ProjectId>()
                    .map_err(|_| "Standalone product identity must be a project ID")?;
                if project.as_uuid().is_nil()
                    || project.to_string() != self.id
                    || self.title.trim().is_empty()
                    || self.apps.len() != 2
                    || !self.apps.contains_key("launcher")
                    || !self.apps.contains_key("module-game")
                    || self.authoring_seed.is_some()
                    || !self.optional_packs.is_empty()
                    || !self
                        .required_packs
                        .iter()
                        .any(|pack| pack.kind == Kind::Module)
                    || !names.contains(STANDALONE_GAME_MANIFEST)
                {
                    return Err("Invalid standalone game product definition".into());
                }
                let runtime = self
                    .apps
                    .get("module-game")
                    .ok_or("Missing module runtime")?;
                if !runtime.args.is_empty() || !runtime.env.is_empty() {
                    return Err("Standalone module runtime must use a validated selection".into());
                }
                let launcher = self.apps.get("launcher").ok_or("Missing launcher")?;
                if launcher.args.len() != 2
                    || launcher.args[0] != "--root"
                    || launcher.args[1] != "{install}"
                    || launcher.env.iter().any(|(key, value)| {
                        let key = key.to_ascii_uppercase();
                        value.contains("{workspace}")
                            || !standalone_environment_value(&key, value)
                            || key == "OPENRPG_ASSET_VAULT"
                            || key.starts_with("FORGE_AUTHORING_")
                            || key.starts_with("FORGE_TIDEWATER_")
                            || key.starts_with("FORGE_NATIVE_GAME_")
                            || key.starts_with("FORGE_MODULE_ASSET_")
                            || key.starts_with("FORGE_GAME_")
                            || key.starts_with("WEBVIEW2_")
                    })
                {
                    return Err("Standalone launcher cannot depend on authoring resources".into());
                }
            }
            STUDIO_PRODUCT_SCHEMA => {
                let editor = self.apps.get("editor").ok_or("Studio editor is missing")?;
                let (editor_path, runtime, launcher) = studio_executables(platform)
                    .ok_or("Studio product v3 supports Windows and Linux x64 only")?;
                let runtime_environment = format!("{{app}}/{runtime}");
                let optional_vault = self.optional_packs.iter().any(|id| id == "asset-library");
                let expected_env = [
                    ("FORGE_AUTHORING_ROOT", "{workspace}"),
                    ("FORGE_NATIVE_GAME_EXECUTABLE", runtime_environment.as_str()),
                    (
                        "FORGE_NATIVE_GAME_CONTENT_ROOT",
                        "{app}/bin/game-export-kit",
                    ),
                    ("FORGE_NATIVE_GAME_SESSION_ROOT", "{user}/game-sessions"),
                ];
                let cleared = [
                    "FORGE_AUTHORING_",
                    "FORGE_TIDEWATER_",
                    "FORGE_NATIVE_GAME_",
                    "FORGE_MODULE_ASSET_",
                    "OPENRPG_ASSET_VAULT",
                    "WEBVIEW2_",
                ];
                if self.id != STUDIO_PRODUCT_ID
                    || self.title.trim().is_empty()
                    || self.apps.len() != 1
                    || !self.required_packs.is_empty()
                    || self
                        .optional_packs
                        .iter()
                        .any(|id| !matches!(id.as_str(), "asset-library" | STUDIO_REFERENCE_ASSETS))
                    || self.authoring_seed.is_some()
                    || editor.executable != editor_path
                    || !editor.args.is_empty()
                    || !names.contains(runtime)
                    || !names.contains(launcher)
                    || editor.env.len() != expected_env.len() + usize::from(optional_vault)
                    || expected_env.iter().any(|(key, value)| {
                        editor.env.get(*key).map(String::as_str) != Some(*value)
                    })
                    || (optional_vault
                        && editor.env.get("OPENRPG_ASSET_VAULT").map(String::as_str)
                            != Some("{pack:asset-library}"))
                    || cleared.iter().any(|prefix| {
                        !editor
                            .clear_env_prefixes
                            .iter()
                            .any(|actual| actual.eq_ignore_ascii_case(prefix))
                    })
                {
                    return Err("Invalid editor-only Studio product definition".into());
                }
            }
            _ => return Err("Unsupported product schema".into()),
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

/// Product-declared paths must not refer to the author's machine. System
/// environment inherited by the launcher is handled separately at spawn time.
fn standalone_environment_value(key: &str, value: &str) -> bool {
    let path_key = matches!(
        key,
        "PATH"
            | "HOME"
            | "USERPROFILE"
            | "APPDATA"
            | "LOCALAPPDATA"
            | "TEMP"
            | "TMP"
            | "LD_LIBRARY_PATH"
            | "LD_PRELOAD"
            | "DYLD_LIBRARY_PATH"
            | "DYLD_INSERT_LIBRARIES"
            | "PYTHONPATH"
            | "NODE_PATH"
    ) || ["_PATH", "_DIR", "_ROOT", "_FILE"]
        .iter()
        .any(|suffix| key.ends_with(suffix));
    if path_key && !standalone_path_list(value) {
        return false;
    }
    let lower = value.to_ascii_lowercase();
    if lower.contains("file://") {
        return false;
    }
    if value
        .split([
            '/', '\\', ';', ':', '=', ' ', '\t', '"', '\'', '(', ')', '[', ']', ',',
        ])
        .any(|part| part == "..")
    {
        return false;
    }
    let bytes = value.as_bytes();
    for (index, byte) in bytes.iter().copied().enumerate() {
        let boundary = index == 0
            || matches!(
                bytes[index - 1],
                b';' | b':' | b'=' | b' ' | b'\t' | b'"' | b'\'' | b'(' | b'[' | b','
            );
        if boundary && byte.is_ascii_alphabetic() && bytes.get(index + 1) == Some(&b':') {
            return false;
        }
        if boundary && byte == b'~' && matches!(bytes.get(index + 1), Some(b'/' | b'\\')) {
            return false;
        }
        if byte == b'\\' && boundary {
            return false;
        }
        if byte == b'/' {
            let url_scheme =
                lower[..index].ends_with("http:") || lower[..index].ends_with("https:");
            if boundary && !url_scheme {
                return false;
            }
        }
    }
    true
}

/// Colon/semicolon-separated paths may only start at a declared package or
/// user root. Colons within `{pack:<id>}` are part of the token, not separators.
fn standalone_path_list(value: &str) -> bool {
    let mut parts = Vec::new();
    let mut start = 0;
    let mut in_token = false;
    for (index, character) in value.char_indices() {
        match character {
            '{' if !in_token => in_token = true,
            '}' if in_token => in_token = false,
            ':' | ';' if !in_token => {
                parts.push(&value[start..index]);
                start = index + character.len_utf8();
            }
            _ => (),
        }
    }
    if in_token {
        return false;
    }
    parts.push(&value[start..]);
    parts.into_iter().all(|part| {
        let Some(end) = part.find('}') else {
            return false;
        };
        let token = &part[..=end];
        if !matches!(token, "{app}" | "{install}" | "{user}")
            && !(token.starts_with("{pack:") && token.len() > "{pack:}".len())
        {
            return false;
        }
        let suffix = &part[end + 1..];
        let portable = suffix.replace('\\', "/");
        portable.is_empty()
            || portable
                .strip_prefix('/')
                .is_some_and(|path| relative(path).is_ok())
    })
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

#[cfg(test)]
mod directory_input_tests {
    use super::*;

    #[test]
    fn trailing_separators_are_removed_without_accepting_dot_or_relative_paths() {
        let base = normalized_directory(&std::env::temp_dir()).unwrap();
        let mut trailing = base.clone().into_os_string();
        trailing.push(std::path::MAIN_SEPARATOR_STR.repeat(3));
        assert_eq!(normalized_directory(Path::new(&trailing)).unwrap(), base);
        let mut dotted = base.clone().into_os_string();
        dotted.push(format!("{s}.{s}child{s}", s = std::path::MAIN_SEPARATOR));
        assert!(normalized_directory(Path::new(&dotted)).is_err());
        let mut parent = base.into_os_string();
        parent.push(format!("{s}..{s}child{s}", s = std::path::MAIN_SEPARATOR));
        assert!(normalized_directory(Path::new(&parent)).is_err());
        assert!(normalized_directory(Path::new("relative/directory/")).is_err());
    }

    #[cfg(windows)]
    #[test]
    fn windows_roots_remain_roots_and_unc_still_follows_the_existing_guard() {
        assert_eq!(
            normalized_directory(Path::new(r"C:\\\\")).unwrap(),
            PathBuf::from(r"C:\")
        );
        assert!(normalized_directory(Path::new(r"\\server\share\\")).is_err());
    }

    #[cfg(not(windows))]
    #[test]
    fn unix_root_remains_root_with_multiple_separators() {
        assert_eq!(
            normalized_directory(Path::new("////")).unwrap(),
            PathBuf::from("/")
        );
    }
}
