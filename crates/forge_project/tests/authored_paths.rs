use forge_core::{CommandId, EntityId};
use forge_project::{
    authoring::{new_terrain, playtest::PlaytestSnapshot},
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp,
        migration::ZoneMigrationProposal,
        paths::{AuthoredPath, PathEdit, PathPoint, PathVerticalProfileV1},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn session() -> ZoneEditSession {
    let terrain = new_terrain("paths", "zone", "Zone").unwrap();
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
fn path() -> AuthoredPath {
    AuthoredPath {
        id: EntityId::deterministic("path"),
        name: "Chemin".into(),
        points: vec![
            PathPoint {
                x_m: -12.0,
                z_m: 2.0,
            },
            PathPoint {
                x_m: 20.0,
                z_m: 12.0,
            },
        ],
        width_m: 3.0,
        shoulder_m: 1.0,
        profile: None,
        locked: false,
    }
}
fn apply(s: &mut ZoneEditSession, action: ZoneAction) {
    s.execute(&ZoneRequest {
        command_id: CommandId::deterministic(format!("action{}", s.audit().len())),
        expected_revision: s.head().revision,
        expected_hash: s.head().content_hash().unwrap(),
        action,
    })
    .unwrap();
}
fn verify_archives(s: &ZoneEditSession) {
    for (bytes, version) in [
        (s.archive_bytes().unwrap(), 8),
        (s.delta_archive_bytes().unwrap(), 7),
        (s.compact_archive_bytes().unwrap(), 9),
    ] {
        let json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(json["version"], version);
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), s.head());
        assert_eq!(reopened.audit(), s.audit());
        let mut wrong = json;
        wrong["version"] = serde_json::json!(if version == 8 {
            4
        } else if version == 7 {
            6
        } else {
            5
        });
        assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&wrong).unwrap()).is_err());
    }
}

#[test]
fn paths_persist_retry_undo_redo_and_regenerate() {
    let mut s = session();
    let initial = s.head().clone();
    let request = s
        .head()
        .prepare_path(
            CommandId::deterministic("insert"),
            PathEdit::Insert { path: path() },
        )
        .unwrap();
    let preview = s.preview(&request).unwrap();
    assert_eq!(s.head(), &initial);
    assert_eq!(s.execute(&request).unwrap(), preview);
    assert_eq!(s.execute(&request).unwrap(), preview);
    assert_eq!(s.audit().len(), 1);
    assert_eq!(s.head().version, 3);
    assert_eq!(
        s.head().effective().unwrap(),
        initial
            .effective()
            .map(|(mut t, v)| {
                t.revision = s.head().revision;
                (t, v)
            })
            .unwrap()
    );
    verify_archives(&s);
    let snapshot = PlaytestSnapshot::from_zone(s.head(), s.head().content_hash().unwrap()).unwrap();
    assert_eq!(snapshot.paths(), &[path()]);
    apply(&mut s, ZoneAction::Undo);
    assert!(s.head().corrections.paths.is_none());
    assert_eq!(snapshot.paths(), &[path()]);
    verify_archives(&s); // History still requires the new version after Undo.
    apply(&mut s, ZoneAction::Redo);
    let mut edited = path();
    edited.points[1].x_m = 28.0;
    edited.width_m = 4.0;
    let request = s
        .head()
        .prepare_path(
            CommandId::deterministic("replace"),
            PathEdit::Replace {
                path: edited.clone(),
            },
        )
        .unwrap();
    s.execute(&request).unwrap();
    let mut base = s.head().base.clone();
    base.generator.version = "2".into();
    let request = s
        .head()
        .prepare_regeneration(base, CommandId::deterministic("regen"))
        .unwrap();
    s.execute(&request).unwrap();
    assert_eq!(s.head().corrections.paths.as_ref().unwrap(), &vec![edited]);
    verify_archives(&s);
}

#[test]
fn path_locks_and_invalid_edits_are_atomic_including_raw_commands() {
    let mut s = session();
    let mut locked = path();
    locked.locked = true;
    let request = s
        .head()
        .prepare_path(
            CommandId::deterministic("locked"),
            PathEdit::Insert { path: locked },
        )
        .unwrap();
    s.execute(&request).unwrap();
    let before = s.archive_bytes().unwrap();
    assert!(
        s.head()
            .prepare_path(
                CommandId::deterministic("remove"),
                PathEdit::Remove { id: path().id }
            )
            .is_err()
    );
    let mut corrections = s.head().corrections.clone();
    corrections.paths = None;
    let forged = ZoneRequest {
        command_id: CommandId::deterministic("raw"),
        expected_revision: s.head().revision,
        expected_hash: s.head().content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    };
    assert!(s.execute(&forged).is_err());
    assert_eq!(s.archive_bytes().unwrap(), before);
    let unlock = s
        .head()
        .prepare_path(
            CommandId::deterministic("unlock"),
            PathEdit::SetLocked {
                id: path().id,
                locked: false,
            },
        )
        .unwrap();
    s.execute(&unlock).unwrap();
    let before = s.archive_bytes().unwrap();
    for coordinate in [f32::NAN, f32::INFINITY, 900.0] {
        let mut invalid = path();
        invalid.points[1].x_m = coordinate;
        assert!(
            s.head()
                .prepare_path(
                    CommandId::deterministic("bad"),
                    PathEdit::Replace { path: invalid }
                )
                .is_err()
        );
    }
    let mut invalid = path();
    invalid.points[1] = invalid.points[0];
    assert!(
        s.head()
            .prepare_path(
                CommandId::deterministic("repeat"),
                PathEdit::Replace { path: invalid }
            )
            .is_err()
    );
    assert_eq!(s.archive_bytes().unwrap(), before);
}

#[test]
fn vertical_profile_is_optional_validated_and_persisted_with_the_path() {
    let mut session = session();
    let mut profiled = path();
    profiled.profile = Some(PathVerticalProfileV1 {
        elevations_m: vec![4.0, 6.0],
        crossfall_ratio: 0.05,
        maximum_cut_fill_m: 8.0,
    });
    let request = session
        .head()
        .prepare_path(
            CommandId::deterministic("profile"),
            PathEdit::Insert {
                path: profiled.clone(),
            },
        )
        .unwrap();
    session.execute(&request).unwrap();
    assert_eq!(
        session.head().corrections.paths.as_ref().unwrap(),
        &[profiled]
    );
    verify_archives(&session);

    let mut invalid = path();
    invalid.profile = Some(PathVerticalProfileV1 {
        elevations_m: vec![0.0],
        crossfall_ratio: 0.0,
        maximum_cut_fill_m: 1.0,
    });
    assert!(
        session
            .head()
            .prepare_path(
                CommandId::deterministic("profile-invalid"),
                PathEdit::Replace { path: invalid }
            )
            .is_err()
    );
}
