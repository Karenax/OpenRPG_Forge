//! Camera-driven source LOD selection. Scene reveal remains a separate admission.
use super::source_graph_state::{OriginalLodDriverPending, StatePlan};
use bevy::prelude::*;
use std::collections::{BTreeMap, BTreeSet};

/// Explicit current viewport input; no camera is inferred from an asset name.
#[derive(Clone, Copy)]
pub(super) struct LodCamera {
    pub position: Vec3,
    pub vertical_fov_radians: f32,
    pub lod_bias: f32,
}

/// Read the explicit viewport entity after transform propagation.
pub(super) fn camera(world: &World, entity: Entity, lod_bias: f32) -> Result<LodCamera, String> {
    let view = world
        .get::<Camera>(entity)
        .ok_or("Source viewport camera absent")?;
    if !view.is_active || world.get::<Camera3d>(entity).is_none() {
        return Err("Source viewport camera is not active 3D".into());
    }
    let projection = world
        .get::<Projection>(entity)
        .ok_or("Source viewport projection absent")?;
    let Projection::Perspective(perspective) = projection else {
        return Err("Source LOD orthographic projection requires qualification".into());
    };
    let global = world
        .get::<GlobalTransform>(entity)
        .ok_or("Source viewport transform absent")?;
    Ok(LodCamera {
        position: global.translation(),
        vertical_fov_radians: perspective.fov,
        lod_bias,
    })
}

pub(super) struct LodDecision {
    pub node: usize,
    pub relative_height: f32,
    pub level: Option<usize>,
}

fn vector(value: &serde_json::Value) -> Result<Vec3, String> {
    let values = value.as_array().ok_or("LOD reference point absent")?;
    if values.len() != 3 {
        return Err("LOD reference point width differs".into());
    }
    let mut result = [0.; 3];
    for (out, value) in result.iter_mut().zip(values) {
        *out = value.as_f64().ok_or("LOD reference point is not numeric")? as f32;
    }
    let result = Vec3::from_array(result);
    if !result.is_finite() {
        return Err("LOD reference point is not finite".into());
    }
    Ok(result)
}

/// Preflight every group and renderer before changing any visibility.
/// Fading, disabled groups and other projection policies require their own
/// native measurements. Their source declarations remain intact on refusal.
pub(super) fn update(
    world: &mut World,
    root: Entity,
    nodes: &BTreeMap<usize, Entity>,
    primitives: &[Entity],
    state: &StatePlan,
    target_from_source_object: Mat4,
    camera: &LodCamera,
) -> Result<Vec<LodDecision>, String> {
    if !camera.position.is_finite()
        || !camera.vertical_fov_radians.is_finite()
        || !(0.0..std::f32::consts::PI).contains(&camera.vertical_fov_radians)
        || camera.vertical_fov_radians == 0.
        || !camera.lod_bias.is_finite()
        || camera.lod_bias <= 0.
        || !target_from_source_object.is_finite()
        || target_from_source_object.determinant() == 0.
        || world.get_entity(root).is_err()
    {
        return Err("Invalid current source LOD camera or object basis".into());
    }
    let identities: BTreeMap<_, _> = state
        .renderers
        .iter()
        .map(|(node, r)| ((r.guid.as_str(), r.file_id.as_str()), *node))
        .collect();
    if identities.len() != state.renderers.len() {
        return Err("Repeated original renderer identity".into());
    }
    let mut visibility = BTreeMap::new();
    let mut decisions = Vec::new();
    for (node, group) in &state.lod_groups {
        if group["enabled"].as_bool() != Some(true)
            || group["fade_mode"].as_u64() != Some(0)
            || group["animate_cross_fading"].as_bool() != Some(false)
        {
            return Err("Source LOD fading or disabled-group policy is not admitted".into());
        }
        let entity = *nodes.get(node).ok_or("LOD group entity absent")?;
        let global = world
            .get::<GlobalTransform>(entity)
            .ok_or("Current LOD group transform absent")?
            .to_matrix();
        let matrix = global * target_from_source_object;
        if !matrix.is_finite() || matrix.determinant() == 0. {
            return Err("Current source LOD transform is invalid".into());
        }
        let local = vector(&group["local_reference_point"])?;
        let point = matrix.transform_point3(local);
        let scale = matrix
            .x_axis
            .truncate()
            .length()
            .max(matrix.y_axis.truncate().length())
            .max(matrix.z_axis.truncate().length());
        let size = group["size"].as_f64().ok_or("Source LOD size absent")? as f32;
        let distance = camera.position.distance(point);
        let height = (size * scale * camera.lod_bias)
            / (2. * distance * (camera.vertical_fov_radians * 0.5).tan());
        if !size.is_finite() || size <= 0. || height.is_nan() || height < 0. {
            return Err("Invalid source LOD projected size".into());
        }
        let levels = group["levels"]
            .as_array()
            .ok_or("Source LOD levels absent")?;
        let mut previous = f32::INFINITY;
        let mut selected = None;
        let mut memberships = Vec::new();
        for (level, declaration) in levels.iter().enumerate() {
            let threshold = declaration["screen_relative_height"]
                .as_f64()
                .ok_or("LOD threshold absent")? as f32;
            if !threshold.is_finite() || !(0.0..=1.0).contains(&threshold) || threshold >= previous
            {
                return Err("Source LOD thresholds are not ordered".into());
            }
            previous = threshold;
            if selected.is_none() && height > threshold {
                selected = Some(level);
            }
            let members = declaration["renderer_components"]
                .as_array()
                .ok_or("Source LOD renderer identities absent")?;
            for member in members {
                let key = (
                    member["guid"].as_str().ok_or("LOD renderer GUID absent")?,
                    member["file_id"]
                        .as_str()
                        .ok_or("LOD renderer file ID absent")?,
                );
                let index = *identities
                    .get(&key)
                    .ok_or("LOD renderer outside admitted scene")?;
                memberships.push((level, index));
            }
        }
        let mut seen = BTreeSet::new();
        for (level, index) in memberships {
            if !seen.insert(index) || visibility.contains_key(&index) {
                return Err("Renderer belongs to repeated LOD levels or groups".into());
            }
            let renderer = &state.renderers[&index];
            visibility.insert(
                index,
                selected == Some(level) && renderer.enabled && !renderer.force_rendering_off,
            );
        }
        decisions.push(LodDecision {
            node: *node,
            relative_height: height,
            level: selected,
        });
    }
    let by_entity: BTreeMap<_, _> = nodes.iter().map(|(i, e)| (*e, *i)).collect();
    let mut changes = Vec::new();
    for entity in primitives {
        let parent = world
            .get::<ChildOf>(*entity)
            .ok_or("LOD primitive parent absent")?
            .parent();
        let node = by_entity
            .get(&parent)
            .ok_or("LOD primitive outside source hierarchy")?;
        if let Some(visible) = visibility.get(node) {
            changes.push((*entity, *visible));
        }
    }
    for (entity, visible) in changes {
        world.entity_mut(entity).insert(if visible {
            Visibility::Inherited
        } else {
            Visibility::Hidden
        });
    }
    world.entity_mut(root).remove::<OriginalLodDriverPending>();
    Ok(decisions)
}
