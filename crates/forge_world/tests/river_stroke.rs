use forge_core::Vec3;
use forge_project::RiverControlPoint;
use forge_world::river_stroke::prepare;

fn section() -> RiverControlPoint {
    RiverControlPoint {
        center_m: Vec3::new(100.0, -0.5, 200.0),
        surface_width_m: 6.0,
        bed_depth_m: 1.0,
    }
}

#[test]
fn straight_stroke_has_four_points_and_preserves_endpoints_and_section() {
    let template = section();
    let points = prepare(&[[0.0, 0.0], [20.0, 0.0]], &template).unwrap();
    assert_eq!(points.len(), 4);
    assert_eq!(
        points.first().unwrap().center_m.x.to_bits(),
        0.0_f32.to_bits()
    );
    assert_eq!(
        points.last().unwrap().center_m.x.to_bits(),
        20.0_f32.to_bits()
    );
    assert!(points.windows(2).all(|p| p[0].center_m.x < p[1].center_m.x));
    assert!(points.iter().all(|p| p.center_m.z == 0.0
        && p.center_m.y.to_bits() == template.center_m.y.to_bits()
        && p.surface_width_m.to_bits() == template.surface_width_m.to_bits()
        && p.bed_depth_m.to_bits() == template.bed_depth_m.to_bits()));
    assert_eq!(template, section());
    assert_eq!(
        points,
        prepare(&[[0.0, 0.0], [20.0, 0.0]], &template).unwrap()
    );
}

#[test]
fn bends_survive_and_serialization_preserves_the_draft() {
    let stroke = [[0.0, 0.0], [10.0, 0.0], [10.0, 10.0], [20.0, 10.0]];
    let points = prepare(&stroke, &section()).unwrap();
    assert_eq!(points.len(), 4);
    for (point, [x, z]) in points.iter().zip(stroke) {
        assert_eq!(point.center_m, Vec3::new(x, -0.5, z));
    }
    let encoded = serde_json::to_string(&points).unwrap();
    let decoded: Vec<RiverControlPoint> = serde_json::from_str(&encoded).unwrap();
    assert_eq!(decoded, points);
}

#[test]
fn invalid_or_unrepresentable_strokes_and_sections_are_rejected() {
    for stroke in [
        vec![],
        vec![[0.0, 0.0]],
        vec![[0.0, 0.0]; 2],
        vec![[0.0, 0.0], [f32::NAN, 1.0]],
        vec![[0.0, 0.0], [0.001, 0.0]],
        vec![[0.0, 0.0]; 1025],
    ] {
        assert!(prepare(&stroke, &section()).is_err());
    }
    for invalid in [0.0, -1.0, f32::NAN, f32::INFINITY] {
        let mut template = section();
        template.surface_width_m = invalid;
        assert!(prepare(&[[0.0, 0.0], [20.0, 0.0]], &template).is_err());
        template = section();
        template.bed_depth_m = invalid;
        assert!(prepare(&[[0.0, 0.0], [20.0, 0.0]], &template).is_err());
    }
    let mut template = section();
    template.center_m.y = f32::NAN;
    assert!(prepare(&[[0.0, 0.0], [20.0, 0.0]], &template).is_err());
}
