//! Static FFT comparison from the canonical acquired input package.
use super::{Input, NAMES};
use forge_tidewater::tidewater_ocean::PreparedOcean;
use forge_tidewater_assets::tidewater_ocean::OceanPackage;
use std::{error::Error, path::PathBuf, sync::Arc};

/// Authenticated source callback chronology, distinct from native GPU head data.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum CallbackOrder {
    LegacyMetadata,
    Unavailable,
    Completed(u32),
}

#[derive(Clone)]
pub struct Step {
    pub params: [u8; 224],
    pub global: [u8; 720],
    pub global_time: f64,
    /// The source observation controls which completed native head may be consumed.
    pub feedback_order: CallbackOrder,
}

fn feedback_order(history: &[serde_json::Value]) -> Result<Vec<CallbackOrder>, Box<dyn Error>> {
    if history
        .iter()
        .all(|row| row.get("spray_budget_observation").is_none())
    {
        return Ok(vec![CallbackOrder::LegacyMetadata; history.len()]);
    }
    let mut previous = None;
    history
        .iter()
        .enumerate()
        .map(|(index, row)| {
            let step = u64::try_from(index + 1)?;
            let observation = row
                .get("spray_budget_observation")
                .ok_or("Incomplete acquired spray callback observations")?;
            let global_time = row["after"]["global_time_s"]
                .as_f64()
                .ok_or("Acquired spray transaction clock missing")?;
            if observation["gpu_frame"].as_u64() != Some(step)
                || row["after"]["gpu_frame"].as_u64() != Some(step)
                || !global_time.is_finite()
                || global_time < 0.0
                || observation["global_time_s"].as_f64().map(f64::to_bits)
                    != Some(global_time.to_bits())
            {
                return Err("Acquired spray callback transaction mismatch".into());
            }
            let state = &observation["after"];
            let head = state
                .get("head")
                .ok_or("Acquired spray head observation missing")?;
            let time = state
                .get("sample_time_s")
                .ok_or("Acquired spray sample clock missing")?;
            if head.is_null() {
                if !time.is_null()
                    || state["readback_frame"]
                        .as_i64()
                        .is_some_and(|frame| frame >= 0)
                    || previous.is_some()
                {
                    return Err("Acquired missing callback is inconsistent".into());
                }
                return Ok(CallbackOrder::Unavailable);
            }
            let head = u32::try_from(head.as_u64().ok_or("Acquired spray head invalid")?)?;
            let frame = state["readback_frame"]
                .as_u64()
                .ok_or("Acquired spray callback frame missing")?;
            if frame == 0 || frame >= step {
                return Err("Acquired spray callback is not a preceding submission".into());
            }
            let time = time
                .as_f64()
                .ok_or("Acquired spray callback time invalid")?;
            let expected = history[usize::try_from(frame - 1)?]["after"]["global_time_s"]
                .as_f64()
                .ok_or("Acquired callback submission clock missing")?;
            if !time.is_finite() || time < 0.0 || time.to_bits() != expected.to_bits() {
                return Err("Acquired spray callback clock differs from its submission".into());
            }
            let frame = u32::try_from(frame)?;
            if let Some((previous_frame, previous_time, previous_head)) = previous
                && (frame < previous_frame
                    || (frame == previous_frame
                        && (time.to_bits() != previous_time || head != previous_head)))
            {
                return Err("Acquired spray callback order is inconsistent".into());
            }
            previous = Some((frame, time.to_bits(), head));
            Ok(CallbackOrder::Completed(frame))
        })
        .collect()
}

