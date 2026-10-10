use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, Vec3};
use forge_project::{
    authoring::new_terrain,
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp, ZoneAuthoringHead, ZoneBase, ZoneCorrections,
        migration::ZoneMigrationProposal,
        paths::{AuthoredPath, PathPoint},
        placements::{ManualPlacement, PlacementEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};
use forge_world::path_placement::{PathPlacementOptions, prepare};

fn fixture() -> ZoneAuthoringHead {
    let mut terrain = new_terrain("array", "zone", "Array").unwrap();
    terrain.height_sampling.base_elevation_m = 3.0;
    let placement = ManualPlacement {
        id: EntityId::deterministic("source"),
        name: "Synthetic panel".into(),
        asset_id: AssetId::deterministic("test-only-asset"),
        asset_hash: ContentHash::from_bytes([3; 32]),
        transform: Transform {
            translation_m: Vec3::new(20.0, 40.0, 10.0),
            scale: Vec3::new(2.0, 3.0, 4.0),
            ..Transform::IDENTITY
        },
        locked: true,
    };
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
            placements: Some(vec![placement]),
            paths: Some(vec![AuthoredPath {
                id: EntityId::deterministic("path"),
                name: "Path".into(),
                points: vec![
                    PathPoint { x_m: 0.0, z_m: 0.0 },
                    PathPoint { x_m: 3.0, z_m: 0.0 },
                    PathPoint { x_m: 3.0, z_m: 5.0 },
                ],
                width_m: 1.0,
                shoulder_m: 0.0,
                profile: None,
                locked: true,
            }]),
            ..Default::default()
        },
    }
}

fn options() -> PathPlacementOptions {
    PathPlacementOptions {
        spacing_m: 2.0,
        start_m: 0.0,
        lateral_m: 0.0,
        surface_offset_m: 0.5,
        yaw_offset_degrees: 0.0,
    }
}

fn proposal(
    head: &ZoneAuthoringHead,
    options: PathPlacementOptions,
) -> Result<ZoneRequest, String> {
    prepare(
        head,
        CommandId::deterministic("array"),
        EntityId::deterministic("source"),
        EntityId::deterministic("path"),
        options,
    )
}

fn copies(request: &ZoneRequest) -> Vec<ManualPlacement> {
    let ZoneAction::Edit(corrections) = &request.action else {
        panic!("edit expected")
    };
    corrections
        .placements
        .as_ref()
        .unwrap()
        .iter()
        .filter(|p| p.id != EntityId::deterministic("source"))
        .cloned()
        .collect()
}

#[test]
fn spacing_crosses_segments_without_reset_and_preserves_pinned_source() {
    let source = fixture();
    let before = source.to_bytes().unwrap();
    let request = proposal(&source, options()).unwrap();
    assert_eq!(request, proposal(&source, options()).unwrap());
    let copies = copies(&request);
    assert_eq!(copies.len(), 5);
    for expected in [[0.0, 0.0], [2.0, 0.0], [3.0, 1.0], [3.0, 3.0], [3.0, 5.0]] {
        let copy = copies
            .iter()
            .find(|p| {
                (p.transform.translation_m.x - expected[0]).abs() < 1e-6
                    && (p.transform.translation_m.z - expected[1]).abs() < 1e-6
            })
            .unwrap();
        assert_eq!(
            copy.asset_id,
            source.corrections.placements.as_ref().unwrap()[0].asset_id
        );
        assert_eq!(copy.asset_hash, ContentHash::from_bytes([3; 32]));
        assert_eq!(copy.transform.scale, Vec3::new(2.0, 3.0, 4.0));
        assert!(!copy.locked);
        assert!((copy.transform.translation_m.y - 3.5).abs() < 1e-5);
        let q = copy.transform.rotation;
        let axis = [
            1.0 - 2.0 * (q.y * q.y + q.z * q.z),
            2.0 * (q.x * q.z - q.y * q.w),
        ];
        let expected_direction = if expected[0] < 3.0 {
            [1.0, 0.0]
        } else {
            [0.0, 1.0]
        };
        assert!(
            (axis[0] - expected_direction[0]).abs() < 1e-5
                && (axis[1] - expected_direction[1]).abs() < 1e-5
        );
    }
    assert_eq!(source.to_bytes().unwrap(), before);
}

#[test]
fn offsets_corner_heading_and_edited_surface_are_explicit() {
    let mut source = fixture();
    source.corrections.height_deltas =
        Some(vec![forge_project::TerrainHeightDelta::new(129, 128, 2.0)]);
    let terrain =
        forge_world::TerrainGenerationPlan::from_document(&source.effective().unwrap().0).unwrap();
    let request = proposal(
        &source,
        PathPlacementOptions {
            start_m: 3.0,
            lateral_m: 1.0,
            yaw_offset_degrees: 90.0,
            ..options()
        },
    )
    .unwrap();
    let copies = copies(&request);
    assert_eq!(copies.len(), 3);
    for copy in copies {
        let p = copy.transform.translation_m;
        assert!((p.x - 2.0).abs() < 1e-5);
        assert!((p.y - terrain.surface_height_at(p.x, p.z).unwrap() - 0.5).abs() < 1e-5);
        let q = copy.transform.rotation;
        assert!((1.0 - 2.0 * (q.y * q.y + q.z * q.z) - 1.0).abs() < 1e-5); // +90 offset from +Z restores +X.
    }
}

