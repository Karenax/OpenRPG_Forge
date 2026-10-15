//! Generated counters qualify owner scheduling, not purchased effect semantics.
use super::{image, readback, update};
use bevy::{asset::RenderAssetUsages, prelude::*, render::storage::ShaderBuffer};
use forge_assets::source_particle_emission::{
    SourceEmissionEvent, SourceSingleBurstLoopParameters,
};
use forge_core::bytes_sha256;
use forge_engine::source_particle_compute::{
    SourceParticleComputeQueue, SourceParticleComputeSequence, SourceParticleEmissionCommand,
    SourceParticleEmitterSequence, register_source_particle_compute_program,
};
use std::{path::Path, time::Instant};

const PROGRAM: &str = r"
@group(0) @binding(0) var curve: texture_2d<f32>;
@group(0) @binding(1) var curve_sampler: sampler;
struct Bank { words: array<vec4<u32>,64>, }
@group(0) @binding(2) var<uniform> bank: Bank;
@group(0) @binding(3) var<storage,read> src: array<u32>;
@group(0) @binding(4) var<storage,read_write> dst: array<u32>;
@compute @workgroup_size(1) fn advance() {
 for(var i=0u; i<8u; i=i+1u) { dst[i]=src[i]; }
 let mode=bank.words[63].y;
 if mode==3u {
  dst[1]=0u; dst[2]=0u; dst[3]=0u; dst[4]=src[4]+1u;
 } else if mode==2u {
  dst[1]=src[1]+1u;
 } else if mode==1u {
  dst[2]=src[2]+1u;
  dst[3]=bitcast<u32>(bitcast<f32>(src[3])+bitcast<f32>(bank.words[63].x));
 }
}
";

fn lower(command: SourceParticleEmissionCommand) -> Result<[u8; 1024], String> {
    let (mode, delta) = match command {
        SourceParticleEmissionCommand::Reset => (3_u32, 0.0_f32),
        SourceParticleEmissionCommand::Allocate { count }
            if count.to_bits() == 1.0_f32.to_bits() =>
        {
            (2, 0.0)
        }
        SourceParticleEmissionCommand::Allocate { .. } => {
            return Err("Generated owner probe only admits Count1".into());
        }
        SourceParticleEmissionCommand::Update { delta_seconds } => (1, delta_seconds),
    };
    let mut bank = [0; 1024];
    bank[1008..1012].copy_from_slice(&delta.to_le_bytes());
    bank[1012..1016].copy_from_slice(&mode.to_le_bytes());
    Ok(bank)
}

fn wait(
    app: &mut App,
    owner: &mut SourceParticleEmitterSequence,
    deadline: Instant,
) -> Result<(), String> {
    while !owner.poll()? {
        update(app, deadline)?;
    }
    Ok(())
}

fn submit(
    app: &mut App,
    owner: &mut SourceParticleEmitterSequence,
    delta: f32,
    events: &[SourceEmissionEvent],
    reset: bool,
) -> Result<bool, String> {
    owner.submit_frame(
        &mut app.world_mut().resource_mut::<SourceParticleComputeQueue>(),
        delta,
        events,
        reset,
        lower,
    )
}

fn check(
    app: &mut App,
    buffer: &Handle<ShaderBuffer>,
    expected: [u32; 8],
    deadline: Instant,
    output: &Path,
    label: &str,
) -> Result<(), String> {
    let observed = readback(app, buffer.clone(), deadline)?;
    std::fs::write(output.join(format!("{label}.bin")), &observed).map_err(|e| e.to_string())?;
    let expected: Vec<u8> = expected.into_iter().flat_map(u32::to_le_bytes).collect();
    if observed != expected {
        return Err(format!("Independent owner GPU mismatch: {label}"));
    }
    Ok(())
}

