use super::{SampledTerrainPlan, SampledTerrainPlanError};
use forge_core::ContentHash;

/// Bounded derived tile. Render and collision consumers share these positions
/// and the same CCW-from-above triangle split. No persistent alternate terrain.
#[derive(Clone, Debug, PartialEq)]
pub struct SampledTerrainPatch {
    pub document_hash: ContentHash,
    pub minimum_cell: [u16; 2],
    pub intervals: [u16; 2],
    pub stride: u16,
    pub positions_m: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub indices: Vec<u32>,
}

impl SampledTerrainPlan {
    /// Explicitly requested lattice sampling, never a silent resolution change.
    /// At most 129² vertices per call. Neighbouring tiles sample global positions
    /// and global finite differences, including across correction boundaries.
    #[allow(clippy::cast_possible_truncation)] // Validated finite f32 domain and height bound.
    pub fn build_patch(
        &self,
        minimum_cell: [u16; 2],
        intervals: [u16; 2],
        stride: u16,
    ) -> Result<SampledTerrainPatch, SampledTerrainPlanError> {
        let source = self
            .document()
            .sampled_source
            .as_ref()
            .ok_or(SampledTerrainPlanError::MissingSource)?;
        if stride == 0
            || intervals.iter().any(|n| *n == 0 || *n > 128)
            || (0..2).any(|axis| {
                u32::from(minimum_cell[axis]) + u32::from(intervals[axis]) * u32::from(stride)
                    > [source.width, source.height][axis]
            })
        {
            return Err(SampledTerrainPlanError::PatchBounds);
        }
        let columns = u32::from(intervals[0]) + 1;
        let mut positions_m =
            Vec::with_capacity(columns as usize * (usize::from(intervals[1]) + 1));
        let mut normals = Vec::with_capacity(positions_m.capacity());
        let step = source.sample_spacing_m;
        for row in 0..=intervals[1] {
            for column in 0..=intervals[0] {
                let x = source.origin_xz_m[0]
                    + (f64::from(minimum_cell[0]) + f64::from(column) * f64::from(stride)) * step;
                let z = source.origin_xz_m[1]
                    + (f64::from(minimum_cell[1]) + f64::from(row) * f64::from(stride)) * step;
                let y = self.height_at(x, z)?;
                positions_m.push([x as f32, y as f32, z as f32]);
                let dx =
                    (self.height_at(x + step, z)? - self.height_at(x - step, z)?) / (2.0 * step);
                let dz =
                    (self.height_at(x, z + step)? - self.height_at(x, z - step)?) / (2.0 * step);
                let length = dx.hypot(dz).hypot(1.0);
                normals.push([
                    (-dx / length) as f32,
                    (1.0 / length) as f32,
                    (-dz / length) as f32,
                ]);
            }
        }
        let mut indices =
            Vec::with_capacity(usize::from(intervals[0]) * usize::from(intervals[1]) * 6);
        for row in 0..u32::from(intervals[1]) {
            for column in 0..u32::from(intervals[0]) {
                let a = row * columns + column;
                indices.extend_from_slice(&[
                    a,
                    a + columns,
                    a + 1,
                    a + 1,
                    a + columns,
                    a + columns + 1,
                ]);
            }
        }
        Ok(SampledTerrainPatch {
            document_hash: self.document_hash(),
            minimum_cell,
            intervals,
            stride,
            positions_m,
            normals,
            indices,
        })
    }
}
