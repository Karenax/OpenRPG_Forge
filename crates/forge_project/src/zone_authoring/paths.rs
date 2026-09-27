//! Persistent manually authored paths. Generated meshes are never stored here.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use forge_core::{CommandId, EntityId};
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PathPoint {
    pub x_m: f32,
    pub z_m: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AuthoredPath {
    pub id: EntityId,
    pub name: String,
    pub points: Vec<PathPoint>,
    pub width_m: f32,
    pub shoulder_m: f32,
    /// Optional authored vertical road profile. Omission keeps the historical terrain drape.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub profile: Option<PathVerticalProfileV1>,
    pub locked: bool,
}

/// Absolute centre elevations and crossfall for a path's existing X/Z control points.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PathVerticalProfileV1 {
    pub elevations_m: Vec<f32>,
    /// Signed rise/run across the path: positive rises toward the left of travel.
    pub crossfall_ratio: f32,
    /// Maximum permitted absolute difference between the authored centre and terrain, in metres.
    pub maximum_cut_fill_m: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum PathEdit {
    Insert { path: AuthoredPath },
    Replace { path: AuthoredPath },
    SetLocked { id: EntityId, locked: bool },
    Remove { id: EntityId },
}

/// Validate a complete, ID-sorted path collection against an already validated terrain extent.
pub fn validate(paths: &[AuthoredPath], extent: crate::TerrainExtent) -> Result<(), String> {
    if paths.len() > 64 {
        return Err("Path limit exceeded (64)".into());
    }
    let mut previous = None;
    for path in paths {
        if previous.is_some_and(|id| id >= path.id) {
            return Err("Paths require unique sorted IDs".into());
        }
        previous = Some(path.id);
        if path.name.trim().is_empty() || path.name.len() > 128 {
            return Err("Path name must be nonempty and bounded".into());
        }
        if !(0.1..=64.0).contains(&path.width_m) || !(0.0..=32.0).contains(&path.shoulder_m) {
            return Err("Invalid path width or shoulder".into());
        }
        if !(2..=128).contains(&path.points.len()) {
            return Err("Path requires 2 to 128 control points".into());
        }
        for point in &path.points {
            if !point.x_m.is_finite()
                || !point.z_m.is_finite()
                || point.x_m.abs() > extent.size_x_m * 0.5
                || point.z_m.abs() > extent.size_z_m * 0.5
            {
                return Err("Path point outside terrain or nonfinite".into());
            }
        }
        if path.points.windows(2).any(|p| p[0] == p[1]) {
            return Err("Adjacent path points must differ".into());
        }
        if let Some(profile) = &path.profile {
            validate_profile(path, profile)?;
        }
    }
    Ok(())
}

fn validate_profile(path: &AuthoredPath, profile: &PathVerticalProfileV1) -> Result<(), String> {
    if profile.elevations_m.len() != path.points.len()
        || profile.elevations_m.iter().any(|value| !value.is_finite())
        || !profile.crossfall_ratio.is_finite()
        || !profile.maximum_cut_fill_m.is_finite()
    {
        return Err("Path profile requires finite elevations for every control point".into());
    }
    if !(-0.25..=0.25).contains(&profile.crossfall_ratio)
        || !(0.0..=32.0).contains(&profile.maximum_cut_fill_m)
    {
        return Err("Path profile crossfall or cut/fill limit is outside bounds".into());
    }
    for (segment, points) in path.points.windows(2).enumerate() {
        let distance = (points[1].x_m - points[0].x_m).hypot(points[1].z_m - points[0].z_m);
        let grade = (profile.elevations_m[segment + 1] - profile.elevations_m[segment]) / distance;
        if !grade.is_finite() || !(-0.25..=0.25).contains(&grade) {
            return Err("Path profile grade exceeds 25%".into());
        }
    }
    Ok(())
}

pub(super) fn validate_locks(
    before: &[AuthoredPath],
    after: &[AuthoredPath],
) -> Result<(), String> {
    for old in before.iter().filter(|p| p.locked) {
        let new = after
            .iter()
            .find(|p| p.id == old.id)
            .ok_or("Unlock path before removing it")?;
        let mut permitted = old.clone();
        permitted.locked = new.locked;
        if &permitted != new {
            return Err("Unlock path before changing it".into());
        }
    }
    Ok(())
}

impl ZoneAuthoringHead {
    pub fn prepare_path(
        &self,
        command_id: CommandId,
        edit: PathEdit,
    ) -> Result<ZoneRequest, String> {
        let expected_hash = self.content_hash()?;
        let mut corrections = self.corrections.clone();
        let paths = corrections.paths.get_or_insert_with(Vec::new);
        match edit {
            PathEdit::Insert { path } => {
                if paths.iter().any(|p| p.id == path.id) {
                    return Err("Path ID already exists".into());
                }
                paths.push(path);
                paths.sort_by_key(|p| p.id);
            }
            PathEdit::Replace { path } => {
                let target = find(paths, path.id)?;
                *target = path;
            }
            PathEdit::SetLocked { id, locked } => {
                find(paths, id)?.locked = locked;
            }
            PathEdit::Remove { id } => {
                find(paths, id)?;
                paths.retain(|p| p.id != id);
            }
        }
        validate(paths, self.base.terrain.extent_m)?;
        validate_locks(self.corrections.paths.as_deref().unwrap_or_default(), paths)?;
        Ok(ZoneRequest {
            command_id,
            expected_hash,
            expected_revision: self.revision,
            action: ZoneAction::edit(corrections),
        })
    }
}

fn find(paths: &mut [AuthoredPath], id: EntityId) -> Result<&mut AuthoredPath, String> {
    paths
        .iter_mut()
        .find(|p| p.id == id)
        .ok_or_else(|| "Unknown path ID".into())
}
