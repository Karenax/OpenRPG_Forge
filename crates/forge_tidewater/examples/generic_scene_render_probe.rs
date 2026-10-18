//! Native GPU proof of common optical effects without imported assets.
#[path = "support/generic_optical_probe.rs"]
mod harness;
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let output = std::env::args().nth(1).ok_or("Output folder required")?;
    std::fs::create_dir(&output)?;
    let mut result = bevy::tasks::block_on(harness::run(Some(&output)))?;
    let files = result["images"]
        .as_object()
        .ok_or("Readback images absent")?
        .keys()
        .map(|key| format!("{key}.png"))
        .collect::<Vec<_>>();
    for name in &files {
        if std::fs::metadata(std::path::Path::new(&output).join(name))?.len() == 0 {
            return Err(format!("Empty screenshot: {name}").into());
        }
    }
    result
        .as_object_mut()
        .ok_or("Proof object absent")?
        .remove("images");
    result["capture_files"] = serde_json::to_value(files)?;
    std::fs::write(
        std::path::Path::new(&output).join("proof.json"),
        serde_json::to_vec_pretty(&result)?,
    )?;
    if result["passed"] != true {
        return Err(format!("GPU comparisons failed: {}", result["failure"]).into());
    }
    Ok(())
}
