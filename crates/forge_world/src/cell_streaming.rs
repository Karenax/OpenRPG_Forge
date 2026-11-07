//! Bounded, transactional residency of derived cells; no renderer or authored mutation.
use forge_core::{AreaId, ContentHash, EntityId};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;
use thiserror::Error;

mod procedural;
mod terrain;
#[cfg(test)]
mod tests;
pub use procedural::{ProceduralCellMesh, ProceduralCellSource};
pub use terrain::{SampledCellMesh, SampledCellSource, SampledMeshQuality};

const MAX_AXIS_CELLS: u16 = 256;
const MAX_RADIUS: u16 = 8;

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CellKey {
    pub x: u16,
    pub z: u16,
}

impl CellKey {
    #[must_use]
    pub fn entity_id(self, area: AreaId) -> EntityId {
        EntityId::deterministic(format!("stream-cell:{area}:{}:{}", self.x, self.z))
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum CellDetail {
    Full,
    Hlod,
}

/// Estimates must include all payloads owned by a cell, not only its triangles.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CellStreamingPolicy {
    pub origin_xz_m: [f64; 2],
    pub cell_size_m: f64,
    pub dimensions: [u16; 2],
    pub detail_radius: u16,
    pub far_radius: u16,
    pub max_resident_cells: u16,
    pub full_cell_bytes: u64,
    pub hlod_cell_bytes: u64,
    pub max_resident_bytes: u64,
    pub max_staging_bytes: u64,
}

impl CellStreamingPolicy {
    pub fn validate(&self) -> Result<(), CellStreamError> {
        if !self.cell_size_m.is_finite()
            || !(1.0..=4096.0).contains(&self.cell_size_m)
            || self
                .origin_xz_m
                .iter()
                .any(|v| !v.is_finite() || v.abs() > 1_000_000.0)
            || self
                .dimensions
                .iter()
                .any(|&v| v == 0 || v > MAX_AXIS_CELLS)
            || self.detail_radius >= self.far_radius
            || self.far_radius > MAX_RADIUS
            || self.max_resident_cells == 0
            || self.max_resident_cells > (2 * MAX_RADIUS + 1).pow(2)
            || self.full_cell_bytes == 0
            || self.hlod_cell_bytes == 0
            || self.max_resident_bytes == 0
            || self.max_staging_bytes < self.max_resident_bytes
        {
            return Err(CellStreamError::InvalidPolicy);
        }
        Ok(())
    }

    pub fn cell_at(&self, position: [f64; 2]) -> Result<CellKey, CellStreamError> {
        let relative = std::array::from_fn::<_, 2, _>(|i| {
            (position[i] - self.origin_xz_m[i]) / self.cell_size_m
        });
        if relative
            .iter()
            .enumerate()
            .any(|(i, &v)| !v.is_finite() || v < 0.0 || v >= f64::from(self.dimensions[i]))
        {
            return Err(CellStreamError::OutsideGrid);
        }
        // Bounds and finiteness established above; floor implements half-open cells.
        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        Ok(CellKey {
            x: relative[0].floor() as u16,
            z: relative[1].floor() as u16,
        })
    }

