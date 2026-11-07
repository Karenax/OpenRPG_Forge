//! Admission of derived renderer inputs, independently of any source dataset.
use super::{SourceMaterial, Vegetation};
use std::collections::BTreeSet;

const MAX_GROUPS: usize = 256;
const MAX_LEVELS: usize = 8;
const MAX_INSTANCES: usize = forge_project::imported_vegetation::MAX_IMPORTED_INSTANCES as usize
    + forge_project::zone_authoring::imported_plants::MAX_IMPORTED_PLANT_PLACEMENTS;

impl Vegetation {
    /// Validate caller-decoded streams before asset or entity allocation. Source
    /// package schemas, provenance and reference population guards stay in codecs.
    pub fn validate(&self) -> Result<(), String> {
        if self.groups.len() > MAX_GROUPS {
            return Err("Source vegetation group budget exceeded".into());
        }
        let mut total = 0_usize;
        let mut identities = BTreeSet::new();
        for group in &self.groups {
            total = total
                .checked_add(group.records.len())
                .ok_or("Plant count overflow")?;
            if total > MAX_INSTANCES
                || group.records.len() != group.models.len()
                || group.records.len() != group.query_radii.len()
                || group.identities.as_ref().is_some_and(|ids| {
                    ids.len() != group.records.len()
                        || ids
                            .iter()
                            .any(|id| id.as_uuid().is_nil() || !identities.insert(*id))
                })
                || group.levels.len() > MAX_LEVELS
                || (!group.records.is_empty() && group.levels.is_empty())
                || !group.query_radius.is_finite()
                || group.query_radius < 0.0
                || !group.cell_size.is_finite()
                || group.cell_size <= 0.0
                || !group.near_range.is_finite()
                || group.near_range < 0.0
                || !group.refresh_distance.is_finite()
                || group.refresh_distance < 0.0
            {
                return Err("Invalid or over-budget source vegetation streams/policy".into());
            }
            if group.records.iter().any(|record| {
                !record.i_pos.is_finite() || !record.i_dat.is_finite() || record.i_pos.w <= 0.0
            }) || group.query_radii.iter().any(|r| !r.is_finite() || *r < 0.0)
                || group.models.iter().any(|matrix| {
                    !matrix.is_finite()
                        || !matrix.determinant().is_finite()
                        || matrix.determinant().abs() < 1.0e-12
                })
                || group.levels.iter().any(|level| {
                    let d = &level.description;
                    d.capacity == 0
                        || d.capacity > MAX_INSTANCES
                        || d.range.iter().any(|value| !value.is_finite())
                        || d.range[0] < 0.0
                        || d.range[0] >= d.range[1]
                        || d.range[1] >= d.range[2]
                })
            {
                return Err("Invalid source vegetation geometry inputs or LOD".into());
            }
        }
        Ok(())
    }

    pub(super) fn validate_material<M: SourceMaterial>(&self) -> Result<(), String> {
        self.validate()?;
        for group in &self.groups {
            for level in &group.levels {
                if level.description.kind > M::MAX_KIND {
                    return Err("Plant source kind is unsupported by this material".into());
                }
                M::validate_records(level.description.kind, &group.records)?;
            }
        }
        Ok(())
    }
}
