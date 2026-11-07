//! Shared conservative bounds for the next physical step; never a rendering budget.
use bevy::prelude::*;
use bevy_rapier3d::prelude::{Collider, KinematicCharacterController, RigidBody, Velocity};

const MAX_ACTIVE_BODIES: usize = 1024;

/// The body is held fixed for now, but needs terrain coverage before it can move.
#[derive(Component)]
pub(crate) struct ReservedBodyMotion;

pub(crate) struct BodyEnvelope {
    pub centre: Vec3,
    pub predicted: Vec3,
    pub radius: f32,
}

pub(crate) fn body_envelopes(world: &mut World) -> Result<Vec<BodyEnvelope>, String> {
    let dt = world
        .get_resource::<Time<Fixed>>()
        .ok_or("Fixed clock absent")?
        .timestep()
        .as_secs_f32();
    let input_margin = world
        .get_resource::<crate::player_controller::PlayerRuntimeProfile>()
        .map_or(0.0, |profile| {
            profile.0.controller.walk_speed_mps * profile.0.controller.sprint_multiplier * dt * 2.0
        });
    let bodies: Vec<_> = world
        .query_filtered::<(
            Entity,
            Option<&RigidBody>,
            Has<KinematicCharacterController>,
            Has<ReservedBodyMotion>,
        ), Or<(With<RigidBody>, With<KinematicCharacterController>)>>()
        .iter(world)
        .filter(|(_, body, controller, reserved)| {
            *reserved || *controller || body.is_some_and(|body| *body != RigidBody::Fixed)
        })
        .map(|(entity, _, _, _)| entity)
        .take(MAX_ACTIVE_BODIES + 1)
        .collect();
    if bodies.len() > MAX_ACTIVE_BODIES || !input_margin.is_finite() || !dt.is_finite() || dt <= 0.0
    {
        return Err("Active body coverage exceeds the bounded streaming profile".into());
    }
    let mut result = Vec::with_capacity(bodies.len());
    for entity in bodies {
        let global = super::current_global(world, entity)
            .ok_or("Invalid dynamic body transform hierarchy")?;
        let transform = global.compute_transform();
        let collider = world
            .get::<Collider>(entity)
            .ok_or("Moving compound bodies require an explicit terrain coverage adapter")?;
        let controller = world.get::<KinematicCharacterController>(entity);
        if controller.is_some_and(|controller| controller.custom_shape.is_some()) {
            return Err(
                "Custom character shapes require an explicit terrain coverage adapter".into(),
            );
        }
        let motion = controller
            .and_then(|controller| controller.translation)
            .unwrap_or(Vec3::ZERO);
        let bounds = collider.raw.compute_local_aabb();
        let basis = global.affine().matrix3;
        let stretch = (basis.x_axis.length_squared()
            + basis.y_axis.length_squared()
            + basis.z_axis.length_squared())
        .sqrt();
        // The offset covers asymmetric shapes; Frobenius stretch also covers shear.
        // A command published by a later actuator is observed by the final host guard.
        let radius = (bounds.half_extents().length() + bounds.center().length()) * stretch
            + input_margin.max(motion.length());
        let velocity = world
            .get::<Velocity>(entity)
            .map_or(Vec3::ZERO, |velocity| velocity.linear);
        let predicted = transform.translation + velocity * (2.0 * dt);
        if !radius.is_finite()
            || !predicted.is_finite()
            || !transform.rotation.is_finite()
            || !motion.is_finite()
        {
            return Err("Nonfinite active body envelope".into());
        }
        result.push(BodyEnvelope {
            centre: transform.translation,
            predicted,
            radius,
        });
    }
    Ok(result)
}
