use super::{
    DangerMotionActor, DangerMotionState, GameplayBody, GameplayRuntime, danger_motion, engine,
};
use crate::DomainEntityId;
use bevy::prelude::*;
use bevy_rapier3d::prelude::{
    ActiveEvents, Ccd, Collider, ColliderDisabled, ColliderMassProperties, ExternalImpulse,
    RapierColliderHandle, RigidBody, Sleeping, Velocity,
};
use forge_gameplay::{ActorRole, GameplayEvent, ObjectDefinition, ObjectKind, ObjectState};

#[derive(Resource, Default)]
pub struct GameplayProjectionStatus {
    pub ready: bool,
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameters are borrowed by the scheduler"
)]
pub(super) fn spawn(
    mut commands: Commands,
    mut runtime: ResMut<GameplayRuntime>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<StandardMaterial>>,
) {
    let document = runtime.session.document().clone();
    let state = runtime.session.state().clone();
    for actor in &document.actors {
        if actor.role == ActorRole::Player {
            continue;
        }
        let actor_state = &state.actors[&actor.id];
        let color = if actor.role == ActorRole::Enemy {
            Color::srgb(0.65, 0.12, 0.10)
        } else {
            Color::srgb(0.12, 0.38, 0.65)
        };
        let entity = commands
            .spawn((
                Name::new(format!("Gameplay actor · {}", actor.name)),
                DomainEntityId(actor.id),
                GameplayBody::default(),
                DangerMotionActor,
                DangerMotionState::default(),
                Mesh3d(meshes.add(Capsule3d::new(
                    danger_motion::NPC_CAPSULE_RADIUS_M,
                    danger_motion::NPC_CAPSULE_HEIGHT_M - danger_motion::NPC_CAPSULE_RADIUS_M * 2.0,
                ))),
                MeshMaterial3d(materials.add(StandardMaterial {
                    base_color: color,
                    // These provisional markers must stay readable with custom scene lighting.
                    unlit: true,
                    ..default()
                })),
                Transform::from_translation(engine(actor_state.position)),
                Collider::capsule_y(
                    danger_motion::NPC_CAPSULE_HALF_SEGMENT_M,
                    danger_motion::NPC_CAPSULE_RADIUS_M,
                ),
                danger_motion::npc_controller(),
                RigidBody::KinematicPositionBased,
                ActiveEvents::COLLISION_EVENTS,
                Velocity::default(),
            ))
            .id();
        if actor_state.vitality == 0 {
            commands
                .entity(entity)
                .insert((Visibility::Hidden, ColliderDisabled));
        }
        runtime.entities.insert(actor.id, entity);
    }
    for object in &document.objects {
        let current = &state.objects[&object.id];
        let h = engine(object.physics.body_half_extents_m);
        let body = RigidBody::Fixed;
        let entity = commands
            .spawn((
                Name::new(format!("Gameplay object · {}", object.name)),
                DomainEntityId(object.id),
                GameplayBody::default(),
                Mesh3d(meshes.add(Cuboid::new(h.x * 2.0, h.y * 2.0, h.z * 2.0))),
                MeshMaterial3d(materials.add(StandardMaterial {
                    base_color: object_color(&object.kind, current.burning),
                    unlit: true,
                    ..default()
                })),
                Transform::from_translation(engine(current.position))
                    .with_rotation(Quat::from_array(current.rotation_xyzw)),
                Collider::cuboid(h.x, h.y, h.z),
                body,
                ActiveEvents::COLLISION_EVENTS,
                Velocity::default(),
                Sleeping::default(),
                Ccd::enabled(),
                ColliderMassProperties::Mass(object.physics.mass_kg.max(0.01)),
            ))
            .id();
        if current.consumed || current.destroyed || current.open {
            commands
                .entity(entity)
                .insert((Visibility::Hidden, ColliderDisabled));
        }
        runtime.entities.insert(object.id, entity);
    }
    commands.insert_resource(GameplayProjectionStatus::default());
}

fn object_color(kind: &ObjectKind, burning: bool) -> Color {
    if burning {
        // Unlit materials skip PBR emission; HDR base color still reaches bloom/postprocessing.
        return Color::linear_rgb(4.0, 0.5, 0.05);
    }
    match kind {
        ObjectKind::Pickup { .. } => Color::srgb(0.9, 0.7, 0.10),
        ObjectKind::Door { .. } => Color::srgb(0.35, 0.20, 0.10),
        ObjectKind::Prop => Color::srgb(0.4, 0.4, 0.4),
    }
}

