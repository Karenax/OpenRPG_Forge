//! Common attachment to an existing preview or `ManualPlacement` root.
//! The caller registers source programs/images/uniforms from pinned intake and
//! qualifies pass routing before making this hidden root visible. This adapter
//! does not manufacture textures, tangents, camera mappings or GPU evidence.
#[cfg(target_arch = "wasm32")]
use bevy::ecs as bevy_ecs;
use bevy::mesh::{Indices, MeshVertexAttribute, PrimitiveTopology, VertexAttributeValues};
use bevy::prelude::*;
use forge_assets::source_graph_attributes::SourceGraphPrimitiveGeometry;
use forge_engine::{
    source_graph::SourceGraphMaterial,
    source_graph_scene::spawn_source_graph_hierarchy,
    source_graph_scene_geometry::{
        ScenePrimitiveRoute, SourceGraphSceneGeometry, SourcePrimitiveKey,
    },
};
use std::collections::{BTreeMap, BTreeSet};

pub(super) struct OrdinaryPrimitive {
    pub mesh: Handle<Mesh>,
    pub material_index: Option<usize>,
    /// Includes an original glTF loader default, if that loader supplied one.
    /// A source-graph material can never use this route.
    pub material: Handle<StandardMaterial>,
}

#[derive(Default)]
pub(super) struct SceneBindings {
    pub source_meshes: BTreeMap<SourcePrimitiveKey, Handle<Mesh>>,
    /// Material instances retain their explicit per-node source uniforms.
    pub source_materials: BTreeMap<(usize, usize), Handle<SourceGraphMaterial>>,
    pub ordinary: BTreeMap<SourcePrimitiveKey, OrdinaryPrimitive>,
}

#[derive(Component)]
pub(super) struct SourceSceneAttached;

pub(super) struct AttachedScene {
    pub nodes: BTreeMap<usize, Entity>,
    pub primitives: Vec<Entity>,
}

fn matches3(mesh: &Mesh, attribute: MeshVertexAttribute, values: &[[f32; 3]]) -> bool {
    matches!(mesh.attribute(attribute), Some(VertexAttributeValues::Float32x3(actual)) if actual.as_slice() == values)
}

fn matches2(mesh: &Mesh, attribute: MeshVertexAttribute, values: Option<&[[f32; 2]]>) -> bool {
    match (mesh.attribute(attribute), values) {
        (None, None) => true,
        (Some(VertexAttributeValues::Float32x2(actual)), Some(expected)) => {
            actual.as_slice() == expected
        }
        _ => false,
    }
}

fn matches4(mesh: &Mesh, attribute: MeshVertexAttribute, values: Option<&[[f32; 4]]>) -> bool {
    match (mesh.attribute(attribute), values) {
        (None, None) => true,
        (Some(VertexAttributeValues::Float32x4(actual)), Some(expected)) => {
            actual.as_slice() == expected
        }
        _ => false,
    }
}

fn geometry_matches(mesh: &Mesh, expected: &SourceGraphPrimitiveGeometry) -> bool {
    use forge_engine::source_graph as source;
    mesh.primitive_topology() == PrimitiveTopology::TriangleList
        && matches!(mesh.indices(), Some(Indices::U32(actual)) if actual == &expected.indices)
        && matches3(mesh, Mesh::ATTRIBUTE_POSITION, &expected.positions)
        && matches3(mesh, Mesh::ATTRIBUTE_NORMAL, &expected.normals)
        && matches4(mesh, Mesh::ATTRIBUTE_TANGENT, expected.tangents.as_deref())
        && matches2(mesh, Mesh::ATTRIBUTE_UV_0, Some(&expected.uv0))
        && matches3(
            mesh,
            source::SOURCE_GRAPH_NORMAL,
            &expected.source.original_normals,
        )
        && matches4(
            mesh,
            source::SOURCE_GRAPH_TANGENT,
            expected.source.original_tangents.as_deref(),
        )
        && matches4(
            mesh,
            source::SOURCE_GRAPH_UV0,
            Some(&expected.source.original_uv0),
        )
        && matches4(
            mesh,
            source::SOURCE_GRAPH_COLOR,
            expected.source.original_colors.as_deref(),
        )
        && source::SOURCE_GRAPH_STANDARD_ADDITIONAL_UVS
            .iter()
            .zip(&expected.additional_uvs)
            .all(|(attribute, values)| matches2(mesh, *attribute, values.as_deref()))
        && source::SOURCE_GRAPH_ORIGINAL_ADDITIONAL_UVS
            .iter()
            .zip(&expected.source.original_additional_uvs)
            .all(|(attribute, values)| matches4(mesh, *attribute, values.as_deref()))
}

