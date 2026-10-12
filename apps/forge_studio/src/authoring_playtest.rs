//! Validated native snapshot -> the existing renderer/player, never a second simulation.
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_core::ContentHash;
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
    vegetation: VegetationTarget,
    placements: super::authoring_playtest_sources::PlaytestPlacements,
    has_paths: bool,
    rivers: Vec<forge_core::EntityId>,
    requires_water_projection: bool,
    building_count: usize,
    start: Option<forge_project::authoring::playtest::PlaytestStart>,
    gameplay: Option<forge_gameplay::GameplayDocument>,
}

enum VegetationTarget {
    Procedural(String),
    Imported(ContentHash),
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
                let imported = vegetation.imported_source.is_some();
                let vegetation = if imported {
                    // The imported population is acquired by its pinned blocks in the
                    // Studio source resolver. Never feed those instances to the
                    // procedural generator or silently replace them with new trees.
                    if !snapshot.paths().is_empty()
                        || !snapshot.rivers().is_empty()
                    {
                        return Err("Le Playtest du terrain importé ne projette pas encore les chemins ou rivières auteur.".into());
                    }
                    vegetation.validate().map_err(|e| e.to_string())?;
                    VegetationTarget::Imported(identity.source_hash)
                } else {
                    let plan = forge_world::VegetationGenerationPlan::from_documents_with_buildings(
                        &terrain,
                        &vegetation,
                        snapshot.paths(),
                        snapshot.rivers(),
                        snapshot.buildings(),
                    )
                    .and_then(|plan| plan.with_promotions(snapshot.promotions()))
                    .map_err(|e| e.to_string())?;
                    VegetationTarget::Procedural(plan.source_hash().to_string())
                };
                Ok(Target {
                    has_paths: !snapshot.paths().is_empty(),
                    rivers: snapshot.rivers().iter().map(|r| r.id).collect(),
                    requires_water_projection: state.zone_head.is_some() && !imported,
                    building_count: snapshot.buildings().len(),
                    start: snapshot.start(),
                    gameplay: snapshot.gameplay().cloned(),
                    placements: super::authoring_playtest_sources::PlaytestPlacements::new(
                        state.zone_head.clone(), identity.source_hash, snapshot.placements().to_vec(),
                    )?,
                    identity,
                    terrain_key: identity.terrain_hash.to_string(),
                    vegetation,
                })
            })
            .transpose()
    });
    let identity = result
        .as_ref()
        .map(|target| target.as_ref().map(|t| t.identity))
        .map_err(Clone::clone);
    let was_playing = TARGET.with(|slot| slot.borrow().as_ref().is_ok_and(Option::is_some));
    if was_playing && !identity.as_ref().is_ok_and(Option::is_some) {
        super::authoring_gameplay::invalidate_run();
    }
    TARGET.with(|slot| *slot.borrow_mut() = result);
    STATUS.with(|status| status.set("Simulation suspendue · vérification des projections"));
    identity
}

