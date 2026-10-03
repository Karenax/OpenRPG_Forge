//! Behavioural regressions for the lab-only Rerecast polygon conversion.
//!
//! These tests deliberately exercise the public `ContourSet` conversion used by the
//! campaign. They preserve the canonical contour tuples and reject degenerate input instead
//! of repairing or silently dropping it.

use bevy_math::U16Vec3;
use bevy_rerecast::rerecast::{Contour, ContourSet, PolygonNavmesh};

fn contour(points: &[(u16, u16)]) -> Contour {
    Contour {
        vertices: points
            .iter()
            .enumerate()
            .map(|(index, &(x, z))| {
                (
                    U16Vec3::new(x, 0, z),
                    0x1000 + u32::try_from(index).expect("bounded fixture indices fit u32"),
                )
            })
            .collect(),
        ..Contour::default()
    }
}

fn convert(points: &[(u16, u16)]) -> Result<PolygonNavmesh, String> {
    ContourSet {
        contours: vec![contour(points)],
        ..ContourSet::default()
    }
    .into_polygon_mesh(3)
    .map_err(|error| error.to_string())
}

fn triangle_area2(mesh: &PolygonNavmesh, polygon: &[u16]) -> i64 {
    let a = mesh.vertices[polygon[0] as usize];
    let b = mesh.vertices[polygon[1] as usize];
    let c = mesh.vertices[polygon[2] as usize];
    (i64::from(b.x) - i64::from(a.x)) * (i64::from(c.z) - i64::from(a.z))
        - (i64::from(c.x) - i64::from(a.x)) * (i64::from(b.z) - i64::from(a.z))
}

fn assert_triangle_mesh(mesh: &PolygonNavmesh, expected_count: usize) -> i64 {
    assert_eq!(mesh.polygon_count(), expected_count);
    assert_eq!(mesh.regions.len(), expected_count);
    assert_eq!(mesh.areas.len(), expected_count);
    let mut sign = None;
    for polygon in mesh.polygons() {
        let polygon = polygon.collect::<Vec<_>>();
        assert_eq!(polygon.len(), 3);
        assert!(
            polygon
                .iter()
                .all(|index| (*index as usize) < mesh.vertices.len())
        );
        assert!(polygon[0] != polygon[1] && polygon[0] != polygon[2] && polygon[1] != polygon[2]);
        let area = triangle_area2(mesh, &polygon);
        assert_ne!(area, 0, "degenerate polygon emitted: {polygon:?}");
        let next_sign = area.signum();
        if let Some(previous_sign) = sign {
            assert_eq!(next_sign, previous_sign, "mixed winding in one contour");
        } else {
            sign = Some(next_sign);
        }
    }
    sign.expect("expected at least one polygon")
}

#[test]
fn cw_and_ccw_contours_have_identical_valid_triangle_contracts() {
    let ccw = convert(&[(0, 0), (10, 0), (10, 10), (0, 10)]).expect("CCW contour");
    let cw = convert(&[(0, 0), (0, 10), (10, 10), (10, 0)]).expect("CW contour");

    let original_orientation = assert_triangle_mesh(&ccw, 2);
    let reversed_orientation = assert_triangle_mesh(&cw, 2);
    assert_eq!(original_orientation, reversed_orientation);
    let canonical_points = |mesh: &PolygonNavmesh| {
        let mut points = mesh
            .vertices
            .iter()
            .map(U16Vec3::to_array)
            .collect::<Vec<_>>();
        points.sort_unstable();
        points
    };
    assert_eq!(canonical_points(&ccw), canonical_points(&cw));
    let covered_area = |mesh: &PolygonNavmesh| {
        mesh.polygons()
            .map(|polygon| triangle_area2(mesh, &polygon.collect::<Vec<_>>()).abs())
            .sum::<i64>()
    };
    assert_eq!(covered_area(&ccw), 200);
    assert_eq!(covered_area(&cw), 200);
}

#[test]
fn concave_contour_preserves_indices_and_area_metadata() {
    let mesh = convert(&[(0, 0), (10, 0), (10, 10), (5, 5), (0, 10)]).expect("concave contour");
    assert_triangle_mesh(&mesh, 3);
}

#[test]
fn zero_area_and_self_crossing_contours_are_rejected_without_fallback() {
    for points in [
        &[(0, 0), (4, 0), (8, 0), (12, 0)][..],
        &[(0, 0), (10, 10), (0, 10), (10, 0)][..],
    ] {
        let error = convert(points).expect_err("degenerate contour must be rejected");
        assert!(
            error.starts_with("Invalid contour."),
            "unexpected refusal: {error}"
        );
    }
}

#[test]
fn inner_loop_reconstruction_keeps_each_boundary_valid_and_deterministic() {
    let set = ContourSet {
        contours: vec![
            contour(&[(0, 0), (20, 0), (20, 20), (0, 20)]),
            contour(&[(5, 5), (15, 5), (15, 15), (5, 15)]),
        ],
        ..ContourSet::default()
    };
    let first = set
        .clone()
        .into_polygon_mesh(3)
        .expect("outer and inner loops");
    let second = set
        .into_polygon_mesh(3)
        .expect("repeat outer and inner loops");

    assert_eq!(first, second);
    assert_triangle_mesh(&first, 4);
}
