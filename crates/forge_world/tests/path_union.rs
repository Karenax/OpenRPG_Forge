use forge_core::EntityId;
use forge_project::{
    authoring::new_terrain,
    zone_authoring::{
        GeneratorStamp, ZoneAuthoringHead, ZoneBase, ZoneCorrections,
        paths::{AuthoredPath, PathPoint, PathVerticalProfileV1},
    },
};
use forge_world::path_surface::{PathSurfaceProjection, project};

fn path(name: &str, coordinates: &[[f32; 2]]) -> AuthoredPath {
    AuthoredPath {
        id: EntityId::deterministic(name),
        name: name.into(),
        points: coordinates
            .iter()
            .map(|p| PathPoint {
                x_m: p[0],
                z_m: p[1],
            })
            .collect(),
        width_m: 2.0,
        shoulder_m: 1.0,
        profile: None,
        locked: false,
    }
}

fn profiled_path(
    name: &str,
    coordinates: &[[f32; 2]],
    elevations_m: Vec<f32>,
    crossfall_ratio: f32,
) -> AuthoredPath {
    let mut path = path(name, coordinates);
    path.profile = Some(PathVerticalProfileV1 {
        elevations_m,
        crossfall_ratio,
        maximum_cut_fill_m: 32.0,
    });
    path
}

fn head(mut paths: Vec<AuthoredPath>) -> ZoneAuthoringHead {
    paths.sort_by_key(|p| p.id);
    let terrain = new_terrain("union", "roads", "Roads").unwrap();
    ZoneAuthoringHead {
        schema: "forge.zone-authoring".into(),
        version: 3,
        revision: terrain.revision,
        base: ZoneBase {
            generator: GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: terrain.content_hash().unwrap(),
            },
            terrain,
            vegetation: None,
        },
        corrections: ZoneCorrections {
            paths: Some(paths),
            ..Default::default()
        },
    }
}

fn cross(a: [f64; 2], b: [f64; 2]) -> f64 {
    a[0] * b[1] - a[1] * b[0]
}
fn sub(a: [f64; 2], b: [f64; 2]) -> [f64; 2] {
    [a[0] - b[0], a[1] - b[1]]
}

fn coverage(projection: &PathSurfaceProjection, point: [f64; 2]) -> Vec<f64> {
    let mut hits = Vec::new();
    for mesh in &projection.meshes {
        for indices in mesh.indices.as_chunks::<3>().0 {
            let p = indices.map(|i| {
                let v = mesh.positions[i as usize];
                [f64::from(v[0]), f64::from(v[2])]
            });
            let first = sub(p[1], p[0]);
            let second = sub(p[2], p[0]);
            let offset = sub(point, p[0]);
            let determinant = cross(first, second);
            let b = cross(offset, second) / determinant;
            let c = cross(first, offset) / determinant;
            let a = 1.0 - b - c;
            // Samples deliberately avoid shared edges; count strict interiors only.
            if a > 1e-8 && b > 1e-8 && c > 1e-8 {
                hits.push(
                    [a, b, c]
                        .into_iter()
                        .zip(indices)
                        .map(|(w, i)| w * f64::from(mesh.colors[*i as usize][3]))
                        .sum(),
                );
            }
        }
    }
    hits
}

fn area(projection: &PathSurfaceProjection) -> f64 {
    projection
        .meshes
        .iter()
        .map(|mesh| {
            mesh.indices
                .as_chunks::<3>()
                .0
                .iter()
                .map(|indices| {
                    let p = indices.map(|i| {
                        let v = mesh.positions[i as usize];
                        [f64::from(v[0]), f64::from(v[2])]
                    });
                    let area = -cross(sub(p[1], p[0]), sub(p[2], p[0])) * 0.5;
                    assert!(area > 0.0);
                    area
                })
                .sum::<f64>()
        })
        .sum()
}

fn verify_union(paths: Vec<AuthoredPath>, extent: i16) -> PathSurfaceProjection {
    let source = head(paths.clone());
    let before = source.to_bytes().unwrap();
    let merged = project(&source).unwrap();
    assert_eq!(source.to_bytes().unwrap(), before);
    assert_eq!(merged, project(&source).unwrap());
    let singles: Vec<_> = paths
        .into_iter()
        .map(|p| project(&head(vec![p])).unwrap())
        .collect();
    for x in -extent..extent {
        for z in -extent..extent {
            let point = [f64::from(x) + 0.317, f64::from(z) + 0.193];
            let expected = singles
                .iter()
                .flat_map(|p| coverage(p, point))
                .fold(0.0, f64::max);
            let hits = coverage(&merged, point);
            assert!(hits.len() <= 1, "overlap at {point:?}: {hits:?}");
            let actual = hits.first().copied().unwrap_or(0.0);
            assert!(
                (actual - expected).abs() < 1e-5,
                "coverage at {point:?}: {actual} vs {expected}"
            );
        }
    }
    merged
}

fn newly_clipped_positions(
    merged: &PathSurfaceProjection,
    single: &PathSurfaceProjection,
    id: EntityId,
) -> Vec<[f32; 3]> {
    let merged = merged.meshes.iter().find(|mesh| mesh.id == id).unwrap();
    let single = single.meshes.iter().find(|mesh| mesh.id == id).unwrap();
    merged
        .positions
        .iter()
        .copied()
        .filter(|position| {
            !single.positions.iter().any(|source| {
                source[0].to_bits() == position[0].to_bits()
                    && source[2].to_bits() == position[2].to_bits()
            })
        })
        .collect()
}

