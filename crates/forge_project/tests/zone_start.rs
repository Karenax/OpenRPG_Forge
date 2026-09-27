use forge_core::CommandId;
use forge_project::{
    authoring::{AuthoringRequest, host::AuthoringHost, new_terrain},
    zone_authoring::{
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
        start::ZoneStart,
    },
};

fn start() -> ZoneStart {
    ZoneStart {
        x_m: 12.0,
        z_m: -8.0,
        facing_degrees: Some(90.0),
    }
}
fn action(session: &ZoneEditSession, action: ZoneAction, key: &str) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(key),
        expected_hash: session.head().content_hash().unwrap(),
        expected_revision: session.head().revision,
        action,
    }
}

#[test]
fn authored_start_roundtrips_all_codecs_and_survives_regeneration_undo_redo() {
    let mut session =
        ZoneEditSession::from_initial_terrain(new_terrain("start", "zone", "Zone").unwrap())
            .unwrap();
    let old = session.archive_bytes().unwrap();
    assert_eq!(
        old,
        ZoneEditSession::from_archive_bytes(&old)
            .unwrap()
            .archive_bytes()
            .unwrap()
    );
    let request = session
        .head()
        .prepare_start(Some(start()), CommandId::deterministic("set-start"))
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), old);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.head().version, 6);
    let mut base = session.head().base.clone();
    base.generator.version = "2".into();
    let regen = session
        .head()
        .prepare_regeneration(base, CommandId::deterministic("regen-start"))
        .unwrap();
    session.execute(&regen).unwrap();
    assert_eq!(session.head().corrections.start.as_deref(), Some(&start()));
    for (bytes, version, old_version) in [
        (session.archive_bytes().unwrap(), 16, 13),
        (session.compact_archive_bytes().unwrap(), 17, 14),
        (session.delta_archive_bytes().unwrap(), 18, 15),
    ] {
        let mut json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(json["version"], version);
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        let remove = reopened
            .head()
            .prepare_start(None, CommandId::deterministic("remove-start"))
            .unwrap();
        reopened.execute(&remove).unwrap();
        assert!(reopened.head().corrections.start.is_none());
        let undo = action(&reopened, ZoneAction::Undo, "undo-start");
        reopened.execute(&undo).unwrap();
        assert_eq!(reopened.head().corrections.start.as_deref(), Some(&start()));
        let redo = action(&reopened, ZoneAction::Redo, "redo-start");
        reopened.execute(&redo).unwrap();
        let final_bytes = reopened.archive_bytes().unwrap();
        assert!(
            ZoneEditSession::from_archive_bytes(&final_bytes)
                .unwrap()
                .head()
                .corrections
                .start
                .is_none()
        );
        json["version"] = old_version.into();
        assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&json).unwrap()).is_err());
    }
    for bad in [
        ZoneStart {
            x_m: f32::NAN,
            ..start()
        },
        ZoneStart {
            x_m: 256.0,
            ..start()
        },
        ZoneStart {
            facing_degrees: Some(f32::INFINITY),
            ..start()
        },
    ] {
        assert!(
            session
                .head()
                .prepare_start(Some(bad), CommandId::deterministic("bad-start"))
                .is_err()
        );
        let mut corrections = session.head().corrections.clone();
        corrections.start = Some(Box::new(bad));
        assert!(
            session
                .execute(&action(
                    &session,
                    ZoneAction::edit(corrections),
                    "raw-bad-start"
                ))
                .is_err()
        );
    }
}

#[test]
fn saved_default_and_temporary_override_are_distinct_in_native_host() {
    let directory = std::env::temp_dir().join(format!("forge-saved-start-{}", std::process::id()));
    std::fs::create_dir(&directory).unwrap();
    let mut host = AuthoringHost::new(&directory).unwrap();
    let terrain = new_terrain("host-start", "zone", "Zone").unwrap();
    let area_id = terrain.area_id;
    let state = host
        .execute(AuthoringRequest::CreateZone {
            document: Box::new(terrain),
        })
        .unwrap();
    let request = state
        .zone_head
        .unwrap()
        .prepare_start(Some(start()), CommandId::deterministic("host-start"))
        .unwrap();
    host.execute(AuthoringRequest::ApplyZone { request })
        .unwrap();
    let saved = host.execute(AuthoringRequest::Save).unwrap();
    let hash = saved.zone_head.unwrap().content_hash().unwrap();
    host.execute(AuthoringRequest::Close).unwrap();
    host.execute(AuthoringRequest::Open { area_id }).unwrap();
    let playing = host
        .execute(AuthoringRequest::StartPlaytest {
            expected_hash: hash,
        })
        .unwrap();
    assert_eq!(playing.playtest_start, Some(start()));
    let snapshot = forge_project::authoring::playtest::PlaytestSnapshot::from_state(&playing)
        .unwrap()
        .unwrap();
    assert_eq!(snapshot.start(), Some(start()));
    host.execute(AuthoringRequest::StopPlaytest { diagnostics: None })
        .unwrap();
    let temporary = ZoneStart {
        x_m: -10.0,
        ..start()
    };
    let playing = host
        .execute(AuthoringRequest::StartPlaytestAt {
            expected_hash: hash,
            start: temporary,
        })
        .unwrap();
    assert_eq!(playing.playtest_start, Some(temporary));
    assert_eq!(
        playing.zone_head.unwrap().corrections.start.as_deref(),
        Some(&start())
    );
    host.execute(AuthoringRequest::StopPlaytest { diagnostics: None })
        .unwrap();
    host.execute(AuthoringRequest::Close).unwrap();
    drop(host);
    std::fs::remove_dir_all(directory).unwrap();
}
