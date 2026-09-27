use forge_assets::{
    GovernedAssetKitAsset, GovernedAssetKitBuildReport, GovernedAssetKitError,
    GovernedAssetKitRuntimePreview,
};
use forge_core::ContentHash;
use sha2::{Digest, Sha256};
use std::{
    error::Error,
    fmt, fs, io,
    io::{Read, Seek},
    path::{Path, PathBuf},
};

/// Filesystem-backed governed asset-kit documents after contract and content verification.
#[derive(Clone, Debug)]
pub(crate) struct LoadedGovernedAssetKit {
    pub(crate) report: GovernedAssetKitBuildReport,
    pub(crate) preview: GovernedAssetKitRuntimePreview,
    /// Canonical parent directory of the Blender build report.
    pub(crate) asset_root: PathBuf,
}

impl LoadedGovernedAssetKit {
    pub(crate) fn load(
        build_report_path: impl AsRef<Path>,
        runtime_preview_path: impl AsRef<Path>,
    ) -> Result<Self, GovernedAssetKitLoadError> {
        let build_report_path = build_report_path.as_ref();
        let runtime_preview_path = runtime_preview_path.as_ref();

        let report_json = fs::read_to_string(build_report_path).map_err(|source| {
            GovernedAssetKitLoadError::ReadBuildReport {
                path: build_report_path.to_path_buf(),
                source,
            }
        })?;
        let report = GovernedAssetKitBuildReport::from_json(&report_json).map_err(|source| {
            GovernedAssetKitLoadError::InvalidBuildReport {
                path: build_report_path.to_path_buf(),
                source,
            }
        })?;

        let preview_json = fs::read_to_string(runtime_preview_path).map_err(|source| {
            GovernedAssetKitLoadError::ReadRuntimePreview {
                path: runtime_preview_path.to_path_buf(),
                source,
            }
        })?;
        let preview =
            GovernedAssetKitRuntimePreview::from_json(&preview_json).map_err(|source| {
                GovernedAssetKitLoadError::InvalidRuntimePreview {
                    path: runtime_preview_path.to_path_buf(),
                    source,
                }
            })?;

        preview
            .validate_against_build_report(&report)
            .map_err(GovernedAssetKitLoadError::InvalidContract)?;

        let asset_root = resolve_asset_root(build_report_path)?;
        for asset in &report.assets {
            verify_asset_file(asset, &asset_root)?;
        }

        Ok(Self {
            report,
            preview,
            asset_root,
        })
    }
}

fn resolve_asset_root(build_report_path: &Path) -> Result<PathBuf, GovernedAssetKitLoadError> {
    let candidate = build_report_path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
        .unwrap_or_else(|| Path::new("."));
    fs::canonicalize(candidate).map_err(|source| GovernedAssetKitLoadError::ResolveAssetRoot {
        build_report_path: build_report_path.to_path_buf(),
        candidate: candidate.to_path_buf(),
        source,
    })
}

fn verify_asset_file(
    asset: &GovernedAssetKitAsset,
    asset_root: &Path,
) -> Result<(), GovernedAssetKitLoadError> {
    let candidate = asset_root.join(&asset.glb.path);
    let resolved_path = fs::canonicalize(&candidate).map_err(|source| {
        GovernedAssetKitLoadError::ResolveAssetPath {
            asset_id: asset.id.clone(),
            path: candidate.clone(),
            source,
        }
    })?;
    ensure_asset_is_within_root(&asset.id, &asset.glb.path, asset_root, &resolved_path)?;

    let mut file =
        fs::File::open(&resolved_path).map_err(|source| GovernedAssetKitLoadError::ReadAsset {
            asset_id: asset.id.clone(),
            path: resolved_path.clone(),
            source,
        })?;
    let mut hasher = Sha256::new();
    let mut buffer = vec![0_u8; 64 * 1_024].into_boxed_slice();
    loop {
        let read =
            file.read(&mut buffer)
                .map_err(|source| GovernedAssetKitLoadError::ReadAsset {
                    asset_id: asset.id.clone(),
                    path: resolved_path.clone(),
                    source,
                })?;
        if read == 0 {
            break;
        }
        hasher.update(&buffer[..read]);
    }
    let actual = ContentHash::from_bytes(hasher.finalize().into());
    if actual != asset.glb.sha256 {
        return Err(GovernedAssetKitLoadError::AssetHashMismatch {
            asset_id: asset.id.clone(),
            path: resolved_path,
            expected: asset.glb.sha256,
            actual,
        });
    }
    verify_self_contained_glb(&mut file).map_err(|source| {
        GovernedAssetKitLoadError::ReadAsset {
            asset_id: asset.id.clone(),
            path: resolved_path,
            source,
        }
    })?;
    Ok(())
}