fn assert_profiled_positions(positions: &[[f32; 3]], height: impl Fn(f32, f32) -> f32) {
    assert!(!positions.is_empty());
    for position in positions {
        assert!(
            (position[1] - height(position[0], position[2])).abs() < 0.0001,
            "profiled vertex {position:?} did not retain its source face"
        );
    }
}

#[test]
fn x_and_t_junctions_have_union_area_and_maximum_shoulder_coverage() {
    let east = path("east", &[[-10.0, 0.0], [10.0, 0.0]]);
    let north = path("north", &[[0.0, -10.0], [0.0, 10.0]]);
    let merged = verify_union(vec![east.clone(), north], 12);
    assert!((area(&merged) - 144.0).abs() < 1e-5); // 80 + 80 - 4*4.
    let tee = path("tee", &[[0.0, 0.0], [0.0, 10.0]]);
    assert!((area(&verify_union(vec![east, tee], 12)) - 112.0).abs() < 1e-5);
}

#[test]
fn oblique_different_width_and_collinear_overlaps_have_single_coverage() {
    let mut wide = path("wide", &[[-12.0, -4.0], [12.0, 4.0]]);
    wide.width_m = 5.0;
    wide.shoulder_m = 2.0;
    let narrow = path("narrow", &[[-5.0, 12.0], [5.0, -12.0]]);
    verify_union(vec![wide, narrow], 15);
    let first = path("a", &[[-10.0, 0.0], [5.0, 0.0]]);
    let second = path("b", &[[-5.0, 0.0], [10.0, 0.0]]);
    assert!((area(&verify_union(vec![first, second], 12)) - 80.0).abs() < 1e-5);
}

#[test]
fn identical_roads_and_self_crossing_do_not_double_render() {
    let first = path("a", &[[-10.0, 0.0], [10.0, 0.0]]);
    let mut second = first.clone();
    second.id = EntityId::deterministic("b");
    assert!((area(&verify_union(vec![first, second], 12)) - 80.0).abs() < 1e-5);
    let loop_path = path(
        "loop",
        &[[-10.0, -10.0], [10.0, 10.0], [-10.0, 10.0], [10.0, -10.0]],
    );
    let merged = project(&head(vec![loop_path])).unwrap();
    assert_eq!(coverage(&merged, [0.317, 0.193]).len(), 1);
}

#[test]
fn intersection_vertices_follow_edited_terrain_and_preserve_manual_sources() {
    let mut source = head(vec![
        path("a", &[[-10.0, 0.0], [10.0, 0.0]]),
        path("b", &[[-5.0, -10.0], [5.0, 10.0]]),
    ]);
    source.corrections.height_deltas =
        Some(vec![forge_project::TerrainHeightDelta::new(128, 128, 2.0)]);
    let before = source.to_bytes().unwrap();
    let terrain =
        forge_world::TerrainGenerationPlan::from_document(&source.effective().unwrap().0).unwrap();
    let projected = project(&source).unwrap();
    for mesh in projected.meshes {
        for index in mesh.indices {
            let p = mesh.positions[index as usize];
            assert!((p[1] - terrain.surface_height_at(p[0], p[2]).unwrap() - 0.02).abs() < 1e-5);
        }
    }
    assert_eq!(source.to_bytes().unwrap(), before);
}

#[test]
fn profiled_x_junction_interpolates_new_vertices_without_reapplying_the_lift() {
    let east = profiled_path("east", &[[-10.0, 0.0], [10.0, 0.0]], vec![1.0, 3.0], 0.1);
    let north = profiled_path("north", &[[0.0, -10.0], [0.0, 10.0]], vec![4.0, 2.0], -0.15);
    let source = head(vec![east.clone(), north.clone()]);
    let before = source.to_bytes().unwrap();
    let merged = verify_union(vec![east.clone(), north.clone()], 12);
    let east_single = project(&head(vec![east.clone()])).unwrap();
    let north_single = project(&head(vec![north.clone()])).unwrap();
    let east_new = newly_clipped_positions(&merged, &east_single, east.id);
    let north_new = newly_clipped_positions(&merged, &north_single, north.id);
    assert_profiled_positions(&east_new, |x, z| 2.0 + 0.1 * x + 0.1 * z + 0.02);
    assert_profiled_positions(&north_new, |x, z| 3.0 + 0.15 * x - 0.1 * z + 0.02);
    assert!(merged.meshes.iter().all(|mesh| mesh.profiled));
    assert_eq!(source.to_bytes().unwrap(), before);
    assert_eq!(merged, project(&source).unwrap());
}

#[test]
fn profiled_t_junction_interpolates_each_source_surface_and_crossfall() {
    let east = profiled_path("east", &[[-10.0, 0.0], [10.0, 0.0]], vec![2.0, 4.0], -0.1);
    let branch = profiled_path("branch", &[[0.0, 0.0], [0.0, 10.0]], vec![5.0, 6.0], 0.2);
    let source = head(vec![east.clone(), branch.clone()]);
    let before = source.to_bytes().unwrap();
    let merged = verify_union(vec![east.clone(), branch.clone()], 12);
    let east_single = project(&head(vec![east.clone()])).unwrap();
    let branch_single = project(&head(vec![branch.clone()])).unwrap();
    let east_new = newly_clipped_positions(&merged, &east_single, east.id);
    let branch_new = newly_clipped_positions(&merged, &branch_single, branch.id);
    assert_profiled_positions(&east_new, |x, z| 3.0 + 0.1 * x - 0.1 * z + 0.02);
    assert_profiled_positions(&branch_new, |x, z| 5.0 - 0.2 * x + 0.1 * z + 0.02);
    assert_eq!(source.to_bytes().unwrap(), before);
    assert_eq!(merged, project(&source).unwrap());
}
