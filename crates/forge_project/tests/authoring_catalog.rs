use forge_core::CommandId;
use forge_project::{
    authoring::{AuthoringRequest, host::AuthoringHost, new_terrain},
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest},
};

#[test]
fn catalog_explains_interrupted_missing_mismatched_and_oversized_archives() {
    use forge_project::authoring::UnavailableZoneReason as Reason;
    let root = std::env::temp_dir().join(format!("forge-catalog-reasons-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    let original = new_terrain("catalog", "saved", "Saved").unwrap();
    host.execute(AuthoringRequest::Create {
        document: Box::new(original.clone()),
    })
    .unwrap();
    host.execute(AuthoringRequest::Save).unwrap();
    let saved = root.join(original.area_id.to_string());
    let bytes = std::fs::read(saved.join("authoring.json")).unwrap();
    std::fs::write(saved.join("authoring.pending"), &bytes).unwrap();
    let cases = [
        ("interrupted", Reason::InterruptedSave),
        ("missing", Reason::MissingArchive),
        ("mismatched", Reason::IdentityMismatch),
        ("oversized", Reason::TooLarge),
        ("directory", Reason::UnsafePath),
    ];
    for (key, _) in cases {
        let id = new_terrain("catalog", key, key).unwrap().area_id;
        let directory = root.join(id.to_string());
        std::fs::create_dir(&directory).unwrap();
        match key {
            "interrupted" => std::fs::write(directory.join("authoring.pending"), &bytes).unwrap(),
            "mismatched" => std::fs::write(directory.join("authoring.json"), &bytes).unwrap(),
            "oversized" => std::fs::File::create(directory.join("authoring.json"))
                .unwrap()
                .set_len(4 * 1024 * 1024 + 1)
                .unwrap(),
            "directory" => std::fs::create_dir(directory.join("authoring.json")).unwrap(),
            _ => (),
        }
    }
    let catalog = host
        .execute(AuthoringRequest::ListZones)
        .unwrap()
        .catalog
        .unwrap();
    assert_eq!(catalog.zones.len(), 1);
    assert!(catalog.zones[0].pending_recovery);
    assert_eq!(catalog.unavailable.len(), cases.len());
    for (key, reason) in cases {
        let id = new_terrain("catalog", key, key).unwrap().area_id;
        assert_eq!(
            catalog
                .unavailable
                .iter()
                .find(|zone| zone.area_id == id)
                .unwrap()
                .reason,
            reason
        );
    }
    assert_eq!(
        std::fs::read(saved.join("authoring.pending")).unwrap(),
        bytes
    );
    assert_eq!(std::fs::read(saved.join("authoring.json")).unwrap(), bytes);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

#[test]
fn listing_saved_zones_preserves_dirty_session_and_reports_bad_archives() {
    let root = std::env::temp_dir().join(format!("forge-catalog-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    let original = new_terrain("catalog", "valid", "Saved title").unwrap();
    host.execute(AuthoringRequest::Create {
        document: Box::new(original.clone()),
    })
    .unwrap();
    host.execute(AuthoringRequest::Save).unwrap();
    let path = root
        .join(original.area_id.to_string())
        .join("authoring.json");
    let bytes = std::fs::read(&path).unwrap();
    host.execute(AuthoringRequest::Apply {
        request: TerrainEditRequest {
            command_id: CommandId::deterministic("catalog-edit"),
            expected_revision: original.revision,
            expected_hash: original.content_hash().unwrap(),
            action: TerrainAction::Edit(TerrainEdit::Rename {
                name: "Unsaved title".into(),
            }),
        },
    })
    .unwrap();
    let bad = new_terrain("catalog", "bad", "Bad").unwrap();
    let bad_dir = root.join(bad.area_id.to_string());
    std::fs::create_dir(&bad_dir).unwrap();
    std::fs::write(bad_dir.join("authoring.json"), b"not JSON").unwrap();
    let before = host.execute(AuthoringRequest::Inspect).unwrap();
    let listed = host.execute(AuthoringRequest::ListZones).unwrap();
    assert_eq!(listed.document, before.document);
    assert!(listed.dirty);
    let catalog = listed.catalog.unwrap();
    assert_eq!(catalog.zones.len(), 1);
    assert_eq!(catalog.zones[0].name, "Saved title");
    assert_eq!(
        catalog.zones[0].saved_hash,
        original.content_hash().unwrap()
    );
    assert_eq!(catalog.unavailable.len(), 1);
    assert_eq!(catalog.unavailable[0].area_id, bad.area_id);
    assert_eq!(
        catalog.unavailable[0].reason,
        forge_project::authoring::UnavailableZoneReason::InvalidArchive
    );
    assert!(!catalog.zones[0].pending_recovery);
    assert_eq!(std::fs::read(&path).unwrap(), bytes);
    assert!(
        host.execute(AuthoringRequest::Open {
            area_id: original.area_id
        })
        .is_err()
    );
    host.execute(AuthoringRequest::StartPlaytest {
        expected_hash: before.document.unwrap().content_hash().unwrap(),
    })
    .unwrap();
    assert!(
        host.execute(AuthoringRequest::ListZones)
            .unwrap()
            .playtest_hash
            .is_some()
    );
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}
