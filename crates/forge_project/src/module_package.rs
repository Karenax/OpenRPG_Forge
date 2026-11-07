//! Engine-neutral `.orpmod` v2/v3 admission contract over canonical Forge documents.
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
/// API 2 carries every source referenced by the canonical archive's edit history.
/// API 1 retains its original current-head-only source contract.
pub const MODULE_ARCHIVE_SOURCES_API_VERSION: u32 = 2;
pub const MODULE_VEGETATION_API_VERSION: u32 = 3;
pub const MODULE_ENVIRONMENT_API_VERSION: u32 = 4;
pub const MODULE_LOCAL_LIGHTS_API_VERSION: u32 = 5;
pub const MODULE_SOLAR_CLOCK_API_VERSION: u32 = 6;
pub const MODULE_OBJECTS_API_VERSION: u32 = 7;
pub const MODULE_TERRAIN_API_VERSION: u32 = 8;
pub const MODULE_OCEAN_API_VERSION: u32 = 9;
pub const MODULE_OCEAN_SURFACE_API_VERSION: u32 = 10;
/// Scene wind lives in the canonical zone and must survive undo history.
pub const MODULE_SCENE_WIND_API_VERSION: u32 = 11;
/// Explicit fish profiles are declared by API 12; runtime admission remains separate.
pub const MODULE_FISH_API_VERSION: u32 = 12;
/// Versioned transport for source physics and particle placement companions.
/// Runtime execution remains subject to a separate admission guard.
pub const MODULE_PLACEMENT_COMPANIONS_API_VERSION: u32 = 13;
/// Exact private model revisions retained for the authoring journal.
pub const MODULE_HISTORICAL_MODELS_API_VERSION: u32 = 14;
pub mod placement_companions;
pub use placement_companions::{ModulePlacementCompanion, ModulePlacementCompanionKind};
pub mod fish;
pub use fish::ModuleFishBinding;
pub mod ocean;
pub use ocean::{ModuleOceanBinding, ModuleOceanFiles};
pub mod terrain;
pub use terrain::{ModuleTerrainBinding, ModuleTerrainFiles, ModuleTerrainSource};
pub mod objects;
pub use objects::{ModuleObjectAsset, ModuleObjectBinding, ModuleObjectFiles, ModuleObjectSource};
pub mod local_lights;
pub use local_lights::ModuleLocalLightPackage;
pub mod environment;
pub use environment::{
    ModuleEnvironmentBinding, ModuleEnvironmentFiles, ModuleEnvironmentSource, ModuleSolarClock,
};
pub mod vegetation;
pub use vegetation::{ModuleFilePin, ModuleVegetationBinding, ModuleVegetationSource};
pub const MODULE_PACKAGE_CREATOR_VERSION: u32 = 3;
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
    /// Closed FFT input resources, separate from rendered water and its geometry.
    OceanSimulation,
    /// Closed optical surface programs and textures.
    OceanSurface,
    /// Closed source inputs for the shore simulation.
    ShoreSimulation,
    /// Explicit source object geometry/material packages, independent of terrain association.
    ObjectSource,
    /// Closed terrain material programs/textures explicitly associated with an area.
    TerrainSurface,
    ZoneArchive,
    SourceBlob,
    AssetCatalog,
    Asset,
    HistoricalModel,
    /// Sources, recipes and thumbnails whose references are checked by the catalog adapter.
    AssetAuxiliary,
    /// Existing vegetation catalogs, prototype indices and their closed resources.
    Vegetation,
    /// Closed environment/shader resources explicitly declared for an area.
    Environment,
    /// Original source-light descriptors, receipts and closed dependencies.
    LocalLights,
    /// Pinned source physics or particle package; not an executable program.
    PlacementCompanion,
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
    /// API 1: current head. API 2: full archive, including checkpoint/undo/redo sources.
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

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleHistoricalModel {
    pub asset_id: AssetId,
    pub payload: ModuleFilePin,
    pub license_path: String,
    pub static_triangle_meshes: bool,
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
    /// Older exact revisions used only when replaying the authored journal.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub historical_models: Vec<ModuleHistoricalModel>,
    /// Authored visual bindings never replace gameplay definitions or collision bodies.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub gameplay_visuals: Vec<crate::module_gameplay_visuals::GameplayVisualBinding>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub vegetation: Vec<ModuleVegetationBinding>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub environments: Vec<ModuleEnvironmentBinding>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub objects: Vec<ModuleObjectBinding>,
    /// Fish source profiles and their existing catalog, independently of surfaces.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub fish_profiles: Vec<ModuleFishBinding>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub terrain_surfaces: Vec<ModuleTerrainBinding>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub ocean_inputs: Vec<ModuleOceanBinding>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub local_light_packages: Vec<ModuleLocalLightPackage>,
    /// Source packages referenced by placements and their retained history.
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub placement_companions: Vec<ModulePlacementCompanion>,
    /// Shared definition is pinned by the canonical manifest and verified in every area.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub creator: Option<forge_gameplay::ModuleCreatorProfile>,
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
    #[error("invalid common module Creator definition: {0}")]
    Creator(String),
    #[error("invalid module package JSON: {0}")]
    Json(#[from] serde_json::Error),
    #[error("cannot hash module package: {0}")]
    Hash(#[from] forge_core::CanonicalJsonError),
}

impl ModulePackageManifest {
    pub fn validate(&self) -> Result<(), ModulePackageError> {
        let expected_version = if self.creator.is_some() {
            MODULE_PACKAGE_CREATOR_VERSION
        } else {
            MODULE_PACKAGE_VERSION
        };
        if self.schema != MODULE_PACKAGE_SCHEMA || self.version != expected_version {
            return Err(ModulePackageError::Schema);
        }
        self.validate_creator()?;
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
        self.validate_historical_models(&files, &mut used)?;
        self.validate_vegetation(&files, &mut used)?;
        self.validate_environments(&files, &mut used)?;
        self.validate_objects(&files, &mut used)?;
        self.validate_fish_profiles(&files, &mut used)?;
        self.validate_terrain_surfaces(&files, &mut used)?;
        self.validate_ocean_inputs(&files, &mut used)?;
        self.validate_local_light_packages(&files, &mut used)?;
        self.validate_placement_companions(&files, &mut used)?;
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
        if matches!(manifest.compatibility.module_api_version, 1 | 2)
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("vegetation")
                .is_some()
        {
            return Err(ModulePackageError::Compatibility);
        }
        if manifest.compatibility.module_api_version < MODULE_ENVIRONMENT_API_VERSION
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("environments")
                .is_some()
        {
            return Err(ModulePackageError::Compatibility);
        }
        if manifest.compatibility.module_api_version < MODULE_LOCAL_LIGHTS_API_VERSION
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("local_light_packages")
                .is_some()
        {
            return Err(ModulePackageError::Compatibility);
        }
        if manifest.compatibility.module_api_version < MODULE_SOLAR_CLOCK_API_VERSION
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("environments")
                .and_then(serde_json::Value::as_array)
                .is_some_and(|entries| {
                    entries
                        .iter()
                        .any(|entry| entry.get("solar_clock").is_some())
                })
        {
            return Err(ModulePackageError::Compatibility);
        }
        if manifest.compatibility.module_api_version < MODULE_OBJECTS_API_VERSION
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("objects")
                .is_some()
        {
            return Err(ModulePackageError::Compatibility);
        }
        if manifest.compatibility.module_api_version < MODULE_FISH_API_VERSION
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("fish_profiles")
                .is_some()
        {
            return Err(ModulePackageError::Compatibility);
        }
        if manifest.compatibility.module_api_version < MODULE_PLACEMENT_COMPANIONS_API_VERSION
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("placement_companions")
                .is_some()
        {
            return Err(ModulePackageError::Compatibility);
        }
        if manifest.compatibility.module_api_version < MODULE_HISTORICAL_MODELS_API_VERSION
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("historical_models")
                .is_some()
        {
            return Err(ModulePackageError::Compatibility);
        }
        if manifest.compatibility.module_api_version < MODULE_TERRAIN_API_VERSION
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("terrain_surfaces")
                .is_some()
        {
            return Err(ModulePackageError::Compatibility);
        }
        if manifest.compatibility.module_api_version < MODULE_OCEAN_API_VERSION
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("ocean_inputs")
                .is_some()
        {
            return Err(ModulePackageError::Compatibility);
        }
        if manifest.compatibility.module_api_version < MODULE_OCEAN_SURFACE_API_VERSION
            && serde_json::from_slice::<serde_json::Value>(bytes)?
                .get("ocean_inputs")
                .and_then(serde_json::Value::as_array)
                .is_some_and(|entries| {
                    entries.iter().any(|entry| {
                        entry.get("surface").is_some() || entry.get("shore_sim").is_some()
                    })
                })
        {
            return Err(ModulePackageError::Compatibility);
        }
        manifest.validate()?;
        Ok(manifest)
    }

    fn validate_creator(&self) -> Result<(), ModulePackageError> {
        if let Some(definition) = &self.creator {
            if definition.module_id != self.module_id {
                return Err(ModulePackageError::Creator(
                    "definition belongs to another module".into(),
                ));
            }
            definition
                .to_bytes()
                .map_err(|error| ModulePackageError::Creator(error.to_string()))?;
        }
        Ok(())
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
        if session.has_wind_history()
            && self.compatibility.module_api_version < MODULE_SCENE_WIND_API_VERSION
        {
            return Err(ModulePackageError::Compatibility);
        }
        let head = session.head();
        if head.base.terrain.project_id != self.project_id
            || head.base.terrain.area_id != area_id
            || head.content_hash().map_err(ModulePackageError::Archive)? != zone.head_hash
        {
            return Err(archive_error("zone identity or canonical head differs"));
        }
        let actual_gameplay = head.corrections.gameplay.as_deref();
        if let (Some(definition), Some(document)) = (&self.creator, actual_gameplay) {
            definition
                .validate_document(document)
                .map_err(|error| ModulePackageError::Creator(error.to_string()))?;
        }
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
        self.validate_zone_sources(&session, zone)?;
        self.validate_terrain_document(&head.effective().map_err(ModulePackageError::Archive)?.0)?;
        self.validate_zone_local_lights(&session, area_id)?;
        self.validate_zone_placement_companions(&session, area_id)?;
        self.validate_zone_assets(&session)?;
        Ok(())
    }

    fn validate_zone_sources(
        &self,
        session: &crate::zone_authoring::session::ZoneEditSession,
        zone: &ModuleZone,
    ) -> Result<(), ModulePackageError> {
        if matches!(
            self.compatibility.module_api_version,
            MODULE_ARCHIVE_SOURCES_API_VERSION
                | MODULE_VEGETATION_API_VERSION
                | MODULE_ENVIRONMENT_API_VERSION
                | MODULE_LOCAL_LIGHTS_API_VERSION
                | MODULE_SOLAR_CLOCK_API_VERSION
                | MODULE_OBJECTS_API_VERSION
                | MODULE_TERRAIN_API_VERSION
                | MODULE_OCEAN_API_VERSION
                | MODULE_OCEAN_SURFACE_API_VERSION
                | MODULE_SCENE_WIND_API_VERSION
                | MODULE_FISH_API_VERSION
                | MODULE_PLACEMENT_COMPANIONS_API_VERSION
                | MODULE_HISTORICAL_MODELS_API_VERSION
        ) {
            let sources = session
                .required_source_blobs()
                .map_err(ModulePackageError::Archive)?;
            for (&hash, &length) in &sources {
                self.require_blob_size(hash, length)?;
            }
            if sources.keys().copied().collect::<BTreeSet<_>>()
                != zone.source_blobs.iter().copied().collect()
            {
                return Err(archive_error("zone archive source blob set differs"));
            }
            return Ok(());
        }
        if self.compatibility.module_api_version != 1 {
            return Err(ModulePackageError::Compatibility);
        }
        let head = session.head();
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
        session: &crate::zone_authoring::session::ZoneEditSession,
    ) -> Result<(), ModulePackageError> {
        use crate::zone_authoring::source_objects::SourceObjectState;
        let head = session.head();
        self.validate_zone_vegetation_contract(head)?;
        let assets: BTreeMap<_, _> = self
            .assets
            .iter()
            .map(|asset| (asset.asset_id, asset.sha256))
            .collect();
        let object_assets: BTreeMap<_, _> = self
            .objects
            .iter()
            .filter(|binding| binding.area_id == head.base.terrain.area_id)
            .flat_map(|binding| match &binding.source {
                ModuleObjectSource::SourceSurfaces { assets, .. } => assets.iter(),
            })
            .map(|asset| (asset.asset_id, asset.sha256))
            .collect();
        let fish_assets: BTreeMap<_, _> = self
            .fish_profiles
            .iter()
            .filter(|binding| binding.area_id == head.base.terrain.area_id)
            .flat_map(|binding| &binding.assets)
            .map(|asset| (asset.asset_id, asset.sha256))
            .collect();
        let require = |placement: &crate::zone_authoring::placements::ManualPlacement| {
            if assets.get(&placement.asset_id) == Some(&placement.asset_hash)
                || object_assets.get(&placement.asset_id) == Some(&placement.asset_hash)
                || fish_assets.get(&placement.asset_id) == Some(&placement.asset_hash)
            {
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
        let require_source = |placement: &crate::zone_authoring::placements::ManualPlacement| {
            if crate::zone_authoring::imported_plants::is_source_asset(
                head,
                placement.asset_id,
                placement.asset_hash,
            ) {
                Ok(())
            } else {
                require(placement)
            }
        };
        for edit in head
            .corrections
            .source_object_edits
            .as_deref()
            .unwrap_or_default()
        {
            require_source(&edit.source)?;
            if let SourceObjectState::Edited { placement } = &edit.state {
                require_source(placement)?;
            }
            for copy in &edit.copies {
                require_source(copy)?;
            }
        }
        if (MODULE_FISH_API_VERSION..=MODULE_HISTORICAL_MODELS_API_VERSION)
            .contains(&self.compatibility.module_api_version)
        {
            let history: BTreeSet<_> = self
                .historical_models
                .iter()
                .map(|model| (model.asset_id, model.payload.sha256))
                .collect();
            session
                .visit_referenced_model_placements(|placement| {
                    if history.contains(&(placement.asset_id, placement.asset_hash)) {
                        Ok(())
                    } else {
                        require(placement).map_err(|error| error.to_string())
                    }
                })
                .map_err(ModulePackageError::Archive)?;
        }
        Ok(())
    }

    fn validate_historical_models<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if self.historical_models.is_empty() {
            return Ok(());
        }
        if self.compatibility.module_api_version < MODULE_HISTORICAL_MODELS_API_VERSION
            || self.provenance.rights != ModuleRights::PrivateLocalOnly
        {
            return Err(ModulePackageError::Compatibility);
        }
        if self.historical_models.len() > MAX_MODULE_ASSETS {
            return Err(ModulePackageError::Limit("historical models"));
        }
        let active: BTreeSet<_> = self.assets.iter().map(|a| (a.asset_id, a.sha256)).collect();
        let licenses: BTreeSet<_> = self
            .provenance
            .license_paths
            .iter()
            .map(String::as_str)
            .collect();
        let mut revisions = BTreeSet::new();
        for model in &self.historical_models {
            let pin = (model.asset_id, model.payload.sha256);
            if active.contains(&pin) || !revisions.insert(pin) {
                return Err(ModulePackageError::Duplicate);
            }
            let file = refer(files, &model.payload.path, ModuleFileRole::HistoricalModel)?;
            if file.sha256 != model.payload.sha256
                || !file.path.ends_with(".glb")
                || model.asset_id.as_uuid().is_nil()
                || !licenses.contains(model.license_path.as_str())
            {
                return Err(ModulePackageError::Reference(
                    "historical model payload or license",
                ));
            }
            used.insert(model.payload.path.as_str());
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