pub(super) fn install(app: &mut App, identity: Option<PlaytestIdentity>) -> Result<(), String> {
    let gameplay = TARGET.with(|slot| {
        slot.borrow()
            .as_ref()
            .ok()
            .and_then(|t| t.as_ref())
            .and_then(|t| t.gameplay.clone())
    });
    if let (Some(document), Some(identity)) = (gameplay, identity) {
        let run = super::authoring_gameplay::begin_run();
        let timing = forge_engine::playtest_timing::PlaytestTiming::new(identity, run.id());
        let resume = super::authoring_gameplay::take_resume(identity.source_hash)?;
        let runtime = forge_engine::gameplay::GameplayRuntime::new(
            document,
            identity.source_hash,
            resume.as_ref().map(|r| r.0.as_bytes()),
            resume.as_ref().is_some_and(|r| r.1),
        )?;
        if resume.is_some() {
            let position =
                runtime.session().state().actors[&runtime.session().document().player].position;
            forge_engine::PlayerSpawnPoint::rebase_before_start(
                app.world_mut(),
                Vec3::new(position.x, position.y, position.z),
            )?;
        }
        app.insert_resource(runtime)
            .insert_resource(timing)
            .init_resource::<super::authoring_gameplay::TimingPublication>()
            .insert_resource(run)
            .add_plugins(forge_engine::gameplay::GameplayPlugin)
            .add_plugins(forge_engine::playtest_timing::PlaytestTimingPlugin)
            .add_systems(
                FixedUpdate,
                super::authoring_gameplay::bridge
                    .after(forge_engine::navigation::NavigationSet::Refresh),
            );
        app.add_systems(
            FixedUpdate,
            super::authoring_gameplay::observe_timing.after(super::authoring_gameplay::bridge),
        );
    }
    app.insert_resource(PlaytestRun(identity, start()))
        .insert_resource(forge_engine::PlayerSimulationReady(false))
        .add_systems(
            FixedUpdate,
            update
                .before(forge_engine::PlayerControllerSet::Drive)
                .before(forge_engine::gameplay::GameplaySet::Drive),
        );
    Ok(())
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

#[allow(
    clippy::too_many_lines,
    reason = "The callback orders playtest requests, source readiness, and runtime activation."
)]
fn update(world: &mut World) {
    let (ready, status) = TARGET.with(|slot| {
        let mut slot = slot.borrow_mut();
        let Ok(Some(target)) = slot.as_mut() else {
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
        match &target.vegetation {
            VegetationTarget::Imported(head_hash) => {
                if !super::authoring_imported_terrain::ready(world, target.identity.terrain_hash) {
                    return (
                        false,
                        "Simulation suspendue · terrain importé en reconstruction",
                    );
                }
                if !super::authoring_imported_plants::ready(world, *head_hash) {
                    return (
                        false,
                        "Simulation suspendue · plantes importées en reconstruction",
                    );
                }
            }
            VegetationTarget::Procedural(vegetation_key) => {
                let terrain = world.get_resource::<forge_engine::TerrainRuntimeState>();
                if terrain
                    .is_none_or(|r| r.cache().source_document_hash != target.identity.terrain_hash)
                {
                    return (false, "Simulation suspendue · terrain en reconstruction");
                }
                let Some(stats) = world.get_resource::<forge_engine::VegetationProjectionStats>()
                else {
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
                    || stats.vegetation_document_hash != *vegetation_key
                {
                    return (false, "Simulation suspendue · végétation en reconstruction");
                }
            }
        }
        let Ok(placements) = target.placements.resolve(
            super::authoring_assets::imported_buildings::published_generation(),
            super::authoring_imported_population::ready_head_hash(),
            super::authoring_assets::objects::list,
        ) else {
            return (
                false,
                "Simulation suspendue · sources des objets en reconstruction ou refusées",
            );
        };
        if !super::authoring_assets::placements::accepted_projection(
            placements,
            target.identity.source_hash,
        ) || !forge_engine::manual_placements_ready(world, placements)
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
        if !super::authoring_building_projection::ready(
            world,
            target.identity.source_hash,
            target.building_count,
        ) {
            return (
                false,
                "Simulation suspendue · bâtiments/collisions en reconstruction ou incohérents",
            );
        }
        if world.contains_resource::<forge_engine::TidewaterPlayerState>()
            && world
                .get_resource::<forge_engine::tidewater_surface_queries::Targets>()
                .and_then(|targets| targets.queries.latest())
                .is_none_or(|batch| batch.count < 2)
        {
            return (
                false,
                "Simulation suspendue · attente des hauteurs de vagues GPU",
            );
        }
        if target.gameplay.is_some() {
            if world
                .get_resource::<forge_engine::gameplay::GameplayRuntime>()
                .is_none_or(|runtime| runtime.error().is_some())
            {
                return (false, "Simulation suspendue · gameplay refusé");
            }
            if world
                .get_resource::<forge_engine::gameplay::GameplayProjectionStatus>()
                .is_none_or(|projection| !projection.ready)
            {
                return (
                    false,
                    "Simulation suspendue · collisions de gameplay en reconstruction",
                );
            }
        }
        (
            true,
            "Simulation active · sources CPU concordantes, pas une preuve GPU",
        )
    });
    world
        .resource_mut::<forge_engine::PlayerSimulationReady>()
        .0 = ready;
    if let Some(mut runtime) = world.get_resource_mut::<forge_engine::gameplay::GameplayRuntime>() {
        runtime.set_enabled(ready);
    }
    STATUS.with(|value| value.set(status));
}
