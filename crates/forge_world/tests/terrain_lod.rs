use forge_world::terrain_lod::{
    CdlodConfig, CdlodError, CdlodNode, CdlodRoot, CdlodView, same_node_geometry, select_nodes,
};

#[test]
fn opaque_geometry_reuses_permutations_but_preserves_patch_multiplicity() {
    let a = CdlodNode {
        origin_xz: [0.0, -8.0],
        size_m: 8.0,
        lod: 0,
    };
    let b = CdlodNode {
        origin_xz: [8.0, -8.0],
        ..a
    };
    let original = [a, b, a];
    assert!(same_node_geometry(&original, &[b, a, a]));
    assert_eq!(original, [a, b, a]);
    assert!(!same_node_geometry(&original, &[b, a, b]));
    assert!(!same_node_geometry(&original, &[a, b]));
    assert!(same_node_geometry(&[], &[]));
}

#[test]
fn opaque_geometry_invalidates_each_vertex_input_and_rejects_nonfinite_nodes() {
    let node = CdlodNode {
        origin_xz: [0.0, -8.0],
        size_m: 8.0,
        lod: 0,
    };
    for changed in [
        CdlodNode {
            origin_xz: [1.0, -8.0],
            ..node
        },
        CdlodNode {
            origin_xz: [0.0, -7.0],
            ..node
        },
        CdlodNode {
            size_m: 16.0,
            ..node
        },
        CdlodNode { lod: 1, ..node },
        CdlodNode {
            origin_xz: [-0.0, -8.0],
            ..node
        },
    ] {
        assert!(!same_node_geometry(&[node], &[changed]));
    }
    for invalid in [
        CdlodNode {
            origin_xz: [f64::NAN, -8.0],
            ..node
        },
        CdlodNode {
            size_m: f64::INFINITY,
            ..node
        },
    ] {
        assert!(!same_node_geometry(&[invalid], &[invalid]));
    }
}

fn config() -> CdlodConfig {
    CdlodConfig {
        grid_size: 40,
        leaf_size_m: 8.0,
        levels: 2,
        range_factor: 1.0,
        morph_start_ratio: 0.66,
        max_nodes: 32,
    }
}
fn root() -> CdlodRoot {
    CdlodRoot::Fixed {
        origin_xz: [0.0; 2],
        size_m: 16.0,
    }
}
fn view() -> CdlodView {
    CdlodView {
        position: [0.0; 3],
        planes: [[0.0; 4]; 6],
    }
}

#[test]
#[allow(clippy::float_cmp)] // Integer lattice positions must be exactly preserved.
fn mixed_quarters_keep_parent_lattice_and_stable_tie_order() {
    let nodes = select_nodes(config(), root(), view(), |_, _| Ok([0.0; 2])).unwrap();
    assert_eq!(nodes.len(), 4);
    assert_eq!(
        nodes.iter().map(|n| n.lod).collect::<Vec<_>>(),
        [0, 0, 0, 1]
    );
    // Equal-distance quarters retain source traversal order.
    assert_eq!(nodes[1].origin_xz, [8.0, 0.0]);
    assert_eq!(nodes[2].origin_xz, [0.0, 8.0]);
    assert_eq!(nodes[3].origin_xz, [8.0, 8.0]);
    assert_eq!(nodes[3].size_m.to_bits(), 8.0_f64.to_bits());
}

#[test]
fn invisible_children_do_not_reappear_as_parent_fallback() {
    let mut v = view();
    v.planes[0] = [-1.0, 0.0, 0.0, 7.0]; // x <= 7
    let nodes = select_nodes(config(), root(), v, |_, _| Ok([0.0; 2])).unwrap();
    assert_eq!(nodes.len(), 2);
    assert!(
        nodes
            .iter()
            .all(|n| n.origin_xz[0].to_bits() == 0.0_f64.to_bits())
    );
}

#[test]
fn capacity_and_invalid_input_fail_instead_of_partial_success() {
    let mut c = config();
    c.max_nodes = 1;
    assert_eq!(
        select_nodes(c, root(), view(), |_, _| Ok([0.0; 2])),
        Err(CdlodError::Capacity)
    );
    c = config();
    c.leaf_size_m = f64::NAN;
    assert_eq!(
        select_nodes(c, root(), view(), |_, _| Ok([0.0; 2])),
        Err(CdlodError::Config)
    );
    let mut v = view();
    v.planes[2][1] = f64::INFINITY;
    assert_eq!(
        select_nodes(config(), root(), v, |_, _| Ok([0.0; 2])),
        Err(CdlodError::View)
    );
    assert_eq!(
        select_nodes(config(), root(), view(), |_, _| Ok([1.0, -1.0])),
        Err(CdlodError::Bounds)
    );
}

