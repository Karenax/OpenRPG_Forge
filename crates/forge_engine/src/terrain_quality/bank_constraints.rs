//! Conservative, render-only full-resolution constraints under canonical bank triangles.
use super::{Assets, BTreeSet, Mesh, TerrainChunkId, TerrainChunkMesh, Transform, Vec3};
use bevy::asset::AssetId;

pub(super) fn affected_chunks(
    chunks: &[TerrainChunkMesh],
    bindings: &[(AssetId<Mesh>, Transform)],
    meshes: &Assets<Mesh>,
) -> Result<BTreeSet<TerrainChunkId>, &'static str> {
    let mut protected = BTreeSet::new();
    for (handle, transform) in bindings {
        let mesh = meshes.get(*handle).ok_or("missing bank mesh")?;
        let positions = mesh
            .attribute(Mesh::ATTRIBUTE_POSITION)
            .and_then(|p| p.as_float3())
            .ok_or("missing bank positions")?;
        let indices = mesh.indices().ok_or("missing bank indices")?;
        if indices.len() > 1_000_000 || !indices.len().is_multiple_of(3) {
            return Err("invalid bank triangle budget");
        }
        let indices = indices.iter().collect::<Vec<_>>();
        for triangle in indices.as_chunks::<3>().0 {
            let mut low = Vec3::splat(f32::INFINITY);
            let mut high = Vec3::splat(f32::NEG_INFINITY);
            for index in triangle {
                let point = transform.transform_point(Vec3::from_array(
                    *positions.get(*index).ok_or("bank index out of bounds")?,
                ));
                if !point.is_finite() {
                    return Err("non-finite bank position");
                }
                low = low.min(point);
                high = high.max(point);
            }
            for chunk in chunks {
                if low.x <= chunk.maximum_xz_m[0]
                    && high.x >= chunk.minimum_xz_m[0]
                    && low.z <= chunk.maximum_xz_m[1]
                    && high.z >= chunk.minimum_xz_m[1]
                {
                    protected.insert(chunk.id);
                }
            }
        }
    }
    Ok(protected)
}
