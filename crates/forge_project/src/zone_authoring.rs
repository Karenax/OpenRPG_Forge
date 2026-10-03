//! Canonical aggregate authoring head active under ADR-0014; legacy archives remain readable and are migrated explicitly without becoming a second head.
use crate::{
    TerrainDocument, TerrainHeightDelta, TerrainMaterialOverride, VegetationDensityMask,
    VegetationDocument, VegetationExclusion, VegetationLayerId,
};
use forge_core::{ContentHash, Revision, canonical_json_sha256};
use serde::{Deserialize, Serialize};

pub const MAX_ZONE_HEAD_BYTES: usize = 4 * 1024 * 1024;

pub mod buildings;
pub mod exclusions;
#[cfg(not(target_arch = "wasm32"))]
pub mod file_session;
pub mod gameplay;
pub mod imported_plants;
pub mod inspection;
pub mod migration;
pub mod paths;
pub mod placements;
pub mod promotion;
pub mod regeneration;
pub mod rivers;
pub mod session;
pub mod source_objects;
pub mod species;
pub mod start;
pub mod terrain_commands;
pub mod vegetation_commands;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct GeneratorStamp {
    pub name: String,
    pub version: String,
    pub inputs_hash: ContentHash,
}

/// Accepted recipe inputs and their generated source documents, never an editable second head.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneBase {
    pub generator: GeneratorStamp,
    pub terrain: TerrainDocument,
    pub vegetation: Option<VegetationDocument>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DensityOverride {
    pub layer_id: VegetationLayerId,
    pub mask: VegetationDensityMask,
    /// Unlocking does not remove the correction.
    pub locked: bool,
}

/// Absolute layer replacements. None inherits the base; Some(empty) explicitly clears it.
#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneCorrections {
    /// Sparse edits, copies and persistent deletions of verified source objects (head v14).
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub source_object_edits: Option<Vec<source_objects::SourceObjectEdit>>,
    /// Canonical authored RPG definitions. Absent preserves all historical hashes.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub gameplay: Option<Box<forge_gameplay::GameplayDocument>>,
    /// Authored copies of imported plants; head v11 when present.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub imported_plant_placements: Option<Vec<imported_plants::ImportedPlantPlacement>>,
    /// Authored offsets over immutable imported instances; head v10 when present.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub imported_plant_offsets: Option<Vec<imported_plants::ImportedPlantOffset>>,
    /// None inherits the recipe exclusions; Some, including empty, replaces them (head v9).
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub vegetation_exclusions: Option<Vec<VegetationExclusion>>,
    /// Persistent exclusions of promoted generated instances; head v8 when present.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub promotions: Option<Vec<promotion::PromotedInstance>>,
    /// Authored prototype references; omitted preserves historical hashes (head v7).
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub species: Option<Vec<species::LayerSpecies>>,
    /// Omitted preserves old heads. An authored start requires head v6.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub start: Option<Box<start::ZoneStart>>,
    pub name: Option<String>,
    pub base_elevation_m: Option<f32>,
    pub height_deltas: Option<Vec<TerrainHeightDelta>>,
    pub material_overrides: Option<Vec<TerrainMaterialOverride>>,
    pub density: Vec<DensityOverride>,
    /// None is the historical v1 head. Some, including empty, opts into v2.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub placements: Option<Vec<placements::ManualPlacement>>,
    /// Omitted preserves historical hashes; explicit path edits opt into head v3.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub paths: Option<Vec<paths::AuthoredPath>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub rivers: Option<Vec<rivers::AuthoredRiver>>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub buildings: Option<Vec<buildings::AuthoredBuilding>>,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZoneAuthoringHead {
    pub schema: String,
    pub version: u32,
    pub revision: Revision,
    pub base: ZoneBase,
    pub corrections: ZoneCorrections,
}

