//! Bounded wire preview of an accepted dry-run. Never a second source document.
use crate::TerrainGenerationPlan;
use forge_core::ContentHash;
use forge_project::{TerrainDocument, terrain_edit::TerrainEdit};
use std::collections::{BTreeMap, BTreeSet};

#[derive(Debug)]
pub struct TerrainBrushPreview {
    pub source_hash: ContentHash,
    pub changed_samples: usize,
    pub displayed_samples: usize,
    /// Triples: original height, proposed height, adjacent proposed point.
    pub lines: Vec<[[f32; 3]; 3]>,
    /// Position and proposed logical grass/earth/rock mixture, not final shading.
    pub material_marks: Vec<([f32; 3], [f32; 3])>,
}

impl TerrainBrushPreview {
    pub fn prepare(source: &TerrainDocument, edit: &TerrainEdit) -> Result<Option<Self>, String> {
        if let TerrainEdit::SetMaterialOverrides { samples } = edit {
            let mut target = source.clone();
            target.material_overrides.clone_from(samples);
            let proposed =
                TerrainGenerationPlan::from_document(&target).map_err(|e| e.to_string())?;
            let old: BTreeMap<_, _> = source
                .material_overrides
                .iter()
                .map(|s| ((s.sample_z, s.sample_x), &s.weights))
                .collect();
            let new: BTreeMap<_, _> = samples
                .iter()
                .map(|s| ((s.sample_z, s.sample_x), &s.weights))
                .collect();
            let changed: Vec<_> = old
                .keys()
                .chain(new.keys())
                .copied()
                .collect::<BTreeSet<_>>()
                .into_iter()
                .filter(|key| old.get(key) != new.get(key))
                .collect();
            let stride = changed.len().div_ceil(2048).max(1);
            let mut marks = Vec::new();
            for &(z, x) in changed.iter().step_by(stride) {
                let wx = f32::from(x) * 2.0 - 256.0;
                let wz = f32::from(z) * 2.0 - 256.0;
                let surface = proposed.surface_at(wx, wz).map_err(|e| e.to_string())?;
                marks.push((
                    [wx, surface.height_m, wz],
                    surface.material_weights.as_array(),
                ));
            }
            return Ok(Some(Self {
                source_hash: source.content_hash().map_err(|e| e.to_string())?,
                changed_samples: changed.len(),
                displayed_samples: marks.len(),
                lines: Vec::new(),
                material_marks: marks,
            }));
        }
        let TerrainEdit::SetHeightDeltas { samples } = edit else {
            return Ok(None);
        };
        let original = TerrainGenerationPlan::from_document(source).map_err(|e| e.to_string())?;
        let mut target = source.clone();
        target.height_deltas.clone_from(samples);
        let proposed = TerrainGenerationPlan::from_document(&target).map_err(|e| e.to_string())?;
        let old: BTreeMap<_, _> = source
            .height_deltas
            .iter()
            .map(|s| (s.sort_key(), s.delta_m.to_bits()))
            .collect();
        let new: BTreeMap<_, _> = samples
            .iter()
            .map(|s| (s.sort_key(), s.delta_m.to_bits()))
            .collect();
        let changed: Vec<_> = old
            .keys()
            .chain(new.keys())
            .copied()
            .collect::<BTreeSet<_>>()
            .into_iter()
            .filter(|key| old.get(key) != new.get(key))
            .collect();
        let stride = changed.len().div_ceil(2048).max(1);
        let mut lines = Vec::new();
        for &(z, x) in changed.iter().step_by(stride) {
            let wx = f32::from(x) * 2.0 - 256.0;
            let wz = f32::from(z) * 2.0 - 256.0;
            let before = original.height_at(wx, wz).map_err(|e| e.to_string())?;
            let after = proposed.height_at(wx, wz).map_err(|e| e.to_string())?;
            let neighbor_x = if x < 256 { wx + 2.0 } else { wx - 2.0 };
            let neighbor_y = proposed
                .height_at(neighbor_x, wz)
                .map_err(|e| e.to_string())?;
            lines.push([
                [wx, before, wz],
                [wx, after, wz],
                [neighbor_x, neighbor_y, wz],
            ]);
        }
        Ok(Some(Self {
            source_hash: source.content_hash().map_err(|e| e.to_string())?,
            changed_samples: changed.len(),
            displayed_samples: lines.len(),
            lines,
            material_marks: Vec::new(),
        }))
    }
}
