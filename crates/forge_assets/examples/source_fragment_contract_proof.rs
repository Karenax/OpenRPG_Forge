//! Local proof of imported source equations; input stays outside the repository.
use forge_assets::source_fragment::SourceFragmentContract;
use serde::Deserialize;

#[derive(Deserialize)]
struct Proof {
    rows: Vec<Row>,
}
#[derive(Deserialize)]
struct Row {
    contract: SourceFragmentContract,
    source_ir_probes: Vec<Probe>,
}
#[derive(Deserialize)]
struct Probe {
    linear_base: [f64; 3],
    packed: [f64; 4],
    normal_rgb: [f64; 3],
    expected: Expected,
}
#[derive(Deserialize)]
struct Expected {
    linear_rgb: [f64; 3],
    metallic: f64,
    occlusion: f64,
    perceptual_roughness: f64,
    normal_ts: [f64; 3],
}
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let path = std::env::args()
        .nth(1)
        .ok_or("source proof JSON path required")?;
    let proof: Proof = serde_json::from_slice(&std::fs::read(path)?)?;
    if proof.rows.is_empty() {
        return Err("empty source proof is not a pass".into());
    }
    let mut count = 0_usize;
    let mut maximum = 0.0_f64;
    for row in &proof.rows {
        row.contract.validate()?;
        if row.source_ir_probes.is_empty() {
            return Err("material has no source IR probe".into());
        }
        for probe in &row.source_ir_probes {
            let v = row
                .contract
                .evaluate(probe.linear_base, probe.packed, probe.normal_rgb);
            let actual = v
                .linear_rgb
                .into_iter()
                .chain([v.metallic, v.occlusion, v.perceptual_roughness])
                .chain(v.normal_ts);
            let expected = probe
                .expected
                .linear_rgb
                .into_iter()
                .chain([
                    probe.expected.metallic,
                    probe.expected.occlusion,
                    probe.expected.perceptual_roughness,
                ])
                .chain(probe.expected.normal_ts);
            for (a, b) in actual.zip(expected) {
                let error = (a - b).abs();
                if !error.is_finite() || error > 1e-12 {
                    return Err(format!("source IR/Rust equation mismatch {a} versus {b}").into());
                }
                maximum = maximum.max(error);
            }
            count += 1;
        }
    }
    println!(
        "{}",
        serde_json::json!({"materials_verified":proof.rows.len(),"source_ir_probes":count,"maximum_error":maximum,"source_signed_values_preserved":true,"runtime_qualified":false,"models_qualified":0})
    );
    Ok(())
}
