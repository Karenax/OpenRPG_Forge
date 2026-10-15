// CPU-only regression coverage for the production provenance helper. This test never loads the
// Studio renderer, WebView or Tauri host.
#[path = "../src/authoring_assets/objects/manual_sources.rs"]
mod manual_sources;

use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, Vec3};
use forge_project::{
    authoring::new_terrain,
    zone_authoring::{
        placements::{ManualPlacement, PlacementEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
        source_objects::{self, SourceObjectState},
    },
};

fn session() -> ZoneEditSession {
    ZoneEditSession::from_initial_terrain(
        new_terrain("manual-source-compatibility", "zone", "Manual sources").unwrap(),
    )
    .unwrap()
}

fn object(label: &str, asset: &str, hash: u8) -> ManualPlacement {
    ManualPlacement {
        particles: None,
        physics: None,
        id: EntityId::deterministic(format!("manual-source:{label}")),
        name: format!("{label} object"),
        asset_id: AssetId::deterministic(format!("manual-source-asset:{asset}")),
        asset_hash: ContentHash::from_bytes([hash; 32]),
        transform: Transform::from_translation(Vec3::new(1.0, 2.0, 3.0)),
        locked: false,
    }
}

fn prepare_manual(
    session: &ZoneEditSession,
    sources: &[ManualPlacement],
    action: PlacementEdit,
    label: &str,
) -> ZoneRequest {
    let frozen = manual_sources::freeze_sources(session.head(), sources, &action).unwrap();
    let mut request = frozen
        .prepare_placement(CommandId::deterministic(label), action)
        .unwrap();
    // Production objects::edit prepares against the frozen candidate but applies to the caller's
    // head, so it restores these optimistic-concurrency pins before returning the request.
    request.expected_revision = session.head().revision;
    request.expected_hash = session.head().content_hash().unwrap();
    request
}

fn execute_manual(
    session: &mut ZoneEditSession,
    sources: &[ManualPlacement],
    action: PlacementEdit,
    label: &str,
) {
    let request = prepare_manual(session, sources, action, label);
    session.execute(&request).unwrap();
}

fn history(session: &ZoneEditSession, action: ZoneAction, label: &str) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(label),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    }
}

#[test]
fn new_insert_then_remove_keeps_its_static_source_visible() {
    let mut session = session();
    let source = object("static", "dock", 1);
    let inserted = object("inserted", "dock", 1);

    execute_manual(
        &mut session,
        std::slice::from_ref(&source),
        PlacementEdit::Insert {
            placement: inserted.clone(),
        },
        "new-insert",
    );
    let record = &session
        .head()
        .corrections
        .source_object_edits
        .as_ref()
        .unwrap()[0];
    assert!(matches!(record.state, SourceObjectState::Inherited));
    assert_eq!(
        source_objects::resolve(session.head(), &source).unwrap(),
        vec![source.clone()]
    );

    execute_manual(
        &mut session,
        std::slice::from_ref(&source),
        PlacementEdit::Remove { id: inserted.id },
        "new-remove",
    );
    assert!(
        session
            .head()
            .corrections
            .placements
            .as_deref()
            .unwrap()
            .is_empty()
    );
    assert_eq!(
        source_objects::resolve(session.head(), &source).unwrap(),
        vec![source]
    );
}

#[test]
fn historical_same_pin_import_stays_removed_after_last_manual_delete() {
    let mut session = session();
    let source = object("static", "pier", 2);
    let historical = object("historical", "pier", 2);
    let initial = session
        .head()
        .prepare_placement(
            CommandId::deterministic("historical-insert"),
            PlacementEdit::Insert {
                placement: historical.clone(),
            },
        )
        .unwrap();
    session.execute(&initial).unwrap();

    execute_manual(
        &mut session,
        std::slice::from_ref(&source),
        PlacementEdit::Remove { id: historical.id },
        "historical-remove",
    );
    let record = &session
        .head()
        .corrections
        .source_object_edits
        .as_ref()
        .unwrap()[0];
    assert!(matches!(record.state, SourceObjectState::Removed));
    assert!(
        source_objects::resolve(session.head(), &source)
            .unwrap()
            .is_empty()
    );
}

