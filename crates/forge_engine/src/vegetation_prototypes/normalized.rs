//! Normalize once per unique primitive, never once per scatter placement.
use super::ResolvedVegetationPrototype;
use bevy::{gltf::GltfMaterial, math::Affine3A, mesh::VertexAttributeValues, prelude::*};
use std::collections::BTreeMap;

#[derive(Clone, Debug)]
pub struct RenderVegetationPrimitive {
    /// Root-normalized mesh; apply only the canonical placement transform when spawning.
    pub mesh: Handle<Mesh>,
    pub material: Handle<StandardMaterial>,
}

#[derive(Clone, Debug)]
pub struct RenderVegetationPrototype {
    pub target_height_m: f32,
    pub lods: [Vec<RenderVegetationPrimitive>; 3],
}

type MeshKey = (AssetId<Mesh>, [u32; 12]);

/// Validate all derived meshes before adding any of them to the asset store.
pub(super) fn prepare_render_prototypes(
    prototypes: &BTreeMap<String, ResolvedVegetationPrototype>,
    materials: &BTreeMap<AssetId<GltfMaterial>, Handle<StandardMaterial>>,
    meshes: &mut Assets<Mesh>,
) -> Result<(BTreeMap<String, RenderVegetationPrototype>, usize), String> {
    let mut derived = BTreeMap::<MeshKey, Mesh>::new();
    for primitive in prototypes
        .values()
        .flat_map(|prototype| prototype.lods.iter().flatten())
    {
        if !materials.contains_key(&primitive.material.id()) {
            return Err("missing shared PBR material for normalized primitive".into());
        }
        let key = mesh_key(primitive.mesh.id(), primitive.local_transform);
        if let std::collections::btree_map::Entry::Vacant(entry) = derived.entry(key) {
            let source = meshes
                .get(&primitive.mesh)
                .ok_or("missing source mesh during normalization")?;
            entry.insert(normalize_mesh(source, primitive.local_transform)?);
        }
    }
    let handles: BTreeMap<_, _> = derived
        .into_iter()
        .map(|(key, mesh)| (key, meshes.add(mesh)))
        .collect();
    let layers = prototypes
        .iter()
        .map(|(layer, prototype)| {
            let lods = prototype.lods.each_ref().map(|primitives| {
                primitives
                    .iter()
                    .map(|primitive| {
                        // Both maps were checked above; publication occurs only after the full validation pass.
                        RenderVegetationPrimitive {
                            mesh: handles
                                [&mesh_key(primitive.mesh.id(), primitive.local_transform)]
                                .clone(),
                            material: materials[&primitive.material.id()].clone(),
                        }
                    })
                    .collect()
            });
            (
                layer.clone(),
                RenderVegetationPrototype {
                    lods,
                    target_height_m: prototype.target_height_m,
                },
            )
        })
        .collect();
    Ok((layers, handles.len()))
}

fn mesh_key(mesh: AssetId<Mesh>, transform: Affine3A) -> MeshKey {
    (mesh, transform.to_cols_array().map(f32::to_bits))
}

