#![deny(unsafe_code)]
#![cfg(not(feature = "render-profile"))]

use serde_json::Value;
use std::process::Command;

#[test]
fn runtime_emits_machine_parseable_typed_json_events() {
    let output = Command::new(env!("CARGO_BIN_EXE_forge_runtime"))
        .arg("--log-probe")
        .env("RUST_LOG", "forge=info")
        .output()
        .unwrap();

    assert!(
        output.status.success(),
        "log probe failed: {}",
        String::from_utf8_lossy(&output.stderr)
    );
    let stderr = String::from_utf8(output.stderr).unwrap();
    let events = stderr
        .lines()
        .filter(|line| !line.trim().is_empty())
        .map(|line| serde_json::from_str::<Value>(line).unwrap())
        .collect::<Vec<_>>();

    let probe = events
        .iter()
        .find(|event| event["event_kind"] == "structured_log_probe")
        .expect("structured log probe event");
    assert_eq!(probe["schema"], "forge.runtime-event");
    assert_eq!(probe["schema_version"].as_u64(), Some(1));
    assert_eq!(probe["probe_sequence"].as_u64(), Some(1));
    assert_eq!(probe["probe_ok"].as_bool(), Some(true));
    assert_eq!(probe["target"], "forge::runtime");

    let stopped = events
        .iter()
        .find(|event| event["event_kind"] == "runtime_stopped")
        .expect("clean shutdown event");
    assert_eq!(stopped["success"].as_bool(), Some(true));
    assert_eq!(stopped["exit_code"].as_u64(), Some(0));
}
