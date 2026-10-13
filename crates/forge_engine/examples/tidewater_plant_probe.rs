//! Native Bevy palm/material integration probe. Not a qualified scene or Studio asset.
#![allow(clippy::needless_pass_by_value)] // Bevy system parameters.
use bevy::{
    app::AppExit,
    asset::RenderAssetUsages,
    diagnostic::FrameCount,
    light::NotShadowCaster,
    mesh::Indices,
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, RenderApp,
        render_resource::{CachedPipelineState, PipelineCache, PrimitiveTopology},
        renderer::RenderGraph,
        storage::ShaderBuffer,
        view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    },
    shader::ShaderCacheError,
    window::{
        Monitor, MonitorSelection, PrimaryMonitor, PrimaryWindow, VideoModeSelection, WindowMode,
    },
};
use serde::Deserialize;
use std::{collections::BTreeMap, error::Error, path::PathBuf};
#[path = "tidewater_plant_probe/acquired_environment.rs"]
mod acquired_environment;
#[path = "tidewater_atmosphere_probe/mod.rs"]
mod atmosphere;
#[path = "tidewater_plant_probe/breaker_lip.rs"]
mod breaker_lip;
#[path = "tidewater_breaker_probe/mod.rs"]
mod breakers;
#[path = "tidewater_plant_probe/building_package.rs"]
mod building_package;
#[path = "tidewater_plant_probe/building_placements.rs"]
mod building_placements;
#[path = "tidewater_plant_probe/canopy.rs"]
mod canopy;
#[path = "tidewater_plant_probe/caustics_proof.rs"]
mod caustics_proof;
#[allow(dead_code)]
#[path = "tidewater_cloud_weather_probe/mod.rs"]
mod clouds;
#[path = "tidewater_plant_probe/debris.rs"]
mod debris;
#[path = "tidewater_plant_probe/fish_stand.rs"]
mod fish_stand;
#[path = "tidewater_plant_probe/full_stall.rs"]
mod full_stall;
#[path = "tidewater_plant_probe/grass.rs"]
mod grass;
#[path = "tidewater_plant_probe/hill_shadow.rs"]
mod hill_shadow;
#[path = "tidewater_object_shadow_probe/mod.rs"]
mod object_shadow;
#[path = "tidewater_ocean_spectrum_probe/mod.rs"]
mod ocean;
#[path = "tidewater_plant_probe/ocean_geometry.rs"]
mod ocean_geometry;
#[path = "tidewater_plant_probe/pebbles.rs"]
mod pebbles;
#[path = "tidewater_plant_probe/picking_proof.rs"]
mod picking_proof;
#[path = "tidewater_plant_probe/refraction_proof.rs"]
mod refraction_proof;
#[path = "tidewater_plant_probe/scanned_debris.rs"]
mod scanned_debris;
#[path = "tidewater_plant_probe/shore_field.rs"]
mod shore_field;
#[path = "tidewater_shore_sim_probe/mod.rs"]
mod shore_sim;
#[path = "tidewater_plant_probe/sky.rs"]
mod sky;
#[path = "tidewater_plant_probe/source_fish.rs"]
mod source_fish;
#[path = "tidewater_spray_probe/mod.rs"]
mod spray;
#[path = "tidewater_plant_probe/spray_sprites.rs"]
mod spray_sprites;
#[path = "tidewater_plant_probe/temporal_aux.rs"]
mod temporal_aux;
#[path = "tidewater_plant_probe/temporal_owned.rs"]
mod temporal_owned;
#[path = "tidewater_plant_probe/terrain_edits.rs"]
mod terrain_edits;
#[path = "tidewater_plant_probe/terrain_lod.rs"]
mod terrain_lod;
#[path = "tidewater_plant_probe/terrain_normals.rs"]
mod terrain_normals;
#[path = "tidewater_plant_probe/terrain_surface.rs"]
mod terrain_surface;
#[path = "tidewater_plant_probe/vegetation_scene.rs"]
mod vegetation_scene;
#[path = "tidewater_plant_probe/village.rs"]
mod village;
#[path = "tidewater_plant_probe/walk.rs"]
mod walk;
#[path = "tidewater_plant_probe/walk_clouds.rs"]
mod walk_clouds;
#[path = "tidewater_plant_probe/walk_grass.rs"]
mod walk_grass;
#[path = "tidewater_plant_probe/walk_wind.rs"]
mod walk_wind;
#[path = "tidewater_plant_probe/water_optics.rs"]
mod water_optics;
#[path = "tidewater_plant_probe/water_scene.rs"]
mod water_scene;
#[path = "tidewater_plant_probe/water_time.rs"]
mod water_time;
#[path = "tidewater_plant_probe/workspace_vegetation.rs"]
mod workspace_vegetation;

use forge_engine::tidewater_plants::{LOBE, MAT, Params as ProbeParams, PlantMaterial, VEG};

#[derive(Deserialize)]
struct Instance {
    x: f32,
    y: f32,
    z: f32,
    s: f32,
    #[serde(default = "unit_scale")]
    sy: f32,
    yaw: f32,
    la: f32,
    l: f32,
    #[serde(rename = "H")]
    height: f32,
    seed: f32,
}
fn unit_scale() -> f32 {
    1.0
}
#[derive(Clone, Deserialize)]
struct LeafTexture {
    width: u32,
    height: u32,
    mips: u32,
}
#[derive(Deserialize)]
struct Fixture {
    camera_pose: Option<CameraPose>,
    schema: String,
    version: u32,
    instance: Instance,
    files: BTreeMap<String, String>,
    leaf_texture: Option<LeafTexture>,
    impostor_texture: Option<LeafTexture>,
    camera_offset: Option<[f32; 3]>,
}
#[derive(Resource)]
struct Probe {
    authored_start: Option<forge_project::zone_authoring::start::ZoneStart>,
    grass: Option<grass::Grass>,
    hill_heights: Option<hill_shadow::Input>,
    shadow_readback: ShadowReadback,
    object_readback: ShadowReadback,
    cloud_readback: ShadowReadback,
    environment: Option<Image>,
    vegetation_scene: Option<vegetation_scene::Vegetation>,
    plant_projection: PlantProjection,
    canopy: Option<Vec<canopy::Record>>,
    village: Option<village::Village>,
    fish_stand: Option<forge_engine::tidewater_stall::FishStandPrepared>,
    fish_stand_content: Option<fish_stand::FishStandContent>,
    lod_heights: Option<Vec<u8>>,
    camera_pose: Option<CameraPose>,
    terrain_textures: Option<[Vec<u8>; 3]>,
    terrain_package: Option<forge_assets::tidewater_terrain::TerrainPackage>,
    terrain: Option<forge_world::sampled_terrain::SampledTerrainPlan>,
    mode: ProbeMode,
    mesh: Mesh,
    detail: Vec<u8>,
    leaf: Vec<u8>,
    leaf_texture: LeafTexture,
    impostors: Option<(LeafTexture, Vec<u8>, Vec<u8>)>,
    camera_offset: Vec3,
    instance: Instance,
    screenshot: PathBuf,
    captured: bool,
    requested: bool,
    ready_frames: u32,
    failed: bool,
}

#[derive(Resource)]
struct ProbeEnvironment(Handle<Image>);

/// Values published by setup into the static visible plant materials.
#[derive(Resource, serde::Serialize)]
struct StaticVegetationPose {
    wind_time: [f32; 4],
    gust_offset: [f32; 2],
    frame_index: u32,
    acquired_source_frame: bool,
}

#[derive(Clone, Copy)]
enum PlantProjection {
    Static,
    ResidentNavigation,
    Walk,
    WalkSmoke,
    WalkLongSmoke,
}

#[derive(PartialEq, Eq)]
enum ShadowReadback {
    Pending,
    Complete,
}

#[derive(Clone, Copy, PartialEq, Eq)]
enum ProbeMode {
    Surface,
    Silhouette,
    VegetationDirect,
    VegetationEnvironment,
    SceneLighting,
    GrassCoverage,
    TerrainCoverage,
    VillageAlbedo,
}

#[derive(Deserialize)]
struct CameraPose {
    position: [f32; 3],
    rotation: [f32; 4],
    fov_degrees: f32,
}

