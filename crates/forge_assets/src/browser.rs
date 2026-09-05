//! Cross-platform snapshot contracts plus native, offline subset discovery.

use forge_core::{AssetId, CanonicalJsonError, ContentHash, canonical_json_sha256};
use serde::{Deserialize, Serialize};

pub const ASSET_BROWSER_SNAPSHOT_SCHEMA: &str = "forge.asset-browser-snapshot";
pub const ASSET_BROWSER_SNAPSHOT_VERSION: u32 = 1;
pub const ASSET_SUBSET_MANIFEST_SCHEMA: &str = "forge.asset-subset-manifest";
pub const ASSET_SUBSET_MANIFEST_VERSION: u32 = 1;
pub const ASSET_SUBSET_MANIFEST_FILE: &str = "subset.manifest.json";
pub const MAX_ASSET_SCAN_DEPTH: usize = 6;
pub const MAX_ASSET_SCAN_DIRECTORIES: usize = 256;
pub const MAX_ASSET_MANIFESTS: usize = 32;
pub const MAX_ASSET_MODELS: usize = 512;
pub const MAX_ASSET_FILES: usize = 4_096;
pub const MAX_ASSET_SCAN_ENTRIES: usize =
    MAX_ASSET_SCAN_DIRECTORIES + MAX_ASSET_FILES + MAX_ASSET_MANIFESTS;
pub const MAX_ASSET_MANIFEST_BYTES: u64 = 256 * 1024;
pub const MAX_ASSET_MANIFEST_TOTAL_BYTES: u64 = 4 * 1024 * 1024;
pub const MAX_ASSET_FILE_BYTES: u64 = 128 * 1024 * 1024;
pub const MAX_ASSET_FILE_TOTAL_BYTES: u64 = 512 * 1024 * 1024;
pub const MAX_ASSET_PATH_BYTES: usize = 256;
pub const MAX_GLTF_JSON_BYTES: u64 = 8 * 1024 * 1024;
pub const MAX_GLTF_JSON_TOTAL_BYTES: u64 = 32 * 1024 * 1024;
pub const MAX_GLTF_URIS_PER_SCENE: usize = 512;
pub const MAX_GLTF_URIS_TOTAL: usize = 4_096;

#[cfg(not(target_arch = "wasm32"))]
const MAX_IDENTIFIER_BYTES: usize = 128;
#[cfg(not(target_arch = "wasm32"))]
const MAX_SHORT_TEXT_BYTES: usize = 256;
#[cfg(not(target_arch = "wasm32"))]
const MAX_URL_BYTES: usize = 2_048;
#[cfg(not(target_arch = "wasm32"))]
const MAX_ARCHIVE_BYTES: u64 = 16 * 1024 * 1024 * 1024;
#[cfg(not(target_arch = "wasm32"))]
const ASSET_ID_KEY_PREFIX: &str = "forge.asset-browser:model";

/// Forge-owned immutable pin, intentionally independent from the scanned manifest.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct GovernedAssetSubsetPin {
    pub subset_id: &'static str,
    pub pack_id: &'static str,
    pub manifest_hash: ContentHash,
    pub archive_hash: ContentHash,
    pub normalized_metadata_hash: ContentHash,
    pub license_spdx: &'static str,
    pub license_evidence_path: &'static str,
    pub license_evidence_hash: ContentHash,
}

/// The only external subset governed by the R2-026 browser proof.
pub const GOVERNED_ASSET_SUBSET_PINS: &[GovernedAssetSubsetPin] = &[GovernedAssetSubsetPin {
    subset_id: "visual-valley-quaternius-stylized-nature-standard-1.0",
    pack_id: "quaternius:stylized-nature-megakit-standard-1.0",
    manifest_hash: ContentHash::from_bytes([
        0xc8, 0x6c, 0x44, 0x54, 0xb3, 0xb0, 0xae, 0x4c, 0xa0, 0xbb, 0x54, 0x6d, 0x22, 0xb2, 0x9f,
        0x3e, 0x79, 0xa0, 0xbf, 0xd8, 0x10, 0x7e, 0xf6, 0x3b, 0x8a, 0xf1, 0xf0, 0xeb, 0xbf, 0xfd,
        0x66, 0x77,
    ]),
    archive_hash: ContentHash::from_bytes([
        0x29, 0x8f, 0x67, 0x32, 0xb8, 0x72, 0xe4, 0xcf, 0x7b, 0x30, 0xe6, 0xe7, 0xab, 0xf9, 0x64,
        0x1c, 0x7f, 0x6d, 0xc6, 0xb3, 0x26, 0xdf, 0x37, 0xac, 0x08, 0x95, 0x33, 0xed, 0x7e, 0x3d,
        0x58, 0xc9,
    ]),
    normalized_metadata_hash: ContentHash::from_bytes([
        0xe3, 0x28, 0x6b, 0x81, 0xf3, 0xa6, 0x17, 0x1e, 0xb8, 0x44, 0xa8, 0x35, 0x28, 0xea, 0xc5,
        0x5b, 0x27, 0x27, 0xb2, 0xef, 0x8e, 0x3f, 0x96, 0xd1, 0xd4, 0xe2, 0x9f, 0xad, 0xdf, 0x5b,
        0x45, 0x8d,
    ]),
    license_spdx: "CC0-1.0",
    license_evidence_path: "License_Standard.txt",
    license_evidence_hash: ContentHash::from_bytes([
        0x12, 0x07, 0x10, 0xe5, 0x42, 0xc3, 0xeb, 0xaf, 0x83, 0x85, 0x6c, 0x4e, 0xb5, 0x5b, 0x4a,
        0x1e, 0x68, 0x05, 0x93, 0xb3, 0x93, 0x02, 0xeb, 0xa7, 0xfd, 0xc5, 0xd9, 0x42, 0xdf, 0xe7,
        0x8c, 0x58,
    ]),
}];

