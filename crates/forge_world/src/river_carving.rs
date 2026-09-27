//! Pure earthwork proposal over an accepted terrain and an explicitly authored water line.
//! Not a persisted river feature: the zone must own both inputs before Studio integration.
use crate::TerrainGenerationPlan;
use forge_core::ContentHash;
use forge_project::{
    RiverElevationPolicy, RiverPathAlgorithm, TerrainDocument, TerrainHeightDelta, WaterDocument,
    terrain_edit::TerrainEdit,
};
use std::collections::BTreeMap;

#[derive(Clone, Debug, PartialEq)]
pub struct RiverCarvingPreview {
    pub terrain_hash: ContentHash,
    pub water_hash: ContentHash,
    pub edit: TerrainEdit,
    pub modified_samples: usize,
    pub deepest_cut_m: f32,
}

/// Cut only; never silently fill an existing depression. The optional limit is
/// reserved for callers that explicitly request one; Studio rivers use none.
/// Each linear segment supplies surface level, width and depth. At crossing
/// channels the lowest target wins. Junction water topology is not qualified here.
pub fn prepare(
    terrain: &TerrainDocument,
    water: &WaterDocument,
    maximum_cut_m: f32,
) -> Result<RiverCarvingPreview, String> {
    let preview = derive(terrain, water, Some(maximum_cut_m))?;
    if preview.modified_samples == 0 {
        return Err("River carving changes no terrain sample".into());
    }
    Ok(preview)
}

/// Projection also accepts an already deep natural bed, without inventing an edit.
pub(crate) fn derive(
    terrain: &TerrainDocument,
    water: &WaterDocument,
    maximum_cut_m: Option<f32>,
) -> Result<RiverCarvingPreview, String> {
    validate(terrain, water, maximum_cut_m)?;
    let plan = TerrainGenerationPlan::from_document(terrain).map_err(|e| e.to_string())?;
    let mut deltas: BTreeMap<_, _> = terrain
        .height_deltas
        .iter()
        .map(|p| (p.sort_key(), p.delta_m))
        .collect();
    let shoulder = water.banks.shoulder_width_m + water.banks.blend_width_m;
    let mut count = 0;
    let mut deepest = 0.0_f32;
    for z in 0_u16..=256 {
        for x in 0_u16..=256 {
            let wx = f32::from(x) * 2.0 - 256.0;
            let wz = f32::from(z) * 2.0 - 256.0;
            let height = plan.height_at(wx, wz).map_err(|e| e.to_string())?;
            let target = water
                .river
                .control_points
                .windows(2)
                .filter_map(|pair| target_height(wx, wz, height, pair[0], pair[1], shoulder))
                .fold(height, f32::min);
            let cut = height - target;
            if let Some(maximum_cut_m) = maximum_cut_m
                && cut > maximum_cut_m
            {
                return Err(format!(
                    "River cut {cut} m at ({wx}, {wz}) exceeds budget {maximum_cut_m} m"
                ));
            }
            if cut > 0.0 {
                let delta = deltas.get(&(z, x)).copied().unwrap_or(0.0) - cut;
                if delta == 0.0 {
                    deltas.remove(&(z, x));
                } else {
                    deltas.insert((z, x), delta);
                }
                count += 1;
                deepest = deepest.max(cut);
            }
        }
    }
    let samples: Vec<_> = deltas
        .into_iter()
        .map(|((z, x), delta)| TerrainHeightDelta::new(x, z, delta))
        .collect();
    let mut candidate = terrain.clone();
    candidate.height_deltas.clone_from(&samples);
    candidate.validate().map_err(|e| e.to_string())?;
    Ok(RiverCarvingPreview {
        terrain_hash: terrain.content_hash().map_err(|e| e.to_string())?,
        water_hash: water.content_hash().map_err(|e| e.to_string())?,
        edit: TerrainEdit::SetHeightDeltas { samples },
        modified_samples: count,
        deepest_cut_m: deepest,
    })
}

pub(crate) fn validate(
    terrain: &TerrainDocument,
    water: &WaterDocument,
    maximum_cut: Option<f32>,
) -> Result<(), String> {
    terrain.validate().map_err(|e| e.to_string())?;
    water.validate().map_err(|e| e.to_string())?;
    if water.project_id != terrain.project_id || water.area_id != terrain.area_id {
        return Err("River and terrain must belong to the same zone".into());
    }
    if water.river.algorithm != RiverPathAlgorithm::PiecewiseLinearV1
        || water.river.elevation_policy != RiverElevationPolicy::AuthoredSurfaceV1
    {
        return Err("Carving requires an explicit linear water surface profile".into());
    }
    if maximum_cut.is_some_and(|limit| !limit.is_finite() || !(0.001..=64.0).contains(&limit)) {
        return Err("Carving budget requires 0.001–64 m".into());
    }
    if water.river.control_points.len() > 128 {
        return Err("River carving supports at most 128 controls".into());
    }
    if water.river.control_points.iter().any(|p| {
        p.center_m.x.abs() > 256.0
            || p.center_m.z.abs() > 256.0
            || p.surface_width_m > 64.0
            || p.bed_depth_m > 64.0
    }) || water.banks.shoulder_width_m + water.banks.blend_width_m > 64.0
    {
        return Err("River carving exceeds the current terrain/corridor bounds".into());
    }
    if water
        .river
        .control_points
        .windows(2)
        .any(|p| (p[1].center_m.x - p[0].center_m.x).hypot(p[1].center_m.z - p[0].center_m.z) < 0.5)
    {
        return Err("River controls need horizontal separation of at least 0.5 m".into());
    }
    Ok(())
}

fn target_height(
    x: f32,
    z: f32,
    height: f32,
    a: forge_project::RiverControlPoint,
    b: forge_project::RiverControlPoint,
    shoulder: f32,
) -> Option<f32> {
    let dx = b.center_m.x - a.center_m.x;
    let dz = b.center_m.z - a.center_m.z;
    let fraction =
        (((x - a.center_m.x) * dx + (z - a.center_m.z) * dz) / (dx * dx + dz * dz)).clamp(0.0, 1.0);
    let distance = (x - a.center_m.x - fraction * dx).hypot(z - a.center_m.z - fraction * dz);
    let lerp = |start: f32, end: f32| start + (end - start) * fraction;
    let half = lerp(a.surface_width_m, b.surface_width_m) * 0.5;
    if distance >= half + shoulder {
        return None;
    }
    let surface = lerp(a.center_m.y, b.center_m.y);
    let depth = lerp(a.bed_depth_m, b.bed_depth_m);
    Some(if distance <= half {
        surface - depth * (1.0 - 0.75 * (distance / half).powi(2))
    } else {
        let blend = (distance - half) / shoulder;
        let edge = surface - depth * 0.25;
        edge + (height - edge) * blend * blend * (3.0 - 2.0 * blend)
    })
}