fn read_vectors<const N: usize>(bytes: &[u8]) -> Result<Vec<[f32; N]>, Box<dyn Error>> {
    if N == 0 || !bytes.len().is_multiple_of(N * 4) {
        return Err("invalid attribute size".into());
    }
    bytes
        .as_chunks::<4>()
        .0
        .as_chunks::<N>()
        .0
        .iter()
        .map(|chunk| {
            let mut result = [0.0; N];
            for (index, value) in chunk.iter().enumerate() {
                result[index] = f32::from_le_bytes(*value);
                if !result[index].is_finite() {
                    return Err("nonfinite attribute".into());
                }
            }
            Ok(result)
        })
        .collect()
}

#[expect(
    clippy::too_many_lines,
    reason = "bounded standalone asset validation and probe setup"
)]
fn main() -> Result<(), Box<dyn Error>> {
    let mut args = std::env::args_os().skip(1);
    let root = PathBuf::from(args.next().ok_or("fixture directory required")?).canonicalize()?;
    let screenshot = PathBuf::from(args.next().ok_or("new screenshot path required")?);
    let option = args.next();
    let with_water_clouds = option.as_ref().is_some_and(|v| v == "--water-spray-clouds");
    let with_clouds = with_water_clouds || option.as_ref().is_some_and(|v| v == "--sky-clouds");
    let with_native_spray_budget = with_water_clouds
        || option
            .as_ref()
            .is_some_and(|v| v == "--water-spray-native-budget");
    let with_spray =
        with_native_spray_budget || option.as_ref().is_some_and(|v| v == "--water-spray");
    let with_breakers = with_spray
        || option
            .as_ref()
            .is_some_and(|v| v == "--water-breaker-compute");
    let with_shore_material =
        with_breakers || option.as_ref().is_some_and(|v| v == "--water-shore-sim");
    let with_shore_compute = with_shore_material
        || option
            .as_ref()
            .is_some_and(|v| v == "--water-shore-compute");
    let with_optics = with_shore_compute || option.as_ref().is_some_and(|v| v == "--water-optics");
    let water_diagnostic = match option.as_ref().and_then(|v| v.to_str()) {
        Some("--ocean-scene-color") => 1,
        Some("--ocean-scene-depth") => 2,
        _ => 0,
    };
    let with_geometry = with_optics
        || water_diagnostic != 0
        || option.as_ref().is_some_and(|v| v == "--ocean-geometry");
    let with_ocean = with_geometry || option.as_ref().is_some_and(|v| v == "--sky-ocean-compute");
    let with_sky = with_clouds || with_ocean || option.as_ref().is_some_and(|v| v == "--sky");
    let with_object_filter = with_sky || option.as_ref().is_some_and(|v| v == "--object-shadows");
    let with_object_depth = with_clouds
        || with_ocean
        || option.as_ref().is_some_and(|v| {
            v == "--scene-shadow-depth" || v == "--object-shadows" || v == "--sky"
        });
    let mode = match option {
        Some(value)
            if value == "--sky"
                || value == "--sky-clouds"
                || value == "--sky-ocean-compute"
                || value == "--ocean-geometry" =>
        {
            ProbeMode::SceneLighting
        }
        Some(value)
            if value == "--ocean-scene-color"
                || value == "--ocean-scene-depth"
                || value == "--water-optics"
                || value == "--water-shore-compute"
                || value == "--water-shore-sim"
                || value == "--water-breaker-compute"
                || value == "--water-spray"
                || value == "--water-spray-native-budget"
                || value == "--water-spray-clouds" =>
        {
            ProbeMode::SceneLighting
        }
        None => ProbeMode::Surface,
        Some(value) if value == "--silhouette" => ProbeMode::Silhouette,
        Some(value) if value == "--vegetation-direct" => ProbeMode::VegetationDirect,
        Some(value) if value == "--vegetation-environment" => ProbeMode::VegetationEnvironment,
        Some(value) if value == "--scene-lighting" => ProbeMode::SceneLighting,
        Some(value) if value == "--scene-shadow-depth" || value == "--object-shadows" => {
            ProbeMode::SceneLighting
        }
        Some(value) if value == "--grass-mask" => ProbeMode::GrassCoverage,
        Some(value) if value == "--terrain-mask" => ProbeMode::TerrainCoverage,
        Some(value) if value == "--village-albedo" => ProbeMode::VillageAlbedo,
        Some(_) => return Err("unknown probe option".into()),
    };
    let ocean_root = if with_ocean {
        Some(PathBuf::from(args.next().ok_or("ocean fixture required")?))
    } else {
        None
    };
    let ocean_output = screenshot.with_extension("ocean");
    let shore_root = if with_shore_compute {
        Some(PathBuf::from(
            args.next().ok_or("shore simulation fixture required")?,
        ))
    } else {
        None
    };
    let shore_output = screenshot.with_extension("shore");
    let breaker_root = if with_breakers {
        Some(PathBuf::from(
            args.next().ok_or("breaker fixture required")?,
        ))
    } else {
        None
    };
    let breaker_output = screenshot.with_extension("breakers");
    let spray_output = screenshot.with_extension("spray");
    let cloud_root = if with_clouds {
        Some(PathBuf::from(args.next().ok_or("cloud fixture required")?))
    } else {
        None
    };
    let mut studio_snapshot = None;
    let workspace_zone = match args.next() {
        None => None,
        Some(flag) if flag == "--workspace-vegetation" => Some((
            PathBuf::from(args.next().ok_or("workspace root required")?),
            args.next()
                .ok_or("workspace area id required")?
                .to_str()
                .ok_or("invalid area id")?
                .parse::<forge_core::AreaId>()?,
        )),
        Some(flag) if flag == "--studio-snapshot" => {
            studio_snapshot = Some((
                PathBuf::from(args.next().ok_or("snapshot directory required")?),
                args.next()
                    .ok_or("snapshot hash required")?
                    .to_str()
                    .ok_or("invalid snapshot hash")?
                    .parse::<forge_core::ContentHash>()?,
            ));
            None
        }
        Some(_) => return Err("unexpected additional option".into()),
    };
    let package_input = match args.next() {
        None => None,
        Some(flag) if flag == "--building-package" => Some((
            PathBuf::from(args.next().ok_or("building package root required")?),
            args.next()
                .ok_or("building package hash required")?
                .to_str()
                .ok_or("invalid package hash")?
                .parse::<forge_core::ContentHash>()?,
        )),
        Some(_) => return Err("unexpected additional option".into()),
    };
    let plant_projection = match args.next() {
        None => PlantProjection::Static,
        Some(flag) if flag == "--resident-plant-navigation" => PlantProjection::ResidentNavigation,
        Some(flag) if flag == "--walk" => PlantProjection::Walk,
        Some(flag) if flag == "--walk-smoke" => PlantProjection::WalkSmoke,
        Some(flag) if flag == "--walk-smoke-long" => PlantProjection::WalkLongSmoke,
        Some(_) => return Err("unexpected additional option".into()),
    };
    if args.next().is_some() {
        return Err("unexpected additional option".into());
    }
    if screenshot.exists() {
        return Err("refusing to overwrite screenshot".into());
    }
    let fixture: Fixture = serde_json::from_slice(&std::fs::read(root.join("fixture.json"))?)?;
    if fixture.schema != "forge.tidewater.plant-probe" || ![1, 2].contains(&fixture.version) {
        return Err("invalid fixture".into());
    }
    let mut files = BTreeMap::new();
    for (name, hash) in fixture.files {
        if name.contains(['/', '\\', ':']) || name.starts_with('.') {
            return Err("unsafe fixture path".into());
        }
        let path = root.join(&name).canonicalize()?;
        if !path.starts_with(&root) || path.metadata()?.len() > 32 * 1024 * 1024 {
            return Err("fixture bound".into());
        }
        let bytes = std::fs::read(path)?;
        if forge_core::bytes_sha256(&bytes).to_string() != hash {
            return Err("fixture hash mismatch".into());
        }
        files.insert(name, bytes);
    }
    if let Some((workspace, area)) = workspace_zone {
        workspace_vegetation::load(&workspace, area, &mut files)?;
    }
    if let Some((directory, hash)) = studio_snapshot {
        workspace_vegetation::load_snapshot(&directory, hash, &mut files)?;
    }
    if let Some((package, hash)) = package_input {
        building_package::load(&package, hash, &mut files)?;
    }
    building_placements::apply(&mut files)?;
    let static_assets = static_asset_placements::load(&files)?;
    let acquired_environment = acquired_environment::load(
        &mut files,
        fixture.camera_pose.as_ref(),
        matches!(plant_projection, PlantProjection::Static),
    )?;
    if acquired_environment.is_some() && !with_clouds {
        return Err("Acquired environment requires the shared sky/cloud scene".into());
    }
    let get = |name: &str| files.get(name).ok_or("missing fixture file");
    if files.contains_key("spray-sprites.wgsl") != with_spray {
        return Err("spray material mode and fixture must match".into());
    }
    if files.contains_key("uniform-shoreSimP.bin") != with_shore_material {
        return Err("shore material mode and fixture must match".into());
    }
    if let Some(shore_root) = &shore_root {
        let meta: serde_json::Value =
            serde_json::from_slice(&std::fs::read(shore_root.join("shore-sim.json"))?)?;
        if meta["terrain_sha256"] != forge_core::bytes_sha256(get("heights.bin")?).to_string() {
            return Err("shore simulation terrain differs from scene".into());
        }
        if with_shore_material
            && std::fs::read(shore_root.join("simulation-params.bin"))?
                != *get("uniform-shoreSimP.bin")?
        {
            return Err("shore simulation parameters differ from material".into());
        }
    }
    if files.contains_key("water-optics.wgsl") != with_optics {
        return Err("water optical mode and fixture must match".into());
    }
    if files.contains_key("ocean-geometry.wgsl") != with_geometry {
        return Err("ocean geometry mode and fixture must match".into());
    }
    if files.contains_key("sky.wgsl") != with_sky {
        return Err("sky mode and prepared sky fixture must match".into());
    }
    if files.contains_key("cloud-view-params.bin") != with_clouds {
        return Err("cloud sky mode and prepared fixture must match".into());
    }
    if files.contains_key("object-shadow.wgsl") && !with_object_depth {
        return Err("object shadow receiver fixture requires native depth mode".into());
    }
    if with_object_filter && !files.contains_key("object-shadow.wgsl") {
        return Err("object shadow mode requires receiver programs".into());
    }
    let positions = read_vectors::<3>(get("position.bin")?)?;
    let normals = read_vectors::<3>(get("normal.bin")?)?;
    let uv = read_vectors::<2>(get("uv.bin")?)?;
    let veg = read_vectors::<4>(get("aVeg.bin")?)?;
    let mat = read_vectors::<4>(get("aMat.bin")?)?;
    let lobe = read_vectors::<4>(get("aLobe.bin")?)?;
    if [normals.len(), uv.len(), veg.len(), mat.len(), lobe.len()]
        .into_iter()
        .any(|n| n != positions.len())
    {
        return Err("attribute counts differ".into());
    }
    let index_bytes = get("indices.bin")?;
    if !index_bytes.len().is_multiple_of(12) {
        return Err("invalid triangles".into());
    }
    let indices: Vec<_> = index_bytes
        .as_chunks::<4>()
        .0
        .iter()
        .map(|chunk| u32::from_le_bytes(*chunk))
        .collect();
    if indices.iter().any(|&i| i as usize >= positions.len()) {
        return Err("invalid index".into());
    }
    let mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    )
    .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, positions)
    .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, normals)
    .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, uv)
    .with_inserted_attribute(VEG, veg)
    .with_inserted_attribute(MAT, mat)
    .with_inserted_attribute(LOBE, lobe)
    .with_inserted_indices(Indices::U32(indices));
    let detail = get("detail.bin")?.clone();
    if detail.len() != 512 * 512 * 4 {
        return Err("invalid detail texture size".into());
    }
    let (leaf_texture, leaf) = if let Some(meta) = fixture.leaf_texture {
        if meta.width == 0
            || meta.height == 0
            || meta.width > 2048
            || meta.height > 2048
            || !meta.width.is_power_of_two()
            || !meta.height.is_power_of_two()
            || meta.mips != meta.width.max(meta.height).ilog2() + 1
        {
            return Err("invalid leaf texture".into());
        }
        let expected: u64 = (0..meta.mips)
            .map(|level| {
                u64::from((meta.width >> level).max(1))
                    * u64::from((meta.height >> level).max(1))
                    * 4
            })
            .sum();
        let bytes = get("leaf.bin")?.clone();
        if bytes.len() as u64 != expected {
            return Err("invalid leaf mip payload".into());
        }
        (meta, bytes)
    } else {
        if fixture.version != 1 {
            return Err("missing leaf metadata".into());
        }
        (
            LeafTexture {
                width: 1,
                height: 1,
                mips: 1,
            },
            vec![255; 4],
        )
    };
    let impostors = if let Some(meta) = fixture.impostor_texture {
        if meta.width != 3840 || meta.height != 768 || meta.mips != 12 {
            return Err("invalid source impostor dimensions".into());
        }
        let expected: usize = (0..meta.mips)
            .map(|level| {
                ((meta.width >> level).max(1) * (meta.height >> level).max(1) * 4) as usize
            })
            .sum();
        let a = get("impostor-a.bin")?.clone();
        let b = get("impostor-b.bin")?.clone();
        if a.len() != expected || b.len() != expected {
            return Err("invalid impostor mip payload".into());
        }
        Some((meta, a, b))
    } else {
        None
    };
    let camera_offset = Vec3::from_array(fixture.camera_offset.unwrap_or([15.0, 8.0, 19.0]));
    if !camera_offset.is_finite()
        || camera_offset.length() > 10000.0
        || camera_offset.length() < 1.0
    {
        return Err("invalid camera offset".into());
    }
    let instance = fixture.instance;
    if let Some(pose) = &fixture.camera_pose {
        let position = Vec3::from_array(pose.position);
        let rotation = Quat::from_array(pose.rotation);
        if !position.is_finite()
            || position.length() > 10000.0
            || !rotation.is_finite()
            || (rotation.length_squared() - 1.0).abs() > 0.0001
            || !(5.0..=150.0).contains(&pose.fov_degrees)
        {
            return Err("invalid explicit camera pose".into());
        }
    }
    if ![
        instance.x,
        instance.y,
        instance.z,
        instance.s,
        instance.sy,
        instance.yaw,
        instance.la,
        instance.l,
        instance.height,
        instance.seed,
    ]
    .into_iter()
    .all(f32::is_finite)
        || instance.s <= 0.0
        || instance.sy == 0.0
        || instance.height <= 0.0
    {
        return Err("invalid instance".into());
    }
    let mut app = App::new();
    let with_temporal_aux = std::env::var("FORGE_TIDEWATER_TEMPORAL_AUX").as_deref() == Ok("1");
    let with_temporal_owned =
        std::env::var("FORGE_TIDEWATER_TEMPORAL_RENDER_OWNED").as_deref() == Ok("1");
    if with_temporal_owned && !with_temporal_aux {
        return Err("Render-owned proof requires real temporal auxiliary draws".into());
    }
    let mut terrain_textures: Option<[Vec<u8>; 3]> = if files.contains_key("terrain.wgsl") {
        let mut textures = Vec::new();
        for (name, size) in [("normal", 2048_u32), ("splat", 2048), ("detail", 512)] {
            let data = get(&format!("terrain-{name}.bin"))?.clone();
            let expected: usize = (0..=size.ilog2())
                .map(|level| ((size >> level).pow(2) * 4) as usize)
                .sum();
            if data.len() != expected {
                return Err("invalid terrain mip chain".into());
            }
            textures.push(data);
        }
        Some(textures.try_into().map_err(|_| "terrain texture count")?)
    } else {
        None
    };
    let terrain = if let Some(encoded) = files.get("terrain.json") {
        let document = forge_project::TerrainDocument::from_json(std::str::from_utf8(encoded)?)?;
        Some(
            forge_world::sampled_terrain::SampledTerrainPlan::from_document(
                &document,
                get("heights.bin")?,
            )?,
        )
    } else {
        None
    };
    if terrain_textures.is_some() {
        let source = terrain
            .as_ref()
            .ok_or("terrain material needs a terrain document")?
            .document()
            .sampled_source
            .as_ref()
            .ok_or("missing raster source")?;
        if source.width != 2048
            || source.height != 2048
            || source.sample_spacing_m.to_bits() != 1.0_f64.to_bits()
        {
            return Err("terrain material domain mismatch".into());
        }
    }
    let lod_heights = if files.contains_key("terrain-lod.wgsl") {
        let document = terrain.as_ref().ok_or("LOD requires terrain")?.document();
        if terrain_textures.is_none()
            || document
                .sampled_source
                .as_ref()
                .is_none_or(|source| source.outside_height_m.to_bits() != (-90.0_f32).to_bits())
        {
            return Err("LOD probe requires the source raster domain".into());
        }
        Some(get("heights.bin")?.clone())
    } else {
        None
    };
    if let (Some(plan), Some(textures)) = (&terrain, &mut terrain_textures)
        && (!plan.document().height_deltas.is_empty()
            || plan.document().height_sampling.base_elevation_m != 0.0)
    {
        terrain_normals::update_base(plan, &mut textures[0])?;
    }
    if mode == ProbeMode::TerrainCoverage && lod_heights.is_none() {
        return Err("terrain coverage requires a LOD fixture".into());
    }
    let village = village::load(&files)?;
    let fish_stand = fish_stand::load(&root)?;
    if fish_stand.is_some() && village.is_none() {
        return Err("FishStand requires the pinned village scene lighting".into());
    }
    let fish_stand_content = fish_stand::load_content(&root)?;
    let canopy = canopy::load(&files)?;
    let vegetation_scene = vegetation_scene::load(&files)?;
    let grass = grass::load(&files)?;
    if (grass.is_some() && (lod_heights.is_none() || fixture.camera_pose.is_none()))
        || (mode == ProbeMode::GrassCoverage && grass.is_none())
    {
        return Err("grass requires source terrain and camera".into());
    }
    let environment = files
        .get("environment-cube.bin")
        .map(|bytes| forge_engine::tidewater_environment::cube_rgba16f(128, 6, bytes))
        .transpose()?;
    let hill_heights = hill_shadow::load(&files)?;
    let has_hill_shadow = hill_heights.is_some();
    if mode == ProbeMode::SceneLighting
        && (environment.is_none()
            || !files.contains_key("scene-lighting.wgsl")
            || vegetation_scene.is_none()
            || village.is_none()
            || lod_heights.is_none())
    {
        return Err(
            "scene lighting requires terrain, village, vegetation and source lighting".into(),
        );
    }
    if mode == ProbeMode::VegetationEnvironment
        && (environment.is_none() || vegetation_scene.is_none())
    {
        return Err("environment mode requires vegetation and acquired sky".into());
    }
    if mode == ProbeMode::VegetationDirect
        && (vegetation_scene.is_none() || !files.contains_key("source-direct-brdf.wgsl"))
    {
        return Err("direct vegetation mode requires source lighting fixture".into());
    }
    if vegetation_scene.is_some() && fixture.camera_pose.is_none() {
        return Err("vegetation scene requires an explicit camera".into());
    }
    if mode == ProbeMode::VillageAlbedo && village.is_none() {
        return Err("village albedo requires village data".into());
    }
    let terrain_package = if lod_heights.is_some() {
        Some(terrain_lod::package(&files)?)
    } else {
        None
    };
    // Opt-in fixed-pose comparison: visible plants and depth casters consume
    // the same retained source frame. Live play keeps its own wind clock.
    if std::env::var("FORGE_TIDEWATER_STATIC_SOURCE_FRAME").as_deref() == Ok("1") {
        if matches!(
            plant_projection,
            PlantProjection::Walk | PlantProjection::WalkSmoke | PlantProjection::WalkLongSmoke
        ) {
            return Err("Acquired static source frame is unavailable during live play".into());
        }
        app.insert_resource(forge_engine::tidewater_wind::AcquiredFrame::reference()?);
    }
    app.insert_resource(Probe {
        authored_start: files
            .get("canonical-zone.json")
            .map(|bytes| {
                forge_project::zone_authoring::session::ZoneEditSession::from_archive_bytes(bytes)
            })
            .transpose()?
            .and_then(|session| session.head().corrections.start.as_deref().copied()),
        grass,
        hill_heights,
        object_readback: if with_object_depth {
            ShadowReadback::Pending
        } else {
            ShadowReadback::Complete
        },
        shadow_readback: if has_hill_shadow {
            ShadowReadback::Pending
        } else {
            ShadowReadback::Complete
        },
        cloud_readback: if with_clouds
            && std::env::var("FORGE_TIDEWATER_STATIC_CLOUD_READBACK").as_deref() == Ok("1")
        {
            ShadowReadback::Pending
        } else {
            ShadowReadback::Complete
        },
        environment,
        vegetation_scene,
        plant_projection,
        canopy,
        village,
        fish_stand,
        fish_stand_content,
        lod_heights,
        camera_pose: fixture.camera_pose,
        terrain_package,
        terrain_textures,
        terrain,
        mode,
        mesh,
        detail,
        leaf,
        leaf_texture,
        impostors,
        camera_offset,
        instance,
        screenshot,
        captured: false,
        requested: false,
        ready_frames: 0,
        failed: false,
    })
    .insert_resource(ClearColor(if mode == ProbeMode::Surface {
        Color::srgb(0.22, 0.30, 0.38)
    } else {
        Color::BLACK
    }))
    .add_plugins({
        let plugins = DefaultPlugins
            .set(AssetPlugin {
                file_path: root.to_string_lossy().into_owned(),
                ..default()
            })
            .set(WindowPlugin {
                primary_window: Some(Window {
                    title: "OpenRPG Forge — Tidewater".into(),
                    resolution: bevy::window::WindowResolution::new(1920, 1080)
                        .with_scale_factor_override(1.0),
                    mode: if matches!(plant_projection, PlantProjection::Walk) {
                        WindowMode::BorderlessFullscreen(MonitorSelection::Primary)
                    } else {
                        WindowMode::Windowed
                    },
                    resizable: matches!(plant_projection, PlantProjection::Walk),
                    enabled_buttons: bevy::window::EnabledButtons {
                        maximize: matches!(plant_projection, PlantProjection::Walk),
                        ..default()
                    },
                    ..default()
                }),
                ..default()
            });
        if with_temporal_aux && !with_temporal_owned {
            plugins.disable::<bevy::render::pipelined_rendering::PipelinedRenderingPlugin>()
        } else {
            plugins
        }
    })
    .add_plugins(forge_engine::finite_perspective::FinitePerspectivePlugin)
    .add_plugins(MaterialPlugin::<PlantMaterial>::default())
    .add_plugins(MaterialPlugin::<terrain_surface::TerrainMaterial>::default())
    .add_plugins(MaterialPlugin::<terrain_lod::LodMaterial>::default())
    .add_plugins(MaterialPlugin::<village::VillageMaterial>::default())
    .add_plugins(MaterialPlugin::<
        forge_engine::tidewater_debris::nature::NatureMaterial,
    >::default())
    .add_plugins(MaterialPlugin::<
        forge_engine::tidewater_debris::scanned_material::ScannedMaterial,
    >::default())
    .add_plugins(MaterialPlugin::<
        forge_engine::tidewater_debris::pebble_material::PebbleMaterial,
    >::default())
    .add_plugins(MaterialPlugin::<
        forge_engine::tidewater_stall::FishStandMaterial,
    >::default())
    .add_plugins(MaterialPlugin::<
        forge_engine::tidewater_stall::full_material::FullMaterial,
    >::default())
    .add_plugins(MaterialPlugin::<sky::SkyMaterial>::default())
    .add_plugins(MaterialPlugin::<ocean_geometry::OceanGeometryMaterial>::default())
    .add_plugins(MaterialPlugin::<water_optics::WaterMaterial>::default())
    .add_plugins(terrain_normals::NormalPlugin)
    .add_systems(
        Startup,
        (
            hill_shadow::setup,
            terrain_normals::setup,
            setup_environment,
            setup,
            setup_terrain,
            terrain_lod::setup,
            village::setup,
            debris::setup,
            pebbles::setup,
            scanned_debris::setup,
            fish_stand::setup,
            sky::setup,
            ocean_geometry::setup,
            water_optics::setup,
        )
            .chain(),
    )
    .add_systems(Update, (picking_proof::start_if_ready, capture).chain())
    .add_systems(PostUpdate, debris::update)
    .add_systems(
        PostUpdate,
        pebbles::update.after(bevy::camera::visibility::VisibilitySystems::UpdateFrusta),
    )
    .add_systems(
        PostUpdate,
        scanned_debris::update
            .after(bevy::camera::visibility::VisibilitySystems::UpdateFrusta)
            .before(bevy::asset::AssetEventSystems)
            .before(bevy::camera::visibility::VisibilitySystems::CheckVisibility),
    )
    .add_systems(
        PostUpdate,
        water_optics::update_camera.after(bevy::transform::TransformSystems::Propagate),
    )
    .add_systems(
        PostUpdate,
        water_optics::update_lighting.after(water_optics::update_camera),
    );
    forge_engine::tidewater_terrain::scene::register(&mut app)?;
    if let Some(input) = scanned_debris::load()? {
        if !with_object_depth {
            return Err("Native scanned Debris requires the shared object shadow graph".into());
        }
        app.insert_resource(scanned_debris::Requested);
        app.insert_resource(input);
    }
    if let Some(input) = debris::load()? {
        if !with_object_depth {
            return Err("Native Debris requires the shared object shadow graph".into());
        }
        app.insert_resource(object_shadow::AdditionalOpaqueDraws(input.shadow_draws()?));
        app.insert_resource(debris::Requested);
        app.insert_resource(input);
    }
    if pebbles::requested() {
        if !app.world().contains_resource::<debris::Requested>() {
            return Err("Native Pebbles require the verified Debris package".into());
        }
        app.insert_resource(pebbles::Requested);
    }
    if with_object_depth
        && let Some(draw) = fish_stand::shadow_draw(app.world().resource::<Probe>())?
    {
        if let Some(mut additional) = app
            .world_mut()
            .get_resource_mut::<object_shadow::AdditionalOpaqueDraws>()
        {
            additional.0.push(draw);
        } else {
            app.insert_resource(object_shadow::AdditionalOpaqueDraws(vec![draw]));
        }
    }
    if with_geometry {
        water_scene::install(&mut app);
        app.insert_resource(ocean_geometry::Enabled(water_diagnostic));
    }
    if with_optics {
        forge_engine::tidewater_refraction::register(&mut app)?;
        water_optics::install(&mut app, &files)?;
        app.add_systems(
            PostUpdate,
            (
                water_optics::select_refraction_camera
                    .before(forge_engine::tidewater_refraction::Synchronize),
                water_optics::update_refraction
                    .after(forge_engine::tidewater_refraction::Synchronize),
            ),
        );
    }
    if has_hill_shadow {
        forge_engine::tidewater_hill_shadow::install_shader(
            &mut app,
            std::str::from_utf8(files.get("hill-shadow.wgsl").ok_or("Missing hill shader")?)?,
        )?;
        app.add_plugins(forge_engine::tidewater_hill_shadow::HillShadowPlugin);
    }
    if let Some(plan) = &static_assets {
        let draws = plan.shadow_draws()?;
        if let Some(mut extra) = app
            .world_mut()
            .get_resource_mut::<object_shadow::AdditionalOpaqueDraws>()
        {
            extra.0.extend(draws);
        } else {
            app.insert_resource(object_shadow::AdditionalOpaqueDraws(draws));
        }
    }
    if with_object_depth {
        if matches!(
            plant_projection,
            PlantProjection::Walk | PlantProjection::WalkSmoke | PlantProjection::WalkLongSmoke
        ) {
            object_shadow::live::install(&mut app, &root, &files)?;
        } else {
            object_shadow::install(&mut app, &root, &files)?;
        }
    } else {
        object_shadow::install_disabled(&mut app);
    }
    if with_sky {
        forge_engine::tidewater_sky::install_shader(
            &mut app,
            std::str::from_utf8(get("sky.wgsl")?)?,
        )?;
        app.insert_resource(forge_engine::tidewater_clouds::quality::CloudTraceQuality::Dense);
        if let Some(acquired) = acquired_environment {
            let targets = acquired.install(&mut app)?;
            sky::install_clouds(&mut app, get("cloud-view-params.bin")?, targets)?;
        } else {
            let atmosphere = atmosphere::install(&mut app, &root)?;
            if let Some(cloud_root) = cloud_root {
                let targets = clouds::install(
                    &mut app,
                    &cloud_root,
                    clouds::SharedAtmosphere {
                        images: atmosphere.images,
                        ready: atmosphere.ready,
                        params: atmosphere.params,
                    },
                    get("cloud-view-params.bin")?,
                )?;
                sky::install_clouds(&mut app, get("cloud-view-params.bin")?, targets)?;
            }
        }
    }
    if let Some(ocean_root) = ocean_root {
        let ocean_targets = ocean::install(&mut app, &ocean_root, 120, Some(ocean_output))?;
        if with_optics {
            let optics = water_optics::underwater_optics(&app)?;
            let sun = optics.sun_direction.normalize();
            let (sea_level, reach) = water_optics::underwater_column(&app);
            let maps = forge_engine::tidewater_caustics::prepare_maps(
                &mut app,
                ocean_targets.images[1].clone(),
                ocean_targets.sizes[3].x,
                ocean_targets.sizes[2].x,
                sun,
                ocean_targets.completed_steps.clone(),
            )?;
            let params = forge_engine::tidewater_caustics::CausticSampleParams::new(
                ocean_targets.sizes[3].x,
                ocean_targets.sizes[2].x,
                sun,
                if std::env::var_os("FORGE_TIDEWATER_CAUSTICS_OFF").is_some() {
                    0.0
                } else {
                    0.75
                },
            )?
            .with_underwater(
                ocean_targets.sizes.map(|size| size.x),
                sea_level,
                reach,
                optics.absorption,
                optics.scattering,
            )?;
            forge_engine::tidewater_underwater_lighting::register(&mut app,
                <water_optics::WaterMaterial as bevy::render::render_resource::AsBindGroup>::bind_group_layout_descriptor)?;
            let light_maps = app
                .world()
                .resource::<forge_engine::tidewater_underwater_lighting::Targets>()
                .bindings
                .clone();
            app.insert_resource(forge_engine::tidewater_underwater_lighting::Control {
                enabled: true,
                caustics_strength: params.sun_and_strength.w,
                shadow_refraction: true,
            });
            app.insert_resource(forge_engine::tidewater_caustics::CausticMaterialInputs {
                fine: maps.fine.clone(),
                broad: maps.broad.clone(),
                light_maps,
                params,
            });
            if std::env::var("FORGE_TIDEWATER_CAUSTIC_PROOF").as_deref() == Ok("1") {
                let path = app.world().resource::<Probe>().screenshot.clone();
                caustics_proof::install(&mut app, &path, maps);
            }
            app.sub_app_mut(RenderApp).add_systems(
                RenderGraph,
                forge_engine::tidewater_caustics::render_maps
                    .after(ocean::OceanComputeSet)
                    .before(bevy::core_pipeline::schedule::camera_driver),
            );
        }
    }
    if with_spray {
        let terrain_document = serde_json::from_slice(get("terrain.json")?)?;
        let authored =
            shore_sim::AuthoredTerrain::new(&terrain_document, water_optics::shore_field(&app))?;
        let targets = spray::install(
            &mut app,
            breaker_root.as_ref().expect("spray fixture"),
            shore_root.as_ref().expect("shore fixture"),
            spray_output,
            get("water-frame.bin")?,
            with_native_spray_budget,
            authored.as_ref(),
        )?;
        shore_sim::install_external(
            &mut app,
            targets.shore_state.clone().expect("coupled shore"),
            targets.ready.clone(),
            targets.readback_complete.clone(),
        );
        app.insert_resource(breakers::Targets {
            stations: targets.stations,
            buffers: vec![targets.buffers[0].clone()],
            ready: targets.ready.clone(),
            readback_complete: targets.readback_complete.clone(),
        });
        breaker_lip::install(&mut app, &files)?;
        spray_sprites::install(&mut app, &files)?;
        if matches!(
            plant_projection,
            PlantProjection::Walk | PlantProjection::WalkSmoke | PlantProjection::WalkLongSmoke
        ) || std::env::var("FORGE_TIDEWATER_WATER_QUERY_PROOF").as_deref() == Ok("1")
        {
            let output = app
                .world()
                .resource::<Probe>()
                .screenshot
                .with_extension("water-query.json");
            spray::enable_water_queries(&mut app, output)?;
        }
    } else {
        if let Some(shore_root) = shore_root {
            shore_sim::install(&mut app, &shore_root, 120, Some(shore_output))?;
        }
        if let Some(breaker_root) = breaker_root {
            breakers::install(&mut app, &breaker_root, breaker_output)?;
            breaker_lip::install(&mut app, &files)?;
        }
    }
    if let Some(render) = app.get_sub_app_mut(RenderApp) {
        render.add_systems(ExtractSchedule, observe_pipelines);
    }
    if let Some(shader) = files.get("terrain.wgsl") {
        forge_engine::tidewater_terrain::install_shader(&mut app, std::str::from_utf8(shader)?)?;
    }
    if let Some(shader) = files.get("terrain-lod.wgsl") {
        let document =
            forge_project::TerrainDocument::from_json(std::str::from_utf8(get("terrain.json")?)?)?;
        terrain_lod::install_shader(&mut app, std::str::from_utf8(shader)?, &document)?;
        if with_geometry {
            ocean_geometry::install_shaders(&mut app, &files, &document)?;
        }
    }
    if let Some(plan) = static_assets {
        static_asset_placements::install(&mut app, plan);
    }
    if let Some(shader) = files.get("village.wgsl") {
        let source = static_asset_placements::shader_source(std::str::from_utf8(shader)?);
        forge_engine::tidewater_material::install_shader(&mut app, &source)?;
        if app.world().resource::<Probe>().fish_stand.is_some() {
            forge_engine::tidewater_stall::full_material::stage_shader(
                app.world_mut(),
                std::str::from_utf8(shader)?,
            )?;
        }
    }
    forge_engine::tidewater_stall::install_shader(&mut app)?;
    forge_engine::tidewater_plants::stage_shader(
        app.world_mut(),
        std::str::from_utf8(get("plant.wgsl")?)?,
    )?;
    if matches!(
        app.world().resource::<Probe>().plant_projection,
        PlantProjection::ResidentNavigation
    ) {
        forge_engine::tidewater_plants::picking::render::install(&mut app);
        forge_engine::tidewater_plants::picking::occluders::install(
            &mut app,
            std::str::from_utf8(get("village.wgsl")?)?,
        )?;
        forge_engine::tidewater_plants::picking::stage_shader(
            app.world_mut(),
            std::str::from_utf8(get("plant.wgsl")?)?,
        )?;
    }
    if matches!(
        plant_projection,
        PlantProjection::Walk | PlantProjection::WalkSmoke | PlantProjection::WalkLongSmoke
    ) {
        walk::install(&mut app)?;
    } else if std::env::var_os("FORGE_TIDEWATER_STATIC_POSTFX").is_some() {
        walk::install_static_graphics(&mut app)?;
    }
    if matches!(plant_projection, PlantProjection::Walk) {
        app.add_systems(Update, toggle_play_window);
    }
    if matches!(plant_projection, PlantProjection::Walk)
        && std::env::var_os("FORGE_TIDEWATER_FULLSCREEN").is_some()
    {
        app.add_systems(PreUpdate, enter_fullscreen_1080p);
    }
    refraction_proof::install(&mut app)?;
    if with_temporal_aux {
        if with_temporal_owned {
            temporal_owned::install(&mut app)?;
        } else {
            temporal_aux::install(&mut app)?;
        }
    }
    full_stall::install(&mut app)?;
    if app
        .world()
        .resource::<Probe>()
        .fish_stand_content
        .as_ref()
        .is_some_and(|content| content.source.is_some())
    {
        if !with_object_depth {
            return Err("Native source fish require the shared object shadow graph".into());
        }
        source_fish::install(&mut app, std::str::from_utf8(get("village.wgsl")?)?)?;
    }
    if app.run() != AppExit::Success {
        return Err("native render probe failed".into());
    }
    Ok(())
}

