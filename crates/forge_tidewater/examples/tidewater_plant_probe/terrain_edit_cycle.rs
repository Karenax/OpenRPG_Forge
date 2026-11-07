//! Explicit, bounded replay of the shared renderer's paint/clear/paint lifetime.
//! Only in-memory qualification documents change; admitted files stay immutable.
use super::{Probe, capture, terrain_lod};
use bevy::{
    app::AppExit,
    diagnostic::FrameCount,
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, RenderApp,
        render_resource::{CachedPipelineState, PipelineCache},
    },
};
use forge_engine::source_terrain::scene::ResidentTerrain;
use std::{path::PathBuf, sync::atomic::Ordering};

#[derive(Resource)]
pub(super) struct Cycle {
    original: forge_project::TerrainDocument,
    phase: u8,
    started: Option<f64>,
    output: PathBuf,
    observations: Vec<serde_json::Value>,
    failures: Vec<String>,
    full_rebuild: bool,
}
impl Cycle {
    pub(super) fn finished(&self) -> bool {
        self.phase == 3
    }
}

pub(super) fn install(app: &mut App) -> Result<(), String> {
    let probe = app.world().resource::<Probe>();
    let original = probe
        .terrain
        .as_ref()
        .ok_or("Terrain cycle needs a canonical raster")?
        .document()
        .clone();
    if original.material_overrides.is_empty() {
        return Err("Terrain cycle needs a painted input".into());
    }
    let output = probe.screenshot.with_extension("terrain-edit-cycle.json");
    if output.exists() {
        return Err("Existing terrain cycle evidence preserved".into());
    }
    app.insert_resource(Cycle {
        original,
        phase: 0,
        started: None,
        output,
        observations: Vec::new(),
        failures: Vec::new(),
        full_rebuild: std::env::var("FORGE_TERRAIN_REPAINT_BASELINE").as_deref() == Ok("1"),
    })
    .add_systems(Update, tick.before(capture));
    app.sub_app_mut(RenderApp)
        .add_systems(ExtractSchedule, observe);
    Ok(())
}

fn observe(mut main: ResMut<MainWorld>, pipelines: Res<PipelineCache>) {
    let Some(mut cycle) = main.get_resource_mut::<Cycle>() else {
        return;
    };
    cycle.failures = pipelines
        .pipelines()
        .filter_map(|p| {
            if let CachedPipelineState::Err(error) = &p.state {
                Some(format!("{error:?}").chars().take(4096).collect())
            } else {
                None
            }
        })
        .take(8)
        .collect();
}

fn write(cycle: &Cycle, verdict: &str, reason: &str) -> Result<(), String> {
    use std::io::Write;
    let report = serde_json::json!({"schema":"forge.source-terrain.edit-cycle.v1","verdict":verdict,"reason":reason,"phase":cycle.phase,"observations":cycle.observations,"pipeline_errors":cycle.failures,"phase_deadline_seconds":20,"maximum_frame":900,"immutable_files_modified":false,"studio_qualified":false,"performance_qualified":false});
    let mut file = std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&cycle.output)
        .map_err(|e| e.to_string())?;
    file.write_all(&serde_json::to_vec_pretty(&report).map_err(|e| e.to_string())?)
        .map_err(|e| e.to_string())
}

fn tick(world: &mut World) {
    world.resource_scope(|world,mut cycle:Mut<Cycle>| {
        if cycle.finished() { return; }
        let frame=world.resource::<FrameCount>().0;
        let now=world.resource::<Time<Real>>().elapsed_secs_f64();
        let failed=world.resource::<Probe>().failed;
        if failed || frame>=900 || cycle.started.is_some_and(|start|now-start>=20.0) {
            let reason=if failed {"GPU pipeline failed"} else {"Terrain cycle deadline exceeded"};
            if let Err(error)=write(&cycle,"REFUSED",reason) { eprintln!("Terrain cycle receipt failed: {error}"); }
            cycle.phase=3;
            world.write_message(AppExit::error());
            return;
        }
        let ready=frame>=120 && world.resource::<Probe>().ready_frames>=8
            && world.get_resource::<ResidentTerrain>().is_some_and(|terrain|terrain.normals.ready.load(Ordering::Acquire));
        if !ready { return; }
        let hash=world.resource::<Probe>().terrain.as_ref().expect("validated raster").document_hash();
        let phase=cycle.phase;
        let failures=cycle.failures.clone();
        let resident=world.resource::<ResidentTerrain>();
        let root=resident.entity.expect("terrain root");
        let material=resident.material.as_ref().expect("terrain material");
        let assets=world.resource::<Assets<forge_engine::source_terrain::lod::LodMaterial>>();
        let base=&assets.get(material).expect("live terrain material").base;
        let transition_elapsed_s=cycle.started.map(|started|now-started);
        let full_rebuild_control=cycle.full_rebuild;
        cycle.observations.push(serde_json::json!({"phase":phase,"frame":frame,"document_hash":hash,"normals_ready":true,"stable_pipeline_frames":world.resource::<Probe>().ready_frames,"pipeline_errors":failures,
            "transition_elapsed_s":transition_elapsed_s,"full_rebuild_control":full_rebuild_control,
            "root":format!("{root:?}"),"mesh":format!("{:?}",world.get::<Mesh3d>(root).expect("terrain mesh").0.id()),
            "material":format!("{:?}",material.id()),"normal":format!("{:?}",resident.normals.image.id()),
            "splat":format!("{:?}",base.splat.id()),"detail":format!("{:?}",base.detail.id())}));
        if phase==2 {
            if let Err(error)=write(&cycle,"PASS","Painted, cleared and restored programs became ready") { eprintln!("Terrain cycle receipt failed: {error}");world.write_message(AppExit::error()); }
            cycle.phase=3;
            return;
        }
        let mut document=cycle.original.clone();
        if phase==0 { document.material_overrides.clear(); }
        let plan=world.resource::<Probe>().terrain.as_ref().expect("validated raster").with_document(&document).expect("same admitted raster");
        {
            let mut probe=world.resource_mut::<Probe>();
            probe.terrain=Some(plan);
            probe.ready_frames=0;
        }
        if cycle.full_rebuild { terrain_lod::setup_full(world); } else { terrain_lod::setup(world); }
        cycle.phase+=1;
        cycle.started=Some(now);
    });
}
