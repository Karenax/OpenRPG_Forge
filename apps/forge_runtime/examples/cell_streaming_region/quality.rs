//! Native raster topology proof. No full global-profile or gameplay verdict.
use bevy::{
    app::AppExit,
    diagnostic::FrameCount,
    prelude::*,
    render::view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
};
use forge_core::{ContentHash, EntityId, canonical_json_sha256};
use forge_engine::{
    cell_streaming::{self, CellStreamingStats, StreamedTerrainCell, StreamedTerrainHlod},
    render_inventory::{self, RenderInventoryEvidence},
};
use forge_project::VisualQualityTier;
use forge_world::cell_streaming::SampledMeshQuality;
use std::{ffi::OsString, fs::OpenOptions, io::Write, path::PathBuf};

const LABELS: [&str; 5] = ["low", "medium", "high", "ultra", "restore"];

#[derive(Resource)]
struct Proof {
    directory: PathBuf,
    source: ContentHash,
    qualities: [SampledMeshQuality; 5],
    preset_source: ContentHash,
    stage: usize,
    queued: bool,
    registered: Option<u32>,
    capture: Option<u32>,
    capture_done: bool,
    reports: Vec<serde_json::Value>,
}

fn owner() -> EntityId {
    EntityId::deterministic("gate7-native-cell-mesh-quality")
}

fn qualities() -> Result<([SampledMeshQuality; 5], ContentHash), String> {
    let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .map_err(|e| e.to_string())?;
    let mut values = [SampledMeshQuality::default(); 5];
    for (index, tier) in [
        VisualQualityTier::Low,
        VisualQualityTier::Medium,
        VisualQualityTier::High,
        VisualQualityTier::Ultra,
    ]
    .into_iter()
    .enumerate()
    {
        let terrain = policy.preset(tier).terrain;
        values[index] =
            SampledMeshQuality::new(terrain.lod1_sample_stride, terrain.lod2_sample_stride)?;
    }
    Ok((values, policy.content_hash().map_err(|e| e.to_string())?))
}

pub(super) fn run(args: &[OsString]) -> AppExit {
    match prepare(args) {
        Ok(mut app) => app.run(),
        Err(error) => {
            eprintln!("{error}");
            AppExit::error()
        }
    }
}

fn prepare(args: &[OsString]) -> Result<App, String> {
    let [directory] = args else {
        return Err("Expected --mesh-quality ABSOLUTE_FRESH_OUTPUT_DIRECTORY".into());
    };
    let directory = PathBuf::from(directory);
    if !directory.is_absolute() || directory.exists() {
        return Err("Expected a fresh absolute proof directory".into());
    }
    let terrain = super::source()?;
    let source = terrain.document_hash();
    let (qualities, preset_source) = qualities()?;
    let policy = cell_streaming::sampled_policy(terrain.raster().source())?;
    let plugin = cell_streaming::SampledCellStreamingPlugin::new(
        terrain,
        policy,
        owner(),
        StandardMaterial {
            base_color: Color::srgb(0.27, 0.39, 0.18),
            perceptual_roughness: 0.95,
            ..default()
        },
        [0.0; 2],
    )?
    .with_terrain_hlod();
    std::fs::create_dir(&directory).map_err(|e| e.to_string())?;
    let mut app = App::new();
    app.add_plugins(DefaultPlugins.set(WindowPlugin {
        primary_window: Some(Window {
            title: "OpenRPG Forge — contrôle de finesse du terrain".into(),
            resolution: (1280, 800).into(),
            ..default()
        }),
        ..default()
    }))
    .add_plugins(render_inventory::RenderInventoryPlugin)
    .insert_resource(ClearColor(Color::srgb(0.32, 0.43, 0.57)))
    .insert_resource(Proof {
        directory,
        source,
        qualities,
        preset_source,
        stage: 0,
        queued: false,
        registered: None,
        capture: None,
        capture_done: false,
        reports: Vec::new(),
    });
    forge_engine::sampled_terrain_physics::register(&mut app);
    app.add_plugins(plugin)
        .add_systems(Startup, setup)
        .add_systems(Last, drive);
    Ok(app)
}

fn setup(mut commands: Commands) {
    commands.spawn((
        Camera3d::default(),
        AmbientLight {
            color: Color::WHITE,
            brightness: 180.0,
            ..default()
        },
        Transform::from_xyz(-360.0, 210.0, 360.0).looking_at(Vec3::ZERO, Vec3::Y),
    ));
    commands.spawn((
        DirectionalLight {
            illuminance: 16_000.0,
            shadow_maps_enabled: true,
            ..default()
        },
        Transform::from_xyz(100.0, 200.0, 100.0).looking_at(Vec3::ZERO, Vec3::Y),
    ));
}

fn drive(world: &mut World) {
    let Some(mut proof) = world.remove_resource::<Proof>() else {
        return;
    };
    match step(world, &mut proof) {
        Ok(true) => {
            world.write_message(AppExit::Success);
        }
        Ok(false) => {
            world.insert_resource(proof);
        }
        Err(error) => {
            error!(%error, "Sampled mesh quality proof failed");
            world.write_message(AppExit::error());
        }
    }
}

