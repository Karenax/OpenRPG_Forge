//! Real source state behind an absent owner-local texture; oracle stays on CPU.
use super::{emission, receive_buffer, u32_at, verify_state};
use bevy::{
    asset::RenderAssetUsages,
    prelude::*,
    render::{
        RenderApp,
        gpu_readback::Readback,
        render_asset::RenderAssets,
        storage::{GpuShaderBuffer, ShaderBuffer},
    },
};
use forge_assets::source_particle_emission::{
    SourceEmissionEvent, SourceSingleBurstLoopParameters,
};
use forge_engine::source_particle_compute::{
    SourceParticleComputeQueue, SourceParticleComputeSequence, SourceParticleSceneOwner,
};
use std::{
    path::Path,
    time::{Duration, Instant},
};

fn update(app: &mut App, deadline: Instant) -> Result<(), String> {
    if Instant::now() > deadline {
        return Err("Scene owner native resource probe timeout".into());
    }
    app.update();
    std::thread::sleep(Duration::from_millis(2));
    Ok(())
}

fn state(app: &App, entity: Entity) -> Result<&SourceParticleSceneOwner, String> {
    let owner = app
        .world()
        .get::<SourceParticleSceneOwner>(entity)
        .ok_or("Scene probe owner absent")?;
    if let Some(error) = owner.error() {
        return Err(error.to_owned());
    }
    Ok(owner)
}

fn wait(app: &mut App, entity: Entity, frames: u64, deadline: Instant) -> Result<(), String> {
    while state(app, entity)?.completed_frames() < frames {
        update(app, deadline)?;
    }
    Ok(())
}

fn read(
    app: &mut App,
    buffer: &Handle<ShaderBuffer>,
    deadline: Instant,
) -> Result<Vec<u8>, String> {
    let (entity, slot) = receive_buffer(app, buffer.clone());
    let mut previous = None;
    let mut stable = 0;
    loop {
        update(app, deadline)?;
        if let Some(data) = slot
            .lock()
            .map_err(|_| "Probe readback mutex poisoned")?
            .take()
        {
            if previous.as_ref() == Some(&data) {
                stable += 1;
            } else {
                stable = 0;
            }
            previous = Some(data);
            if stable >= 3 {
                break;
            }
        }
    }
    app.world_mut().entity_mut(entity).remove::<Readback>();
    previous.ok_or("Scene probe readback absent".into())
}

fn pair(app: &mut App, initial: &[u8]) -> [Handle<ShaderBuffer>; 2] {
    std::array::from_fn(|_| {
        app.world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::new(initial, RenderAssetUsages::default()))
    })
}

fn queue(app: &mut App, entity: Entity, events: &[SourceEmissionEvent]) -> Result<(), String> {
    app.world_mut()
        .get_mut::<SourceParticleSceneOwner>(entity)
        .ok_or("Scene probe owner removed")?
        .queue_frame(0.0, events, false)
        .map_err(str::to_owned)
}

fn checkpoint(
    app: &mut App,
    entity: Entity,
    oracle: (&[u8], &[u8]),
    stamp: u16,
    deadline: Instant,
    directory: &Path,
    label: &str,
) -> Result<Vec<u8>, String> {
    let buffer = state(app, entity)?.current_buffer();
    let data = read(app, &buffer, deadline)?;
    std::fs::write(directory.join(format!("{label}.bin")), &data).map_err(|e| e.to_string())?;
    verify_state(&data, oracle.0, oracle.1, stamp)?;
    Ok(data)
}

