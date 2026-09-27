//! Derived read-only population, resolved from the canonical imported source.
//! Storage adapters supply bounded bytes; no procedural fallback or filesystem dependency.
//! This is the immutable base population. Authoring exclusions, promotions and
//! density overrides must be applied by the subsequent editor projection.
use forge_core::{ContentHash, EntityId};
use forge_project::{VegetationDocument, imported_vegetation::ImportedVegetationInstance};
use std::collections::BTreeMap;

mod exclusions;
pub use exclusions::{ImportedExclusionView, ImportedVisibleLayer};
mod density;
pub use density::ImportedAuthoredView;
pub mod acquisition;
mod terrain;
pub use terrain::{ImportedTerrainLayer, ImportedTerrainView};

#[derive(Debug)]
pub struct ImportedPopulation {
    document: VegetationDocument,
    layers: Vec<ImportedLayer>,
}

#[derive(Debug)]
pub struct ImportedLayer {
    pub layer_id: String,
    pub prototype_key: String,
    pub parameter_names: Vec<String>,
    instances: Vec<ImportedEntity>,
    chunks: BTreeMap<[u16; 2], Vec<usize>>,
}

#[derive(Clone, Debug, PartialEq)]
pub struct ImportedEntity {
    pub id: EntityId,
    pub source: ImportedVegetationInstance,
}

impl ImportedPopulation {
    /// Resolve all layers atomically. A missing, corrupt or oversized block fails
    /// the entire result; the caller can keep its previous population unchanged.
    pub fn resolve(
        document: &VegetationDocument,
        mut load: impl FnMut(ContentHash, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        document.validate().map_err(|e| e.to_string())?;
        let source = document
            .imported_source
            .as_ref()
            .ok_or("Imported source required")?;
        let mut layers = Vec::with_capacity(source.blocks.len());
        for block in &source.blocks {
            let bytes = load(block.instances_sha256, block.byte_len()?)
                .map_err(|e| format!("Imported layer {}: {e}", block.layer_id))?;
            let records = block.decode(&bytes, document.extent_m)?;
            let mut chunks: BTreeMap<[u16; 2], Vec<usize>> = BTreeMap::new();
            let mut instances = Vec::with_capacity(records.len());
            for record in records {
                let chunk = [
                    chunk_axis(record.position_m[0], document.extent_m.size_x_m),
                    chunk_axis(record.position_m[2], document.extent_m.size_z_m),
                ];
                chunks.entry(chunk).or_default().push(instances.len());
                instances.push(ImportedEntity {
                    id: block.instance_id(document.project_id, document.area_id, record.source_key),
                    source: record,
                });
            }
            layers.push(ImportedLayer {
                layer_id: block.layer_id.clone(),
                prototype_key: block.prototype_key.clone(),
                parameter_names: block.parameter_names.clone(),
                instances,
                chunks,
            });
        }
        Ok(Self {
            document: document.clone(),
            layers,
        })
    }

    #[must_use]
    pub const fn document(&self) -> &VegetationDocument {
        &self.document
    }

    #[must_use]
    pub fn layers(&self) -> &[ImportedLayer] {
        &self.layers
    }

    #[must_use]
    pub fn instance_count(&self) -> usize {
        self.layers.iter().map(|l| l.instances.len()).sum()
    }
}

impl ImportedLayer {
    #[must_use]
    pub fn instances(&self) -> &[ImportedEntity] {
        &self.instances
    }

    /// Each instance belongs to exactly one 64 m chunk. The positive area edge
    /// belongs to the last chunk, not an extra chunk outside the document.
    pub fn chunk(&self, coordinate: [u16; 2]) -> impl Iterator<Item = &ImportedEntity> {
        self.chunks
            .get(&coordinate)
            .into_iter()
            .flatten()
            .map(|index| &self.instances[*index])
    }
}

#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
fn chunk_axis(position: f64, extent: f32) -> u16 {
    // Source validation proves finite, nonnegative translated coordinates and
    // an extent divisible by 64, at most 4096 m (64 chunks per axis).
    let extent = f64::from(extent);
    (((position + extent * 0.5) / 64.0).floor() as u16).min((extent / 64.0) as u16 - 1)
}
