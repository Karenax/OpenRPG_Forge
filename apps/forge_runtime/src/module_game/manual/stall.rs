//! Module bindings for the engine's source stall and fish hosts.
use bevy::prelude::*;
use forge_core::EntityId;
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::{
    ManualPlacementProjection,
    cell_streaming::CellStreamingSet,
    source_surface::{stall_assets, stall_material::FullMaterial, stall_scene::live},
    static_hlod::StaticHlodSource,
};
use forge_project::{
    module_package::ModuleStallAnimatedModel, zone_authoring::placements::ManualPlacement,
};
use std::collections::BTreeMap;

#[derive(Component)]
pub(in crate::module_game) struct SourceStallProjection;

pub(super) struct Prepared {
    assets: stall_assets::PreparedStall,
    pub(super) placements: Vec<ManualPlacement>,
    shader: String,
    animated: Option<ModuleStallAnimatedModel>,
}

pub(super) fn prepare(
    loaded: &LoadedModuleZone,
    placements: &[ManualPlacement],
    shader: impl FnOnce() -> Result<String, String>,
    fish: &mut Vec<ManualPlacement>,
) -> Result<(Option<Prepared>, BTreeMap<EntityId, EntityId>), String> {
    let area = loaded.zone.head().base.terrain.area_id;
    let Some(binding) = loaded
        .manifest
        .source_stalls
        .iter()
        .find(|binding| binding.area_id == area)
    else {
        return Ok((None, BTreeMap::new()));
    };
    let selected: Vec<_> = placements
        .iter()
        .filter(|placement| placement.asset_id == binding.asset_id)
        .cloned()
        .collect();
    if selected.is_empty() {
        return Ok((None, BTreeMap::new()));
    }
    if selected.iter().any(|placement| {
        placement.asset_hash != binding.package.manifest.sha256
            || placement.physics.is_some()
            || placement.particles.is_some()
            || placement.local_lights.is_some()
    }) {
        return Err("Source stall placement has a changed pin or unsupported companion".into());
    }
    let mut parents = BTreeMap::new();
    for attachment in &binding.attached_fish {
        let asset_hash = loaded
            .manifest
            .fish_profiles
            .iter()
            .filter(|profile| profile.area_id == area)
            .flat_map(|profile| &profile.assets)
            .find(|asset| asset.asset_id == attachment.asset_id)
            .ok_or("Attached fish is missing from the admitted area profiles")?
            .sha256;
        for parent in &selected {
            let id = EntityId::deterministic(format!(
                "stall-fish/{}/{}",
                parent.id, attachment.asset_id
            ));
            parents.insert(id, parent.id);
            fish.push(ManualPlacement {
                id,
                asset_id: attachment.asset_id,
                asset_hash,
                name: "Poisson de l’étal".into(),
                transform: attachment.local_transform,
                locked: false,
                physics: None,
                particles: None,
                local_lights: None,
            });
        }
    }
    let resolved = loaded
        .source_stall()?
        .ok_or("Source stall resources are missing")?;
    let assets = stall_assets::prepare(&resolved.layout, |name| {
        resolved.resources.get(name).map(Vec::as_slice)
    })?;
    Ok((
        Some(Prepared {
            assets,
            placements: selected,
            shader: shader()?,
            animated: binding.animated_model.clone(),
        }),
        parents,
    ))
}

impl Prepared {
    pub(super) fn install(self, app: &mut App, water: bool) -> Result<(), String> {
        live::install(
            app,
            self.assets,
            self.placements,
            &self.shader,
            water,
            self.animated,
        )?;
        app.add_systems(
            Update,
            register_static_parts.before(CellStreamingSet::Update),
        );
        Ok(())
    }
}

fn register_static_parts(world: &mut World) {
    let draws: Vec<_> = world
        .query_filtered::<(Entity, &ChildOf), (
            With<MeshMaterial3d<FullMaterial>>,
            Without<StaticHlodSource>,
        )>()
        .iter(world)
        .map(|(draw, link)| (draw, link.parent()))
        .collect();
    for (draw, root) in draws {
        let Some(placement) = world.get::<ManualPlacementProjection>(root) else {
            continue;
        };
        if live::owns(world, draw) {
            let source = StaticHlodSource {
                object: placement.0.id,
                source: placement.0.asset_hash,
            };
            world.entity_mut(draw).insert(source);
            world.entity_mut(root).insert(SourceStallProjection);
        }
    }
}
