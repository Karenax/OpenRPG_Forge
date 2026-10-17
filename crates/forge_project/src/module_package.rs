//! Engine-neutral `.orpmod` v2 admission contract over canonical Forge documents.
//!
//! The transport owns `module.json`; its digest is not listed inside itself.
//! `files` is the closed inventory of every other payload file. This module does
//! not extract files, execute programs, infer redistribution rights or decode assets.
use forge_core::{AreaId, AssetId, ContentHash, ModuleId, ProjectId};
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use thiserror::Error;

pub const MODULE_PACKAGE_SCHEMA: &str = "forge.module-package";
pub const MODULE_PACKAGE_VERSION: u32 = 2;
pub const MODULE_MANIFEST_PATH: &str = "module.json";
pub const MAX_MODULE_MANIFEST_BYTES: usize = 4 * 1024 * 1024;
pub const MAX_MODULE_FILES: usize = 100_000;
pub const MAX_MODULE_ASSETS: usize = 65_536;
pub const MAX_MODULE_ZONES: usize = 4096;
pub const MAX_MODULE_DEPENDENCIES: usize = 128;
pub const MAX_MODULE_FILE_BYTES: u64 = 16 * 1024 * 1024 * 1024;
pub const MAX_MODULE_PAYLOAD_BYTES: u64 = 256 * 1024 * 1024 * 1024;

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ModuleFileRole {
    ZoneArchive,
    SourceBlob,
    AssetCatalog,
    Asset,
    /// Sources, recipes and thumbnails whose references are checked by the catalog adapter.
    AssetAuxiliary,
    License,
    Provenance,
    Credits,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleFile {
    pub path: String,
    pub bytes: u64,
    pub sha256: ContentHash,
    pub role: ModuleFileRole,
}

/// Exact engine version and public module API; adapters must check both.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleCompatibility {
    pub engine_version: String,
    pub module_api_version: u32,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ModuleRights {
    PrivateLocalOnly,
    Redistributable,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleProvenance {
    pub publisher: String,
    pub source: String,
    pub source_revision: String,
    pub rights: ModuleRights,
    pub license_paths: Vec<String>,
    pub proof_paths: Vec<String>,
    pub credits_path: String,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleZone {
    pub area_id: AreaId,
    pub archive_path: String,
    pub archive_sha256: ContentHash,
    pub head_hash: ContentHash,
    /// Absence is explicit: this zone contains no authored RPG document.
    pub gameplay_sha256: Option<ContentHash>,
    /// Closed source-blob hashes referenced by this zone's canonical head.
    pub source_blobs: Vec<ContentHash>,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleAsset {
    pub asset_id: AssetId,
    pub file_path: String,
    pub sha256: ContentHash,
    pub dependencies: Vec<AssetId>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModulePackageManifest {
    pub schema: String,
    pub version: u32,
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub name: String,
    pub module_version: String,
    pub compatibility: ModuleCompatibility,
    pub provenance: ModuleProvenance,
    pub asset_catalog_path: String,
    pub files: Vec<ModuleFile>,
    pub zones: Vec<ModuleZone>,
    pub assets: Vec<ModuleAsset>,
    /// Authored visual bindings never replace gameplay definitions or collision bodies.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub gameplay_visuals: Vec<crate::module_gameplay_visuals::GameplayVisualBinding>,
}

#[derive(Debug, Error)]
pub enum ModulePackageError {
    #[error("unsupported module package schema or version")]
    Schema,
    #[error("module package limit exceeded: {0}")]
    Limit(&'static str),
    #[error("invalid module package text: {0}")]
    Text(&'static str),
    #[error("unsafe or reserved module path: {0}")]
    Path(String),
    #[error("duplicate module package identity or path")]
    Duplicate,
    #[error("invalid module package reference: {0}")]
    Reference(&'static str),
    #[error("module engine/API compatibility does not match")]
    Compatibility,
    #[error("module payload inventory does not match")]
    Inventory,
    #[error("invalid canonical zone archive: {0}")]
    Archive(String),
    #[error("invalid module package JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash module package: {0}")]
    Hash(#[from] forge_core::CanonicalJsonError),
}

impl ModulePackageManifest {
    pub fn validate(&self) -> Result<(), ModulePackageError> {
        if self.schema != MODULE_PACKAGE_SCHEMA || self.version != MODULE_PACKAGE_VERSION {
            return Err(ModulePackageError::Schema);
        }
        text(&self.name, 256, "name")?;
        text(&self.module_version, 128, "module version")?;
        text(&self.compatibility.engine_version, 128, "engine version")?;
        if self.compatibility.module_api_version == 0 {
            return Err(ModulePackageError::Compatibility);
        }
        text(&self.provenance.publisher, 256, "publisher")?;
        text(&self.provenance.source, 2048, "source")?;
        text(&self.provenance.source_revision, 256, "source revision")?;
        if self.files.is_empty() || self.files.len() > MAX_MODULE_FILES {
            return Err(ModulePackageError::Limit("files"));
        }
        if self.zones.is_empty() || self.zones.len() > MAX_MODULE_ZONES {
            return Err(ModulePackageError::Limit("zones"));
        }
        if self.assets.len() > MAX_MODULE_ASSETS {
            return Err(ModulePackageError::Limit("assets"));
        }
        let areas = self
            .zones
            .iter()
            .map(|zone| zone.area_id)
            .collect::<Vec<_>>();
        let pins = self
            .assets
            .iter()
            .map(|asset| crate::module_gameplay_visuals::VisualAssetPin {
                asset_id: asset.asset_id,
                sha256: asset.sha256,
            })
            .collect::<Vec<_>>();
        crate::module_gameplay_visuals::validate_catalog(&self.gameplay_visuals, &areas, &pins)
            .map_err(|_| ModulePackageError::Reference("gameplay visual catalog"))?;
        let files = self.file_index()?;
        let mut used = BTreeSet::new();
        refer(
            &files,
            &self.asset_catalog_path,
            ModuleFileRole::AssetCatalog,
        )?;
        used.insert(self.asset_catalog_path.as_str());
        // The pure contract pins these bytes. The catalog adapter must prove
        // their exact thumbnail/durable-source references before admission.
        used.extend(
            self.files
                .iter()
                .filter(|file| file.role == ModuleFileRole::AssetAuxiliary)
                .map(|file| file.path.as_str()),
        );
        self.validate_provenance(&files, &mut used)?;
        self.validate_zones(&files, &mut used)?;
        self.validate_assets(&files, &mut used)?;
        if used.len() != files.len() {
            return Err(ModulePackageError::Reference("unreferenced payload file"));
        }
        let mut counter = ManifestSize::default();
        if let Err(error) = serde_json::to_writer(&mut counter, self) {
            return Err(if counter.exceeded {
                ModulePackageError::Limit("manifest bytes")
            } else {
                ModulePackageError::Json(error)
            });
        }
        Ok(())
    }

    pub fn from_bytes(bytes: &[u8]) -> Result<Self, ModulePackageError> {
        if bytes.len() > MAX_MODULE_MANIFEST_BYTES {
            return Err(ModulePackageError::Limit("manifest bytes"));
        }
        let manifest: Self = serde_json::from_slice(bytes)?;
        manifest.validate()?;
        Ok(manifest)
    }

    pub fn to_bytes(&self) -> Result<Vec<u8>, ModulePackageError> {
        self.validate()?;
        let bytes = serde_json::to_vec(self)?;
        if bytes.len() > MAX_MODULE_MANIFEST_BYTES {
            return Err(ModulePackageError::Limit("manifest bytes"));
        }
        Ok(bytes)
    }

    pub fn content_hash(&self) -> Result<ContentHash, ModulePackageError> {
        // The serialized byte bound applies equally to an already constructed value.
        self.to_bytes()?;
        Ok(forge_core::canonical_json_sha256(self)?)
    }

    pub fn check_compatibility(
        &self,
        engine_version: &str,
        module_api_version: u32,
    ) -> Result<(), ModulePackageError> {
        self.validate()?;
        if self.compatibility.engine_version != engine_version
            || self.compatibility.module_api_version != module_api_version
        {
            return Err(ModulePackageError::Compatibility);
        }
        Ok(())
    }

    /// Compare actual payload entries, excluding transport-owned `module.json`.
    pub fn validate_inventory(&self, actual: &[ModuleFile]) -> Result<(), ModulePackageError> {
        self.validate()?;
        if actual.len() != self.files.len() {
            return Err(ModulePackageError::Inventory);
        }
        let expected: BTreeMap<_, _> = self.files.iter().map(|file| (&file.path, file)).collect();
        let mut seen = BTreeSet::new();
        for file in actual {
            if !seen.insert(&file.path) || expected.get(&file.path) != Some(&file) {
                return Err(ModulePackageError::Inventory);
            }
        }
        Ok(())
    }

    /// Verify bytes through the existing canonical zone archive reader.
    pub fn validate_zone_archive(
        &self,
        area_id: AreaId,
        bytes: &[u8],
    ) -> Result<(), ModulePackageError> {
        use crate::zone_authoring::session::ZoneEditSession;
        self.validate()?;
        let zone = self
            .zones
            .iter()
            .find(|zone| zone.area_id == area_id)
            .ok_or_else(|| archive_error("area is not declared"))?;
        let file = self
            .files
            .iter()
            .find(|file| file.path == zone.archive_path)
            .ok_or_else(|| archive_error("archive is not declared"))?;
        if bytes.len() > crate::zone_authoring::MAX_ZONE_HEAD_BYTES
            || u64::try_from(bytes.len()).ok() != Some(file.bytes)
            || forge_core::bytes_sha256(bytes) != zone.archive_sha256
        {
            return Err(archive_error("archive byte length or hash differs"));
        }
        let session =
            ZoneEditSession::from_archive_bytes(bytes).map_err(ModulePackageError::Archive)?;
        let head = session.head();
        if head.base.terrain.project_id != self.project_id
            || head.base.terrain.area_id != area_id
            || head.content_hash().map_err(ModulePackageError::Archive)? != zone.head_hash
        {
            return Err(archive_error("zone identity or canonical head differs"));
        }
        let actual_gameplay = head.corrections.gameplay.as_deref();
        if actual_gameplay.is_some_and(|gameplay| gameplay.module_id != self.module_id)
            || actual_gameplay
                .map(forge_core::canonical_json_sha256)
                .transpose()?
                != zone.gameplay_sha256
        {
            return Err(archive_error("gameplay module identity or hash differs"));
        }
        let bindings = self
            .gameplay_visuals
            .iter()
            .filter(|binding| binding.area_id == area_id)
            .cloned()
            .collect::<Vec<_>>();
        if head
            .corrections
            .gameplay_visuals
            .as_deref()
            .unwrap_or_default()
            != bindings.as_slice()
        {
            return Err(archive_error(
                "manifest gameplay visuals differ from canonical zone",
            ));
        }
        if let Some(document) = actual_gameplay {
            let pins = self
                .assets
                .iter()
                .map(|asset| crate::module_gameplay_visuals::VisualAssetPin {
                    asset_id: asset.asset_id,
                    sha256: asset.sha256,
                })
                .collect::<Vec<_>>();
            crate::module_gameplay_visuals::validate_for_document(&bindings, document, &pins)
                .map_err(ModulePackageError::Archive)?;
        } else if !bindings.is_empty() {
            return Err(archive_error(
                "gameplay visual binding targets a zone without gameplay",
            ));
        }
        self.validate_zone_sources(head, zone)?;
        self.validate_zone_assets(head)?;
        Ok(())
    }

    fn validate_zone_sources(
        &self,
        head: &crate::zone_authoring::ZoneAuthoringHead,
        zone: &ModuleZone,
    ) -> Result<(), ModulePackageError> {
        let (terrain, vegetation) = head.effective().map_err(ModulePackageError::Archive)?;
        let mut blobs = BTreeSet::new();
        if let Some(source) = &terrain.sampled_source {
            self.require_blob_size(
                source.samples_sha256,
                source
                    .byte_length()
                    .map_err(|error| archive_error(&error.to_string()))?,
            )?;
            blobs.insert(source.samples_sha256);
        }
        if let Some(source) = vegetation.as_ref().and_then(|v| v.imported_source.as_ref()) {
            for block in &source.blocks {
                self.require_blob_size(
                    block.instances_sha256,
                    block.byte_len().map_err(ModulePackageError::Archive)?,
                )?;
                blobs.insert(block.instances_sha256);
            }
        }
        if blobs != zone.source_blobs.iter().copied().collect() {
            return Err(archive_error("zone source blob set differs"));
        }
        Ok(())
    }

    fn require_blob_size(&self, hash: ContentHash, bytes: usize) -> Result<(), ModulePackageError> {
        if self.files.iter().any(|file| {
            file.role == ModuleFileRole::SourceBlob
                && file.sha256 == hash
                && Some(file.bytes) == u64::try_from(bytes).ok()
        }) {
            Ok(())
        } else {
            Err(archive_error("source blob length or hash is not declared"))
        }
    }

    fn validate_zone_assets(
        &self,
        head: &crate::zone_authoring::ZoneAuthoringHead,
    ) -> Result<(), ModulePackageError> {
        use crate::zone_authoring::source_objects::SourceObjectState;
        // These historical contracts name separate catalogs/packages which the
        // v2 adapter does not resolve yet. Refuse them instead of silently
        // substituting a built-in prototype or omitting companion resources.
        if head
            .corrections
            .species
            .as_ref()
            .is_some_and(|species| !species.is_empty())
        {
            return Err(archive_error(
                "species catalogs are not supported by this module API",
            ));
        }
        if head
            .corrections
            .imported_plant_placements
            .as_ref()
            .is_some_and(|placements| !placements.is_empty())
        {
            return Err(archive_error(
                "imported plant copies require a resolved prototype catalog",
            ));
        }
        if head
            .base
            .vegetation
            .as_ref()
            .is_some_and(|vegetation| vegetation.imported_source.is_some())
        {
            return Err(archive_error(
                "imported vegetation prototype catalogs are not supported by this module API",
            ));
        }
        let assets: BTreeMap<_, _> = self
            .assets
            .iter()
            .map(|asset| (asset.asset_id, asset.sha256))
            .collect();
        let require = |placement: &crate::zone_authoring::placements::ManualPlacement| {
            if placement.physics.is_some() || placement.particles.is_some() {
                return Err(archive_error(
                    "physics and particle companion packages are not supported by this module API",
                ));
            }
            if assets.get(&placement.asset_id) == Some(&placement.asset_hash) {
                Ok(())
            } else {
                Err(archive_error(
                    "placement asset identity or pin is not declared",
                ))
            }
        };
        for placement in head.corrections.placements.as_deref().unwrap_or_default() {
            require(placement)?;
        }
        for edit in head
            .corrections
            .source_object_edits
            .as_deref()
            .unwrap_or_default()
        {
            require(&edit.source)?;
            if let SourceObjectState::Edited { placement } = &edit.state {
                require(placement)?;
            }
            for copy in &edit.copies {
                require(copy)?;
            }
        }
        Ok(())
    }

    fn file_index(&self) -> Result<BTreeMap<&str, &ModuleFile>, ModulePackageError> {
        let mut files = BTreeMap::new();
        let mut aliases = BTreeSet::new();
        let mut bytes = 0_u64;
        for file in &self.files {
            portable_path(&file.path)?;
            if file.bytes == 0 || file.bytes > MAX_MODULE_FILE_BYTES {
                return Err(ModulePackageError::Limit("file bytes"));
            }
            if file.role == ModuleFileRole::ZoneArchive
                && file.bytes > crate::zone_authoring::MAX_ZONE_HEAD_BYTES as u64
            {
                return Err(ModulePackageError::Limit("zone archive bytes"));
            }
            bytes = bytes
                .checked_add(file.bytes)
                .ok_or(ModulePackageError::Limit("payload bytes"))?;
            if bytes > MAX_MODULE_PAYLOAD_BYTES {
                return Err(ModulePackageError::Limit("payload bytes"));
            }
            if !aliases.insert(file.path.to_ascii_lowercase()) {
                return Err(ModulePackageError::Duplicate);
            }
            files.insert(file.path.as_str(), file);
        }
        for name in &aliases {
            let mut parent = name.as_str();
            while let Some((prefix, _)) = parent.rsplit_once('/') {
                if aliases.contains(prefix) {
                    return Err(ModulePackageError::Duplicate);
                }
                parent = prefix;
            }
        }
        Ok(files)
    }

    fn validate_provenance<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if self.provenance.license_paths.is_empty() || self.provenance.proof_paths.is_empty() {
            return Err(ModulePackageError::Reference(
                "licenses and provenance are required",
            ));
        }
        for (paths, role) in [
            (&self.provenance.license_paths, ModuleFileRole::License),
            (&self.provenance.proof_paths, ModuleFileRole::Provenance),
        ] {
            if paths.len() > MAX_MODULE_FILES {
                return Err(ModulePackageError::Limit("provenance references"));
            }
            for path in paths {
                refer(files, path, role)?;
                if !used.insert(path.as_str()) {
                    return Err(ModulePackageError::Duplicate);
                }
            }
        }
        refer(
            files,
            &self.provenance.credits_path,
            ModuleFileRole::Credits,
        )?;
        used.insert(self.provenance.credits_path.as_str());
        Ok(())
    }

    fn validate_zones<'a>(
        &'a self,
        files: &BTreeMap<&'a str, &'a ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        let mut areas = BTreeSet::new();
        let mut blob_files = BTreeMap::new();
        for file in files
            .values()
            .filter(|file| file.role == ModuleFileRole::SourceBlob)
        {
            if blob_files.insert(file.sha256, file.path.as_str()).is_some() {
                return Err(ModulePackageError::Duplicate);
            }
        }
        for zone in &self.zones {
            if !areas.insert(zone.area_id) || !used.insert(zone.archive_path.as_str()) {
                return Err(ModulePackageError::Duplicate);
            }
            let file = refer(files, &zone.archive_path, ModuleFileRole::ZoneArchive)?;
            if file.sha256 != zone.archive_sha256 {
                return Err(ModulePackageError::Reference("zone archive hash"));
            }
            if zone.source_blobs.len() > MAX_MODULE_FILES {
                return Err(ModulePackageError::Limit("zone source blobs"));
            }
            let mut seen = BTreeSet::new();
            for blob in &zone.source_blobs {
                if !seen.insert(blob) {
                    return Err(ModulePackageError::Duplicate);
                }
                used.insert(
                    *blob_files
                        .get(blob)
                        .ok_or(ModulePackageError::Reference("missing source blob"))?,
                );
            }
        }
        Ok(())
    }

    fn validate_assets<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        let mut assets = BTreeMap::new();
        for asset in &self.assets {
            if assets.insert(asset.asset_id, asset).is_some() {
                return Err(ModulePackageError::Duplicate);
            }
            // Distinct semantic identities may share one content-addressed file.
            // The file inventory remains unique, and each identity checks its pin below.
            used.insert(asset.file_path.as_str());
            let file = refer(files, &asset.file_path, ModuleFileRole::Asset)?;
            if file.sha256 != asset.sha256 {
                return Err(ModulePackageError::Reference("asset payload hash"));
            }
            if asset.dependencies.len() > MAX_MODULE_DEPENDENCIES {
                return Err(ModulePackageError::Limit("asset dependencies"));
            }
        }
        for asset in &self.assets {
            let mut dependencies = BTreeSet::new();
            for dependency in &asset.dependencies {
                if *dependency == asset.asset_id || !assets.contains_key(dependency) {
                    return Err(ModulePackageError::Reference(
                        "missing or self asset dependency",
                    ));
                }
                if !dependencies.insert(*dependency) {
                    return Err(ModulePackageError::Duplicate);
                }
            }
        }
        // Kahn's algorithm bounds work and stack use even for adversarial long graphs.
        let mut pending: BTreeMap<_, _> = assets
            .iter()
            .map(|(id, asset)| (*id, asset.dependencies.len()))
            .collect();
        let mut dependents: BTreeMap<AssetId, Vec<AssetId>> = BTreeMap::new();
        for asset in &self.assets {
            for dependency in &asset.dependencies {
                dependents
                    .entry(*dependency)
                    .or_default()
                    .push(asset.asset_id);
            }
        }
        let mut ready: Vec<_> = pending
            .iter()
            .filter_map(|(id, count)| (*count == 0).then_some(*id))
            .collect();
        let mut visited = 0;
        while let Some(id) = ready.pop() {
            visited += 1;
            if let Some(children) = dependents.get(&id) {
                for child in children {
                    let count = pending
                        .get_mut(child)
                        .expect("validated dependency identity");
                    *count -= 1;
                    if *count == 0 {
                        ready.push(*child);
                    }
                }
            }
        }
        if visited != assets.len() {
            return Err(ModulePackageError::Reference("cyclic asset dependencies"));
        }
        Ok(())
    }
}

fn archive_error(message: &str) -> ModulePackageError {
    ModulePackageError::Archive(message.into())
}

fn text(value: &str, maximum: usize, field: &'static str) -> Result<(), ModulePackageError> {
    if value.trim().is_empty()
        || value != value.trim()
        || value.len() > maximum
        || value.chars().any(char::is_control)
    {
        return Err(ModulePackageError::Text(field));
    }
    Ok(())
}

fn refer<'a>(
    files: &BTreeMap<&str, &'a ModuleFile>,
    path: &str,
    role: ModuleFileRole,
) -> Result<&'a ModuleFile, ModulePackageError> {
    portable_path(path)?;
    files
        .get(path)
        .copied()
        .filter(|file| file.role == role)
        .ok_or(ModulePackageError::Reference(
            "missing file or incorrect role",
        ))
}

fn portable_path(path: &str) -> Result<(), ModulePackageError> {
    let invalid = || ModulePackageError::Path(path.into());
    if path.is_empty()
        || path.len() > 1024
        || !path.is_ascii()
        || path.contains(['\\', ':'])
        || path.chars().any(char::is_control)
        || path.eq_ignore_ascii_case(MODULE_MANIFEST_PATH)
        || path.eq_ignore_ascii_case("package-manifest.json")
        || path.split('/').next().is_some_and(|part| {
            part.eq_ignore_ascii_case(MODULE_MANIFEST_PATH)
                || part.eq_ignore_ascii_case("package-manifest.json")
        })
    {
        return Err(invalid());
    }
    for part in path.split('/') {
        let stem = part
            .split('.')
            .next()
            .unwrap_or_default()
            .trim_end()
            .to_ascii_uppercase();
        let numbered_device = ["COM", "LPT"].iter().any(|prefix| {
            stem.strip_prefix(prefix).is_some_and(|suffix| {
                matches!(
                    suffix,
                    "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" | "¹" | "²" | "³"
                )
            })
        });
        if part.is_empty()
            || matches!(part, "." | "..")
            || part.ends_with(['.', ' '])
            || part.contains(['<', '>', '"', '|', '?', '*'])
            || matches!(
                stem.as_str(),
                "CON" | "PRN" | "AUX" | "NUL" | "CLOCK$" | "CONIN$" | "CONOUT$"
            )
            || numbered_device
        {
            return Err(invalid());
        }
    }
    let extension = path
        .rsplit('.')
        .next()
        .unwrap_or_default()
        .to_ascii_lowercase();
    if matches!(
        extension.as_str(),
        "exe"
            | "dll"
            | "so"
            | "dylib"
            | "sys"
            | "ocx"
            | "cpl"
            | "msi"
            | "msp"
            | "bat"
            | "cmd"
            | "ps1"
            | "psm1"
            | "com"
            | "scr"
            | "vbs"
            | "vbe"
            | "wsf"
            | "wsh"
            | "jse"
            | "hta"
            | "wasm"
    ) {
        return Err(invalid());
    }
    Ok(())
}

#[derive(Default)]
struct ManifestSize {
    bytes: usize,
    exceeded: bool,
}

impl std::io::Write for ManifestSize {
    fn write(&mut self, bytes: &[u8]) -> std::io::Result<usize> {
        if let Some(total) = self
            .bytes
            .checked_add(bytes.len())
            .filter(|total| *total <= MAX_MODULE_MANIFEST_BYTES)
        {
            self.bytes = total;
            Ok(bytes.len())
        } else {
            self.exceeded = true;
            Err(std::io::Error::other("module manifest byte limit exceeded"))
        }
    }

    fn flush(&mut self) -> std::io::Result<()> {
        Ok(())
    }
}