#[test]
fn rectangular_domain_selects_only_intersecting_patches_and_covers_its_interior() {
    let mut c = config();
    c.range_factor = 100.0;
    let origin = [-12.0, -4.0];
    let size = [23.0, 11.0];
    let end = [origin[0] + size[0], origin[1] + size[1]];
    let root = CdlodRoot::FixedRectangle {
        origin_xz: origin,
        size_xz: size,
    };
    let nodes = select_nodes(c, root, view(), |_, _| Ok([0.0; 2])).unwrap();
    assert_eq!(nodes.len(), 6);
    assert!(
        nodes
            .iter()
            .all(|node| { node.origin_xz[0] < end[0] && node.origin_xz[1] < end[1] })
    );
    // Every cell centre belongs to exactly one patch; edge patches are still
    // square and require clipping by the consumer at the rectangle boundary.
    for z in 0..11 {
        for x in 0..23 {
            let point = [
                origin[0] + f64::from(x) + 0.5,
                origin[1] + f64::from(z) + 0.5,
            ];
            assert_eq!(
                nodes
                    .iter()
                    .filter(|node| {
                        (0..2).all(|axis| {
                            point[axis] >= node.origin_xz[axis]
                                && point[axis] < node.origin_xz[axis] + node.size_m
                        })
                    })
                    .count(),
                1,
                "uncovered or overlapping point {point:?}"
            );
        }
    }
    assert!(
        nodes
            .iter()
            .any(|node| node.origin_xz[0] + node.size_m > end[0])
    );
    assert!(
        nodes
            .iter()
            .any(|node| node.origin_xz[1] + node.size_m > end[1])
    );
}

#[test]
fn rectangular_square_preserves_reference_selection_and_order() {
    let fixed = CdlodRoot::Fixed {
        origin_xz: [-16.0; 2],
        size_m: 32.0,
    };
    let rectangle = CdlodRoot::FixedRectangle {
        origin_xz: [-16.0; 2],
        size_xz: [32.0; 2],
    };
    let old = select_nodes(config(), fixed, view(), |_, _| Ok([0.0; 2])).unwrap();
    let new = select_nodes(config(), rectangle, view(), |_, _| Ok([0.0; 2])).unwrap();
    assert_eq!(old, new);
}

#[test]
fn rectangular_outside_children_never_become_range_fallbacks() {
    let end = [11.0, 7.0];
    let rectangle = CdlodRoot::FixedRectangle {
        origin_xz: [-12.0, -4.0],
        size_xz: [23.0, 11.0],
    };
    let nodes = select_nodes(config(), rectangle, view(), |origin, _| {
        assert!(origin[0] < end[0] && origin[1] < end[1]);
        Ok([0.0; 2])
    })
    .unwrap();
    assert!(!nodes.is_empty());
    assert!(
        nodes
            .iter()
            .all(|node| { node.origin_xz[0] < end[0] && node.origin_xz[1] < end[1] })
    );
}

#[test]
fn positive_subnormal_rectangle_does_not_silently_lose_its_root() {
    let root = CdlodRoot::FixedRectangle {
        origin_xz: [0.0; 2],
        size_xz: [f64::from_bits(1); 2],
    };
    let nodes = select_nodes(config(), root, view(), |_, _| Ok([0.0; 2])).unwrap();
    assert_eq!(nodes.len(), 1);
    assert_eq!(nodes[0].origin_xz.map(f64::to_bits), [0.0_f64.to_bits(); 2]);
}

#[test]
fn rectangular_domain_rejects_invalid_bounds_and_preserves_capacity_errors() {
    for (origin_xz, size_xz) in [
        ([0.0, 0.0], [0.0, 1.0]),
        ([0.0, 0.0], [1.0, f64::NAN]),
        ([f64::INFINITY, 0.0], [1.0, 1.0]),
        ([1e9, 0.0], [1.0, 1.0]),
        ([0.0, 0.0], [257.0, 1.0]),
        ([1.0, 0.0], [f64::MIN_POSITIVE, 1.0]),
    ] {
        assert_eq!(
            select_nodes(
                config(),
                CdlodRoot::FixedRectangle { origin_xz, size_xz },
                view(),
                |_, _| Ok([0.0; 2])
            ),
            Err(CdlodError::Root)
        );
    }
    let mut c = config();
    c.max_nodes = 1;
    assert_eq!(
        select_nodes(
            c,
            CdlodRoot::FixedRectangle {
                origin_xz: [0.0; 2],
                size_xz: [16.0, 8.0]
            },
            view(),
            |_, _| Ok([0.0; 2])
        ),
        Err(CdlodError::Capacity)
    );
}
