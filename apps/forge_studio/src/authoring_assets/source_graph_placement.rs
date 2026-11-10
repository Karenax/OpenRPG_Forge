//! Schedule the common source-placement owner alongside Studio projections.
use super::source_graph_draw_probe as probe;
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::source_graph_placement::{ManualAdmission, admit_manual};
use forge_engine::source_graph_runtime::{SourceFrameStatus, SourceSceneResidents};
use std::sync::Arc;

pub(super) use forge_engine::source_graph_placement::Status;

#[derive(Component)]
pub(super) enum DrawStatus {
    Drawn(Vec<Entity>),
    Refused(String),
}

struct Active {
    root: Entity,
    carrier: forge_core::ContentHash,
    probe: probe::Probe,
    parts: Vec<Entity>,
    pose: Mat4,
}

fn refuse(world: &mut World, root: Entity, reason: String) {
    if let Ok(mut entity) = world.get_entity_mut(root) {
        entity.insert((Visibility::Hidden, DrawStatus::Refused(reason)));
    }
}

fn draw_sources(world: &mut World, mut active: Local<Option<Active>>) {
    if let Some(mut current) = active.take() {
        if let Some(SourceFrameStatus::Refused(reason)) =
            world.get::<SourceFrameStatus>(current.root)
        {
            let reason = reason.clone();
            refuse(world, current.root, reason);
            current.probe.finish(world);
        } else if world
            .get_resource::<SourceSceneResidents>()
            .and_then(|residents| {
                residents.static_primitives_for_carrier(current.root, current.carrier)
            })
            == Some(current.parts.as_slice())
            && world
                .get::<GlobalTransform>(current.root)
                .is_some_and(|pose| pose.to_matrix() == current.pose)
        {
            match current.probe.poll(world) {
                probe::Outcome::Pending => *active = Some(current),
                result => {
                    let diagnostics = matches!(result, probe::Outcome::Empty)
                        .then(|| current.probe.diagnostics(world));
                    if matches!(result, probe::Outcome::Drawn)
                        && let Some(placement) =
                            world.get::<forge_engine::ManualPlacementProjection>(current.root)
                        && let Some((pixels, size)) = current.probe.stable_pixels(world)
                    {
                        super::map_shelf::record_verified_pixels(
                            (placement.0.asset_id, current.carrier),
                            pixels,
                            size,
                        );
                    }
                    world.entity_mut(current.root).insert(Visibility::Hidden);
                    current.probe.finish(world);
                    match result {
                        probe::Outcome::Drawn => {
                            world
                                .entity_mut(current.root)
                                .insert(DrawStatus::Drawn(current.parts));
                        }
                        probe::Outcome::Empty => refuse(
                            world,
                            current.root,
                            format!(
                                "Aucun dessin stable pour le placement source : {}",
                                diagnostics.as_deref().unwrap_or("diagnostic absent")
                            ),
                        ),
                        probe::Outcome::Pending => unreachable!(),
                    }
                }
            }
            return;
        } else {
            if let Ok(mut root) = world.get_entity_mut(current.root) {
                root.insert(Visibility::Hidden);
            }
            current.probe.finish(world);
        }
    }

    let roots: Vec<_> = world
        .query_filtered::<Entity, (With<Status>, Without<DrawStatus>)>()
        .iter(world)
        .collect();
    for root in roots {
        if !matches!(
            world.get::<Status>(root),
            Some(Status::MountedPendingRender)
        ) {
            continue;
        }
        let Some(placement) = world.get::<forge_engine::ManualPlacementProjection>(root) else {
            continue;
        };
        if placement.0.physics.is_some() || placement.0.particles.is_some() {
            continue;
        }
        let source = placement.0.asset_hash;
        let Some(parts) = world
            .get_resource::<SourceSceneResidents>()
            .and_then(|residents| residents.static_primitives_for_carrier(root, source))
            .map(<[Entity]>::to_vec)
        else {
            continue;
        };
        match admit_manual(world, root) {
            Ok(ManualAdmission::Pending) => continue,
            Err(reason) => {
                refuse(world, root, reason);
                continue;
            }
            Ok(ManualAdmission::Attached) => {}
        }
        match world.get::<forge_engine::ManualPlacementCollisionStatus>(root) {
            Some(forge_engine::ManualPlacementCollisionStatus::Failed(reason)) => {
                let reason = (*reason).to_owned();
                refuse(world, root, reason);
                continue;
            }
            Some(forge_engine::ManualPlacementCollisionStatus::Registered { .. }) => {}
            _ => continue,
        }
        match probe::Probe::start(world, root, &parts, probe::PLACEMENT_LAYER) {
            Ok(Some(probe)) => {
                let pose = world
                    .get::<GlobalTransform>(root)
                    .expect("Mounted source placement has a global transform")
                    .to_matrix();
                *active = Some(Active {
                    root,
                    carrier: source,
                    probe,
                    parts,
                    pose,
                });
                return;
            }
            Ok(None) => {}
            Err(reason) => refuse(world, root, reason.into()),
        }
    }
}

pub(super) fn submit(
    commands: &mut Commands,
    root: Entity,
    carrier: Arc<[u8]>,
    physics: Option<forge_engine::source_graph_mount::SourcePhysicsPayload>,
    particles: Option<forge_engine::source_graph_mount::SourceParticlePayload>,
) {
    forge_engine::source_graph_placement::submit(commands, root, carrier, physics, particles, 31);
}

pub(super) fn remove(commands: &mut Commands, root: Entity) {
    forge_engine::source_graph_placement::remove(commands, root);
}

pub(super) fn register(app: &mut App) {
    app.configure_sets(
        PostUpdate,
        forge_engine::source_graph_runtime::SourceSceneSystems::Placements
            .after(super::preview::PreviewSystems::Requests)
            .after(super::placements::PlacementSystems::Projection),
    );
    app.add_systems(
        PostUpdate,
        draw_sources
            .after(forge_engine::source_graph_runtime::SourceSceneSystems::Placements)
            .after(forge_engine::source_graph_runtime::update),
    );
}
