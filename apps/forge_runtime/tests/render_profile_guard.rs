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