fn normalize_mesh(source: &Mesh, affine: Affine3A) -> Result<Mesh, String> {
    if !affine.is_finite() || affine.matrix3.determinant() <= 0.0 {
        return Err("invalid prototype normalization transform".into());
    }
    let transform = Transform::from_matrix(Mat4::from(affine));
    // Flat GLB node TRS and uniform normalization cannot introduce shear. Reject it
    // explicitly instead of silently losing authored geometry through decomposition.
    if !transform.compute_affine().abs_diff_eq(affine, 0.0001) {
        return Err("prototype normalization contains unsupported shear".into());
    }
    let mut mesh = source.clone();
    mesh.try_transform_by(transform)
        .map_err(|error| error.to_string())?;
    let Some(VertexAttributeValues::Float32x3(positions)) =
        mesh.attribute(Mesh::ATTRIBUTE_POSITION)
    else {
        return Err("normalized mesh has no readable positions".into());
    };
    let Some(VertexAttributeValues::Float32x3(normals)) = mesh.attribute(Mesh::ATTRIBUTE_NORMAL)
    else {
        return Err("normalized PBR mesh has no normals".into());
    };
    if positions.is_empty()
        || normals.len() != positions.len()
        || positions
            .iter()
            .any(|point| !Vec3::from_array(*point).is_finite())
        || normals.iter().any(|normal| {
            let normal = Vec3::from_array(*normal);
            !normal.is_finite() || normal.length_squared() < 0.0001
        })
    {
        return Err("invalid normalized PBR positions or normals".into());
    }
    Ok(mesh)
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::RenderAssetUsages, render::render_resource::PrimitiveTopology};

    fn mesh() -> Mesh {
        Mesh::new(PrimitiveTopology::TriangleList, RenderAssetUsages::all())
            .with_inserted_attribute(
                Mesh::ATTRIBUTE_POSITION,
                vec![[1., 2., 3.], [1., 8., 3.], [2., 2., 3.]],
            )
            .with_inserted_attribute(Mesh::ATTRIBUTE_NORMAL, vec![[0., 0., 1.]; 3])
            .with_inserted_attribute(Mesh::ATTRIBUTE_TANGENT, vec![[1., 0., 0., -1.]; 3])
            .with_inserted_attribute(Mesh::ATTRIBUTE_UV_0, vec![[0.2, 0.7]; 3])
            .with_inserted_attribute(Mesh::ATTRIBUTE_UV_1, vec![[0.8, 0.3]; 3])
            .with_inserted_attribute(Mesh::ATTRIBUTE_COLOR, vec![[0.2, 0.3, 0.4, 0.6]; 3])
    }

    #[test]
    fn normalization_preserves_uvs_colors_tangents_and_source() {
        let source = mesh();
        let transform = Affine3A::from_scale_rotation_translation(
            Vec3::splat(0.5),
            Quat::IDENTITY,
            -Vec3::new(1., 2., 3.) * 0.5,
        );
        let result = normalize_mesh(&source, transform).unwrap();
        let VertexAttributeValues::Float32x3(positions) =
            result.attribute(Mesh::ATTRIBUTE_POSITION).unwrap()
        else {
            panic!()
        };
        assert!(Vec3::from_array(positions[0]).abs_diff_eq(Vec3::ZERO, 0.00001));
        assert!(Vec3::from_array(positions[1]).abs_diff_eq(Vec3::Y * 3., 0.00001));
        for attribute in [
            Mesh::ATTRIBUTE_UV_0,
            Mesh::ATTRIBUTE_UV_1,
            Mesh::ATTRIBUTE_COLOR,
            Mesh::ATTRIBUTE_TANGENT,
        ] {
            assert_eq!(
                result.attribute(attribute).unwrap().get_bytes(),
                source.attribute(attribute).unwrap().get_bytes()
            );
        }
        let VertexAttributeValues::Float32x3(original) =
            source.attribute(Mesh::ATTRIBUTE_POSITION).unwrap()
        else {
            panic!()
        };
        assert_eq!(
            original[0].map(f32::to_bits),
            [1_f32, 2., 3.].map(f32::to_bits)
        );
    }

    #[test]
    fn normalization_shares_meshes_across_lods_and_rejects_before_allocation() {
        let mut meshes = Assets::<Mesh>::default();
        let source = meshes.add(mesh());
        let mut materials = Assets::<GltfMaterial>::default();
        let source_material = materials.add(GltfMaterial::default());
        let mut pbr = Assets::<StandardMaterial>::default();
        let pbr_handle = pbr.add(StandardMaterial::default());
        let material_map = BTreeMap::from([(source_material.id(), pbr_handle.clone())]);
        let primitive = super::super::ResolvedVegetationPrimitive {
            mesh: source.clone(),
            material: source_material,
            local_transform: Affine3A::IDENTITY,
        };
        let mut prototypes = BTreeMap::from([(
            "canopy".into(),
            ResolvedVegetationPrototype {
                target_height_m: 3.0,
                lods: std::array::from_fn(|_| vec![primitive.clone()]),
                source_min: Vec3::ZERO,
                source_max: Vec3::ONE,
            },
        )]);
        let (render, count) =
            prepare_render_prototypes(&prototypes, &material_map, &mut meshes).unwrap();
        assert_eq!(count, 1);
        assert_eq!(meshes.len(), 2);
        let lods = &render["canopy"].lods;
        assert_eq!(lods[0][0].mesh, lods[2][0].mesh);
        assert_ne!(lods[0][0].mesh, source);
        assert_eq!(lods[0][0].material, pbr_handle);
        prototypes.get_mut("canopy").unwrap().lods[2][0].local_transform =
            Affine3A::from_scale(Vec3::ZERO);
        assert!(prepare_render_prototypes(&prototypes, &material_map, &mut meshes).is_err());
        assert_eq!(meshes.len(), 2);
    }

    #[test]
    fn normalization_transforms_pbr_basis_and_rejects_shear_or_missing_normals() {
        let mut source = mesh();
        let affine = Affine3A::from_scale_rotation_translation(
            Vec3::new(2., 0.5, 3.),
            Quat::from_rotation_y(std::f32::consts::FRAC_PI_2),
            Vec3::ZERO,
        );
        let result = normalize_mesh(&source, affine).unwrap();
        let VertexAttributeValues::Float32x3(normals) =
            result.attribute(Mesh::ATTRIBUTE_NORMAL).unwrap()
        else {
            panic!()
        };
        let VertexAttributeValues::Float32x4(tangents) =
            result.attribute(Mesh::ATTRIBUTE_TANGENT).unwrap()
        else {
            panic!()
        };
        assert!(Vec3::from_array(normals[0]).abs_diff_eq(Vec3::X, 0.00001));
        assert!(Vec4::from_array(tangents[0]).abs_diff_eq((-Vec3::Z).extend(-1.), 0.00001));
        let mut shear = Affine3A::IDENTITY;
        shear.matrix3.y_axis.x = 0.5;
        assert!(normalize_mesh(&source, shear).is_err());
        source.remove_attribute(Mesh::ATTRIBUTE_NORMAL);
        assert!(normalize_mesh(&source, Affine3A::IDENTITY).is_err());
    }
}
