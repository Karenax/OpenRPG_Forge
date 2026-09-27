use super::*;
use bevy::{
    asset::RenderAssetUsages,
    gltf::{GltfExtras, GltfPrimitive},
    render::render_resource::PrimitiveTopology,
};
use forge_core::ContentHash;

struct Fixture {
    binding: VegetationPrototypeBinding,
    handles: Vec<Handle<GltfNode>>,
    nodes: Assets<GltfNode>,
    gltf_meshes: Assets<GltfMesh>,
    meshes: Assets<Mesh>,
    materials: Assets<GltfMaterial>,
}

impl Fixture {
    fn new() -> Self {
        let mut meshes = Assets::<Mesh>::default();
        let mesh = meshes.add(
            Mesh::new(PrimitiveTopology::TriangleList, RenderAssetUsages::all())
                .with_inserted_attribute(
                    Mesh::ATTRIBUTE_POSITION,
                    vec![[-1.0, 0.0, 0.0], [1.0, 6.0, 0.0], [0.0, 0.0, 1.0]],
                )
                .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, vec![[0.0, 0.0]; 3]),
        );
        let mut materials = Assets::<GltfMaterial>::default();
        let material = materials.add(GltfMaterial::default());
        let mut gltf_meshes = Assets::<GltfMesh>::default();
        let gltf_mesh = gltf_meshes.add(GltfMesh {
            index: 0,
            name: "shared".into(),
            extras: None,
            primitives: vec![GltfPrimitive {
                index: 0,
                parent_mesh_index: 0,
                name: "part".into(),
                mesh,
                material: Some(material),
                extras: None,
                material_extras: None,
            }],
        });
        let mut nodes = Assets::<GltfNode>::default();
        let handles = (0..4)
            .map(|lod| {
                nodes.add(GltfNode {
                    index: lod,
                    name: format!("node-{lod}"),
                    children: vec![],
                    mesh: Some(gltf_mesh.clone()),
                    skin: None,
                    transform: Transform::from_xyz(1.0, 2.0, 3.0),
                    is_animation_root: false,
                    extras: Some(GltfExtras {
                        value: serde_json::json!({"forge_asset_id":"tree",
                "forge_role":if lod == 3 {"collision"} else {"render"}, "forge_lod":lod})
                        .to_string(),
                    }),
                })
            })
            .collect();
        Self {
            binding: VegetationPrototypeBinding {
                layer_id: "canopy".into(),
                asset_id: "tree".into(),
                glb_sha256: ContentHash::from_bytes([1; 32]),
                source_pivot_m: [1.0, 2.0, 3.0],
                source_height_m: 6.0,
                target_height_m: 3.0,
            },
            handles,
            nodes,
            gltf_meshes,
            meshes,
            materials,
        }
    }

    fn resolve(&self) -> Result<ResolvedVegetationPrototype, VegetationPrototypeResolveError> {
        resolve_vegetation_prototype(
            &self.binding,
            &self.handles,
            &self.nodes,
            &self.gltf_meshes,
            &self.meshes,
            &self.materials,
        )
    }
}

#[test]
fn shares_handles_preserves_sources_normalizes_root_and_skips_collision() {
    let fixture = Fixture::new();
    let result = fixture.resolve().unwrap();
    assert_eq!(
        result.lods.iter().map(Vec::len).collect::<Vec<_>>(),
        vec![1, 1, 1]
    );
    for lod in &result.lods {
        assert_eq!(lod[0].mesh, result.lods[0][0].mesh);
        assert_eq!(lod[0].material, result.lods[0][0].material);
        assert!(lod[0].local_transform.transform_point3(Vec3::ZERO).length() < 1e-6);
        assert!(
            (lod[0].local_transform.transform_point3(Vec3::Y * 6.0) - Vec3::Y * 3.0).length()
                < 1e-6
        );
    }
    assert_eq!(fixture.nodes.len(), 4);
    assert_eq!(fixture.meshes.len(), 1);
    assert_eq!(fixture.materials.len(), 1);
    let positions = fixture
        .meshes
        .get(&result.lods[0][0].mesh)
        .unwrap()
        .attribute(Mesh::ATTRIBUTE_POSITION)
        .unwrap();
    assert!(
        matches!(positions, VertexAttributeValues::Float32x3(p) if p.len() == 3 && (p[1][1] - 6.0).abs() < 1e-6)
    );
}

#[test]
fn pending_missing_lod_and_bad_dimensions_are_distinct() {
    let mut fixture = Fixture::new();
    fixture.binding.source_height_m = 10.0;
    assert_eq!(
        fixture.resolve().unwrap_err(),
        VegetationPrototypeResolveError::DimensionMismatch
    );
    fixture.binding.source_height_m = 6.0;
    let handle = fixture.handles.remove(2);
    assert_eq!(
        fixture.resolve().unwrap_err(),
        VegetationPrototypeResolveError::MissingLod
    );
    fixture.handles.push(handle.clone());
    fixture.nodes.remove(handle.id());
    assert_eq!(
        fixture.resolve().unwrap_err(),
        VegetationPrototypeResolveError::PendingDependency
    );
}

#[test]
fn rejects_hierarchy_and_missing_uv_instead_of_silent_projection() {
    let mut fixture = Fixture::new();
    let first = fixture.handles[0].clone();
    fixture
        .nodes
        .get_mut(&first)
        .unwrap()
        .children
        .push(first.clone());
    assert!(matches!(
        fixture.resolve(),
        Err(VegetationPrototypeResolveError::UnsupportedNode(_))
    ));
    fixture.nodes.get_mut(&first).unwrap().children.clear();
    let result = fixture.resolve().unwrap();
    fixture
        .meshes
        .get_mut(&result.lods[0][0].mesh)
        .unwrap()
        .remove_attribute(Mesh::ATTRIBUTE_UV_0);
    assert!(matches!(
        fixture.resolve(),
        Err(VegetationPrototypeResolveError::InvalidMesh(_))
    ));
}
