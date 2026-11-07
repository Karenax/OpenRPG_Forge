// SPDX-License-Identifier: MIT
//! Explicit closed water-exclusion volumes. These are not inferred from render materials.
use bevy::prelude::*;
use forge_core::{ContentHash, bytes_sha256};
use std::{collections::HashMap, sync::Arc};

#[derive(Debug)]
pub struct Geometry {
    pub(super) positions: Vec<[f32; 3]>,
    pub(super) indices: Vec<u32>,
    pub(super) min: Vec3,
    pub(super) max: Vec3,
    pub(super) radius: f32,
    boundary_edges: u32,
    verified_source: Option<ContentHash>,
}

impl Geometry {
    /// Validate the supplied proxy, including closed edges after exact position welding.
    /// No convex hull, guessed lid or visual mesh is substituted for authored geometry.
    pub fn new(positions: Vec<[f32; 3]>, indices: Vec<u32>) -> Result<Arc<Self>, String> {
        Self::validated(positions, indices, None)
    }

    /// Preserve an upstream mask proxy verbatim, including its documented open
    /// seams. The governed importer supplies the independently pinned fingerprint
    /// and boundary count. Editing those bytes invalidates this exception.
    /// This never adds closing triangles or relaxes validation of new volumes.
    pub fn from_verified_source_proxy(
        positions: Vec<[f32; 3]>,
        indices: Vec<u32>,
        expected: ContentHash,
        expected_boundary_edges: u32,
    ) -> Result<Arc<Self>, String> {
        if Self::content_hash(&positions, &indices) != expected {
            return Err("Hull proxy differs from its pinned source fingerprint".into());
        }
        Self::validated(
            positions,
            indices,
            Some((expected, expected_boundary_edges)),
        )
    }

    /// Exact f32 LE positions and u32 LE indices, with separate length prefixes.
    #[must_use]
    pub fn content_hash(positions: &[[f32; 3]], indices: &[u32]) -> ContentHash {
        let mut bytes = Vec::with_capacity(16 + positions.len() * 12 + indices.len() * 4);
        bytes.extend_from_slice(&(positions.len() as u64).to_le_bytes());
        bytes.extend_from_slice(&(indices.len() as u64).to_le_bytes());
        bytes.extend(positions.iter().flatten().flat_map(|x| x.to_le_bytes()));
        bytes.extend(indices.iter().flat_map(|x| x.to_le_bytes()));
        bytes_sha256(&bytes)
    }

    #[must_use]
    pub fn boundary_edges(&self) -> u32 {
        self.boundary_edges
    }
    #[must_use]
    pub fn fingerprint(&self) -> ContentHash {
        Self::content_hash(&self.positions, &self.indices)
    }
    #[must_use]
    pub fn verified_source(&self) -> Option<ContentHash> {
        self.verified_source
    }

    fn validated(
        positions: Vec<[f32; 3]>,
        indices: Vec<u32>,
        source: Option<(ContentHash, u32)>,
    ) -> Result<Arc<Self>, String> {
        if positions.len() < 4
            || positions.len() > u32::MAX as usize
            || indices.is_empty()
            || !indices.len().is_multiple_of(3)
            || indices.len() > u32::MAX as usize
            || positions.iter().flatten().any(|x| !x.is_finite())
        {
            return Err("Hull volume requires finite indexed triangle geometry".into());
        }
        let mut welded = HashMap::new();
        let mut vertex_ids = Vec::with_capacity(positions.len());
        for p in &positions {
            let key = p.map(|x| if x == 0.0 { 0 } else { x.to_bits() });
            let next = welded.len();
            vertex_ids.push(*welded.entry(key).or_insert(next));
        }
        let mut edges = HashMap::<(usize, usize), (u32, i32)>::new();
        for triangle in indices.as_chunks::<3>().0 {
            let [a, b, c] = std::array::from_fn(|i| triangle[i] as usize);
            if [a, b, c].into_iter().any(|i| i >= positions.len()) {
                return Err("Hull triangle references a missing vertex".into());
            }
            let p = [a, b, c].map(|i| Vec3::from_array(positions[i]));
            if (p[1] - p[0]).cross(p[2] - p[0]).length_squared() == 0.0 {
                return Err("Hull volume contains a zero-area triangle".into());
            }
            for (a, b) in [(a, b), (b, c), (c, a)] {
                let (a, b) = (vertex_ids[a], vertex_ids[b]);
                let (key, direction) = if a < b { ((a, b), 1) } else { ((b, a), -1) };
                let edge = edges.entry(key).or_default();
                edge.0 += 1;
                edge.1 += direction;
            }
        }
        let boundary_edges =
            u32::try_from(edges.values().filter(|&&(count, _)| count == 1).count())
                .map_err(|_| "Hull boundary edge count exceeds the indexed geometry limit")?;
        let expected_boundary_edges = source.map_or(0, |(_, count)| count);
        if boundary_edges != expected_boundary_edges
            || edges
                .values()
                .any(|&(count, direction)| !matches!((count, direction), (1, -1 | 1) | (2, 0)))
        {
            return Err("Hull volume is open, non-manifold or inconsistently wound".into());
        }
        let min = positions.iter().fold(Vec3::splat(f32::INFINITY), |p, q| {
            p.min(Vec3::from_array(*q))
        });
        let max = positions
            .iter()
            .fold(Vec3::splat(f32::NEG_INFINITY), |p, q| {
                p.max(Vec3::from_array(*q))
            });
        let center = (min + max) * 0.5;
        let radius = positions
            .iter()
            .map(|p| Vec3::from_array(*p).distance_squared(center))
            .fold(0.0_f32, f32::max)
            .sqrt();
        if !radius.is_finite() || radius <= 0.0 {
            return Err("Hull bounds are invalid".into());
        }
        Ok(Arc::new(Self {
            positions,
            indices,
            min,
            max,
            radius,
            boundary_edges,
            verified_source: source.map(|(hash, _)| hash),
        }))
    }