fn body_kind(object: &ObjectDefinition, state: &ObjectState, supported: bool) -> RigidBody {
    if object.physics.mass_kg > 0.0
        && (state.released || (!supported && (object.physics.pushable || object.physics.throwable)))
    {
        RigidBody::Dynamic
    } else {
        RigidBody::Fixed
    }
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::type_complexity,
    reason = "Bevy borrows the projection resources and disjoint component query"
)]
pub(super) fn synchronize(
    mut commands: Commands,
    mut runtime: ResMut<GameplayRuntime>,
    mut status: ResMut<GameplayProjectionStatus>,
    mut materials: ResMut<Assets<StandardMaterial>>,
    mut bodies: Query<
        (
            Entity,
            &DomainEntityId,
            &mut Visibility,
            &MeshMaterial3d<StandardMaterial>,
            Option<&RapierColliderHandle>,
            &mut RigidBody,
        ),
        With<GameplayBody>,
    >,
    visuals: Option<Res<super::visuals::GameplayVisualStatus>>,
) {
    let events = std::mem::take(&mut runtime.effects);
    for event in &events {
        apply_effect(&mut commands, &runtime, event);
    }
    let mut expected = 0;
    let mut ready = 0;
    for (entity, id, mut visibility, material, backend, mut body) in &mut bodies {
        let unavailable = runtime.session.state().objects.get(&id.0).map_or_else(
            || {
                runtime
                    .session
                    .state()
                    .actors
                    .get(&id.0)
                    .is_none_or(|a| a.vitality == 0)
            },
            |o| o.consumed || o.destroyed || o.open,
        );
        *visibility = if unavailable {
            Visibility::Hidden
        } else {
            Visibility::Visible
        };
        if unavailable {
            commands.entity(entity).insert(ColliderDisabled);
        } else {
            commands.entity(entity).remove::<ColliderDisabled>();
            expected += 1;
            ready += usize::from(backend.is_some());
        }
        if let Some(current) = runtime.session.state().objects.get(&id.0) {
            let color = runtime
                .session
                .document()
                .object(id.0)
                .map(|definition| object_color(&definition.kind, current.burning));
            refresh_object_surface(&mut materials, &material.0, color, current.burning);
            if let Some(definition) = runtime.session.document().object(id.0) {
                let supported = runtime
                    .session
                    .document()
                    .objects
                    .iter()
                    .any(|parent| parent.physics.supports.contains(&id.0));
                let desired = if runtime.enabled {
                    body_kind(definition, current, supported)
                } else {
                    RigidBody::Fixed
                };
                if *body != desired {
                    *body = desired;
                }
            }
        }
    }
    status.ready = visuals.is_none_or(|v| v.error.is_none() && v.ready == v.expected)
        && expected == ready
        && runtime.entities.len()
            == runtime.session.document().objects.len() + runtime.session.document().actors.len();
}

fn refresh_object_surface(
    materials: &mut Assets<StandardMaterial>,
    handle: &Handle<StandardMaterial>,
    color: Option<Color>,
    burning: bool,
) {
    let emissive = if burning {
        LinearRgba::rgb(4.0, 0.5, 0.05)
    } else {
        LinearRgba::BLACK
    };
    let changed = materials.get(handle).is_some_and(|material| {
        color.is_some_and(|color| material.base_color != color) || material.emissive != emissive
    });
    if changed {
        let mut material = materials
            .get_mut(handle)
            .expect("Compared gameplay material still exists");
        if let Some(color) = color {
            material.base_color = color;
        }
        material.emissive = emissive;
    }
}

#[cfg(test)]
mod material_tests;

fn apply_effect(commands: &mut Commands, runtime: &GameplayRuntime, event: &GameplayEvent) {
    match event {
        GameplayEvent::Impulse { object, impulse } => {
            if let Some(entity) = runtime.entities.get(object) {
                commands.entity(*entity).insert((
                    RigidBody::Dynamic,
                    ExternalImpulse {
                        impulse: engine(*impulse),
                        ..Default::default()
                    },
                    Sleeping::default(),
                ));
            }
        }
        GameplayEvent::Released { object } => {
            if let Some(entity) = runtime.entities.get(object) {
                commands
                    .entity(*entity)
                    .insert((RigidBody::Dynamic, Sleeping::default()));
            }
        }
        GameplayEvent::Destroyed { object } => {
            if let Some(entity) = runtime.entities.get(object) {
                commands
                    .entity(*entity)
                    .insert((Visibility::Hidden, ColliderDisabled));
            }
        }
        _ => (),
    }
}
