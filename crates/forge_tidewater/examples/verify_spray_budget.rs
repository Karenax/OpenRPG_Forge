//! Compare the native controller against acquired source head feedback and budgets.
use forge_tidewater::spray_budget::{HeadSample, SprayBudget};
use serde_json::Value;
use std::{error::Error, fs, io::Write, path::PathBuf};

#[allow(clippy::cast_possible_truncation)] // Comparing the actual f32 GPU uniform representation.
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.len() != 2 {
        return Err("SPRAY_EXPORT NEW_REPORT required".into());
    }
    let root = PathBuf::from(&args[0]);
    let output = PathBuf::from(&args[1]);
    if output.exists() {
        return Err("report exists".into());
    }
    let manifest = fs::read(root.join("spray.json"))?;
    let meta: Value = serde_json::from_slice(&manifest)?;
    if meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || meta["capacities"]["gpu"] != 32768
    {
        return Err("source or capacity mismatch".into());
    }
    let bytes = fs::read(root.join("history-head.u32.bin"))?;
    if bytes.len() != 540 * 4
        || meta["files"]["history-head.u32.bin"] != forge_core::bytes_sha256(&bytes).to_string()
    {
        return Err("head feedback integrity".into());
    }
    let heads: Vec<_> = bytes
        .as_chunks::<4>()
        .0
        .iter()
        .map(|v| u32::from_le_bytes(*v))
        .collect();
    let schedule = meta["schedule"].as_array().ok_or("schedule missing")?;
    if schedule.len() != 540 {
        return Err("schedule size".into());
    }
    let mut budget = SprayBudget::new(32768)?;
    let mut time = 0.0_f64;
    let mut previous_time = 0.0_f64;
    let mut max_error = 0.0_f64;
    let mut rows = Vec::new();
    for (i, row) in schedule.iter().enumerate() {
        time += 1.0 / 60.0;
        if row["frame"].as_u64() != Some(u64::try_from(i + 1)?)
            || (row["time"].as_f64().ok_or("time")? - time).abs() > 1e-12
        {
            return Err("source simulation clock mismatch".into());
        }
        let emitter = budget.value();
        budget.after_emission(if i == 0 {
            None
        } else {
            Some(HeadSample {
                time: previous_time,
                head: heads[i - 1],
            })
        })?;
        previous_time = time;
        for (name, value) in [("emitter_budget", emitter), ("budget", budget.value())] {
            let expected = row[name].as_f64().ok_or("budget missing")?;
            max_error = max_error.max((expected - value).abs());
            if (expected as f32).to_bits() != (value as f32).to_bits()
                || (expected - value).abs() > 1e-12
            {
                return Err(format!(
                    "native budget mismatch at frame {} {name}: {value} versus {expected}",
                    i + 1
                )
                .into());
            }
        }
        rows.push(
            serde_json::json!({"frame":i+1,"emitter_budget":emitter,"budget":budget.value()}),
        );
    }
    let report = serde_json::json!({"schema":"forge.tidewater.spray-budget-verification","version":1,"source_manifest_sha256":forge_core::bytes_sha256(&manifest).to_string(),"steps":540,"max_f64_error":max_error,"all_gpu_f32_budgets_exact":true,"schedule":rows,"limits":["source head counters supplied as paired feedback","one-step readback latency matching acquired source run","native GPU feedback connection not covered"]});
    fs::OpenOptions::new()
        .create_new(true)
        .write(true)
        .open(output)?
        .write_all(&serde_json::to_vec_pretty(&report)?)?;
    println!("540 steps, exact f32 budgets, maximum f64 error {max_error}");
    Ok(())
}
