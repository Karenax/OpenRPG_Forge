//! Governed manual meshes use the existing authored placement and collision contracts.
use bevy::{
    asset::LoadState,
    gltf::GltfAssetLabel,
    prelude::*,
    world_serialization::{WorldAsset, WorldAssetRoot, WorldInstance, WorldInstanceSpawner},
};
use forge_assets::governed_import::{GovernedAssetKind, GovernedImportCatalog};
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::{
    ManualPlacementCollision, ManualPlacementCollisionExpected, ManualPlacementCollisionGeometry,
    ManualPlacementCollisionPlugin, ManualPlacementPart, ManualPlacementProjection,
};
use forge_project::zone_authoring::{placements::ManualPlacement, source_objects};

#[derive(Resource)]
pub struct PreparedManual(Vec<(ManualPlacement, String)>);

impl PreparedManual {
    pub fn accepted(&self) -> Vec<ManualPlacement> {
        let mut placements = self
            .0
            .iter()
            .map(|(placement, _)| placement.clone())
            .collect::<Vec<_>>();
        placements.sort_by_key(|placement| placement.id);
        placements
    }
}

#[derive(Component)]
struct ManualSceneHandle(Handle<WorldAsset>);

pub fn prepare(loaded: &LoadedModuleZone) -> Result<PreparedManual, String> {
    let pin = loaded
        .manifest
        .files
        .iter()
        .find(|file| file.path == loaded.manifest.asset_catalog_path)
        .ok_or("Module asset catalog is absent")?;
    let bytes = loaded.read_payload(
        &pin.path,
        forge_project::module_package::MAX_MODULE_MANIFEST_BYTES,
    )?;
    let catalog: GovernedImportCatalog =
        serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
    let head = loaded.zone.head();
    let mut placements = head.corrections.placements.clone().unwrap_or_default();
    for edit in head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default()
    {
        placements.extend(source_objects::resolve(head, &edit.source)?);
    }
    let mut result = Vec::new();
    for placement in placements {
        let asset = catalog
            .assets
            .iter()
            .find(|asset| {
                asset.id == placement.asset_id
                    && asset.payload.sha256 == placement.asset_hash
                    && asset.kind == GovernedAssetKind::Mesh
            })
            .ok_or("Manual placement must name a governed mesh with its exact pin")?;
        result.push((placement, asset.payload.path.clone()));
    }
    Ok(PreparedManual(result))
}

pub fn install(app: &mut App, prepared: PreparedManual) {
    app.insert_resource(prepared)
        .add_plugins(ManualPlacementCollisionPlugin)
        .add_systems(Startup, spawn)
        .add_systems(PostUpdate, observe);
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy borrows system parameters"
)]
fn spawn(mut commands: Commands, prepared: Res<PreparedManual>, server: Res<AssetServer>) {
    for (placement, path) in &prepared.0 {
        let handle: Handle<WorldAsset> =
            server.load(GltfAssetLabel::Scene(0).from_asset(path.clone()));
        commands.spawn((
            Name::new(format!("Module object · {}", placement.name)),
            WorldAssetRoot(handle.clone()),
            ManualSceneHandle(handle),
            ManualPlacementProjection(placement.clone()),
            forge_engine::placement_transform(placement),
        ));
    }
}

fn observe(world: &mut World) {
    let pending = world
        .query_filtered::<(
            Entity,
            &ManualPlacementProjection,
            &ManualSceneHandle,
            Option<&WorldInstance>,
        ), Without<ManualPlacementCollisionExpected>>()
        .iter(world)
        .map(|(root, placement, handle, instance)| {
            (
                root,
                placement.0.id,
                handle.0.clone(),
                instance.map(|instance| **instance),
            )
        })
        .collect::<Vec<_>>();
    for (root, id, handle, instance) in pending {
        if let Some(LoadState::Failed(error)) =
            world.resource::<AssetServer>().get_load_state(handle.id())
        {
            error!("Module manual mesh failed to load: {error}");
            world.write_message(bevy::app::AppExit::error());
            continue;
        }
        let Some(instance) = instance else {
            continue;
        };
        let spawner = world.resource::<WorldInstanceSpawner>();
        if !spawner.instance_is_ready(instance) {
            continue;
        }
        let meshes = spawner
            .iter_instance_entities(instance)
            .filter(|entity| world.get::<Mesh3d>(*entity).is_some())
            .collect::<Vec<_>>();
        if meshes.is_empty() {
            error!("Module manual mesh contains no renderable primitives: {id}");
            world.write_message(bevy::app::AppExit::error());
            continue;
        }
        for entity in &meshes {
            world.entity_mut(*entity).insert((
                ManualPlacementPart(id),
                ManualPlacementCollision(id),
                ManualPlacementCollisionGeometry::TriangleMesh,
            ));
        }
        world
            .entity_mut(root)
            .insert(ManualPlacementCollisionExpected {
                placement: id,
                primitives: meshes,
            });
    }
}
