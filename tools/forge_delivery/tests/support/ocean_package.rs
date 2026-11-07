//! Small structural FFT input fixture. Shaders are placeholders, not render proof.
use forge_core::bytes_sha256;
use serde_json::json;
use std::collections::BTreeMap;

pub fn files() -> BTreeMap<String, Vec<u8>> {
    let mut files = BTreeMap::new();
    let mut frame = vec![0; 720];
    frame[584..588].copy_from_slice(&(1.0_f32 / 60.0).to_le_bytes());
    files.insert("frame-120-frame.bin".into(), frame);
    files.insert("frame-120-params.bin".into(), vec![0; 224]);
    files.insert("params-initial.bin".into(), vec![0; 224]);
    files.insert("LICENSE-Tidewater.txt".into(), b"MIT License".to_vec());
    for name in [
        "initSpectrum",
        "conjugate",
        "copyH0",
        "row",
        "column",
        "mipA-0",
        "mipA-1",
        "mipB-0",
        "mipB-1",
    ] {
        files.insert(format!("{name}.wgsl"), b"@compute fn main() {}".to_vec());
    }
    let pins: BTreeMap<_, _> = files
        .iter()
        .filter(|(name, _)| name.as_str() != "LICENSE-Tidewater.txt")
        .map(|(name, bytes)| (name.clone(), bytes_sha256(bytes).to_string()))
        .collect();
    files.insert(
        "ocean.json".into(),
        serde_json::to_vec(&json!({
            "schema":"forge.tidewater.ocean-probe", "version":1,
            "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c",
            "fft_size":256,"cascades":4,"files":pins
        }))
        .unwrap(),
    );
    files
}

pub fn index(files: &BTreeMap<String, Vec<u8>>) -> Vec<u8> {
    let entries: BTreeMap<_, _> = files
        .iter()
        .map(|(name, bytes)| {
            (
                name.clone(),
                json!({"sha256":bytes_sha256(bytes), "bytes":bytes.len()}),
            )
        })
        .collect();
    serde_json::to_vec(
        &json!({"schema":"forge.tidewater.ocean-input-package", "version":1,
        "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c",
        "source_bundle_sha256":bytes_sha256(b"synthetic source"),"files":entries}),
    )
    .unwrap()
}
