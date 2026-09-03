#![deny(unsafe_code)]
#![cfg(feature = "render-profile")]

use std::{
    path::PathBuf,
    process::Command,
    time::{SystemTime, UNIX_EPOCH},
};

#[test]
fn profiled_binary_refuses_gate1_routes_before_creating_outputs() {
    for route in ["--gate1-gameplay-stutter", "--gate1-visual-proof"] {
        let output_root = unique_absent_output_root(route.trim_start_matches('-'));
        let output = Command::new(env!("CARGO_BIN_EXE_forge_runtime"))
            .args(["--visual-valley", "--quality", "high", "--no-vsync", route])
            .arg(&output_root)
            .output()
            .unwrap();

        assert_eq!(output.status.code(), Some(2));
        assert!(
            String::from_utf8_lossy(&output.stderr)
                .contains("NONQUALIFYING render-profile builds refuse Gate 1 qualification routes")
        );
        assert!(
            !output_root.exists(),
            "profiled route created forbidden output directory {}",
            output_root.display()
        );
    }
}

#[test]
fn profiled_binary_refuses_structured_log_probe() {
    let output = Command::new(env!("CARGO_BIN_EXE_forge_runtime"))
        .arg("--log-probe")
        .output()
        .unwrap();

    assert_eq!(output.status.code(), Some(2));
    assert!(
        String::from_utf8_lossy(&output.stderr)
            .contains("NONQUALIFYING render-profile builds refuse --log-probe")
    );
}

#[test]
fn profiled_gate1_diagnostic_opt_in_requires_the_gameplay_stutter_route() {
    let output = Command::new(env!("CARGO_BIN_EXE_forge_runtime"))
        .arg("--render-profile-gate1-diagnostic")
        .output()
        .unwrap();

    assert_eq!(output.status.code(), Some(2));
    assert!(
        String::from_utf8_lossy(&output.stderr)
            .contains("--render-profile-gate1-diagnostic requires --gate1-gameplay-stutter")
    );
}

#[test]
fn profiled_gate1_diagnostic_opt_in_never_allows_visual_proof_or_log_probe() {
    let visual_output_root = unique_absent_output_root("visual-proof-opt-in");
    let visual = Command::new(env!("CARGO_BIN_EXE_forge_runtime"))
        .args([
            "--visual-valley",
            "--quality",
            "high",
            "--no-vsync",
            "--gate1-visual-proof",
        ])
        .arg(&visual_output_root)
        .arg("--render-profile-gate1-diagnostic")
        .output()
        .unwrap();

    assert_eq!(visual.status.code(), Some(2));
    assert!(String::from_utf8_lossy(&visual.stderr).contains(
        "--render-profile-gate1-diagnostic cannot be combined with Gate 1 qualification capture routes"
    ));
    assert!(
        !visual_output_root.exists(),
        "profiled visual-proof route created forbidden output directory {}",
        visual_output_root.display()
    );

    let renderdoc_output_root = unique_absent_output_root("renderdoc-opt-in");
    let renderdoc = Command::new(env!("CARGO_BIN_EXE_forge_runtime"))
        .arg("--gate1-renderdoc-capture")
        .arg(&renderdoc_output_root)
        .arg("--render-profile-gate1-diagnostic")
        .output()
        .unwrap();
    assert_eq!(renderdoc.status.code(), Some(2));
    assert!(String::from_utf8_lossy(&renderdoc.stderr).contains(
        "--render-profile-gate1-diagnostic cannot be combined with Gate 1 qualification capture routes"
    ));
    assert!(
        !renderdoc_output_root.exists(),
        "profiled RenderDoc route created forbidden output directory {}",
        renderdoc_output_root.display()
    );

    let log = Command::new(env!("CARGO_BIN_EXE_forge_runtime"))
        .args(["--log-probe", "--render-profile-gate1-diagnostic"])
        .output()
        .unwrap();
    assert_eq!(log.status.code(), Some(2));
    assert!(
        String::from_utf8_lossy(&log.stderr)
            .contains("--render-profile-gate1-diagnostic cannot be combined with --log-probe")
    );
}

fn unique_absent_output_root(label: &str) -> PathBuf {
    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    std::env::temp_dir().join(format!(
        "forge-render-profile-{label}-{}-{nonce}",
        std::process::id()
    ))
}
