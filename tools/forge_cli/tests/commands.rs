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

fn repository_root() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../..")
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

#[test]
fn world_sketch_commands_verify_sources_and_compile_bounded_outputs() {
    let root = repository_root();
    let document = root.join("examples/visual_valley/world-sketch.v1.json");
    let directory = temporary_directory();
    let output_directory = directory.join("compiled");

    let validate = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args([
            "validate-world-sketch",
            document.to_str().unwrap(),
            "--source-root",
            root.to_str().unwrap(),
        ])
        .output()
        .unwrap();
    assert!(validate.status.success());
    assert!(
        String::from_utf8(validate.stdout)
            .unwrap()
            .starts_with("VALID_WORLD_SKETCH schema_version=1 revision=3 sources=2")
    );

    let compile = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args([
            "compile-world-sketch",
            document.to_str().unwrap(),
            output_directory.to_str().unwrap(),
            "--source-root",
            root.to_str().unwrap(),
        ])
        .output()
        .unwrap();
    assert!(compile.status.success());
    assert!(output_directory.join("terrain.v1.json").is_file());
    assert!(output_directory.join("terrain-carved.v2.json").is_file());
    assert!(output_directory.join("vegetation.v1.json").is_file());
    assert!(output_directory.join("semantic-masks.v1.json").is_file());
    assert!(output_directory.join("spline-carving.v1.json").is_file());
    assert!(output_directory.join("traversal-audit.v2.json").is_file());
    assert!(output_directory.join("bridge-placements.v1.json").is_file());
    assert!(output_directory.join("blender-input.v4.json").is_file());
    assert!(
        output_directory
            .join("world-sketch.compilation.v4.json")
            .is_file()
    );

    let second = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args([
            "compile-world-sketch",
            document.to_str().unwrap(),
            output_directory.to_str().unwrap(),
            "--source-root",
            root.to_str().unwrap(),
        ])
        .output()
        .unwrap();
    assert_eq!(second.status.code(), Some(2));

    fs::remove_dir_all(directory).unwrap();
}

#[test]
fn aurora_scenario_runs_the_m3_fixture_and_writes_a_report() {
    let directory = temporary_directory();
    let bundle = repository_root()
        .join("crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1");
    let report_path = directory.join("m3-report.json");

    let output = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args([
            "aurora-scenario",
            bundle.to_str().unwrap(),
            "--report",
            report_path.to_str().unwrap(),
        ])
        .output()
        .unwrap();
    assert!(
        output.status.success(),
        "{}",
        String::from_utf8_lossy(&output.stderr)
    );
    let stdout = String::from_utf8(output.stdout).unwrap();
    assert!(stdout.starts_with("AURORA_SCENARIO module="));
    assert!(stdout.contains("quest=completed"));
    assert!(stdout.contains("journal=[10, 20, 30]"));
    assert!(stdout.contains("resume_idempotent=true"));

    let report: serde_json::Value =
        serde_json::from_slice(&fs::read(&report_path).unwrap()).unwrap();
    assert_eq!(report["schema"], "forge.aurora-m3-report@1");
    assert_eq!(report["questStatus"], "completed");
    assert_eq!(report["gold"], 50);
    assert_eq!(report["unsupported"].as_array().unwrap().len(), 0);

    fs::remove_dir_all(directory).unwrap();
}

