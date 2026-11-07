//! Admitted source objects in the existing live scene, with shared geometry and collisions.
use super::{PreparedLibrary, ResidentLibrary, material, prepare_meshes};
use crate::{
    source_lighting::surfaces::live::{self, SurfaceHostSet},
    source_surface::{
        SourceSurfaceMaterial,
        geometry::{PreparedMeshes, ResidentMeshes},
        objects::{PreparedScene, ResidentScene},
        scene::{self, WaterBindings, required_water as water_bindings},
        underwater,
    },
};
use bevy::prelude::*;
use forge_assets::source_surface_package::SurfacePackage;
use forge_project::zone_authoring::placements::ManualPlacement;
use std::collections::{BTreeMap, HashSet};
#[cfg(test)]
mod tests;

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct ObjectHostSet;

pub struct PreparedHost {
    geometry: PreparedMeshes,
    library: PreparedLibrary,
    placements: Vec<ManualPlacement>,
}

#[derive(Resource)]
struct Pending(Option<(PreparedHost, WaterMode)>);

#[derive(Clone, Copy)]
enum WaterMode {
    Absent,
    Required,
}

#[derive(Resource)]
struct State {
    geometry: ResidentMeshes,
    _library: ResidentLibrary,
    materials: BTreeMap<String, Handle<SourceSurfaceMaterial>>,
    entities: HashSet<Entity>,
    water_mode: WaterMode,
    water: WaterBindings,
}

#[derive(Resource, Clone, Debug, Default, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
pub struct Status {
    pub placements: usize,
    pub parts: usize,
    pub synchronized: bool,
    pub error: Option<String>,
}

impl PreparedHost {
    pub fn from_prepared(
        geometry: PreparedMeshes,
        library: PreparedLibrary,
        placements: Vec<ManualPlacement>,
    ) -> Result<Self, String> {
        forge_project::zone_authoring::placements::PlacementChanges::between(&[], &placements)?;
        Ok(Self {
            geometry,
            library,
            placements,
        })
    }
    /// The package reader has already admitted every resource and exact revision.
    /// This path uses local geometry and only the caller's canonical placements.
    pub fn new(package: &SurfacePackage, placements: Vec<ManualPlacement>) -> Result<Self, String> {
        forge_project::zone_authoring::placements::PlacementChanges::between(&[], &placements)?;
        for placement in &placements {
            package
                .catalog()
                .resolve_import_pin(placement.asset_id, placement.asset_hash)?;
        }
        Self::from_prepared(
            prepare_meshes(package)?,
            PreparedLibrary::load(package)?,
            placements,
        )
    }

    /// Water is explicitly absent in this host. A scene with authored water must
    /// supply its water receiver integration rather than using this constructor.
    /// The caller installs the admitted surface shader before extraction.
    pub fn install_without_water(self, app: &mut App) -> Result<(), String> {
        self.install(app, WaterMode::Absent)
    }

    /// Bind the same live underwater lighting inputs as the admitted water host.
    /// The water host must be installed before registering its surface receivers.
    pub fn install_with_water(self, app: &mut App) -> Result<(), String> {
        self.install(app, WaterMode::Required)
    }

    fn install(self, app: &mut App, water_mode: WaterMode) -> Result<(), String> {
        validate_water_mode(app.world(), water_mode)?;
        if app.world().contains_resource::<Pending>()
            || app.world().contains_resource::<ResidentScene>()
        {
            return Err("Source object scene already owned".into());
        }
        if !live::installed(app.world()) {
            return Err(
                "Source objects require their admitted live environment and surface host".into(),
            );
        }
        if !app.is_plugin_added::<MaterialPlugin<SourceSurfaceMaterial>>() {
            app.add_plugins(MaterialPlugin::<SourceSurfaceMaterial>::default());
        }
        app.insert_resource(Pending(Some((self, water_mode))))
            .init_resource::<Status>()
            .add_systems(
                PostStartup,
                start.in_set(ObjectHostSet).after(SurfaceHostSet),
            )
            .add_systems(
                PostUpdate,
                update.in_set(ObjectHostSet).after(SurfaceHostSet),
            );
        Ok(())
    }
}

#[must_use]
pub fn owns(world: &World, entity: Entity) -> bool {
    world
        .get_resource::<State>()
        .is_some_and(|state| state.entities.contains(&entity))
}

/// Replace accepted placements through the same geometry, material and collider
/// projection used at startup. Refuse a stale caller before preparing anything.
pub fn replace_placements(
    world: &mut World,
    expected: &[ManualPlacement],
    placements: &[ManualPlacement],
) -> Result<(), String> {
    let scene = world
        .get_resource::<ResidentScene>()
        .ok_or("Source object scene absent")?;
    if scene
        .instances
        .iter()
        .map(|instance| &instance.source)
        .ne(expected)
    {
        return Err("Source object placements changed before replacement".into());
    }
    let state = world
        .get_resource::<State>()
        .ok_or("Source object owner absent")?;
    let prepared = PreparedScene::new(
        placements,
        &state.geometry,
        &state.materials,
        world.resource::<Assets<Mesh>>(),
        world.resource::<Assets<SourceSurfaceMaterial>>(),
    )?;
    prepared.stage(world);
    let entities = mark_parts(world);
    world.resource_mut::<State>().entities = entities;
    world.resource_mut::<Status>().synchronized = false;
    Ok(())
}