fn owner(
    slot: u64,
    shader: Handle<Shader>,
    curve: Handle<Image>,
    pair: [Handle<ShaderBuffer>; 2],
) -> Result<SourceParticleEmitterSequence, String> {
    let compute =
        SourceParticleComputeSequence::new(slot, shader, "advance".into(), curve, pair, 1)
            .map_err(str::to_owned)?;
    SourceParticleEmitterSequence::new(
        compute,
        SourceSingleBurstLoopParameters {
            loop_duration_seconds: 3.0,
            loop_count: 1,
            burst_delay_seconds: 0.0,
            burst_count: 1.0,
            burst_active: true,
            initial_play: false,
            prewarm_steps: 0,
        },
    )
    .map_err(str::to_owned)
}

#[allow(
    clippy::too_many_lines,
    reason = "Ordered two-owner GPU resource/backpressure and event checkpoints"
)]
pub(super) fn verify(
    app: &mut App,
    deadline: Instant,
    output: &Path,
) -> Result<serde_json::Value, String> {
    let shader = register_source_particle_compute_program(
        &mut app.world_mut().resource_mut::<Assets<Shader>>(),
        PROGRAM,
        bytes_sha256(PROGRAM.as_bytes()),
        "advance",
    )?;
    let make_pair = |app: &mut App, id: u32| {
        let bytes: Vec<u8> = [id, 0, 0, 0, 0, 0xABCD, 0, 0]
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect();
        std::array::from_fn(|_| {
            app.world_mut()
                .resource_mut::<Assets<ShaderBuffer>>()
                .add(ShaderBuffer::new(&bytes, RenderAssetUsages::default()))
        })
    };
    let pair_a = make_pair(app, 11);
    let pair_b = make_pair(app, 22);
    let curve_a = app.world_mut().resource_mut::<Assets<Image>>().add(image());
    let curve_b = app.world().resource::<Assets<Image>>().reserve_handle();
    let mut a = owner(11, shader.clone(), curve_a, pair_a)?;
    let mut b = owner(22, shader, curve_b.clone(), pair_b.clone())?;
    let sent = |value| {
        if value {
            Ok(())
        } else {
            Err("Unexpected owner backpressure".to_owned())
        }
    };
    sent(submit(
        app,
        &mut a,
        0.125,
        &[SourceEmissionEvent::Play],
        false,
    )?)?;
    sent(submit(
        app,
        &mut b,
        0.25,
        &[SourceEmissionEvent::Play],
        false,
    )?)?;
    wait(app, &mut a, deadline)?;
    check(
        app,
        &a.current_buffer(),
        [11, 1, 1, 0.125_f32.to_bits(), 1, 0xABCD, 0, 0],
        deadline,
        output,
        "owner-a-first",
    )?;
    if b.poll()? || b.snapshot().effect_time_seconds.to_bits() != 0 {
        return Err("Missing owner B curve advanced its committed clock".into());
    }
    for (i, buffer) in pair_b.iter().enumerate() {
        check(
            app,
            buffer,
            [22, 0, 0, 0, 0, 0xABCD, 0, 0],
            deadline,
            output,
            &format!("owner-b-waiting-{i}"),
        )?;
    }
    if submit(app, &mut b, 0.5, &[SourceEmissionEvent::Stop], false)? {
        return Err("Pending owner B discarded unsent Stop/delta".into());
    }
    sent(submit(
        app,
        &mut a,
        0.125,
        &[SourceEmissionEvent::Stop],
        false,
    )?)?;
    wait(app, &mut a, deadline)?;
    if a.snapshot().playing() {
        return Err("Owner A Stop was not committed".into());
    }
    check(
        app,
        &a.current_buffer(),
        [11, 1, 2, 0.25_f32.to_bits(), 1, 0xABCD, 0, 0],
        deadline,
        output,
        "owner-a-stop",
    )?;
    sent(submit(
        app,
        &mut a,
        0.25,
        &[SourceEmissionEvent::Play],
        false,
    )?)?;
    wait(app, &mut a, deadline)?;
    check(
        app,
        &a.current_buffer(),
        [11, 2, 3, 0.5_f32.to_bits(), 1, 0xABCD, 0, 0],
        deadline,
        output,
        "owner-a-replay",
    )?;
    if !a.snapshot().playing() || b.snapshot().effect_time_seconds.to_bits() != 0 {
        return Err("Owner A Play altered waiting owner B clock".into());
    }
    app.world_mut()
        .resource_mut::<Assets<Image>>()
        .insert(curve_b.id(), image())
        .map_err(|e| e.to_string())?;
    wait(app, &mut b, deadline)?;
    check(
        app,
        &b.current_buffer(),
        [22, 1, 1, 0.25_f32.to_bits(), 1, 0xABCD, 0, 0],
        deadline,
        output,
        "owner-b-recovered",
    )?;
    sent(submit(
        app,
        &mut b,
        0.5,
        &[SourceEmissionEvent::Stop],
        false,
    )?)?;
    wait(app, &mut b, deadline)?;
    check(
        app,
        &b.current_buffer(),
        [22, 1, 2, 0.75_f32.to_bits(), 1, 0xABCD, 0, 0],
        deadline,
        output,
        "owner-b-retained-stop",
    )?;
    if b.snapshot().playing() || a.snapshot().effect_time_seconds.to_bits() != 0.5_f32.to_bits() {
        return Err("Retained owner B Stop did not remain isolated".into());
    }
    check(
        app,
        &a.current_buffer(),
        [11, 2, 3, 0.5_f32.to_bits(), 1, 0xABCD, 0, 0],
        deadline,
        output,
        "owner-a-after-b-stop",
    )?;
    sent(submit(app, &mut a, 0.0625, &[], true)?)?;
    sent(submit(
        app,
        &mut b,
        0.125,
        &[SourceEmissionEvent::Play],
        false,
    )?)?;
    wait(app, &mut a, deadline)?;
    wait(app, &mut b, deadline)?;
    let final_a = [11, 0, 1, 0.0625_f32.to_bits(), 2, 0xABCD, 0, 0];
    let final_b = [22, 2, 3, 0.875_f32.to_bits(), 1, 0xABCD, 0, 0];
    check(
        app,
        &a.current_buffer(),
        final_a,
        deadline,
        output,
        "owner-a-reinit",
    )?;
    check(
        app,
        &b.current_buffer(),
        final_b,
        deadline,
        output,
        "owner-b-second-play",
    )?;
    if a.snapshot().playing()
        || !b.snapshot().playing()
        || a.snapshot().effect_time_seconds.to_bits() != 0.0625_f32.to_bits()
        || b.snapshot().effect_time_seconds.to_bits() != 0.875_f32.to_bits()
        || a.encoded_steps() != 8
        || b.encoded_steps() != 6
    {
        return Err("Independent final clocks or phase counts differ".into());
    }
    for _ in 0..8 {
        update(app, deadline)?;
    }
    check(
        app,
        &a.current_buffer(),
        final_a,
        deadline,
        output,
        "owner-a-retained-final",
    )?;
    check(
        app,
        &b.current_buffer(),
        final_b,
        deadline,
        output,
        "owner-b-retained-final",
    )?;
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .remove(11);
    app.world_mut()
        .resource_mut::<SourceParticleComputeQueue>()
        .remove(22);
    Ok(
        serde_json::json!({"status":"TWO_INDEPENDENT_OWNER_GPU_EVENT_SCHEDULES_AND_RESOURCE_ISOLATION_PASS",
        "owners":2,"logical_submissions":7,"encoded_phases":14,"GPU_checkpoints":12,
        "GPU_checked_bytes":384,"waiting_owner_preserved_bytes":64,
        "other_owner_advances_while_one_curve_absent":true,"unsent_Stop_and_delta_retained":true,
        "Play_Stop_Reinit_clocks_isolated":true,"retained_groups_not_reencoded":true,
        "a_clock":a.snapshot(),"b_clock":b.snapshot(),"generated_counter_program_only":true,
        "CPU_particle_uploads_between_submissions":0,"purchased_effect_independent_owners_not_qualified":true,
        "actual_scene_and_Studio_not_qualified":true}),
    )
}