#[test]
fn aurora_gameplay_runs_the_m4_fixture_and_writes_a_report() {
    let directory = temporary_directory();
    let bundle = repository_root()
        .join("crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1");
    let report_path = directory.join("m4-report.json");

    let output = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args([
            "aurora-gameplay",
            bundle.to_str().unwrap(),
            "--seed",
            "2026",
            "--profile",
            repository_root()
                .join("docs/aurora-bridge/nwn-rules-profile.v1.json")
                .to_str()
                .unwrap(),
            "--report",
            report_path.to_str().unwrap(),
        ])
        .output()
        .unwrap();
    assert!(
        output.status.success(),
        "{}",
        String::from_utf8_lossy(&output.stderr)
    );
    let stdout = String::from_utf8(output.stdout).unwrap();
    assert!(stdout.starts_with("AURORA_GAMEPLAY module="));
    assert!(stdout.contains("quest=completed"));
    assert!(stdout.contains("oracle=not-run"));
    assert!(stdout.contains("resume_idempotent=true"));

    let report: serde_json::Value =
        serde_json::from_slice(&fs::read(&report_path).unwrap()).unwrap();
    assert_eq!(report["schema"], "forge.aurora-m4-report@1");
    assert_eq!(report["questStatus"], "completed");
    assert_eq!(report["guardianHp"], 0);
    assert!(report["rounds"].as_u64().unwrap() > 0);
    assert!(report["unsupported"].as_array().unwrap().len() >= 4);

    fs::remove_dir_all(directory).unwrap();
}

#[test]
fn aurora_m5_writes_a_trace_and_diff_oracle_detects_a_mutation() {
    let directory = temporary_directory();
    let bundle = repository_root()
        .join("crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1");
    let report_path = directory.join("m5-report.json");
    let trace_path = directory.join("m5-trace.json");

    let output = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args([
            "aurora-m5",
            bundle.to_str().unwrap(),
            "--seed",
            "2026",
            "--report",
            report_path.to_str().unwrap(),
            "--trace",
            trace_path.to_str().unwrap(),
        ])
        .output()
        .unwrap();
    assert!(
        output.status.success(),
        "{}",
        String::from_utf8_lossy(&output.stderr)
    );
    let stdout = String::from_utf8(output.stdout).unwrap();
    assert!(stdout.starts_with("AURORA_M5 module="));
    assert!(stdout.contains("trace_entries=54"));
    assert!(stdout.contains("accepted=true"));
    assert!(stdout.contains("resume_idempotent=true"));

    let report: serde_json::Value =
        serde_json::from_slice(&fs::read(&report_path).unwrap()).unwrap();
    assert_eq!(report["schema"], "forge.aurora-m5-report@1");
    assert_eq!(report["migration"]["accepted"], true);
    assert_eq!(report["resumeIdempotent"], true);
    let trace: serde_json::Value = serde_json::from_slice(&fs::read(&trace_path).unwrap()).unwrap();
    assert_eq!(trace["schema"], "forge.aurora-m5-trace@1");
    assert_eq!(trace["redacted"], true);
    assert_eq!(trace["entries"].as_array().unwrap().len(), 54);

    let same = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args([
            "aurora-diff-oracle",
            report_path.to_str().unwrap(),
            report_path.to_str().unwrap(),
        ])
        .output()
        .unwrap();
    assert!(same.status.success());
    assert!(
        String::from_utf8(same.stdout)
            .unwrap()
            .contains("matches=true")
    );

    let mut mutated = report;
    mutated["m4"]["gold"] = serde_json::json!(49);
    let mutated_path = directory.join("m5-mutated.json");
    fs::write(&mutated_path, serde_json::to_vec_pretty(&mutated).unwrap()).unwrap();
    let diff_path = directory.join("m5-diff.json");
    let different = Command::new(env!("CARGO_BIN_EXE_forge_cli"))
        .args([
            "aurora-diff-oracle",
            report_path.to_str().unwrap(),
            mutated_path.to_str().unwrap(),
            "--report",
            diff_path.to_str().unwrap(),
        ])
        .output()
        .unwrap();
    assert_eq!(different.status.code(), Some(2));
    let diff: serde_json::Value = serde_json::from_slice(&fs::read(&diff_path).unwrap()).unwrap();
    assert_eq!(diff["matches"], false);
    assert!(
        diff["divergences"]
            .as_array()
            .unwrap()
            .iter()
            .any(|item| item["path"] == "gold")
    );

    fs::remove_dir_all(directory).unwrap();
}
