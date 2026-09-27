//! Engine-neutral contracts between the governed Blender asset build and its runtime preview.

use forge_core::ContentHash;
use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use std::path::{Component, Path};
use thiserror::Error;

pub const GOVERNED_ASSET_KIT_BUILD_SCHEMA: &str = "forge.blender-asset-kit-build";
pub const GOVERNED_ASSET_KIT_BUILD_SCHEMA_VERSION: u32 = 1;
pub const GOVERNED_ASSET_KIT_PREVIEW_SCHEMA: &str = "forge.asset-kit-runtime-preview";
pub const GOVERNED_ASSET_KIT_PREVIEW_SCHEMA_VERSION: u32 = 1;
pub const GOVERNED_ASSET_KIT_SOURCE_KIND: &str = "procedural_original";
pub const GOVERNED_ASSET_KIT_AUTHORED_SOURCE_KIND: &str = "procedural_original_with_authored_edits";
pub const GOVERNED_ASSET_KIT_LICENSE: &str = "LicenseRef-OpenRPG-Forge-Project-Generated";
pub const MAX_GOVERNED_ASSET_KIT_ASSETS: usize = 1_024;
pub const MAX_GOVERNED_ASSET_KIT_PLACEMENTS: usize = 100_000;
pub const MAX_GOVERNED_ASSET_KIT_WORLD_COORDINATE_M: f32 = 1_000_000.0;
pub const MAX_GOVERNED_ASSET_KIT_SURFACE_OFFSET_M: f32 = 100.0;
pub const MAX_GOVERNED_ASSET_KIT_YAW_DEGREES: f32 = 360.0;
pub const MIN_GOVERNED_ASSET_KIT_SCALE: f32 = 0.01;
pub const MAX_GOVERNED_ASSET_KIT_SCALE: f32 = 100.0;

/// Minimal generator evidence consumed from the richer Blender report.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
pub struct GovernedAssetKitGenerator {
    pub input_manifest_sha256: ContentHash,
}

/// Provenance fields that must remain safe before a generated kit can enter the runtime.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
pub struct GovernedAssetKitProvenance {
    pub source_kind: String,
    pub license: String,
    pub cadle_content_used: bool,
    pub commercial_game_content_used: bool,
}

/// Technical qualification consumed by R1-064. Artistic and publication reviews remain separate.
#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
pub struct GovernedAssetKitQualification {
    pub technical_export_validated: bool,
}

/// One GLB output recorded by the governed Blender build.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
pub struct GovernedAssetKitGlb {
    pub path: String,
    pub sha256: ContentHash,
}

/// One asset consumed from the richer governed Blender build report.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
pub struct GovernedAssetKitAsset {
    pub id: String,
    pub kind: String,
    pub glb: GovernedAssetKitGlb,
}

/// Read-only projection of the Blender report fields required by the runtime integration.
///
/// Unknown fields are intentionally accepted here because the DCC report also carries geometry,
/// texture, render and reproducibility evidence that does not belong in the runtime contract.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
pub struct GovernedAssetKitBuildReport {
    pub schema: String,
    pub schema_version: u32,
    pub asset_kit_id: String,
    pub asset_kit_revision: u32,
    pub generator: GovernedAssetKitGenerator,
    pub provenance: GovernedAssetKitProvenance,
    pub qualification: GovernedAssetKitQualification,
    pub assets: Vec<GovernedAssetKitAsset>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub authored_source: Option<GovernedAssetKitAuthoredSource>,
}

/// Lineage recorded by the DCC worker; source files are not runtime dependencies.
#[derive(Clone, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GovernedAssetKitAuthoredSource {
    pub descriptor_sha256: ContentHash,
    pub blend_sha256: ContentHash,
    pub base_manifest_sha256: ContentHash,
    pub assets: Vec<String>,
    pub human_review: String,
}

impl GovernedAssetKitBuildReport {
    pub fn from_json(json: &str) -> Result<Self, GovernedAssetKitError> {
        let report: Self = serde_json::from_str(json)?;
        report.validate()?;
        Ok(report)
    }