pub(super) fn load() -> Result<Option<Input>, Box<dyn Error>> {
    let root = std::env::var_os("FORGE_TIDEWATER_STATIC_OCEAN_PACKAGE");
    let pin = std::env::var("FORGE_TIDEWATER_STATIC_OCEAN_SHA256").ok();
    let (root, pin) = match (root, pin) {
        (None, None) => return Ok(None),
        (Some(root), Some(pin)) => (PathBuf::from(root).canonicalize()?, pin),
        _ => return Err("Static ocean requires package path and manifest hash".into()),
    };
    if std::env::var("FORGE_TIDEWATER_STATIC_SOURCE_FRAME").as_deref() != Ok("1") {
        return Err("Acquired ocean requires an acquired static comparison".into());
    }
    let expected: forge_core::ContentHash = pin.parse()?;
    let manifest = std::fs::read(root.join("package.json"))?;
    let package = OceanPackage::load(&manifest, expected, |name, size| {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if !path.starts_with(&root)
            || path.metadata().map_err(|e| e.to_string())?.len() != size as u64
        {
            return Err("Static ocean payload path or size mismatch".into());
        }
        std::fs::read(path).map_err(|e| e.to_string())
    })?;
    if !package.has_acquired_frames() {
        return Err("Static ocean requires explicit acquired frames".into());
    }
    // The same validation used by the resident Studio renderer must pass first.
    let _validated = PreparedOcean::new(&package)?;
    let file = |name: &str| package.file(name).ok_or("Acquired ocean input missing");
    let params = file("params-initial.bin")?.try_into()?;
    let frames = u32::try_from(package.bootstrap_frames())?;
    let reference_path = PathBuf::from(
        std::env::var_os("FORGE_TIDEWATER_STATIC_REFERENCE_METADATA")
            .ok_or("Acquired ocean requires reference metadata for its global clock")?,
    );
    let reference_pin: forge_core::ContentHash =
        std::env::var("FORGE_TIDEWATER_STATIC_REFERENCE_SHA256")?.parse()?;
    if reference_path.metadata()?.len() > 2 * 1024 * 1024 {
        return Err("Acquired reference metadata size limit".into());
    }
    let reference_bytes = std::fs::read(reference_path)?;
    if forge_core::bytes_sha256(&reference_bytes) != reference_pin {
        return Err("Acquired reference metadata pin mismatch".into());
    }
    let reference: serde_json::Value = serde_json::from_slice(&reference_bytes)?;
    if reference["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c" {
        return Err("Acquired reference commit mismatch".into());
    }
    let history = reference["frame_history"]
        .as_array()
        .ok_or("Acquired reference history missing")?;
    if history.len() != frames as usize {
        return Err("Acquired reference frame count mismatch".into());
    }
    let feedback_order = feedback_order(history)?;
    let steps = (1..=frames)
        .map(|step| {
            let global: [u8; 720] = file(&format!("ocean-step{step:03}-frame.bin"))?.try_into()?;
            let global_time = history[step as usize - 1]["after"]["global_time_s"]
                .as_f64()
                .ok_or("Acquired reference global time missing")?;
            if !global_time.is_finite() || global_time < 0.0 || global_time > f64::from(f32::MAX) {
                return Err("Acquired reference global time invalid".into());
            }
            #[allow(clippy::cast_possible_truncation)]
            if global[580..584] != (global_time as f32).to_le_bytes() {
                return Err("Acquired global clock differs from packed source frame".into());
            }
            Ok(Step {
                params: file(&format!("ocean-step{step:03}-params.bin"))?.try_into()?,
                global,
                global_time,
                feedback_order: feedback_order[step as usize - 1],
            })
        })
        .collect::<Result<Vec<_>, Box<dyn Error>>>()?;
    let shaders = NAMES
        .into_iter()
        .map(|name| Ok(std::str::from_utf8(file(&format!("{name}.wgsl"))?)?.to_owned()))
        .collect::<Result<Vec<_>, Box<dyn Error>>>()?;
    Ok(Some(Input {
        shaders,
        params,
        frames,
        acquired: Some(Arc::new(steps)),
        acquired_manifest: Some(expected.to_string()),
    }))
}

#[cfg(test)]
mod tests {
    use super::{CallbackOrder, feedback_order};
    use serde_json::{Value, json};

    fn history() -> Vec<Value> {
        (1..=4)
            .map(|step| {
                let frame = step - 1;
                json!({"after":{"gpu_frame":step,"global_time_s":f64::from(step)},
                    "spray_budget_observation":{"gpu_frame":step,"global_time_s":f64::from(step),
                        "after":{"head":if frame==0 {Value::Null} else {json!(frame)},
                            "sample_time_s":if frame==0 {Value::Null} else {json!(f64::from(frame))},
                            "readback_frame":if frame==0 {-1} else {frame}}}})
            })
            .collect()
    }

    #[test]
    fn acquired_callback_distinguishes_legacy_pending_and_completed_native_frames() {
        let legacy = vec![json!({"after":{"gpu_frame":1,"global_time_s":1.0}})];
        assert_eq!(
            feedback_order(&legacy).unwrap(),
            vec![CallbackOrder::LegacyMetadata]
        );
        let mut rows = history();
        rows[3]["spray_budget_observation"]["after"] =
            rows[2]["spray_budget_observation"]["after"].clone();
        assert_eq!(
            feedback_order(&rows).unwrap(),
            vec![
                CallbackOrder::Unavailable,
                CallbackOrder::Completed(1),
                CallbackOrder::Completed(2),
                CallbackOrder::Completed(2)
            ]
        );
    }

    #[test]
    fn acquired_callback_rejects_incomplete_future_wrong_clock_and_regressed_observations() {
        for mode in 0..8 {
            let mut rows = history();
            match mode {
                0 => {
                    rows[1]
                        .as_object_mut()
                        .unwrap()
                        .remove("spray_budget_observation");
                }
                1 => rows[1]["spray_budget_observation"]["after"]["readback_frame"] = json!(2),
                2 => rows[1]["spray_budget_observation"]["after"]["sample_time_s"] = json!(1.25),
                3 => rows[1]["spray_budget_observation"]["global_time_s"] = json!(2.5),
                4 => {
                    rows[3]["spray_budget_observation"]["after"] =
                        rows[1]["spray_budget_observation"]["after"].clone()
                }
                5 => {
                    rows[2]["spray_budget_observation"]["after"] =
                        rows[0]["spray_budget_observation"]["after"].clone()
                }
                6 => rows[1]["spray_budget_observation"]["after"]["head"] = json!(-1),
                _ => rows[1]["spray_budget_observation"]["gpu_frame"] = json!(3),
            }
            assert!(
                feedback_order(&rows).is_err(),
                "invalid callback chronology mode {mode}"
            );
        }
    }
}
