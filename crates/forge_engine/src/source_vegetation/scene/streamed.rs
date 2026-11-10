//! Source-profile geometry sharing the canonical terrain partition. No map-name condition.
//! GPU inventory publication is separate from host physics and final visual qualification.
use super::Vegetation;
use bevy::prelude::Component;
use forge_assets::source_vegetation_package::{PlantPackage, geometry::PlantGeometry};
use forge_core::{ContentHash, EntityId, bytes_sha256};
use forge_project::zone_authoring::ZoneAuthoringHead;
use forge_world::imported_vegetation::{
    ImportedEntity, ImportedPopulation,
    streaming::{PlantRepresentation, StreamedPlantSelection},
};
use std::{collections::BTreeMap, sync::Arc};

mod bounds;
pub mod host;
mod prepare;
mod resident;
mod shadow_range;
pub use bounds::WorldEnvelopes;
pub use resident::Resident;
#[cfg(test)]
mod tests;

struct LayerPin {
    layer: String,
    prototype: String,
    parameters: Vec<String>,
}
#[derive(Clone)]
struct Pin {
    layer: Arc<LayerPin>,
    content: ContentHash,
    group: usize,
    index: usize,
}

pub struct Prepared {
    scene: Vegetation,
    head: ContentHash,
    terrain: ContentHash,
    identity: ContentHash,
    shader: Option<ContentHash>,
    population: ContentHash,
    pins: Arc<BTreeMap<EntityId, Pin>>,
    levels: Vec<usize>,
    triangles: Vec<Vec<usize>>,
}

/// Complete immutable CPU selection; no partial application occurs for a bad pose or identity.
pub struct Admission {
    identity: ContentHash,
    instances: BTreeMap<(usize, usize), (EntityId, PlantRepresentation)>,
}

/// Source-selected render instances; one source can overlap two fading levels.
pub struct Selection {
    identity: ContentHash,
    indices: Vec<Vec<Vec<usize>>>,
    parts: BTreeMap<(EntityId, u8), PlantRepresentation>,
    pub detail_instances: usize,
    pub group_instances: usize,
    pub cpu_selected_triangles: usize,
}

#[derive(Component, Clone, Copy, Debug)]
pub struct StreamedPlantPart {
    pub id: EntityId,
    pub representation: PlantRepresentation,
    pub source_lod: u8,
}

fn content(entity: &ImportedEntity) -> ContentHash {
    let source = &entity.source;
    let mut bytes = source.source_key.as_bytes().to_vec();
    for value in source
        .position_m
        .iter()
        .chain(&source.rotation_xyzw)
        .chain(&source.scale)
        .chain(&source.parameters)
    {
        bytes.extend(value.to_le_bytes());
    }
    bytes_sha256(&bytes)
}

impl Prepared {
    /// Consume derived CPU inputs for authoring/preview. This grants no GPU
    /// publication or physical readiness; the receiving host owns its admission.
    #[must_use]
    pub fn into_scene(self) -> Vegetation {
        self.scene
    }

    /// `geometry` comes from the closed package decoder. The same authored terrain
    /// view is used for pins and packing; source ordering and authored copies survive.
    pub fn new(
        package: &PlantPackage,
        geometry: PlantGeometry,
        population: &ImportedPopulation,
        head: &ZoneAuthoringHead,
        heights: &[u8],
        terrain: ContentHash,
    ) -> Result<Self, String> {
        prepare::prepare(package, geometry, population, head, heights, terrain)
    }

    /// Admit every canonical source independently of the visible LOD selection.
    pub fn admit(&self, selection: &StreamedPlantSelection<'_>) -> Result<Admission, String> {
        self.admit_inner(selection, true)
    }

    /// Reuse source checks only when the sealed selection borrows the very
    /// same immutable pins as this projection. A different projection must
    /// use the complete public admission path.
    fn admit_verified(
        &self,
        verified: &bounds::VerifiedSelection<'_>,
    ) -> Result<Admission, String> {
        if !verified.matches_pins(&self.pins) {
            return Err("Verified plant selection belongs to another projection".into());
        }
        self.admit_inner(verified.selection(), false)
    }

    fn admit_inner(
        &self,
        selection: &StreamedPlantSelection<'_>,
        check_sources: bool,
    ) -> Result<Admission, String> {
        if selection.head_hash != self.head
            || selection.terrain_hash != self.terrain
            || selection.plants.len() != self.pins.len()
        {
            return Err("Plant selection differs from its prepared authored/terrain inputs".into());
        }
        let mut instances = BTreeMap::new();
        for plant in &selection.plants {
            let pin = self
                .pins
                .get(&plant.entity.id)
                .ok_or("Unknown selected plant identity")?;
            if check_sources
                && (pin.layer.layer != plant.layer_id
                    || pin.layer.prototype != plant.prototype_key
                    || pin.layer.parameters.as_slice() != plant.parameter_names
                    || pin.content != content(plant.entity))
            {
                return Err(
                    "Selected plant source/pose differs from the prepared projection".into(),
                );
            }
            if matches!(plant.representation, PlantRepresentation::Group { lod, .. } if !(1..=2).contains(&lod))
            {
                return Err("Unsupported streamed vegetation band".into());
            }
            if instances
                .insert(
                    (pin.group, pin.index),
                    (plant.entity.id, plant.representation),
                )
                .is_some()
            {
                return Err("Duplicate selected plant identity".into());
            }
        }
        Ok(Admission {
            identity: self.identity,
            instances,
        })
    }

    pub fn select(
        &self,
        admitted: &Admission,
        camera: bevy::prelude::Vec3,
    ) -> Result<Selection, String> {
        self.plan(admitted, self.scene.selection_indices(camera)?)
    }

    fn plan(
        &self,
        admitted: &Admission,
        indices: Vec<Vec<Vec<usize>>>,
    ) -> Result<Selection, String> {
        if admitted.identity != self.identity
            || admitted.instances.len() != self.pins.len()
            || indices.len() != self.levels.len()
        {
            return Err("Plant source admission/selection belongs to another projection".into());
        }
        let mut result = Selection {
            identity: self.identity,
            indices,
            parts: BTreeMap::new(),
            detail_instances: 0,
            group_instances: 0,
            cpu_selected_triangles: 0,
        };
        for (group, levels) in result.indices.iter().enumerate() {
            if levels.len() != self.levels[group] {
                return Err("Plant source selection omits a level".into());
            }
            for (lod, ids) in levels.iter().enumerate() {
                let source_lod = u8::try_from(lod).map_err(|_| "LOD index overflow")?;
                for &index in ids {
                    let &(id, representation) = admitted
                        .instances
                        .get(&(group, index))
                        .ok_or("Source-selected plant is not admitted")?;
                    if result
                        .parts
                        .insert((id, source_lod), representation)
                        .is_some()
                    {
                        return Err("Duplicate source-selected plant level".into());
                    }
                    if lod == 0 {
                        result.detail_instances += 1;
                    } else {
                        result.group_instances += 1;
                    }
                    result.cpu_selected_triangles = result
                        .cpu_selected_triangles
                        .checked_add(self.triangles[group][lod])
                        .ok_or("Selected triangle count overflow")?;
                }
            }
        }
        Ok(result)
    }
}
