//! Repeat a pinned manual object along an accepted path, in one normal transaction.
//! Copies stay independently editable; later path/terrain edits never move them silently.
use forge_core::{CommandId, EntityId, Transform, Vec3};
use forge_project::zone_authoring::{
    ZoneAuthoringHead,
    paths::AuthoredPath,
    placements::{MAX_PLACEMENT_BATCH, PlacementEdit},
    session::ZoneRequest,
};
use serde::Serialize;

#[derive(Clone, Copy, Debug, Serialize)]
pub struct PathPlacementOptions {
    /// Horizontal arc distance, not 3D slope length. No forced final copy.
    pub spacing_m: f32,
    pub start_m: f32,
    /// Signed offset along the X/Z normal `(-direction_z, direction_x)`, before terrain projection.
    pub lateral_m: f32,
    /// Pivot height above effective terrain, including authored river carving.
    pub surface_offset_m: f32,
    /// Adjust local +X heading while preserving source tilt and scale.
    pub yaw_offset_degrees: f32,
}

impl PathPlacementOptions {
    fn validate(self) -> Result<(), String> {
        if !self.spacing_m.is_finite()
            || !(0.1..=128.0).contains(&self.spacing_m)
            || !self.start_m.is_finite()
            || self.start_m < 0.0
            || !self.lateral_m.is_finite()
            || self.lateral_m.abs() > 64.0
            || !self.surface_offset_m.is_finite()
            || self.surface_offset_m.abs() > 64.0
            || !self.yaw_offset_degrees.is_finite()
        {
            return Err(
                "Répétition : pas 0,1–128 m, départ positif et décalages ±64 m finis requis."
                    .into(),
            );
        }
        Ok(())
    }
}

/// Return absolute poses along an accepted path without choosing an authoring
/// storage domain. Studio can feed these poses through its active raster
/// alignment and then submit common `PlacementEdit::Duplicate` commands for
/// either a manual placement or a sparse imported source object.
pub fn transforms(
    head: &ZoneAuthoringHead,
    source: Transform,
    path_id: EntityId,
    options: PathPlacementOptions,
) -> Result<Vec<Transform>, String> {
    options.validate()?;
    let path = head
        .corrections
        .paths
        .as_deref()
        .unwrap_or_default()
        .iter()
        .find(|path| path.id == path_id)
        .ok_or("Tracé absent")?;
    poses(path, source, options)
}

pub fn prepare(
    head: &ZoneAuthoringHead,
    command_id: CommandId,
    source_id: EntityId,
    path_id: EntityId,
    options: PathPlacementOptions,
) -> Result<ZoneRequest, String> {
    options.validate()?;
    let hash = head.content_hash()?;
    let source = head
        .corrections
        .placements
        .as_deref()
        .unwrap_or_default()
        .iter()
        .find(|p| p.id == source_id)
        .ok_or("Objet source absent")?;
    let terrain = crate::river_projection::project(head)?.terrain;
    let terrain =
        crate::TerrainGenerationPlan::from_document(&terrain).map_err(|e| e.to_string())?;
    let poses = transforms(head, source.transform, path_id, options)?;
    let key = serde_json::to_string(&(hash, command_id, source_id, path_id, options))
        .map_err(|e| e.to_string())?;
    let copies = poses
        .into_iter()
        .enumerate()
        .map(|(index, pose)| {
            let mut copy = source.clone();
            copy.id = EntityId::deterministic(format!("path-placement:{key}:{index}"));
            copy.locked = false;
            copy.transform = crate::placement_snap::PlacementSnap {
                grid_step_m: None,
                surface_offset_m: Some(options.surface_offset_m),
            }
            .resolve(pose, &terrain)?;
            Ok(copy)
        })
        .collect::<Result<Vec<_>, String>>()?;
    head.prepare_placement(command_id, PlacementEdit::InsertMany { placements: copies })
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss,
    reason = "finite positions bounded by path validation and a maximum of 256 copies"
)]
fn poses(
    path: &AuthoredPath,
    source: Transform,
    options: PathPlacementOptions,
) -> Result<Vec<Transform>, String> {
    let lengths: Vec<_> = path
        .points
        .windows(2)
        .map(|p| {
            (f64::from(p[1].x_m) - f64::from(p[0].x_m))
                .hypot(f64::from(p[1].z_m) - f64::from(p[0].z_m))
        })
        .collect();
    if lengths.is_empty() || lengths.iter().any(|length| *length < 0.001) {
        return Err("Répétition : segments d'au moins 1 mm requis.".into());
    }
    let total: f64 = lengths.iter().sum();
    let start = f64::from(options.start_m);
    if start > total {
        return Err("Le départ est après la fin du tracé.".into());
    }
    let mut count = ((total - start) / f64::from(options.spacing_m)).floor() + 1.0;
    // A closed loop starting at zero must not place its first module twice.
    let last = (count - 1.0) * f64::from(options.spacing_m);
    if start == 0.0
        && path.points.first() == path.points.last()
        && count > 1.0
        && (last - total).abs() <= f64::EPSILON * total
    {
        count -= 1.0;
    }
    if count > MAX_PLACEMENT_BATCH as f64 {
        return Err("Répétition : plus de 256 copies, augmenter le pas.".into());
    }
    let q = source.rotation;
    let axis_x = 1.0 - 2.0 * (q.y * q.y + q.z * q.z);
    let axis_z = 2.0 * (q.x * q.z - q.y * q.w);
    if axis_x.hypot(axis_z) < 0.001 {
        return Err("Axe X source vertical : réorienter l'objet avant de le répéter.".into());
    }
    let source_yaw = (-axis_z).atan2(axis_x).to_degrees();
    let mut result = Vec::new();
    let mut segment = 0;
    let mut accumulated = 0.0;
    for index in 0..count as usize {
        let distance = start + index as f64 * f64::from(options.spacing_m);
        // At an exact corner, use the outgoing segment, including for lateral offset.
        while segment + 1 < lengths.len() && distance >= accumulated + lengths[segment] {
            accumulated += lengths[segment];
            segment += 1;
        }
        let first = path.points[segment];
        let second = path.points[segment + 1];
        let dx = (f64::from(second.x_m) - f64::from(first.x_m)) / lengths[segment];
        let dz = (f64::from(second.z_m) - f64::from(first.z_m)) / lengths[segment];
        let along = distance - accumulated;
        let lateral = f64::from(options.lateral_m);
        let mut transform = crate::placement_transform::rotate(
            source,
            crate::placement_transform::Axis::Y,
            (-dz).atan2(dx).to_degrees() as f32 - source_yaw + options.yaw_offset_degrees % 360.0,
            crate::placement_transform::RotationSpace::World,
        )?;
        transform.translation_m = Vec3::new(
            (f64::from(first.x_m) + along * dx - lateral * dz) as f32,
            0.0,
            (f64::from(first.z_m) + along * dz + lateral * dx) as f32,
        );
        result.push(transform);
    }
    Ok(result)
}
