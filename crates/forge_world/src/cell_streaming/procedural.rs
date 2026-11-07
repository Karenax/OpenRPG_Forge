//! On-demand cells for the existing finite procedural terrain contract.
use super::{CellDetail, CellKey, CellStreamPlan, CellStreamingPolicy, LoadedCell};
use crate::terrain_generation::{SurfaceVertex, grid_uv, surface_vertex};
use crate::{
    TERRAIN_CHUNK_INTERVALS, TERRAIN_CHUNK_VERTICES_PER_AXIS, TERRAIN_CHUNKS_PER_AXIS,
    TERRAIN_SAMPLE_SPACING_M, TerrainChunkCoordinate, TerrainChunkId, TerrainGenerationPlan,
    TerrainLodTopology, TerrainMaterialWeights,
};
use forge_core::{ContentHash, bytes_sha256};
use std::sync::Arc;

#[cfg(test)]
mod tests;

/// One pinned terrain plan and two shared topologies; no complete terrain cache is built.
#[derive(Clone, Debug)]
pub struct ProceduralCellSource {
    terrain: Arc<TerrainGenerationPlan>,
    policy: CellStreamingPolicy,
    residency_source: ContentHash,
    full_topology: TerrainLodTopology,
    far_topology: TerrainLodTopology,
}

/// Owned CPU geometry for one cell. `acknowledgement.bytes` counts the geometry
/// buffers, excluding the shared plan, allocator overhead, renderer and GPU memory.
/// Full-detail vertices and triangles reproduce the source chunk's canonical
/// collision surface; the distant mesh is presentation-only. Backend collision
/// admission and the multi-cell terrain hierarchy are separate responsibilities.
#[derive(Clone, Debug, PartialEq)]
pub struct ProceduralCellMesh {
    pub acknowledgement: LoadedCell,
    /// The original procedural chunk identity, retained independently of `CellKey`.
    pub source_chunk_id: TerrainChunkId,
    pub positions_m: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub uvs: Vec<[f32; 2]>,
    pub material_weights: Vec<TerrainMaterialWeights>,
    pub indices: Vec<u32>,
}

impl ProceduralCellSource {
    /// Requires an exact 8 by 8 partition of the currently supported 512 m plan.
    /// The per-cell estimates must be at least the topology's owned buffer size.
    pub fn new(
        terrain: Arc<TerrainGenerationPlan>,
        policy: CellStreamingPolicy,
        far_stride: u8,
    ) -> Result<Self, String> {
        policy.validate().map_err(|error| error.to_string())?;
        let expected_origin = terrain.origin_xz_m().map(f64::from);
        let expected_size =
            f64::from(TERRAIN_CHUNK_INTERVALS) * f64::from(TERRAIN_SAMPLE_SPACING_M);
        if policy.dimensions != [TERRAIN_CHUNKS_PER_AXIS; 2]
            || policy.origin_xz_m.map(f64::to_bits) != expected_origin.map(f64::to_bits)
            || policy.cell_size_m.to_bits() != expected_size.to_bits()
        {
            return Err("Cell grid must exactly partition the pinned procedural terrain".into());
        }
        if far_stride < 2 {
            return Err("Distant procedural terrain requires a stride of at least two".into());
        }
        let full_topology = TerrainLodTopology::new(1).map_err(|error| error.to_string())?;
        let far_topology =
            TerrainLodTopology::new(far_stride).map_err(|error| error.to_string())?;
        if policy.full_cell_bytes < minimum_buffer_bytes(&full_topology)?
            || policy.hlod_cell_bytes < minimum_buffer_bytes(&far_topology)?
        {
            return Err("Cell byte estimates do not cover the procedural geometry buffers".into());
        }
        let residency_source = bytes_sha256(
            format!(
                "forge-procedural-cell-source-v1:{}:{far_stride}",
                terrain.document_hash()
            )
            .as_bytes(),
        );
        Ok(Self {
            terrain,
            policy,
            residency_source,
            full_topology,
            far_topology,
        })
    }

    #[must_use]
    pub const fn terrain(&self) -> &Arc<TerrainGenerationPlan> {
        &self.terrain
    }

    /// Pin for `CellStreamer::new`: changes when either the document or the
    /// distant topology changes. Use a fresh controller when this pin changes.
    #[must_use]
    pub const fn residency_source(&self) -> ContentHash {
        self.residency_source
    }

