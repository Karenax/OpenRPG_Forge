//! Bind Tidewater's acquired scan inputs to the common renderer.
use bevy::{camera::primitives::Frustum, prelude::*};
use forge_engine::source_surface::{
    SourceSurfaceMaterial,
    scanned_host::{PreparedHost, Resident},
    scanned_material::TerrainBindings,
};
use forge_project::zone_authoring::placements::ManualPlacement;
use forge_tidewater::tidewater_debris::scanned_placements;
use forge_tidewater_assets::tidewater_debris::{scanned, scanned_input::ScannedInput};
use std::{error::Error, io::Read, path::Path};

#[derive(Resource)]
pub struct Requested;

#[derive(Component)]
pub struct MainCamera;

#[derive(Resource)]
pub struct Input(PreparedHost);

#[derive(Resource)]
struct Live {
    base: Handle<SourceSurfaceMaterial>,
    scene: Resident,
}

fn bounded(path: &Path, limit: usize) -> Result<Vec<u8>, String> {
    let mut bytes = Vec::new();
    std::fs::File::open(path)
        .map_err(|e| e.to_string())?
        .take(limit as u64 + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() > limit {
        return Err("Scanned native payload exceeds bound".into());
    }
    Ok(bytes)
}

pub fn load() -> Result<Option<Input>, Box<dyn Error>> {
    let Some(root) = std::env::var_os("FORGE_TIDEWATER_SCANNED_ROOT") else {
        return Ok(None);
    };
    let root = Path::new(&root).canonicalize()?;
    let manifest = bounded(&root.join("package.json"), 64 * 1024)?;
    let shader_path = std::env::var_os("FORGE_TIDEWATER_SCANNED_PROGRAM")
        .ok_or("Scanned instance program path required")?;
    let shader = bounded(Path::new(&shader_path), 128 * 1024)?;
    let acquired = ScannedInput::load(&manifest, &shader, |name, limit| {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if !path.starts_with(&root) {
            return Err("Scanned payload escapes native package".into());
        }
        bounded(&path, limit)
    })?;
    let source = scanned_placements::from_geometry(&acquired.geometry)?;
    Ok(Some(Input(PreparedHost::new(
        &acquired.package,
        acquired.geometry,
        &acquired.collisions,
        acquired.instance_program,
        scanned::revision(),
        source.placements,
    )?)))
}

pub fn setup(world: &mut World) {
    let Some(Input(input)) = world.remove_resource::<Input>() else {
        return;
    };
    if let Err(error) = stage(world, input) {
        world.resource_mut::<super::Probe>().failed = true;
        error!(%error,"Scanned native adoption failed");
    }
}

fn stage(world: &mut World, input: PreparedHost) -> Result<(), String> {
    let base_handle = world
        .get_resource::<super::village::SharedMaterials>()
        .and_then(|r| r.0.first())
        .ok_or("Scans require resident Village lighting")?
        .clone();
    let base = world
        .resource::<Assets<SourceSurfaceMaterial>>()
        .get(&base_handle)
        .ok_or("Scanned base material absent")?
        .clone();
    let terrain_handle = world
        .get_resource::<forge_engine::source_terrain::scene::ResidentTerrain>()
        .and_then(|terrain| terrain.material.as_ref())
        .ok_or("Scans require resident terrain")?;
    let terrain = world
        .resource::<Assets<super::terrain_lod::LodMaterial>>()
        .get(terrain_handle)
        .ok_or("Scanned terrain material absent")?;
    let terrain = TerrainBindings {
        heights: terrain.extension.height.clone(),
        domain: terrain.base.settings.domain,
        sun: terrain.base.settings.sun,
    };
    let scene = input.stage(world, base, terrain)?;
    world.insert_resource(Live {
        base: base_handle,
        scene,
    });
    Ok(())
}

pub fn update(world: &mut World) {
    let camera = world
        .query_filtered::<(&GlobalTransform, &Frustum), Or<(With<forge_engine::ForgePlayerCamera>, With<MainCamera>)>>()
        .single(world)
        .ok()
        .map(|(transform, frustum)| (transform.translation(), *frustum));
    let Some((position, frustum)) = camera else {
        return;
    };
    let Some(mut live) = world.remove_resource::<Live>() else {
        return;
    };
    let base = world
        .resource::<Assets<SourceSurfaceMaterial>>()
        .get(&live.base)
        .cloned()
        .ok_or_else(|| "Scanned base material absent".to_owned());
    let result = base.and_then(|base| live.scene.synchronize(world, position, &frustum, base));
    world.insert_resource(live);
    if let Err(error) = result {
        world.resource_mut::<super::Probe>().failed = true;
        error!(%error,"Scanned frame adoption failed");
    }
}

pub fn accepted(world: &World) -> Option<Vec<ManualPlacement>> {
    if !world.contains_resource::<Requested>() {
        return Some(Vec::new());
    }
    world
        .get_resource::<Live>()
        .map(|live| live.scene.placements().to_vec())
}
