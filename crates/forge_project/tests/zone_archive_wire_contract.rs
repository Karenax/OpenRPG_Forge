//! Wire compatibility: refactoring dispatch must preserve bytes, replay and refusals.
use forge_core::CommandId;
use forge_project::{
    authoring::new_terrain,
    zone_authoring::{
        ZoneCorrections,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

#[test]
fn archive_codecs_preserve_historical_versions_bytes_and_undo() {
    // Formats 1/3/6 predate paths; adding paths selects the historical 8/9/7
    // family (not an arithmetic +1/+2 relationship).
    for with_paths in [false, true] {
        let mut session = ZoneEditSession::from_initial_terrain(
            new_terrain("wire-contract", "independent-zone", "Archive contract").unwrap(),
        )
        .unwrap();
        let corrections = ZoneCorrections {
            name: Some("Edited independent zone".into()),
            paths: with_paths.then(Vec::new),
            ..Default::default()
        };
        let request = ZoneRequest {
            command_id: CommandId::deterministic("wire-edit"),
            expected_revision: session.head().revision,
            expected_hash: session.head().content_hash().unwrap(),
            action: ZoneAction::edit(corrections),
        };
        session.execute(&request).unwrap();
        let full = session.archive_bytes().unwrap();
        let compact = session.compact_archive_bytes().unwrap();
        let delta = session.delta_archive_bytes().unwrap();
        let expected = if with_paths { [8, 9, 7] } else { [1, 3, 6] };
        for (bytes, version) in [&full, &compact, &delta].into_iter().zip(expected) {
            let value: serde_json::Value = serde_json::from_slice(bytes).unwrap();
            assert_eq!(value["version"], version);
            let mut reopened = ZoneEditSession::from_archive_bytes(bytes).unwrap();
            assert_eq!(reopened.head(), session.head());
            assert_eq!(reopened.audit(), session.audit());
            // archive_bytes preserves the encoding selected on read.
            assert_eq!(reopened.archive_bytes().unwrap(), *bytes);
            let undo = ZoneRequest {
                command_id: CommandId::deterministic("wire-undo"),
                expected_revision: reopened.head().revision,
                expected_hash: reopened.head().content_hash().unwrap(),
                action: ZoneAction::Undo,
            };
            reopened.execute(&undo).unwrap();
            assert!(reopened.head().corrections.name.is_none());
            let mut future = value.clone();
            future["version"] = serde_json::json!(49);
            assert!(
                ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&future).unwrap()).is_err()
            );
            let mut wrong_codec = value;
            wrong_codec["version"] =
                serde_json::json!(if version == 3 || version == 9 { 6 } else { 3 });
            assert!(
                ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&wrong_codec).unwrap())
                    .is_err()
            );
        }
    }
}