#[allow(
    clippy::too_many_arguments,
    clippy::too_many_lines,
    reason = "Explicit pinned source ABI, resource wait and owner lifecycle evidence"
)]
pub(super) fn verify(
    app: &mut App,
    shader: &Handle<Shader>,
    curve: &Handle<Image>,
    entry: &str,
    parameters: SourceSingleBurstLoopParameters,
    bank: &[u8; 1024],
    initial: &[u8],
    reference: &[u8],
    output: &Path,
) -> Result<serde_json::Value, String> {
    let directory = output.join("scene-resource-probe");
    std::fs::create_dir(&directory).map_err(|e| e.to_string())?;
    let deadline = Instant::now() + Duration::from_secs(120);
    let pair_a = pair(app, initial);
    let pair_b = pair(app, initial);
    let absent = app.world().resource::<Assets<Image>>().reserve_handle();
    let published_a = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(initial, RenderAssetUsages::default()));
    let published_b = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(initial, RenderAssetUsages::default()));
    let make = |slot, curve, pair, published| {
        let compute = SourceParticleComputeSequence::new(
            slot,
            shader.clone(),
            entry.to_owned(),
            curve,
            pair,
            1,
        )
        .map_err(str::to_owned)?
        .with_publication_buffer(published, initial.len() as u64)
        .map_err(str::to_owned)?;
        emission::scene_owner(compute, parameters, bank)
    };
    let a = app
        .world_mut()
        .spawn(make(17, curve.clone(), pair_a, published_a)?)
        .id();
    let b = app
        .world_mut()
        .spawn(make(
            19,
            absent.clone(),
            pair_b.clone(),
            published_b.clone(),
        )?)
        .id();
    queue(app, a, &[])?;
    queue(app, b, &[])?;
    queue(app, b, &[SourceEmissionEvent::Stop])?;
    wait(app, a, 1, deadline)?;
    checkpoint(
        app,
        a,
        (reference, initial),
        1,
        deadline,
        &directory,
        "a-initial",
    )?;
    if state(app, b)?.completed_frames() != 0
        || state(app, b)?.queued_frames() != 1
        || state(app, b)?.last_frame().is_some()
    {
        return Err("Unloaded scene owner consumed pending Stop or published its candidate".into());
    }
    for (index, buffer) in pair_b
        .iter()
        .chain(std::iter::once(&published_b))
        .enumerate()
    {
        let data = read(app, buffer, deadline)?;
        std::fs::write(directory.join(format!("b-waiting-{index}.bin")), &data)
            .map_err(|e| e.to_string())?;
        if data != initial {
            return Err("Missing scene curve partially mutated its state buffers".into());
        }
    }
    queue(app, a, &[SourceEmissionEvent::Stop])?;
    wait(app, a, 2, deadline)?;
    checkpoint(
        app,
        a,
        (reference, initial),
        2,
        deadline,
        &directory,
        "a-stop",
    )?;
    if state(app, a)?.snapshot().playing() {
        return Err("Scene owner A Stop not committed".into());
    }
    queue(app, a, &[SourceEmissionEvent::Play])?;
    wait(app, a, 3, deadline)?;
    let before = checkpoint(
        app,
        a,
        (reference, initial),
        3,
        deadline,
        &directory,
        "a-replay",
    )?;
    if !state(app, a)?.snapshot().playing()
        || state(app, b)?.completed_frames() != 0
        || state(app, b)?.queued_frames() != 1
    {
        return Err("Owner A events affected owner B resource wait".into());
    }
    let image = app
        .world()
        .resource::<Assets<Image>>()
        .get(curve)
        .ok_or("Admitted curve template absent")?
        .clone();
    app.world_mut()
        .resource_mut::<Assets<Image>>()
        .insert(absent.id(), image)
        .map_err(|e| e.to_string())?;
    wait(app, b, 2, deadline)?;
    checkpoint(
        app,
        b,
        (reference, initial),
        2,
        deadline,
        &directory,
        "b-recovered-stop",
    )?;
    if state(app, b)?.snapshot().playing()
        || state(app, b)?.queued_frames() != 0
        || state(app, a)?.encoded_steps() != 6
        || state(app, b)?.encoded_steps() != 4
    {
        return Err("Scene owner FIFO recovery clock or phase counts differ".into());
    }
    let render = app
        .get_sub_app(RenderApp)
        .ok_or("Probe renderer absent")?
        .world();
    let gpu = render.resource::<RenderAssets<GpuShaderBuffer>>();
    let ha = state(app, a)?.current_buffer();
    let hb = state(app, b)?.current_buffer();
    if gpu.get(&ha).ok_or("Owner A GPU state absent")?.buffer.id()
        == gpu.get(&hb).ok_or("Owner B GPU state absent")?.buffer.id()
    {
        return Err("Probe owners share a physical GPU buffer".into());
    }
    let fresh_published = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::new(initial, RenderAssetUsages::default()));
    let replacement = make(19, curve.clone(), pair(app, initial), fresh_published)?;
    app.world_mut().entity_mut(b).insert(replacement);
    for _ in 0..4 {
        update(app, deadline)?;
    }
    if state(app, b)?.completed_frames() != 0
        || state(app, b)?.submitted_frames() != 0
        || state(app, b)?.queued_frames() != 0
        || app
            .world()
            .resource::<SourceParticleComputeQueue>()
            .contains_slot(19)
    {
        return Err(
            "Component replacement retained its previous jobs or activated without input".into(),
        );
    }
    let unaffected_buffer = state(app, a)?.current_buffer();
    let after = read(app, &unaffected_buffer, deadline)?;
    std::fs::write(directory.join("a-after-b-replacement.bin"), &after)
        .map_err(|e| e.to_string())?;
    if after != before || u32_at(&after, 56) != 3 {
        return Err("Replacement changed the unrelated owner state".into());
    }
    app.world_mut().despawn(a);
    app.world_mut().despawn(b);
    for _ in 0..4 {
        update(app, deadline)?;
    }
    if [17, 19].iter().any(|slot| {
        app.world()
            .resource::<SourceParticleComputeQueue>()
            .contains_slot(*slot)
    }) || app.world().entities().contains(a)
        || app.world().entities().contains(b)
        || app.world().resource::<Assets<Image>>().get(curve).is_none()
    {
        return Err("Probe owner retirement left jobs or removed the borrowed curve".into());
    }
    Ok(
        serde_json::json!({"status":"NATIVE_SCENE_OWNER_RESOURCE_WAIT_FIFO_RECOVERY_REPLACEMENT_AND_ISOLATION_PASS",
        "native_fields_exact":initial.len()/160*52,"native_fields_total":initial.len()/160*52,"native_checkpoints":4,
        "waiting_GPU_bytes_preserved":initial.len()*3,"unrelated_owner_after_replacement_bytes_preserved":initial.len(),
        "owners":2,"logical_frames":5,"encoded_phases":10,"queued_Stop_preserved":true,
        "other_owner_Stop_Play_progresses_while_curve_missing":true,"physical_GPU_state_buffers_distinct":true,
        "fresh_component_retires_old_jobs_without_implicit_activation":true,"owners_retired":2,
        "borrowed_curve_preserved":true,"CPU_particle_state_uploads_between_steps":0,
        "stable_GPU_publication_used":true,"zero_delta_initial_native_oracle_only":true,"scene_lighting_and_Studio_not_qualified":true}),
    )
}
