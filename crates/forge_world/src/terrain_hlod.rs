//! Derived, balanced terrain hierarchy outside the resident simulation/view cells.
//! No persistent terrain, collision surface or renderer-specific state is introduced.
use crate::cell_streaming::{CellDetail, CellKey, CellStreamingPolicy};
use forge_core::{AreaId, EntityId};
use std::collections::{BTreeMap, BTreeSet};

mod mesh;
pub use mesh::{TerrainHlodMesh, build_mesh, build_mesh_with_stride};
mod procedural;
pub use procedural::{
    ProceduralTerrainHlodMesh, build_procedural_mesh, build_procedural_mesh_with_stride,
};
#[cfg(test)]
mod tests;

/// Hard preparation bound, independent of the size of the immutable raster.
pub const MAX_TERRAIN_HLOD_TILES: usize = 1024;

/// Coordinates of the first base cell; a level L tile covers 2^L cells per axis.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub struct TerrainHlodKey {
    pub x: u16,
    pub z: u16,
    pub level: u8,
}

impl TerrainHlodKey {
    #[must_use]
    pub const fn side_cells(self) -> u16 {
        // Invalid externally constructed keys are rejected by the mesh builder.
        if self.level < 16 {
            1_u16 << self.level
        } else {
            0
        }
    }

    #[must_use]
    pub fn entity_id(self, area: AreaId) -> EntityId {
        EntityId::deterministic(format!(
            "terrain-hlod:{area}:{}:{}:{}",
            self.level, self.x, self.z
        ))
    }
}

/// Fine-edge flags are west/east/north/south. Each flagged edge must retain the
/// half-step samples of its two finer neighbours; no skirts hide geometric gaps.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct TerrainHlodTile {
    pub key: TerrainHlodKey,
    pub finer_edges: [bool; 4],
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct TerrainHlodLayout {
    dimensions: [u16; 2],
    tiles: BTreeMap<TerrainHlodKey, TerrainHlodTile>,
}

impl TerrainHlodLayout {
    /// Exact complement of the fine resident cells, including distant protected
    /// simulation islands. Every map cell has one owner; adjacent levels differ
    /// by at most one. Rectangular/non-power-of-two grids are supported explicitly.
    pub fn new(
        policy: &CellStreamingPolicy,
        resident: &BTreeMap<CellKey, CellDetail>,
    ) -> Result<Self, String> {
        policy.validate().map_err(|error| error.to_string())?;
        if resident.len() > usize::from(policy.max_resident_cells)
            || resident
                .keys()
                .any(|key| key.x >= policy.dimensions[0] || key.z >= policy.dimensions[1])
        {
            return Err("Terrain HLOD fine coverage is outside the admitted grid/budget".into());
        }
        let fine: BTreeSet<_> = resident.keys().copied().collect();
        let side = policy.dimensions[0]
            .max(policy.dimensions[1])
            .next_power_of_two();
        let root = TerrainHlodKey {
            x: 0,
            z: 0,
            level: u8::try_from(side.ilog2()).map_err(|e| e.to_string())?,
        };
        let mut leaves = BTreeSet::new();
        partition(root, policy.dimensions, &fine, &mut leaves)?;
        loop {
            let owners = owners(policy.dimensions, &leaves)?;
            let mut split = BTreeSet::new();
            for &key in &leaves {
                visit_neighbours(key, policy.dimensions, &owners, |_, other| {
                    if key.level > other.level + 1 {
                        split.insert(key);
                    }
                });
            }
            if split.is_empty() {
                let mut tiles = BTreeMap::new();
                for key in leaves {
                    if key.level == 0 && fine.contains(&CellKey { x: key.x, z: key.z }) {
                        continue;
                    }
                    let mut finer_edges = [false; 4];
                    visit_neighbours(key, policy.dimensions, &owners, |edge, other| {
                        finer_edges[edge] |= other.level < key.level;
                    });
                    tiles.insert(key, TerrainHlodTile { key, finer_edges });
                }
                if tiles.len() > MAX_TERRAIN_HLOD_TILES {
                    return Err("Terrain HLOD tile budget exceeded".into());
                }
                return Ok(Self {
                    dimensions: policy.dimensions,
                    tiles,
                });
            }
            for key in split {
                leaves.remove(&key);
                leaves.extend(children(key));
            }
            check_count(leaves.len(), fine.len())?;
        }
    }