    /// Source uses the local bounding box expanded by exactly 0.05, not a ray test.
    pub fn camera_inside(&self, world_from_local: Mat4, camera: Vec3) -> Result<bool, String> {
        let determinant = world_from_local.determinant();
        if !world_from_local.is_finite()
            || !determinant.is_finite()
            || determinant.abs() <= f32::MIN_POSITIVE
        {
            return Err("Hull transform is singular or non-finite".into());
        }
        let local = world_from_local.inverse().transform_point3(camera);
        if !local.is_finite() {
            return Err("Hull camera position is non-finite".into());
        }
        let min = self.min - Vec3::splat(0.05);
        let max = self.max + Vec3::splat(0.05);
        Ok(local.cmpge(min).all() && local.cmple(max).all())
    }
}

/// An explicit scene contributor; moving/removing its entity moves/removes the proxy.
/// Registration does not attest that every expected scene hull has been supplied.
#[derive(Component, Clone, Debug)]
#[require(Transform)]
pub struct HullVolume(pub Arc<Geometry>);

#[cfg(test)]
mod tests {
    use super::*;
    pub(super) fn tetrahedron() -> (Vec<[f32; 3]>, Vec<u32>) {
        (
            vec![[0., 0., 0.], [1., 0., 0.], [0., 1., 0.], [0., 0., 1.]],
            vec![0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3],
        )
    }
    #[test]
    fn open_wrong_winding_and_invalid_geometry_are_rejected() {
        let (p, i) = tetrahedron();
        assert!(Geometry::new(p.clone(), i.clone()).is_ok());
        assert!(Geometry::new(p.clone(), i[..9].to_vec()).is_err());
        let mut reversed = i.clone();
        reversed.swap(0, 1);
        assert!(Geometry::new(p.clone(), reversed).is_err());
        let mut missing = i;
        missing[0] = 4;
        assert!(Geometry::new(p.clone(), missing).is_err());
        assert!(Geometry::new(p.clone(), vec![0, 0, 1]).is_err());
        let mut nan = p;
        nan[0][0] = f32::NAN;
        assert!(Geometry::new(nan, vec![0, 1, 2]).is_err());
    }
    #[test]
    fn seam_vertices_and_signed_zero_weld_without_changing_source_positions() {
        let (mut p, mut i) = tetrahedron();
        p.push([-0., 0., 0.]);
        i[0] = 4;
        let g = Geometry::new(p, i).unwrap();
        assert_eq!(g.positions.len(), 5);
        assert!(g.positions[4][0].is_sign_negative());
    }
    #[test]
    fn source_box_camera_rule_follows_object_pose_and_margin() {
        let (p, i) = tetrahedron();
        let g = Geometry::new(p, i).unwrap();
        let transform = Mat4::from_translation(Vec3::new(10., 2., -4.));
        // Intentionally inside the BOX, even outside the actual tetrahedron.
        assert!(
            g.camera_inside(transform, Vec3::new(10.9, 2.9, -3.1))
                .unwrap()
        );
        assert!(
            g.camera_inside(Mat4::IDENTITY, Vec3::new(-0.05, 0., 0.))
                .unwrap()
        );
        // A decimal world-space boundary such as 9.95 is not exactly representable
        // after f32 translation by -10. Keep the source's strict margin unchanged.
        assert!(
            g.camera_inside(transform, Vec3::new(9.951, 2., -4.))
                .unwrap()
        );
        assert!(
            !g.camera_inside(transform, Vec3::new(9.949, 2., -4.))
                .unwrap()
        );
        assert!(g.camera_inside(Mat4::ZERO, Vec3::ZERO).is_err());
    }
}
