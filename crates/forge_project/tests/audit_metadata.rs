use forge_core::{CommandId, ProjectId};
use forge_project::{
    audit::{AuditActor, AuditStamp},
    authoring::{AuthoringRequest, history, host::AuthoringHost, new_terrain},
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest, TerrainEditSession},
    workspace::{
        WorkspaceHead, history as workspace_history,
        session::{WorkspaceAction, WorkspaceRequest, WorkspaceSession},
    },
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};

#[test]
fn terrain_and_zone_audit_stamps_survive_all_archive_encodings() {
    let terrain = new_terrain("audit", "terrain", "Initial").unwrap();
    let mut terrain_session = TerrainEditSession::new(terrain).unwrap();
    let terrain_request = TerrainEditRequest {
        command_id: CommandId::deterministic("audit-terrain"),
        expected_revision: terrain_session.document().revision,
        expected_hash: terrain_session.document().content_hash().unwrap(),
        action: TerrainAction::Edit(TerrainEdit::Rename {
            name: "Edited".into(),
        }),
    };
    let human = AuditStamp::human("author-42", "2026-09-22T12:34:56Z").unwrap();
    terrain_session
        .execute_with_audit(&terrain_request, human.clone())
        .unwrap();
    let terrain_bytes = terrain_session.archive_bytes().unwrap();
    let terrain_archive: serde_json::Value = serde_json::from_slice(&terrain_bytes).unwrap();
    assert_eq!(terrain_archive["version"], 3);
    let terrain_reopened = TerrainEditSession::from_archive_bytes(&terrain_bytes).unwrap();
    assert_eq!(
        history::read(
            &terrain_reopened,
            terrain_reopened.document().content_hash().unwrap(),
            0
        )
        .unwrap()
        .entries[0]
            .audit,
        human
    );

    let initial = new_terrain("audit", "zone", "Initial").unwrap();
    let mut zone = ZoneEditSession::from_initial_terrain(initial).unwrap();
    let mut corrections = zone.head().corrections.clone();
    corrections.name = Some("Zone edited".into());
    let zone_request = ZoneRequest {
        command_id: CommandId::deterministic("audit-zone"),
        expected_revision: zone.head().revision,
        expected_hash: zone.head().content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    };
    let ai = AuditStamp::ai("forge-assistant", "2026-09-22T12:34:57Z").unwrap();
    zone.execute_with_audit(&zone_request, ai.clone()).unwrap();
    for bytes in [
        zone.archive_bytes().unwrap(),
        zone.compact_archive_bytes().unwrap(),
        zone.delta_archive_bytes().unwrap(),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.audit()[0].audit, ai);
    }
}

#[test]
fn terrain_v1_v2_archives_keep_their_bytes_and_reject_stamped_receipts() {
    let terrain = new_terrain("audit", "legacy", "Initial").unwrap();
    let mut session = TerrainEditSession::new(terrain).unwrap();
    let request = TerrainEditRequest {
        command_id: CommandId::deterministic("legacy-terrain"),
        expected_revision: session.document().revision,
        expected_hash: session.document().content_hash().unwrap(),
        action: TerrainAction::Edit(TerrainEdit::Rename {
            name: "Legacy".into(),
        }),
    };
    session.execute(&request).unwrap();
    let old = session.archive_bytes().unwrap();
    assert_eq!(
        serde_json::from_slice::<serde_json::Value>(&old).unwrap()["version"],
        1
    );
    assert_eq!(
        TerrainEditSession::from_archive_bytes(&old)
            .unwrap()
            .archive_bytes()
            .unwrap(),
        old
    );

    let mut forged: serde_json::Value = serde_json::from_slice(&old).unwrap();
    forged["receipts"][0]["audit"] = serde_json::json!({
        "actor": {"kind": "human", "subject": "author"},
        "recorded_at_utc": "2026-09-22T12:34:56Z"
    });
    assert!(TerrainEditSession::from_archive_bytes(&serde_json::to_vec(&forged).unwrap()).is_err());

    // Opening a historical archive and authoring again must upgrade it without
    // inventing provenance for the pre-existing receipt.
    let mut upgraded = TerrainEditSession::from_archive_bytes(&old).unwrap();
    let stamped_request = TerrainEditRequest {
        command_id: CommandId::deterministic("stamped-after-legacy"),
        expected_revision: upgraded.document().revision,
        expected_hash: upgraded.document().content_hash().unwrap(),
        action: TerrainAction::Edit(TerrainEdit::Rename {
            name: "Audited".into(),
        }),
    };
    let human = AuditStamp::human("author", "2026-09-22T12:35:00Z").unwrap();
    upgraded
        .execute_with_audit(&stamped_request, human.clone())
        .unwrap();
    let upgraded_bytes = upgraded.archive_bytes().unwrap();
    assert_eq!(
        serde_json::from_slice::<serde_json::Value>(&upgraded_bytes).unwrap()["version"],
        3
    );
    let reopened = TerrainEditSession::from_archive_bytes(&upgraded_bytes).unwrap();
    assert!(reopened.audit()[0].audit.is_legacy());
    assert_eq!(reopened.audit()[1].audit, human);
}

#[test]
fn host_requires_a_nonlegacy_stamp_for_new_receipts_and_accepts_ai_injection() {
    let root = std::env::temp_dir().join(format!("forge-audit-host-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let document = new_terrain("audit", "host", "Initial").unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute(AuthoringRequest::Create {
        document: Box::new(document.clone()),
    })
    .unwrap();
    let request = TerrainEditRequest {
        command_id: CommandId::deterministic("host-ai"),
        expected_revision: document.revision,
        expected_hash: document.content_hash().unwrap(),
        action: TerrainAction::Edit(TerrainEdit::Rename {
            name: "AI edit".into(),
        }),
    };
    assert!(
        host.execute_with_audit(
            AuthoringRequest::Apply {
                request: request.clone()
            },
            AuditStamp::legacy()
        )
        .is_err()
    );
    let ai = AuditStamp::ai("automation", "2026-09-22T12:35:00Z").unwrap();
    let state = host
        .execute_with_audit(AuthoringRequest::Apply { request }, ai.clone())
        .unwrap();
    assert_eq!(state.receipt.unwrap().audit, ai);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn workspace_v1_archives_remain_readable_and_v2_retains_audit_stamp() {
    let mut session = WorkspaceSession::new(WorkspaceHead::default()).unwrap();
    let request = WorkspaceRequest {
        command_id: CommandId::deterministic("audit-workspace"),
        base_revision: session.head().revision,
        base_hash: session.head().content_hash().unwrap(),
        action: WorkspaceAction::CreateProject {
            id: ProjectId::deterministic("audit-project"),
            name: "Audit".into(),
        },
    };
    let stamp = AuditStamp::human("author-42", "2026-09-22T12:34:58Z").unwrap();
    session.execute_with_audit(&request, stamp.clone()).unwrap();
    let reopened = WorkspaceSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    let page =
        workspace_history::read(&reopened, reopened.head().content_hash().unwrap(), 0).unwrap();
    assert_eq!(page.entries[0].audit, stamp);

    let mut v1: serde_json::Value =
        serde_json::from_slice(&session.archive_bytes().unwrap()).unwrap();
    v1["schema_version"] = serde_json::json!(1);
    v1["commands"] = serde_json::json!([request]);
    let legacy = WorkspaceSession::from_archive_bytes(&serde_json::to_vec(&v1).unwrap()).unwrap();
    assert!(matches!(legacy.audit()[0].audit.actor, AuditActor::Legacy));
}
