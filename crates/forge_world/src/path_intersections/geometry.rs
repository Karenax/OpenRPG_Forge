use forge_project::zone_authoring::paths::PathPoint;

type Crossing = Option<(f64, f64, PathPoint)>;

fn vector(a: PathPoint, b: PathPoint) -> [f64; 2] {
    [
        f64::from(b.x_m) - f64::from(a.x_m),
        f64::from(b.z_m) - f64::from(a.z_m),
    ]
}

fn cross(a: [f64; 2], b: [f64; 2]) -> f64 {
    a[0] * b[1] - a[1] * b[0]
}

/// No proximity tolerance: nearby routes are never snapped or connected.
#[allow(
    clippy::cast_possible_truncation,
    reason = "intersection stays inside validated f32 endpoints"
)]
#[allow(
    clippy::float_cmp,
    reason = "only exact endpoints are reused; a tolerance would silently snap nearby paths"
)]
pub(super) fn crossing(
    a: PathPoint,
    b: PathPoint,
    c: PathPoint,
    d: PathPoint,
) -> Result<Crossing, String> {
    let first_direction = vector(a, b);
    let second_direction = vector(c, d);
    let offset = vector(a, c);
    let denominator = cross(first_direction, second_direction);
    if denominator == 0.0 {
        if cross(offset, first_direction) == 0.0 {
            let axis = usize::from(first_direction[1].abs() > first_direction[0].abs());
            if first_direction[axis] == 0.0 {
                return Err("Segment de longueur nulle.".into());
            }
            let t0 = offset[axis] / first_direction[axis];
            let t1 = vector(a, d)[axis] / first_direction[axis];
            if t0.min(t1).max(0.0) < t0.max(t1).min(1.0) {
                return Err("Segments colinéaires superposés : ajuster les tracés avant de créer les croisements.".into());
            }
        }
        return Ok(None);
    }
    let ta = cross(offset, second_direction) / denominator;
    let tb = cross(offset, first_direction) / denominator;
    if !(0.0..=1.0).contains(&ta) || !(0.0..=1.0).contains(&tb) {
        return Ok(None);
    }
    // Preserve a T-junction endpoint bit-for-bit instead of recomputing it.
    let point = if ta == 0.0 {
        a
    } else if ta == 1.0 {
        b
    } else if tb == 0.0 {
        c
    } else if tb == 1.0 {
        d
    } else {
        PathPoint {
            x_m: (f64::from(a.x_m) + ta * first_direction[0]) as f32,
            z_m: (f64::from(a.z_m) + ta * first_direction[1]) as f32,
        }
    };
    Ok(Some((ta, tb, point)))
}
