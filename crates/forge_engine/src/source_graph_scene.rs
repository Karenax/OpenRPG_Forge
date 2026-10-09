//! Static source-scene hierarchy for the renderer adapter. This preserves the
//! declared local poses; it does not qualify Unity import coordinates or images.
use bevy::prelude::{
    ChildOf, Component, Entity, GlobalTransform, Mat4, Name, Quat, Transform, Vec3, Visibility,
    World,
};
use forge_assets::source_graph_glb::SourceGraphGlb;
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

pub struct SourceGraphSceneNode {
    pub source_node: usize,
    pub parent_source_node: Option<usize>,
    pub name: Option<String>,
    pub mesh: Option<usize>,
    pub local_matrix: Mat4,
    pub local_transform: Transform,
}

/// Parent nodes precede children. Indices remain original glTF indices.
pub struct SourceGraphScenePlan {
    pub scene: usize,
    pub nodes: Vec<SourceGraphSceneNode>,
}

/// Runtime adapter identity. It is not a persistent Forge entity identifier.
#[derive(Component, Debug)]
pub struct SourceGraphNode {
    pub source_node: usize,
    pub source_mesh: Option<usize>,
}

/// Create only the declared node hierarchy beneath the host's placement root.
/// Mesh primitives, source materials and shader/pass routing must be attached
/// by the host; this does not draw geometry or manufacture material defaults.
/// Validate the complete plan before changing the caller's World.
pub fn spawn_source_graph_hierarchy(
    plan: &SourceGraphScenePlan,
    world: &mut World,
    placement_root: Entity,
) -> Result<BTreeMap<usize, Entity>, String> {
    if !world.entities().contains(placement_root) {
        return Err("source placement root does not exist".into());
    }
    if !world
        .get::<Transform>(placement_root)
        .is_some_and(|t| t.to_matrix().is_finite())
        || world.get::<GlobalTransform>(placement_root).is_none()
    {
        return Err("source placement root lacks a finite transform".into());
    }
    if plan.nodes.is_empty() || plan.nodes.len() > 100_000 {
        return Err("source hierarchy is empty or exceeds its node budget".into());
    }
    let mut seen = BTreeSet::new();
    for node in &plan.nodes {
        if node.parent_source_node.is_some_and(|p| !seen.contains(&p))
            || !seen.insert(node.source_node)
        {
            return Err("source hierarchy is not in unique parent-first order".into());
        }
        let matrix = node.local_transform.to_matrix();
        if !matrix.is_finite()
            || !node.local_matrix.is_finite()
            || node
                .local_matrix
                .to_cols_array()
                .iter()
                .zip(matrix.to_cols_array())
                .any(|(a, b)| (a - b).abs() > 1e-5 * a.abs().max(1.0))
        {
            return Err("source local transform no longer matches its declared matrix".into());
        }
    }
    let mut entities = BTreeMap::new();
    for node in &plan.nodes {
        let parent = node
            .parent_source_node
            .map_or(placement_root, |p| entities[&p]);
        let mut entity = world.spawn((
            node.local_transform,
            GlobalTransform::IDENTITY,
            Visibility::Inherited,
            ChildOf(parent),
            SourceGraphNode {
                source_node: node.source_node,
                source_mesh: node.mesh,
            },
        ));
        if let Some(name) = &node.name {
            entity.insert(Name::new(name.clone()));
        }
        entities.insert(node.source_node, entity.id());
    }
    Ok(entities)
}

/// No scene, frame, scale, or missing node is inferred from an asset name.
pub fn read_source_graph_scene(
    glb: &SourceGraphGlb<'_>,
    selected_scene: Option<usize>,
) -> Result<SourceGraphScenePlan, String> {
    read_scene_document(glb.document(), selected_scene)
}

fn index(value: &Value) -> Result<usize, String> {
    value
        .as_u64()
        .and_then(|v| usize::try_from(v).ok())
        .ok_or_else(|| "scene reference must be an unsigned integer".into())
}

