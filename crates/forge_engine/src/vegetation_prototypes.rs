//! Resolve flat, baked governed GLBs without spawning world instances or colliders.
use bevy::{
    gltf::{GltfMaterial, GltfMesh, GltfNode},
    math::Affine3A,
    mesh::VertexAttributeValues,
    prelude::*,
};
use forge_assets::VegetationPrototypeBinding;
use std::{error::Error, fmt};
mod authored;
mod promotion;
pub use promotion::{TreePromotionSelection, prepare_tree_promotion};
mod loading;
pub use authored::{PromotedTreeAsset, resolve_authored_species, resolve_promoted_tree_asset};
mod normalized;
mod wind;
pub use loading::{
    VegetationPrototypeCache, VegetationPrototypeLoadPlugin, VegetationPrototypeLoadStatus,
    VegetationPrototypeLoaderPlugin,
};
pub use normalized::{RenderVegetationPrimitive, RenderVegetationPrototype};
pub use wind::{VegetationPrototypeWindCache, WindVegetationPrimitive};

pub(crate) fn prototype_key(version: u32, binding: &VegetationPrototypeBinding) -> String {
    if version == 1 {
        binding.layer_id.clone()
    } else {
        // Length-prefixed layer prevents collisions, even when IDs contain delimiters.
        format!(
            "{}:{}{}",
            binding.layer_id.len(),
            binding.layer_id,
            binding.asset_id
        )
    }
}

#[derive(Clone, Debug)]
pub struct ResolvedVegetationPrimitive {
    pub mesh: Handle<Mesh>,
    pub material: Handle<GltfMaterial>,
    /// Prototype normalization followed by the original node transform.
    /// Apply the unchanged scatter pose outside this transform.
    pub local_transform: Affine3A,
}

#[derive(Clone, Debug)]
pub struct ResolvedVegetationPrototype {
    pub target_height_m: f32,
    pub lods: [Vec<ResolvedVegetationPrimitive>; 3],
    pub source_min: Vec3,
    pub source_max: Vec3,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum VegetationPrototypeResolveError {
    PendingDependency,
    InvalidBinding,
    UnsupportedNode(String),
    InvalidMetadata(String),
    InvalidMesh(String),
    MissingLod,
    DimensionMismatch,
}

impl fmt::Display for VegetationPrototypeResolveError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "vegetation prototype resolution: {self:?}")
    }
}
impl Error for VegetationPrototypeResolveError {}

#[cfg(test)]
mod tests;

/// Call after catalog/hash verification and GLB dependency loading. A pending dependency
/// is distinct from invalid content; the caller must also observe `AssetServer` load failures.
/// Only flat, baked nodes are accepted in this first adapter, matching the DCC worker.
/// No meshes/materials are copied and no ECS entities are created here.
pub fn resolve_vegetation_prototype(
    binding: &VegetationPrototypeBinding,
    node_handles: &[Handle<GltfNode>],
    nodes: &Assets<GltfNode>,
    gltf_meshes: &Assets<GltfMesh>,
    meshes: &Assets<Mesh>,
    materials: &Assets<GltfMaterial>,
) -> Result<ResolvedVegetationPrototype, VegetationPrototypeResolveError> {
    use VegetationPrototypeResolveError as Failure;
    let scale = binding.uniform_scale();
    let pivot = Vec3::from_array(binding.source_pivot_m);
    if !pivot.is_finite()
        || !scale.is_finite()
        || scale <= 0.0
        || !binding.source_height_m.is_finite()
        || binding.source_height_m <= 0.0
    {
        return Err(Failure::InvalidBinding);
    }
    let normalization = Affine3A::from_scale_rotation_translation(
        Vec3::splat(scale),
        Quat::IDENTITY,
        -pivot * scale,
    );
    let mut output = ResolvedVegetationPrototype {
        target_height_m: binding.target_height_m,
        lods: std::array::from_fn(|_| Vec::new()),
        source_min: Vec3::splat(f32::INFINITY),
        source_max: Vec3::splat(f32::NEG_INFINITY),
    };
    for handle in node_handles {
        let node = nodes.get(handle).ok_or(Failure::PendingDependency)?;
        if !node.children.is_empty() || node.skin.is_some() || node.is_animation_root {
            return Err(Failure::UnsupportedNode(node.name.clone()));
        }
        let Some(mesh_handle) = &node.mesh else {
            continue;
        };
        let metadata: serde_json::Value = node
            .extras
            .as_ref()
            .and_then(|extras| serde_json::from_str(&extras.value).ok())
            .ok_or_else(|| Failure::InvalidMetadata(node.name.clone()))?;
        if metadata["forge_asset_id"].as_str() != Some(binding.asset_id.as_str()) {
            return Err(Failure::InvalidMetadata(node.name.clone()));
        }
        if metadata["forge_role"].as_str() == Some("collision") {
            continue;
        }
        let lod = metadata["forge_lod"]
            .as_u64()
            .filter(|lod| *lod <= 2)
            .and_then(|lod| usize::try_from(lod).ok())
            .filter(|_| metadata["forge_role"].as_str() == Some("render"))
            .ok_or_else(|| Failure::InvalidMetadata(node.name.clone()))?;
        let transform = node.transform.compute_affine();
        if !transform.is_finite() || transform.matrix3.determinant() <= 0.0 {
            return Err(Failure::UnsupportedNode(node.name.clone()));
        }
        let gltf_mesh = gltf_meshes
            .get(mesh_handle)
            .ok_or(Failure::PendingDependency)?;
        for primitive in &gltf_mesh.primitives {
            let mesh = meshes
                .get(&primitive.mesh)
                .ok_or(Failure::PendingDependency)?;
            let material = primitive
                .material
                .as_ref()
                .ok_or_else(|| Failure::InvalidMesh(node.name.clone()))?;
            materials.get(material).ok_or(Failure::PendingDependency)?;
            let Some(VertexAttributeValues::Float32x3(positions)) =
                mesh.attribute(Mesh::ATTRIBUTE_POSITION)
            else {
                return Err(Failure::InvalidMesh(node.name.clone()));
            };
            if positions.is_empty() || !mesh.contains_attribute(Mesh::ATTRIBUTE_UV_0) {
                return Err(Failure::InvalidMesh(node.name.clone()));
            }
            for position in positions {
                let point = transform.transform_point3(Vec3::from_array(*position));
                if !point.is_finite() {
                    return Err(Failure::InvalidMesh(node.name.clone()));
                }
                if lod == 0 {
                    output.source_min = output.source_min.min(point);
                    output.source_max = output.source_max.max(point);
                }
            }
            output.lods[lod].push(ResolvedVegetationPrimitive {
                mesh: primitive.mesh.clone(),
                material: material.clone(),
                local_transform: normalization * transform,
            });
        }
    }
    if output.lods.iter().any(Vec::is_empty) {
        return Err(Failure::MissingLod);
    }
    let tolerance = (binding.source_height_m * 0.01).max(0.02);
    if ((output.source_max.y - output.source_min.y) - binding.source_height_m).abs() > tolerance
        || (output.source_min.y - pivot.y).abs() > tolerance
    {
        return Err(Failure::DimensionMismatch);
    }
    Ok(output)
}
