//! Bounded simplification of terrain-picked strokes into editable control points.
//! No document mutation. Simplification preserves sampled coordinates; explicit
//! segment subdivision interpolates a new editable point without moving its neighbours.

/// Translate the complete draft in world metres. No document or input mutation;
/// terrain bounds and locks are checked by the normal path-command preflight.
pub fn translate(points: &[[f32; 2]], offset: [f32; 2]) -> Result<Vec<[f32; 2]>, String> {
    if !(2..=128).contains(&points.len())
        || points
            .iter()
            .flatten()
            .chain(offset.iter())
            .any(|v| !v.is_finite())
    {
        return Err("Translation : 2–128 points et décalage finis requis".into());
    }
    let result: Vec<_> = points
        .iter()
        .map(|p| [p[0] + offset[0], p[1] + offset[1]])
        .collect();
    if result.iter().flatten().any(|v| !v.is_finite())
        || result
            .windows(2)
            .any(|p| distance_squared(p[0], p[1], p[1]) < 0.000_001)
    {
        return Err("Translation impossible : dépassement ou segments inférieurs à 1 mm".into());
    }
    if result == points {
        return Err("Translation sans effet à cette précision".into());
    }
    Ok(result)
}

/// Subdivide one draft segment at its midpoint. Acceptance still uses the normal
/// path command (bounds, locks, geometry preflight, preview and inverse).
pub fn insert_midpoint(points: &[[f32; 2]], segment: usize) -> Result<Vec<[f32; 2]>, String> {
    if !(2..128).contains(&points.len()) || points.iter().flatten().any(|v| !v.is_finite()) {
        return Err("Insertion requise : 2–127 points finis, maximum 128 après insertion".into());
    }
    let pair = points
        .windows(2)
        .nth(segment)
        .ok_or("Segment de chemin absent")?;
    // Standard midpoint avoids overflow; reject rounding to an endpoint below.
    let midpoint = [0, 1].map(|axis| pair[0][axis].midpoint(pair[1][axis]));
    if pair
        .iter()
        .any(|&endpoint| distance_squared(midpoint, endpoint, endpoint) < 0.000_001)
    {
        return Err(
            "Segment trop court ou précision insuffisante pour deux segments de 1 mm".into(),
        );
    }
    let mut result = points.to_vec();
    result.insert(segment + 1, midpoint);
    Ok(result)
}

pub fn simplify(points: &[[f32; 2]], tolerance_m: f32) -> Result<Vec<[f32; 2]>, String> {
    if !(2..=1024).contains(&points.len())
        || !tolerance_m.is_finite()
        || !(0.0..=64.0).contains(&tolerance_m)
        || points.iter().flatten().any(|v| !v.is_finite())
    {
        return Err("Tracé requis : 2–1024 points finis et tolérance 0–64 m".into());
    }
    let mut kept = vec![false; points.len()];
    kept[0] = true;
    kept[points.len() - 1] = true;
    let mut count = 2;
    let mut pending = vec![(0, points.len() - 1)];
    let threshold = f64::from(tolerance_m).powi(2);
    while let Some((first, last)) = pending.pop() {
        let mut farthest = None;
        let mut greatest = threshold;
        for index in first + 1..last {
            let distance = distance_squared(points[index], points[first], points[last]);
            if distance > greatest {
                greatest = distance;
                farthest = Some(index);
            }
        }
        if let Some(index) = farthest {
            kept[index] = true;
            count += 1;
            if count > 128 {
                return Err("Tracé trop complexe : plus de 128 points après simplification".into());
            }
            pending.push((index, last));
            pending.push((first, index));
        }
    }
    let result: Vec<_> = points
        .iter()
        .zip(kept)
        .filter_map(|(&p, keep)| keep.then_some(p))
        .collect();
    if result
        .windows(2)
        .any(|pair| distance_squared(pair[0], pair[1], pair[1]) < 0.000_001)
    {
        return Err("Tracé sans longueur suffisante : espacer les points d'au moins 1 mm".into());
    }
    Ok(result)
}

fn distance_squared(p: [f32; 2], a: [f32; 2], b: [f32; 2]) -> f64 {
    let [px, pz] = p.map(f64::from);
    let [ax, az] = a.map(f64::from);
    let [bx, bz] = b.map(f64::from);
    let dx = bx - ax;
    let dz = bz - az;
    let length = dx * dx + dz * dz;
    let t = if length == 0.0 {
        0.0
    } else {
        (((px - ax) * dx + (pz - az) * dz) / length).clamp(0.0, 1.0)
    };
    (px - ax - t * dx).powi(2) + (pz - az - t * dz).powi(2)
}