fn step(world: &mut World, proof: &mut Proof) -> Result<bool, String> {
    if world.resource::<Time<Real>>().elapsed_secs_f64() > 180.0 {
        return Err("Mesh quality GPU proof timed out".into());
    }
    let quality = proof.qualities[proof.stage];
    let identity = canonical_json_sha256(&(
        proof.source,
        quality.coarse_stride(),
        quality.distant_stride(),
        proof.stage,
    ))
    .map_err(|e| e.to_string())?;
    let generation = u64::try_from(proof.stage).map_err(|e| e.to_string())? + 1;
    if !proof.queued {
        cell_streaming::request_mesh_quality(world, proof.source, quality)?;
        proof.queued = true;
        return Ok(false);
    }
    let stats = world.resource::<CellStreamingStats>().clone();
    if let Some(error) = stats.last_error {
        return Err(error);
    }
    if stats.pending
        || cell_streaming::admitted_mesh_quality(world) != Some(quality)
        || !cell_streaming::source_is_synchronized(world, proof.source)
    {
        return Ok(false);
    }
    if proof.registered.is_none() {
        let mut meshes = Vec::new();
        let mut materials = Vec::new();
        for (mesh, material) in world.query_filtered::<(&Mesh3d, &MeshMaterial3d<StandardMaterial>),
            Or<(With<StreamedTerrainCell>, With<StreamedTerrainHlod>)>>().iter(world) {
            meshes.push(mesh.0.clone());
            if !materials.contains(&material.0) { materials.push(material.0.clone()); }
        }
        render_inventory::request(world, owner(), identity, generation, &meshes, &materials)?;
        proof.registered = Some(world.resource::<FrameCount>().0);
        return Ok(false);
    }
    let Some(token) = render_inventory::acquire(world, owner(), identity, generation) else {
        return Ok(false);
    };
    if !token.covers_frame(
        world,
        proof.registered.ok_or("Mesh admission frame absent")?,
    ) {
        return Ok(false);
    }
    let path = proof.directory.join(format!("{}.png", LABELS[proof.stage]));
    let Some(capture) = proof.capture else {
        proof.capture = Some(world.resource::<FrameCount>().0);
        world
            .spawn(Screenshot::primary_window())
            .observe(save_to_disk(path))
            .observe(|_: On<ScreenshotCaptured>, mut proof: ResMut<Proof>| {
                proof.capture_done = true;
            });
        return Ok(false);
    };
    if !proof.capture_done || !token.covers_frame(world, capture) {
        return Ok(false);
    }
    let png = std::fs::read(&path).map_err(|e| e.to_string())?;
    if !png.starts_with(b"\x89PNG\r\n\x1a\n") {
        return Err("Mesh quality screenshot is invalid".into());
    }
    record_stage(world, proof, &stats, &png, capture)
}

fn record_stage(
    world: &mut World,
    proof: &mut Proof,
    stats: &CellStreamingStats,
    png: &[u8],
    capture: u32,
) -> Result<bool, String> {
    let quality = proof.qualities[proof.stage];
    let hlod = world.resource::<cell_streaming::TerrainHlodStats>();
    proof.reports.push(serde_json::json!({"tier":LABELS[proof.stage],"coarse_stride":quality.coarse_stride(),"distant_stride":quality.distant_stride(),
        "fine_triangles":stats.resident_triangles,"distant_triangles":hlod.resident_triangles,"resident_cells":stats.resident_cells,
        "cpu_mesh_bytes":stats.cpu_mesh_bytes+hlod.cpu_mesh_bytes,"peak_staging_bytes":hlod.peak_staging_mesh_bytes,
        "committed_transitions":stats.committed_transitions,"backend_collisions_ready":true,"capture_frame":capture,
        "capture_sha256":forge_core::bytes_sha256(png),"gpu":world.resource::<RenderInventoryEvidence>()}));
    render_inventory::release(world, owner())?;
    proof.stage += 1;
    if proof.stage == LABELS.len() {
        let adapter = world
            .get_resource::<bevy::render::renderer::RenderAdapterInfo>()
            .ok_or("Render adapter identity absent")?;
        let result = serde_json::json!({"schema":"forge.gate7.sampled-mesh-quality-native.v1","passed":true,"source_hash":proof.source,
            "scope":"Actual raster CPU topology and mesh/material GPU allocation/fence, near backend shapes, five captures; no full global-profile/gameplay/performance verdict",
            "region_extent_m":[2048,2048],"raster_bytes":4_194_304,"requested_resolution":[1280,800],
            "renderer":{"name":adapter.name,"driver":adapter.driver,"driver_info":adapter.driver_info,"backend":format!("{:?}",adapter.backend)},
            "stages":proof.reports,"topology_preset_source_hash":proof.preset_source,"gate_closed":false});
        let mut file = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(proof.directory.join("proof.json"))
            .map_err(|e| e.to_string())?;
        file.write_all(&serde_json::to_vec_pretty(&result).map_err(|e| e.to_string())?)
            .and_then(|()| file.sync_all())
            .map_err(|e| e.to_string())?;
        return Ok(true);
    }
    proof.queued = false;
    proof.registered = None;
    proof.capture = None;
    proof.capture_done = false;
    Ok(false)
}
