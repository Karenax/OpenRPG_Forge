use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, Vec3};
use forge_project::{
    authoring::new_terrain,
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp,
        migration::ZoneMigrationProposal,
        placements::{ManualPlacement, PlacementEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};
use forge_world::placement_transform::{Axis, RotationSpace, drag_xz, rotate, set_scale};

fn execute(session: &mut ZoneEditSession, key: &str, action: ZoneAction) {
    session
        .execute(&ZoneRequest {
            command_id: CommandId::deterministic(key),
            expected_revision: session.head().revision,
            expected_hash: session.head().content_hash().unwrap(),
            action,
        })
        .unwrap();
}

#[test]
fn rotation_scale_preserve_history_lock_and_regeneration_contracts() {
    exercise_transform_history(false);
}

#[test]
fn local_numeric_and_gesture_snap_preserve_history_lock_and_regeneration() {
    exercise_transform_history(true);
}

fn assert_local_snap_matches_gesture(changed: Transform) -> Transform {
    use forge_world::placement_axis::{ray_parameter, translate_local};
    // Rays already converted into the initial interaction frame by the adapter.
    let start = ray_parameter(
        Vec3::ZERO,
        Axis::X,
        Vec3::new(1.0, 2.0, 0.0),
        Vec3::new(0.0, -1.0, 0.0),
    )
    .unwrap();
    let end = ray_parameter(
        Vec3::ZERO,
        Axis::X,
        Vec3::new(2.3, 2.0, 0.0),
        Vec3::new(0.0, -1.0, 0.0),
    )
    .unwrap();
    let numeric = translate_local(changed, Axis::X, 1.3, Some(0.5)).unwrap();
    let gesture = translate_local(changed, Axis::X, end - start, Some(0.5)).unwrap();
    assert_eq!(numeric, gesture);
    numeric
}

fn new_fixture_session() -> ZoneEditSession {
    let terrain = new_terrain("transform-session", "zone", "Zone").unwrap();
    let hash = terrain.content_hash().unwrap();
    let legacy = TerrainEditSession::new(terrain).unwrap();
    ZoneEditSession::from_migration(
        ZoneMigrationProposal::prepare(
            &legacy.archive_bytes().unwrap(),
            hash,
            GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: hash,
            },
            None,
        )
        .unwrap(),
    )
    .unwrap()
}

fn exercise_transform_history(local_snap: bool) {
    let mut session = new_fixture_session();
    let id = EntityId::deterministic("object");
    let original = Transform::from_translation(Vec3::new(3.0, 4.0, 5.0));
    let insert = session
        .head()
        .prepare_placement(
            CommandId::deterministic("insert"),
            PlacementEdit::Insert {
                placement: ManualPlacement {
                    physics: None,
                    id,
                    name: "Fixture".into(),
                    asset_id: AssetId::deterministic("fixture"),
                    asset_hash: ContentHash::from_bytes([1; 32]),
                    transform: original,
                    locked: false,
                },
            },
        )
        .unwrap();
    session.execute(&insert).unwrap();
    let changed = set_scale(
        rotate(original, Axis::Z, -37.0, RotationSpace::Local).unwrap(),
        Vec3::new(0.5, 2.0, 3.0),
    )
    .unwrap();
    let changed = drag_xz(changed, [20.0, 30.0], [22.0, 29.0]).unwrap();
    assert_eq!(changed.translation_m, Vec3::new(5.0, 4.0, 4.0));
    let changed = if local_snap {
        assert_local_snap_matches_gesture(changed)
    } else {
        changed
    };
    let before = session.head().clone();
    let request = before
        .prepare_placement(
            CommandId::deterministic("transform"),
            PlacementEdit::Transform {
                id,
                transform: changed,
            },
        )
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    execute(&mut session, "undo", ZoneAction::Undo);
    assert_eq!(session.head().corrections, before.corrections);
    execute(&mut session, "redo", ZoneAction::Redo);
    assert_eq!(
        session.head().corrections.placements.as_ref().unwrap()[0].transform,
        changed
    );
    let lock = session
        .head()
        .prepare_placement(
            CommandId::deterministic("lock"),
            PlacementEdit::SetLocked { id, locked: true },
        )
        .unwrap();
    session.execute(&lock).unwrap();
    assert!(
        session
            .head()
            .prepare_placement(
                CommandId::deterministic("forbidden"),
                PlacementEdit::Transform {
                    id,
                    transform: original
                }
            )
            .is_err()
    );
    let mut regenerated = session.head().base.clone();
    regenerated.terrain.seed += 1;
    execute(
        &mut session,
        "regenerate",
        ZoneAction::Regenerate(Box::new(regenerated)),
    );
    let reopened =
        ZoneEditSession::from_archive_bytes(&session.delta_archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
    assert_eq!(reopened.audit(), session.audit());
    assert_eq!(
        reopened.head().corrections.placements.as_ref().unwrap()[0].transform,
        changed
    );
}
