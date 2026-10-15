//! Generated values prove stable GPU publication admission and copy ordering.
use super::{image, readback, requests, update};
use bevy::{asset::RenderAssetUsages, prelude::*, render::storage::ShaderBuffer};
use forge_engine::source_particle_compute::{
    SourceParticleComputeQueue, SourceParticleComputeRequest, SourceParticleComputeStatus,
};
use std::{path::Path, time::Instant};

fn attach(
    mut phases: Vec<SourceParticleComputeRequest>,
    stable: &Handle<ShaderBuffer>,
) -> Result<Vec<SourceParticleComputeRequest>, String> {
    let last = phases.pop().ok_or("Publication phases absent")?;
    phases.push(
        last.publish_into(stable.clone(), 128)
            .map_err(str::to_owned)?,
    );
    Ok(phases)
}

fn read_exact(
    app: &mut App,
    buffer: &Handle<ShaderBuffer>,
    expected: &[u8],
    deadline: Instant,
    output: &Path,
    label: &str,
) -> Result<(), String> {
    let observed = readback(app, buffer.clone(), deadline)?;
    std::fs::write(output.join(format!("publication-{label}.bin")), &observed)
        .map_err(|e| e.to_string())?;
    if observed != expected {
        return Err(format!("Stable publication GPU differs: {label}"));
    }
    Ok(())
}

fn failed(
    app: &mut App,
    phases: &[SourceParticleComputeRequest],
    deadline: Instant,
) -> Result<(), String> {
    while !phases
        .iter()
        .all(|phase| matches!(phase.status(), Ok(SourceParticleComputeStatus::Failed(_))))
    {
        update(app, deadline)?;
    }
    Ok(())
}

fn complete(
    app: &mut App,
    phases: &[SourceParticleComputeRequest],
    deadline: Instant,
) -> Result<(), String> {
    while !phases
        .iter()
        .all(|phase| matches!(phase.status(), Ok(SourceParticleComputeStatus::Encoded)))
    {
        if let Some(error) = phases.iter().find_map(|phase| match phase.status() {
            Ok(SourceParticleComputeStatus::Failed(error)) => Some(error),
            _ => None,
        }) {
            return Err(error);
        }
        update(app, deadline)?;
    }
    Ok(())
}

#[allow(
    clippy::too_many_lines,
    reason = "GPU admission/refusal/recovery before final publication"
)]
pub(super) fn verify(
    app: &mut App,
    shader: &Handle<Shader>,
    curve: &Handle<Image>,
    deadline: Instant,
    output: &Path,
) -> Result<serde_json::Value, String> {
    let initial: Vec<u8> = (10_u32..42).flat_map(u32::to_le_bytes).collect();
    let make_pair = |app: &mut App| {
        std::array::from_fn(|_| {
            app.world_mut()
                .resource_mut::<Assets<ShaderBuffer>>()
                .add(ShaderBuffer::new(&initial, RenderAssetUsages::default()))
        })
    };
    let pair = make_pair(app);
    let sentinel = vec![0xA5; 128];
    let stable = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(&sentinel, RenderAssetUsages::default()));
    let broken = app
        .world_mut()
        .resource_mut::<Assets<Shader>>()
        .add(Shader::from_wgsl(
            "invalid generated WGSL",
            "generated publication failure",
        ));
    let phases = attach(
        requests(shader, curve, &pair, None, Some(&broken))?,
        &stable,
    )?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert_ordered(31, phases.clone())
        .map_err(str::to_owned)?;
    failed(app, &phases, deadline)?;
    read_exact(
        app,
        &stable,
        &sentinel,
        deadline,
        output,
        "failed-shader-stable",
    )?;
    for (index, buffer) in pair.iter().enumerate() {
        read_exact(
            app,
            buffer,
            &initial,
            deadline,
            output,
            &format!("failed-shader-state-{index}"),
        )?;
    }
    let absent = app.world().resource::<Assets<Image>>().reserve_handle();
    let phases = attach(
        requests(shader, curve, &pair, Some(&absent), None)?,
        &stable,
    )?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert_ordered(31, phases.clone())
        .map_err(str::to_owned)?;
    for _ in 0..16 {
        update(app, deadline)?;
    }
    read_exact(
        app,
        &stable,
        &sentinel,
        deadline,
        output,
        "missing-curve-stable",
    )?;
    for (index, buffer) in pair.iter().enumerate() {
        read_exact(
            app,
            buffer,
            &initial,
            deadline,
            output,
            &format!("missing-curve-state-{index}"),
        )?;
    }
    app.world_mut()
        .resource_mut::<Assets<Image>>()
        .insert(absent.id(), image())
        .map_err(|e| e.to_string())?;
    complete(app, &phases, deadline)?;
    let expected: Vec<u8> = (10_u32..42)
        .flat_map(|word| (word * 1000 + 123).to_le_bytes())
        .collect();
    read_exact(app, &stable, &expected, deadline, output, "recovered-final")?;
    for _ in 0..8 {
        update(app, deadline)?;
    }
    read_exact(app, &stable, &expected, deadline, output, "retained-once")?;
    let pair = make_pair(app);
    let missing = app
        .world()
        .resource::<Assets<ShaderBuffer>>()
        .reserve_handle();
    let phases = attach(requests(shader, curve, &pair, None, None)?, &missing)?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert_ordered(31, phases.clone())
        .map_err(str::to_owned)?;
    for _ in 0..16 {
        update(app, deadline)?;
    }
    for (index, buffer) in pair.iter().enumerate() {
        read_exact(
            app,
            buffer,
            &initial,
            deadline,
            output,
            &format!("missing-publication-state-{index}"),
        )?;
    }
    app.world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .insert(
            missing.id(),
            ShaderBuffer::new(&sentinel, RenderAssetUsages::default()),
        )
        .map_err(|e| e.to_string())?;
    complete(app, &phases, deadline)?;
    read_exact(
        app,
        &missing,
        &expected,
        deadline,
        output,
        "publication-resource-recovered",
    )?;
    let pair = make_pair(app);
    let small_bytes = vec![0x5A; 124];
    let small = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(
            &small_bytes,
            RenderAssetUsages::default(),
        ));
    let phases = attach(requests(shader, curve, &pair, None, None)?, &small)?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .insert_ordered(31, phases.clone())
        .map_err(str::to_owned)?;
    failed(app, &phases, deadline)?;
    for (index, buffer) in pair.iter().enumerate() {
        read_exact(
            app,
            buffer,
            &initial,
            deadline,
            output,
            &format!("undersized-publication-state-{index}"),
        )?;
    }
    read_exact(
        app,
        &small,
        &small_bytes,
        deadline,
        output,
        "undersized-publication-destination",
    )?;
    read_exact(
        app,
        &stable,
        &expected,
        deadline,
        output,
        "prior-published-state-retained",
    )?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .remove(31);
    Ok(
        serde_json::json!({"status":"GPU_STABLE_PUBLICATION_FINAL_ONLY_FAILURE_WAIT_SIZE_REFUSAL_AND_RECOVERY_PASS",
        "shader_failure_and_missing_curve_preserve_all_three_buffers":true,
        "missing_publication_buffer_preserves_both_simulation_buffers":true,
        "same_requests_recover_after_publication_resource_admission":true,
        "undersized_publication_refused_before_any_dispatch_or_copy":true,
        "prior_stable_publication_preserved":true,"published_words_exact":32,
        "retained_group_not_republished":true,"generated_program_only":true,"full_effect_not_qualified":true}),
    )
}
