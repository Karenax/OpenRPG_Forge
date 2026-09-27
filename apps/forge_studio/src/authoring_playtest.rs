//! Validated native snapshot -> the existing renderer/player, never a second simulation.
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_project::authoring::{
    AuthoringState,
    playtest::{PlaytestIdentity, PlaytestSnapshot},
};
use std::cell::{Cell, RefCell};

thread_local! {
    static TARGET: RefCell<Result<Option<Target>, String>> = const { RefCell::new(Ok(None)) };
    static STATUS: Cell<&'static str> = const { Cell::new("Simulation suspendue") };
}

struct Target {
    identity: PlaytestIdentity,
    terrain_key: String,
    vegetation_key: String,
    placements: Vec<forge_project::zone_authoring::placements::ManualPlacement>,
    has_paths: bool,
    rivers: Vec<forge_core::EntityId>,
    requires_water_projection: bool,
    building_count: usize,
    start: Option<forge_project::authoring::playtest::PlaytestStart>,
}

#[derive(Resource)]
struct PlaytestRun(
    Option<PlaytestIdentity>,
    Option<forge_project::authoring::playtest::PlaytestStart>,
);

pub(super) fn observe(state: &AuthoringState) -> Result<Option<PlaytestIdentity>, String> {
    let result = PlaytestSnapshot::from_state(state).and_then(|snapshot| {
        snapshot
            .map(|snapshot| {
                // The host identity attests canonical documents. Derive the separate
                // renderer terrain identity only after validating that native snapshot.
                let terrain = state.zone_head.as_ref().map_or_else(
                    || Ok(snapshot.terrain().clone()),
                    super::authoring_scene::terrain,
                )?;
                let mut identity = snapshot.identity();
                identity.terrain_hash = terrain.content_hash().map_err(|e| e.to_string())?;
                // An absent authored vegetation document is represented by a verified empty
                // derived source in this renderer. Never treat arbitrary old instances as empty.
                let vegetation = snapshot
                    .vegetation()
                    .cloned()
                    .map_or_else(|| super::authoring_vegetation::empty_source(&terrain), Ok)?;
                let vegetation =
                    forge_world::VegetationGenerationPlan::from_documents_with_buildings(
                        &terrain,
                        &vegetation,
                        snapshot.paths(),
                        snapshot.rivers(),
                        snapshot.buildings(),
                    )
                    .and_then(|plan| plan.with_promotions(snapshot.promotions()))
                    .map_err(|e| e.to_string())?;
                Ok(Target {
                    has_paths: !snapshot.paths().is_empty(),
                    rivers: snapshot.rivers().iter().map(|r| r.id).collect(),
                    requires_water_projection: state.zone_head.is_some(),
                    building_count: snapshot.buildings().len(),
                    start: snapshot.start(),
                    placements: snapshot.placements().to_vec(),
                    identity,
                    terrain_key: identity.terrain_hash.to_string(),
                    vegetation_key: vegetation.source_hash().to_string(),
                })
            })
            .transpose()
    });
    let identity = result
        .as_ref()
        .map(|target| target.as_ref().map(|t| t.identity))
        .map_err(Clone::clone);
    TARGET.with(|slot| *slot.borrow_mut() = result);
    STATUS.with(|status| status.set("Simulation suspendue · vérification des projections"));
    identity
}

pub(super) fn install(app: &mut App, identity: Option<PlaytestIdentity>) {
    app.insert_resource(PlaytestRun(identity, start()))
        .insert_resource(forge_engine::PlayerSimulationReady(false))
        .add_systems(
            FixedUpdate,
            update.before(forge_engine::PlayerControllerSet::Drive),
        );
}

pub(super) fn status() -> &'static str {
    STATUS.with(Cell::get)
}

pub(super) fn start() -> Option<forge_project::authoring::playtest::PlaytestStart> {
    TARGET.with(|slot| {
        slot.borrow()
            .as_ref()
            .ok()
            .and_then(|t| t.as_ref())
            .and_then(|t| t.start)
    })
}

fn update(world: &mut World) {
    let (ready, status) = TARGET.with(|slot| {
        let slot = slot.borrow();
        let Ok(Some(target)) = slot.as_ref() else {
            return (false, "Simulation suspendue · aucune source de jeu valide");
        };
        if world.resource::<PlaytestRun>().0 != Some(target.identity)
            || world.resource::<PlaytestRun>().1 != target.start
            || !super::authoring_viewport::can_project_sources()
        {
            return (
                false,
                "Simulation suspendue · projection périmée, recharger",
            );
        }
        let terrain = world.get_resource::<forge_engine::TerrainRuntimeState>();
        if terrain.is_none_or(|r| r.cache().source_document_hash != target.identity.terrain_hash) {
            return (false, "Simulation suspendue · terrain en reconstruction");
        }
        let Some(stats) = world.get_resource::<forge_engine::VegetationProjectionStats>() else {
            return (false, "Simulation suspendue · végétation absente");
        };
        if stats
            .quality
            .as_ref()
            .is_some_and(|q| q.status.starts_with("rejected_"))
        {
            return (false, "Simulation suspendue · projection végétale refusée");
        }
        if stats.terrain_document_hash != target.terrain_key
            || stats.vegetation_document_hash != target.vegetation_key
        {
            return (false, "Simulation suspendue · végétation en reconstruction");
        }
        if !super::authoring_assets::placements::accepted_projection(&target.placements)
            || !forge_engine::manual_placements_ready(world, &target.placements)
        {
            return (
                false,
                "Simulation suspendue · objets/collisions en reconstruction ou incohérents",
            );
        }
        if target.has_paths
            && !super::authoring_path_projection::ready(world, target.identity.source_hash)
        {
            return (
                false,
                "Simulation suspendue · chemins en reconstruction ou refusés",
            );
        }
        if target.requires_water_projection
            && !forge_engine::authoring_water::ready(
                world,
                target.identity.source_hash,
                target.identity.terrain_hash,
                &target.rivers,
            )
        {
            return (
                false,
                "Simulation suspendue · eau en reconstruction ou incohérente",
            );
        }
        if target.requires_water_projection
            && !super::authoring_building_projection::ready(
                world,
                target.identity.source_hash,
                target.building_count,
            )
        {
            return (
                false,
                "Simulation suspendue · bâtiments/collisions en reconstruction ou incohérents",
            );
        }
        (
            true,
            "Simulation active · sources CPU concordantes, pas une preuve GPU",
        )
    });
    world
        .resource_mut::<forge_engine::PlayerSimulationReady>()
        .0 = ready;
    STATUS.with(|value| value.set(status));
}