fn enter_fullscreen_1080p(
    mut window: Query<&mut Window, With<PrimaryWindow>>,
    monitor: Query<&Monitor, With<PrimaryMonitor>>,
    mut applied: Local<bool>,
) {
    if *applied {
        return;
    }
    let (Ok(mut window), Ok(monitor)) = (window.single_mut(), monitor.single()) else {
        return;
    };
    let Some(mode) = monitor
        .video_modes
        .iter()
        .filter(|mode| mode.physical_size == UVec2::new(1920, 1080))
        .max_by_key(|mode| mode.refresh_rate_millihertz)
    else {
        eprintln!("Tidewater fullscreen unavailable: no 1920x1080 display mode");
        *applied = true;
        return;
    };
    window.mode = WindowMode::Fullscreen(
        MonitorSelection::Primary,
        VideoModeSelection::Specific(*mode),
    );
    *applied = true;
}

fn setup_environment(mut commands: Commands, probe: Res<Probe>, mut images: ResMut<Assets<Image>>) {
    commands.insert_resource(ProbeEnvironment(
        images.add(environment_image(probe.environment.as_ref())),
    ));
}

#[allow(clippy::too_many_arguments, clippy::too_many_lines)] // Independent Bevy asset resources and shared lighting.
fn setup(
    mut commands: Commands,
    probe: Res<Probe>,
    acquired: Option<Res<forge_engine::tidewater_wind::AcquiredFrame>>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut images: ResMut<Assets<Image>>,
    mut materials: ResMut<Assets<PlantMaterial>>,
    mut buffers: ResMut<Assets<ShaderBuffer>>,
    environment: Res<ProbeEnvironment>,
    hill: Res<hill_shadow::Images>,
    object: Res<object_shadow::Target>,
) {
    let instance = &probe.instance;
    let base = Vec3::new(instance.x, instance.y, instance.z);
    let texture =
        forge_engine::tidewater_plants::textures::detail(&probe.detail).expect("validated detail");
    let sun = Vec3::new(0.3, 0.6, -0.7).normalize();
    let leaf = forge_engine::tidewater_plants::textures::leaf(
        forge_engine::tidewater_plants::textures::Layout {
            width: probe.leaf_texture.width,
            height: probe.leaf_texture.height,
            mips: probe.leaf_texture.mips,
        },
        &probe.leaf,
    )
    .expect("validated leaf");
    let leaf_handle = images.add(leaf);
    let (impostor_a, impostor_b) = if let Some((meta, a, b)) = &probe.impostors {
        (
            images.add(mipped_image(meta, a)),
            images.add(mipped_image(meta, b)),
        )
    } else {
        (leaf_handle.clone(), leaf_handle.clone())
    };
    let params = ProbeParams {
        i_pos: base.extend(instance.s),
        i_dat: Vec4::new(instance.la, instance.l, instance.height, instance.seed),
        wind: acquired
            .as_ref()
            .map_or(Vec4::new(1.0, 0.0, 6.0, 2.0), |frame| {
                frame
                    .wind_frame()
                    .expect("validated acquired wind")
                    .material_uniform()
            }),
        gust_lod: acquired
            .as_ref()
            .map_or(Vec4::new(0.0, 0.0, 0.0, 10000.0), |frame| {
                bevy::math::DVec4::new(
                    frame.vegetation_gust_offset[0],
                    frame.vegetation_gust_offset[1],
                    0.0,
                    10000.0,
                )
                .as_vec4()
            }),
        sun_night: sun.extend(0.0),
        lod_end_frame: Vec4::new(
            10001.0,
            acquired.as_ref().map_or(124.0, |frame| {
                f32::from(u8::try_from(frame.global_frame % 64).expect("six source frame bits"))
            }),
            0.0,
            if matches!(probe.mode, ProbeMode::Silhouette | ProbeMode::GrassCoverage) {
                1.0
            } else {
                0.0
            },
        ),
    };
    commands.insert_resource(StaticVegetationPose {
        wind_time: params.wind.to_array(),
        gust_offset: [params.gust_lod.x, params.gust_lod.y],
        frame_index: acquired
            .as_ref()
            .map_or(124, |frame| frame.global_frame % 64),
        acquired_source_frame: acquired.is_some(),
    });
    let instances = buffers.add(ShaderBuffer::from(
        probe
            .canopy
            .clone()
            .unwrap_or_else(|| canopy::fallback(&params)),
    ));
    let (grass_heights, grass_mask) = grass::textures(&probe, &mut images);
    let material = PlantMaterial {
        scene_lighting: forge_engine::tidewater_material::scene_buffer::HANDLE.clone(),
        motion: forge_engine::tidewater_plants::motion::DEFAULT_BUFFER.clone(),
        grass_heights,
        grass_mask,
        params,
        detail: images.add(texture),
        leaf: leaf_handle,
        impostor_a,
        impostor_b,
        instances,
        models: buffers.add(ShaderBuffer::from(vec![Mat4::IDENTITY])),
        environment: environment.0.clone(),
        hill_shadow: hill.output.clone(),
        object_shadow: object.image.clone(),
        object_settings: object.settings.clone(),
        live_shadows: forge_engine::tidewater_object_shadow::receiver::DEFAULT_BUFFER.clone(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        kind: 0,
    };
    grass::spawn(
        &mut commands,
        &probe,
        &material,
        &mut meshes,
        &mut materials,
        &mut buffers,
    );
    if probe.vegetation_scene.is_some() {
        vegetation_scene::spawn(
            &mut commands,
            &probe,
            &material,
            &mut meshes,
            &mut materials,
            &mut buffers,
        );
    } else {
        canopy::spawn(
            &mut commands,
            &probe,
            meshes.add(probe.mesh.clone()),
            materials.add(material),
        );
    }
    commands.spawn((
        DirectionalLight {
            illuminance: 10000.0,
            ..default()
        },
        Transform::default().looking_to(-sun, Vec3::Y),
    ));
    spawn_camera(&mut commands, &probe, base);
}

fn spawn_camera(commands: &mut Commands, probe: &Probe, base: Vec3) {
    if matches!(
        probe.plant_projection,
        PlantProjection::Walk | PlantProjection::WalkSmoke | PlantProjection::WalkLongSmoke
    ) {
        return;
    }
    let mut camera = commands.spawn((
        scanned_debris::MainCamera,
        Camera3d {
            depth_texture_usages: (bevy::render::render_resource::TextureUsages::RENDER_ATTACHMENT
                | bevy::render::render_resource::TextureUsages::COPY_SRC
                | bevy::render::render_resource::TextureUsages::TEXTURE_BINDING)
                .into(),
            ..default()
        },
        if probe.mode == ProbeMode::Surface {
            Msaa::Sample4
        } else {
            Msaa::Off
        },
        camera_transform(probe, base),
        camera_projection(probe),
    ));
    // Install the same graphics chain as the playable camera for static A/B captures.
    if probe.camera_pose.is_some() {
        camera.insert(forge_engine::finite_perspective::FinitePerspectiveDepth);
    }
    if std::env::var_os("FORGE_TIDEWATER_STATIC_POSTFX").is_some() {
        camera.insert(forge_engine::ForgePlayerCamera);
    }
}

fn camera_transform(probe: &Probe, base: Vec3) -> Transform {
    if let Some(pose) = &probe.camera_pose {
        Transform::from_translation(Vec3::from_array(pose.position))
            .with_rotation(Quat::from_array(pose.rotation).normalize())
    } else {
        Transform::from_translation(base + probe.camera_offset)
            .looking_at(base + Vec3::Y * 6.0, Vec3::Y)
    }
}

fn camera_projection(probe: &Probe) -> Projection {
    let (fov, near, far) = probe
        .camera_pose
        .as_ref()
        .map_or((45.0_f32, 0.1, 20000.0), |pose| {
            (pose.fov_degrees, 0.1, 60000.0)
        });
    Projection::Perspective(PerspectiveProjection {
        fov: fov.to_radians(),
        // Match the initial capture target before CameraUpdateSystems publishes
        // the actual live window aspect ratio.
        aspect_ratio: 1920.0 / 1080.0,
        near,
        far,
        ..default()
    })
}

#[allow(clippy::too_many_arguments)] // Independent Bevy asset resources and shared lighting.
fn setup_terrain(
    mut commands: Commands,
    probe: Res<Probe>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut images: ResMut<Assets<Image>>,
    mut terrain_materials: ResMut<Assets<StandardMaterial>>,
    mut source_terrain_materials: ResMut<Assets<terrain_surface::TerrainMaterial>>,
    environment: Res<ProbeEnvironment>,
    hill: Res<hill_shadow::Images>,
    object: Res<object_shadow::Target>,
    normals: Res<terrain_normals::Shared>,
    caustics: Option<Res<forge_engine::tidewater_caustics::CausticMaterialInputs>>,
) {
    if probe.lod_heights.is_some()
        || matches!(
            probe.mode,
            ProbeMode::VillageAlbedo
                | ProbeMode::Silhouette
                | ProbeMode::VegetationDirect
                | ProbeMode::VegetationEnvironment
                | ProbeMode::GrassCoverage
        )
    {
        return;
    }
    if let Some(plan) = &probe.terrain {
        let source_material = probe.terrain_textures.as_ref().map(|data| {
            terrain_surface::load(
                data,
                &mut images,
                &mut source_terrain_materials,
                environment.0.clone(),
                hill.output.clone(),
                &object,
                normals.image.clone(),
                caustics.as_deref(),
            )
        });
        spawn_terrain(
            &mut commands,
            plan,
            &mut meshes,
            &mut terrain_materials,
            source_material,
        );
    }
}

fn spawn_terrain(
    commands: &mut Commands,
    plan: &forge_world::sampled_terrain::SampledTerrainPlan,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<StandardMaterial>,
    source_material: Option<Handle<terrain_surface::TerrainMaterial>>,
) {
    // Neutral diagnostic surface: exact 1 m authored lattice, no claim that this
    // substitutes Tidewater's procedural materials or camera-dependent CDLOD.
    let material = materials.add(StandardMaterial {
        base_color: Color::srgb(0.5, 0.5, 0.5),
        perceptual_roughness: 1.0,
        ..default()
    });
    let source = plan
        .document()
        .sampled_source
        .as_ref()
        .expect("validated source");
    for z in (0..source.height).step_by(32) {
        for x in (0..source.width).step_by(32) {
            let patch = plan
                .build_patch(
                    [
                        u16::try_from(x).expect("bounded source"),
                        u16::try_from(z).expect("bounded source"),
                    ],
                    [
                        u16::try_from((source.width - x).min(32)).expect("bounded tile"),
                        u16::try_from((source.height - z).min(32)).expect("bounded tile"),
                    ],
                    1,
                )
                .expect("validated tile bounds");
            let mesh = Mesh::new(
                PrimitiveTopology::TriangleList,
                RenderAssetUsages::default(),
            )
            .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, patch.positions_m)
            .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, patch.normals)
            .with_inserted_indices(Indices::U32(patch.indices));
            let mut entity = commands.spawn((Mesh3d(meshes.add(mesh)), NotShadowCaster));
            if let Some(source) = &source_material {
                entity.insert(MeshMaterial3d(source.clone()));
            } else {
                entity.insert(MeshMaterial3d(material.clone()));
            }
        }
    }
}

