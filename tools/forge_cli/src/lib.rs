#![deny(unsafe_code)]

//! Testable operations behind the Forge command-line interface.

use forge_core::{AreaId, ContentHash, ProjectId, Revision};
use forge_project::{SceneProofDocument, SceneProofError};
use forge_world::{
    ProofSceneRecipe, WorldSketchCompilationError, WorldSketchDocument, WorldSketchError,
    compile_world_sketch,
};
use sha2::{Digest, Sha256};
use std::fs::{self, OpenOptions};
use std::io::{self, Write};
use std::path::{Path, PathBuf};
use thiserror::Error;

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct ValidationReport {
    pub schema_version: u32,
    pub entity_count: usize,
    pub revision: u64,
    pub content_hash: ContentHash,
}

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct WorldSketchValidationReport {
    pub schema_version: u32,
    pub revision: u64,
    pub source_image_count: usize,
    pub semantic_region_count: usize,
    pub spline_count: usize,
    pub landmark_count: usize,
    pub validation_camera_count: usize,
    pub content_hash: ContentHash,
}

#[derive(Clone, Debug, Eq, PartialEq)]
pub struct WorldSketchCompileReport {
    pub output_directory: PathBuf,
    pub terrain_path: PathBuf,
    pub carved_terrain_path: PathBuf,
    pub vegetation_path: PathBuf,
    pub semantic_masks_path: PathBuf,
    pub spline_carving_path: PathBuf,
    pub traversal_audit_path: PathBuf,
    pub bridge_placements_path: PathBuf,
    pub blender_input_path: PathBuf,
    pub report_path: PathBuf,
    pub world_sketch_hash: ContentHash,
    pub terrain_hash: ContentHash,
    pub carved_terrain_hash: ContentHash,
    pub vegetation_hash: ContentHash,
    pub semantic_masks_hash: ContentHash,
    pub spline_carving_hash: ContentHash,
    pub traversal_audit_hash: ContentHash,
    pub bridge_placements_hash: ContentHash,
    pub blender_input_hash: ContentHash,
    pub validated_pending_output_count: usize,
}

pub fn read_scene_proof(path: impl AsRef<Path>) -> Result<SceneProofDocument, CliError> {
    let path = path.as_ref();
    let json = fs::read_to_string(path).map_err(|source| CliError::Read {
        path: path.to_path_buf(),
        source,
    })?;
    Ok(SceneProofDocument::from_json(&json)?)
}

pub fn validate_file(path: impl AsRef<Path>) -> Result<ValidationReport, CliError> {
    let document = read_scene_proof(path)?;
    Ok(ValidationReport {
        schema_version: document.schema_version,
        entity_count: document.entities.len(),
        revision: document.revision.get(),
        content_hash: document.content_hash()?,
    })
}

pub fn hash_file(path: impl AsRef<Path>) -> Result<ContentHash, CliError> {
    Ok(read_scene_proof(path)?.content_hash()?)
}

pub fn read_world_sketch(path: impl AsRef<Path>) -> Result<WorldSketchDocument, CliError> {
    let path = path.as_ref();
    let json = fs::read_to_string(path).map_err(|source| CliError::Read {
        path: path.to_path_buf(),
        source,
    })?;
    Ok(WorldSketchDocument::from_json(&json)?)
}

pub fn validate_world_sketch_file(
    path: impl AsRef<Path>,
    source_root: impl AsRef<Path>,
) -> Result<WorldSketchValidationReport, CliError> {
    let document = read_world_sketch(path)?;
    verify_world_sketch_sources(&document, source_root.as_ref())?;
    Ok(WorldSketchValidationReport {
        schema_version: document.schema_version,
        revision: document.revision.get(),
        source_image_count: document.source_images.len(),
        semantic_region_count: document.semantic_regions.len(),
        spline_count: document.splines.len(),
        landmark_count: document.landmarks.len(),
        validation_camera_count: document.validation_cameras.len(),
        content_hash: document.content_hash()?,
    })
}