    #[must_use]
    pub const fn dimensions(&self) -> [u16; 2] {
        self.dimensions
    }

    #[must_use]
    pub const fn tiles(&self) -> &BTreeMap<TerrainHlodKey, TerrainHlodTile> {
        &self.tiles
    }

    #[must_use]
    pub fn covered_cells(&self) -> usize {
        self.tiles
            .keys()
            .map(|key| usize::from(key.side_cells()).pow(2))
            .sum()
    }
}

fn children(key: TerrainHlodKey) -> [TerrainHlodKey; 4] {
    let level = key.level - 1;
    let half = key.side_cells() / 2;
    [[0, 0], [half, 0], [0, half], [half, half]].map(|[x, z]| TerrainHlodKey {
        x: key.x + x,
        z: key.z + z,
        level,
    })
}

fn check_count(count: usize, fine: usize) -> Result<(), String> {
    if count > MAX_TERRAIN_HLOD_TILES + fine {
        Err("Terrain HLOD tile budget exceeded".into())
    } else {
        Ok(())
    }
}

fn partition(
    key: TerrainHlodKey,
    dimensions: [u16; 2],
    fine: &BTreeSet<CellKey>,
    leaves: &mut BTreeSet<TerrainHlodKey>,
) -> Result<(), String> {
    if key.x >= dimensions[0] || key.z >= dimensions[1] {
        return Ok(());
    }
    let side = key.side_cells();
    let contains_fine = fine.iter().any(|cell| {
        (key.x..key.x + side).contains(&cell.x) && (key.z..key.z + side).contains(&cell.z)
    });
    if key.level == 0
        || (!contains_fine && key.x + side <= dimensions[0] && key.z + side <= dimensions[1])
    {
        leaves.insert(key);
        return check_count(leaves.len(), fine.len());
    }
    for child in children(key) {
        partition(child, dimensions, fine, leaves)?;
    }
    Ok(())
}

fn owners(
    dimensions: [u16; 2],
    leaves: &BTreeSet<TerrainHlodKey>,
) -> Result<Vec<TerrainHlodKey>, String> {
    let mut result = vec![None; usize::from(dimensions[0]) * usize::from(dimensions[1])];
    for &key in leaves {
        for z in key.z..key.z + key.side_cells() {
            for x in key.x..key.x + key.side_cells() {
                let slot =
                    &mut result[usize::from(z) * usize::from(dimensions[0]) + usize::from(x)];
                if slot.replace(key).is_some() {
                    return Err("Terrain HLOD coverage overlaps".into());
                }
            }
        }
    }
    result
        .into_iter()
        .map(|key| key.ok_or_else(|| "Terrain HLOD coverage has a hole".into()))
        .collect()
}

fn visit_neighbours(
    key: TerrainHlodKey,
    dimensions: [u16; 2],
    owners: &[TerrainHlodKey],
    mut visit: impl FnMut(usize, TerrainHlodKey),
) {
    let side = key.side_cells();
    let at = |x, z| owners[usize::from(z) * usize::from(dimensions[0]) + usize::from(x)];
    for offset in 0..side {
        if key.x > 0 {
            visit(0, at(key.x - 1, key.z + offset));
        }
        if key.x + side < dimensions[0] {
            visit(1, at(key.x + side, key.z + offset));
        }
        if key.z > 0 {
            visit(2, at(key.x + offset, key.z - 1));
        }
        if key.z + side < dimensions[1] {
            visit(3, at(key.x + offset, key.z + side));
        }
    }
}
