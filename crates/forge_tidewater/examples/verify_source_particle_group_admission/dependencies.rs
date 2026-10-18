//! Real GPU dependency ordering, prefix preservation, refusals and recovery.
use super::{image, readback, update};
use bevy::{asset::RenderAssetUsages, prelude::*, render::storage::ShaderBuffer};
use forge_engine::source_particle_compute::{
    SourceParticleComputeQueue, SourceParticleComputeRequest, SourceParticleComputeStatus,
};
use std::{path::Path, time::Instant};

fn buffer(app: &mut App, bytes: &[u8]) -> Handle<ShaderBuffer> {
    app.world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(bytes, RenderAssetUsages::default()))
}
fn request(
    shader: &Handle<Shader>,
    curve: &Handle<Image>,
    input: &Handle<ShaderBuffer>,
    output: &Handle<ShaderBuffer>,
    phase: u32,
) -> Result<SourceParticleComputeRequest, String> {
    let mut bank = [0; 1024];
    bank[1008..1012].copy_from_slice(&phase.to_le_bytes());
    SourceParticleComputeRequest::new(
        shader.clone(),
        "advance".into(),
        curve.clone(),
        bank,
        input.clone(),
        output.clone(),
        1,
    )
    .map_err(str::to_owned)
}
fn observe(
    app: &mut App,
    handle: &Handle<ShaderBuffer>,
    expected: &[u8],
    deadline: Instant,
    path: &Path,
    label: &str,
) -> Result<(), String> {
    let actual = readback(app, handle.clone(), deadline)?;
    std::fs::write(path.join(format!("dependency-{label}.bin")), &actual)
        .map_err(|e| e.to_string())?;
    if actual != expected {
        return Err(format!("Dependency GPU state differs: {label}"));
    }
    Ok(())
}
fn wait(
    app: &mut App,
    requests: &[&SourceParticleComputeRequest],
    failed: bool,
    deadline: Instant,
) -> Result<(), String> {
    while !requests.iter().all(|request| match request.status() {
        Ok(SourceParticleComputeStatus::Encoded) => !failed,
        Ok(SourceParticleComputeStatus::Failed(_)) => failed,
        _ => false,
    }) {
        update(app, deadline)?;
    }
    Ok(())
}

