use forge_core::EntityId;
use forge_project::{
    authoring::new_terrain,
    zone_authoring::paths::{AuthoredPath, PathPoint},
};
use forge_world::{VegetationGenerationPlan, editor_vegetation, path_vegetation::project};

fn path() -> AuthoredPath {
    AuthoredPath {
        id: EntityId::deterministic("cleared-path"),
        name: "Chemin".into(),
        width_m: 64.0,
        shoulder_m: 8.0,
        profile: None,
        locked: false,
        points: vec![
            PathPoint {
                x_m: -190.0,
                z_m: 0.0,
            },
            PathPoint {
                x_m: 190.0,
                z_m: 0.0,
            },
        ],
    }
}

#[test]
fn path_corridor_clears_generated_instances_without_editing_layers() {
    let terrain = new_terrain("path-test", "forest", "Forest").unwrap();
    let source = editor_vegetation::prepare(&terrain, 42).unwrap();
    let before = source.content_hash().unwrap();
    let derived = project(&source, &[path()]).unwrap();
    assert_eq!(derived.layers, source.layers);
    assert_eq!(source.content_hash().unwrap(), before);
    assert_eq!(project(&derived, &[]).unwrap(), derived);
    assert_ne!(derived.content_hash().unwrap(), before);
    let initial = VegetationGenerationPlan::from_documents(&terrain, &source)
        .unwrap()
        .generate()
        .unwrap();
    let cleared = VegetationGenerationPlan::from_documents(&terrain, &derived)
        .unwrap()
        .generate()
        .unwrap();
    let inside = |x: f32, z: f32| {
        let dx = x - x.clamp(-190.0, 190.0);
        dx * dx + z * z < 40.0 * 40.0
    };
    assert!(
        initial
            .chunks
            .iter()
            .flat_map(|c| &c.instances)
            .any(|p| inside(p.position_m.x, p.position_m.z))
    );
    assert!(
        !cleared
            .chunks
            .iter()
            .flat_map(|c| &c.instances)
            .any(|p| inside(p.position_m.x, p.position_m.z))
    );
    assert_eq!(project(&source, &[]).unwrap(), source);
}

#[test]
fn exclusion_collision_quota_and_invalid_geometry_are_not_silently_ignored() {
    let terrain = new_terrain("path-test", "bounds", "Bounds").unwrap();
    let source = editor_vegetation::prepare(&terrain, 42).unwrap();
    let derived = project(&source, &[path()]).unwrap();
    assert!(project(&derived, &[path()]).is_err());
    let paths = (0..33)
        .map(|i| {
            let mut p = path();
            p.id = EntityId::deterministic(format!("path-{i}"));
            p
        })
        .collect::<Vec<_>>();
    assert!(project(&source, &paths).is_err());
    let mut invalid = path();
    invalid.points[1].z_m = f32::NAN;
    assert!(project(&source, &[invalid]).is_err());
}

#[test]
fn miter_corner_is_cleared_without_changing_authored_masks() {
    let terrain = new_terrain("path-test", "bend", "Bend").unwrap();
    let source = editor_vegetation::prepare(&terrain, 42).unwrap();
    let before = source.content_hash().unwrap();
    let mut bend = path();
    bend.points = vec![
        PathPoint {
            x_m: -190.0,
            z_m: 0.0,
        },
        PathPoint { x_m: 0.0, z_m: 0.0 },
        PathPoint {
            x_m: 0.0,
            z_m: 190.0,
        },
    ];
    let derived = project(&source, &[bend]).unwrap();
    assert_eq!(source.content_hash().unwrap(), before);
    assert_eq!(source.layers, derived.layers);
    let forge_project::VegetationExclusion::Circle { radius_m, .. } =
        derived.exclusions.last().unwrap()
    else {
        panic!("missing miter clearance")
    };
    assert!((*radius_m - 40.0 * 2.0_f32.sqrt()).abs() < 0.001);
    let generated = VegetationGenerationPlan::from_documents(&terrain, &derived)
        .unwrap()
        .generate()
        .unwrap();
    assert!(
        !generated
            .chunks
            .iter()
            .flat_map(|c| &c.instances)
            .any(|p| { p.position_m.x.hypot(p.position_m.z) < *radius_m })
    );
}

