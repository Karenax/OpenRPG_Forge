use forge_core::CommandId;
use forge_project::{
    authoring::new_terrain,
    zone_authoring::{
        physics::PhysicsSceneSettings,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn settings() -> PhysicsSceneSettings {
    PhysicsSceneSettings {
        schema_version: 1,
        layer_matrix: [u32::MAX; 32],
        contact_skin_m: 0.,
        evidence: "Explicit target scene policy; original-project equivalence unqualified".into(),
    }
}
fn request(session: &ZoneEditSession, action: ZoneAction, key: &str) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(key),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    }
}
#[test]
fn physics_roundtrips_all_codecs_and_preserves_other_edits() {
    let mut session =
        ZoneEditSession::from_initial_terrain(new_terrain("phys-scene", "zone", "Zone").unwrap())
            .unwrap();
    let old = session.archive_bytes().unwrap();
    assert_eq!(
        old,
        ZoneEditSession::from_archive_bytes(&old)
            .unwrap()
            .archive_bytes()
            .unwrap()
    );
    assert!(
        serde_json::to_value(&session.head().corrections)
            .unwrap()
            .get("physics")
            .is_none()
    );
    let mut corrections = session.head().corrections.clone();
    corrections.name = Some("Retouche conservée".into());
    session
        .execute(&request(&session, ZoneAction::edit(corrections), "rename"))
        .unwrap();
    let set = session
        .head()
        .prepare_physics(Some(settings()), CommandId::deterministic("physics-set"))
        .unwrap();
    let before = session.archive_bytes().unwrap();
    let preview = session.preview(&set).unwrap();
    assert_eq!(before, session.archive_bytes().unwrap());
    assert_eq!(session.execute(&set).unwrap(), preview);
    assert_eq!(session.head().version, 15);
    for (bytes, version, old_version) in [
        (session.archive_bytes().unwrap(), 40, 1),
        (session.compact_archive_bytes().unwrap(), 41, 3),
        (session.delta_archive_bytes().unwrap(), 42, 6),
    ] {
        let mut value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(value["version"], version);
        let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(
            reopened.head().corrections.name.as_deref(),
            Some("Retouche conservée")
        );
        let remove = reopened
            .head()
            .prepare_physics(None, CommandId::deterministic("physics-remove"))
            .unwrap();
        reopened.execute(&remove).unwrap();
        assert!(reopened.head().corrections.physics.is_none());
        let undo = request(&reopened, ZoneAction::Undo, "physics-undo");
        reopened.execute(&undo).unwrap();
        assert_eq!(
            reopened.head().corrections.physics.as_deref(),
            Some(&settings())
        );
        let redo = request(&reopened, ZoneAction::Redo, "physics-redo");
        reopened.execute(&redo).unwrap();
        assert!(reopened.head().corrections.physics.is_none());
        assert_eq!(
            ZoneEditSession::from_archive_bytes(&reopened.archive_bytes().unwrap())
                .unwrap()
                .head(),
            reopened.head()
        );
        value["version"] = old_version.into();
        assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&value).unwrap()).is_err());
    }
}
#[test]
fn invalid_physics_and_stale_request_never_mutate_session() {
    let mut session =
        ZoneEditSession::from_initial_terrain(new_terrain("phys-scene", "zone", "Zone").unwrap())
            .unwrap();
    let bytes = session.archive_bytes().unwrap();
    for fault in 0..6 {
        let mut bad = settings();
        match fault {
            0 => bad.schema_version = 2,
            1 => bad.contact_skin_m = f32::NAN,
            2 => bad.contact_skin_m = -1.,
            3 => bad.evidence.clear(),
            4 => bad.layer_matrix[1] &= !1,
            _ => bad.evidence = "x".repeat(2049),
        }
        assert!(
            session
                .head()
                .prepare_physics(Some(bad), CommandId::deterministic("bad-physics"))
                .is_err()
        );
        assert_eq!(session.archive_bytes().unwrap(), bytes);
    }
    let stale = session
        .head()
        .prepare_physics(Some(settings()), CommandId::deterministic("stale-physics"))
        .unwrap();
    let other = session
        .head()
        .prepare_physics(Some(settings()), CommandId::deterministic("other-physics"))
        .unwrap();
    session.execute(&other).unwrap();
    let updated = session.archive_bytes().unwrap();
    assert!(session.execute(&stale).is_err());
    assert_eq!(session.archive_bytes().unwrap(), updated);
}