#[allow(
    clippy::too_many_lines,
    reason = "Generated GPU ordering, resource admission and cycle/size refusal evidence"
)]
pub(super) fn verify(
    app: &mut App,
    shader: &Handle<Shader>,
    curve: &Handle<Image>,
    deadline: Instant,
    path: &Path,
) -> Result<serde_json::Value, String> {
    let source: Vec<u8> = (10_u32..42).flat_map(u32::to_le_bytes).collect();
    let raw: Vec<u8> = (100_u32..132).flat_map(u32::to_le_bytes).collect();
    let zero = vec![0; 128];
    let src = buffer(app, &source);
    let intermediate = buffer(app, &zero);
    let stable = buffer(app, &zero);
    let input = buffer(app, &raw);
    let output = buffer(app, &zero);
    let absent = app.world().resource::<Assets<Image>>().reserve_handle();
    let producer = request(shader, &absent, &src, &intermediate, 1)?
        .publish_into(stable.clone(), 128)
        .map_err(str::to_owned)?;
    let consumer = request(shader, curve, &input, &output, 2)?
        .copy_input_prefix_from(stable.clone(), 64)
        .map_err(str::to_owned)?
        .after_slot(90)
        .map_err(str::to_owned)?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert(90, producer.clone());
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert(2, consumer.clone());
    for _ in 0..16 {
        update(app, deadline)?;
    }
    observe(app, &input, &raw, deadline, path, "waiting-consumer-input")?;
    observe(
        app,
        &output,
        &zero,
        deadline,
        path,
        "waiting-consumer-output",
    )?;
    observe(
        app,
        &stable,
        &zero,
        deadline,
        path,
        "waiting-producer-publication",
    )?;
    app.world_mut()
        .resource_mut::<Assets<Image>>()
        .insert(absent.id(), image())
        .map_err(|e| e.to_string())?;
    wait(app, &[&producer, &consumer], false, deadline)?;
    let published: Vec<u8> = (10_u32..42)
        .flat_map(|word| (word * 10 + 1).to_le_bytes())
        .collect();
    let mut copied = published[..64].to_vec();
    copied.extend_from_slice(&raw[64..]);
    let expected: Vec<u8> = (10_u32..26)
        .map(|word| word * 100 + 12)
        .chain((116_u32..132).map(|word| word * 10 + 2))
        .flat_map(u32::to_le_bytes)
        .collect();
    observe(
        app,
        &input,
        &copied,
        deadline,
        path,
        "ordered-prefix-and-static-suffix",
    )?;
    observe(
        app,
        &output,
        &expected,
        deadline,
        path,
        "ordered-consumer-output",
    )?;
    let pending_input = buffer(app, &raw);
    let pending_output = buffer(app, &zero);
    let missing = app
        .world()
        .resource::<Assets<ShaderBuffer>>()
        .reserve_handle();
    let pending = request(shader, curve, &pending_input, &pending_output, 3)?
        .copy_input_prefix_from(missing.clone(), 64)
        .map_err(str::to_owned)?
        .after_slot(90)
        .map_err(str::to_owned)?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert(2, pending.clone());
    for _ in 0..16 {
        update(app, deadline)?;
    }
    observe(
        app,
        &pending_input,
        &raw,
        deadline,
        path,
        "missing-copy-source-input",
    )?;
    observe(
        app,
        &pending_output,
        &zero,
        deadline,
        path,
        "missing-copy-source-output",
    )?;
    let restored: Vec<u8> = (300_u32..316).flat_map(u32::to_le_bytes).collect();
    app.world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .insert(
            missing.id(),
            ShaderBuffer::new(&restored, RenderAssetUsages::default()),
        )
        .map_err(|e| e.to_string())?;
    wait(app, &[&pending], false, deadline)?;
    let expected: Vec<u8> = (300_u32..316)
        .chain(116_u32..132)
        .map(|word| word * 10 + 3)
        .flat_map(u32::to_le_bytes)
        .collect();
    observe(
        app,
        &pending_output,
        &expected,
        deadline,
        path,
        "copy-source-recovered",
    )?;
    let invalid_input = buffer(app, &raw);
    let invalid_output = buffer(app, &zero);
    let invalid = request(shader, curve, &invalid_input, &invalid_output, 4)?
        .copy_input_prefix_from(stable.clone(), 132)
        .map_err(str::to_owned)?
        .after_slot(90)
        .map_err(str::to_owned)?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert(2, invalid.clone());
    wait(app, &[&invalid], true, deadline)?;
    observe(
        app,
        &invalid_input,
        &raw,
        deadline,
        path,
        "invalid-copy-input-preserved",
    )?;
    observe(
        app,
        &invalid_output,
        &zero,
        deadline,
        path,
        "invalid-copy-output-preserved",
    )?;
    let a_input = buffer(app, &raw);
    let a_output = buffer(app, &zero);
    let b_input = buffer(app, &raw);
    let b_output = buffer(app, &zero);
    let a = request(shader, curve, &a_input, &a_output, 5)?
        .copy_input_prefix_from(stable.clone(), 64)
        .map_err(str::to_owned)?
        .after_slot(3)
        .map_err(str::to_owned)?;
    let b = request(shader, curve, &b_input, &b_output, 6)?
        .copy_input_prefix_from(stable.clone(), 64)
        .map_err(str::to_owned)?
        .after_slot(2)
        .map_err(str::to_owned)?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert(2, a.clone());
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert(3, b.clone());
    wait(app, &[&a, &b], true, deadline)?;
    for (handle, expected, label) in [
        (&a_input, &raw, "cycle-a-input"),
        (&a_output, &zero, "cycle-a-output"),
        (&b_input, &raw, "cycle-b-input"),
        (&b_output, &zero, "cycle-b-output"),
    ] {
        observe(app, handle, expected, deadline, path, label)?;
    }
    let independent_input = buffer(app, &source);
    let independent_output = buffer(app, &zero);
    let independent = request(shader, curve, &independent_input, &independent_output, 7)?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert(91, independent.clone());
    wait(app, &[&independent], false, deadline)?;
    let expected: Vec<u8> = (10_u32..42)
        .map(|word| word * 10 + 7)
        .flat_map(u32::to_le_bytes)
        .collect();
    observe(
        app,
        &independent_output,
        &expected,
        deadline,
        path,
        "independent-group-after-cycle",
    )?;
    for slot in [2, 3, 90, 91] {
        app.world_mut()
            .resource_mut::<SourceParticleComputeQueue>()
            .remove(slot);
    }
    Ok(
        serde_json::json!({"status":"GPU_DEPENDENCY_REVERSE_SLOT_ORDER_PREFIX_SUFFIX_RESOURCE_RECOVERY_CYCLE_AND_INVALID_COPY_REFUSAL_PASS",
        "producer_slot":90,"consumer_slot":2,"same_frame_producer_consumer_words_exact":32,
        "static_suffix_bytes_preserved":64,"missing_producer_curve_preserved_consumer_input_output":true,
        "missing_copy_source_recovery_same_request":true,"invalid_copy_preserves_input_output":true,
        "cycle_preserves_four_buffers":true,"independent_group_runs_despite_cycle":true,
        "generated_data_only":true,"full_effect_and_Studio_not_qualified":true}),
    )
}