fn environment_image(image: Option<&Image>) -> Image {
    image.cloned().unwrap_or_else(|| {
        // Explicit neutral input for the probe's non-environment test modes.
        forge_engine::tidewater_environment::cube_rgba16f(1, 1, &[0; 48])
            .expect("literal six-face black cube")
    })
}

fn mipped_image(meta: &LeafTexture, bytes: &[u8]) -> Image {
    forge_engine::tidewater_plants::textures::mipped(
        forge_engine::tidewater_plants::textures::Layout {
            width: meta.width,
            height: meta.height,
            mips: meta.mips,
        },
        bytes,
    )
    .expect("validated source mips")
}

#[allow(clippy::too_many_arguments, clippy::too_many_lines)] // Independent render completion resources and readback observer.
#[expect(
    clippy::type_complexity,
    reason = "Capture SystemParams keep the evidence resources visible at the system boundary"
)]
fn capture(
    mut commands: Commands,
    capture_state: (
        Res<FrameCount>,
        Option<Res<walk::graphics::GraphicsState>>,
        Option<Res<StaticVegetationPose>>,
        Option<Res<acquired_environment::Receipt>>,
        Option<Res<static_asset_placements::Status>>,
    ),
    mut probe: ResMut<Probe>,
    mut exit: MessageWriter<AppExit>,
    hill: Res<hill_shadow::Images>,
    object: Option<Res<object_shadow::Target>>,
    atmosphere: Option<Res<atmosphere::Targets>>,
    clouds: Option<Res<clouds::Targets>>,
    ocean: Option<Res<ocean::Targets>>,
    shore: Option<Res<shore_sim::Targets>>,
    breakers: Option<Res<breakers::Targets>>,
    normals: Res<terrain_normals::Shared>,
    picking: Option<Res<picking_proof::Pending>>,
    queries: Option<Res<spray::water_queries::Targets>>,
    refraction_proof: Option<Res<refraction_proof::Proof>>,
    temporal_proofs: (
        Option<Res<temporal_aux::Proof>>,
        Option<Res<temporal_owned::Proof>>,
    ),
) {
    let (frame, graphics_state, vegetation_pose, acquired_environment, static_assets) =
        capture_state;
    if static_assets.is_some_and(|assets| !assets.0) {
        return;
    }
    if probe.failed {
        exit.write(AppExit::error());
        return;
    }
    if refraction_proof.is_some() || temporal_proofs.0.is_some() || temporal_proofs.1.is_some() {
        return;
    }
    if matches!(
        probe.plant_projection,
        PlantProjection::Walk | PlantProjection::WalkSmoke | PlantProjection::WalkLongSmoke
    ) {
        return;
    }
    let object_ready = object
        .as_ref()
        .is_none_or(|v| v.ready.load(std::sync::atomic::Ordering::Acquire));
    let ocean_ready = ocean.as_ref().is_none_or(|v| {
        v.ready.load(std::sync::atomic::Ordering::Acquire)
            && v.readback_complete
                .load(std::sync::atomic::Ordering::Acquire)
    });
    let sky_ready = atmosphere
        .as_ref()
        .is_none_or(|v| v.ready.load(std::sync::atomic::Ordering::Acquire));
    let shore_ready = shore.as_ref().is_none_or(|v| {
        v.ready.load(std::sync::atomic::Ordering::Acquire)
            && v.readback_complete
                .load(std::sync::atomic::Ordering::Acquire)
    });
    if frame.0 >= 120
        && normals.ready.load(std::sync::atomic::Ordering::Acquire)
        && probe.ready_frames >= 8
        && !probe.requested
        && object_ready
        && sky_ready
        && clouds
            .as_ref()
            .is_none_or(|v| v.ready.load(std::sync::atomic::Ordering::Acquire))
        && ocean_ready
        && shore_ready
        && queries
            .as_ref()
            .is_none_or(|q| q.bootstrap_ready.load(std::sync::atomic::Ordering::Acquire))
        && breakers.as_ref().is_none_or(|v| {
            v.ready.load(std::sync::atomic::Ordering::Acquire)
                && v.readback_complete
                    .load(std::sync::atomic::Ordering::Acquire)
        })
    {
        let receipt = serde_json::json!({
            "schema": "forge.tidewater.static-capture-runtime",
            "version": 1,
            "requested_at_main_frame": frame.0,
            "configured_camera": probe.camera_pose.as_ref().map(|pose| serde_json::json!({
                "position": pose.position,
                "rotation": pose.rotation,
                "fov_degrees": pose.fov_degrees,
            })),
            "graphics_values": graphics_state.as_ref().map(|state| state.values_receipt()),
            "visible_vegetation_pose": vegetation_pose.as_deref(),
            "acquired_environment": acquired_environment.as_deref(),
            "cloud_completed_steps": clouds.as_ref().map(|targets| targets.completed_steps.load(std::sync::atomic::Ordering::Acquire)),
            "acquired_ocean": ocean.as_ref().and_then(|targets| targets.acquired_manifest.as_ref().map(|manifest| serde_json::json!({
                "manifest_sha256": manifest,
                "bootstrap_frames": targets.bootstrap_steps,
                "completed_steps": targets.completed_steps.load(std::sync::atomic::Ordering::Acquire),
                "coupled_globals_shared": targets.acquired.is_some(),
            }))),
            "visual_parity": false,
            "limits": [
                "MainWorld values at screenshot request; asynchronous image completion follows",
                "Other animation clocks and GPU uniform readbacks not covered",
                "No whole-map, performance, Studio or portable qualification",
            ],
        });
        let written = (|| -> Result<(), Box<dyn std::error::Error>> {
            use std::io::Write;
            let bytes = serde_json::to_vec_pretty(&receipt)?;
            std::fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(probe.screenshot.with_extension("capture.json"))?
                .write_all(&bytes)?;
            Ok(())
        })();
        if let Err(error) = written {
            error!(%error, "static capture runtime receipt failed");
            probe.failed = true;
            exit.write(AppExit::error());
            return;
        }
        probe.requested = true;
        picking_proof::request(&mut commands);
        if let Some(object) = object.filter(|_| probe.object_readback == ShadowReadback::Pending) {
            use bevy::render::gpu_readback::{Readback, ReadbackComplete};
            commands
                .spawn(Readback::texture(object.image.clone()))
                .observe(
                    |event: On<ReadbackComplete>,
                     mut probe: ResMut<Probe>,
                     mut commands: Commands| {
                        use std::io::Write;
                        let result = (|| -> std::io::Result<()> {
                            if event.data.len() != 2048 * 2048 * 3 * 4 {
                                return Err(std::io::Error::other("invalid object depth readback"));
                            }
                            std::fs::OpenOptions::new()
                                .write(true)
                                .create_new(true)
                                .open(probe.screenshot.with_extension("object-depth.bin"))?
                                .write_all(&event.data)
                        })();
                        if let Err(error) = result {
                            error!(%error, "object shadow readback failed");
                            probe.failed = true;
                        } else {
                            probe.object_readback = ShadowReadback::Complete;
                        }
                        commands.entity(event.entity).despawn();
                    },
                );
        }
        if probe.hill_heights.is_some() {
            use bevy::render::gpu_readback::{Readback, ReadbackComplete};
            commands
                .spawn(Readback::texture(hill.output.clone()))
                .observe(
                    |event: On<ReadbackComplete>,
                     mut probe: ResMut<Probe>,
                     mut commands: Commands| {
                        use std::io::Write;
                        let result = std::fs::OpenOptions::new()
                            .write(true)
                            .create_new(true)
                            .open(probe.screenshot.with_extension("shadow.bin"))
                            .and_then(|mut file| file.write_all(&event.data));
                        if let Err(error) = result {
                            error!(%error,"shadow readback failed");
                            probe.failed = true;
                        } else {
                            probe.shadow_readback = ShadowReadback::Complete;
                        }
                        commands.entity(event.entity).despawn();
                    },
                );
        }
        if let Some(clouds) = clouds.filter(|_| probe.cloud_readback == ShadowReadback::Pending) {
            use bevy::render::gpu_readback::{Readback, ReadbackComplete};
            commands
                .spawn(Readback::texture(clouds.images[2].clone()))
                .observe(
                    |event: On<ReadbackComplete>,
                     mut probe: ResMut<Probe>,
                     mut commands: Commands| {
                        use std::io::Write;
                        let result = std::fs::OpenOptions::new()
                            .write(true)
                            .create_new(true)
                            .open(probe.screenshot.with_extension("cloud-shadow.bin"))
                            .and_then(|mut file| file.write_all(&event.data));
                        if let Err(error) = result {
                            error!(%error, "cloud shadow readback failed");
                            probe.failed = true;
                        } else {
                            probe.cloud_readback = ShadowReadback::Complete;
                        }
                        commands.entity(event.entity).despawn();
                    },
                );
        }
        commands
            .spawn(Screenshot::primary_window())
            .observe(save_to_disk(probe.screenshot.clone()))
            .observe(|_: On<ScreenshotCaptured>, mut probe: ResMut<Probe>| {
                probe.captured = true;
            });
    }
    if probe.captured
        && probe.shadow_readback == ShadowReadback::Complete
        && probe.object_readback == ShadowReadback::Complete
        && probe.cloud_readback == ShadowReadback::Complete
        && frame.0 >= 135
        && picking.as_ref().is_none_or(|p| p.done)
    {
        exit.write(AppExit::Success);
    }
    if frame.0 >= 900 {
        exit.write(AppExit::error());
    }
}