    pub fn validate(&self) -> Result<(), GovernedAssetKitError> {
        validate_schema(
            &self.schema,
            self.schema_version,
            GOVERNED_ASSET_KIT_BUILD_SCHEMA,
            GOVERNED_ASSET_KIT_BUILD_SCHEMA_VERSION,
        )?;
        validate_asset_kit_identity(&self.asset_kit_id, self.asset_kit_revision)?;
        self.provenance.validate()?;
        if !self.qualification.technical_export_validated {
            return Err(GovernedAssetKitError::TechnicalExportNotValidated);
        }
        validate_report_assets(&self.assets)?;
        match (&self.authored_source, self.provenance.source_kind.as_str()) {
            (None, GOVERNED_ASSET_KIT_SOURCE_KIND) => Ok(()),
            (Some(source), GOVERNED_ASSET_KIT_AUTHORED_SOURCE_KIND) => {
                let ids: HashSet<_> = source.assets.iter().collect();
                if source.base_manifest_sha256 != self.generator.input_manifest_sha256
                    || source.assets.is_empty()
                    || ids.len() != source.assets.len()
                    || source.human_review != "pending"
                    || ids
                        .iter()
                        .any(|id| !self.assets.iter().any(|asset| &asset.id == *id))
                {
                    return Err(GovernedAssetKitError::InvalidAuthoredSource);
                }
                Ok(())
            }
            _ => Err(GovernedAssetKitError::InvalidAuthoredSource),
        }
    }
}

impl GovernedAssetKitProvenance {
    pub fn validate(&self) -> Result<(), GovernedAssetKitError> {
        if !matches!(
            self.source_kind.as_str(),
            GOVERNED_ASSET_KIT_SOURCE_KIND | GOVERNED_ASSET_KIT_AUTHORED_SOURCE_KIND
        ) {
            return Err(GovernedAssetKitError::InvalidSourceKind(
                self.source_kind.clone(),
            ));
        }
        if self.license != GOVERNED_ASSET_KIT_LICENSE {
            return Err(GovernedAssetKitError::InvalidLicense(self.license.clone()));
        }
        if self.cadle_content_used {
            return Err(GovernedAssetKitError::CadleContentForbidden);
        }
        if self.commercial_game_content_used {
            return Err(GovernedAssetKitError::CommercialGameContentForbidden);
        }
        Ok(())
    }
}

/// One deterministic world-space placement from the runtime preview document.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GovernedAssetKitPlacement {
    pub id: String,
    pub asset_id: String,
    /// World-space X/Z coordinates in metres. Y is sampled from the linked terrain.
    pub xz_m: [f32; 2],
    /// Signed vertical offset from the sampled terrain surface, in metres.
    pub surface_offset_m: f32,
    pub yaw_degrees: f32,
    pub scale: f32,
}

/// Closed, engine-neutral input used to materialize and compare the R1-064 runtime composition.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GovernedAssetKitRuntimePreview {
    pub schema: String,
    pub schema_version: u32,
    pub asset_kit_id: String,
    pub asset_kit_revision: u32,
    pub source_world_sketch_sha256: ContentHash,
    pub source_terrain_sha256: ContentHash,
    pub input_manifest_sha256: ContentHash,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub authored_source_sha256: Option<ContentHash>,
    pub camera_id: String,
    /// Complete list of governed asset IDs expected to be loaded by the preview.
    pub assets: Vec<String>,
    pub placements: Vec<GovernedAssetKitPlacement>,
}

impl GovernedAssetKitRuntimePreview {
    pub fn from_json(json: &str) -> Result<Self, GovernedAssetKitError> {
        let preview: Self = serde_json::from_str(json)?;
        preview.validate()?;
        Ok(preview)
    }

