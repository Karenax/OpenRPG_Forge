//! Read-only vegetation selection over the terrain's admitted partition.
//! Prototype bounds must come from the verified renderer assets, not a guessed species size.
use super::{ImportedEntity, ImportedPopulation, ImportedTerrainView};
use crate::{
    cell_streaming::{CellDetail, CellKey, CellStreamingPolicy, LoadedCell},
    terrain_hlod::{TerrainHlodKey, TerrainHlodLayout},
};
use forge_core::ContentHash;
use forge_project::VegetationCollisionProfile;
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::Arc,
};

#[cfg(test)]
mod tests;

/// Bounds about the prototype's placement pivot, before instance scale.
/// Radius encloses every source LOD; deformation is the maximum displacement after scaling.
#[derive(Clone, Copy, Debug)]
pub struct PrototypeEnvelope {
    pub radius_m: f64,
    pub deformation_m: f64,
}

/// The host supplies installed cells, never a pending request or camera-only approximation.
#[derive(Clone, Copy)]
pub struct AdmittedPlantCells<'a> {
    pub policy: &'a CellStreamingPolicy,
    pub cells: &'a BTreeMap<CellKey, LoadedCell>,
    pub distant: &'a TerrainHlodLayout,
    pub terrain_hash: ContentHash,
    pub head_hash: ContentHash,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PlantRepresentation {
    Detail,
    /// Resident coarse cell uses LOD1; the distant aggregate uses LOD2.
    Group {
        tile: TerrainHlodKey,
        lod: u8,
    },
}

pub struct SelectedPlant<'a> {
    pub entity: &'a ImportedEntity,
    pub layer_id: &'a str,
    pub prototype_key: &'a str,
    pub parameter_names: &'a [String],
    pub representation: PlantRepresentation,
}

/// Physical sources remain independent of visual residence and graphics quality.
/// The physics adapter still owns shape validation, body activation and backend acknowledgement.
pub struct PlantCollisionSource<'a> {
    pub entity: &'a ImportedEntity,
    pub profile: VegetationCollisionProfile,
}

pub struct StreamedPlantSelection<'a> {
    pub head_hash: ContentHash,
    pub terrain_hash: ContentHash,
    pub plants: Vec<SelectedPlant<'a>>,
    pub collisions: Vec<PlantCollisionSource<'a>>,
}

/// Read-only projected sources; an owned snapshot avoids repeating terrain
/// reprojection and cloning every authored instance on each simulation tick.
pub trait PlantView {
    fn head_hash(&self) -> ContentHash;
    fn layers(&self) -> impl ExactSizeIterator<Item = (&str, &[ImportedEntity])>;
}

impl PlantView for ImportedTerrainView<'_> {
    fn head_hash(&self) -> ContentHash {
        self.head_hash
    }
    fn layers(&self) -> impl ExactSizeIterator<Item = (&str, &[ImportedEntity])> {
        self.layers
            .iter()
            .map(|layer| (layer.layer_id, layer.instances.as_slice()))
    }
}

/// Immutable, derived cache. It is neither serialized nor a second authored
/// world; construction moves the terrain view's records without reprojection.
pub struct ImportedTerrainSnapshot {
    head: ContentHash,
    layers: Vec<(String, Vec<ImportedEntity>)>,
}
impl ImportedTerrainView<'_> {
    #[must_use]
    pub fn into_snapshot(self) -> ImportedTerrainSnapshot {
        ImportedTerrainSnapshot {
            head: self.head_hash,
            layers: self
                .layers
                .into_iter()
                .map(|layer| (layer.layer_id.to_owned(), layer.instances))
                .collect(),
        }
    }
}
impl PlantView for ImportedTerrainSnapshot {
    fn head_hash(&self) -> ContentHash {
        self.head
    }
    fn layers(&self) -> impl ExactSizeIterator<Item = (&str, &[ImportedEntity])> {
        self.layers
            .iter()
            .map(|(id, instances)| (id.as_str(), instances.as_slice()))
    }
}

/// Prepare the entire selection or fail without changing source records or a previous plan.
/// Every authored visible instance survives; this API neither thins nor regenerates vegetation.
pub fn select<'a, V: PlantView + ?Sized>(
    population: &'a ImportedPopulation,
    view: &'a V,
    admission: AdmittedPlantCells<'_>,
    envelopes: &BTreeMap<String, PrototypeEnvelope>,
) -> Result<StreamedPlantSelection<'a>, String> {
    validate_context(population, view, admission)?;
    let grid = Grid::new(&admission)?;
    let geometry = GridGeometry::from(admission.policy);
    let mut plants = Vec::new();
    let mut collisions = Vec::new();
    visit_sources(population, view, envelopes, |source, _, envelope| {
        let footprint = geometry.footprint(source.entity, envelope)?;
        plants.push(source.selected(grid.representation(footprint)?));
        if let Some(collision) = source.collision() {
            collisions.push(collision);
        }
        Ok(())
    })?;
    sort_unique(&mut plants, |plant| plant.entity.id)?;
    collisions.sort_unstable_by_key(|source| source.entity.id);
    Ok(StreamedPlantSelection {
        head_hash: admission.head_hash,
        terrain_hash: admission.terrain_hash,
        plants,
        collisions,
    })
}

