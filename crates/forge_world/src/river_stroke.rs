//! Terrain-picked river drafts. No mutation or automatic terrain-level sampling.
use forge_core::Vec3;
use forge_project::RiverControlPoint;

/// Replace draft X/Z with a simplified stroke, keeping the first point's absolute
/// level and section. Full river/terrain conflict validation still happens at review.
pub fn prepare(
    stroke: &[[f32; 2]],
    section: &RiverControlPoint,
) -> Result<Vec<RiverControlPoint>, String> {
    if !section.center_m.y.is_finite()
        || !section.surface_width_m.is_finite()
        || section.surface_width_m <= 0.0
        || !section.bed_depth_m.is_finite()
        || section.bed_depth_m <= 0.0
    {
        return Err("Niveau fini, largeur et profondeur positives requis".into());
    }
    let mut points = crate::path_stroke::simplify(stroke, 1.0)?;
    // Water recipes require at least four points, even for a straight line.
    // Subdivide the longest segment without changing its shape or endpoints.
    while points.len() < 4 {
        let segment = points
            .windows(2)
            .enumerate()
            .max_by(|(_, a), (_, b)| length_squared(a).total_cmp(&length_squared(b)))
            .map(|(index, _)| index)
            .ok_or("Tracé sans segment")?;
        points = crate::path_stroke::insert_midpoint(&points, segment)?;
    }
    Ok(points
        .into_iter()
        .map(|[x, z]| RiverControlPoint {
            center_m: Vec3::new(x, section.center_m.y, z),
            surface_width_m: section.surface_width_m,
            bed_depth_m: section.bed_depth_m,
        })
        .collect())
}

fn length_squared(pair: &[[f32; 2]]) -> f64 {
    (f64::from(pair[1][0]) - f64::from(pair[0][0])).powi(2)
        + (f64::from(pair[1][1]) - f64::from(pair[0][1])).powi(2)
}