impl ZoneAuthoringHead {
    pub fn validate(&self) -> Result<(), String> {
        if self.schema != "forge.zone-authoring"
            || self.version != self.corrections.schema_version()
        {
            return Err("Unsupported zone authoring schema/version".into());
        }
        let stamp = &self.base.generator;
        if let Some(edits) = &self.corrections.source_object_edits {
            source_objects::validate(self, edits)?;
        }
        if let Some(document) = self.corrections.gameplay.as_deref() {
            gameplay::validate(self, document)?;
        }
        if let Some(origins) = &self.corrections.promotions {
            promotion::validate(self, origins)?;
        }
        if let Some(species) = &self.corrections.species {
            species::validate(species, self.base.vegetation.as_ref())?;
        }
        if let Some(exclusions) = &self.corrections.vegetation_exclusions {
            exclusions::validate(exclusions, self.base.vegetation.as_ref())?;
        }
        if let Some(offsets) = &self.corrections.imported_plant_offsets {
            imported_plants::validate(self, offsets)?;
        }
        if let Some(placements) = &self.corrections.imported_plant_placements {
            imported_plants::validate_placements(self, placements)?;
        }
        if let Some(start) = self.corrections.start.as_deref() {
            start.validate(&self.base.terrain)?;
        }
        if let Some(buildings) = &self.corrections.buildings {
            buildings::validate(buildings, &self.base.terrain)?;
        }
        if let Some(rivers) = &self.corrections.rivers {
            rivers::validate(rivers, &self.base.terrain)?;
        }
        if let Some(paths) = &self.corrections.paths {
            paths::validate(paths, self.base.terrain.extent_m)?;
        }
        if let Some(placements) = &self.corrections.placements {
            placements::validate(placements)?;
        }
        if stamp.name.trim().is_empty()
            || stamp.name.len() > 128
            || stamp.version.trim().is_empty()
            || stamp.version.len() > 128
        {
            return Err("Generator name/version must be nonempty and bounded".into());
        }
        self.base.terrain.validate().map_err(|e| e.to_string())?;
        if let Some(vegetation) = &self.base.vegetation {
            vegetation.validate().map_err(|e| e.to_string())?;
            if vegetation.project_id != self.base.terrain.project_id
                || vegetation.area_id != self.base.terrain.area_id
                || vegetation.extent_m.size_x_m.to_bits()
                    != self.base.terrain.extent_m.size_x_m.to_bits()
                || vegetation.extent_m.size_z_m.to_bits()
                    != self.base.terrain.extent_m.size_z_m.to_bits()
            {
                return Err("Vegetation identity/extent differs from the terrain".into());
            }
            for layer in &vegetation.layers {
                if let Some(filter) = &layer.material_filter
                    && self
                        .base
                        .terrain
                        .material_layers
                        .get(filter.material_role.index())
                        .is_none_or(|l| l.id != filter.material_layer_id)
                {
                    return Err(
                        "Vegetation references an absent or incompatible terrain material".into(),
                    );
                }
            }
        }
        let mut previous: Option<&str> = None;
        for correction in &self.corrections.density {
            let id = correction.layer_id.as_str();
            if previous.is_some_and(|p| p >= id) {
                return Err("Density overrides must have unique sorted layer IDs".into());
            }
            previous = Some(id);
        }
        let (terrain, vegetation) = self.effective_unchecked()?;
        terrain.validate().map_err(|e| e.to_string())?;
        if let Some(vegetation) = vegetation {
            vegetation.validate().map_err(|e| e.to_string())?;
        }
        Ok(())
    }

    fn effective_unchecked(&self) -> Result<(TerrainDocument, Option<VegetationDocument>), String> {
        let mut terrain = self.base.terrain.clone();
        terrain.revision = self.revision;
        if let Some(name) = &self.corrections.name {
            terrain.name.clone_from(name);
        }
        if let Some(elevation) = self.corrections.base_elevation_m {
            terrain.height_sampling.base_elevation_m = elevation;
        }
        if let Some(samples) = &self.corrections.height_deltas {
            terrain.height_deltas.clone_from(samples);
        }
        if let Some(samples) = &self.corrections.material_overrides {
            terrain.material_overrides.clone_from(samples);
        }
        let mut vegetation = self.base.vegetation.clone();
        if let Some(document) = &mut vegetation {
            document.revision = self.revision;
            if let Some(exclusions) = &self.corrections.vegetation_exclusions {
                document.exclusions.clone_from(exclusions);
            }
        }
        for correction in &self.corrections.density {
            let layer = vegetation
                .as_mut()
                .and_then(|v| v.layers.iter_mut().find(|l| l.id == correction.layer_id))
                .ok_or("Orphan density correction: target layer absent")?;
            layer.density_mask = Some(correction.mask.clone());
        }
        Ok((terrain, vegetation))
    }

    /// Derived value for adapters; callers must retain the aggregate hash as authority.
    pub fn effective(&self) -> Result<(TerrainDocument, Option<VegetationDocument>), String> {
        self.validate()?;
        self.effective_unchecked()
    }

    pub fn to_bytes(&self) -> Result<Vec<u8>, String> {
        self.validate()?;
        let bytes = serde_json::to_vec(self).map_err(|e| e.to_string())?;
        if bytes.len() > MAX_ZONE_HEAD_BYTES {
            return Err("Zone head exceeds 4 MiB".into());
        }
        Ok(bytes)
    }

    pub fn from_bytes(bytes: &[u8]) -> Result<Self, String> {
        if bytes.len() > MAX_ZONE_HEAD_BYTES {
            return Err("Zone head exceeds 4 MiB".into());
        }
        let head: Self = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
        head.validate()?;
        Ok(head)
    }

    pub fn content_hash(&self) -> Result<ContentHash, String> {
        self.to_bytes()?;
        canonical_json_sha256(self).map_err(|e| e.to_string())
    }
}

impl ZoneCorrections {
    /// Minimum head version required by the present authoring domains.
    #[must_use]
    pub fn schema_version(&self) -> u32 {
        if self.source_object_edits.is_some() {
            return 14;
        }
        if self.gameplay.is_some() {
            13
        } else if self
            .imported_plant_placements
            .as_ref()
            .is_some_and(|placements| {
                placements.iter().any(|placement| {
                    placement.yaw_degrees.is_some() || placement.scale_factor.is_some()
                })
            })
        {
            12
        } else if self.imported_plant_placements.is_some() {
            11
        } else if self.imported_plant_offsets.is_some() {
            10
        } else if self.vegetation_exclusions.is_some() {
            9
        } else if self.promotions.is_some() {
            8
        } else if self.species.is_some() {
            7
        } else if self.start.is_some() {
            6
        } else if self.buildings.is_some() {
            5
        } else if self.rivers.is_some() {
            4
        } else if self.paths.is_some() {
            3
        } else if self.placements.is_some() {
            2
        } else {
            1
        }
    }
}