fn verify_self_contained_glb(file: &mut fs::File) -> io::Result<()> {
    file.rewind()?;
    let mut header = [0_u8; 20];
    file.read_exact(&mut header)?;
    let length = forge_assets::governed_glb_json_length(&header, file.metadata()?.len())
        .map_err(|error| io::Error::new(io::ErrorKind::InvalidData, error))?;
    let mut json = vec![0; length];
    file.read_exact(&mut json)?;
    forge_assets::validate_governed_glb_json(&json)
        .map_err(|error| io::Error::new(io::ErrorKind::InvalidData, error))
}

fn ensure_asset_is_within_root(
    asset_id: &str,
    declared_path: &str,
    asset_root: &Path,
    resolved_path: &Path,
) -> Result<(), GovernedAssetKitLoadError> {
    if resolved_path.starts_with(asset_root) {
        return Ok(());
    }
    Err(GovernedAssetKitLoadError::AssetOutsideRoot {
        asset_id: asset_id.to_owned(),
        declared_path: declared_path.to_owned(),
        resolved_path: resolved_path.to_path_buf(),
        asset_root: asset_root.to_path_buf(),
    })
}

#[derive(Debug)]
pub(crate) enum GovernedAssetKitLoadError {
    ReadBuildReport {
        path: PathBuf,
        source: io::Error,
    },
    InvalidBuildReport {
        path: PathBuf,
        source: GovernedAssetKitError,
    },
    ReadRuntimePreview {
        path: PathBuf,
        source: io::Error,
    },
    InvalidRuntimePreview {
        path: PathBuf,
        source: GovernedAssetKitError,
    },
    InvalidContract(GovernedAssetKitError),
    ResolveAssetRoot {
        build_report_path: PathBuf,
        candidate: PathBuf,
        source: io::Error,
    },
    ResolveAssetPath {
        asset_id: String,
        path: PathBuf,
        source: io::Error,
    },
    AssetOutsideRoot {
        asset_id: String,
        declared_path: String,
        resolved_path: PathBuf,
        asset_root: PathBuf,
    },
    ReadAsset {
        asset_id: String,
        path: PathBuf,
        source: io::Error,
    },
    AssetHashMismatch {
        asset_id: String,
        path: PathBuf,
        expected: ContentHash,
        actual: ContentHash,
    },
}