fn mark_parts(world: &mut World) -> HashSet<Entity> {
    let parts: Vec<_> = world
        .resource::<ResidentScene>()
        .instances
        .iter()
        .flat_map(|instance| {
            instance
                .parts
                .iter()
                .map(|&part| (part, instance.source.id, instance.source.asset_hash))
        })
        .collect();
    for &(part, object, source) in &parts {
        world
            .entity_mut(part)
            .insert(crate::static_hlod::StaticHlodSource { object, source });
        let opaque = world
            .get::<MeshMaterial3d<SourceSurfaceMaterial>>(part)
            .and_then(|material| {
                world
                    .resource::<Assets<SourceSurfaceMaterial>>()
                    .get(&material.0)
            })
            .is_some_and(|material| material.kind < 6);
        if opaque {
            world
                .entity_mut(part)
                .insert(crate::source_shadows::casters::scene::OpaqueCaster);
        }
    }
    parts.into_iter().map(|(part, _, _)| part).collect()
}

fn validate_water_mode(world: &World, mode: WaterMode) -> Result<(), String> {
    let present = world.contains_resource::<underwater::CausticMaterialInputs>();
    match (mode, present) {
        (WaterMode::Absent, true) => {
            Err("Source objects cannot ignore installed water lighting inputs".into())
        }
        (WaterMode::Required, false) => {
            Err("Source objects require the admitted water lighting inputs".into())
        }
        _ => Ok(()),
    }
}

fn apply_water(material: &mut SourceSurfaceMaterial, water: &WaterBindings) {
    material.underwater_params = water.params.clone();
    material.underwater_level.clone_from(&water.level);
    material.underwater_map_frame.clone_from(&water.frame);
}

fn sync_scene_motion(material: &mut SourceSurfaceMaterial, lighting: &SourceSurfaceMaterial) {
    if crate::source_surface::uses_wind(material.kind) {
        material.settings.wind = lighting.settings.wind;
    } else if material.kind == 0 {
        // Village glass uses scene time for lantern flicker even without wind.
        material.settings.wind.w = lighting.settings.wind.w;
        material.settings.flags.y = lighting.settings.flags.y;
    }
}

fn start(world: &mut World) {
    let Some((input, water_mode)) = world.resource_mut::<Pending>().0.take() else {
        return;
    };
    let result = (|| {
        validate_water_mode(world, water_mode)?;
        let water = match water_mode {
            WaterMode::Absent => scene::absent_water(world)?,
            WaterMode::Required => water_bindings(world)?,
        };
        let lighting = scene::lighting_material(world, &water)?;
        let library = input
            .library
            .stage(&mut world.resource_mut::<Assets<Image>>());
        let prepared = library
            .materials
            .iter()
            .map(|(name, parameters)| {
                material::prepare(parameters, &library.textures, &lighting)
                    .map(|material| (name.clone(), material))
            })
            .collect::<Result<Vec<_>, _>>()?;
        let mut materials = BTreeMap::new();
        for (name, material) in prepared {
            materials.insert(
                name,
                world
                    .resource_mut::<Assets<SourceSurfaceMaterial>>()
                    .add(material),
            );
        }
        let geometry = input
            .geometry
            .stage(&mut world.resource_mut::<Assets<Mesh>>());
        PreparedScene::new(
            &input.placements,
            &geometry,
            &materials,
            world.resource::<Assets<Mesh>>(),
            world.resource::<Assets<SourceSurfaceMaterial>>(),
        )?
        .stage(world);
        let entities = mark_parts(world);
        world.insert_resource(State {
            geometry,
            _library: library,
            materials,
            entities,
            water_mode,
            water,
        });
        Ok::<_, String>(())
    })();
    if let Err(error) = result {
        fail(world, error);
    }
}

fn update(world: &mut World) {
    if !world.contains_resource::<State>() || world.resource::<Status>().error.is_some() {
        return;
    }
    let result = world.resource_scope(|world, state: Mut<State>| -> Result<(), String> {
        validate_water_mode(world, state.water_mode)?;
        let water = match state.water_mode {
            WaterMode::Required => water_bindings(world)?,
            WaterMode::Absent => state.water.clone(),
        };
        let lighting = scene::lighting_material(world, &water)?;
        let mut assets = world.resource_mut::<Assets<SourceSurfaceMaterial>>();
        for handle in state.materials.values() {
            let current = assets
                .get(handle)
                .ok_or("Source object material disappeared")?;
            let mut material = current.clone();
            // Only live scene inputs change; retain every admitted surface parameter.
            material.scene_lighting = lighting.scene_lighting.clone();
            material.environment = lighting.environment.clone();
            material.hill_shadow = lighting.hill_shadow.clone();
            material.object_shadow = lighting.object_shadow.clone();
            material.object_settings = lighting.object_settings.clone();
            apply_water(&mut material, &water);
            material.clouds = lighting.clouds;
            material.cloud_shadow.clone_from(&lighting.cloud_shadow);
            sync_scene_motion(&mut material, &lighting);
            if &material != current {
                *assets
                    .get_mut(handle)
                    .ok_or("Source object material disappeared")? = material;
            }
        }
        let scene = world.resource::<ResidentScene>();
        let status = Status {
            placements: scene.instances.len(),
            parts: state.entities.len(),
            synchronized: scene.is_synchronized(world)
                && scene
                    .instances
                    .iter()
                    .flat_map(|instance| instance.parts.iter().copied())
                    .collect::<HashSet<_>>()
                    == state.entities,
            error: None,
        };
        *world.resource_mut::<Status>() = status;
        Ok(())
    });
    if let Err(error) = result {
        fail(world, error);
    }
}

fn fail(world: &mut World, error: String) {
    error!(%error, "Source object projection failed");
    world.resource_mut::<Status>().error = Some(error);
    world.write_message(bevy::app::AppExit::error());
}