#[test]
fn derived_paths_do_not_consume_the_authored_exclusion_quota() {
    use forge_project::{VegetationExclusion, VegetationExclusionId};
    let terrain = new_terrain("path-test", "separate-quota", "Quotas").unwrap();
    let mut source = editor_vegetation::prepare(&terrain, 42).unwrap();
    source.exclusions = (0..32)
        .map(|i| VegetationExclusion::Circle {
            id: VegetationExclusionId::new(format!("manual_{i}")),
            center_x_m: 0.0,
            center_z_m: 150.0,
            radius_m: 10.0,
        })
        .collect();
    let before = source.content_hash().unwrap();
    let mut paths = (0..64)
        .map(|i| {
            let mut path = path();
            path.id = EntityId::deterministic(format!("quota-path-{i}"));
            path
        })
        .collect::<Vec<_>>();
    paths.sort_by_key(|p| p.id);
    let plan =
        VegetationGenerationPlan::from_documents_with_paths(&terrain, &source, &paths).unwrap();
    let generated = plan.generate().unwrap();
    assert_eq!(plan.vegetation(), &source);
    assert_eq!(source.content_hash().unwrap(), before);
    assert_ne!(generated.vegetation_document_hash, before);
    assert_eq!(
        generated.vegetation_document_hash,
        forge_world::path_vegetation::prepare(&source, &paths)
            .unwrap()
            .source_hash()
    );
    let mut instances = generated.chunks.iter().flat_map(|c| &c.instances);
    assert!(instances.clone().next().is_some());
    assert!(instances.clone().all(|p| {
        let dx = p.position_m.x - p.position_m.x.clamp(-190.0, 190.0);
        dx * dx + p.position_m.z.powi(2) > 40.0 * 40.0
    }));
    assert!(instances.all(|p| p.position_m.x.hypot(p.position_m.z - 150.0) > 10.0));
    // Neither input quota nor duplicate identity checks were weakened.
    paths.push(path());
    paths.sort_by_key(|p| p.id);
    assert!(
        VegetationGenerationPlan::from_documents_with_paths(&terrain, &source, &paths).is_err()
    );
    assert!(
        VegetationGenerationPlan::from_documents_with_paths(&terrain, &source, &[path(), path()])
            .is_err()
    );
    source.exclusions.push(VegetationExclusion::Circle {
        id: VegetationExclusionId::new("manual_extra"),
        center_x_m: 0.0,
        center_z_m: 150.0,
        radius_m: 10.0,
    });
    assert!(VegetationGenerationPlan::from_documents_with_paths(&terrain, &source, &[]).is_err());
}

#[test]
fn absent_paths_preserve_generation_and_clearance_is_clipped_at_the_zone_edge() {
    let terrain = new_terrain("path-test", "edge", "Edge").unwrap();
    let source = editor_vegetation::prepare(&terrain, 42).unwrap();
    let baseline = VegetationGenerationPlan::from_documents(&terrain, &source)
        .unwrap()
        .generate()
        .unwrap();
    let empty = VegetationGenerationPlan::from_documents_with_paths(&terrain, &source, &[])
        .unwrap()
        .generate()
        .unwrap();
    assert_eq!(
        serde_json::to_value(&*baseline).unwrap(),
        serde_json::to_value(&*empty).unwrap()
    );
    let mut bend = path();
    bend.points = vec![
        PathPoint { x_m: 0.0, z_m: 0.0 },
        PathPoint {
            x_m: 230.0,
            z_m: 0.0,
        },
        PathPoint {
            x_m: 230.0,
            z_m: 190.0,
        },
    ];
    // A derived corner can extend beyond the zone; no off-zone instance exists.
    // It is not a newly authorized out-of-bounds manually authored exclusion.
    assert!(project(&source, &[bend.clone()]).is_err());
    let plan =
        VegetationGenerationPlan::from_documents_with_paths(&terrain, &source, &[bend]).unwrap();
    let generated = plan.generate().unwrap();
    assert!(
        generated
            .chunks
            .iter()
            .flat_map(|c| &c.instances)
            .all(|p| (p.position_m.x - 230.0).hypot(p.position_m.z) > 40.0 * 2.0_f32.sqrt())
    );
    assert_eq!(plan.vegetation(), &source);
}