impl fmt::Display for GovernedAssetKitLoadError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::ReadBuildReport { path, source } => write!(
                formatter,
                "cannot read governed asset-kit build report {}: {source}",
                path.display()
            ),
            Self::InvalidBuildReport { path, source } => write!(
                formatter,
                "invalid governed asset-kit build report {}: {source}",
                path.display()
            ),
            Self::ReadRuntimePreview { path, source } => write!(
                formatter,
                "cannot read governed asset-kit runtime preview {}: {source}",
                path.display()
            ),
            Self::InvalidRuntimePreview { path, source } => write!(
                formatter,
                "invalid governed asset-kit runtime preview {}: {source}",
                path.display()
            ),
            Self::InvalidContract(source) => {
                write!(
                    formatter,
                    "incompatible governed asset-kit documents: {source}"
                )
            }
            Self::ResolveAssetRoot {
                build_report_path,
                candidate,
                source,
            } => write!(
                formatter,
                "cannot resolve asset root {} from build report {}: {source}",
                candidate.display(),
                build_report_path.display()
            ),
            Self::ResolveAssetPath {
                asset_id,
                path,
                source,
            } => write!(
                formatter,
                "cannot resolve GLB for governed asset `{asset_id}` at {}: {source}",
                path.display()
            ),
            Self::AssetOutsideRoot {
                asset_id,
                declared_path,
                resolved_path,
                asset_root,
            } => write!(
                formatter,
                "GLB `{declared_path}` for governed asset `{asset_id}` resolves to {}, outside asset root {}",
                resolved_path.display(),
                asset_root.display()
            ),
            Self::ReadAsset {
                asset_id,
                path,
                source,
            } => write!(
                formatter,
                "cannot read GLB for governed asset `{asset_id}` at {}: {source}",
                path.display()
            ),
            Self::AssetHashMismatch {
                asset_id,
                path,
                expected,
                actual,
            } => write!(
                formatter,
                "GLB hash mismatch for governed asset `{asset_id}` at {}: expected {expected}, got {actual}",
                path.display()
            ),
        }
    }
}