    const fn estimate(&self, detail: CellDetail) -> u64 {
        match detail {
            CellDetail::Full => self.full_cell_bytes,
            CellDetail::Hlod => self.hlod_cell_bytes,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct LoadedCell {
    pub key: CellKey,
    pub detail: CellDetail,
    pub source: ContentHash,
    pub bytes: u64,
}

#[derive(Clone, Debug)]
pub struct CellStreamPlan {
    owner: EntityId,
    generation: u64,
    source: ContentHash,
    policy: CellStreamingPolicy,
    resident_before_bytes: u64,
    retained_bytes: u64,
    desired: BTreeMap<CellKey, CellDetail>,
    acquire: BTreeMap<CellKey, CellDetail>,
    retire: Vec<CellKey>,
    pub estimated_resident_bytes: u64,
    pub estimated_peak_bytes: u64,
}

impl CellStreamPlan {
    #[must_use]
    pub const fn source(&self) -> ContentHash {
        self.source
    }

    #[must_use]
    pub const fn acquisitions(&self) -> &BTreeMap<CellKey, CellDetail> {
        &self.acquire
    }

    #[must_use]
    pub fn retirements(&self) -> &[CellKey] {
        &self.retire
    }

    #[must_use]
    pub const fn desired(&self) -> &BTreeMap<CellKey, CellDetail> {
        &self.desired
    }
}

/// `owner` is a unique injectable session identity, never a renderer entity handle.
#[derive(Debug)]
pub struct CellStreamer {
    owner: EntityId,
    source: ContentHash,
    policy: CellStreamingPolicy,
    generation: u64,
    resident: BTreeMap<CellKey, LoadedCell>,
}

impl CellStreamer {
    pub fn new(
        owner: EntityId,
        source: ContentHash,
        policy: CellStreamingPolicy,
    ) -> Result<Self, CellStreamError> {
        policy.validate()?;
        Ok(Self {
            owner,
            source,
            policy,
            generation: 0,
            resident: BTreeMap::new(),
        })
    }

    #[must_use]
    pub const fn resident(&self) -> &BTreeMap<CellKey, LoadedCell> {
        &self.resident
    }

    pub fn prepare(&self, position_xz_m: [f64; 2]) -> Result<CellStreamPlan, CellStreamError> {
        self.prepare_with_protected_cells(position_xz_m, &[])
    }

    /// Retain full-detail collision neighbours for active bodies independently of the view.
    /// Every protected payload consumes the ordinary cell, resident-byte and staging budgets.
    pub fn prepare_with_protected_cells(
        &self,
        position_xz_m: [f64; 2],
        protected: &[CellKey],
    ) -> Result<CellStreamPlan, CellStreamError> {
        if protected.len() > usize::from(self.policy.max_resident_cells) {
            return Err(CellStreamError::CellBudget);
        }
        let centre = self.policy.cell_at(position_xz_m)?;
        let mut desired = BTreeMap::new();
        self.include_neighbourhood(
            &mut desired,
            centre,
            self.policy.far_radius,
            self.policy.detail_radius,
        )?;
        for &cell in protected {
            if cell.x >= self.policy.dimensions[0] || cell.z >= self.policy.dimensions[1] {
                return Err(CellStreamError::OutsideGrid);
            }
            self.include_neighbourhood(&mut desired, cell, 1, 1)?;
        }
        self.plan_for(desired)
    }

    fn include_neighbourhood(
        &self,
        desired: &mut BTreeMap<CellKey, CellDetail>,
        centre: CellKey,
        radius: u16,
        full_radius: u16,
    ) -> Result<(), CellStreamError> {
        for z in centre.z.saturating_sub(radius)
            ..=centre
                .z
                .saturating_add(radius)
                .min(self.policy.dimensions[1] - 1)
        {
            for x in centre.x.saturating_sub(radius)
                ..=centre
                    .x
                    .saturating_add(radius)
                    .min(self.policy.dimensions[0] - 1)
            {
                let distance = x.abs_diff(centre.x).max(z.abs_diff(centre.z));
                desired.insert(
                    CellKey { x, z },
                    if distance <= full_radius
                        || desired.get(&CellKey { x, z }) == Some(&CellDetail::Full)
                    {
                        CellDetail::Full
                    } else {
                        CellDetail::Hlod
                    },
                );
                if desired.len() > usize::from(self.policy.max_resident_cells) {
                    return Err(CellStreamError::CellBudget);
                }
            }
        }
        Ok(())
    }

    fn plan_for(
        &self,
        desired: BTreeMap<CellKey, CellDetail>,
    ) -> Result<CellStreamPlan, CellStreamError> {
        let acquire: BTreeMap<_, _> = desired
            .iter()
            .filter(|&(key, detail)| {
                self.resident
                    .get(key)
                    .is_none_or(|cell| cell.detail != *detail)
            })
            .map(|(&key, &detail)| (key, detail))
            .collect();
        let retire = self
            .resident
            .keys()
            .filter(|key| !desired.contains_key(key))
            .copied()
            .collect();
        let retained = desired
            .keys()
            .filter(|key| !acquire.contains_key(key))
            .map(|key| self.resident[key].bytes);
        let incoming = sum_bytes(acquire.values().map(|&detail| self.policy.estimate(detail)))?;
        let retained_bytes = sum_bytes(retained)?;
        let resident_before_bytes = sum_bytes(self.resident.values().map(|cell| cell.bytes))?;
        let estimated_resident_bytes = sum_bytes([retained_bytes, incoming].into_iter())?;
        let estimated_peak_bytes = sum_bytes([resident_before_bytes, incoming].into_iter())?;
        self.check_bytes(estimated_resident_bytes, estimated_peak_bytes)?;
        Ok(CellStreamPlan {
            owner: self.owner,
            generation: self.generation,
            source: self.source,
            policy: self.policy.clone(),
            resident_before_bytes,
            retained_bytes,
            desired,
            acquire,
            retire,
            estimated_resident_bytes,
            estimated_peak_bytes,
        })
    }

    /// The adapter supplies precisely the prepared payloads. A refusal changes nothing.
    pub fn commit(
        &mut self,
        plan: &CellStreamPlan,
        loaded: &[LoadedCell],
    ) -> Result<(), CellStreamError> {
        if plan.owner != self.owner
            || plan.generation != self.generation
            || plan.source != self.source
        {
            return Err(CellStreamError::StalePlan);
        }
        let mut incoming = BTreeMap::new();
        for &cell in loaded {
            if cell.source != self.source
                || cell.bytes == 0
                || plan.acquire.get(&cell.key) != Some(&cell.detail)
                || incoming.insert(cell.key, cell).is_some()
            {
                return Err(CellStreamError::InvalidAcknowledgement);
            }
        }
        if incoming.len() != plan.acquire.len() {
            return Err(CellStreamError::InvalidAcknowledgement);
        }
        let mut next = BTreeMap::new();
        for &key in plan.desired.keys() {
            let cell = incoming
                .get(&key)
                .or_else(|| self.resident.get(&key))
                .copied()
                .ok_or(CellStreamError::InvalidAcknowledgement)?;
            next.insert(key, cell);
        }
        let actual_resident = sum_bytes(next.values().map(|cell| cell.bytes))?;
        let actual_peak = sum_bytes(
            self.resident
                .values()
                .chain(incoming.values())
                .map(|cell| cell.bytes),
        )?;
        self.check_bytes(actual_resident, actual_peak)?;
        let generation = self
            .generation
            .checked_add(1)
            .ok_or(CellStreamError::GenerationExhausted)?;
        self.resident = next;
        self.generation = generation;
        Ok(())
    }

    fn check_bytes(&self, resident: u64, peak: u64) -> Result<(), CellStreamError> {
        if resident > self.policy.max_resident_bytes || peak > self.policy.max_staging_bytes {
            Err(CellStreamError::ByteBudget)
        } else {
            Ok(())
        }
    }
}

fn sum_bytes(mut bytes: impl Iterator<Item = u64>) -> Result<u64, CellStreamError> {
    bytes
        .try_fold(0_u64, u64::checked_add)
        .ok_or(CellStreamError::ByteBudget)
}

#[derive(Clone, Copy, Debug, Error, PartialEq, Eq)]
pub enum CellStreamError {
    #[error("invalid bounded cell streaming policy")]
    InvalidPolicy,
    #[error("observer position is outside the finite cell grid")]
    OutsideGrid,
    #[error("cell residency exceeds the configured cell count")]
    CellBudget,
    #[error("cell residency or staging exceeds the configured byte budget")]
    ByteBudget,
    #[error("cell plan belongs to another session, source or generation")]
    StalePlan,
    #[error("loaded cells do not exactly acknowledge the prepared acquisitions")]
    InvalidAcknowledgement,
    #[error("cell generation is exhausted")]
    GenerationExhausted,
}