/// Footprints and canonical ordering for one closed, immutable population and grid.
pub struct SnapshotSelection {
    population: Arc<ImportedPopulation>,
    view: Arc<ImportedTerrainSnapshot>,
    geometry: GridGeometry,
    terrain: ContentHash,
    records: Vec<SnapshotPlant>,
}

struct SnapshotPlant {
    indices: PlantIndices,
    profile: Option<VegetationCollisionProfile>,
    footprint: PlantFootprint,
}

impl SnapshotSelection {
    pub fn prepare(
        population: Arc<ImportedPopulation>,
        view: Arc<ImportedTerrainSnapshot>,
        admission: AdmittedPlantCells<'_>,
        envelopes: &BTreeMap<String, PrototypeEnvelope>,
    ) -> Result<Self, String> {
        validate_context(&population, view.as_ref(), admission)?;
        admission
            .policy
            .validate()
            .map_err(|error| error.to_string())?;
        let geometry = GridGeometry::from(admission.policy);
        let mut records = Vec::new();
        visit_sources(
            &population,
            view.as_ref(),
            envelopes,
            |source, indices, envelope| {
                records.push(SnapshotPlant {
                    indices,
                    profile: source.profile,
                    footprint: geometry.footprint(source.entity, envelope)?,
                });
                Ok(())
            },
        )?;
        sort_unique(&mut records, |record| record.indices.entity(&view).id)?;
        Ok(Self {
            population,
            view,
            geometry,
            terrain: admission.terrain_hash,
            records,
        })
    }

    #[must_use]
    pub fn matches_grid(&self, policy: &CellStreamingPolicy) -> bool {
        self.geometry == GridGeometry::from(policy)
    }

    pub fn select(
        &self,
        admission: AdmittedPlantCells<'_>,
    ) -> Result<StreamedPlantSelection<'_>, String> {
        if admission.head_hash != self.view.head
            || admission.terrain_hash != self.terrain
            || !self.matches_grid(admission.policy)
        {
            return Err("Vegetation footprints differ from the admitted world or grid".into());
        }
        // The current cell and HLOD owners are still checked on every transition.
        let grid = Grid::new(&admission)?;
        let mut plants = Vec::with_capacity(self.records.len());
        let mut collisions = Vec::new();
        for record in &self.records {
            let source = PlantSource {
                entity: record.indices.entity(&self.view),
                layer: &self.population.layers()[record.indices.source_layer],
                profile: record.profile,
            };
            plants.push(source.selected(grid.representation(record.footprint)?));
            if let Some(collision) = source.collision() {
                collisions.push(collision);
            }
        }
        Ok(StreamedPlantSelection {
            head_hash: admission.head_hash,
            terrain_hash: admission.terrain_hash,
            plants,
            collisions,
        })
    }
}

fn validate_context(
    population: &ImportedPopulation,
    view: &(impl PlantView + ?Sized),
    admission: AdmittedPlantCells<'_>,
) -> Result<(), String> {
    if view.head_hash() != admission.head_hash {
        return Err("Vegetation view differs from the admitted head".into());
    }
    let extent = population.document().extent_m;
    let sizes = [extent.size_x_m, extent.size_z_m].map(f64::from);
    if (0..2).any(|axis| {
        admission.policy.origin_xz_m[axis].to_bits() != (-sizes[axis] * 0.5).to_bits()
            || (f64::from(admission.policy.dimensions[axis]) * admission.policy.cell_size_m)
                .to_bits()
                != sizes[axis].to_bits()
    }) {
        return Err("Vegetation extent differs from the admitted terrain grid".into());
    }
    Ok(())
}

struct PlantIndices {
    view_layer: usize,
    instance: usize,
    source_layer: usize,
}

impl PlantIndices {
    fn entity<'a>(&self, view: &'a ImportedTerrainSnapshot) -> &'a ImportedEntity {
        &view.layers[self.view_layer].1[self.instance]
    }
}

struct PlantSource<'a> {
    entity: &'a ImportedEntity,
    layer: &'a super::ImportedLayer,
    profile: Option<VegetationCollisionProfile>,
}

