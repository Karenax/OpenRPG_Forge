//! Cell payloads from the existing canonical sampled terrain; shared LOD seam topology.
use super::{CellDetail, CellKey, CellStreamPlan, CellStreamingPolicy, LoadedCell};
use crate::{TerrainLodTopology, sampled_terrain::SampledTerrainPlan};
use std::sync::Arc;

const INTERVALS: u16 = 32;

/// Renderer-only topology. Full cells and their collision surface always use stride one.
/// Construction validates both distant sampling rates without changing the authored raster.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SampledMeshQuality {
    coarse_stride: u8,
    distant_stride: u8,
}

impl Default for SampledMeshQuality {
    fn default() -> Self {
        Self {
            coarse_stride: 8,
            distant_stride: 8,
        }
    }
}

impl SampledMeshQuality {
    pub fn new(coarse_stride: u8, distant_stride: u8) -> Result<Self, String> {
        if coarse_stride < 2 || distant_stride < 2 {
            return Err(
                "Distant sampled terrain requires strides between two and thirty-two".into(),
            );
        }
        TerrainLodTopology::new(coarse_stride).map_err(|e| e.to_string())?;
        TerrainLodTopology::new(distant_stride).map_err(|e| e.to_string())?;
        Ok(Self {
            coarse_stride,
            distant_stride,
        })
    }
    #[must_use]
    pub const fn coarse_stride(self) -> u8 {
        self.coarse_stride
    }
    #[must_use]
    pub const fn distant_stride(self) -> u8 {
        self.distant_stride
    }
}

/// Immutable raster is retained once; generated cell meshes are created only on request.
#[derive(Clone)]
pub struct SampledCellSource {
    terrain: Arc<SampledTerrainPlan>,
    policy: CellStreamingPolicy,
    full_topology: TerrainLodTopology,
    far_topology: TerrainLodTopology,
    quality: SampledMeshQuality,
}

/// CPU geometry payload. `bytes` counts owned buffers, not total process/GPU memory.
#[derive(Clone, Debug)]
pub struct SampledCellMesh {
    pub acknowledgement: LoadedCell,
    pub positions_m: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    /// Canonical grass/earth/rock weights in RGB; alpha is one, not another layer.
    pub material_weights: Vec<[f32; 4]>,
    pub indices: Vec<u32>,
}

impl SampledCellSource {
    pub fn new(
        terrain: Arc<SampledTerrainPlan>,
        policy: CellStreamingPolicy,
        far_stride: u8,
    ) -> Result<Self, String> {
        Self::with_mesh_quality(terrain, policy, SampledMeshQuality::new(far_stride, 8)?)
    }

    pub fn with_mesh_quality(
        terrain: Arc<SampledTerrainPlan>,
        policy: CellStreamingPolicy,
        quality: SampledMeshQuality,
    ) -> Result<Self, String> {
        policy.validate().map_err(|error| error.to_string())?;
        let source = terrain.raster().source();
        if source.width != u32::from(policy.dimensions[0]) * u32::from(INTERVALS)
            || source.height != u32::from(policy.dimensions[1]) * u32::from(INTERVALS)
            || source.origin_xz_m.map(f64::to_bits) != policy.origin_xz_m.map(f64::to_bits)
            || (source.sample_spacing_m * f64::from(INTERVALS)).to_bits()
                != policy.cell_size_m.to_bits()
        {
            return Err(
                "Cell grid must exactly partition the pinned raster into 32-interval cells".into(),
            );
        }
        Ok(Self {
            terrain,
            policy,
            full_topology: TerrainLodTopology::new(1).map_err(|e| e.to_string())?,
            far_topology: TerrainLodTopology::new(quality.coarse_stride)
                .map_err(|e| e.to_string())?,
            quality,
        })
    }

    #[must_use]
    pub const fn terrain(&self) -> &Arc<SampledTerrainPlan> {
        &self.terrain
    }

    #[must_use]
    pub const fn mesh_quality(&self) -> SampledMeshQuality {
        self.quality
    }

    /// Read-only preparation is suitable for a bounded background task.
    pub fn prepare(&self, plan: &CellStreamPlan) -> Result<Vec<SampledCellMesh>, String> {
        if plan.source() != self.terrain.document_hash() {
            return Err("Cell plan source differs from the pinned sampled terrain".into());
        }
        plan.acquisitions()
            .iter()
            .map(|(&key, &detail)| self.build(key, detail))
            .collect()
    }

    pub fn build(&self, key: CellKey, detail: CellDetail) -> Result<SampledCellMesh, String> {
        if key.x >= self.policy.dimensions[0] || key.z >= self.policy.dimensions[1] {
            return Err("Cell coordinate is outside the pinned raster grid".into());
        }
        let patch = self
            .terrain
            .build_patch([key.x * INTERVALS, key.z * INTERVALS], [INTERVALS; 2], 1)
            .map_err(|e| e.to_string())?;
        let topology = match detail {
            CellDetail::Full => &self.full_topology,
            CellDetail::Hlod => &self.far_topology,
        };
        let positions_m: Vec<_> = topology
            .source_vertex_indices()
            .iter()
            .map(|&i| patch.positions_m[usize::from(i)])
            .collect();
        let normals: Vec<_> = topology
            .source_vertex_indices()
            .iter()
            .map(|&i| patch.normals[usize::from(i)])
            .collect();
        let indices = topology.triangle_indices().to_vec();
        let material_weights: Vec<_> = topology
            .source_vertex_indices()
            .iter()
            .enumerate()
            .map(|(output, &i)| {
                let sample_x = key.x * INTERVALS + i % (INTERVALS + 1);
                let sample_z = key.z * INTERVALS + i / (INTERVALS + 1);
                let weights = crate::terrain_generation::material_weights_for(
                    self.terrain.document(),
                    positions_m[output][1],
                    normals[output][1],
                    sample_x,
                    sample_z,
                );
                [weights.grass, weights.earth, weights.rock, 1.0]
            })
            .collect();
        // The source is <=4096²; selected topology is <=33² and indices <=6144.
        let bytes = (positions_m.capacity() * size_of::<[f32; 3]>()
            + normals.capacity() * size_of::<[f32; 3]>()
            + material_weights.capacity() * size_of::<[f32; 4]>()
            + indices.capacity() * size_of::<u32>()) as u64;
        Ok(SampledCellMesh {
            acknowledgement: LoadedCell {
                key,
                detail,
                source: self.terrain.document_hash(),
                bytes,
            },
            positions_m,
            normals,
            material_weights,
            indices,
        })
    }
}