/// Fail before creating entities when a route or a resident binding is missing.
/// A hidden host root keeps async registration/pass setup out of the visible
/// scene. Root transforms, selection markers and persisted placement data stay
/// owned by the existing common editor path.
#[allow(
    clippy::too_many_lines,
    reason = "Keep the ordered scene attachment and its ownership bookkeeping together."
)]
pub(super) fn attach(
    world: &mut World,
    root: Entity,
    scene: &SourceGraphSceneGeometry,
    bindings: &SceneBindings,
) -> Result<AttachedScene, String> {
    if scene.instances.is_empty() {
        return Err("Source scene has no primitive instances".into());
    }
    if world.get::<Visibility>(root) != Some(&Visibility::Hidden)
        || world.get::<SourceSceneAttached>(root).is_some()
    {
        return Err("Source scene requires a hidden, unattached host root".into());
    }
    if scene.absent_original_tangents().next().is_some() {
        return Err(
            "Absent original tangent requires a separately qualified native-default adapter".into(),
        );
    }
    let mut source_meshes = BTreeSet::new();
    let mut source_materials = BTreeSet::new();
    let mut ordinary = BTreeSet::new();
    let source_nodes: BTreeSet<_> = scene
        .hierarchy
        .nodes
        .iter()
        .map(|n| n.source_node)
        .collect();
    for primitive in &scene.instances {
        if !source_nodes.contains(&primitive.source_node) {
            return Err("Primitive references an absent source node".into());
        }
        match primitive.route {
            ScenePrimitiveRoute::SourceGraph { material } => {
                if scene
                    .source_geometries
                    .get(&primitive.geometry)
                    .is_none_or(|g| g.source.material_index != material)
                {
                    return Err(
                        "Source primitive material differs from its retained geometry".into(),
                    );
                }
                source_meshes.insert(primitive.geometry);
                source_materials.insert((primitive.source_node, material));
            }
            ScenePrimitiveRoute::HostGltf { material } => {
                if bindings
                    .ordinary
                    .get(&primitive.geometry)
                    .is_none_or(|p| p.material_index != material)
                {
                    return Err("Ordinary primitive material index changed".into());
                }
                ordinary.insert(primitive.geometry);
            }
        }
    }
    if source_meshes != bindings.source_meshes.keys().copied().collect()
        || source_materials != bindings.source_materials.keys().copied().collect()
        || ordinary != bindings.ordinary.keys().copied().collect()
        || source_meshes != scene.source_geometries.keys().copied().collect()
    {
        return Err("Native binding sets differ from the complete scene routes".into());
    }
    let meshes = world
        .get_resource::<Assets<Mesh>>()
        .ok_or("Mesh assets unavailable")?;
    for handle in bindings
        .source_meshes
        .values()
        .chain(bindings.ordinary.values().map(|p| &p.mesh))
    {
        if meshes.get(handle).is_none() {
            return Err("Scene mesh is not resident".into());
        }
    }
    for (key, handle) in &bindings.source_meshes {
        if !geometry_matches(
            meshes.get(handle).expect("Residency checked"),
            &scene.source_geometries[key],
        ) {
            return Err("Native mesh differs from its retained source streams".into());
        }
    }
    if !source_materials.is_empty() {
        let materials = world
            .get_resource::<Assets<SourceGraphMaterial>>()
            .ok_or("Source material assets unavailable")?;
        for handle in bindings.source_materials.values() {
            if materials.get(handle).is_none() {
                return Err("Original source material is not resident".into());
            }
        }
    }
    if !ordinary.is_empty() {
        let materials = world
            .get_resource::<Assets<StandardMaterial>>()
            .ok_or("Ordinary glTF material assets unavailable")?;
        for primitive in bindings.ordinary.values() {
            if materials.get(&primitive.material).is_none() {
                return Err("Original ordinary glTF material is not resident".into());
            }
        }
    }
    // The existing hierarchy adapter validates root pose, local matrices and
    // parent order in full before mutation. No fallible work follows it here.
    let nodes = spawn_source_graph_hierarchy(&scene.hierarchy, world, root)?;
    let mut primitives = Vec::with_capacity(scene.instances.len());
    for primitive in &scene.instances {
        let parent = nodes[&primitive.source_node];
        let entity = match primitive.route {
            ScenePrimitiveRoute::SourceGraph { material } => world
                .spawn((
                    Mesh3d(bindings.source_meshes[&primitive.geometry].clone()),
                    MeshMaterial3d(
                        bindings.source_materials[&(primitive.source_node, material)].clone(),
                    ),
                    Transform::IDENTITY,
                    Visibility::Inherited,
                    ChildOf(parent),
                ))
                .id(),
            ScenePrimitiveRoute::HostGltf { .. } => {
                let original = &bindings.ordinary[&primitive.geometry];
                world
                    .spawn((
                        Mesh3d(original.mesh.clone()),
                        MeshMaterial3d(original.material.clone()),
                        Transform::IDENTITY,
                        Visibility::Inherited,
                        ChildOf(parent),
                    ))
                    .id()
            }
        };
        primitives.push(entity);
    }
    world.entity_mut(root).insert(SourceSceneAttached);
    Ok(AttachedScene { nodes, primitives })
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::asset::RenderAssetUsages;
    use forge_engine::source_graph_scene::{SourceGraphSceneNode, SourceGraphScenePlan};
    use forge_engine::source_graph_scene_geometry::SourceScenePrimitive;

    fn fixture() -> (World, Entity, SourceGraphSceneGeometry, SceneBindings) {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<StandardMaterial>>();
        let root = world
            .spawn((
                Transform::from_xyz(9.0, 4.0, -3.0),
                GlobalTransform::IDENTITY,
                Visibility::Hidden,
                Name::new("Common placement root"),
            ))
            .id();
        let local = Transform::from_xyz(2.0, 3.0, 4.0).with_scale(Vec3::new(-1.0, 2.0, 1.0));
        let node =
            |source_node, parent_source_node, local_transform: Transform| SourceGraphSceneNode {
                source_node,
                parent_source_node,
                name: None,
                mesh: Some(0),
                local_matrix: local_transform.to_matrix(),
                local_transform,
            };
        let key = SourcePrimitiveKey {
            mesh: 0,
            primitive: 0,
        };
        let scene = SourceGraphSceneGeometry {
            hierarchy: SourceGraphScenePlan {
                scene: 0,
                nodes: vec![node(3, None, local), node(7, Some(3), Transform::IDENTITY)],
            },
            instances: [3, 7]
                .into_iter()
                .map(|source_node| SourceScenePrimitive {
                    source_node,
                    geometry: key,
                    route: ScenePrimitiveRoute::HostGltf { material: Some(2) },
                })
                .collect(),
            source_geometries: BTreeMap::new(),
        };
        let mesh = world.resource_mut::<Assets<Mesh>>().add(Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
        ));
        let material = world
            .resource_mut::<Assets<StandardMaterial>>()
            .add(StandardMaterial::default());
        let mut bindings = SceneBindings::default();
        bindings.ordinary.insert(
            key,
            OrdinaryPrimitive {
                mesh,
                material,
                material_index: Some(2),
            },
        );
        (world, root, scene, bindings)
    }

    #[test]
    fn common_host_keeps_shared_mesh_hierarchy_and_placement_root() {
        let (mut world, root, scene, bindings) = fixture();
        let original_root = *world.get::<Transform>(root).unwrap();
        let attached = attach(&mut world, root, &scene, &bindings).unwrap();
        assert_eq!(*world.get::<Transform>(root).unwrap(), original_root);
        assert_eq!(
            world.get::<Name>(root).unwrap().as_str(),
            "Common placement root"
        );
        assert_eq!(attached.nodes.len(), 2);
        assert_eq!(attached.primitives.len(), 2);
        assert_eq!(
            world.get::<ChildOf>(attached.nodes[&3]).unwrap().parent(),
            root
        );
        assert_eq!(
            world.get::<ChildOf>(attached.nodes[&7]).unwrap().parent(),
            attached.nodes[&3]
        );
        assert_eq!(
            *world.get::<Transform>(attached.nodes[&3]).unwrap(),
            scene.hierarchy.nodes[0].local_transform
        );
        let handles: Vec<_> = attached
            .primitives
            .iter()
            .map(|e| world.get::<Mesh3d>(*e).unwrap().0.id())
            .collect();
        assert_eq!(handles[0], handles[1]);
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 1);
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
    }

    #[test]
    fn common_host_refuses_missing_resident_material_before_mutation() {
        let (mut world, root, scene, bindings) = fixture();
        let handle = &bindings.ordinary.values().next().unwrap().material;
        world
            .resource_mut::<Assets<StandardMaterial>>()
            .remove(handle.id());
        let count = world.entities().len();
        assert!(attach(&mut world, root, &scene, &bindings).is_err());
        assert_eq!(world.entities().len(), count);
        assert!(world.get::<SourceSceneAttached>(root).is_none());
    }

    #[test]
    fn common_host_refuses_source_graph_on_an_ordinary_route() {
        let (mut world, root, mut scene, bindings) = fixture();
        scene.instances[0].route = ScenePrimitiveRoute::SourceGraph { material: 2 };
        let count = world.entities().len();
        assert!(attach(&mut world, root, &scene, &bindings).is_err());
        assert_eq!(world.entities().len(), count);
    }

    #[test]
    fn common_host_refuses_material_index_swap_before_mutation() {
        let (mut world, root, scene, mut bindings) = fixture();
        bindings
            .ordinary
            .values_mut()
            .next()
            .unwrap()
            .material_index = Some(1);
        let count = world.entities().len();
        assert!(attach(&mut world, root, &scene, &bindings).is_err());
        assert_eq!(world.entities().len(), count);
    }

    #[test]
    fn common_host_refuses_invalid_local_pose_before_mutation() {
        let (mut world, root, mut scene, bindings) = fixture();
        scene.hierarchy.nodes[1].local_matrix = Mat4::from_translation(Vec3::X);
        let count = world.entities().len();
        assert!(attach(&mut world, root, &scene, &bindings).is_err());
        assert_eq!(world.entities().len(), count);
    }

    #[test]
    fn common_host_refuses_repeated_attachment_and_visible_root() {
        let (mut world, root, scene, bindings) = fixture();
        world.entity_mut(root).insert(Visibility::Inherited);
        assert!(attach(&mut world, root, &scene, &bindings).is_err());
        world.entity_mut(root).insert(Visibility::Hidden);
        attach(&mut world, root, &scene, &bindings).unwrap();
        let count = world.entities().len();
        assert!(attach(&mut world, root, &scene, &bindings).is_err());
        assert_eq!(world.entities().len(), count);
    }
}