fn observe_pipelines(
    mut main: ResMut<MainWorld>,
    pipelines: Res<PipelineCache>,
    hill: Option<Res<hill_shadow::Ready>>,
) {
    let Some(mut probe) = main.get_resource_mut::<Probe>() else {
        return;
    };
    let mut total = 0;
    let mut ready = hill.is_none_or(|h| h.0);
    for pipeline in pipelines.pipelines() {
        total += 1;
        match &pipeline.state {
            CachedPipelineState::Ok(_) => {}
            CachedPipelineState::Err(
                ShaderCacheError::ShaderNotLoaded(_)
                | ShaderCacheError::ShaderImportNotYetAvailable,
            ) => {
                ready = false;
            }
            CachedPipelineState::Err(error) => {
                eprintln!("Tidewater probe GPU pipeline failed: {error:?}");
                probe.failed = true;
                ready = false;
            }
            _ => ready = false,
        }
    }
    probe.ready_frames = if ready && total > 0 {
        probe.ready_frames.saturating_add(1)
    } else {
        0
    };
}

fn toggle_play_window(
    keys: Res<ButtonInput<KeyCode>>,
    mut windows: Query<&mut Window, With<PrimaryWindow>>,
) {
    if !keys.just_pressed(KeyCode::F11) {
        return;
    }
    let Ok(mut window) = windows.single_mut() else {
        return;
    };
    window.mode = if matches!(window.mode, WindowMode::Windowed) {
        WindowMode::BorderlessFullscreen(MonitorSelection::Primary)
    } else {
        WindowMode::Windowed
    };
}

#[path = "tidewater_plant_probe/static_asset_placements.rs"]
mod static_asset_placements;
