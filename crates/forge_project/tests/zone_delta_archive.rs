use forge_core::CommandId;
use forge_project::{
    authoring::new_terrain,
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp,
        migration::ZoneMigrationProposal,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn command(session: &mut ZoneEditSession, action: ZoneAction) {
    session
        .execute(&ZoneRequest {
            command_id: CommandId::deterministic(format!("delta-{}", session.audit().len())),
            expected_hash: session.head().content_hash().unwrap(),
            expected_revision: session.head().revision,
            action,
        })
        .unwrap();
}

#[test]
fn lossless_delta_keeps_audit_undo_and_rejects_tampering() {
    let terrain = new_terrain("delta", "zone", "Zone").unwrap();
    let hash = terrain.content_hash().unwrap();
    let legacy = TerrainEditSession::new(terrain).unwrap();
    let mut source = ZoneEditSession::from_migration(
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
    .unwrap();
    for i in 0..6 {
        let mut corrections = source.head().corrections.clone();
        corrections.name = Some(format!("Edit {i}"));
        corrections.height_deltas = Some(
            (0..200)
                .map(|x| forge_project::TerrainHeightDelta::new(x, 0, 1.0))
                .collect(),
        );
        command(&mut source, ZoneAction::edit(corrections));
    }
    command(&mut source, ZoneAction::Undo);
    command(&mut source, ZoneAction::Redo);
    let before = source.archive_bytes().unwrap();
    let compact = source.compact_archive_bytes().unwrap();
    let bytes = source.delta_archive_bytes().unwrap();
    assert!(bytes.len() < compact.len() / 2);
    assert_eq!(source.archive_bytes().unwrap(), before);
    let mut restored = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
    assert_eq!(restored.head(), source.head());
    assert_eq!(restored.audit(), source.audit());
    assert_eq!(restored.archive_bytes().unwrap(), bytes);
    command(&mut restored, ZoneAction::Undo);
    command(&mut source, ZoneAction::Undo);
    assert_eq!(restored.head(), source.head());
    verify_interleaved_regeneration(restored);
    let value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
    for path in ["head_hash", "version"] {
        let mut bad = value.clone();
        bad[path] = serde_json::json!(0);
        assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&bad).unwrap()).is_err());
    }
    let mut bad = value.clone();
    bad["commands"][1]["action"]["value"]["name"] = serde_json::json!({"kind":"keep"});
    assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&bad).unwrap()).is_err());
    let mut bad = value;
    bad["commands"][1]["receipt_hash"] = serde_json::json!("00".repeat(32));
    assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&bad).unwrap()).is_err());
}

fn verify_interleaved_regeneration(mut session: ZoneEditSession) {
    // Explicitly release authored height samples before changing their generator seed.
    let mut corrections = session.head().corrections.clone();
    corrections.height_deltas = None;
    command(&mut session, ZoneAction::edit(corrections));
    let mut proposed = session.head().base.clone();
    proposed.terrain.seed += 1;
    command(&mut session, ZoneAction::Regenerate(Box::new(proposed)));
    command(&mut session, ZoneAction::Undo);
    command(&mut session, ZoneAction::Redo);
    let mut corrections = session.head().corrections.clone();
    corrections.name = None;
    corrections.height_deltas = None;
    corrections.base_elevation_m = Some(-0.0);
    command(&mut session, ZoneAction::edit(corrections));
    let before_undo = session.head().clone();
    command(&mut session, ZoneAction::Undo);
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
    assert_eq!(reopened.audit(), session.audit());
    command(&mut reopened, ZoneAction::Redo);
    assert_eq!(reopened.head().corrections, before_undo.corrections);
    assert_eq!(reopened.head().base, before_undo.base);
    assert_eq!(
        reopened
            .head()
            .corrections
            .base_elevation_m
            .unwrap()
            .to_bits(),
        (-0.0_f32).to_bits()
    );
}

#[test]
fn editing_continues_by_switching_to_lossless_delta_before_archive_limit() {
    let mut session =
        ZoneEditSession::from_initial_terrain(new_terrain("delta-auto", "zone", "Zone").unwrap())
            .unwrap();
    for edit in 0..18 {
        let mut corrections = session.head().corrections.clone();
        corrections.name = Some(format!("Edit {edit}"));
        corrections.height_deltas = Some(
            (0..25)
                .flat_map(|z| {
                    (0..80).map(move |x| forge_project::TerrainHeightDelta::new(x, z, 1.0))
                })
                .collect(),
        );
        command(&mut session, ZoneAction::edit(corrections));
    }
    let bytes = session.archive_bytes().unwrap();
    assert_eq!(bytes, session.delta_archive_bytes().unwrap());
    let mut reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
    assert_eq!(reopened.head(), session.head());
    assert_eq!(reopened.audit(), session.audit());
    command(&mut reopened, ZoneAction::Undo);
    command(&mut reopened, ZoneAction::Redo);
    assert_eq!(reopened.head().corrections, session.head().corrections);
    assert_eq!(reopened.head().base, session.head().base);
}

#[test]
#[ignore = "Opt-in read-only replay of the local Studio evidence via FORGE_DELTA_SOURCE"]
fn real_studio_archive_can_continue_without_losing_its_history() {
    let path = std::env::var("FORGE_DELTA_SOURCE").unwrap();
    let original = std::fs::read(&path).unwrap();
    let old = ZoneEditSession::from_archive_bytes(&original).unwrap();
    let mut corrections = old.head().corrections.clone();
    corrections.name = Some("Archive reprise après limite".into());
    let request = ZoneRequest {
        command_id: CommandId::deterministic("resume-blocked-edit"),
        expected_hash: old.head().content_hash().unwrap(),
        expected_revision: old.head().revision,
        action: ZoneAction::edit(corrections),
    };
    assert!(old.preview(&request).is_ok());
    let continued = old.archive_bytes().unwrap();
    assert_eq!(continued, original);
    let bytes = old.delta_archive_bytes().unwrap();
    let mut upgraded = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
    assert_eq!(upgraded.head(), old.head());
    assert_eq!(upgraded.audit(), old.audit());
    upgraded.execute(&request).unwrap();
    let reopened = ZoneEditSession::from_archive_bytes(&upgraded.archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), upgraded.head());
    assert_eq!(reopened.audit(), upgraded.audit());
    assert_eq!(std::fs::read(path).unwrap(), original);
    println!(
        "old_bytes={} delta_bytes={} after_edit_bytes={} retained_commands={}",
        original.len(),
        bytes.len(),
        upgraded.archive_bytes().unwrap().len(),
        old.audit().len()
    );
}