    pub fn validate(&self) -> Result<(), GovernedAssetKitError> {
        validate_schema(
            &self.schema,
            self.schema_version,
            GOVERNED_ASSET_KIT_PREVIEW_SCHEMA,
            GOVERNED_ASSET_KIT_PREVIEW_SCHEMA_VERSION,
        )?;
        validate_asset_kit_identity(&self.asset_kit_id, self.asset_kit_revision)?;
        if !valid_identifier(&self.camera_id) {
            return Err(GovernedAssetKitError::InvalidCameraId(
                self.camera_id.clone(),
            ));
        }
        if self.assets.is_empty() || self.assets.len() > MAX_GOVERNED_ASSET_KIT_ASSETS {
            return Err(GovernedAssetKitError::InvalidAssetCount(self.assets.len()));
        }
        if self.placements.is_empty() || self.placements.len() > MAX_GOVERNED_ASSET_KIT_PLACEMENTS {
            return Err(GovernedAssetKitError::InvalidPlacementCount(
                self.placements.len(),
            ));
        }

        let mut asset_ids = HashSet::with_capacity(self.assets.len());
        for asset_id in &self.assets {
            if !valid_identifier(asset_id) {
                return Err(GovernedAssetKitError::InvalidAssetId(asset_id.clone()));
            }
            if !asset_ids.insert(asset_id.as_str()) {
                return Err(GovernedAssetKitError::DuplicateAssetId(asset_id.clone()));
            }
        }

        let mut placement_ids = HashSet::with_capacity(self.placements.len());
        let mut placed_asset_ids = HashSet::with_capacity(self.assets.len());
        for placement in &self.placements {
            if !valid_identifier(&placement.id) {
                return Err(GovernedAssetKitError::InvalidPlacementId(
                    placement.id.clone(),
                ));
            }
            if !placement_ids.insert(placement.id.as_str()) {
                return Err(GovernedAssetKitError::DuplicatePlacementId(
                    placement.id.clone(),
                ));
            }
            if !asset_ids.contains(placement.asset_id.as_str()) {
                return Err(GovernedAssetKitError::UnknownPlacementAsset {
                    placement_id: placement.id.clone(),
                    asset_id: placement.asset_id.clone(),
                });
            }
            placement.validate()?;
            placed_asset_ids.insert(placement.asset_id.as_str());
        }
        if let Some(asset_id) = asset_ids.difference(&placed_asset_ids).next() {
            return Err(GovernedAssetKitError::PreviewAssetNotPlaced(
                (*asset_id).to_owned(),
            ));
        }
        Ok(())
    }

    /// Verifies that a preview consumes the complete output of one validated Blender build.
    pub fn validate_against_build_report(
        &self,
        report: &GovernedAssetKitBuildReport,
    ) -> Result<(), GovernedAssetKitError> {
        self.validate()?;
        report.validate()?;
        if self.authored_source_sha256
            != report
                .authored_source
                .as_ref()
                .map(|source| source.descriptor_sha256)
        {
            return Err(GovernedAssetKitError::AuthoredSourceMismatch);
        }
        if self.asset_kit_id != report.asset_kit_id {
            return Err(GovernedAssetKitError::AssetKitIdMismatch {
                preview: self.asset_kit_id.clone(),
                build: report.asset_kit_id.clone(),
            });
        }
        if self.asset_kit_revision != report.asset_kit_revision {
            return Err(GovernedAssetKitError::AssetKitRevisionMismatch {
                preview: self.asset_kit_revision,
                build: report.asset_kit_revision,
            });
        }
        if self.input_manifest_sha256 != report.generator.input_manifest_sha256 {
            return Err(GovernedAssetKitError::InputManifestHashMismatch {
                preview: self.input_manifest_sha256,
                build: report.generator.input_manifest_sha256,
            });
        }

        let preview_assets: HashSet<_> = self.assets.iter().map(String::as_str).collect();
        let build_assets: HashSet<_> = report
            .assets
            .iter()
            .map(|asset| asset.id.as_str())
            .collect();
        if let Some(asset_id) = build_assets.difference(&preview_assets).next() {
            return Err(GovernedAssetKitError::BuildAssetMissingFromPreview(
                (*asset_id).to_owned(),
            ));
        }
        if let Some(asset_id) = preview_assets.difference(&build_assets).next() {
            return Err(GovernedAssetKitError::PreviewAssetMissingFromBuild(
                (*asset_id).to_owned(),
            ));
        }
        Ok(())
    }
}

impl GovernedAssetKitPlacement {
    pub fn validate(&self) -> Result<(), GovernedAssetKitError> {
        let valid_xz = self.xz_m.iter().all(|value| {
            value.is_finite() && value.abs() <= MAX_GOVERNED_ASSET_KIT_WORLD_COORDINATE_M
        });
        let valid_surface_offset = self.surface_offset_m.is_finite()
            && self.surface_offset_m.abs() <= MAX_GOVERNED_ASSET_KIT_SURFACE_OFFSET_M;
        let valid_yaw = self.yaw_degrees.is_finite()
            && self.yaw_degrees.abs() <= MAX_GOVERNED_ASSET_KIT_YAW_DEGREES;
        let valid_scale = self.scale.is_finite()
            && (MIN_GOVERNED_ASSET_KIT_SCALE..=MAX_GOVERNED_ASSET_KIT_SCALE).contains(&self.scale);
        if !valid_identifier(&self.asset_id)
            || !valid_xz
            || !valid_surface_offset
            || !valid_yaw
            || !valid_scale
        {
            return Err(GovernedAssetKitError::InvalidPlacement(self.id.clone()));
        }
        Ok(())
    }
}

