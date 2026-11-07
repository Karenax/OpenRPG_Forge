// SPDX-License-Identifier: MIT
// Source deformation bounds derived from dgreenheck/tidewater 1438b1ab, plant.wgsl.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Bounds of the actual immutable prepared meshes/records, before world publication.
use super::{Pin, Prepared, content};
use crate::source_lighting::frame::MAX_WIND_SPEED;
use crate::source_vegetation::world::SourceMaterial;
use crate::source_vegetation::world::WorldPlantMaterial;
use bevy::math::{DMat3, DVec3};
use forge_assets::source_vegetation_package::profile::supports_world_shader;
use forge_core::{ContentHash, EntityId};
use forge_world::imported_vegetation::{
    ImportedPopulation, ImportedTerrainView,
    streaming::{
        self, AdmittedPlantCells, ImportedTerrainSnapshot, PlantView, PrototypeEnvelope,
        StreamedPlantSelection,
    },
};
use std::{collections::BTreeMap, sync::Arc};
mod geometry;
use geometry::Summary;
// Keep adversarial seed classes from turning a linear population walk into
// one full mesh scan per instance. The ten source profiles use far fewer.
const MAX_GEOMETRY_SUMMARIES: usize = 128;
#[cfg(test)]
mod tests;

/// Closed to one prepared source/authoring/terrain identity. Dynamic wind in the
/// native material's complete admitted range cannot enlarge these envelopes.
pub struct WorldEnvelopes {
    identity: ContentHash,
    head: ContentHash,
    terrain: ContentHash,
    population: ContentHash,
    pins: Arc<BTreeMap<EntityId, Pin>>,
    prototypes: BTreeMap<String, PrototypeEnvelope>,
}

/// Created only after exact source validation; exposes no mutable selection.
pub(super) struct VerifiedSelection<'a> {
    pins: &'a Arc<BTreeMap<EntityId, Pin>>,
    population: ContentHash,
    selection: StreamedPlantSelection<'a>,
    snapshot_binding: Option<SnapshotBinding<'a>>,
}

struct SnapshotBinding<'a> {
    bounds: &'a Arc<WorldEnvelopes>,
    population: &'a Arc<ImportedPopulation>,
    snapshot: &'a Arc<ImportedTerrainSnapshot>,
}

/// The complete population and terrain snapshot have already passed exact
/// source checks. Strong immutable ownership prevents later in-place edits.
pub(super) struct BoundSources {
    bounds: Arc<WorldEnvelopes>,
    population: Arc<ImportedPopulation>,
    snapshot: Arc<ImportedTerrainSnapshot>,
}

impl BoundSources {
    pub(super) fn matches(
        &self,
        bounds: &Arc<WorldEnvelopes>,
        population: &Arc<ImportedPopulation>,
        snapshot: &Arc<ImportedTerrainSnapshot>,
    ) -> bool {
        Arc::ptr_eq(&self.bounds, bounds)
            && Arc::ptr_eq(&self.population, population)
            && Arc::ptr_eq(&self.snapshot, snapshot)
    }

    pub(super) fn select(
        &self,
        admission: AdmittedPlantCells<'_>,
    ) -> Result<VerifiedSelection<'_>, String> {
        if admission.head_hash != self.bounds.head || admission.terrain_hash != self.bounds.terrain
        {
            return Err("Bound plant sources belong to another authored terrain".into());
        }
        let selection = {
            let _profile = crate::cpu_timing::Span::new("plants.partition.world_select");
            streaming::select(
                &self.population,
                self.snapshot.as_ref(),
                admission,
                &self.bounds.prototypes,
            )?
        };
        if selection.plants.len() != self.bounds.pins.len() {
            return Err("Bound plant population changed".into());
        }
        Ok(VerifiedSelection {
            pins: &self.bounds.pins,
            population: self.bounds.population,
            selection,
            snapshot_binding: None,
        })
    }
}

/// Identity of a complete physical source set already checked against this
/// immutable population. It does not certify active Rapier bodies or poses.
pub(super) struct VerifiedCollisionSource {
    pins: Arc<BTreeMap<EntityId, Pin>>,
    population: ContentHash,
    head: ContentHash,
    terrain: ContentHash,
    physical_identity: ContentHash,
}

