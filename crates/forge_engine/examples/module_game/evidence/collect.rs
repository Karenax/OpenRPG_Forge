use super::super::evidence_contract::{
    ExpectedManual, ObservedManual, ObservedVisual, ProjectionObservation, verify,
};
use super::Context;
use bevy::{
    app::AppExit,
    asset::AssetServer,
    prelude::*,
    world_serialization::{WorldAssetRoot, WorldInstance, WorldInstanceSpawner},
};
use bevy_rapier3d::prelude::Collider;
use forge_engine::{
    DomainEntityId, ManualPlacementCollisionExpected, ManualPlacementCollisionStatus,
    ManualPlacementProjection,
    gameplay::{
        GameplayBody, GameplayRuntime,
        visuals::{GameplayVisualStatus, GovernedGameplayVisual},
    },
};

pub(super) fn observe(world: &mut World) {
    let context = world.resource::<Context>().clone();
    let frame = world.resource::<bevy::diagnostic::FrameCount>().0;
    let capture_issued_frame = forge_engine::issued_capture_frame(world);
    let (renderer, cached) = if let Ok(shared) = context.bridge.0.lock() {
        (shared.renderer.clone(), shared.functional_identity)
    } else {
        world.write_message(AppExit::error());
        return;
    };
    let mut observation = ProjectionObservation {
        pid: std::process::id(),
        frame,
        module_id: None,
        area_id: None,
        source_hash: None,
        functional_hash: None,
        gameplay_ready: false,
        tick: 0,
        simulation_error: None,
        visual_error: None,
        visual_expected: 0,
        visual_spawned: 0,
        visual_ready: 0,
        manual_projection_ready: false,
        visuals: vec![],
        manual: vec![],
        renderer,
    };
    if let Some(runtime) = world.get_resource::<GameplayRuntime>() {
        let document = runtime.session().document();
        let source = runtime.session().source_hash();
        observation.module_id = Some(document.module_id);
        observation.area_id = Some(document.area_id);
        observation.source_hash = Some(source);
        // GameSession exposes the authored document immutably for this native run.
        observation.functional_hash = cached
            .filter(|(hash, _)| *hash == source)
            .map(|(_, hash)| hash)
            .or_else(|| document.functional_hash().ok());
        observation.gameplay_ready = runtime.is_ready();
        observation.tick = runtime.session().state().tick;
        observation.simulation_error = runtime.error().map(str::to_owned);
    }
    if let Some(status) = world.get_resource::<GameplayVisualStatus>() {
        observation.visual_expected = status.expected;
        observation.visual_spawned = status.spawned;
        observation.visual_ready = status.ready;
        observation.visual_error.clone_from(&status.error);
    }
    observation.visuals = visuals(world);
    observation.manual_projection_ready =
        forge_engine::manual_placements_ready(world, &context.manual);
    observation.manual = manual(world);
    if observation.visual_error.is_none() {
        observation.visual_error = world
            .query::<&ManualPlacementCollisionStatus>()
            .iter(world)
            .find_map(|status| {
                if let ManualPlacementCollisionStatus::Failed(reason) = status {
                    Some(format!(
                        "Native manual collision projection failed: {reason}"
                    ))
                } else {
                    None
                }
            });
    }
    let fatal = observation.simulation_error.is_some()
        || observation.visual_error.is_some()
        || observation.renderer.failed > 0;
    let bounded_end = context.last_frame.is_some_and(|last| frame >= last);
    let ready = verify(&context.expected, &observation).is_ok();
    if let Ok(mut shared) = context.bridge.0.lock() {
        if context.capture_frame.is_some() && capture_issued_frame == Some(frame) {
            shared.capture_snapshot = Some(observation.clone());
        }
        shared.functional_identity = observation.source_hash.zip(observation.functional_hash);
        shared.final_snapshot = Some(observation);
    } else {
        world.write_message(AppExit::error());
        return;
    }
    if fatal || (bounded_end && !ready) {
        world.write_message(AppExit::error());
    }
}

fn visuals(world: &mut World) -> Vec<ObservedVisual> {
    let mut query = world.query::<(
        &GovernedGameplayVisual,
        &Transform,
        &ChildOf,
        &WorldAssetRoot,
        Option<&WorldInstance>,
    )>();
    let server = world.resource::<AssetServer>();
    let spawner = world.resource::<WorldInstanceSpawner>();
    query
        .iter(world)
        .map(|(visual, transform, parent, root, instance)| {
            let scene_ready = instance
                .is_some_and(|instance| spawner.instance_is_ready(**instance))
                && server.is_loaded_with_dependencies(root.0.id());
            let mesh_primitives = instance.map_or(0, |instance| {
                spawner
                    .iter_instance_entities(**instance)
                    .filter(|entity| world.get::<Mesh3d>(*entity).is_some())
                    .count()
            });
            ObservedVisual {
                entity_id: visual.target,
                asset: visual.pin,
                transform: domain_transform(*transform),
                parent_identity: world.get::<DomainEntityId>(parent.parent()).map(|id| id.0),
                parent_is_gameplay_body: world.get::<GameplayBody>(parent.parent()).is_some(),
                parent_has_collider: world.get::<Collider>(parent.parent()).is_some(),
                scene_ready,
                mesh_primitives,
            }
        })
        .collect()
}

fn manual(world: &mut World) -> Vec<ObservedManual> {
    world
        .query::<(
            &ManualPlacementProjection,
            &Transform,
            Option<&ManualPlacementCollisionExpected>,
            Option<&ManualPlacementCollisionStatus>,
        )>()
        .iter(world)
        .map(|(placement, transform, expected, status)| ObservedManual {
            identity: ExpectedManual {
                entity_id: placement.0.id,
                asset: forge_project::module_gameplay_visuals::VisualAssetPin {
                    asset_id: placement.0.asset_id,
                    sha256: placement.0.asset_hash,
                },
                transform: domain_transform(*transform),
            },
            mesh_primitives: expected.map_or(0, |expected| expected.primitives.len()),
            registered_primitives: match status {
                Some(ManualPlacementCollisionStatus::Registered { primitives }) => *primitives,
                Some(ManualPlacementCollisionStatus::Waiting { registered, .. }) => *registered,
                _ => 0,
            },
        })
        .collect()
}

fn domain_transform(transform: Transform) -> forge_core::Transform {
    forge_core::Transform {
        translation_m: forge_core::Vec3::new(
            transform.translation.x,
            transform.translation.y,
            transform.translation.z,
        ),
        rotation: forge_core::UnitQuaternion::new(
            transform.rotation.x,
            transform.rotation.y,
            transform.rotation.z,
            transform.rotation.w,
        ),
        scale: forge_core::Vec3::new(transform.scale.x, transform.scale.y, transform.scale.z),
    }
}