impl Error for GovernedAssetKitLoadError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::ReadBuildReport { source, .. }
            | Self::ReadRuntimePreview { source, .. }
            | Self::ResolveAssetRoot { source, .. }
            | Self::ResolveAssetPath { source, .. }
            | Self::ReadAsset { source, .. } => Some(source),
            Self::InvalidBuildReport { source, .. }
            | Self::InvalidRuntimePreview { source, .. }
            | Self::InvalidContract(source) => Some(source),
            Self::AssetOutsideRoot { .. } | Self::AssetHashMismatch { .. } => None,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::{Value, json};
    use std::sync::atomic::{AtomicU64, Ordering};

    static NEXT_TEMP_DIRECTORY: AtomicU64 = AtomicU64::new(0);
    const MANIFEST_HASH: &str = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const SOURCE_WORLD_SKETCH_HASH: &str =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const SOURCE_TERRAIN_HASH: &str =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    const ASSET_BYTES: &[u8] = b"glTF\x02\x00\x00\x00\x30\x00\x00\x00\x1c\x00\x00\x00JSON{\"asset\":{\"version\":\"2.0\"}} ";

    struct TestDirectory(PathBuf);

    impl TestDirectory {
        fn new(label: &str) -> Self {
            let sequence = NEXT_TEMP_DIRECTORY.fetch_add(1, Ordering::Relaxed);
            let path = std::env::temp_dir().join(format!(
                "forge-runtime-governed-asset-kit-{label}-{}-{sequence}",
                std::process::id()
            ));
            fs::create_dir(&path).expect("test directory must be creatable");
            Self(path)
        }

        fn path(&self) -> &Path {
            &self.0
        }
    }

    impl Drop for TestDirectory {
        fn drop(&mut self) {
            let _ = fs::remove_dir_all(&self.0);
        }
    }

    struct Fixture {
        directory: TestDirectory,
        build_report_path: PathBuf,
        runtime_preview_path: PathBuf,
    }

    impl Fixture {
        fn new() -> Self {
            let directory = TestDirectory::new("fixture");
            let assets = directory.path().join("assets");
            fs::create_dir(&assets).expect("asset directory must be creatable");
            fs::write(assets.join("test-tree.glb"), ASSET_BYTES)
                .expect("synthetic GLB must be writable");

            let build_report_path = directory.path().join("build-report.v1.json");
            let runtime_preview_path = directory.path().join("runtime-preview.v1.json");
            write_json(&build_report_path, &build_report(content_hash(ASSET_BYTES)));
            write_json(&runtime_preview_path, &runtime_preview(1));
            Self {
                directory,
                build_report_path,
                runtime_preview_path,
            }
        }
    }

    fn content_hash(bytes: &[u8]) -> ContentHash {
        ContentHash::from_bytes(Sha256::digest(bytes).into())
    }

    fn build_report(asset_hash: ContentHash) -> Value {
        json!({
            "schema": "forge.blender-asset-kit-build",
            "schema_version": 1,
            "asset_kit_id": "test-kit",
            "asset_kit_revision": 1,
            "generator": { "input_manifest_sha256": MANIFEST_HASH },
            "provenance": {
                "source_kind": "procedural_original",
                "license": "LicenseRef-OpenRPG-Forge-Project-Generated",
                "cadle_content_used": false,
                "commercial_game_content_used": false
            },
            "qualification": { "technical_export_validated": true },
            "assets": [{
                "id": "test-tree",
                "kind": "tree",
                "glb": {
                    "path": "assets/test-tree.glb",
                    "sha256": asset_hash.to_string()
                }
            }]
        })
    }

    fn runtime_preview(asset_kit_revision: u32) -> Value {
        json!({
            "schema": "forge.asset-kit-runtime-preview",
            "schema_version": 1,
            "asset_kit_id": "test-kit",
            "asset_kit_revision": asset_kit_revision,
            "source_world_sketch_sha256": SOURCE_WORLD_SKETCH_HASH,
            "source_terrain_sha256": SOURCE_TERRAIN_HASH,
            "input_manifest_sha256": MANIFEST_HASH,
            "camera_id": "hero_reference_match",
            "assets": ["test-tree"],
            "placements": [{
                "id": "tree-one",
                "asset_id": "test-tree",
                "xz_m": [0.0, 0.0],
                "surface_offset_m": 0.0,
                "yaw_degrees": 0.0,
                "scale": 1.0
            }]
        })
    }

    fn write_json(path: &Path, value: &Value) {
        fs::write(path, value.to_string()).expect("test JSON must be writable");
    }

    #[test]
    fn loads_valid_documents_and_hashes_glb_bytes_from_the_build_parent() {
        let fixture = Fixture::new();

        let loaded =
            LoadedGovernedAssetKit::load(&fixture.build_report_path, &fixture.runtime_preview_path)
                .expect("valid filesystem-backed kit must load");

        assert_eq!(loaded.report.asset_kit_id, "test-kit");
        assert_eq!(loaded.preview.placements.len(), 1);
        assert_eq!(
            loaded.asset_root,
            fs::canonicalize(fixture.directory.path()).unwrap()
        );
    }

    #[test]
    fn matching_hash_does_not_authorize_an_external_texture() {
        let fixture = Fixture::new();
        let mut json = br#"{"asset":{"version":"2.0"},"images":[{"uri":"https://example.invalid/texture.png"}]}"#.to_vec();
        json.resize(json.len().next_multiple_of(4), b' ');
        let mut bytes = b"glTF".to_vec();
        bytes.extend_from_slice(&2_u32.to_le_bytes());
        bytes.extend_from_slice(&u32::try_from(20 + json.len()).unwrap().to_le_bytes());
        bytes.extend_from_slice(&u32::try_from(json.len()).unwrap().to_le_bytes());
        bytes.extend_from_slice(b"JSON");
        bytes.extend_from_slice(&json);
        fs::write(
            fixture.directory.path().join("assets/test-tree.glb"),
            &bytes,
        )
        .unwrap();
        write_json(
            &fixture.build_report_path,
            &build_report(content_hash(&bytes)),
        );
        let error =
            LoadedGovernedAssetKit::load(&fixture.build_report_path, &fixture.runtime_preview_path)
                .unwrap_err();
        assert!(matches!(error, GovernedAssetKitLoadError::ReadAsset { .. }));
        assert!(error.to_string().contains("without URI references"));
    }

    #[test]
    fn authored_kit_requires_preview_pin_but_not_blender_source_files() {
        let fixture = Fixture::new();
        let mut report = build_report(content_hash(ASSET_BYTES));
        report["provenance"]["source_kind"] = json!("procedural_original_with_authored_edits");
        report["authored_source"] = json!({
            "descriptor_sha256": SOURCE_WORLD_SKETCH_HASH,
            "blend_sha256": SOURCE_TERRAIN_HASH,
            "base_manifest_sha256": MANIFEST_HASH,
            "assets": ["test-tree"], "human_review": "pending"
        });
        write_json(&fixture.build_report_path, &report);
        assert!(matches!(
            LoadedGovernedAssetKit::load(&fixture.build_report_path, &fixture.runtime_preview_path),
            Err(GovernedAssetKitLoadError::InvalidContract(
                GovernedAssetKitError::AuthoredSourceMismatch
            ))
        ));
        let mut preview = runtime_preview(1);
        preview["authored_source_sha256"] = json!(SOURCE_WORLD_SKETCH_HASH);
        write_json(&fixture.runtime_preview_path, &preview);
        let loaded =
            LoadedGovernedAssetKit::load(&fixture.build_report_path, &fixture.runtime_preview_path)
                .unwrap();
        assert!(loaded.report.authored_source.is_some());
        fs::write(
            fixture.directory.path().join("assets/test-tree.glb"),
            b"changed mesh",
        )
        .unwrap();
        assert!(matches!(
            LoadedGovernedAssetKit::load(&fixture.build_report_path, &fixture.runtime_preview_path),
            Err(GovernedAssetKitLoadError::AssetHashMismatch { .. })
        ));
    }

    #[test]
    fn rejects_a_preview_that_does_not_match_the_build_contract() {
        let fixture = Fixture::new();
        write_json(&fixture.runtime_preview_path, &runtime_preview(2));

        let error =
            LoadedGovernedAssetKit::load(&fixture.build_report_path, &fixture.runtime_preview_path)
                .unwrap_err();

        assert!(matches!(
            &error,
            GovernedAssetKitLoadError::InvalidContract(
                GovernedAssetKitError::AssetKitRevisionMismatch { .. }
            )
        ));
        assert!(Error::source(&error).is_some());
    }

    #[test]
    fn rejects_glb_bytes_that_do_not_match_the_report_hash() {
        let fixture = Fixture::new();
        write_json(
            &fixture.build_report_path,
            &build_report(content_hash(b"different GLB bytes")),
        );

        let error =
            LoadedGovernedAssetKit::load(&fixture.build_report_path, &fixture.runtime_preview_path)
                .unwrap_err();

        assert!(matches!(
            &error,
            GovernedAssetKitLoadError::AssetHashMismatch { asset_id, .. }
                if asset_id.as_str() == "test-tree"
        ));
        assert!(error.to_string().contains("expected"));
    }

    #[test]
    fn rejects_a_resolved_asset_path_outside_the_canonical_root() {
        let directory = TestDirectory::new("escape");
        let asset_root = directory.path().join("root");
        fs::create_dir(&asset_root).unwrap();
        let outside = directory.path().join("outside.glb");
        fs::write(&outside, ASSET_BYTES).unwrap();
        let asset_root = fs::canonicalize(asset_root).unwrap();
        let outside = fs::canonicalize(outside).unwrap();

        let error =
            ensure_asset_is_within_root("test-tree", "assets/test-tree.glb", &asset_root, &outside)
                .unwrap_err();

        assert!(matches!(
            error,
            GovernedAssetKitLoadError::AssetOutsideRoot { asset_id, .. }
                if asset_id == "test-tree"
        ));
    }

    #[test]
    fn filesystem_and_contract_errors_expose_their_sources() {
        let directory = TestDirectory::new("sources");
        let missing_report = directory.path().join("missing-build-report.json");
        let missing_preview = directory.path().join("missing-preview.json");

        let error = LoadedGovernedAssetKit::load(&missing_report, &missing_preview).unwrap_err();

        assert!(matches!(
            &error,
            GovernedAssetKitLoadError::ReadBuildReport { .. }
        ));
        assert!(Error::source(&error).is_some());
    }
}