impl<'a> VerifiedSelection<'a> {
    pub(super) fn bind_sources(&self) -> Option<BoundSources> {
        self.snapshot_binding.as_ref().map(|binding| BoundSources {
            bounds: Arc::clone(binding.bounds),
            population: Arc::clone(binding.population),
            snapshot: Arc::clone(binding.snapshot),
        })
    }
    pub(super) fn matches_pins(&self, pins: &Arc<BTreeMap<EntityId, Pin>>) -> bool {
        Arc::ptr_eq(self.pins, pins)
    }

    pub(super) fn selection(&self) -> &StreamedPlantSelection<'a> {
        &self.selection
    }

    pub(super) fn collision_source(
        &self,
        physical_identity: ContentHash,
    ) -> VerifiedCollisionSource {
        VerifiedCollisionSource {
            pins: Arc::clone(self.pins),
            population: self.population,
            head: self.selection.head_hash,
            terrain: self.selection.terrain_hash,
            physical_identity,
        }
    }

    pub(super) fn matches_collision_source(
        &self,
        source: &VerifiedCollisionSource,
        physical_identity: ContentHash,
    ) -> bool {
        Arc::ptr_eq(self.pins, &source.pins)
            && self.population == source.population
            && self.selection.head_hash == source.head
            && self.selection.terrain_hash == source.terrain
            && physical_identity == source.physical_identity
    }
}

impl WorldEnvelopes {
    /// Enlarge this same authored projection with admitted replacement geometry.
    /// Called before partition admission; no source envelope is made smaller.
    pub(super) fn include_models(
        &mut self,
        view: &ImportedTerrainSnapshot,
        radii: &BTreeMap<EntityId, f64>,
    ) -> Result<(), String> {
        if view.head_hash() != self.head {
            return Err("Replacement model bounds belong to another head".into());
        }
        let mut prototypes = self.prototypes.clone();
        let mut seen = 0;
        for (_, entities) in view.layers() {
            for entity in entities {
                let Some(&radius) = radii.get(&entity.id) else {
                    continue;
                };
                let pin = self
                    .pins
                    .get(&entity.id)
                    .ok_or("Replacement bound identity absent")?;
                if pin.content != content(entity) || !radius.is_finite() || radius <= 0.0 {
                    return Err("Replacement model bound or pose differs".into());
                }
                let position = DVec3::from_array(entity.source.position_m);
                let rounding = position.distance(position.as_vec3().as_dvec3());
                let scale = entity.source.scale.iter().copied().fold(0.0, f64::max);
                let normalized = expanded(radius + rounding) / scale;
                if !normalized.is_finite() || normalized > 10_000.0 {
                    return Err("Replacement model exceeds the streaming bound contract".into());
                }
                let bound = prototypes
                    .get_mut(&pin.layer.prototype)
                    .ok_or("Replacement source prototype bound absent")?;
                bound.radius_m = bound.radius_m.max(normalized.next_up());
                seen += 1;
            }
        }
        if seen != radii.len() {
            return Err("Replacement bounds contain an unknown plant".into());
        }
        self.prototypes = prototypes;
        Ok(())
    }

    #[must_use]
    pub fn identity(&self) -> ContentHash {
        self.identity
    }

    #[must_use]
    pub fn prototypes(&self) -> &BTreeMap<String, PrototypeEnvelope> {
        &self.prototypes
    }