fn numbers<const N: usize>(value: &Value) -> Result<[f32; N], String> {
    let values = value.as_array().ok_or("pose must be an array")?;
    if values.len() != N {
        return Err("pose array has the wrong width".into());
    }
    let mut result = [0.0; N];
    for (out, value) in result.iter_mut().zip(values) {
        *out = value.as_f64().ok_or("pose component must be numeric")? as f32;
        if !out.is_finite() {
            return Err("pose component is outside finite Float32".into());
        }
    }
    Ok(result)
}

fn local_pose(node: &Value) -> Result<(Mat4, Transform), String> {
    if let Some(matrix) = node.get("matrix") {
        if ["translation", "rotation", "scale"]
            .iter()
            .any(|key| node.get(key).is_some())
        {
            return Err("node declares both matrix and TRS".into());
        }
        let matrix = Mat4::from_cols_array(&numbers::<16>(matrix)?);
        if matrix.x_axis.w != 0.0
            || matrix.y_axis.w != 0.0
            || matrix.z_axis.w != 0.0
            || matrix.w_axis.w != 1.0
        {
            return Err("source node matrix is not affine".into());
        }
        let determinant = matrix.determinant();
        if !determinant.is_finite() || determinant == 0.0 {
            return Err("singular source matrix requires a separate adapter".into());
        }
        let transform = Transform::from_matrix(matrix);
        let reconstructed = transform.to_matrix();
        if !reconstructed.is_finite()
            || matrix
                .to_cols_array()
                .iter()
                .zip(reconstructed.to_cols_array())
                .any(|(a, b)| (a - b).abs() > 1e-5 * a.abs().max(1.0))
        {
            return Err("source matrix contains shear not representable by Bevy TRS".into());
        }
        return Ok((matrix, transform));
    }
    let translation = node
        .get("translation")
        .map(numbers::<3>)
        .transpose()?
        .unwrap_or([0.0; 3]);
    let rotation = node
        .get("rotation")
        .map(numbers::<4>)
        .transpose()?
        .unwrap_or([0.0, 0.0, 0.0, 1.0]);
    let scale = node
        .get("scale")
        .map(numbers::<3>)
        .transpose()?
        .unwrap_or([1.0; 3]);
    let rotation = Quat::from_array(rotation);
    if !rotation.is_finite() || (rotation.length_squared() - 1.0).abs() > 1e-5 {
        return Err("source rotation must be a unit quaternion; no normalization performed".into());
    }
    let transform = Transform {
        translation: Vec3::from_array(translation),
        rotation,
        scale: Vec3::from_array(scale),
    };
    let matrix = transform.to_matrix();
    if !matrix.is_finite() {
        return Err("composed source pose overflows Float32".into());
    }
    Ok((matrix, transform))
}

