//! Compare externally pinned native clock observations with the source model.
//! This checks CPU emission state only; it does not claim a GPU or Studio result.
use forge_assets::source_particle_emission::{
    SourceEmissionEvent, SourceEmissionLoopState, SourceEmissionSnapshot, SourceSingleBurstLoop,
    SourceSingleBurstLoopParameters,
};
use forge_core::bytes_sha256;
use serde::Deserialize;
use std::path::Path;

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Input {
    schema_version: u32,
    source_spawner: SourceSingleBurstLoopParameters,
    steps: Vec<Step>,
    references: Vec<Reference>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Step {
    delta_time: f32,
    events: Vec<SourceEmissionEvent>,
    reinitialize: bool,
    label: String,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Reference {
    step: usize,
    snapshot: ExpectedClock,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ExpectedClock {
    effect_time_seconds: f32,
    loop_total_time_seconds: f32,
    loop_duration_seconds: f32,
    loop_count: u32,
    loop_index: u32,
    loop_state: SourceEmissionLoopState,
    spawn_count: f32,
    new_loop: bool,
}

fn words(value: SourceEmissionSnapshot) -> [u32; 8] {
    [
        value.effect_time_seconds.to_bits(),
        value.loop_total_time_seconds.to_bits(),
        value.loop_duration_seconds.to_bits(),
        value.loop_count,
        value.loop_index,
        u32::from(value.playing()),
        value.spawn_count.to_bits(),
        u32::from(value.new_loop),
    ]
}

fn expected_words(value: &ExpectedClock) -> [u32; 8] {
    words(SourceEmissionSnapshot {
        effect_time_seconds: value.effect_time_seconds,
        loop_total_time_seconds: value.loop_total_time_seconds,
        loop_duration_seconds: value.loop_duration_seconds,
        loop_count: value.loop_count,
        loop_index: value.loop_index,
        loop_state: value.loop_state,
        spawn_count: value.spawn_count,
        new_loop: value.new_loop,
    })
}

fn run(input_path: &Path, expected_hash: &str, output: &Path) -> Result<(), String> {
    let bytes = std::fs::read(input_path).map_err(|e| e.to_string())?;
    if bytes_sha256(&bytes).to_string() != expected_hash || output.exists() {
        return Err("Changed clock input or existing output".into());
    }
    let input: Input = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
    if input.schema_version != 1
        || input.references.is_empty()
        || input
            .references
            .iter()
            .any(|r| r.step == 0 || r.step > input.steps.len())
        || input
            .references
            .windows(2)
            .any(|pair| pair[0].step >= pair[1].step)
    {
        return Err("Invalid native clock reference schedule".into());
    }
    let mut clock = SourceSingleBurstLoop::new(input.source_spawner).map_err(str::to_owned)?;
    let mut reference = 0;
    let mut frames = Vec::with_capacity(input.steps.len());
    for (i, step) in input.steps.iter().enumerate() {
        if step.reinitialize {
            clock.reinitialize();
        }
        for event in &step.events {
            clock.queue_event(*event);
        }
        let frame = clock.advance(step.delta_time).map_err(str::to_owned)?;
        if let Some(expected) = input.references.get(reference)
            && expected.step == i + 1
        {
            if words(frame.snapshot) != expected_words(&expected.snapshot) {
                return Err(format!(
                    "Native clock mismatch at step {} ({})",
                    i + 1,
                    step.label
                ));
            }
            reference += 1;
        }
        frames.push(serde_json::json!({"step":i+1,"label":step.label,"spawn_request":frame.spawn_request,"reset_particles":frame.reset_particles,"snapshot":frame.snapshot}));
    }
    if reference != input.references.len() {
        return Err("Native clock references not all consumed".into());
    }
    std::fs::create_dir(output).map_err(|e| e.to_string())?;
    let report = serde_json::json!({"status":"SOURCE_CLOCK_NATIVE_REFERENCES_EXACT","steps":input.steps.len(),"native_reference_states":reference,"native_fields_exact":reference*8,"GPU_or_Studio_qualified":false,"frames":frames});
    std::fs::write(
        output.join("RESULT.v1.json"),
        serde_json::to_vec_pretty(&report).map_err(|e| e.to_string())?,
    )
    .map_err(|e| e.to_string())?;
    println!(
        "PASS {} native clock states / {} fields",
        reference,
        reference * 8
    );
    Ok(())
}

fn main() {
    let args: Vec<_> = std::env::args().collect();
    let result = if args.len() == 4 {
        run(Path::new(&args[1]), &args[2], Path::new(&args[3]))
    } else {
        Err("Pinned clock manifest, SHA256 and new output directory required".into())
    };
    if let Err(error) = result {
        eprintln!("FAIL {error}");
        std::process::exit(1);
    }
}