#[test]
fn invalid_options_missing_sources_bounds_and_batch_collisions_are_atomic() {
    let source = fixture();
    let before = source.to_bytes().unwrap();
    for invalid in [
        PathPlacementOptions {
            spacing_m: 0.0,
            ..options()
        },
        PathPlacementOptions {
            start_m: 9.0,
            ..options()
        },
        PathPlacementOptions {
            lateral_m: f32::NAN,
            ..options()
        },
        PathPlacementOptions {
            surface_offset_m: 65.0,
            ..options()
        },
    ] {
        assert!(proposal(&source, invalid).is_err());
    }
    let mut long = source.clone();
    long.corrections.paths.as_mut().unwrap()[0].points = vec![
        PathPoint {
            x_m: -200.0,
            z_m: 0.0,
        },
        PathPoint {
            x_m: 200.0,
            z_m: 0.0,
        },
    ];
    assert!(
        proposal(
            &long,
            PathPlacementOptions {
                spacing_m: 0.1,
                ..options()
            }
        )
        .unwrap_err()
        .contains("256")
    );
    let mut edge = source.clone();
    edge.corrections.paths.as_mut().unwrap()[0].points = vec![
        PathPoint {
            x_m: 256.0,
            z_m: 0.0,
        },
        PathPoint {
            x_m: 256.0,
            z_m: 5.0,
        },
    ];
    assert!(
        proposal(
            &edge,
            PathPlacementOptions {
                lateral_m: -1.0,
                ..options()
            }
        )
        .is_err()
    );
    assert!(
        prepare(
            &source,
            CommandId::deterministic("missing"),
            EntityId::deterministic("absent"),
            EntityId::deterministic("path"),
            options()
        )
        .is_err()
    );
    let original = source.corrections.placements.as_ref().unwrap()[0].clone();
    for additions in [vec![], vec![original.clone()], vec![original; 257]] {
        assert!(
            source
                .prepare_placement(
                    CommandId::deterministic("bad-batch"),
                    PlacementEdit::InsertMany {
                        placements: additions
                    }
                )
                .is_err()
        );
    }
    assert_eq!(source.to_bytes().unwrap(), before);
}

#[test]
fn closed_loop_does_not_duplicate_first_copy_and_source_tilt_is_retained() {
    use forge_world::placement_transform::{Axis, RotationSpace, rotate};
    let mut source = fixture();
    source.corrections.paths.as_mut().unwrap()[0].points =
        [[0.0, 0.0], [4.0, 0.0], [4.0, 4.0], [0.0, 4.0], [0.0, 0.0]]
            .map(|p| PathPoint {
                x_m: p[0],
                z_m: p[1],
            })
            .to_vec();
    let placement = &mut source.corrections.placements.as_mut().unwrap()[0];
    placement.transform = rotate(placement.transform, Axis::Z, 30.0, RotationSpace::World).unwrap();
    placement.transform = rotate(placement.transform, Axis::Y, 90.0, RotationSpace::World).unwrap();
    let before = source.to_bytes().unwrap();
    let copies = copies(&proposal(&source, options()).unwrap());
    assert_eq!(copies.len(), 8);
    let origins = copies
        .iter()
        .filter(|p| {
            p.transform.translation_m.x.abs() < 1e-5 && p.transform.translation_m.z.abs() < 1e-5
        })
        .count();
    assert_eq!(origins, 1);
    for copy in copies {
        let q = copy.transform.rotation;
        assert!((2.0 * (q.x * q.y + q.z * q.w) - 0.5).abs() < 1e-5);
    }
    assert_eq!(source.to_bytes().unwrap(), before);
}

fn action(session: &ZoneEditSession, action: ZoneAction) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(format!("action-{}", session.audit().len())),
        expected_hash: session.head().content_hash().unwrap(),
        expected_revision: session.head().revision,
        action,
    }
}

#[test]
fn array_is_one_transaction_and_retouched_copy_survives_regeneration_and_all_archives() {
    let fixture = fixture();
    let legacy = TerrainEditSession::new(fixture.base.terrain.clone()).unwrap();
    let mut session = ZoneEditSession::from_migration(
        ZoneMigrationProposal::prepare(
            &legacy.archive_bytes().unwrap(),
            fixture.base.terrain.content_hash().unwrap(),
            fixture.base.generator,
            None,
        )
        .unwrap(),
    )
    .unwrap();
    session
        .execute(&action(&session, ZoneAction::edit(fixture.corrections)))
        .unwrap();
    let before = session.head().clone();
    let request = proposal(session.head(), options()).unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.audit().len(), 2);
    session
        .execute(&action(&session, ZoneAction::Undo))
        .unwrap();
    assert_eq!(session.head().corrections, before.corrections);
    session
        .execute(&action(&session, ZoneAction::Redo))
        .unwrap();
    let copy = copies(&request)[0].clone();
    let mut transform = copy.transform;
    transform.translation_m.y += 1.0;
    let change = session
        .head()
        .prepare_placement(
            CommandId::deterministic("manual-retouch"),
            PlacementEdit::Transform {
                id: copy.id,
                transform,
            },
        )
        .unwrap();
    session.execute(&change).unwrap();
    let accepted = session.head().corrections.clone();
    let mut base = session.head().base.clone();
    base.terrain.height_sampling.base_elevation_m += 5.0;
    session
        .execute(&action(&session, ZoneAction::Regenerate(Box::new(base))))
        .unwrap();
    assert_eq!(session.head().corrections, accepted);
    for bytes in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
    }
    let current = session.head().clone();
    let mut stale = request;
    stale.command_id = CommandId::deterministic("stale-array");
    assert!(session.execute(&stale).is_err());
    assert_eq!(session.head(), &current);
}
