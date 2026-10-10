// Headless live atmosphere verification on the production Bevy render graph.
use bevy::{
    app::PluginsState,
    prelude::*,
    render::{RenderApp, pipelined_rendering::PipelinedRenderingPlugin, renderer::RenderDevice},
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::{tidewater_atmosphere as atmosphere, tidewater_sky::solar};
use serde_json::{Value, json};
use std::io::Write;
use std::{
    error::Error,
    path::Path,
    sync::Arc,
    time::{Duration, Instant},
};

#[expect(
    clippy::too_many_lines,
    reason = "Atmosphere startup, current generation observation and cancellation remain one ordered acquisition probe"
)]
fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 {
        return Err("ATMOSPHERE_FIXTURE MANIFEST_SHA256 FRESH_REPORT required".into());
    }
    let root = Path::new(&args[0]);
    let report = Path::new(&args[2]);
    if report.exists() {
        return Err("report must be new".into());
    }
    let raw = std::fs::read(root.join("atmosphere.json"))?;
    if forge_core::bytes_sha256(&raw).to_string() != args[1] {
        return Err("atmosphere manifest hash differs".into());
    }
    let meta: Value = serde_json::from_slice(&raw)?;
    for (name, pin) in meta["files"]
        .as_object()
        .ok_or("missing acquisition pins")?
    {
        if name.contains(['/', '\\']) || name == "." || name == ".." {
            return Err("non-flat acquisition path".into());
        }
        if forge_core::bytes_sha256(&std::fs::read(root.join(name))?).to_string()
            != pin.as_str().ok_or("missing hash")?
        {
            return Err(format!("acquisition hash differs: {name}").into());
        }
    }
    let input = atmosphere::from_acquisition(&meta, false, |name| {
        std::fs::read(root.join(name)).map_err(|e| e.to_string())
    })?;
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    );
    atmosphere::register(&mut app)?;
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("Bevy startup timeout".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    let original = atmosphere::stage(app.world_mut(), input)?;
    let device = app
        .sub_app(RenderApp)
        .world()
        .resource::<RenderDevice>()
        .clone();
    let mut base: [f32; 16] = std::array::from_fn(|i| original.params[i / 4][i % 4]);
    let mut records = Vec::new();
    for hours in [16.2, 18.2, 23.0, 16.2] {
        let previous_reader = atmosphere::readback::last_completed_reader(app.world());
        let frame = *solar::Clock::new(solar::Settings { hours, ..default() })?.frame();
        base[12..15].copy_from_slice(&frame.sun.as_vec3().to_array());
        let mut completed = None;
        for iteration in 0..120 {
            atmosphere::controller::advance(app.world_mut(), 1.0 / 60.0, frame, base, false)?;
            app.update();
            device.poll(wgpu::PollType::Poll)?;
            if !atmosphere::readback::is_pending(app.world())
                && atmosphere::readback::last_completed_reader(app.world()).is_some()
                && atmosphere::readback::last_completed_reader(app.world()) != previous_reader
                && let Some(result) = atmosphere::readback::latest(app.world(), frame)
                && iteration >= 20
            {
                completed = Some(result?);
                break;
            }
            std::thread::sleep(Duration::from_millis(5));
        }
        let completed = completed.ok_or("live atmosphere readback timeout")?;
        let current = app.world().resource::<atmosphere::Targets>();
        if current.images != original.images
            || current.irradiance != original.irradiance
            || !Arc::ptr_eq(&current.ready, &original.ready)
        {
            return Err("live atmosphere replaced resident resources".into());
        }
        records.push(json!({"hours":hours,"night":frame.night,"completion_entity_bits":atmosphere::readback::last_completed_reader(app.world()).map(Entity::to_bits),"sun_color":completed.lighting.sun_color,"sky_irradiance":completed.lighting.sky_irradiance,"horizon_color":completed.lighting.horizon_color}));
    }
    if records[0]["sun_color"] == records[2]["sun_color"]
        || records[0]["sky_irradiance"] == records[2]["sky_irradiance"]
    {
        return Err("day/night GPU lighting did not change".into());
    }
    atmosphere::controller::cancel(app.world_mut());
    if atmosphere::readback::is_pending(app.world()) {
        return Err("readback survived scene cancellation".into());
    }
    let receipt = json!({"source_commit":meta["source_commit"],"manifest_sha256":args[1],"gpu_executed":true,"same_resident_handles":true,"frames":records,"scope":"production atmosphere render graph and real asynchronous readback; no Studio/material/visual qualification","visual_parity":false});
    std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(report)?
        .write_all(&serde_json::to_vec_pretty(&receipt)?)?;
    Ok(())
}