    /// Prepare only the requested acquisitions; refusal leaves residency unchanged.
    pub fn prepare(&self, plan: &CellStreamPlan) -> Result<Vec<ProceduralCellMesh>, String> {
        if plan.source != self.residency_source || plan.policy != self.policy {
            return Err("Cell plan differs from the pinned procedural source or policy".into());
        }
        let mut resident_bytes = plan.retained_bytes;
        let mut peak_bytes = plan.resident_before_bytes;
        let mut meshes = Vec::with_capacity(plan.acquire.len());
        for (&key, &detail) in &plan.acquire {
            let mesh = self.build(key, detail)?;
            resident_bytes = resident_bytes
                .checked_add(mesh.acknowledgement.bytes)
                .ok_or("Procedural cell resident byte count overflow")?;
            peak_bytes = peak_bytes
                .checked_add(mesh.acknowledgement.bytes)
                .ok_or("Procedural cell staging byte count overflow")?;
            if resident_bytes > self.policy.max_resident_bytes
                || peak_bytes > self.policy.max_staging_bytes
            {
                return Err(
                    "Procedural cell geometry exceeds the resident or staging budget".into(),
                );
            }
            meshes.push(mesh);
        }
        Ok(meshes)
    }

    /// Build one bounded cell directly from selected canonical grid samples.
    pub fn build(&self, key: CellKey, detail: CellDetail) -> Result<ProceduralCellMesh, String> {
        if key.x >= self.policy.dimensions[0] || key.z >= self.policy.dimensions[1] {
            return Err("Cell coordinate is outside the pinned procedural grid".into());
        }
        let topology = match detail {
            CellDetail::Full => &self.full_topology,
            CellDetail::Hlod => &self.far_topology,
        };
        let count = topology.source_vertex_indices().len();
        let coordinate = TerrainChunkCoordinate { x: key.x, z: key.z };
        let mut mesh = ProceduralCellMesh {
            acknowledgement: LoadedCell {
                key,
                detail,
                source: self.residency_source,
                bytes: 0,
            },
            source_chunk_id: self.terrain.chunk_id(coordinate),
            positions_m: Vec::with_capacity(count),
            normals: Vec::with_capacity(count),
            uvs: Vec::with_capacity(count),
            material_weights: Vec::with_capacity(count),
            indices: topology.triangle_indices().to_vec(),
        };
        for &index in topology.source_vertex_indices() {
            let x = key.x * TERRAIN_CHUNK_INTERVALS + index % TERRAIN_CHUNK_VERTICES_PER_AXIS;
            let z = key.z * TERRAIN_CHUNK_INTERVALS + index / TERRAIN_CHUNK_VERTICES_PER_AXIS;
            let vertex: SurfaceVertex = surface_vertex(self.terrain.document(), x, z);
            mesh.positions_m.push(vertex.position_m);
            mesh.normals.push(vertex.shading_normal);
            mesh.uvs.push(grid_uv(x, z));
            mesh.material_weights.push(vertex.material_weights);
        }
        mesh.acknowledgement.bytes = buffer_bytes(&mesh)?;
        if mesh.acknowledgement.bytes > self.policy.estimate(detail) {
            return Err("Procedural cell geometry exceeds its admitted byte estimate".into());
        }
        Ok(mesh)
    }
}

fn minimum_buffer_bytes(topology: &TerrainLodTopology) -> Result<u64, String> {
    let vertex_size =
        size_of::<[f32; 3]>() * 2 + size_of::<[f32; 2]>() + size_of::<TerrainMaterialWeights>();
    let bytes = topology
        .source_vertex_indices()
        .len()
        .checked_mul(vertex_size)
        .and_then(|n| {
            topology
                .triangle_indices()
                .len()
                .checked_mul(size_of::<u32>())
                .and_then(|indices| n.checked_add(indices))
        })
        .ok_or("Procedural cell buffer size overflow")?;
    u64::try_from(bytes).map_err(|error| error.to_string())
}

fn buffer_bytes(mesh: &ProceduralCellMesh) -> Result<u64, String> {
    let buffers = [
        (mesh.positions_m.capacity(), size_of::<[f32; 3]>()),
        (mesh.normals.capacity(), size_of::<[f32; 3]>()),
        (mesh.uvs.capacity(), size_of::<[f32; 2]>()),
        (
            mesh.material_weights.capacity(),
            size_of::<TerrainMaterialWeights>(),
        ),
        (mesh.indices.capacity(), size_of::<u32>()),
    ];
    let bytes = buffers
        .into_iter()
        .try_fold(0_usize, |sum, (count, size)| {
            count
                .checked_mul(size)
                .and_then(|value| sum.checked_add(value))
        })
        .ok_or("Procedural cell buffer size overflow")?;
    u64::try_from(bytes).map_err(|error| error.to_string())
}
