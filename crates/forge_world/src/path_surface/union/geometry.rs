//! Convex clipping in X/Z. Clockwise polygons have upward-facing triangles.
pub(super) type Point = [f64; 2];
pub(super) type Plane = [f64; 3];

pub(super) fn value(plane: Plane, point: Point) -> f64 {
    plane[0] * point[0] + plane[1] * point[1] + plane[2]
}

pub(super) fn area(polygon: &[Point]) -> f64 {
    if polygon.len() < 3 {
        return 0.0;
    }
    polygon
        .windows(2)
        .skip(1)
        .map(|p| {
            (p[0][1] - polygon[0][1]) * (p[1][0] - polygon[0][0])
                - (p[0][0] - polygon[0][0]) * (p[1][1] - polygon[0][1])
        })
        .sum::<f64>()
        * 0.5
}

pub(super) fn edge(first: Point, second: Point) -> Plane {
    let dx = second[0] - first[0];
    let dz = second[1] - first[1];
    [dz, -dx, dx * first[1] - dz * first[0]]
}

/// Keep the positive half-plane, including its zero-area boundary.
pub(super) fn clip(polygon: &[Point], plane: Plane) -> Vec<Point> {
    let mut result = Vec::new();
    let Some(mut previous) = polygon.last().copied() else {
        return result;
    };
    let mut previous_side = value(plane, previous);
    for &current in polygon {
        let current_side = value(plane, current);
        if (previous_side >= 0.0) != (current_side >= 0.0) {
            let fraction = previous_side / (previous_side - current_side);
            result.push([
                previous[0] + fraction * (current[0] - previous[0]),
                previous[1] + fraction * (current[1] - previous[1]),
            ]);
        }
        if current_side >= 0.0 {
            result.push(current);
        }
        previous = current;
        previous_side = current_side;
    }
    result.dedup();
    if result.len() > 1 && result.first() == result.last() {
        result.pop();
    }
    result
}

pub(super) fn intersection(subject: &[Point], cutter: &[Point]) -> Vec<Point> {
    let mut result = subject.to_vec();
    for i in 0..cutter.len() {
        result = clip(&result, edge(cutter[i], cutter[(i + 1) % cutter.len()]));
        if result.is_empty() {
            break;
        }
    }
    result
}

/// Disjoint convex pieces outside a convex cutter; no fan across a hole.
pub(super) fn subtract(subject: &[Point], cutter: &[Point]) -> Vec<Vec<Point>> {
    if area(&intersection(subject, cutter)) <= 0.0 {
        return vec![subject.to_vec()];
    }
    let mut inside = subject.to_vec();
    let mut result = Vec::new();
    for i in 0..cutter.len() {
        let plane = edge(cutter[i], cutter[(i + 1) % cutter.len()]);
        let outside = clip(&inside, plane.map(|v| -v));
        if area(&outside) > 0.0 {
            result.push(outside);
        }
        inside = clip(&inside, plane);
        if inside.is_empty() {
            break;
        }
    }
    result
}

pub(super) fn alpha_plane(points: [Point; 3], alpha: [f64; 3]) -> Plane {
    let dx = points[1][0] - points[0][0];
    let dz = points[1][1] - points[0][1];
    let ex = points[2][0] - points[0][0];
    let ez = points[2][1] - points[0][1];
    let first = alpha[1] - alpha[0];
    let second = alpha[2] - alpha[0];
    let determinant = dx * ez - dz * ex;
    let x = (first * ez - second * dz) / determinant;
    let z = (dx * second - ex * first) / determinant;
    [x, z, alpha[0] - x * points[0][0] - z * points[0][1]]
}
