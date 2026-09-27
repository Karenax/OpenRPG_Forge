use forge_world::path_stroke::simplify;

#[test]
fn translation_moves_all_points_and_refuses_invalid_results() {
    use forge_world::path_stroke::translate;
    let points = [[-10.0, 2.0], [10.0, 6.0], [20.0, 18.0]];
    let moved = translate(&points, [4.0, -2.0]).unwrap();
    assert_eq!(moved, [[-6.0, 0.0], [14.0, 4.0], [24.0, 16.0]]);
    assert_eq!(translate(&moved, [-4.0, 2.0]).unwrap(), points);
    assert_eq!(points, [[-10.0, 2.0], [10.0, 6.0], [20.0, 18.0]]);
    for offset in [
        [0.0, 0.0],
        [f32::NAN, 0.0],
        [f32::INFINITY, 0.0],
        [f32::MAX, f32::MAX],
    ] {
        assert!(translate(&points, offset).is_err());
    }
    assert!(translate(&[], [1.0, 0.0]).is_err());
    assert!(translate(&[[0.0, 0.0]; 129], [1.0, 0.0]).is_err());
    assert!(translate(&[[f32::MAX, 0.0], [0.0, 0.0]], [f32::MAX, 1.0]).is_err());
}

#[test]
fn subdivision_preserves_neighbours_and_is_deterministic() {
    use forge_world::path_stroke::insert_midpoint;
    let points = [[-10.0, 2.0], [10.0, 6.0], [20.0, 18.0]];
    assert_eq!(
        insert_midpoint(&points, 0).unwrap(),
        vec![points[0], [0.0, 4.0], points[1], points[2]]
    );
    assert_eq!(
        insert_midpoint(&points, 1).unwrap(),
        vec![points[0], points[1], [15.0, 12.0], points[2]]
    );
    assert_eq!(points, [[-10.0, 2.0], [10.0, 6.0], [20.0, 18.0]]);
    assert_eq!(
        insert_midpoint(&points, 0).unwrap(),
        insert_midpoint(&points, 0).unwrap()
    );
}

#[test]
fn subdivision_rejects_invalid_or_unrepresentable_points_without_partial_result() {
    use forge_world::path_stroke::insert_midpoint;
    for points in [
        vec![],
        vec![[0.0, 0.0]],
        vec![[0.0, 0.0]; 128],
        vec![[0.0, 0.0], [f32::NAN, 0.0]],
        vec![[0.0, 0.0], [0.001, 0.0]],
        vec![[f32::MAX, 0.0], [f32::MAX, 0.0]],
    ] {
        assert!(insert_midpoint(&points, 0).is_err());
    }
    let points = [[0.0, 0.0], [10.0, 0.0]];
    for index in [1, 2, usize::MAX] {
        assert!(insert_midpoint(&points, index).is_err());
    }
    let long: Vec<_> = (0_u16..127).map(|i| [f32::from(i), 0.0]).collect();
    let subdivided = insert_midpoint(&long, 125).unwrap();
    assert_eq!(subdivided.len(), 128);
    assert_eq!(
        subdivided[126].map(f32::to_bits),
        [125.5_f32, 0.0].map(f32::to_bits)
    );
    assert_eq!(
        subdivided[127].map(f32::to_bits),
        long[126].map(f32::to_bits)
    );
    let huge = [[f32::MAX * 0.5, 0.0], [f32::MAX, 0.0]];
    assert!(
        insert_midpoint(&huge, 0)
            .unwrap()
            .iter()
            .flatten()
            .all(|v| v.is_finite())
    );
}

#[test]
fn straight_stroke_keeps_exact_endpoints_and_does_not_mutate_input() {
    let points: Vec<_> = (0_u16..=1000).map(|i| [f32::from(i) * 0.1, 0.0]).collect();
    let before = points.clone();
    assert_eq!(
        simplify(&points, 1.0).unwrap(),
        vec![points[0], points[1000]]
    );
    assert_eq!(points, before);
}

#[test]
fn corners_and_small_deviations_are_deterministic() {
    let points = [
        [0.0, 0.0],
        [5.0, 0.2],
        [10.0, 0.0],
        [10.2, 5.0],
        [10.0, 10.0],
    ];
    let result = simplify(&points, 1.0).unwrap();
    assert_eq!(result, vec![points[0], points[2], points[4]]);
    assert_eq!(result, simplify(&points, 1.0).unwrap());
    assert_eq!(simplify(&points, 0.0).unwrap(), points);
    // A reversal must not collapse merely because the three points are collinear.
    assert_eq!(
        simplify(&[[0.0, 0.0], [10.0, 0.0], [1.0, 0.0]], 1.0).unwrap(),
        vec![[0.0, 0.0], [10.0, 0.0], [1.0, 0.0]]
    );
}

#[test]
fn invalid_and_overcomplex_strokes_are_rejected_without_partial_result() {
    for points in [
        vec![],
        vec![[0.0, 0.0]],
        vec![[0.0, 0.0]; 2],
        vec![[0.0, 0.0]; 1025],
        vec![[0.0, 0.0], [f32::NAN, 1.0]],
    ] {
        assert!(simplify(&points, 1.0).is_err());
    }
    for tolerance in [-1.0, 65.0, f32::NAN, f32::INFINITY] {
        assert!(simplify(&[[0.0, 0.0], [1.0, 0.0]], tolerance).is_err());
    }
    let zigzag: Vec<_> = (0_u16..129)
        .map(|i| [f32::from(i), if i % 2 == 0 { 0.0 } else { 10.0 }])
        .collect();
    assert!(simplify(&zigzag, 0.0).unwrap_err().contains("128"));
}