/// Browser categories accepted by the v1 subset-manifest contract.
#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum AssetCategory {
    Detail,
    Fern,
    Rock,
    Shrub,
    Tree,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum AssetFileTransformation {
    ByteForByteCopy,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetSourceProvenance {
    pub pack_id: String,
    pub name: String,
    pub version: String,
    pub author: String,
    pub source_page: String,
    pub distribution_page: String,
    pub archive_url: String,
    pub retrieved_at: String,
    pub archive_hash: ContentHash,
    pub archive_bytes: u64,
    pub normalized_metadata_hash: ContentHash,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct VerifiedAssetLicense {
    pub spdx: String,
    pub url: String,
    pub attribution_required: bool,
    /// Root-relative path of the verified evidence file.
    pub evidence_path: String,
    pub evidence_hash: ContentHash,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetFileSnapshot {
    /// Root-relative portable path.
    pub relative_path: String,
    pub source_path: String,
    pub bytes: u64,
    pub content_hash: ContentHash,
    pub transformation: AssetFileTransformation,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetSubsetSnapshot {
    pub subset_id: String,
    /// Root-relative path of the raw manifest.
    pub manifest_path: String,
    pub manifest_hash: ContentHash,
    pub source: AssetSourceProvenance,
    pub license: VerifiedAssetLicense,
    pub files: Vec<AssetFileSnapshot>,
}

impl AssetSubsetSnapshot {
    /// Counts model payload/dependency files, excluding the separately bound license evidence.
    #[must_use]
    pub fn content_file_count(&self) -> usize {
        self.files
            .iter()
            .filter(|file| file.relative_path != self.license.evidence_path)
            .count()
    }
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetBrowserProvenance {
    pub subset_id: String,
    pub pack_id: String,
    pub author: String,
    pub source_version: String,
    pub retrieved_at: String,
    pub source_path: String,
    pub license_spdx: String,
    pub license_verified: bool,
    pub attribution_required: bool,
    pub manifest_hash: ContentHash,
    pub scene_content_hash: ContentHash,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetBrowserEntry {
    pub id: AssetId,
    /// Source-governed lowercase key used to derive `id`.
    pub stable_key: String,
    pub model_id: String,
    pub category: AssetCategory,
    pub scene_path: String,
    pub base_scale: f32,
    pub provenance: AssetBrowserProvenance,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetCategorySnapshot {
    pub category: AssetCategory,
    pub asset_ids: Vec<AssetId>,
}

/// Derived, non-persistent view of all verified subset manifests under one root.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetBrowserSnapshot {
    pub schema: String,
    pub schema_version: u32,
    pub snapshot_hash: ContentHash,
    pub manifests: Vec<AssetSubsetSnapshot>,
    pub categories: Vec<AssetCategorySnapshot>,
    /// Entries are sorted by stable `AssetId`.
    pub assets: Vec<AssetBrowserEntry>,
}

impl AssetBrowserSnapshot {
    pub fn recompute_hash(&self) -> Result<ContentHash, CanonicalJsonError> {
        hash_snapshot(
            &self.schema,
            self.schema_version,
            &self.manifests,
            &self.categories,
            &self.assets,
        )
    }

    /// Counts verified model payload/dependency files across all manifests.
    #[must_use]
    pub fn content_file_count(&self) -> usize {
        self.manifests
            .iter()
            .map(AssetSubsetSnapshot::content_file_count)
            .sum()
    }
}

#[derive(Serialize)]
#[serde(deny_unknown_fields)]
struct SnapshotHashInput<'a> {
    schema: &'a str,
    schema_version: u32,
    manifests: &'a [AssetSubsetSnapshot],
    categories: &'a [AssetCategorySnapshot],
    assets: &'a [AssetBrowserEntry],
}

fn hash_snapshot(
    schema: &str,
    schema_version: u32,
    manifests: &[AssetSubsetSnapshot],
    categories: &[AssetCategorySnapshot],
    assets: &[AssetBrowserEntry],
) -> Result<ContentHash, CanonicalJsonError> {
    canonical_json_sha256(&SnapshotHashInput {
        schema,
        schema_version,
        manifests,
        categories,
        assets,
    })
}

#[cfg(not(target_arch = "wasm32"))]
mod native {
    use super::{
        ASSET_BROWSER_SNAPSHOT_SCHEMA, ASSET_BROWSER_SNAPSHOT_VERSION, ASSET_ID_KEY_PREFIX,
        ASSET_SUBSET_MANIFEST_FILE, ASSET_SUBSET_MANIFEST_SCHEMA, ASSET_SUBSET_MANIFEST_VERSION,
        AssetBrowserEntry, AssetBrowserProvenance, AssetBrowserSnapshot, AssetCategory,
        AssetCategorySnapshot, AssetFileSnapshot, AssetFileTransformation, AssetId,
        AssetSourceProvenance, AssetSubsetSnapshot, CanonicalJsonError, ContentHash,
        GOVERNED_ASSET_SUBSET_PINS, GovernedAssetSubsetPin, MAX_ARCHIVE_BYTES,
        MAX_ASSET_FILE_BYTES, MAX_ASSET_FILE_TOTAL_BYTES, MAX_ASSET_FILES,
        MAX_ASSET_MANIFEST_BYTES, MAX_ASSET_MANIFEST_TOTAL_BYTES, MAX_ASSET_MANIFESTS,
        MAX_ASSET_MODELS, MAX_ASSET_PATH_BYTES, MAX_ASSET_SCAN_DEPTH, MAX_ASSET_SCAN_DIRECTORIES,
        MAX_ASSET_SCAN_ENTRIES, MAX_GLTF_JSON_BYTES, MAX_GLTF_JSON_TOTAL_BYTES,
        MAX_GLTF_URIS_PER_SCENE, MAX_GLTF_URIS_TOTAL, MAX_IDENTIFIER_BYTES, MAX_SHORT_TEXT_BYTES,
        MAX_URL_BYTES, VerifiedAssetLicense, hash_snapshot,
    };
    use serde::Deserialize;
    use sha2::{Digest, Sha256};
    use std::collections::{BTreeMap, BTreeSet, HashSet};
    use std::fs::{self, File, Metadata};
    use std::io::{self, Read};
    use std::path::{Component, Path, PathBuf};
    use thiserror::Error;

    /// Scans a trusted compiled root without following links or performing network access.
    ///
    /// The root must remain stable for the duration of the call. This v1 scanner is not a security
    /// boundary against concurrent filesystem mutation; packaged-runtime root binding is Gate 4.
    pub fn scan_asset_browser(
        root: impl AsRef<Path>,
    ) -> Result<AssetBrowserSnapshot, AssetBrowserError> {
        scan_asset_browser_with_pins(root.as_ref(), GOVERNED_ASSET_SUBSET_PINS)
    }

    fn scan_asset_browser_with_pins(
        root: &Path,
        pins: &[GovernedAssetSubsetPin],
    ) -> Result<AssetBrowserSnapshot, AssetBrowserError> {
        let root = validate_root(root)?;
        let tree = discover_tree(&root)?;
        if tree.manifests.is_empty() {
            return Err(AssetBrowserError::NoSubsetManifests);
        }
        let mut budget = AggregateBudget::default();
        let mut subset_ids = HashSet::with_capacity(tree.manifests.len());
        let mut asset_ids = HashSet::new();
        let mut manifests = Vec::with_capacity(tree.manifests.len());
        let mut assets = Vec::new();

        for manifest_path in &tree.manifests {
            let scanned = scan_manifest(
                &root,
                manifest_path,
                &tree.entries,
                &mut budget,
                &mut subset_ids,
                &mut asset_ids,
                pins,
            )?;
            manifests.push(scanned.subset);
            assets.extend(scanned.assets);
        }

        manifests.sort_unstable_by(|left, right| {
            left.subset_id
                .cmp(&right.subset_id)
                .then_with(|| left.manifest_path.cmp(&right.manifest_path))
        });
        assets.sort_unstable_by_key(|asset| asset.id);
        let categories = category_snapshots(&assets);
        let schema = ASSET_BROWSER_SNAPSHOT_SCHEMA.to_owned();
        let snapshot_hash = hash_snapshot(
            &schema,
            ASSET_BROWSER_SNAPSHOT_VERSION,
            &manifests,
            &categories,
            &assets,
        )?;
        Ok(AssetBrowserSnapshot {
            schema,
            schema_version: ASSET_BROWSER_SNAPSHOT_VERSION,
            snapshot_hash,
            manifests,
            categories,
            assets,
        })
    }

    #[derive(Debug, Error)]
    pub enum AssetBrowserError {
        #[error("asset browser root `{0}` is not a directory")]
        RootNotDirectory(PathBuf),
        #[error("filesystem {operation} failed for `{path}`: {source}")]
        Io {
            operation: &'static str,
            path: PathBuf,
            #[source]
            source: io::Error,
        },
        #[error("path `{0}` is a symbolic link or reparse point")]
        ReparsePoint(PathBuf),
        #[error("path `{0}` contains the forbidden Cadle name")]
        ForbiddenCadlePath(PathBuf),
        #[error("canonical path `{path}` escapes asset root `{root}`")]
        PathEscapesRoot { path: PathBuf, root: PathBuf },
        #[error("unsupported filesystem entry `{0}`")]
        UnsupportedEntry(PathBuf),
        #[error("asset scan exceeds {resource} limit {limit}")]
        LimitExceeded { resource: &'static str, limit: u64 },
        #[error("asset browser root contains no `{ASSET_SUBSET_MANIFEST_FILE}`")]
        NoSubsetManifests,
        #[error("asset subset `{subset_id}` is not present in the Forge governed pin ledger")]
        UnpinnedSubset { subset_id: String },
        #[error("asset subset `{subset_id}` differs from its Forge pin at `{field}`")]
        GovernedPinMismatch {
            subset_id: String,
            field: &'static str,
        },
        #[error("manifest `{path}` is invalid JSON: {source}")]
        ManifestJson {
            path: PathBuf,
            #[source]
            source: serde_json::Error,
        },
        #[error("manifest `{manifest}` is invalid: {reason}")]
        InvalidManifest { manifest: PathBuf, reason: String },
        #[error("manifest `{manifest}` has unsafe {field} path `{value}`: {reason}")]
        UnsafeManifestPath {
            manifest: PathBuf,
            field: &'static str,
            value: String,
            reason: &'static str,
        },
        #[error("duplicate subset id `{0}`")]
        DuplicateSubsetId(String),
        #[error("manifest `{manifest}` contains duplicate model id `{model_id}`")]
        DuplicateModelId { manifest: PathBuf, model_id: String },
        #[error("manifest `{manifest}` contains duplicate file path `{path}`")]
        DuplicateFilePath { manifest: PathBuf, path: String },
        #[error("manifest `{manifest}` contains duplicate source path `{path}`")]
        DuplicateSourcePath { manifest: PathBuf, path: String },
        #[error("deterministic asset id collision for `{stable_key}`")]
        DuplicateAssetId { stable_key: String },
        #[error("declared file `{path}` does not exist for manifest `{manifest}`")]
        MissingDeclaredFile { manifest: PathBuf, path: String },
        #[error("undeclared file `{path}` exists beside manifest `{manifest}`")]
        UnexpectedFile { manifest: PathBuf, path: String },
        #[error("file `{path}` has {actual} bytes; manifest declares {expected}")]
        FileSizeMismatch {
            path: String,
            expected: u64,
            actual: u64,
        },
        #[error("file `{path}` does not match its declared SHA-256")]
        FileHashMismatch { path: String },
        #[error("manifest `{0}` does not mark its license as verified")]
        LicenseNotVerified(PathBuf),
        #[error("license evidence `{path}` is not declared by manifest `{manifest}`")]
        LicenseEvidenceMissing { manifest: PathBuf, path: String },
        #[error("license evidence `{path}` does not match the declared evidence SHA-256")]
        LicenseEvidenceHashMismatch { path: String },
        #[error("model `{model_id}` scene `{scene_path}` is not an exact declared scene file")]
        ModelSceneNotDeclared {
            model_id: String,
            scene_path: String,
        },
        #[error("model `{model_id}` scene `{scene_path}` must use JSON `.gltf` in browser v1")]
        UnsupportedSceneFormat {
            model_id: String,
            scene_path: String,
        },
        #[error("scene `{scene_path}` is invalid glTF JSON: {source}")]
        GltfJson {
            scene_path: String,
            #[source]
            source: serde_json::Error,
        },
        #[error("scene `{scene_path}` buffer {buffer_index} has no external URI")]
        MissingGltfBufferUri {
            scene_path: String,
            buffer_index: usize,
        },
        #[error("scene `{scene_path}` has unsafe {kind} URI `{uri}`: {reason}")]
        UnsafeGltfUri {
            scene_path: String,
            kind: &'static str,
            uri: String,
            reason: &'static str,
        },
        #[error("scene `{scene_path}` references undeclared {kind} URI `{uri}`")]
        UndeclaredGltfUri {
            scene_path: String,
            kind: &'static str,
            uri: String,
        },
        #[error("binary glTF `{path}` is not inspected and is forbidden by browser v1")]
        UnsupportedGlb { path: String },
        #[error("cannot hash asset browser snapshot: {0}")]
        SnapshotHash(#[from] CanonicalJsonError),
    }

    #[derive(Deserialize)]
    #[serde(deny_unknown_fields)]
    struct RawSubsetManifest {
        schema: String,
        schema_version: u32,
        subset_id: String,
        source: RawSourceProvenance,
        license: RawLicense,
        models: Vec<RawModel>,
        files: Vec<RawFile>,
    }

    #[derive(Deserialize)]
    #[serde(deny_unknown_fields)]
    struct RawSourceProvenance {
        pack_id: String,
        name: String,
        version: String,
        author: String,
        source_page: String,
        distribution_page: String,
        archive_url: String,
        retrieved_at: String,
        archive_sha256: ContentHash,
        archive_bytes: u64,
        normalized_metadata_sha256: ContentHash,
    }

    #[derive(Deserialize)]
    #[serde(deny_unknown_fields)]
    struct RawLicense {
        spdx: String,
        url: String,
        verified: bool,
        attribution_required: bool,
        evidence_path: String,
        evidence_sha256: ContentHash,
    }

    #[derive(Deserialize)]
    #[serde(deny_unknown_fields)]
    struct RawModel {
        id: String,
        kind: AssetCategory,
        scene_path: String,
        source_path: String,
        base_scale: f32,
    }

    #[derive(Deserialize)]
    #[serde(deny_unknown_fields)]
    struct RawFile {
        path: String,
        source_path: String,
        bytes: u64,
        sha256: ContentHash,
        transformation: AssetFileTransformation,
    }

    #[derive(Deserialize)]
    struct RawGltfDocument {
        #[serde(default)]
        buffers: Vec<RawGltfBuffer>,
        #[serde(default)]
        images: Vec<RawGltfImage>,
    }

    #[derive(Deserialize)]
    struct RawGltfBuffer {
        uri: Option<String>,
    }

    #[derive(Deserialize)]
    struct RawGltfImage {
        uri: Option<String>,
    }

    struct DiscoveredTree {
        manifests: Vec<PathBuf>,
        entries: Vec<DiscoveredEntry>,
    }

    struct DiscoveredEntry {
        path: PathBuf,
    }

    #[derive(Clone, Copy)]
    struct DiscoveryLimits {
        depth: usize,
        directories: usize,
        manifests: usize,
        files: usize,
        entries: usize,
    }

    const DISCOVERY_LIMITS: DiscoveryLimits = DiscoveryLimits {
        depth: MAX_ASSET_SCAN_DEPTH,
        directories: MAX_ASSET_SCAN_DIRECTORIES,
        manifests: MAX_ASSET_MANIFESTS,
        files: MAX_ASSET_FILES,
        entries: MAX_ASSET_SCAN_ENTRIES,
    };

    #[derive(Default)]
    struct DiscoveryBudget {
        directories: usize,
        manifests: usize,
        files: usize,
        entries: usize,
    }

    #[derive(Clone, Copy)]
    enum DiscoveredKind {
        Directory,
        Manifest,
        File,
    }

    struct PendingEntry {
        path: PathBuf,
        kind: DiscoveredKind,
    }

    #[derive(Default)]
    struct AggregateBudget {
        manifest_bytes: u64,
        models: usize,
        files: usize,
        file_bytes: u64,
        gltf_bytes: u64,
        gltf_uris: usize,
    }

    struct ScannedManifest {
        subset: AssetSubsetSnapshot,
        assets: Vec<AssetBrowserEntry>,
    }

    fn validate_root(root: &Path) -> Result<PathBuf, AssetBrowserError> {
        let metadata = symlink_metadata(root, "read root metadata")?;
        reject_reparse(root, &metadata)?;
        if !metadata.is_dir() {
            return Err(AssetBrowserError::RootNotDirectory(root.to_path_buf()));
        }
        reject_cadle_components(root)?;
        let canonical = canonicalize(root, "canonicalize root")?;
        reject_cadle_components(&canonical)?;
        Ok(canonical)
    }

    fn discover_tree(root: &Path) -> Result<DiscoveredTree, AssetBrowserError> {
        discover_tree_with_limits(root, DISCOVERY_LIMITS)
    }

    fn discover_tree_with_limits(
        root: &Path,
        limits: DiscoveryLimits,
    ) -> Result<DiscoveredTree, AssetBrowserError> {
        let mut stack = vec![(root.to_path_buf(), 0_usize)];
        let mut budget = DiscoveryBudget {
            directories: checked_count(0, 1, limits.directories, "directories")?,
            ..DiscoveryBudget::default()
        };
        let mut manifests = Vec::new();
        let mut entries = Vec::new();

        while let Some((directory, depth)) = stack.pop() {
            let children = read_directory_bounded(&directory, root, depth, &mut budget, limits)?;
            for child in children {
                match child.kind {
                    DiscoveredKind::Directory => {
                        stack.push((child.path, depth.saturating_add(1)));
                    }
                    DiscoveredKind::Manifest => {
                        manifests.push(child.path.clone());
                        entries.push(DiscoveredEntry { path: child.path });
                    }
                    DiscoveredKind::File => {
                        entries.push(DiscoveredEntry { path: child.path });
                    }
                }
            }
        }
        manifests.sort_unstable();
        Ok(DiscoveredTree { manifests, entries })
    }

    fn read_directory_bounded(
        directory: &Path,
        root: &Path,
        depth: usize,
        budget: &mut DiscoveryBudget,
        limits: DiscoveryLimits,
    ) -> Result<Vec<PendingEntry>, AssetBrowserError> {
        let iterator = fs::read_dir(directory)
            .map_err(|source| io_error("read directory", directory, source))?;
        let mut children = Vec::new();
        for child in iterator {
            budget.entries = checked_count(budget.entries, 1, limits.entries, "scan entries")?;
            let child =
                child.map_err(|source| io_error("read directory entry", directory, source))?;
            let path = child.path();
            reject_cadle_components(&path)?;
            let metadata = symlink_metadata(&path, "read entry metadata")?;
            reject_reparse(&path, &metadata)?;
            let canonical = canonicalize(&path, "canonicalize entry")?;
            ensure_under_root(&canonical, root)?;
            let kind = classify_discovered_entry(&child, &metadata, depth, budget, limits)?;
            children.push(PendingEntry {
                path: canonical,
                kind,
            });
        }
        children.sort_unstable_by(|left, right| left.path.cmp(&right.path));
        Ok(children)
    }

    fn classify_discovered_entry(
        entry: &fs::DirEntry,
        metadata: &Metadata,
        depth: usize,
        budget: &mut DiscoveryBudget,
        limits: DiscoveryLimits,
    ) -> Result<DiscoveredKind, AssetBrowserError> {
        if metadata.is_dir() {
            if depth.saturating_add(1) > limits.depth {
                return Err(limit_error("directory depth", limits.depth));
            }
            budget.directories =
                checked_count(budget.directories, 1, limits.directories, "directories")?;
            Ok(DiscoveredKind::Directory)
        } else if metadata.is_file() && entry.file_name() == ASSET_SUBSET_MANIFEST_FILE {
            budget.manifests = checked_count(budget.manifests, 1, limits.manifests, "manifests")?;
            Ok(DiscoveredKind::Manifest)
        } else if metadata.is_file() {
            budget.files = checked_count(budget.files, 1, limits.files, "regular files")?;
            Ok(DiscoveredKind::File)
        } else {
            Err(AssetBrowserError::UnsupportedEntry(entry.path()))
        }
    }

    fn scan_manifest(
        root: &Path,
        manifest_path: &Path,
        entries: &[DiscoveredEntry],
        budget: &mut AggregateBudget,
        subset_ids: &mut HashSet<String>,
        asset_ids: &mut HashSet<AssetId>,
        pins: &[GovernedAssetSubsetPin],
    ) -> Result<ScannedManifest, AssetBrowserError> {
        let bytes = read_manifest(manifest_path, budget)?;
        let manifest_hash = ContentHash::from_bytes(Sha256::digest(&bytes).into());
        let raw: RawSubsetManifest =
            serde_json::from_slice(&bytes).map_err(|source| AssetBrowserError::ManifestJson {
                path: manifest_path.to_path_buf(),
                source,
            })?;
        validate_manifest_header(&raw, manifest_path, subset_ids)?;
        validate_governed_pin(&raw, manifest_hash, pins)?;
        budget.models = checked_count(budget.models, raw.models.len(), MAX_ASSET_MODELS, "models")?;
        let source = validate_source(&raw.source, manifest_path)?;
        let manifest_directory = manifest_path.parent().ok_or_else(|| {
            invalid_manifest(manifest_path, "manifest has no containing directory")
        })?;
        let manifest_directory_relative = portable_relative(root, manifest_directory)?;
        let files = scan_declared_files(
            root,
            manifest_path,
            manifest_directory,
            &manifest_directory_relative,
            entries,
            &raw.files,
            budget,
        )?;
        let license = validate_license(
            &raw.license,
            manifest_path,
            &manifest_directory_relative,
            &files,
        )?;
        validate_gltf_files(root, &files, budget)?;
        let assets = scan_models(
            &raw,
            &ModelScanContext {
                manifest_path,
                manifest_hash,
                source: &source,
                license: &license,
                files: &files,
            },
            asset_ids,
        )?;
        let manifest_path_relative = portable_relative(root, manifest_path)?;
        Ok(ScannedManifest {
            subset: AssetSubsetSnapshot {
                subset_id: raw.subset_id,
                manifest_path: manifest_path_relative,
                manifest_hash,
                source,
                license,
                files,
            },
            assets,
        })
    }

    fn validate_manifest_header(
        raw: &RawSubsetManifest,
        manifest_path: &Path,
        subset_ids: &mut HashSet<String>,
    ) -> Result<(), AssetBrowserError> {
        if raw.schema != ASSET_SUBSET_MANIFEST_SCHEMA {
            return Err(invalid_manifest(
                manifest_path,
                format!("unsupported schema `{}`", raw.schema),
            ));
        }
        if raw.schema_version != ASSET_SUBSET_MANIFEST_VERSION {
            return Err(invalid_manifest(
                manifest_path,
                format!("unsupported schema version {}", raw.schema_version),
            ));
        }
        validate_identifier(&raw.subset_id, "subset_id", manifest_path)?;
        let subset_key = raw.subset_id.to_ascii_lowercase();
        if !subset_ids.insert(subset_key) {
            return Err(AssetBrowserError::DuplicateSubsetId(raw.subset_id.clone()));
        }
        if raw.models.is_empty() {
            return Err(invalid_manifest(manifest_path, "models cannot be empty"));
        }
        if raw.files.is_empty() {
            return Err(invalid_manifest(manifest_path, "files cannot be empty"));
        }
        Ok(())
    }

    fn validate_governed_pin(
        raw: &RawSubsetManifest,
        manifest_hash: ContentHash,
        pins: &[GovernedAssetSubsetPin],
    ) -> Result<(), AssetBrowserError> {
        let pin = pins
            .iter()
            .find(|pin| pin.subset_id == raw.subset_id)
            .ok_or_else(|| AssetBrowserError::UnpinnedSubset {
                subset_id: raw.subset_id.clone(),
            })?;
        let mismatched_field = if raw.source.pack_id != pin.pack_id {
            Some("source.pack_id")
        } else if manifest_hash != pin.manifest_hash {
            Some("manifest_sha256")
        } else if raw.source.archive_sha256 != pin.archive_hash {
            Some("source.archive_sha256")
        } else if raw.source.normalized_metadata_sha256 != pin.normalized_metadata_hash {
            Some("source.normalized_metadata_sha256")
        } else if raw.license.spdx != pin.license_spdx {
            Some("license.spdx")
        } else if raw.license.evidence_path != pin.license_evidence_path {
            Some("license.evidence_path")
        } else if raw.license.evidence_sha256 != pin.license_evidence_hash {
            Some("license.evidence_sha256")
        } else {
            None
        };
        if let Some(field) = mismatched_field {
            return Err(AssetBrowserError::GovernedPinMismatch {
                subset_id: raw.subset_id.clone(),
                field,
            });
        }
        Ok(())
    }

    fn validate_source(
        raw: &RawSourceProvenance,
        manifest_path: &Path,
    ) -> Result<AssetSourceProvenance, AssetBrowserError> {
        validate_identifier(&raw.pack_id, "source.pack_id", manifest_path)?;
        validate_text(
            &raw.name,
            "source.name",
            MAX_SHORT_TEXT_BYTES,
            manifest_path,
        )?;
        validate_text(
            &raw.version,
            "source.version",
            MAX_SHORT_TEXT_BYTES,
            manifest_path,
        )?;
        validate_text(
            &raw.author,
            "source.author",
            MAX_SHORT_TEXT_BYTES,
            manifest_path,
        )?;
        validate_https_url(&raw.source_page, "source.source_page", manifest_path)?;
        validate_https_url(
            &raw.distribution_page,
            "source.distribution_page",
            manifest_path,
        )?;
        validate_https_url(&raw.archive_url, "source.archive_url", manifest_path)?;
        validate_text(
            &raw.retrieved_at,
            "source.retrieved_at",
            MAX_SHORT_TEXT_BYTES,
            manifest_path,
        )?;
        if raw.archive_bytes == 0 || raw.archive_bytes > MAX_ARCHIVE_BYTES {
            return Err(invalid_manifest(
                manifest_path,
                format!("source.archive_bytes must be in 1..={MAX_ARCHIVE_BYTES}"),
            ));
        }
        Ok(AssetSourceProvenance {
            pack_id: raw.pack_id.clone(),
            name: raw.name.clone(),
            version: raw.version.clone(),
            author: raw.author.clone(),
            source_page: raw.source_page.clone(),
            distribution_page: raw.distribution_page.clone(),
            archive_url: raw.archive_url.clone(),
            retrieved_at: raw.retrieved_at.clone(),
            archive_hash: raw.archive_sha256,
            archive_bytes: raw.archive_bytes,
            normalized_metadata_hash: raw.normalized_metadata_sha256,
        })
    }

    fn scan_declared_files(
        root: &Path,
        manifest_path: &Path,
        manifest_directory: &Path,
        manifest_directory_relative: &str,
        entries: &[DiscoveredEntry],
        raw_files: &[RawFile],
        budget: &mut AggregateBudget,
    ) -> Result<Vec<AssetFileSnapshot>, AssetBrowserError> {
        budget.files = checked_count(
            budget.files,
            raw_files.len(),
            MAX_ASSET_FILES,
            "declared files",
        )?;
        let mut paths = HashSet::with_capacity(raw_files.len());
        let mut source_paths = HashSet::with_capacity(raw_files.len());
        let mut declared = BTreeSet::new();
        let mut files = Vec::with_capacity(raw_files.len());
        for raw in raw_files {
            let local_path = validate_manifest_path(&raw.path, "files.path", manifest_path)?;
            let source_path =
                validate_manifest_path(&raw.source_path, "files.source_path", manifest_path)?;
            insert_file_keys(
                manifest_path,
                &local_path,
                &source_path,
                &mut paths,
                &mut source_paths,
            )?;
            validate_declared_size(raw.bytes, budget)?;
            let full_path = manifest_directory.join(path_from_portable(&local_path));
            let relative_path = join_portable(manifest_directory_relative, &local_path);
            let (actual_bytes, content_hash) =
                hash_declared_file(root, manifest_path, &full_path, &relative_path)?;
            if actual_bytes != raw.bytes {
                return Err(AssetBrowserError::FileSizeMismatch {
                    path: relative_path,
                    expected: raw.bytes,
                    actual: actual_bytes,
                });
            }
            if content_hash != raw.sha256 {
                return Err(AssetBrowserError::FileHashMismatch {
                    path: relative_path,
                });
            }
            declared.insert(local_path);
            files.push(AssetFileSnapshot {
                relative_path,
                source_path,
                bytes: actual_bytes,
                content_hash,
                transformation: raw.transformation,
            });
        }
        validate_exact_inventory(manifest_path, manifest_directory, entries, &declared)?;
        files.sort_unstable_by(|left, right| left.relative_path.cmp(&right.relative_path));
        Ok(files)
    }

    fn insert_file_keys(
        manifest_path: &Path,
        local_path: &str,
        source_path: &str,
        paths: &mut HashSet<String>,
        source_paths: &mut HashSet<String>,
    ) -> Result<(), AssetBrowserError> {
        if !paths.insert(local_path.to_ascii_lowercase()) {
            return Err(AssetBrowserError::DuplicateFilePath {
                manifest: manifest_path.to_path_buf(),
                path: local_path.to_owned(),
            });
        }
        if !source_paths.insert(source_path.to_ascii_lowercase()) {
            return Err(AssetBrowserError::DuplicateSourcePath {
                manifest: manifest_path.to_path_buf(),
                path: source_path.to_owned(),
            });
        }
        Ok(())
    }

    fn validate_declared_size(
        declared_bytes: u64,
        budget: &mut AggregateBudget,
    ) -> Result<(), AssetBrowserError> {
        if declared_bytes > MAX_ASSET_FILE_BYTES {
            return Err(limit_error("bytes per file", MAX_ASSET_FILE_BYTES));
        }
        budget.file_bytes = budget
            .file_bytes
            .checked_add(declared_bytes)
            .ok_or_else(|| limit_error("aggregate file bytes", MAX_ASSET_FILE_TOTAL_BYTES))?;
        if budget.file_bytes > MAX_ASSET_FILE_TOTAL_BYTES {
            return Err(limit_error(
                "aggregate file bytes",
                MAX_ASSET_FILE_TOTAL_BYTES,
            ));
        }
        Ok(())
    }

    fn validate_exact_inventory(
        manifest_path: &Path,
        manifest_directory: &Path,
        entries: &[DiscoveredEntry],
        declared: &BTreeSet<String>,
    ) -> Result<(), AssetBrowserError> {
        let mut actual = BTreeSet::new();
        for entry in entries {
            if entry.path == manifest_path {
                continue;
            }
            if let Ok(relative) = entry.path.strip_prefix(manifest_directory) {
                let relative = portable_path(relative)?;
                actual.insert(relative);
            }
        }
        if let Some(missing) = declared.difference(&actual).next() {
            return Err(AssetBrowserError::MissingDeclaredFile {
                manifest: manifest_path.to_path_buf(),
                path: (*missing).clone(),
            });
        }
        if let Some(unexpected) = actual.difference(declared).next() {
            return Err(AssetBrowserError::UnexpectedFile {
                manifest: manifest_path.to_path_buf(),
                path: (*unexpected).clone(),
            });
        }
        Ok(())
    }

    fn validate_license(
        raw: &RawLicense,
        manifest_path: &Path,
        manifest_directory_relative: &str,
        files: &[AssetFileSnapshot],
    ) -> Result<VerifiedAssetLicense, AssetBrowserError> {
        if !raw.verified {
            return Err(AssetBrowserError::LicenseNotVerified(
                manifest_path.to_path_buf(),
            ));
        }
        validate_text(&raw.spdx, "license.spdx", 64, manifest_path)?;
        if !raw
            .spdx
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'.' | b'-' | b'+'))
        {
            return Err(invalid_manifest(
                manifest_path,
                "license.spdx is not a portable SPDX identifier",
            ));
        }
        validate_https_url(&raw.url, "license.url", manifest_path)?;
        let evidence_local =
            validate_manifest_path(&raw.evidence_path, "license.evidence_path", manifest_path)?;
        let evidence_path = join_portable(manifest_directory_relative, &evidence_local);
        let evidence = files
            .iter()
            .find(|file| file.relative_path == evidence_path)
            .ok_or_else(|| AssetBrowserError::LicenseEvidenceMissing {
                manifest: manifest_path.to_path_buf(),
                path: evidence_path.clone(),
            })?;
        if evidence.bytes == 0 {
            return Err(invalid_manifest(
                manifest_path,
                "license evidence file cannot be empty",
            ));
        }
        if evidence.content_hash != raw.evidence_sha256 {
            return Err(AssetBrowserError::LicenseEvidenceHashMismatch {
                path: evidence_path,
            });
        }
        Ok(VerifiedAssetLicense {
            spdx: raw.spdx.clone(),
            url: raw.url.clone(),
            attribution_required: raw.attribution_required,
            evidence_path,
            evidence_hash: raw.evidence_sha256,
        })
    }

    fn validate_gltf_files(
        root: &Path,
        files: &[AssetFileSnapshot],
        budget: &mut AggregateBudget,
    ) -> Result<(), AssetBrowserError> {
        let declared: BTreeMap<_, _> = files
            .iter()
            .map(|file| (file.relative_path.as_str(), file))
            .collect();
        for file in files {
            let extension = Path::new(&file.relative_path)
                .extension()
                .and_then(|value| value.to_str())
                .map(str::to_ascii_lowercase);
            match extension.as_deref() {
                Some("gltf") => validate_gltf_file(root, file, &declared, budget)?,
                Some("glb") => {
                    return Err(AssetBrowserError::UnsupportedGlb {
                        path: file.relative_path.clone(),
                    });
                }
                _ => {}
            }
        }
        Ok(())
    }

    fn validate_gltf_file(
        root: &Path,
        scene_file: &AssetFileSnapshot,
        declared: &BTreeMap<&str, &AssetFileSnapshot>,
        budget: &mut AggregateBudget,
    ) -> Result<(), AssetBrowserError> {
        if scene_file.bytes > MAX_GLTF_JSON_BYTES {
            return Err(limit_error("bytes per glTF JSON", MAX_GLTF_JSON_BYTES));
        }
        budget.gltf_bytes = budget
            .gltf_bytes
            .checked_add(scene_file.bytes)
            .ok_or_else(|| limit_error("aggregate glTF JSON bytes", MAX_GLTF_JSON_TOTAL_BYTES))?;
        if budget.gltf_bytes > MAX_GLTF_JSON_TOTAL_BYTES {
            return Err(limit_error(
                "aggregate glTF JSON bytes",
                MAX_GLTF_JSON_TOTAL_BYTES,
            ));
        }
        let path = root.join(path_from_portable(&scene_file.relative_path));
        let bytes = read_file_bounded(&path, MAX_GLTF_JSON_BYTES, "read glTF JSON")?;
        let document: RawGltfDocument =
            serde_json::from_slice(&bytes).map_err(|source| AssetBrowserError::GltfJson {
                scene_path: scene_file.relative_path.clone(),
                source,
            })?;
        let uri_count = document
            .buffers
            .len()
            .checked_add(document.images.len())
            .ok_or_else(|| limit_error("URIs per glTF scene", MAX_GLTF_URIS_PER_SCENE))?;
        if uri_count > MAX_GLTF_URIS_PER_SCENE {
            return Err(limit_error("URIs per glTF scene", MAX_GLTF_URIS_PER_SCENE));
        }
        budget.gltf_uris = checked_count(
            budget.gltf_uris,
            uri_count,
            MAX_GLTF_URIS_TOTAL,
            "aggregate glTF URIs",
        )?;
        for (index, buffer) in document.buffers.iter().enumerate() {
            let uri =
                buffer
                    .uri
                    .as_deref()
                    .ok_or_else(|| AssetBrowserError::MissingGltfBufferUri {
                        scene_path: scene_file.relative_path.clone(),
                        buffer_index: index,
                    })?;
            validate_gltf_uri(&scene_file.relative_path, "buffer", uri, declared)?;
        }
        for image in &document.images {
            if let Some(uri) = &image.uri {
                validate_gltf_uri(&scene_file.relative_path, "image", uri, declared)?;
            }
        }
        Ok(())
    }

    fn validate_gltf_uri(
        scene_path: &str,
        kind: &'static str,
        uri: &str,
        declared: &BTreeMap<&str, &AssetFileSnapshot>,
    ) -> Result<(), AssetBrowserError> {
        let uri = normalize_gltf_uri(scene_path, kind, uri)?;
        let directory = scene_path
            .rsplit_once('/')
            .map_or("", |(directory, _)| directory);
        let referenced_path = join_portable(directory, &uri);
        if !declared.contains_key(referenced_path.as_str()) {
            return Err(AssetBrowserError::UndeclaredGltfUri {
                scene_path: scene_path.to_owned(),
                kind,
                uri,
            });
        }
        Ok(())
    }

    fn normalize_gltf_uri(
        scene_path: &str,
        kind: &'static str,
        uri: &str,
    ) -> Result<String, AssetBrowserError> {
        let unsafe_uri = |reason| AssetBrowserError::UnsafeGltfUri {
            scene_path: scene_path.to_owned(),
            kind,
            uri: uri.to_owned(),
            reason,
        };
        if uri.is_empty() || uri.len() > MAX_ASSET_PATH_BYTES {
            return Err(unsafe_uri("URI length is outside 1..=256 bytes"));
        }
        if uri.contains('\\') {
            return Err(unsafe_uri("backslashes are forbidden"));
        }
        if Path::new(uri).is_absolute()
            || uri.starts_with('/')
            || uri.as_bytes().get(1) == Some(&b':')
        {
            return Err(unsafe_uri("absolute URIs are forbidden"));
        }
        if uri.contains(['%', '?', '#']) {
            return Err(unsafe_uri("encoded, query and fragment URIs are forbidden"));
        }
        let mut components = Vec::new();
        for component in uri.split('/') {
            if component.is_empty() || matches!(component, "." | "..") {
                return Err(unsafe_uri("empty, dot and parent components are forbidden"));
            }
            if component.contains(':') || component.chars().any(char::is_control) {
                return Err(unsafe_uri(
                    "schemes, colons and control characters are forbidden",
                ));
            }
            if contains_cadle(component) {
                return Err(unsafe_uri("Cadle components are forbidden"));
            }
            components.push(component);
        }
        Ok(components.join("/"))
    }

    struct ModelScanContext<'a> {
        manifest_path: &'a Path,
        manifest_hash: ContentHash,
        source: &'a AssetSourceProvenance,
        license: &'a VerifiedAssetLicense,
        files: &'a [AssetFileSnapshot],
    }

    fn scan_models(
        raw: &RawSubsetManifest,
        context: &ModelScanContext<'_>,
        asset_ids: &mut HashSet<AssetId>,
    ) -> Result<Vec<AssetBrowserEntry>, AssetBrowserError> {
        let file_by_path: BTreeMap<_, _> = context
            .files
            .iter()
            .map(|file| (file.relative_path.as_str(), file))
            .collect();
        let mut model_ids = HashSet::with_capacity(raw.models.len());
        let mut scene_paths = HashSet::with_capacity(raw.models.len());
        let mut assets = Vec::with_capacity(raw.models.len());
        for model in &raw.models {
            validate_identifier(&model.id, "models.id", context.manifest_path)?;
            if !model_ids.insert(model.id.to_ascii_lowercase()) {
                return Err(AssetBrowserError::DuplicateModelId {
                    manifest: context.manifest_path.to_path_buf(),
                    model_id: model.id.clone(),
                });
            }
            let scene_path = validate_manifest_path(
                &model.scene_path,
                "models.scene_path",
                context.manifest_path,
            )?;
            if !scene_paths.insert(scene_path.to_ascii_lowercase()) {
                return Err(invalid_manifest(
                    context.manifest_path,
                    format!("duplicate model scene path `{scene_path}`"),
                ));
            }
            let source_path = validate_manifest_path(
                &model.source_path,
                "models.source_path",
                context.manifest_path,
            )?;
            let scene_file = file_by_path.get(scene_path.as_str()).ok_or_else(|| {
                AssetBrowserError::ModelSceneNotDeclared {
                    model_id: model.id.clone(),
                    scene_path: scene_path.clone(),
                }
            })?;
            validate_scene_file(model, scene_file, &source_path)?;
            if !model.base_scale.is_finite() || !(0.01..=100.0).contains(&model.base_scale) {
                return Err(invalid_manifest(
                    context.manifest_path,
                    format!(
                        "model `{}` base_scale must be finite and in 0.01..=100",
                        model.id
                    ),
                ));
            }
            let stable_key = stable_asset_key(&context.source.pack_id, &model.id);
            let id =
                AssetId::deterministic(format!("{ASSET_ID_KEY_PREFIX}:{stable_key}").as_bytes());
            if !asset_ids.insert(id) {
                return Err(AssetBrowserError::DuplicateAssetId { stable_key });
            }
            assets.push(AssetBrowserEntry {
                id,
                stable_key,
                model_id: model.id.clone(),
                category: model.kind,
                scene_path,
                base_scale: model.base_scale,
                provenance: AssetBrowserProvenance {
                    subset_id: raw.subset_id.clone(),
                    pack_id: context.source.pack_id.clone(),
                    author: context.source.author.clone(),
                    source_version: context.source.version.clone(),
                    retrieved_at: context.source.retrieved_at.clone(),
                    source_path,
                    license_spdx: context.license.spdx.clone(),
                    license_verified: true,
                    attribution_required: context.license.attribution_required,
                    manifest_hash: context.manifest_hash,
                    scene_content_hash: scene_file.content_hash,
                },
            });
        }
        Ok(assets)
    }

    fn stable_asset_key(pack_id: &str, model_id: &str) -> String {
        format!(
            "{}/{}",
            pack_id.to_ascii_lowercase(),
            model_id.to_ascii_lowercase()
        )
    }

    fn validate_scene_file(
        model: &RawModel,
        scene_file: &AssetFileSnapshot,
        source_path: &str,
    ) -> Result<(), AssetBrowserError> {
        let extension = Path::new(&scene_file.relative_path)
            .extension()
            .and_then(|value| value.to_str());
        if extension != Some("gltf") {
            return Err(AssetBrowserError::UnsupportedSceneFormat {
                model_id: model.id.clone(),
                scene_path: scene_file.relative_path.clone(),
            });
        }
        if scene_file.source_path != source_path {
            return Err(AssetBrowserError::ModelSceneNotDeclared {
                model_id: model.id.clone(),
                scene_path: scene_file.relative_path.clone(),
            });
        }
        Ok(())
    }

    fn category_snapshots(assets: &[AssetBrowserEntry]) -> Vec<AssetCategorySnapshot> {
        let mut grouped = BTreeMap::<AssetCategory, Vec<AssetId>>::new();
        for asset in assets {
            grouped.entry(asset.category).or_default().push(asset.id);
        }
        grouped
            .into_iter()
            .map(|(category, mut asset_ids)| {
                asset_ids.sort_unstable();
                AssetCategorySnapshot {
                    category,
                    asset_ids,
                }
            })
            .collect()
    }

    fn read_manifest(
        path: &Path,
        budget: &mut AggregateBudget,
    ) -> Result<Vec<u8>, AssetBrowserError> {
        let metadata = symlink_metadata(path, "read manifest metadata")?;
        reject_reparse(path, &metadata)?;
        if metadata.len() > MAX_ASSET_MANIFEST_BYTES {
            return Err(limit_error("bytes per manifest", MAX_ASSET_MANIFEST_BYTES));
        }
        budget.manifest_bytes = budget
            .manifest_bytes
            .checked_add(metadata.len())
            .ok_or_else(|| {
                limit_error("aggregate manifest bytes", MAX_ASSET_MANIFEST_TOTAL_BYTES)
            })?;
        if budget.manifest_bytes > MAX_ASSET_MANIFEST_TOTAL_BYTES {
            return Err(limit_error(
                "aggregate manifest bytes",
                MAX_ASSET_MANIFEST_TOTAL_BYTES,
            ));
        }
        read_file_bounded(path, MAX_ASSET_MANIFEST_BYTES, "read manifest")
    }

    fn read_file_bounded(
        path: &Path,
        maximum: u64,
        operation: &'static str,
    ) -> Result<Vec<u8>, AssetBrowserError> {
        let file = File::open(path).map_err(|source| io_error(operation, path, source))?;
        let mut bytes = Vec::new();
        file.take(maximum.saturating_add(1))
            .read_to_end(&mut bytes)
            .map_err(|source| io_error(operation, path, source))?;
        let length = u64::try_from(bytes.len()).unwrap_or(u64::MAX);
        if length > maximum {
            return Err(limit_error(operation, maximum));
        }
        Ok(bytes)
    }

    fn hash_declared_file(
        root: &Path,
        manifest_path: &Path,
        path: &Path,
        relative_path: &str,
    ) -> Result<(u64, ContentHash), AssetBrowserError> {
        let metadata = match fs::symlink_metadata(path) {
            Ok(metadata) => metadata,
            Err(source) if source.kind() == io::ErrorKind::NotFound => {
                return Err(AssetBrowserError::MissingDeclaredFile {
                    manifest: manifest_path.to_path_buf(),
                    path: relative_path.to_owned(),
                });
            }
            Err(source) => return Err(io_error("read declared file metadata", path, source)),
        };
        reject_reparse(path, &metadata)?;
        if !metadata.is_file() {
            return Err(AssetBrowserError::UnsupportedEntry(path.to_path_buf()));
        }
        if metadata.len() > MAX_ASSET_FILE_BYTES {
            return Err(limit_error("bytes per file", MAX_ASSET_FILE_BYTES));
        }
        let canonical = canonicalize(path, "canonicalize declared file")?;
        ensure_under_root(&canonical, root)?;
        let mut file = File::open(&canonical)
            .map_err(|source| io_error("open declared file", &canonical, source))?;
        let mut hash = Sha256::new();
        let mut total = 0_u64;
        let mut buffer = [0_u8; 16 * 1024];
        loop {
            let read = file
                .read(&mut buffer)
                .map_err(|source| io_error("hash declared file", &canonical, source))?;
            if read == 0 {
                break;
            }
            total = total
                .checked_add(u64::try_from(read).unwrap_or(u64::MAX))
                .ok_or_else(|| limit_error("bytes per file", MAX_ASSET_FILE_BYTES))?;
            if total > MAX_ASSET_FILE_BYTES {
                return Err(limit_error("bytes per file", MAX_ASSET_FILE_BYTES));
            }
            hash.update(&buffer[..read]);
        }
        Ok((total, ContentHash::from_bytes(hash.finalize().into())))
    }

    fn validate_manifest_path(
        value: &str,
        field: &'static str,
        manifest: &Path,
    ) -> Result<String, AssetBrowserError> {
        let unsafe_path = |reason| AssetBrowserError::UnsafeManifestPath {
            manifest: manifest.to_path_buf(),
            field,
            value: value.to_owned(),
            reason,
        };
        if value.is_empty() || value.len() > MAX_ASSET_PATH_BYTES {
            return Err(unsafe_path("path length is outside 1..=256 bytes"));
        }
        if value.contains('\\') {
            return Err(unsafe_path("backslashes are forbidden"));
        }
        if value.contains(['%', '?', '#']) {
            return Err(unsafe_path(
                "encoded, query and fragment path characters are forbidden",
            ));
        }
        if Path::new(value).is_absolute()
            || value.starts_with('/')
            || value.as_bytes().get(1) == Some(&b':')
        {
            return Err(unsafe_path("absolute paths are forbidden"));
        }
        let mut normalized = Vec::new();
        for component in value.split('/') {
            if component.is_empty() || matches!(component, "." | "..") {
                return Err(unsafe_path(
                    "empty, dot and parent components are forbidden",
                ));
            }
            if component.contains(':') || component.chars().any(char::is_control) {
                return Err(unsafe_path("colon and control characters are forbidden"));
            }
            if contains_cadle(component) {
                return Err(unsafe_path("Cadle components are forbidden"));
            }
            normalized.push(component);
        }
        Ok(normalized.join("/"))
    }

    fn validate_identifier(
        value: &str,
        field: &'static str,
        manifest: &Path,
    ) -> Result<(), AssetBrowserError> {
        if value.is_empty()
            || value.len() > MAX_IDENTIFIER_BYTES
            || !value.bytes().all(|byte| {
                byte.is_ascii_alphanumeric() || matches!(byte, b'.' | b'-' | b'_' | b':')
            })
            || contains_cadle(value)
        {
            return Err(invalid_manifest(
                manifest,
                format!("{field} is not a valid portable identifier"),
            ));
        }
        Ok(())
    }

    fn validate_text(
        value: &str,
        field: &'static str,
        maximum: usize,
        manifest: &Path,
    ) -> Result<(), AssetBrowserError> {
        if value.is_empty()
            || value.len() > maximum
            || value.trim() != value
            || value.chars().any(char::is_control)
            || contains_cadle(value)
        {
            return Err(invalid_manifest(
                manifest,
                format!("{field} is empty, malformed or exceeds {maximum} bytes"),
            ));
        }
        Ok(())
    }

    fn validate_https_url(
        value: &str,
        field: &'static str,
        manifest: &Path,
    ) -> Result<(), AssetBrowserError> {
        validate_text(value, field, MAX_URL_BYTES, manifest)?;
        if !value.starts_with("https://") || value.chars().any(char::is_whitespace) {
            return Err(invalid_manifest(
                manifest,
                format!("{field} must be an HTTPS URL"),
            ));
        }
        Ok(())
    }

    fn path_from_portable(value: &str) -> PathBuf {
        value.split('/').collect()
    }

    fn join_portable(parent: &str, child: &str) -> String {
        if parent.is_empty() {
            child.to_owned()
        } else {
            format!("{parent}/{child}")
        }
    }

    fn portable_relative(root: &Path, path: &Path) -> Result<String, AssetBrowserError> {
        let relative = path
            .strip_prefix(root)
            .map_err(|_| AssetBrowserError::PathEscapesRoot {
                path: path.to_path_buf(),
                root: root.to_path_buf(),
            })?;
        portable_path(relative)
    }

    fn portable_path(path: &Path) -> Result<String, AssetBrowserError> {
        let mut components = Vec::new();
        for component in path.components() {
            match component {
                Component::Normal(value) => {
                    let value = value
                        .to_str()
                        .ok_or_else(|| AssetBrowserError::UnsupportedEntry(path.to_path_buf()))?;
                    components.push(value);
                }
                _ => return Err(AssetBrowserError::UnsupportedEntry(path.to_path_buf())),
            }
        }
        Ok(components.join("/"))
    }

    fn symlink_metadata(
        path: &Path,
        operation: &'static str,
    ) -> Result<Metadata, AssetBrowserError> {
        fs::symlink_metadata(path).map_err(|source| io_error(operation, path, source))
    }

    fn canonicalize(path: &Path, operation: &'static str) -> Result<PathBuf, AssetBrowserError> {
        fs::canonicalize(path).map_err(|source| io_error(operation, path, source))
    }

    fn ensure_under_root(path: &Path, root: &Path) -> Result<(), AssetBrowserError> {
        if !path.starts_with(root) {
            return Err(AssetBrowserError::PathEscapesRoot {
                path: path.to_path_buf(),
                root: root.to_path_buf(),
            });
        }
        Ok(())
    }

    fn reject_reparse(path: &Path, metadata: &Metadata) -> Result<(), AssetBrowserError> {
        if metadata.file_type().is_symlink() || is_reparse_point(metadata) {
            return Err(AssetBrowserError::ReparsePoint(path.to_path_buf()));
        }
        Ok(())
    }

    #[cfg(windows)]
    fn is_reparse_point(metadata: &Metadata) -> bool {
        use std::os::windows::fs::MetadataExt;

        const FILE_ATTRIBUTE_REPARSE_POINT: u32 = 0x0400;
        metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0
    }

    #[cfg(not(windows))]
    const fn is_reparse_point(_metadata: &Metadata) -> bool {
        false
    }

    fn reject_cadle_components(path: &Path) -> Result<(), AssetBrowserError> {
        if path.components().any(|component| match component {
            Component::Normal(value) => value.to_str().is_some_and(contains_cadle),
            _ => false,
        }) {
            return Err(AssetBrowserError::ForbiddenCadlePath(path.to_path_buf()));
        }
        Ok(())
    }

    fn contains_cadle(value: &str) -> bool {
        value.to_ascii_lowercase().contains("cadle")
    }

    fn checked_count(
        current: usize,
        increment: usize,
        maximum: usize,
        resource: &'static str,
    ) -> Result<usize, AssetBrowserError> {
        let next = current
            .checked_add(increment)
            .ok_or_else(|| limit_error(resource, maximum))?;
        if next > maximum {
            return Err(limit_error(resource, maximum));
        }
        Ok(next)
    }

    fn limit_error(resource: &'static str, maximum: impl TryInto<u64>) -> AssetBrowserError {
        AssetBrowserError::LimitExceeded {
            resource,
            limit: maximum.try_into().unwrap_or(u64::MAX),
        }
    }

    fn invalid_manifest(manifest: &Path, reason: impl Into<String>) -> AssetBrowserError {
        AssetBrowserError::InvalidManifest {
            manifest: manifest.to_path_buf(),
            reason: reason.into(),
        }
    }

    fn io_error(operation: &'static str, path: &Path, source: io::Error) -> AssetBrowserError {
        AssetBrowserError::Io {
            operation,
            path: path.to_path_buf(),
            source,
        }
    }

    #[cfg(test)]
    mod tests {
        use super::*;
        use serde_json::{Value, json};
        use std::sync::atomic::{AtomicU64, Ordering};

        static NEXT_FIXTURE: AtomicU64 = AtomicU64::new(0);

        struct TestRoot {
            path: PathBuf,
        }

        impl TestRoot {
            fn new() -> Self {
                let suffix = NEXT_FIXTURE.fetch_add(1, Ordering::Relaxed);
                let path = std::env::temp_dir().join(format!(
                    "forge-assets-browser-{}-{suffix}",
                    std::process::id()
                ));
                fs::create_dir(&path).unwrap();
                Self { path }
            }

            fn write_valid_subset(&self, directory: &str, subset_id: &str) -> PathBuf {
                let directory_path = self.path.join(directory);
                fs::create_dir_all(&directory_path).unwrap();
                let license = fixture_license();
                let scene = fixture_scene();
                let binary = [0_u8, 1, 2, 3];
                let image = [4_u8, 5, 6, 7];
                fs::write(directory_path.join("License.txt"), license).unwrap();
                fs::write(directory_path.join("Tree.gltf"), scene).unwrap();
                fs::write(directory_path.join("Tree.bin"), binary).unwrap();
                fs::write(directory_path.join("Texture.png"), image).unwrap();
                let manifest =
                    fixture_manifest(directory, subset_id, license, scene, &binary, &image);
                let manifest_path = directory_path.join(ASSET_SUBSET_MANIFEST_FILE);
                write_manifest(&manifest_path, &manifest);
                manifest_path
            }
        }

        impl Drop for TestRoot {
            fn drop(&mut self) {
                let temporary_root = std::env::temp_dir();
                let is_owned_fixture = self.path.parent() == Some(temporary_root.as_path())
                    && self.path.file_name().is_some_and(|name| {
                        name.to_string_lossy().starts_with("forge-assets-browser-")
                    });
                if is_owned_fixture {
                    let _ = fs::remove_dir_all(&self.path);
                }
            }
        }

        fn fixture_manifest(
            directory: &str,
            subset_id: &str,
            license: &[u8],
            scene: &[u8],
            binary: &[u8],
            image: &[u8],
        ) -> Value {
            json!({
                "schema": ASSET_SUBSET_MANIFEST_SCHEMA,
                "schema_version": ASSET_SUBSET_MANIFEST_VERSION,
                "subset_id": subset_id,
                "source": {
                    "pack_id": "fixture:pack-1.0",
                    "name": "Synthetic pack",
                    "version": "1.0",
                    "author": "Forge tests",
                    "source_page": "https://example.invalid/source",
                    "distribution_page": "https://example.invalid/distribution",
                    "archive_url": "https://example.invalid/archive.zip",
                    "retrieved_at": "2026-09-05T00:00:00Z",
                    "archive_sha256": hash_hex(b"archive"),
                    "archive_bytes": 7,
                    "normalized_metadata_sha256": hash_hex(b"metadata")
                },
                "license": {
                    "spdx": "CC0-1.0",
                    "url": "https://creativecommons.org/publicdomain/zero/1.0/",
                    "verified": true,
                    "attribution_required": false,
                    "evidence_path": "License.txt",
                    "evidence_sha256": hash_hex(license)
                },
                "models": [{
                    "id": "tree",
                    "kind": "tree",
                    "scene_path": format!("{directory}/Tree.gltf"),
                    "source_path": "glTF/Tree.gltf",
                    "base_scale": 1.0
                }],
                "files": [
                file_declaration("License.txt", "License.txt", license),
                file_declaration("Texture.png", "glTF/Texture.png", image),
                file_declaration("Tree.bin", "glTF/Tree.bin", binary),
                    file_declaration("Tree.gltf", "glTF/Tree.gltf", scene)
                ]
            })
        }

        fn fixture_license() -> &'static [u8] {
            b"Synthetic CC0 evidence\n"
        }

        fn fixture_scene() -> &'static [u8] {
            br#"{"asset":{"version":"2.0"},"buffers":[{"byteLength":4,"uri":"Tree.bin"}],"images":[{"uri":"Texture.png"}]}"#
        }

        fn file_declaration(path: &str, source_path: &str, bytes: &[u8]) -> Value {
            json!({
                "path": path,
                "source_path": source_path,
                "bytes": bytes.len(),
                "sha256": hash_hex(bytes),
                "transformation": "byte-for-byte-copy"
            })
        }

        fn hash_hex(bytes: &[u8]) -> String {
            ContentHash::from_bytes(Sha256::digest(bytes).into()).to_string()
        }

        fn read_manifest_value(path: &Path) -> Value {
            serde_json::from_slice(&fs::read(path).unwrap()).unwrap()
        }

        fn write_manifest(path: &Path, manifest: &Value) {
            fs::write(path, serde_json::to_vec_pretty(manifest).unwrap()).unwrap();
        }

        fn write_fixture_scene(manifest_path: &Path, scene: &Value) {
            let scene_bytes = serde_json::to_vec(scene).unwrap();
            fs::write(
                manifest_path.parent().unwrap().join("Tree.gltf"),
                &scene_bytes,
            )
            .unwrap();
            let mut manifest = read_manifest_value(manifest_path);
            let scene_file = manifest["files"]
                .as_array_mut()
                .unwrap()
                .iter_mut()
                .find(|file| file["path"] == "Tree.gltf")
                .unwrap();
            scene_file["bytes"] = Value::from(u64::try_from(scene_bytes.len()).unwrap());
            scene_file["sha256"] = Value::String(hash_hex(&scene_bytes));
            write_manifest(manifest_path, &manifest);
        }

        fn test_pin_for_manifest(path: &Path) -> GovernedAssetSubsetPin {
            let bytes = fs::read(path).unwrap();
            let manifest: Value = serde_json::from_slice(&bytes).unwrap();
            GovernedAssetSubsetPin {
                subset_id: leak_json_string(&manifest["subset_id"]),
                pack_id: leak_json_string(&manifest["source"]["pack_id"]),
                manifest_hash: ContentHash::from_bytes(Sha256::digest(&bytes).into()),
                archive_hash: parse_json_hash(&manifest["source"]["archive_sha256"]),
                normalized_metadata_hash: parse_json_hash(
                    &manifest["source"]["normalized_metadata_sha256"],
                ),
                license_spdx: leak_json_string(&manifest["license"]["spdx"]),
                license_evidence_path: leak_json_string(&manifest["license"]["evidence_path"]),
                license_evidence_hash: parse_json_hash(&manifest["license"]["evidence_sha256"]),
            }
        }

        fn leak_json_string(value: &Value) -> &'static str {
            Box::leak(value.as_str().unwrap().to_owned().into_boxed_str())
        }

        fn parse_json_hash(value: &Value) -> ContentHash {
            ContentHash::parse(value.as_str().unwrap()).unwrap()
        }

        fn scan_test_asset_browser(root: &Path) -> Result<AssetBrowserSnapshot, AssetBrowserError> {
            let canonical = validate_root(root)?;
            let tree = discover_tree(&canonical)?;
            let pins: Vec<_> = tree
                .manifests
                .iter()
                .map(|path| test_pin_for_manifest(path))
                .collect();
            scan_asset_browser_with_pins(root, &pins)
        }

        #[test]
        fn synthetic_snapshot_is_root_independent_sorted_and_hash_bound() {
            let first_root = TestRoot::new();
            first_root.write_valid_subset("pack", "fixture-pack");
            fs::write(first_root.path.join("ignored-invalid.json"), b"not JSON").unwrap();
            let second_root = TestRoot::new();
            second_root.write_valid_subset("pack", "fixture-pack");
            fs::write(second_root.path.join("ignored-invalid.json"), b"not JSON").unwrap();

            let first = scan_test_asset_browser(&first_root.path).unwrap();
            let second = scan_test_asset_browser(&second_root.path).unwrap();
            let json = serde_json::to_string(&first).unwrap();
            let decoded: AssetBrowserSnapshot = serde_json::from_str(&json).unwrap();

            assert_eq!(first, second);
            assert_eq!(first, decoded);
            assert_eq!(first.schema, ASSET_BROWSER_SNAPSHOT_SCHEMA);
            assert_eq!(first.schema_version, ASSET_BROWSER_SNAPSHOT_VERSION);
            assert_eq!(first.snapshot_hash, first.recompute_hash().unwrap());
            assert_eq!(first.manifests.len(), 1);
            assert_eq!(first.manifests[0].files.len(), 4);
            assert_eq!(first.content_file_count(), 3);
            assert_eq!(first.assets.len(), 1);
            assert_eq!(first.categories.len(), 1);
            assert_eq!(first.assets[0].stable_key, "fixture:pack-1.0/tree");
            assert_eq!(
                first.assets[0].provenance.scene_content_hash,
                ContentHash::parse(&hash_hex(fixture_scene())).unwrap()
            );
        }

        #[test]
        fn asset_identity_depends_on_source_pack_not_local_subset_id() {
            let first_root = TestRoot::new();
            first_root.write_valid_subset("pack", "local-subset-one");
            let second_root = TestRoot::new();
            let second_manifest = second_root.write_valid_subset("pack", "local-subset-two");
            let mut manifest = read_manifest_value(&second_manifest);
            manifest["source"]["pack_id"] = Value::String("FIXTURE:PACK-1.0".to_owned());
            manifest["models"][0]["id"] = Value::String("TREE".to_owned());
            write_manifest(&second_manifest, &manifest);

            let first = scan_test_asset_browser(&first_root.path).unwrap();
            let second = scan_test_asset_browser(&second_root.path).unwrap();

            assert_eq!(first.assets[0].stable_key, second.assets[0].stable_key);
            assert_eq!(first.assets[0].id, second.assets[0].id);
            assert_ne!(
                first.assets[0].provenance.subset_id,
                second.assets[0].provenance.subset_id
            );
        }

        #[test]
        fn repository_subset_proves_one_manifest_nine_models_and_twenty_six_files() {
            let asset_root = Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("..")
                .join("..")
                .join("assets");

            let snapshot = scan_asset_browser(asset_root).unwrap();

            assert_eq!(snapshot.manifests.len(), 1);
            assert_eq!(GOVERNED_ASSET_SUBSET_PINS.len(), 1);
            assert_eq!(snapshot.assets.len(), 9);
            assert_eq!(snapshot.content_file_count(), 26);
            assert_eq!(snapshot.manifests[0].files.len(), 27);
            assert_eq!(snapshot.categories.len(), 5);
            assert_eq!(
                snapshot.manifests[0].subset_id,
                "visual-valley-quaternius-stylized-nature-standard-1.0"
            );
            assert_eq!(snapshot.manifests[0].license.spdx, "CC0-1.0");
            assert_eq!(snapshot.snapshot_hash, snapshot.recompute_hash().unwrap());
            assert!(
                snapshot
                    .assets
                    .windows(2)
                    .all(|pair| pair[0].id < pair[1].id)
            );
        }

        #[test]
        fn unpinned_and_pin_tuple_changes_are_rejected() {
            let unpinned_root = TestRoot::new();
            unpinned_root.write_valid_subset("pack", "fixture-pack");
            assert!(matches!(
                scan_asset_browser(&unpinned_root.path),
                Err(AssetBrowserError::UnpinnedSubset { .. })
            ));

            let manifest_root = TestRoot::new();
            let manifest_path = manifest_root.write_valid_subset("pack", "fixture-pack");
            let pin = test_pin_for_manifest(&manifest_path);
            let mut manifest_bytes = fs::read(&manifest_path).unwrap();
            manifest_bytes.push(b'\n');
            fs::write(&manifest_path, manifest_bytes).unwrap();
            assert!(matches!(
                scan_asset_browser_with_pins(&manifest_root.path, &[pin]),
                Err(AssetBrowserError::GovernedPinMismatch {
                    field: "manifest_sha256",
                    ..
                })
            ));

            let tuple_root = TestRoot::new();
            let tuple_path = tuple_root.write_valid_subset("pack", "fixture-pack");
            let mut manifest = read_manifest_value(&tuple_path);
            manifest["source"]["archive_sha256"] = Value::String(hash_hex(b"forged archive"));
            write_manifest(&tuple_path, &manifest);
            let mut tuple_pin = test_pin_for_manifest(&tuple_path);
            tuple_pin.archive_hash = ContentHash::parse(&hash_hex(b"archive")).unwrap();
            assert!(matches!(
                scan_asset_browser_with_pins(&tuple_root.path, &[tuple_pin]),
                Err(AssetBrowserError::GovernedPinMismatch {
                    field: "source.archive_sha256",
                    ..
                })
            ));
        }

        #[test]
        fn unknown_manifest_field_is_rejected_by_strict_parser() {
            let root = TestRoot::new();
            let path = root.write_valid_subset("pack", "fixture-pack");
            let mut manifest = read_manifest_value(&path);
            manifest["unexpected"] = Value::Bool(true);
            write_manifest(&path, &manifest);

            assert!(matches!(
                scan_test_asset_browser(&root.path),
                Err(AssetBrowserError::ManifestJson { .. })
            ));
        }

        #[test]
        fn absolute_parent_dot_and_backslash_paths_are_rejected() {
            for unsafe_path in [
                "C:/outside/License.txt",
                "/outside/License.txt",
                "../License.txt",
                "./License.txt",
                "folder\\License.txt",
                "folder/scene#variant.gltf",
                "folder/scene?variant.gltf",
                "folder/scene%23variant.gltf",
            ] {
                let root = TestRoot::new();
                let path = root.write_valid_subset("pack", "fixture-pack");
                let mut manifest = read_manifest_value(&path);
                manifest["files"][0]["path"] = Value::String(unsafe_path.to_owned());
                write_manifest(&path, &manifest);

                assert!(matches!(
                    scan_test_asset_browser(&root.path),
                    Err(AssetBrowserError::UnsafeManifestPath { .. })
                ));
            }
        }

        #[test]
        fn cadle_component_is_rejected_before_manifest_parsing() {
            let root = TestRoot::new();
            fs::create_dir(root.path.join("Cadle-reference")).unwrap();

            assert!(matches!(
                scan_asset_browser(&root.path),
                Err(AssetBrowserError::ForbiddenCadlePath(_))
            ));

            let manifest_root = TestRoot::new();
            let manifest_path = manifest_root.write_valid_subset("pack", "fixture-pack");
            let mut manifest = read_manifest_value(&manifest_path);
            manifest["source"]["source_page"] =
                Value::String("https://example.invalid/Cadle".to_owned());
            write_manifest(&manifest_path, &manifest);
            assert!(matches!(
                scan_test_asset_browser(&manifest_root.path),
                Err(AssetBrowserError::InvalidManifest { .. })
            ));
        }

        #[test]
        fn duplicate_file_model_and_subset_identifiers_are_rejected() {
            let file_root = TestRoot::new();
            let file_manifest = file_root.write_valid_subset("pack", "fixture-pack");
            let mut manifest = read_manifest_value(&file_manifest);
            let duplicate_file = manifest["files"][0].clone();
            manifest["files"]
                .as_array_mut()
                .unwrap()
                .push(duplicate_file);
            write_manifest(&file_manifest, &manifest);
            assert!(matches!(
                scan_test_asset_browser(&file_root.path),
                Err(AssetBrowserError::DuplicateFilePath { .. })
            ));

            let model_root = TestRoot::new();
            let model_manifest = model_root.write_valid_subset("pack", "fixture-pack");
            let mut manifest = read_manifest_value(&model_manifest);
            let duplicate_model = manifest["models"][0].clone();
            manifest["models"]
                .as_array_mut()
                .unwrap()
                .push(duplicate_model);
            write_manifest(&model_manifest, &manifest);
            assert!(matches!(
                scan_test_asset_browser(&model_root.path),
                Err(AssetBrowserError::DuplicateModelId { .. })
            ));

            let subset_root = TestRoot::new();
            subset_root.write_valid_subset("first", "fixture-pack");
            subset_root.write_valid_subset("second", "fixture-pack");
            assert!(matches!(
                scan_test_asset_browser(&subset_root.path),
                Err(AssetBrowserError::DuplicateSubsetId(id)) if id == "fixture-pack"
            ));
        }

        #[test]
        fn exact_inventory_size_and_hash_are_enforced() {
            let inventory_root = TestRoot::new();
            inventory_root.write_valid_subset("pack", "fixture-pack");
            fs::write(inventory_root.path.join("pack").join("extra.bin"), b"extra").unwrap();
            assert!(matches!(
                scan_test_asset_browser(&inventory_root.path),
                Err(AssetBrowserError::UnexpectedFile { .. })
            ));

            let size_root = TestRoot::new();
            let size_manifest = size_root.write_valid_subset("pack", "fixture-pack");
            let mut manifest = read_manifest_value(&size_manifest);
            manifest["files"][1]["bytes"] = Value::from(999_u64);
            write_manifest(&size_manifest, &manifest);
            assert!(matches!(
                scan_test_asset_browser(&size_root.path),
                Err(AssetBrowserError::FileSizeMismatch { .. })
            ));

            let hash_root = TestRoot::new();
            hash_root.write_valid_subset("pack", "fixture-pack");
            let scene_path = hash_root.path.join("pack").join("Tree.gltf");
            let scene_bytes = usize::try_from(fs::metadata(&scene_path).unwrap().len()).unwrap();
            fs::write(scene_path, vec![b'x'; scene_bytes]).unwrap();
            assert!(matches!(
                scan_test_asset_browser(&hash_root.path),
                Err(AssetBrowserError::FileHashMismatch { .. })
            ));
        }

        #[test]
        fn license_must_be_verified_declared_and_hash_exact() {
            let unverified_root = TestRoot::new();
            let path = unverified_root.write_valid_subset("pack", "fixture-pack");
            let mut manifest = read_manifest_value(&path);
            manifest["license"]["verified"] = Value::Bool(false);
            write_manifest(&path, &manifest);
            assert!(matches!(
                scan_test_asset_browser(&unverified_root.path),
                Err(AssetBrowserError::LicenseNotVerified(_))
            ));

            let mismatch_root = TestRoot::new();
            let path = mismatch_root.write_valid_subset("pack", "fixture-pack");
            let mut manifest = read_manifest_value(&path);
            manifest["license"]["evidence_sha256"] = Value::String(hash_hex(b"other"));
            write_manifest(&path, &manifest);
            assert!(matches!(
                scan_test_asset_browser(&mismatch_root.path),
                Err(AssetBrowserError::LicenseEvidenceHashMismatch { .. })
            ));

            let missing_root = TestRoot::new();
            let path = missing_root.write_valid_subset("pack", "fixture-pack");
            let mut manifest = read_manifest_value(&path);
            manifest["license"]["evidence_path"] = Value::String("Missing.txt".to_owned());
            write_manifest(&path, &manifest);
            assert!(matches!(
                scan_test_asset_browser(&missing_root.path),
                Err(AssetBrowserError::LicenseEvidenceMissing { .. })
            ));
        }

        #[test]
        fn model_scene_must_match_declared_path_source_and_supported_format() {
            for (field, value) in [
                ("scene_path", "pack/Missing.gltf"),
                ("scene_path", "pack/Tree.bin"),
                ("source_path", "glTF/Other.gltf"),
            ] {
                let root = TestRoot::new();
                let path = root.write_valid_subset("pack", "fixture-pack");
                let mut manifest = read_manifest_value(&path);
                manifest["models"][0][field] = Value::String(value.to_owned());
                write_manifest(&path, &manifest);

                let result = scan_test_asset_browser(&root.path);
                assert!(matches!(
                    result,
                    Err(AssetBrowserError::ModelSceneNotDeclared { .. }
                        | AssetBrowserError::UnsupportedSceneFormat { .. })
                ));
            }
        }

        #[test]
        fn gltf_buffer_and_image_uris_are_strictly_local_and_declared() {
            for (kind, uri, is_undeclared) in [
                ("buffers", "../outside.bin", false),
                ("buffers", "./Tree.bin", false),
                ("buffers", "/absolute.bin", false),
                ("buffers", "folder\\Tree.bin", false),
                ("buffers", "file:///Tree.bin", false),
                ("buffers", "http://example.invalid/Tree.bin", false),
                ("images", "data:image/png;base64,AA==", false),
                ("images", "Cadle.png", false),
                ("images", "..%2Foutside.png", false),
                ("buffers", "Missing.bin", true),
            ] {
                let root = TestRoot::new();
                let manifest_path = root.write_valid_subset("pack", "fixture-pack");
                let mut scene: Value = serde_json::from_slice(fixture_scene()).unwrap();
                scene[kind][0]["uri"] = Value::String(uri.to_owned());
                write_fixture_scene(&manifest_path, &scene);

                let result = scan_test_asset_browser(&root.path);
                if is_undeclared {
                    assert!(matches!(
                        result,
                        Err(AssetBrowserError::UndeclaredGltfUri { .. })
                    ));
                } else {
                    assert!(matches!(
                        result,
                        Err(AssetBrowserError::UnsafeGltfUri { .. })
                    ));
                }
            }
        }

        #[test]
        fn gltf_buffers_require_uris_and_glb_is_fail_closed() {
            let missing_uri_root = TestRoot::new();
            let manifest_path = missing_uri_root.write_valid_subset("pack", "fixture-pack");
            let mut scene: Value = serde_json::from_slice(fixture_scene()).unwrap();
            scene["buffers"][0].as_object_mut().unwrap().remove("uri");
            write_fixture_scene(&manifest_path, &scene);
            assert!(matches!(
                scan_test_asset_browser(&missing_uri_root.path),
                Err(AssetBrowserError::MissingGltfBufferUri { .. })
            ));

            let glb_root = TestRoot::new();
            let manifest_path = glb_root.write_valid_subset("pack", "fixture-pack");
            let bytes = b"not an inspected glb";
            fs::write(manifest_path.parent().unwrap().join("Extra.glb"), bytes).unwrap();
            let mut manifest = read_manifest_value(&manifest_path);
            manifest["files"]
                .as_array_mut()
                .unwrap()
                .push(file_declaration("Extra.glb", "glTF/Extra.glb", bytes));
            write_manifest(&manifest_path, &manifest);
            assert!(matches!(
                scan_test_asset_browser(&glb_root.path),
                Err(AssetBrowserError::UnsupportedGlb { .. })
            ));
        }

        #[test]
        fn manifest_byte_budget_is_enforced_before_json_parsing() {
            let root = TestRoot::new();
            let directory = root.path.join("pack");
            fs::create_dir(&directory).unwrap();
            let oversized = vec![b' '; usize::try_from(MAX_ASSET_MANIFEST_BYTES).unwrap() + 1];
            fs::write(directory.join(ASSET_SUBSET_MANIFEST_FILE), oversized).unwrap();

            assert!(matches!(
                scan_asset_browser(&root.path),
                Err(AssetBrowserError::LimitExceeded {
                    resource: "bytes per manifest",
                    ..
                })
            ));
        }

        #[test]
        fn depth_and_model_count_budgets_are_enforced() {
            let depth_root = TestRoot::new();
            let mut directory = depth_root.path.clone();
            for index in 0..=MAX_ASSET_SCAN_DEPTH {
                directory = directory.join(format!("level-{index}"));
                fs::create_dir(&directory).unwrap();
            }
            assert!(matches!(
                scan_asset_browser(&depth_root.path),
                Err(AssetBrowserError::LimitExceeded {
                    resource: "directory depth",
                    ..
                })
            ));

            let model_root = TestRoot::new();
            let manifest_path = model_root.write_valid_subset("pack", "fixture-pack");
            let mut manifest = read_manifest_value(&manifest_path);
            let model = manifest["models"][0].clone();
            let models = manifest["models"].as_array_mut().unwrap();
            while models.len() <= MAX_ASSET_MODELS {
                models.push(model.clone());
            }
            write_manifest(&manifest_path, &manifest);
            assert!(matches!(
                scan_test_asset_browser(&model_root.path),
                Err(AssetBrowserError::LimitExceeded {
                    resource: "models",
                    ..
                })
            ));
        }

        #[test]
        fn massive_directory_is_stopped_during_streaming_before_sort() {
            assert_eq!(
                MAX_ASSET_SCAN_ENTRIES,
                MAX_ASSET_SCAN_DIRECTORIES + MAX_ASSET_FILES + MAX_ASSET_MANIFESTS
            );
            let root = TestRoot::new();
            for index in 0..9 {
                fs::write(root.path.join(format!("entry-{index}.bin")), []).unwrap();
            }
            let canonical = validate_root(&root.path).unwrap();
            let limits = DiscoveryLimits {
                depth: 1,
                directories: 2,
                manifests: 2,
                files: 16,
                entries: 8,
            };

            assert!(matches!(
                discover_tree_with_limits(&canonical, limits),
                Err(AssetBrowserError::LimitExceeded {
                    resource: "scan entries",
                    limit: 8
                })
            ));
        }

        #[test]
        fn empty_root_is_rejected_explicitly() {
            let root = TestRoot::new();

            assert!(matches!(
                scan_asset_browser(&root.path),
                Err(AssetBrowserError::NoSubsetManifests)
            ));
        }

        #[test]
        fn symbolic_link_is_rejected_when_platform_allows_fixture_creation() {
            let root = TestRoot::new();
            root.write_valid_subset("pack", "fixture-pack");
            let target = root.path.join("pack").join("Tree.bin");
            let link = root.path.join("linked.bin");
            if let Err(error) = create_file_symlink(&target, &link) {
                assert!(
                    error.kind() == io::ErrorKind::PermissionDenied
                        || (cfg!(windows) && error.raw_os_error() == Some(1_314))
                );
                return;
            }

            assert!(matches!(
                scan_asset_browser(&root.path),
                Err(AssetBrowserError::ReparsePoint(_))
            ));
        }

        #[cfg(windows)]
        fn create_file_symlink(target: &Path, link: &Path) -> io::Result<()> {
            std::os::windows::fs::symlink_file(target, link)
        }

        #[cfg(unix)]
        fn create_file_symlink(target: &Path, link: &Path) -> io::Result<()> {
            std::os::unix::fs::symlink(target, link)
        }
    }
}

#[cfg(not(target_arch = "wasm32"))]
pub use native::{AssetBrowserError, scan_asset_browser};
