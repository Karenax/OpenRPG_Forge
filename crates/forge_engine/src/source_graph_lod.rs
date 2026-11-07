//! Camera-driven source LOD selection. Scene reveal remains a separate admission.
use super::source_graph_state::{OriginalLodDriverPending, StatePlan};
use bevy::prelude::*;
use std::collections::{BTreeMap, BTreeSet};

/// Explicit current viewport input; no camera is inferred from an asset name.
#[derive(Clone, Copy)]
pub struct LodCamera {
    pub position: Vec3,
    pub vertical_fov_radians: f32,
    pub lod_bias: f32,
}

/// Read the explicit viewport entity after transform propagation.
pub fn camera(world: &World, entity: Entity, lod_bias: f32) -> Result<LodCamera, String> {
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

pub struct LodDecision {
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
        *out = super::source_graph_context::number(value, "LOD reference point")?;
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
pub fn update(
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
        let height = relative_height(matrix, camera, group)?;
        let levels = group["levels"]
            .as_array()
            .ok_or("Source LOD levels absent")?;
        let mut previous = f32::INFINITY;
        let mut selected = None;
        let mut memberships = Vec::new();
        for (level, declaration) in levels.iter().enumerate() {
            let threshold = super::source_graph_context::number(
                &declaration["screen_relative_height"],
                "LOD threshold",
            )?;
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
            let paths = declaration["renderer_node_paths"]
                .as_array()
                .ok_or("Source LOD renderer paths absent")?;
            if members.len() != paths.len() {
                return Err("Source LOD renderer paths/identities differ".into());
            }
            for (member, path) in members.iter().zip(paths) {
                let path = path.as_str().ok_or("LOD renderer path invalid")?;
                let index = *state
                    .renderer_paths
                    .get(path)
                    .ok_or("LOD renderer outside admitted scene")?;
                let renderer = state
                    .renderers
                    .get(&index)
                    .ok_or("LOD renderer state absent")?;
                if member["guid"].as_str() != Some(renderer.guid.as_str())
                    || member["file_id"].as_str() != Some(renderer.file_id.as_str())
                {
                    return Err("LOD reference changed renderer identity".into());
                }
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
    apply_visibility(world, root, nodes, primitives, &visibility)?;
    Ok(decisions)
}

fn apply_visibility(
    world: &mut World,
    root: Entity,
    nodes: &BTreeMap<usize, Entity>,
    primitives: &[Entity],
    visibility: &BTreeMap<usize, bool>,
) -> Result<(), String> {
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
    Ok(())
}

fn relative_height(
    matrix: Mat4,
    camera: &LodCamera,
    group: &serde_json::Value,
) -> Result<f32, String> {
    let local = vector(&group["local_reference_point"])?;
    let point = matrix.transform_point3(local);
    let scale = matrix
        .x_axis
        .truncate()
        .length()
        .max(matrix.y_axis.truncate().length())
        .max(matrix.z_axis.truncate().length());
    let size = super::source_graph_context::number(&group["size"], "Source LOD size")?;
    let distance = camera.position.distance(point);
    let height = (size * scale * camera.lod_bias)
        / (2. * distance * (camera.vertical_fov_radians * 0.5).tan());
    if !size.is_finite() || size <= 0. || height.is_nan() || height < 0. {
        return Err("Invalid source LOD projected size".into());
    }
    Ok(height)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_graph_state;

    fn repeated_prototype_document() -> serde_json::Value {
        let identity = serde_json::json!({
            "guid": "0123456789abcdef0123456789abcdef",
            "file_id": "17",
            "exact_asset_identity_available": true
        });
        let renderer = serde_json::json!({
            "component": identity.clone(),
            "enabled": true,
            "force_rendering_off": false,
            "shadow_casting_mode": 1,
            "receive_shadows": true
        });
        serde_json::json!({
            "nodes": [
                {"extras":{"unity_source_node":{
                    "path":"root", "parent_path":"", "active_self":true,
                    "active_source_parent_chain":true, "renderers":[]
                }}},
                {"extras":{"unity_source_node":{
                    "path":"root/near", "parent_path":"root", "active_self":true,
                    "active_source_parent_chain":true, "renderers":[renderer.clone()]
                }}},
                {"extras":{"unity_source_node":{
                    "path":"root/far", "parent_path":"root", "active_self":true,
                    "active_source_parent_chain":true, "renderers":[renderer]
                }}}
            ],
            "extras":{"original_lod_groups":[{
                "node_path":"root", "source_component_identity":identity.clone(),
                "enabled":true, "fade_mode":0, "animate_cross_fading":false,
                "local_reference_point":[0.0,0.0,0.0], "size":2.0,
                "levels":[
                    {"screen_relative_height":0.4, "renderer_components":[identity.clone()],
                     "renderer_node_paths":["root/near"]},
                    {"screen_relative_height":0.05, "renderer_components":[identity],
                     "renderer_node_paths":["root/far"]}
                ]
            }]}
        })
    }

    #[test]
    fn repeated_prototype_identity_selects_each_instance_by_path() {
        let parents = BTreeMap::from([(0, None), (1, Some(0)), (2, Some(0))]);
        let mut state =
            source_graph_state::prepare(&repeated_prototype_document(), &parents, &[1, 2])
                .unwrap()
                .unwrap();
        let mut world = World::new();
        let root = world.spawn(Visibility::Hidden).id();
        let group = world
            .spawn((
                GlobalTransform::default(),
                Visibility::Inherited,
                ChildOf(root),
            ))
            .id();
        let near_node = world.spawn((Visibility::Inherited, ChildOf(group))).id();
        let far_node = world.spawn((Visibility::Inherited, ChildOf(group))).id();
        let nodes = BTreeMap::from([(0, group), (1, near_node), (2, far_node)]);
        let near_draw = world
            .spawn((Visibility::Inherited, ChildOf(near_node)))
            .id();
        let far_draw = world.spawn((Visibility::Inherited, ChildOf(far_node))).id();
        let draws = [near_draw, far_draw];
        state.apply(&mut world, root, &nodes, &draws);
        assert!(world.get::<OriginalLodDriverPending>(root).is_some());

        let near = LodCamera {
            position: Vec3::Z,
            vertical_fov_radians: std::f32::consts::FRAC_PI_2,
            lod_bias: 1.0,
        };
        let decisions = update(
            &mut world,
            root,
            &nodes,
            &draws,
            &state,
            Mat4::IDENTITY,
            &near,
        )
        .unwrap();
        assert_eq!(decisions[0].level, Some(0));
        assert_eq!(
            *world.get::<Visibility>(near_draw).unwrap(),
            Visibility::Inherited
        );
        assert_eq!(
            *world.get::<Visibility>(far_draw).unwrap(),
            Visibility::Hidden
        );
        assert!(world.get::<OriginalLodDriverPending>(root).is_none());

        let far = LodCamera {
            position: Vec3::Z * 10.0,
            ..near
        };
        let decisions = update(
            &mut world,
            root,
            &nodes,
            &draws,
            &state,
            Mat4::IDENTITY,
            &far,
        )
        .unwrap();
        assert_eq!(decisions[0].level, Some(1));
        assert_eq!(
            *world.get::<Visibility>(near_draw).unwrap(),
            Visibility::Hidden
        );
        assert_eq!(
            *world.get::<Visibility>(far_draw).unwrap(),
            Visibility::Inherited
        );

        state.lod_groups.get_mut(&0).unwrap()["levels"][1]["renderer_node_paths"][0] =
            serde_json::json!("root/missing");
        assert!(
            update(
                &mut world,
                root,
                &nodes,
                &draws,
                &state,
                Mat4::IDENTITY,
                &near
            )
            .is_err()
        );
        assert_eq!(
            *world.get::<Visibility>(near_draw).unwrap(),
            Visibility::Hidden
        );
        assert_eq!(
            *world.get::<Visibility>(far_draw).unwrap(),
            Visibility::Inherited
        );
    }
}
