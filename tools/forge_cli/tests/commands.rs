#![deny(unsafe_code)]

use forge_core::{AreaId, ProjectId, Revision};
use forge_world::ProofSceneRecipe;
use std::fs;
use std::path::PathBuf;
use std::process::Command;
use std::time::{SystemTime, UNIX_EPOCH};

fn temporary_directory() -> PathBuf {
    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    let path = std::env::temp_dir().join(format!("forge-cli-test-{}-{nonce}", std::process::id()));
    fs::create_dir_all(&path).unwrap();
    path
}

#[test]
fn validate_and_hash_commands_accept_the_procedural_proof() {
    let directory = temporary_directory();
    let document_path = directory.join("proof.scene.json");
    let document = ProofSceneRecipe::default()
        .build(
            ProjectId::deterministic("cli-project"),
            AreaId::deterministic("cli-area"),
            Revision::INITIAL,
        )
        .unwrap();
    fs::write(&document_path, document.to_pretty_json().unwrap()).unwrap();

    let validate = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args(["validate", document_path.to_str().unwrap()])
        .output()
        .unwrap();
    assert!(validate.status.success());
    let validate_stdout = String::from_utf8(validate.stdout).unwrap();
    assert!(validate_stdout.starts_with("VALID schema_version=1 revision=1 entities=4 sha256="));

    let hash = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args(["hash", document_path.to_str().unwrap()])
        .output()
        .unwrap();
    assert!(hash.status.success());
    let hash_stdout = String::from_utf8(hash.stdout).unwrap();
    assert_eq!(
        hash_stdout.trim(),
        document.content_hash().unwrap().to_string()
    );

    fs::remove_dir_all(directory).unwrap();
}

#[test]
fn invalid_document_returns_a_nonzero_exit_code() {
    let directory = temporary_directory();
    let document_path = directory.join("invalid.scene.json");
    fs::write(
        &document_path,
        r#"{"schema":"forge.scene-proof","schema_version":999}"#,
    )
    .unwrap();

    let output = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args(["validate", document_path.to_str().unwrap()])
        .output()
        .unwrap();
    assert_eq!(output.status.code(), Some(2));
    assert!(
        String::from_utf8(output.stderr)
            .unwrap()
            .starts_with("ERROR invalid scene-proof JSON:")
    );

    fs::remove_dir_all(directory).unwrap();
}

#[test]
fn generate_proof_is_deterministic_and_refuses_overwrite() {
    let directory = temporary_directory();
    let document_path = directory.join("generated.scene.json");
    let first = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args(["generate-proof", document_path.to_str().unwrap()])
        .output()
        .unwrap();
    assert!(first.status.success());
    let document =
        forge_project::SceneProofDocument::from_json(&fs::read_to_string(&document_path).unwrap())
            .unwrap();
    assert_eq!(
        document.content_hash().unwrap().to_string(),
        "0ea53d539067f399e7e76678532144955f35e5a15e95d11becf3a174843c67df"
    );

    let second = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args(["generate-proof", document_path.to_str().unwrap()])
        .output()
        .unwrap();
    assert_eq!(second.status.code(), Some(2));
    fs::remove_dir_all(directory).unwrap();
}