#[test]
fn existing_edited_locked_source_record_is_not_rewritten_or_duplicated() {
    let mut session = session();
    let source = object("static", "stand", 3);
    let transform = Transform::from_translation(Vec3::new(8.0, 9.0, 10.0));
    let request = session
        .head()
        .prepare_source_object(
            source.clone(),
            PlacementEdit::Transform {
                id: source.id,
                transform,
            },
            CommandId::deterministic("source-transform"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let lock = session
        .head()
        .prepare_source_object(
            source.clone(),
            PlacementEdit::SetLocked {
                id: source.id,
                locked: true,
            },
            CommandId::deterministic("source-lock"),
        )
        .unwrap();
    session.execute(&lock).unwrap();
    let before = session.head().corrections.source_object_edits.clone();

    let frozen = manual_sources::freeze_sources(
        session.head(),
        std::slice::from_ref(&source),
        &PlacementEdit::Insert {
            placement: object("new", "stand", 3),
        },
    )
    .unwrap();
    assert_eq!(frozen.corrections.source_object_edits, before);
    assert_eq!(
        frozen
            .corrections
            .source_object_edits
            .as_ref()
            .unwrap()
            .len(),
        1
    );
}

#[test]
fn replacement_freezes_old_and_new_static_pins_and_batch_round_trips_history() {
    let mut session = session();
    let old_source = object("old-source", "old", 4);
    let new_source = object("new-source", "new", 5);
    let inserted = object("inserted", "old", 4);
    execute_manual(
        &mut session,
        std::slice::from_ref(&old_source),
        PlacementEdit::Insert {
            placement: inserted.clone(),
        },
        "replacement-seed",
    );

    execute_manual(
        &mut session,
        &[old_source.clone(), new_source.clone()],
        PlacementEdit::ReplaceAsset {
            ids: vec![inserted.id],
            expected_asset_id: inserted.asset_id,
            expected_asset_hash: inserted.asset_hash,
            asset_id: new_source.asset_id,
            asset_hash: new_source.asset_hash,
        },
        "replacement",
    );
    let edits = session
        .head()
        .corrections
        .source_object_edits
        .as_ref()
        .unwrap();
    assert_eq!(edits.len(), 2);
    assert_eq!(
        source_objects::resolve(session.head(), &old_source).unwrap(),
        vec![old_source.clone()]
    );
    assert_eq!(
        source_objects::resolve(session.head(), &new_source).unwrap(),
        vec![new_source.clone()]
    );

    let batch_a = object("batch-a", "foreign", 6);
    let batch_b = object("batch-b", "foreign", 6);
    execute_manual(
        &mut session,
        &[old_source, new_source],
        PlacementEdit::InsertMany {
            placements: vec![batch_a, batch_b],
        },
        "batch",
    );
    assert_eq!(
        session
            .head()
            .corrections
            .placements
            .as_ref()
            .unwrap()
            .len(),
        3
    );
    let undo = history(&session, ZoneAction::Undo, "batch-undo");
    session.execute(&undo).unwrap();
    assert_eq!(
        session
            .head()
            .corrections
            .placements
            .as_ref()
            .unwrap()
            .len(),
        1
    );
    let redo = history(&session, ZoneAction::Redo, "batch-redo");
    session.execute(&redo).unwrap();
    assert_eq!(
        session
            .head()
            .corrections
            .placements
            .as_ref()
            .unwrap()
            .len(),
        3
    );
}

#[test]
fn unrelated_asset_does_not_create_source_provenance() {
    let session = session();
    let source = object("static", "verified", 7);
    let foreign = object("foreign", "other", 8);
    let frozen = manual_sources::freeze_sources(
        session.head(),
        std::slice::from_ref(&source),
        &PlacementEdit::Insert { placement: foreign },
    )
    .unwrap();
    assert_eq!(&frozen, session.head());
}