fn validate_report_assets(assets: &[GovernedAssetKitAsset]) -> Result<(), GovernedAssetKitError> {
    if assets.is_empty() || assets.len() > MAX_GOVERNED_ASSET_KIT_ASSETS {
        return Err(GovernedAssetKitError::InvalidAssetCount(assets.len()));
    }
    let mut ids = HashSet::with_capacity(assets.len());
    let mut glb_paths = HashSet::with_capacity(assets.len());
    for asset in assets {
        if !valid_identifier(&asset.id) {
            return Err(GovernedAssetKitError::InvalidAssetId(asset.id.clone()));
        }
        if !ids.insert(asset.id.as_str()) {
            return Err(GovernedAssetKitError::DuplicateAssetId(asset.id.clone()));
        }
        if !matches!(asset.kind.as_str(), "rock" | "tree" | "architecture") {
            return Err(GovernedAssetKitError::InvalidAssetKind {
                asset_id: asset.id.clone(),
                kind: asset.kind.clone(),
            });
        }
        if !valid_relative_glb_path(&asset.glb.path) {
            return Err(GovernedAssetKitError::InvalidGlbPath {
                asset_id: asset.id.clone(),
                path: asset.glb.path.clone(),
            });
        }
        if !glb_paths.insert(asset.glb.path.as_str()) {
            return Err(GovernedAssetKitError::DuplicateGlbPath(
                asset.glb.path.clone(),
            ));
        }
    }
    Ok(())
}

fn validate_schema(
    schema: &str,
    version: u32,
    expected_schema: &'static str,
    expected_version: u32,
) -> Result<(), GovernedAssetKitError> {
    if schema != expected_schema {
        return Err(GovernedAssetKitError::InvalidSchema {
            expected: expected_schema,
            actual: schema.to_owned(),
        });
    }
    if version != expected_version {
        return Err(GovernedAssetKitError::UnsupportedVersion {
            schema: expected_schema,
            version,
        });
    }
    Ok(())
}

fn validate_asset_kit_identity(id: &str, revision: u32) -> Result<(), GovernedAssetKitError> {
    if !valid_identifier(id) {
        return Err(GovernedAssetKitError::InvalidAssetKitId(id.to_owned()));
    }
    if revision == 0 {
        return Err(GovernedAssetKitError::InvalidAssetKitRevision(revision));
    }
    Ok(())
}

fn valid_identifier(value: &str) -> bool {
    !value.is_empty()
        && value.trim() == value
        && value.len() <= 128
        && !value.chars().any(char::is_control)
}

fn valid_relative_glb_path(value: &str) -> bool {
    let path = Path::new(value);
    if value.is_empty()
        || value.trim() != value
        || !path
            .extension()
            .is_some_and(|extension| extension.eq_ignore_ascii_case("glb"))
        || value.contains(['\\', ':'])
        || value.chars().any(char::is_control)
    {
        return false;
    }
    !path.is_absolute()
        && path
            .components()
            .all(|component| matches!(component, Component::Normal(_)))
        && value
            .split('/')
            .all(|component| !component.is_empty() && component != "." && component != "..")
}