impl<'a> PlantSource<'a> {
    fn selected(&self, representation: PlantRepresentation) -> SelectedPlant<'a> {
        SelectedPlant {
            entity: self.entity,
            layer_id: &self.layer.layer_id,
            prototype_key: &self.layer.prototype_key,
            parameter_names: &self.layer.parameter_names,
            representation,
        }
    }

    fn collision(&self) -> Option<PlantCollisionSource<'a>> {
        self.profile.map(|profile| PlantCollisionSource {
            entity: self.entity,
            profile,
        })
    }
}

fn visit_sources<'a>(
    population: &'a ImportedPopulation,
    view: &'a (impl PlantView + ?Sized),
    envelopes: &BTreeMap<String, PrototypeEnvelope>,
    mut visit: impl FnMut(PlantSource<'a>, PlantIndices, &PrototypeEnvelope) -> Result<(), String>,
) -> Result<(), String> {
    let maximum_plants = forge_project::imported_vegetation::MAX_IMPORTED_INSTANCES as usize
        + forge_project::zone_authoring::imported_plants::MAX_IMPORTED_PLANT_PLACEMENTS;
    let mut remaining = maximum_plants;
    let mut layers = BTreeSet::new();
    for (view_layer, (layer_id, instances)) in view.layers().enumerate() {
        if !layers.insert(layer_id) {
            return Err("Duplicate projected vegetation layer".into());
        }
        let source_layer = population
            .layers()
            .iter()
            .position(|source| source.layer_id == layer_id)
            .ok_or("Projected vegetation layer has no immutable source")?;
        let layer = &population.layers()[source_layer];
        let definition = population
            .document()
            .layers
            .iter()
            .find(|definition| definition.id.as_str() == layer_id)
            .ok_or("Projected vegetation layer has no definition")?;
        if instances.is_empty() {
            continue;
        }
        remaining = remaining
            .checked_sub(instances.len())
            .ok_or("Vegetation identity/count budget exceeded")?;
        let envelope = envelopes
            .get(&layer.prototype_key)
            .ok_or("Verified vegetation prototype envelope is missing")?;
        if !(0.0..=10_000.0).contains(&envelope.radius_m)
            || !(0.0..=128.0).contains(&envelope.deformation_m)
        {
            return Err("Invalid projected vegetation pose or verified envelope".into());
        }
        for (instance, entity) in instances.iter().enumerate() {
            if entity.source.parameters.len() != layer.parameter_names.len() {
                return Err(
                    "Projected vegetation parameter layout differs from its prototype".into(),
                );
            }
            if entity.id.as_uuid().is_nil() {
                return Err("Vegetation identity/count budget exceeded".into());
            }
            visit(
                PlantSource {
                    entity,
                    layer,
                    profile: definition.collision,
                },
                PlantIndices {
                    view_layer,
                    instance,
                    source_layer,
                },
                envelope,
            )?;
        }
    }
    if layers.len() != population.layers().len() {
        return Err("Projected vegetation omits a source layer".into());
    }
    Ok(())
}

fn sort_unique<T>(items: &mut [T], id: impl Fn(&T) -> forge_core::EntityId) -> Result<(), String> {
    items.sort_unstable_by_key(&id);
    if items.windows(2).any(|pair| id(&pair[0]) == id(&pair[1])) {
        return Err("Vegetation identity/count budget exceeded".into());
    }
    Ok(())
}

#[derive(Clone, Copy, PartialEq)]
struct GridGeometry {
    origin_xz_m: [f64; 2],
    dimensions: [u16; 2],
    cell_size_m: f64,
}

impl From<&CellStreamingPolicy> for GridGeometry {
    fn from(policy: &CellStreamingPolicy) -> Self {
        Self {
            origin_xz_m: policy.origin_xz_m,
            dimensions: policy.dimensions,
            cell_size_m: policy.cell_size_m,
        }
    }
}

#[derive(Clone, Copy)]
struct PlantFootprint {
    minimum: [usize; 2],
    maximum: [usize; 2],
    center: [usize; 2],
}

