//! Same-frame surface samples, tagged for asynchronous gameplay readback.
use bevy::{
    prelude::*,
    render::{
        RenderApp,
        gpu_readback::{Readback, ReadbackComplete},
        storage::ShaderBuffer,
    },
};
use forge_engine::tidewater_water_query::{PACKET_BYTES, Queries, SampleBatch};
use std::{
    path::PathBuf,
    sync::{
        Arc, Mutex,
        atomic::{AtomicBool, Ordering},
    },
};

#[derive(Clone, Default, serde::Serialize)]
pub struct Report {
    pub accepted: u64,
    pub first_live: Option<SampleBatch>,
    pub latest: Option<SampleBatch>,
}

impl Report {
    #[allow(dead_code)] // The complete scene smoke checks this; the standalone spray probe does not.
    pub fn valid(&self) -> bool {
        let (Some(first), Some(last)) = (&self.first_live, &self.latest) else {
            return false;
        };
        self.accepted > 10
            && last.step > first.step
            && last.time > first.time
            && first.count == 20
            && last.count == 20
            && Vec2::from_array(first.points[1]).distance(Vec2::from_array(last.points[1])) > 3.0
            && (2..20).any(|i| (first.values[i].height - last.values[i].height).abs() > 0.0001)
    }
}

#[derive(Resource, Clone)]
pub struct Targets {
    pub packet: Handle<ShaderBuffer>,
    pub queries: Queries,
    pub bootstrap_ready: Arc<AtomicBool>,
    output: PathBuf,
    report: Arc<Mutex<Report>>,
}

impl Targets {
    #[allow(dead_code)] // Exposed to the complete scene smoke.
    pub fn report(&self) -> Report {
        self.report
            .lock()
            .expect("water query report mutex")
            .clone()
    }
}

pub(super) fn install(app: &mut App, output: PathBuf) -> Result<(), String> {
    let input = app.sub_app(RenderApp).world().resource::<super::Input>();
    if !input.history || !input.integrate || input.coupled.is_none() {
        return Err("Water queries require the coupled ocean/shore graph".into());
    }
    let queries = Queries::default();
    queries.set_point(0, 42.0, -90.0)?;
    let player = queries.allocate("player", 1)?;
    queries.set_point(player, 42.0, -90.0)?;
    let mut slot = queries.allocate("surface qualification", 18)?;
    for x in [-120.0, -40.0, 40.0, 120.0] {
        for z in [-80.0, -20.0, 40.0, 100.0] {
            queries.set_point(slot, x, z)?;
            slot += 1;
        }
    }
    queries.set_point(slot, 400.0, 400.0)?;
    queries.set_point(slot + 1, -78.0, 58.0)?;
    let mut buffer = ShaderBuffer::from(vec![0_u32; PACKET_BYTES / 4]);
    buffer.buffer_description.usage |= wgpu::BufferUsages::COPY_SRC | wgpu::BufferUsages::COPY_DST;
    let packet = app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(buffer);
    let targets = Targets {
        packet,
        queries,
        bootstrap_ready: default(),
        output,
        report: default(),
    };
    app.insert_resource(targets.clone())
        .add_systems(Update, request);
    app.sub_app_mut(RenderApp).insert_resource(targets);
    Ok(())
}

fn request(
    mut commands: Commands,
    targets: Res<Targets>,
    spray: Res<super::Targets>,
    mut started: Local<bool>,
) {
    if *started || spray.completed_steps.load(Ordering::Acquire) < 120 {
        return;
    }
    *started = true;
    commands
        .spawn(Readback::buffer(targets.packet.clone()))
        .observe(receive);
}

fn receive(
    event: On<ReadbackComplete>,
    targets: Res<Targets>,
    mut exit: MessageWriter<bevy::app::AppExit>,
) {
    let accepted = match targets.queries.accept(&event.data) {
        Ok(accepted) => accepted,
        Err(error) => {
            error!(%error, "water query readback rejected");
            exit.write(bevy::app::AppExit::error());
            return;
        }
    };
    if !accepted {
        return;
    }
    let sample = targets.queries.latest().expect("accepted water sample");
    let mut report = targets.report.lock().expect("water query report mutex");
    report.accepted += 1;
    if sample.step > 120 && report.first_live.is_none() {
        report.first_live = Some(sample.clone());
    }
    if sample.step == 120 && !targets.bootstrap_ready.load(Ordering::Acquire) {
        let saved = serde_json::to_vec_pretty(&sample)
            .map_err(std::io::Error::other)
            .and_then(|bytes| {
                use std::io::Write;
                std::fs::OpenOptions::new()
                    .write(true)
                    .create_new(true)
                    .open(&targets.output)?
                    .write_all(&bytes)
            });
        if let Err(error) = saved {
            error!(%error, "water query bootstrap receipt failed");
            exit.write(bevy::app::AppExit::error());
            return;
        }
        targets.bootstrap_ready.store(true, Ordering::Release);
    }
    report.latest = Some(sample);
}