pub fn compile_world_sketch_file(
    path: impl AsRef<Path>,
    source_root: impl AsRef<Path>,
    output_directory: impl AsRef<Path>,
) -> Result<WorldSketchCompileReport, CliError> {
    let document = read_world_sketch(path)?;
    verify_world_sketch_sources(&document, source_root.as_ref())?;
    let compiled = compile_world_sketch(&document)?;

    let terrain_json = compiled.terrain.to_pretty_json()? + "\n";
    let carved_terrain_json = compiled.carved_terrain.to_pretty_json()? + "\n";
    let vegetation_json = compiled.vegetation.to_pretty_json()? + "\n";
    let semantic_masks_json = compiled.semantic_masks.to_pretty_json()? + "\n";
    let spline_carving_json = compiled.spline_carving.to_pretty_json()? + "\n";
    let traversal_audit_json = compiled.traversal_audit.to_pretty_json()? + "\n";
    let bridge_placements_json = compiled.bridge_placements.to_pretty_json()? + "\n";
    let blender_input_json = compiled.blender_input.to_pretty_json()? + "\n";
    let report_json = compiled.report.to_pretty_json()? + "\n";

    let output_directory = output_directory.as_ref();
    fs::create_dir(output_directory).map_err(|source| CliError::CreateOutputDirectory {
        path: output_directory.to_path_buf(),
        source,
    })?;
    let terrain_path = output_directory.join("terrain.v1.json");
    let carved_terrain_path = output_directory.join("terrain-carved.v2.json");
    let vegetation_path = output_directory.join("vegetation.v1.json");
    let semantic_masks_path = output_directory.join("semantic-masks.v1.json");
    let spline_carving_path = output_directory.join("spline-carving.v1.json");
    let traversal_audit_path = output_directory.join("traversal-audit.v2.json");
    let bridge_placements_path = output_directory.join("bridge-placements.v1.json");
    let blender_input_path = output_directory.join("blender-input.v4.json");
    let report_path = output_directory.join("world-sketch.compilation.v4.json");
    write_new_text(&terrain_path, &terrain_json)?;
    write_new_text(&carved_terrain_path, &carved_terrain_json)?;
    write_new_text(&vegetation_path, &vegetation_json)?;
    write_new_text(&semantic_masks_path, &semantic_masks_json)?;
    write_new_text(&spline_carving_path, &spline_carving_json)?;
    write_new_text(&traversal_audit_path, &traversal_audit_json)?;
    write_new_text(&bridge_placements_path, &bridge_placements_json)?;
    write_new_text(&blender_input_path, &blender_input_json)?;
    write_new_text(&report_path, &report_json)?;

    Ok(WorldSketchCompileReport {
        output_directory: output_directory.to_path_buf(),
        terrain_path,
        carved_terrain_path,
        vegetation_path,
        semantic_masks_path,
        spline_carving_path,
        traversal_audit_path,
        bridge_placements_path,
        blender_input_path,
        report_path,
        world_sketch_hash: compiled.report.source_world_sketch_sha256,
        terrain_hash: compiled.report.terrain_sha256,
        carved_terrain_hash: compiled.report.carved_terrain_sha256,
        vegetation_hash: compiled.report.vegetation_sha256,
        semantic_masks_hash: compiled.report.semantic_masks_sha256,
        spline_carving_hash: compiled.report.spline_carving_sha256,
        traversal_audit_hash: compiled.report.traversal_audit_sha256,
        bridge_placements_hash: compiled.report.bridge_placements_sha256,
        blender_input_hash: compiled.report.blender_input_sha256,
        validated_pending_output_count: compiled.report.validated_pending_outputs.len(),
    })
}

fn verify_world_sketch_sources(
    document: &WorldSketchDocument,
    source_root: &Path,
) -> Result<(), CliError> {
    for source_image in &document.source_images {
        let path = source_root.join(&source_image.path);
        let bytes = fs::read(&path).map_err(|source| CliError::ReadSourceImage {
            path: path.clone(),
            source,
        })?;
        let actual_hash = ContentHash::from_bytes(Sha256::digest(&bytes).into());
        if actual_hash != source_image.sha256 {
            return Err(CliError::SourceHashMismatch {
                path,
                expected: source_image.sha256,
                actual: actual_hash,
            });
        }
        if source_image.path.to_ascii_lowercase().ends_with(".png") {
            let (width, height) = png_dimensions(&bytes)
                .ok_or_else(|| CliError::InvalidPngHeader { path: path.clone() })?;
            if width != source_image.pixel_width || height != source_image.pixel_height {
                return Err(CliError::SourceDimensionsMismatch {
                    path,
                    expected_width: source_image.pixel_width,
                    expected_height: source_image.pixel_height,
                    actual_width: width,
                    actual_height: height,
                });
            }
        }
    }
    Ok(())
}

fn png_dimensions(bytes: &[u8]) -> Option<(u32, u32)> {
    const PNG_SIGNATURE: &[u8; 8] = b"\x89PNG\r\n\x1a\n";
    if bytes.len() < 24 || &bytes[..8] != PNG_SIGNATURE || &bytes[12..16] != b"IHDR" {
        return None;
    }
    let width = u32::from_be_bytes(bytes[16..20].try_into().ok()?);
    let height = u32::from_be_bytes(bytes[20..24].try_into().ok()?);
    Some((width, height))
}

fn write_new_text(path: &Path, contents: &str) -> Result<(), CliError> {
    let mut file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(|source| CliError::Write {
            path: path.to_path_buf(),
            source,
        })?;
    file.write_all(contents.as_bytes())
        .map_err(|source| CliError::Write {
            path: path.to_path_buf(),
            source,
        })
}

pub fn default_proof_document() -> Result<SceneProofDocument, CliError> {
    ProofSceneRecipe::default()
        .build(
            ProjectId::deterministic("gate-0-project"),
            AreaId::deterministic("gate-0-area"),
            Revision::INITIAL,
        )
        .map_err(|error| CliError::Generate(error.to_string()))
}

