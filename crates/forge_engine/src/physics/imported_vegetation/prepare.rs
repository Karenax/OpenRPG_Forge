use super::{Body, MAX_DERIVED_HULL_VERTICES, MAX_IMPORTED_COLLIDERS, Prepared};
use bevy::{
    math::{DQuat, DVec3},
    prelude::*,
};
use bevy_rapier3d::{geometry::get_snapped_scale, prelude::Collider};
use forge_core::{ContentHash, canonical_json_sha256};
use forge_project::{VegetationCollisionProfile, VegetationCollisionShape};
use forge_world::imported_vegetation::streaming::{PlantCollisionSource, StreamedPlantSelection};
use std::collections::BTreeMap;

pub(super) fn identity(selection: &StreamedPlantSelection<'_>) -> Result<ContentHash, String> {
    if selection.collisions.len() > MAX_IMPORTED_COLLIDERS {
        return Err("Imported collision count exceeds the bounded physical adapter".into());
    }
    let mut pins: Vec<_> = selection
        .collisions
        .iter()
        .map(|body| {
            let source = &body.entity.source;
            (
                body.entity.id,
                source.source_key,
                source.position_m.map(f64::to_bits),
                source.rotation_xyzw.map(f64::to_bits),
                source.scale.map(f64::to_bits),
                body.profile,
            )
        })
        .collect();
    pins.sort_by_key(|pin| pin.0);
    if pins.windows(2).any(|pair| pair[0].0 == pair[1].0) {
        return Err("Duplicate imported physical identity".into());
    }
    canonical_json_sha256(&(
        "forge.imported-plant-collisions.v1",
        selection.head_hash,
        selection.terrain_hash,
        pins,
    ))
    .map_err(|error| error.to_string())
}

pub(super) fn prepare(selection: &StreamedPlantSelection<'_>) -> Result<Prepared, String> {
    if selection.plants.len()
        > forge_project::imported_vegetation::MAX_IMPORTED_INSTANCES as usize
            + forge_project::zone_authoring::imported_plants::MAX_IMPORTED_PLANT_PLACEMENTS
    {
        return Err("Imported visible population exceeds the canonical budget".into());
    }
    let identity = identity(selection)?;
    let visible: BTreeMap<_, _> = selection
        .plants
        .iter()
        .map(|plant| (plant.entity.id, plant.entity))
        .collect();
    if visible.len() != selection.plants.len() {
        return Err("Duplicate visible plant identity".into());
    }
    let mut bodies = BTreeMap::new();
    let mut hull_vertices = 0_usize;
    for source in &selection.collisions {
        if visible
            .get(&source.entity.id)
            .is_none_or(|visible| *visible != source.entity)
        {
            return Err("Imported collider is not its canonical visible source".into());
        }
        let body = prepare_body(source)?;
        let vertices = body
            .collider
            .raw
            .as_convex_polyhedron()
            .map_or(0, |hull| hull.points().len());
        hull_vertices = hull_vertices
            .checked_add(vertices)
            .ok_or("Collision hull budget overflow")?;
        if hull_vertices > MAX_DERIVED_HULL_VERTICES {
            return Err("Imported collision hull vertex budget exceeded".into());
        }
        bodies.insert(source.entity.id, body);
    }
    Ok(Prepared {
        identity,
        head: selection.head_hash,
        terrain: selection.terrain_hash,
        bodies,
    })
}

fn valid_profile(profile: VegetationCollisionProfile) -> bool {
    profile.radius_m.is_finite()
        && (0.0..=16.0).contains(&profile.radius_m)
        && profile.radius_m > 0.0
        && profile.height_m.is_finite()
        && (0.0..=128.0).contains(&profile.height_m)
        && profile.height_m > 0.0
        && profile.activation_distance_m.is_finite()
        && (0.0..=4096.0).contains(&profile.activation_distance_m)
        && profile.activation_distance_m > 0.0
        && (profile.shape != VegetationCollisionShape::Capsule
            || profile.height_m >= 2.0 * profile.radius_m)
}

fn prepare_body(source: &PlantCollisionSource<'_>) -> Result<Body, String> {
    let raw = &source.entity.source;
    let profile = source.profile;
    let rotation = DQuat::from_array(raw.rotation_xyzw);
    if source.entity.id.as_uuid().is_nil()
        || !valid_profile(profile)
        || raw
            .position_m
            .iter()
            .chain(&raw.rotation_xyzw)
            .chain(&raw.scale)
            .any(|value| !value.is_finite())
        || raw
            .scale
            .iter()
            .any(|value| *value <= 0.0 || *value > 10_000.0)
        || (rotation.length_squared() - 1.0).abs() > 1e-9
    {
        return Err("Invalid imported collision pose or profile".into());
    }
    let base = DVec3::from_array(raw.position_m);
    let scale = DVec3::from_array(raw.scale).as_vec3();
    let half_height = profile.height_m * 0.5;
    let centre =
        base + rotation * (DVec3::from_array(raw.scale) * DVec3::Y * f64::from(half_height));
    let transform = Transform {
        translation: centre.as_vec3(),
        rotation: rotation.as_quat().normalize(),
        scale: Vec3::ONE,
    };
    if !transform.translation.is_finite()
        || !transform.rotation.is_finite()
        || !scale.is_finite()
        || scale.min_element() <= 0.0
    {
        return Err("Imported collision pose overflows the physical adapter".into());
    }
    let collider = scaled_shape(profile, scale)?;
    let bounds = collider.raw.compute_local_aabb();
    let reach = (bounds.half_extents().length()
        + bounds.center().length()
        + (centre - base).as_vec3().length())
    .next_up();
    if !reach.is_finite() {
        return Err("Imported collision activation envelope overflows".into());
    }
    Ok(Body {
        transform,
        collider,
        base: base.as_vec3(),
        reach,
        activation: profile.activation_distance_m,
    })
}

fn scaled_shape(profile: VegetationCollisionProfile, scale: Vec3) -> Result<Collider, String> {
    let half_height = profile.height_m * 0.5;
    let snapped = get_snapped_scale(scale);
    let radii = snapped * profile.radius_m;
    let radial_solid = match profile.shape {
        VegetationCollisionShape::Cylinder => radii.x > 0.0 && radii.z > 0.0,
        VegetationCollisionShape::Capsule => radii.min_element() > 0.0,
    };
    if half_height <= 0.0
        || snapped.min_element() <= 0.0
        || half_height * snapped.y <= 0.0
        || !radial_solid
    {
        return Err("Imported collision dimensions underflow the physical adapter".into());
    }
    let mut shape = match profile.shape {
        VegetationCollisionShape::Cylinder => Collider::cylinder(half_height, profile.radius_m),
        VegetationCollisionShape::Capsule => {
            Collider::capsule_y(half_height - profile.radius_m, profile.radius_m)
        }
    };
    // Use the existing Rapier scaling policy, with bounded convex approximation
    // for unsupported smooth nonuniform shapes. Freeze that actual prepared shape.
    shape.set_scale(scale, 20);
    if shape.scale() != get_snapped_scale(scale) || shape.scale().min_element() <= 0.0 {
        return Err("Imported collision scale cannot be represented by Rapier".into());
    }
    let collider = Collider::from(shape.raw.clone());
    let bounds = collider.raw.compute_local_aabb();
    // Rapier may return a zero-size fallback after failed shape scaling.
    // Positive input dimensions and a recorded scale alone do not prove a solid.
    if !bounds.half_extents().is_finite() || bounds.half_extents().min_element() <= 0.0 {
        return Err("Imported collision scaling produced a degenerate shape".into());
    }
    Ok(collider)
}