    pub(super) fn select_verified<'a, V: PlantView + ?Sized>(
        &'a self,
        population: &'a ImportedPopulation,
        view: &'a V,
        admission: AdmittedPlantCells<'_>,
    ) -> Result<VerifiedSelection<'a>, String> {
        Ok(VerifiedSelection {
            pins: &self.pins,
            population: self.population,
            selection: self.select(population, view, admission)?,
            snapshot_binding: None,
        })
    }

    pub(super) fn select_snapshot_verified<'a>(
        self: &'a Arc<Self>,
        population: &'a Arc<ImportedPopulation>,
        snapshot: &'a Arc<ImportedTerrainSnapshot>,
        admission: AdmittedPlantCells<'_>,
    ) -> Result<VerifiedSelection<'a>, String> {
        let mut verified =
            self.select_verified(population.as_ref(), snapshot.as_ref(), admission)?;
        verified.snapshot_binding = Some(SnapshotBinding {
            bounds: self,
            population,
            snapshot,
        });
        Ok(verified)
    }

    /// Only admitted terrain cells can select a live representation. Verify all
    /// raw source bits again: a copied head hash cannot admit a changed pose.
    pub fn select<'a, V: PlantView + ?Sized>(
        &self,
        population: &'a ImportedPopulation,
        view: &'a V,
        admission: AdmittedPlantCells<'_>,
    ) -> Result<StreamedPlantSelection<'a>, String> {
        if admission.head_hash != self.head
            || admission.terrain_hash != self.terrain
            || population
                .document()
                .content_hash()
                .map_err(|error| error.to_string())?
                != self.population
        {
            return Err("Plant envelopes belong to another authored terrain".into());
        }
        let selected = {
            let _profile = crate::cpu_timing::Span::new("plants.partition.world_select");
            streaming::select(population, view, admission, &self.prototypes)?
        };
        let _profile = crate::cpu_timing::Span::new("plants.partition.pins_validate");
        if selected.plants.len() != self.pins.len() {
            return Err("Plant envelope population changed".into());
        }
        for plant in &selected.plants {
            let pin = self
                .pins
                .get(&plant.entity.id)
                .ok_or("Unknown bounded plant identity")?;
            if pin.content != content(plant.entity)
                || pin.layer.layer != plant.layer_id
                || pin.layer.prototype != plant.prototype_key
                || pin.layer.parameters != plant.parameter_names
            {
                return Err("Plant envelope source, layout or exact pose changed".into());
            }
        }
        Ok(selected)
    }
}

impl Prepared {
    /// Enclose every real source LOD of this same immutable projection. One
    /// geometry scan per group/kind, then bounded work per authored instance.
    /// No source query radius, guessed tree size or camera distance is reused.
    #[expect(
        clippy::too_many_lines,
        reason = "atomic closure of bounds to all prepared authoring pins and levels"
    )]
    pub fn world_envelopes(
        &self,
        population: &ImportedPopulation,
        view: &ImportedTerrainView<'_>,
    ) -> Result<WorldEnvelopes, String> {
        if view.head_hash != self.head
            || population
                .document()
                .content_hash()
                .map_err(|error| error.to_string())?
                != self.population
            || self.shader.is_none_or(|hash| !supports_world_shader(hash))
        {
            return Err(
                "World envelopes require the pinned plant shader and prepared authored view".into(),
            );
        }
        let mut result = WorldEnvelopes {
            identity: self.identity,
            head: self.head,
            terrain: self.terrain,
            population: self.population,
            pins: self.pins.clone(),
            prototypes: BTreeMap::new(),
        };
        let mut summaries = BTreeMap::new();
        let mut seen = std::collections::BTreeSet::new();
        for layer in &view.layers {
            let source = population
                .layers()
                .iter()
                .find(|source| source.layer_id == layer.layer_id)
                .ok_or("Bounded plant source layer disappeared")?;
            for entity in &layer.instances {
                let pin = self
                    .pins
                    .get(&entity.id)
                    .ok_or("Bounded plant identity differs")?;
                if !seen.insert(entity.id)
                    || pin.content != content(entity)
                    || pin.layer.layer != layer.layer_id
                    || pin.layer.prototype != source.prototype_key
                    || pin.layer.parameters != source.parameter_names
                {
                    return Err("Bounded plant inputs differ from their prepared projection".into());
                }
                let group = &self.scene.groups[pin.group];
                let record = &group.records[pin.index];
                let model = group.models[pin.index];
                let base = record.i_pos.truncate().as_dvec3();
                let pivot = DVec3::from_array(entity.source.position_m);
                let scale = entity.source.scale.iter().copied().fold(0.0, f64::max);
                if !scale.is_finite() || scale <= 0.0 {
                    return Err("Invalid bounded plant scale".into());
                }
                let matrix = DMat3::from_cols(
                    model.x_axis.truncate().as_dvec3(),
                    model.y_axis.truncate().as_dvec3(),
                    model.z_axis.truncate().as_dvec3(),
                );
                let transform =
                    TransformBounds::new(matrix, model.w_axis.truncate().as_dvec3() - base)?;
                let mut static_radius = 0.0_f64;
                let mut displacement = 0.0_f64;
                for (level_index, level) in group.levels.iter().enumerate() {
                    let kind = level.description.kind;
                    WorldPlantMaterial::validate_records(kind, std::slice::from_ref(record))?;
                    let class = if kind == 0 {
                        record.i_dat.w.floor().to_bits()
                    } else {
                        u32::from(record.i_dat.y < 0.0)
                    };
                    let key = (pin.group, level_index, class);
                    if !summaries.contains_key(&key) && summaries.len() >= MAX_GEOMETRY_SUMMARIES {
                        return Err("Plant geometry-summary budget exceeded".into());
                    }
                    if let std::collections::btree_map::Entry::Vacant(entry) = summaries.entry(key)
                    {
                        entry.insert(Summary::from_mesh(
                            &level.mesh,
                            kind,
                            record.i_dat.w.floor(),
                            record.i_dat.y < 0.0,
                        )?);
                    }
                    let summary = &summaries[&key];
                    let (radius, motion) = summary.enclose(kind, record, &transform)?;
                    static_radius = static_radius.max(radius);
                    displacement = displacement.max(motion);
                }
                let radius = (expanded(static_radius + base.distance(pivot)) / scale).next_up();
                let motion = expanded(displacement);
                if !radius.is_finite() || radius > 10_000.0 || !motion.is_finite() || motion > 128.0
                {
                    return Err(
                        "Derived plant model/deformation envelope exceeds the world contract"
                            .into(),
                    );
                }
                let bound = result
                    .prototypes
                    .entry(pin.layer.prototype.clone())
                    .or_insert(PrototypeEnvelope {
                        radius_m: 0.0,
                        deformation_m: 0.0,
                    });
                bound.radius_m = bound.radius_m.max(radius);
                bound.deformation_m = bound.deformation_m.max(motion);
            }
        }
        if seen.len() != self.pins.len() {
            return Err("Bounded view omits prepared plants".into());
        }
        Ok(result)
    }
}