fn read_scene_document(
    document: &Value,
    selected_scene: Option<usize>,
) -> Result<SourceGraphScenePlan, String> {
    if document
        .get("animations")
        .is_some_and(|v| v.as_array().is_none_or(|a| !a.is_empty()))
    {
        return Err("animated source scene requires its animation adapter".into());
    }
    let scene = match selected_scene {
        Some(scene) => scene,
        None => index(
            document
                .get("scene")
                .ok_or("no default scene; select one explicitly")?,
        )?,
    };
    let scenes = document
        .get("scenes")
        .and_then(Value::as_array)
        .ok_or("source scenes missing")?;
    let roots = scenes
        .get(scene)
        .and_then(|v| v.get("nodes"))
        .and_then(Value::as_array)
        .ok_or("selected source scene roots missing")?;
    if roots.is_empty() {
        return Err("selected source scene is empty".into());
    }
    let nodes = document
        .get("nodes")
        .and_then(Value::as_array)
        .ok_or("source nodes missing")?;
    if nodes.len() > 100_000 {
        return Err("source scene exceeds node budget".into());
    }
    let meshes = document.get("meshes").and_then(Value::as_array);
    let mut pending = Vec::new();
    for root in roots.iter().rev() {
        pending.push((index(root)?, None, 0usize));
    }
    let mut visited = BTreeSet::new();
    let mut output = Vec::new();
    while let Some((source_node, parent_source_node, depth)) = pending.pop() {
        if depth > 256 {
            return Err("source hierarchy exceeds depth budget".into());
        }
        if !visited.insert(source_node) {
            return Err("source hierarchy has a cycle, shared child, or repeated root".into());
        }
        let node = nodes
            .get(source_node)
            .ok_or("source node reference out of range")?;
        if !node.is_object() {
            return Err("source node is not an object".into());
        }
        if ["skin", "weights", "camera"]
            .iter()
            .any(|key| node.get(key).is_some())
            || node.get("extensions").is_some_and(|v| {
                v.as_object().is_none_or(|extensions| {
                    extensions.iter().any(|(name, value)| {
                        // Hierarchy admission preserves the existing node only. The
                        // host must separately validate and mount its light adapter.
                        if name != "KHR_lights_punctual" {
                            return true;
                        }
                        let Some(light) = value
                            .as_object()
                            .filter(|v| v.len() == 1)
                            .and_then(|v| v.get("light"))
                            .and_then(Value::as_u64)
                            .and_then(|v| usize::try_from(v).ok())
                        else {
                            return true;
                        };
                        document
                            .pointer("/extensions/KHR_lights_punctual/lights")
                            .and_then(Value::as_array)
                            .and_then(|v| v.get(light))
                            .is_none()
                    })
                })
            })
        {
            return Err("source node has rig, morph, camera, or extension content requiring its own adapter".into());
        }
        let mesh = node.get("mesh").map(index).transpose()?;
        if let Some(mesh) = mesh {
            let mesh = meshes
                .and_then(|m| m.get(mesh))
                .ok_or("source mesh reference out of range")?;
            if mesh.get("weights").is_some() {
                return Err("source morph mesh requires its own adapter".into());
            }
            let primitives = mesh
                .get("primitives")
                .and_then(Value::as_array)
                .ok_or("source mesh primitives missing")?;
            if primitives.is_empty() || primitives.iter().any(|p| p.get("targets").is_some()) {
                return Err("empty or morph source mesh cannot become a static scene".into());
            }
        }
        let name = node
            .get("name")
            .map(|v| {
                v.as_str()
                    .map(str::to_owned)
                    .ok_or("source name must be text")
            })
            .transpose()?;
        let (local_matrix, local_transform) = local_pose(node)?;
        output.push(SourceGraphSceneNode {
            source_node,
            parent_source_node,
            name,
            mesh,
            local_matrix,
            local_transform,
        });
        if let Some(children) = node.get("children") {
            for child in children
                .as_array()
                .ok_or("source children must be an array")?
                .iter()
                .rev()
            {
                pending.push((index(child)?, Some(source_node), depth + 1));
            }
        }
    }
    Ok(SourceGraphScenePlan {
        scene,
        nodes: output,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn source_scene_light_reference_requires_known_extension_and_existing_definition() {
        let base = serde_json::json!({"scene":0,"scenes":[{"nodes":[0]}],
            "nodes":[{"extensions":{"KHR_lights_punctual":{"light":0}}}],
            "extensions":{"KHR_lights_punctual":{"lights":[{"type":"point"}]}}});
        assert!(read_scene_document(&base, None).is_ok());
        for extension in [
            serde_json::json!({"UNKNOWN":{}}),
            serde_json::json!({"KHR_lights_punctual":{"light":1}}),
            serde_json::json!({"KHR_lights_punctual":{"light":-1}}),
            serde_json::json!({"KHR_lights_punctual":{"light":0,"extra":true}}),
        ] {
            let mut document = base.clone();
            document["nodes"][0]["extensions"] = extension;
            assert!(read_scene_document(&document, None).is_err());
        }
    }
    #[test]
    fn source_hierarchy_world_preserves_host_root_and_fails_before_mutation() {
        let d = serde_json::json!({"scene":0,"scenes":[{"nodes":[1]}],"nodes":[
            {"translation":[3,0,0]},{"name":"frame","translation":[1,2,3],"children":[0]}
        ]});
        let mut plan = read_scene_document(&d, None).unwrap();
        let mut world = World::new();
        let root = world.spawn(Transform::from_xyz(100.0, 0.0, 0.0)).id();
        let entities = spawn_source_graph_hierarchy(&plan, &mut world, root).unwrap();
        assert_eq!(world.get::<ChildOf>(entities[&1]).unwrap().parent(), root);
        assert_eq!(
            world.get::<ChildOf>(entities[&0]).unwrap().parent(),
            entities[&1]
        );
        assert_eq!(world.get::<Name>(entities[&1]).unwrap().as_str(), "frame");
        assert_eq!(world.get::<Transform>(root).unwrap().translation.x, 100.0);
        assert_eq!(
            world.get::<Transform>(entities[&0]).unwrap().translation.x,
            3.0
        );
        assert_eq!(
            world
                .get::<SourceGraphNode>(entities[&0])
                .unwrap()
                .source_node,
            0
        );
        assert_eq!(
            *world.get::<Visibility>(entities[&0]).unwrap(),
            Visibility::Inherited
        );
        let count = world.entities().len();
        plan.nodes.swap(0, 1);
        assert!(spawn_source_graph_hierarchy(&plan, &mut world, root).is_err());
        assert_eq!(world.entities().len(), count);
        plan.nodes.swap(0, 1);
        plan.nodes[1].local_transform.translation.x = 999.0;
        assert!(spawn_source_graph_hierarchy(&plan, &mut world, root).is_err());
        assert_eq!(world.entities().len(), count);
        world.despawn(root);
        assert!(spawn_source_graph_hierarchy(&plan, &mut world, root).is_err());
    }
    #[test]
    fn source_scene_preserves_hierarchy_negative_scale_and_local_pivot() {
        let d = serde_json::json!({"scene":0,"scenes":[{"nodes":[1]}],"nodes":[
            {"translation":[3,0,0],"mesh":0},{"name":"frame","translation":[1,2,3],"scale":[-2,1,1],"children":[0]}
        ],"meshes":[{"primitives":[{}]}]});
        let p = read_scene_document(&d, None).unwrap();
        assert_eq!(
            (p.nodes[0].source_node, p.nodes[1].parent_source_node),
            (1, Some(1))
        );
        assert_eq!(p.nodes[1].mesh, Some(0));
        assert_eq!(
            (p.nodes[0].local_matrix * p.nodes[1].local_matrix).transform_point3(Vec3::ZERO),
            Vec3::new(-5.0, 2.0, 3.0)
        );
    }
    #[test]
    fn source_scene_refuses_graph_and_pose_loss() {
        let base =
            serde_json::json!({"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"children":[1]},{}]});
        for change in [
            serde_json::json!({"children":[0]}),
            serde_json::json!({"children":[1,1]}),
            serde_json::json!({"children":[2]}),
            serde_json::json!({"rotation":[0,0,0,2]}),
            serde_json::json!({"mesh":0}),
            serde_json::json!({"skin":0}),
            serde_json::json!({"matrix":[1,0,0,0,0.5,1,0,0,0,0,1,0,0,0,0,1]}),
            serde_json::json!({"translation":[0,0,0],"matrix":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}),
        ] {
            let mut d = base.clone();
            d["nodes"][0] = change;
            assert!(read_scene_document(&d, None).is_err());
        }
        let mut d = base;
        d.as_object_mut().unwrap().remove("scene");
        assert!(read_scene_document(&d, None).is_err());
        assert!(read_scene_document(&d, Some(0)).is_ok());
    }
    #[test]
    fn source_scene_accepts_explicit_matrix_without_axis_or_unit_substitution() {
        let d = serde_json::json!({"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"matrix":[0.01,0,0,0,0,0.01,0,0,0,0,0.01,0,7,8,9,1]}]});
        let p = read_scene_document(&d, None).unwrap();
        assert_eq!(
            p.nodes[0].local_matrix.w_axis.truncate(),
            Vec3::new(7.0, 8.0, 9.0)
        );
        assert!((p.nodes[0].local_transform.scale.x - 0.01).abs() < 1e-7);
    }
}
