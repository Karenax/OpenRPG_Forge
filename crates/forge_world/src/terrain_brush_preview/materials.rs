//! Logical palette previews share their change set across procedural and raster terrain.
use super::TerrainBrushPreview;
use crate::sampled_terrain::SampledTerrainPlan;
use forge_project::{TerrainDocument, TerrainMaterialOverride};
use std::collections::{BTreeMap, BTreeSet};

pub(super) fn changed(
    source: &TerrainDocument,
    samples: &[TerrainMaterialOverride],
) -> Vec<(u16, u16)> {
    let old: BTreeMap<_, _> = source
        .material_overrides
        .iter()
        .map(|sample| ((sample.sample_z, sample.sample_x), &sample.weights))
        .collect();
    let new: BTreeMap<_, _> = samples
        .iter()
        .map(|sample| ((sample.sample_z, sample.sample_x), &sample.weights))
        .collect();
    old.keys()
        .chain(new.keys())
        .copied()
        .collect::<BTreeSet<_>>()
        .into_iter()
        .filter(|key| old.get(key) != new.get(key))
        .collect()
}

impl TerrainBrushPreview {
    pub(super) fn sampled_materials(
        source: &SampledTerrainPlan,
        samples: &[TerrainMaterialOverride],
    ) -> Result<Self, String> {
        if source.document().material_layers.len() != 3 {
            return Err("La peinture nécessite les trois couches de la palette du terrain.".into());
        }
        let mut target = source.document().clone();
        samples.clone_into(&mut target.material_overrides);
        let proposed = source.with_document(&target).map_err(|e| e.to_string())?;
        let changed = changed(source.document(), samples);
        let stride = changed.len().div_ceil(2048).max(1);
        let mut marks = Vec::new();
        for &(z, x) in changed.iter().step_by(stride) {
            let vertex = proposed.sample_vertex([x, z]).map_err(|e| e.to_string())?;
            let weights = crate::terrain_generation::material_weights_for(
                proposed.document(),
                vertex.position_m[1],
                vertex.normal[1],
                x,
                z,
            );
            marks.push((vertex.position_m, weights.as_array()));
        }
        Ok(Self {
            source_hash: source.document_hash(),
            changed_samples: changed.len(),
            displayed_samples: marks.len(),
            lines: Vec::new(),
            material_marks: marks,
        })
    }
}