// Arithmetic is evaluated outwards in f64 over the actual packed f32 data.
// Reserve 512 f32 epsilons and 1 mm for shader/normalization accumulation.
fn expanded(value: f64) -> f64 {
    (value * (1.0 + 512.0 * f64::from(f32::EPSILON)) + 0.001).next_up()
}

struct TransformBounds {
    matrix: DMat3,
    offset: DVec3,
    operator: f64,
    normal_operator: f64,
}
impl TransformBounds {
    fn new(matrix: DMat3, offset: DVec3) -> Result<Self, String> {
        let columns = [matrix.x_axis, matrix.y_axis, matrix.z_axis];
        let lengths = columns.map(DVec3::length_squared);
        if !matrix.is_finite()
            || !offset.is_finite()
            || lengths
                .iter()
                .any(|&n| n < f64::from(f32::MIN_POSITIVE) || n > f64::from(f32::MAX) * 0.25)
        {
            return Err("Plant instance normal transform is not safely representable".into());
        }
        let normal = DMat3::from_cols(
            columns[0] / lengths[0],
            columns[1] / lengths[1],
            columns[2] / lengths[2],
        );
        Ok(Self {
            matrix,
            offset,
            operator: operator_bound(matrix),
            normal_operator: operator_bound(normal),
        })
    }
    fn radius(&self, absolute: DVec3, radius: f64) -> f64 {
        let linear = self.matrix.abs() * absolute;
        (linear.length().min(self.operator * radius) + self.offset.length()).next_up()
    }
    fn horizontal(&self, absolute: DVec3) -> f64 {
        let linear = self.matrix.abs() * absolute + self.offset.abs();
        linear.x.hypot(linear.z).next_up()
    }
}

/// Gershgorin bounds the largest eigenvalue of the Gram matrix, including shear.
fn operator_bound(matrix: DMat3) -> f64 {
    let gram = matrix.transpose() * matrix;
    let rows = gram.abs().transpose();
    rows.x_axis
        .element_sum()
        .max(rows.y_axis.element_sum())
        .max(rows.z_axis.element_sum())
        .sqrt()
        .next_up()
}

// Unit wind validation allows squared length error 0.001; this safely bounds
// both its world direction and perpendicular. Gust output is smoothstep [0,1].
const WIND_DIRECTION_LENGTH: f64 = 1.001;
fn wind_strength() -> f64 {
    (f64::from(MAX_WIND_SPEED) * 0.1).max(0.03)
}
