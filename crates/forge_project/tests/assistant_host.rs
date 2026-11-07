use forge_core::CommandId;
use forge_project::{
    assistant::ScopePolicy,
    audit::AuditStamp,
    authoring::{AuthoringRequest as Request, host::AuthoringHost, new_terrain},
    terrain_edit::{TerrainAction, TerrainEdit},
};

#[test]
fn native_host_reuses_candidate_validation_and_saves_the_accepted_ai_receipt() {
    let root = std::env::temp_dir().join(format!("forge-assistant-host-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root)
        .unwrap()
        .with_zone_validator(|head| {
            if head.corrections.name.as_deref() == Some("Forbidden") {
                Err("Native candidate policy rejected proposal".into())
            } else {
                Ok(())
            }
        });
    let document = new_terrain("assistant-host", "zone", "Original").unwrap();
    let area_id = document.area_id;
    let state = host
        .execute(Request::CreateZone {
            document: Box::new(document),
        })
        .unwrap();
    host.execute(Request::Save).unwrap();
    let head = state.zone_head.unwrap();
    let path = root.join(area_id.to_string()).join("authoring.json");
    let saved = std::fs::read(&path).unwrap();
    let policy = ScopePolicy::new(vec!["/corrections/name".into()], vec![]).unwrap();
    let audit = AuditStamp::ai("synthetic-host-test", "2026-10-22T12:00:00Z").unwrap();
    let request = |key: &str, name: &str| {
        head.prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::Rename { name: name.into() }),
            CommandId::deterministic(key),
        )
        .unwrap()
    };
    assert!(
        host.prepare_assistant(request("reject", "Forbidden"), &policy, audit.clone())
            .is_err()
    );
    assert_eq!(std::fs::read(&path).unwrap(), saved);
    let review = host
        .prepare_assistant(request("accept", "Accepted"), &policy, audit.clone())
        .unwrap();
    assert_eq!(std::fs::read(&path).unwrap(), saved);
    assert!(!host.execute(Request::Inspect).unwrap().dirty);
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    let state = host.apply_assistant(&policy, &review, &approval).unwrap();
    assert_eq!(state.zone_receipt.as_ref(), Some(review.receipt()));
    assert_eq!(state.zone_receipt.unwrap().audit, audit);
    assert!(state.dirty);
    host.execute(Request::Save).unwrap();
    host.execute(Request::Close).unwrap();
    let reopened = host.execute(Request::Open { area_id }).unwrap();
    assert_eq!(reopened.document.unwrap().name, "Accepted");
    let bytes = std::fs::read(&path).unwrap();
    let replay = host.apply_assistant(&policy, &review, &approval).unwrap();
    assert!(!replay.dirty);
    assert_eq!(replay.zone_receipt.as_ref(), Some(review.receipt()));
    assert_eq!(std::fs::read(&path).unwrap(), bytes);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}
