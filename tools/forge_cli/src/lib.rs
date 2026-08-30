#![deny(unsafe_code)]

//! Testable operations behind the Forge command-line interface.

use forge_core::{AreaId, ContentHash, ProjectId, Revision};
use forge_project::{SceneProofDocument, SceneProofError};
use forge_world::ProofSceneRecipe;
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
    #[error("cannot generate the Gate 0 proof document: {0}")]
    Generate(String),
    #[error(transparent)]
    InvalidDocument(#[from] SceneProofError),
}
