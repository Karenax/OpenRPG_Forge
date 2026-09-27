//! Compare native parameters with independently executed pinned OceanFFT.js.
use forge_engine::ocean_spectrum::{OceanFrame, OceanSpectrum};
use serde::Deserialize;
use std::{collections::BTreeMap, fs, path::PathBuf};
#[derive(Deserialize)]
struct Case {
    options: OceanSpectrum,
    file: String,
}
#[derive(Deserialize)]
struct Snapshot {
    frame: u32,
    time: f64,
    prefix: String,
}
#[derive(Deserialize)]
struct Oracle {
    schema: String,
    version: u32,
    source_commit: String,
    files: BTreeMap<String, String>,
    parameter_cases: Vec<Case>,
    snapshots: Vec<Snapshot>,
    #[serde(default)]
    simulation_options: OceanSpectrum,
    #[serde(default)]
    simulation_frame: OceanFrame,
}
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let root = PathBuf::from(
        std::env::args_os()
            .nth(1)
            .ok_or("oracle directory required")?,
    );
    let oracle: Oracle = serde_json::from_slice(&fs::read(root.join("ocean.json"))?)?;
    assert_eq!(oracle.schema, "forge.tidewater.ocean-probe");
    assert_eq!(oracle.version, 1);
    assert_eq!(
        oracle.source_commit,
        "1438b1abfcaee3267092b75573014f4d9b4a983c"
    );
    let compare = |file: &str, bytes: [u8; 224]| {
        assert!(!file.contains(['/', '\\']) && !file.contains(".."));
        let expected = fs::read(root.join(file)).unwrap();
        assert_eq!(
            forge_core::bytes_sha256(&expected).to_string(),
            oracle.files[file]
        );
        assert_eq!(bytes.as_slice(), expected, "{file}");
    };
    compare(
        "params-initial.bin",
        oracle
            .simulation_options
            .uniform_bytes(oracle.simulation_frame)?,
    );
    for case in &oracle.parameter_cases {
        compare(
            &case.file,
            case.options.uniform_bytes(OceanFrame::default())?,
        );
    }
    let mut time = 0.0;
    for frame in 1..=120 {
        time += 1.0 / 60.0;
        if let Some(snapshot) = oracle.snapshots.iter().find(|s| s.frame == frame) {
            // JSON's parser may round the binary64 diagnostic by one ULP.
            // The packed GPU time below is still compared byte for byte.
            assert!((time - snapshot.time).abs() <= 4.0 * f64::EPSILON);
            compare(
                &format!("{}-params.bin", snapshot.prefix),
                oracle.simulation_options.uniform_bytes(OceanFrame {
                    time,
                    ..oracle.simulation_frame
                })?,
            );
        }
    }
    for bad in [f64::NAN, f64::INFINITY, -1.0] {
        assert!(
            OceanSpectrum::default()
                .uniform_bytes(OceanFrame {
                    time: bad,
                    ..Default::default()
                })
                .is_err()
        );
    }
    for count in [0, 5] {
        assert!(
            OceanSpectrum {
                cascades: count,
                ..Default::default()
            }
            .uniform_bytes(OceanFrame::default())
            .is_err()
        );
    }
    assert!(
        OceanSpectrum {
            sizes: vec![7.0, 33.0, 157.0, 733.0],
            ..Default::default()
        }
        .uniform_bytes(OceanFrame::default())
        .is_err()
    );
    println!(
        "PASS: {} parameter blocks byte-exact; timeline and six invalid-input guards",
        1 + oracle.parameter_cases.len() + oracle.snapshots.len()
    );
    Ok(())
}
