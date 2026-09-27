//! One bounds scan per shared mesh within a single immutable preparation.
use bevy::{
    asset::AssetId,
    camera::primitives::{Aabb, MeshAabb},
    prelude::{Assets, Mesh},
};
use std::collections::HashMap;

pub(super) struct ProjectionBounds<'a> {
    meshes: &'a Assets<Mesh>,
    computed: HashMap<AssetId<Mesh>, Aabb>,
}

impl<'a> ProjectionBounds<'a> {
    pub(super) fn new(meshes: &'a Assets<Mesh>) -> Self {
        Self {
            meshes,
            computed: HashMap::new(),
        }
    }

    pub(super) fn get(&mut self, id: AssetId<Mesh>) -> Result<Aabb, &'static str> {
        // The immutable asset borrow prevents edits during this cache's lifetime.
        // A new preparation creates a new cache, so in-place edits are not hidden.
        if let Some(bounds) = self.computed.get(&id) {
            return Ok(*bounds);
        }
        let bounds = self
            .meshes
            .get(id)
            .and_then(MeshAabb::compute_aabb)
            .ok_or("governed mesh has no bounds")?;
        self.computed.insert(id, bounds);
        Ok(bounds)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{asset::RenderAssetUsages, mesh::PrimitiveTopology, prelude::Cuboid};

    #[test]
    fn shared_mesh_is_scanned_once_and_bounds_are_exact() {
        let mut meshes = Assets::<Mesh>::default();
        let handle = meshes.add(Mesh::from(Cuboid::new(2.0, 8.0, 4.0)));
        let expected = meshes.get(&handle).unwrap().compute_aabb().unwrap();
        let mut bounds = ProjectionBounds::new(&meshes);
        for _ in 0..16_620 {
            let actual = bounds.get(handle.id()).unwrap();
            assert_eq!(actual.min(), expected.min());
            assert_eq!(actual.max(), expected.max());
        }
        assert_eq!(bounds.computed.len(), 1);
    }

    #[test]
    fn next_preparation_observes_edits_and_missing_geometry() {
        let mut meshes = Assets::<Mesh>::default();
        let handle = meshes.add(Mesh::from(Cuboid::new(2.0, 8.0, 4.0)));
        let old = ProjectionBounds::new(&meshes).get(handle.id()).unwrap();
        *meshes.get_mut(&handle).unwrap() = Mesh::from(Cuboid::new(4.0, 16.0, 8.0));
        let changed = ProjectionBounds::new(&meshes).get(handle.id()).unwrap();
        assert_eq!(changed.min(), old.min() * 2.0);
        assert_eq!(changed.max(), old.max() * 2.0);
        *meshes.get_mut(&handle).unwrap() = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        );
        assert!(ProjectionBounds::new(&meshes).get(handle.id()).is_err());
        meshes.remove(handle.id());
        assert!(ProjectionBounds::new(&meshes).get(handle.id()).is_err());
    }
}