#[derive(Debug, Error)]
pub enum GovernedAssetKitError {
    #[error("authored asset source lineage is missing, inconsistent or unsupported")]
    InvalidAuthoredSource,
    #[error("runtime preview does not pin the authored source descriptor of this build")]
    AuthoredSourceMismatch,
    #[error("invalid schema `{actual}`, expected `{expected}`")]
    InvalidSchema {
        expected: &'static str,
        actual: String,
    },
    #[error("unsupported `{schema}` schema version {version}")]
    UnsupportedVersion { schema: &'static str, version: u32 },
    #[error("invalid governed asset-kit id `{0}`")]
    InvalidAssetKitId(String),
    #[error("governed asset-kit revision must be greater than zero, found {0}")]
    InvalidAssetKitRevision(u32),
    #[error(
        "governed asset kit must contain 1..={MAX_GOVERNED_ASSET_KIT_ASSETS} assets, found {0}"
    )]
    InvalidAssetCount(usize),
    #[error("invalid governed asset id `{0}`")]
    InvalidAssetId(String),
    #[error("duplicate governed asset id `{0}`")]
    DuplicateAssetId(String),
    #[error("asset `{asset_id}` has unsupported kind `{kind}`")]
    InvalidAssetKind { asset_id: String, kind: String },
    #[error("asset `{asset_id}` has invalid relative GLB path `{path}`")]
    InvalidGlbPath { asset_id: String, path: String },
    #[error("duplicate governed GLB path `{0}`")]
    DuplicateGlbPath(String),
    #[error("unsupported governed asset source kind `{0}`")]
    InvalidSourceKind(String),
    #[error("unsupported governed asset license `{0}`")]
    InvalidLicense(String),
    #[error("Cadle content is forbidden in governed Forge asset kits")]
    CadleContentForbidden,
    #[error("commercial-game content is forbidden in governed Forge asset kits")]
    CommercialGameContentForbidden,
    #[error("the Blender build did not validate its technical export")]
    TechnicalExportNotValidated,
    #[error("invalid runtime-preview camera id `{0}`")]
    InvalidCameraId(String),
    #[error(
        "runtime preview must contain 1..={MAX_GOVERNED_ASSET_KIT_PLACEMENTS} placements, found {0}"
    )]
    InvalidPlacementCount(usize),
    #[error("invalid governed placement id `{0}`")]
    InvalidPlacementId(String),
    #[error("duplicate governed placement id `{0}`")]
    DuplicatePlacementId(String),
    #[error("placement `{placement_id}` references unknown asset `{asset_id}`")]
    UnknownPlacementAsset {
        placement_id: String,
        asset_id: String,
    },
    #[error("runtime preview asset `{0}` has no placement")]
    PreviewAssetNotPlaced(String),
    #[error("placement `{0}` contains an invalid or out-of-bounds transform")]
    InvalidPlacement(String),
    #[error("runtime preview manifest hash {preview} does not match Blender build hash {build}")]
    InputManifestHashMismatch {
        preview: ContentHash,
        build: ContentHash,
    },
    #[error("runtime preview asset-kit id `{preview}` does not match Blender build id `{build}`")]
    AssetKitIdMismatch { preview: String, build: String },
    #[error(
        "runtime preview asset-kit revision {preview} does not match Blender build revision {build}"
    )]
    AssetKitRevisionMismatch { preview: u32, build: u32 },
    #[error("Blender build asset `{0}` is missing from the runtime preview")]
    BuildAssetMissingFromPreview(String),
    #[error("runtime preview asset `{0}` is missing from the Blender build")]
    PreviewAssetMissingFromBuild(String),
    #[error("invalid governed asset-kit JSON: {0}")]
    Json(#[from] serde_json::Error),
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::{Value, json};

    const HASH_A: &str = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const HASH_B: &str = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    fn build_json() -> Value {
        json!({
            "schema": GOVERNED_ASSET_KIT_BUILD_SCHEMA,
            "schema_version": GOVERNED_ASSET_KIT_BUILD_SCHEMA_VERSION,
            "asset_kit_id": "visual-valley-r1-063",
            "asset_kit_revision": 3,
            "generator": {
                "input_manifest_sha256": HASH_A,
                "blender_version": "4.5.10",
                "script": "Build-VisualValleyAssetKit.py"
            },
            "provenance": {
                "author": "OpenRPG Forge project",
                "source_kind": GOVERNED_ASSET_KIT_SOURCE_KIND,
                "source_uri": null,
                "license": GOVERNED_ASSET_KIT_LICENSE,
                "distribution_status": "internal_gate_proof_only",
                "cadle_content_used": false,
                "commercial_game_content_used": false,
                "human_publication_review_required": true
            },
            "qualification": {
                "technical_export_validated": true,
                "human_art_review": "pending",
                "publication_approved": false
            },
            "assets": [
                {
                    "id": "vv-rock-moss-a",
                    "kind": "rock",
                    "dimensions_m": [3.8, 2.6, 2.2],
                    "glb": {
                        "path": "assets/vv-rock-moss-a.glb",
                        "sha256": HASH_B,
                        "bytes": 42
                    }
                },
                {
                    "id": "vv-tree-oak-a",
                    "kind": "tree",
                    "glb": {
                        "path": "assets/vv-tree-oak-a.glb",
                        "sha256": HASH_A
                    }
                }
            ],
            "textures": [{"path": "textures/example.png"}],
            "limitations": ["human review remains required"]
        })
    }

    fn preview_json() -> Value {
        json!({
            "schema": GOVERNED_ASSET_KIT_PREVIEW_SCHEMA,
            "schema_version": GOVERNED_ASSET_KIT_PREVIEW_SCHEMA_VERSION,
            "asset_kit_id": "visual-valley-r1-063",
            "asset_kit_revision": 3,
            "source_world_sketch_sha256": HASH_A,
            "source_terrain_sha256": HASH_B,
            "input_manifest_sha256": HASH_A,
            "camera_id": "heroic_view",
            "assets": ["vv-rock-moss-a", "vv-tree-oak-a"],
            "placements": [
                {
                    "id": "hero-rock",
                    "asset_id": "vv-rock-moss-a",
                    "xz_m": [24.0, -18.0],
                    "surface_offset_m": 0.1,
                    "yaw_degrees": 35.0,
                    "scale": 1.25
                },
                {
                    "id": "hero-oak",
                    "asset_id": "vv-tree-oak-a",
                    "xz_m": [-8.0, 12.0],
                    "surface_offset_m": 0.0,
                    "yaw_degrees": -20.0,
                    "scale": 0.9
                }
            ]
        })
    }

    #[test]
    fn blender_report_accepts_unmodelled_evidence_fields() {
        let report = GovernedAssetKitBuildReport::from_json(&build_json().to_string()).unwrap();

        assert_eq!(report.assets.len(), 2);
        assert_eq!(report.assets[0].kind, "rock");
        assert_eq!(
            report.generator.input_manifest_sha256,
            ContentHash::parse(HASH_A).unwrap()
        );
    }

    fn authored_build_json() -> Value {
        let mut report = build_json();
        report["provenance"]["source_kind"] = json!(GOVERNED_ASSET_KIT_AUTHORED_SOURCE_KIND);
        report["authored_source"] = json!({
            "descriptor_sha256": HASH_B, "blend_sha256": HASH_B,
            "base_manifest_sha256": HASH_A, "assets": ["vv-rock-moss-a"],
            "human_review": "pending"
        });
        report
    }

    #[test]
    fn authored_build_requires_explicit_preview_pin_and_round_trips() {
        let report =
            GovernedAssetKitBuildReport::from_json(&authored_build_json().to_string()).unwrap();
        let mut preview =
            GovernedAssetKitRuntimePreview::from_json(&preview_json().to_string()).unwrap();
        assert!(matches!(
            preview.validate_against_build_report(&report),
            Err(GovernedAssetKitError::AuthoredSourceMismatch)
        ));
        preview.authored_source_sha256 = Some(ContentHash::parse(HASH_B).unwrap());
        preview.validate_against_build_report(&report).unwrap();
        assert_eq!(
            report,
            GovernedAssetKitBuildReport::from_json(&serde_json::to_string(&report).unwrap())
                .unwrap()
        );
        assert_eq!(
            preview,
            GovernedAssetKitRuntimePreview::from_json(&serde_json::to_string(&preview).unwrap())
                .unwrap()
        );
        preview.authored_source_sha256 = Some(ContentHash::parse(HASH_A).unwrap());
        assert!(matches!(
            preview.validate_against_build_report(&report),
            Err(GovernedAssetKitError::AuthoredSourceMismatch)
        ));
    }

    #[test]
    fn authored_lineage_rejects_missing_stale_unknown_and_duplicate_sources() {
        for (field, value) in [
            ("base_manifest_sha256", json!(HASH_B)),
            ("assets", json!([])),
            ("assets", json!(["missing"])),
            ("assets", json!(["vv-rock-moss-a", "vv-rock-moss-a"])),
            ("human_review", json!("approved")),
        ] {
            let mut report = authored_build_json();
            report["authored_source"][field] = value;
            assert!(matches!(
                GovernedAssetKitBuildReport::from_json(&report.to_string()),
                Err(GovernedAssetKitError::InvalidAuthoredSource)
            ));
        }
        let mut report = authored_build_json();
        report["authored_source"] = Value::Null;
        assert!(matches!(
            GovernedAssetKitBuildReport::from_json(&report.to_string()),
            Err(GovernedAssetKitError::InvalidAuthoredSource)
        ));
        let mut report = authored_build_json();
        report["provenance"]["source_kind"] = json!(GOVERNED_ASSET_KIT_SOURCE_KIND);
        assert!(matches!(
            GovernedAssetKitBuildReport::from_json(&report.to_string()),
            Err(GovernedAssetKitError::InvalidAuthoredSource)
        ));
    }

    #[test]
    fn report_rejects_unsafe_provenance_and_unqualified_exports() {
        for field in ["cadle_content_used", "commercial_game_content_used"] {
            let mut candidate = build_json();
            candidate["provenance"][field] = json!(true);
            let error = GovernedAssetKitBuildReport::from_json(&candidate.to_string()).unwrap_err();
            assert!(matches!(
                error,
                GovernedAssetKitError::CadleContentForbidden
                    | GovernedAssetKitError::CommercialGameContentForbidden
            ));
        }

        let mut candidate = build_json();
        candidate["qualification"]["technical_export_validated"] = json!(false);
        assert!(matches!(
            GovernedAssetKitBuildReport::from_json(&candidate.to_string()),
            Err(GovernedAssetKitError::TechnicalExportNotValidated)
        ));
    }

    #[test]
    fn documents_reject_invalid_asset_kit_identity() {
        let mut report = build_json();
        report["asset_kit_revision"] = json!(0);
        assert!(matches!(
            GovernedAssetKitBuildReport::from_json(&report.to_string()),
            Err(GovernedAssetKitError::InvalidAssetKitRevision(0))
        ));

        let mut preview = preview_json();
        preview["asset_kit_id"] = json!(" ");
        assert!(matches!(
            GovernedAssetKitRuntimePreview::from_json(&preview.to_string()),
            Err(GovernedAssetKitError::InvalidAssetKitId(_))
        ));
    }

    #[test]
    fn report_rejects_invalid_license_source_kind_and_kind() {
        let mut candidate = build_json();
        candidate["provenance"]["license"] = json!("CC-BY-4.0");
        assert!(matches!(
            GovernedAssetKitBuildReport::from_json(&candidate.to_string()),
            Err(GovernedAssetKitError::InvalidLicense(_))
        ));

        let mut candidate = build_json();
        candidate["provenance"]["source_kind"] = json!("external");
        assert!(matches!(
            GovernedAssetKitBuildReport::from_json(&candidate.to_string()),
            Err(GovernedAssetKitError::InvalidSourceKind(_))
        ));

        let mut candidate = build_json();
        candidate["assets"][0]["kind"] = json!("vehicle");
        assert!(matches!(
            GovernedAssetKitBuildReport::from_json(&candidate.to_string()),
            Err(GovernedAssetKitError::InvalidAssetKind { .. })
        ));
    }

    #[test]
    fn report_rejects_duplicate_ids_and_non_portable_glb_paths() {
        let mut duplicate = build_json();
        duplicate["assets"][1]["id"] = json!("vv-rock-moss-a");
        assert!(matches!(
            GovernedAssetKitBuildReport::from_json(&duplicate.to_string()),
            Err(GovernedAssetKitError::DuplicateAssetId(_))
        ));

        for path in [
            "../escape.glb",
            "assets/../escape.glb",
            "assets\\escape.glb",
            "/absolute/escape.glb",
            "C:/absolute/escape.glb",
            "assets/not-a-glb.gltf",
        ] {
            let mut candidate = build_json();
            candidate["assets"][0]["glb"]["path"] = json!(path);
            assert!(matches!(
                GovernedAssetKitBuildReport::from_json(&candidate.to_string()),
                Err(GovernedAssetKitError::InvalidGlbPath { .. })
            ));
        }
    }

    #[test]
    fn hashes_are_content_hashes_at_deserialization_boundary() {
        let mut candidate = build_json();
        candidate["assets"][0]["glb"]["sha256"] = json!("not-a-sha256");
        assert!(matches!(
            GovernedAssetKitBuildReport::from_json(&candidate.to_string()),
            Err(GovernedAssetKitError::Json(_))
        ));
    }

    #[test]
    fn runtime_preview_is_closed_and_validates_asset_references() {
        let preview =
            GovernedAssetKitRuntimePreview::from_json(&preview_json().to_string()).unwrap();
        assert_eq!(preview.placements.len(), 2);

        let mut unknown_field = preview_json();
        unknown_field["runtime_entity_count"] = json!(2);
        assert!(matches!(
            GovernedAssetKitRuntimePreview::from_json(&unknown_field.to_string()),
            Err(GovernedAssetKitError::Json(_))
        ));

        let mut unknown_nested_field = preview_json();
        unknown_nested_field["placements"][0]["y_m"] = json!(12.0);
        assert!(matches!(
            GovernedAssetKitRuntimePreview::from_json(&unknown_nested_field.to_string()),
            Err(GovernedAssetKitError::Json(_))
        ));

        let mut unknown_asset = preview_json();
        unknown_asset["placements"][0]["asset_id"] = json!("missing-asset");
        assert!(matches!(
            GovernedAssetKitRuntimePreview::from_json(&unknown_asset.to_string()),
            Err(GovernedAssetKitError::UnknownPlacementAsset { .. })
        ));

        let mut unused_asset = preview_json();
        unused_asset["placements"].as_array_mut().unwrap().pop();
        assert!(matches!(
            GovernedAssetKitRuntimePreview::from_json(&unused_asset.to_string()),
            Err(GovernedAssetKitError::PreviewAssetNotPlaced(_))
        ));
    }

    #[test]
    fn runtime_preview_rejects_duplicate_and_out_of_bounds_placements() {
        let mut duplicate = preview_json();
        duplicate["placements"][1]["id"] = json!("hero-rock");
        assert!(matches!(
            GovernedAssetKitRuntimePreview::from_json(&duplicate.to_string()),
            Err(GovernedAssetKitError::DuplicatePlacementId(_))
        ));

        let mut out_of_bounds = preview_json();
        out_of_bounds["placements"][0]["xz_m"][0] =
            json!(MAX_GOVERNED_ASSET_KIT_WORLD_COORDINATE_M + 1.0);
        assert!(matches!(
            GovernedAssetKitRuntimePreview::from_json(&out_of_bounds.to_string()),
            Err(GovernedAssetKitError::InvalidPlacement(_))
        ));

        let mut preview = GovernedAssetKitRuntimePreview::from_json(&preview_json().to_string())
            .expect("fixture must be valid");
        preview.placements[0].scale = f32::NAN;
        assert!(matches!(
            preview.validate(),
            Err(GovernedAssetKitError::InvalidPlacement(_))
        ));
    }

    #[test]
    fn runtime_preview_must_cover_the_exact_build_asset_set() {
        let report = GovernedAssetKitBuildReport::from_json(&build_json().to_string()).unwrap();
        let preview =
            GovernedAssetKitRuntimePreview::from_json(&preview_json().to_string()).unwrap();
        preview.validate_against_build_report(&report).unwrap();

        let mut missing = preview.clone();
        missing.assets.pop();
        missing.placements.pop();
        assert!(matches!(
            missing.validate_against_build_report(&report),
            Err(GovernedAssetKitError::BuildAssetMissingFromPreview(_))
        ));

        let mut wrong_hash = preview.clone();
        wrong_hash.input_manifest_sha256 = ContentHash::parse(HASH_B).unwrap();
        assert!(matches!(
            wrong_hash.validate_against_build_report(&report),
            Err(GovernedAssetKitError::InputManifestHashMismatch { .. })
        ));

        let mut wrong_revision = preview.clone();
        wrong_revision.asset_kit_revision += 1;
        assert!(matches!(
            wrong_revision.validate_against_build_report(&report),
            Err(GovernedAssetKitError::AssetKitRevisionMismatch { .. })
        ));

        let mut wrong_id = preview;
        wrong_id.asset_kit_id = "another-kit".to_owned();
        assert!(matches!(
            wrong_id.validate_against_build_report(&report),
            Err(GovernedAssetKitError::AssetKitIdMismatch { .. })
        ));
    }
}
