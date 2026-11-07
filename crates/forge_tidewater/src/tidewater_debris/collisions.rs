//! Exact local source primitives; registration and character behaviour remain runtime checks.
use bevy::prelude::*;
use bevy_rapier3d::prelude::Collider;
use forge_tidewater_assets::tidewater_debris::collisions::LocalShape;
use forge_tidewater_assets::tidewater_debris::collisions::Owner;

pub use forge_engine::source_surface::objects::props::{PreparedPrimitive, SourceCollision};

/// Prepare without allocating ECS entities. Yaw is baked into the convex box to preserve
/// its volume when the author applies a nonuniform placement scale.
///
/// # Errors
/// Refuses a hull Rapier cannot construct or unsupported nonsolid source semantics.
#[allow(
    clippy::cast_possible_truncation,
    reason = "Hash-pinned reader bounds finite source values; Rapier uses f32."
)]
pub fn prepare(owner: &Owner) -> Result<Vec<PreparedPrimitive>, String> {
    owner
        .colliders
        .iter()
        .map(|primitive| {
            let (transform, collider, solid, walkable) = match &primitive.local {
                LocalShape::Box {
                    translation_m,
                    half_m,
                    rotation_y_rad,
                    solid,
                    walkable,
                } => (
                    Transform::from_translation(Vec3::from_array(translation_m.map(|v| v as f32))),
                    forge_engine::physics::yawed_box_collider(
                        Vec3::from_array(half_m.map(|v| v as f32)),
                        *rotation_y_rad as f32,
                    )
                    .map_err(str::to_owned)?,
                    *solid,
                    Some(*walkable),
                ),
                LocalShape::Cylinder {
                    translation_m,
                    radius_m,
                    half_height_m,
                } => (
                    Transform::from_translation(Vec3::from_array(translation_m.map(|v| v as f32))),
                    Collider::cylinder(*half_height_m as f32, *radius_m as f32),
                    true,
                    None,
                ),
                LocalShape::ConvexBox {
                    vertices,
                    solid,
                    walkable,
                    ..
                } => (
                    Transform::IDENTITY,
                    Collider::convex_hull(
                        &vertices
                            .iter()
                            .copied()
                            .map(Vec3::from_array)
                            .collect::<Vec<_>>(),
                    )
                    .ok_or("Debris source convex hull failed")?,
                    *solid,
                    Some(*walkable),
                ),
            };
            if !solid {
                return Err("Debris nonsolid collision semantics unsupported".into());
            }
            Ok(PreparedPrimitive {
                transform,
                collider,
                source: SourceCollision {
                    source_index: primitive.source_index,
                    source_shape: primitive.source_shape.clone(),
                    tag: primitive.tag.clone(),
                    solid,
                    walkable,
                },
            })
        })
        .collect()
}
