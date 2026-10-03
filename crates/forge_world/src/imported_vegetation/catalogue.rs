//! A reusable authoring catalogue derived from immutable imported layers.
use super::ImportedPopulation;
use forge_core::EntityId;
use forge_project::{VegetationLayerId, zone_authoring::imported_plants::ImportedPlantPlacement};

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ImportedPlantCatalogEntry {
    pub layer_id: VegetationLayerId,
    pub display_name: String,
    pub prototype_key: String,
    pub source_count: usize,
}

impl ImportedPopulation {
    /// Enumerate every imported prototype exposed by the zone document.
    #[must_use]
    pub fn plant_catalogue(&self) -> Vec<ImportedPlantCatalogEntry> {
        self.layers
            .iter()
            .filter(|layer| !layer.instances().is_empty())
            .map(|layer| ImportedPlantCatalogEntry {
                layer_id: VegetationLayerId::new(layer.layer_id.clone()),
                display_name: self
                    .document
                    .layers
                    .iter()
                    .find(|definition| definition.id.as_str() == layer.layer_id)
                    .map_or_else(
                        || layer.prototype_key.clone(),
                        |definition| definition.display_name.clone(),
                    ),
                prototype_key: layer.prototype_key.clone(),
                source_count: layer.instances().len(),
            })
            .collect()
    }

    /// Position a copy at world X/Z. The nearest immutable instance supplies
    /// this species' variation; its source key remains stable in the edit.
    pub fn plant_placement_at(
        &self,
        layer_id: &VegetationLayerId,
        id: EntityId,
        target_xz_m: [f64; 2],
        clearance_m: f64,
        yaw_degrees: Option<f64>,
        scale_factor: Option<f64>,
    ) -> Result<ImportedPlantPlacement, String> {
        let [x, z] = target_xz_m;
        let extent = self.document.extent_m;
        if !x.is_finite()
            || !z.is_finite()
            || x.abs() > f64::from(extent.size_x_m) * 0.5
            || z.abs() > f64::from(extent.size_z_m) * 0.5
            || !clearance_m.is_finite()
            || clearance_m.abs() > 4096.0
        {
            return Err("Imported plant destination must be finite and inside the map".into());
        }
        let layer = self
            .layers
            .iter()
            .find(|layer| layer.layer_id == layer_id.as_str())
            .ok_or("Imported plant catalogue layer absent")?;
        let source = layer
            .instances()
            .iter()
            .min_by(|a, b| {
                let distance = |position: [f64; 3]| {
                    (position[0] - x).mul_add(position[0] - x, (position[2] - z).powi(2))
                };
                distance(a.source.position_m)
                    .total_cmp(&distance(b.source.position_m))
                    .then_with(|| a.source.source_key.cmp(&b.source.source_key))
            })
            .ok_or("Imported plant catalogue layer empty")?;
        let [source_x, _, source_z] = source.source.position_m;
        Ok(ImportedPlantPlacement {
            id,
            layer_id: layer_id.clone(),
            source_key: source.source.source_key,
            translation_m: [x - source_x, clearance_m, z - source_z],
            yaw_degrees,
            scale_factor,
        })
    }
}
