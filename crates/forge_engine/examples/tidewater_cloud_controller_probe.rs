//! Compare native CPU scheduling with independent original uniform acquisitions.
use forge_engine::cloud_controller::bytes;
#[path = "tidewater_cloud_weather_probe/controller.rs"]
mod controller;
use serde_json::Value;
use std::{error::Error, fs, path::PathBuf};

#[allow(clippy::many_single_char_names)]
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 2 {
        return Err("CLOUD_ACQUISITION NEW_OUTPUT".into());
    }
    let root = PathBuf::from(&args[0]);
    let out = PathBuf::from(&args[1]);
    if out.exists() {
        return Err("output exists".into());
    }
    let meta: Value = serde_json::from_slice(&fs::read(root.join(
        if root.join("controller.json").exists() {
            "controller.json"
        } else {
            "environment.json"
        },
    ))?)?;
    let generated = controller::generate(&meta)?;
    fs::create_dir_all(&out)?;
    let mut results = Vec::new();
    for step in 1..=generated.len() {
        let output = &generated[step - 1];
        for (name, data) in [
            ("frame", bytes(&output.frame)),
            ("settings", bytes(&output.settings)),
            ("public", bytes(&output.public)),
        ] {
            let file = format!("cloud-step{step:03}-{name}.bin");
            let source = fs::read(root.join(&file))?;
            if meta["files"][&file] != forge_core::bytes_sha256(&source).to_string() {
                return Err("source hash".into());
            }
            fs::write(out.join(&file), &data)?;
            results.push(serde_json::json!({"file":file,"exact":data==source}));
        }
    }
    fs::write(
        out.join("probe.json"),
        serde_json::to_vec_pretty(
            &serde_json::json!({"native_controller":true,"source_frames_used_as_inputs":false,"checks":results}),
        )?,
    )?;
    Ok(())
}