/// Writes the canonical Gate 0 document without replacing an existing file.
pub fn write_default_proof(path: impl AsRef<Path>) -> Result<ValidationReport, CliError> {
    let path = path.as_ref();
    let document = default_proof_document()?;
    let json = document.to_pretty_json()?;
    let mut file = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(|source| CliError::Write {
            path: path.to_path_buf(),
            source,
        })?;
    file.write_all(json.as_bytes())
        .and_then(|()| file.write_all(b"\n"))
        .map_err(|source| CliError::Write {
            path: path.to_path_buf(),
            source,
        })?;
    validate_file(path)
}

#[derive(Debug, Error)]
pub enum CliError {
    #[error("cannot read `{}`: {source}", path.display())]
    Read {
        path: PathBuf,
        #[source]
        source: io::Error,
    },
    #[error("cannot write `{}`: {source}", path.display())]
    Write {
        path: PathBuf,
        #[source]
        source: io::Error,
    },
    #[error("cannot read WorldSketch source image `{}`: {source}", path.display())]
    ReadSourceImage {
        path: PathBuf,
        #[source]
        source: io::Error,
    },
    #[error(
        "WorldSketch source image `{}` hash mismatch: expected {expected}, got {actual}",
        path.display()
    )]
    SourceHashMismatch {
        path: PathBuf,
        expected: ContentHash,
        actual: ContentHash,
    },
    #[error("WorldSketch source image `{}` has an invalid PNG header", path.display())]
    InvalidPngHeader { path: PathBuf },
    #[error(
        "WorldSketch source image `{}` dimensions mismatch: expected {expected_width}x{expected_height}, got {actual_width}x{actual_height}",
        path.display()
    )]
    SourceDimensionsMismatch {
        path: PathBuf,
        expected_width: u32,
        expected_height: u32,
        actual_width: u32,
        actual_height: u32,
    },
    #[error("cannot create new WorldSketch output directory `{}`: {source}", path.display())]
    CreateOutputDirectory {
        path: PathBuf,
        #[source]
        source: io::Error,
    },
    #[error("cannot generate the Gate 0 proof document: {0}")]
    Generate(String),
    #[error(transparent)]
    InvalidDocument(#[from] SceneProofError),
    #[error(transparent)]
    InvalidWorldSketch(#[from] WorldSketchError),
    #[error(transparent)]
    CompileWorldSketch(#[from] WorldSketchCompilationError),
    #[error(transparent)]
    InvalidBridgePlacements(#[from] forge_world::WorldSketchBridgePlacementError),
    #[error(transparent)]
    InvalidTerrain(#[from] forge_project::TerrainDocumentError),
    #[error(transparent)]
    InvalidVegetation(#[from] forge_project::VegetationDocumentError),
}

#[cfg(test)]
mod tests {
    use super::*;

    fn repository_root() -> PathBuf {
        PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../..")
    }

    fn world_sketch_path() -> PathBuf {
        repository_root().join("examples/visual_valley/world-sketch.v1.json")
    }

    #[test]
    fn visual_valley_world_sketch_sources_and_metadata_validate() {
        let root = repository_root();
        let report = validate_world_sketch_file(world_sketch_path(), &root).unwrap();
        assert_eq!(report.schema_version, 1);
        assert_eq!(report.revision, 3);
        assert_eq!(report.source_image_count, 2);
        assert_eq!(report.spline_count, 3);
        assert_eq!(report.landmark_count, 5);
        assert_eq!(report.validation_camera_count, 4);
    }

    #[test]
    fn compile_world_sketch_writes_nine_new_valid_outputs_without_overwrite() {
        let root = repository_root();
        let output =
            std::env::temp_dir().join(format!("openrpg-forge-world-sketch-{}", std::process::id()));
        if output.exists() {
            fs::remove_dir_all(&output).unwrap();
        }

        let report = compile_world_sketch_file(world_sketch_path(), &root, &output).unwrap();
        assert!(report.terrain_path.is_file());
        assert!(report.carved_terrain_path.is_file());
        assert!(report.vegetation_path.is_file());
        assert!(report.semantic_masks_path.is_file());
        assert!(report.spline_carving_path.is_file());
        assert!(report.traversal_audit_path.is_file());
        assert!(report.bridge_placements_path.is_file());
        assert!(report.blender_input_path.is_file());
        assert!(report.report_path.is_file());
        assert_eq!(report.validated_pending_output_count, 4);
        assert!(matches!(
            compile_world_sketch_file(world_sketch_path(), &root, &output),
            Err(CliError::CreateOutputDirectory { .. })
        ));

        fs::remove_dir_all(output).unwrap();
    }

    #[test]
    fn png_dimensions_reads_the_ihdr_without_an_image_dependency() {
        let root = repository_root();
        let bytes =
            fs::read(root.join("examples/visual_valley/references/visual-valley-topdown-v1.png"))
                .unwrap();
        assert_eq!(png_dimensions(&bytes), Some((1254, 1254)));
    }
}