impl GridGeometry {
    fn footprint(
        &self,
        entity: &ImportedEntity,
        envelope: &PrototypeEnvelope,
    ) -> Result<PlantFootprint, String> {
        let source = &entity.source;
        if source
            .position_m
            .iter()
            .chain(&source.rotation_xyzw)
            .chain(&source.scale)
            .chain(&source.parameters)
            .any(|value| !value.is_finite())
            || source
                .scale
                .iter()
                .any(|value| *value <= 0.0 || *value > 10_000.0)
            || (source
                .rotation_xyzw
                .iter()
                .map(|value| value * value)
                .sum::<f64>()
                - 1.0)
                .abs()
                > 1e-9
        {
            return Err("Invalid projected vegetation pose or verified envelope".into());
        }
        let position = [source.position_m[0], source.position_m[2]];
        let relative: [f64; 2] = std::array::from_fn(|axis| {
            let value = position[axis];
            (value - self.origin_xz_m[axis]) / self.cell_size_m
        });
        if relative
            .iter()
            .enumerate()
            .any(|(axis, value)| *value < 0.0 || *value > f64::from(self.dimensions[axis]))
        {
            return Err("Projected vegetation lies outside the admitted terrain".into());
        }
        let radius = envelope.radius_m * source.scale.iter().copied().fold(0.0, f64::max)
            + envelope.deformation_m;
        let radius_cells = (radius / self.cell_size_m).next_up();
        // Include both sides of a touching cell boundary; the source perimeter is closed.
        let minimum = std::array::from_fn(|axis| {
            bounded_index(
                (relative[axis] - radius_cells).ceil() - 1.0,
                self.dimensions[axis],
            )
        });
        let maximum = std::array::from_fn(|axis| {
            bounded_index(
                (relative[axis] + radius_cells).floor(),
                self.dimensions[axis],
            )
        });

        let center = std::array::from_fn(|axis| {
            bounded_index(relative[axis].floor(), self.dimensions[axis])
        });
        Ok(PlantFootprint {
            minimum,
            maximum,
            center,
        })
    }
}

struct Grid<'a> {
    policy: &'a CellStreamingPolicy,
    /// Summed-area table makes each conservative crown/full-cell intersection O(1).
    full: Vec<u32>,
    owners: Vec<Option<(TerrainHlodKey, u8)>>,
}

impl<'a> Grid<'a> {
    fn new(admission: &AdmittedPlantCells<'a>) -> Result<Self, String> {
        let policy = admission.policy;
        policy.validate().map_err(|error| error.to_string())?;
        if admission.cells.len() > usize::from(policy.max_resident_cells) {
            return Err("Vegetation admitted-cell count exceeds the terrain budget".into());
        }
        let bytes = admission
            .cells
            .values()
            .try_fold(0_u64, |sum, cell| sum.checked_add(cell.bytes))
            .ok_or("Vegetation admitted-cell byte accounting overflow")?;
        if bytes > policy.max_resident_bytes {
            return Err("Vegetation admitted-cell bytes exceed the terrain budget".into());
        }
        let details = admission
            .cells
            .iter()
            .map(|(&key, cell)| {
                if key != cell.key || cell.source != admission.terrain_hash || cell.bytes == 0 {
                    return Err("Vegetation requires matching admitted terrain cells".to_owned());
                }
                Ok((key, cell.detail))
            })
            .collect::<Result<BTreeMap<_, _>, _>>()?;
        if TerrainHlodLayout::new(policy, &details)? != *admission.distant {
            return Err("Vegetation distant layout differs from admitted terrain coverage".into());
        }
        let [width, height] = policy.dimensions.map(usize::from);
        let mut full = vec![0; (width + 1) * (height + 1)];
        let mut owners = vec![None; width * height];
        for (&key, &detail) in &details {
            let (x, z) = (usize::from(key.x), usize::from(key.z));
            if detail == CellDetail::Full {
                full[(z + 1) * (width + 1) + x + 1] = 1;
            } else {
                owners[z * width + x] = Some((
                    TerrainHlodKey {
                        x: key.x,
                        z: key.z,
                        level: 0,
                    },
                    1,
                ));
            }
        }
        for key in admission.distant.tiles().keys() {
            for z in key.z..key.z + key.side_cells() {
                for x in key.x..key.x + key.side_cells() {
                    owners[usize::from(z) * width + usize::from(x)] = Some((*key, 2));
                }
            }
        }
        for z in 1..=height {
            for x in 1..=width {
                let index = z * (width + 1) + x;
                full[index] += full[index - 1] + full[index - width - 1] - full[index - width - 2];
            }
        }
        Ok(Self {
            policy,
            full,
            owners,
        })
    }

    fn representation(&self, footprint: PlantFootprint) -> Result<PlantRepresentation, String> {
        if self.full_count(footprint.minimum, footprint.maximum) > 0 {
            return Ok(PlantRepresentation::Detail);
        }
        let [x, z] = footprint.center;
        let (tile, lod) = self.owners[z * usize::from(self.policy.dimensions[0]) + x]
            .ok_or("Projected vegetation has no admitted terrain owner")?;
        Ok(PlantRepresentation::Group { tile, lod })
    }

    fn full_count(&self, minimum: [usize; 2], maximum: [usize; 2]) -> u32 {
        let stride = usize::from(self.policy.dimensions[0]) + 1;
        let [x0, z0] = minimum;
        let [x1, z1] = maximum.map(|value| value + 1);
        self.full[z1 * stride + x1] + self.full[z0 * stride + x0]
            - self.full[z0 * stride + x1]
            - self.full[z1 * stride + x0]
    }
}

#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
fn bounded_index(value: f64, dimension: u16) -> usize {
    usize::from(value.max(0.0).min(f64::from(dimension - 1)) as u16)
}
