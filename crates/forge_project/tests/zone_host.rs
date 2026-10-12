use forge_core::CommandId;
use forge_project::{
    authoring::{AuthoringRequest as Request, host::AuthoringHost, new_terrain},
    zone_authoring::{
        GeneratorStamp,
        session::{ZoneAction, ZoneRequest},
    },
};

#[test]
#[allow(
    clippy::too_many_lines,
    reason = "the end-to-end scenario deliberately keeps one host lifetime visible"
)]
fn one_host_reviews_migrates_edits_and_reopens_aggregate_without_losing_legacy() {
    let root = std::env::temp_dir().join(format!("forge-zone-host-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root)
        .unwrap()
        .with_zone_validator(|head| {
            if head.corrections.name.as_deref() == Some("Rejected candidate") {
                Err("Application policy rejected candidate".into())
            } else {
                Ok(())
            }
        });
    let document = new_terrain("aggregate-host", "zone", "Original").unwrap();
    let area_id = document.area_id;
    let hash = document.content_hash().unwrap();
    let preparation = Request::PrepareZoneMigration {
        expected_hash: hash,
        generator: GeneratorStamp {
            name: "accepted-legacy".into(),
            version: "1".into(),
            inputs_hash: hash,
        },
        vegetation: Some(Box::new(vegetation(&document))),
    };
    host.execute(Request::Create {
        document: Box::new(document),
    })
    .unwrap();
    assert!(host.execute(preparation.clone()).is_err(), "save first");
    host.execute(Request::Save).unwrap();
    let path = root.join(area_id.to_string()).join("authoring.json");
    let bytes = std::fs::read(&path).unwrap();
    let reviewed = host.execute(preparation.clone()).unwrap();
    assert!(reviewed.zone_head.is_none());
    let proposal = reviewed.zone_migration.unwrap();
    assert_eq!(proposal.legacy_archive(), bytes);
    assert_eq!(std::fs::read(&path).unwrap(), bytes);
    let migrate = Request::BeginZoneMigration {
        proposal: Box::new(proposal),
    };
    assert!(host.execute(migrate.clone()).is_err(), "close first");
    host.execute(Request::Close).unwrap();
    let migrated = host.execute(migrate).unwrap();
    assert!(migrated.dirty, "migration itself must be saved");
    assert!(host.execute(Request::Close).is_err());
    assert!(
        host.execute(Request::Open { area_id }).is_err(),
        "single active zone"
    );
    assert!(
        host.execute(preparation).is_err(),
        "no legacy commands after migration"
    );
    assert_eq!(
        std::fs::read(&path).unwrap(),
        bytes,
        "migration is not autosave"
    );
    verify_playtest_preparation(&mut host, &migrated);
    assert_eq!(std::fs::read(&path).unwrap(), bytes);
    let head = migrated.zone_head.unwrap();
    verify_candidate_rejection(&mut host, &head, &path);
    let mut corrections = head.corrections.clone();
    corrections.name = Some("Clairière".into());
    let request = ZoneRequest {
        command_id: CommandId::deterministic("zone-host-rename"),
        expected_revision: head.revision,
        expected_hash: head.content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    };
    let preview = host
        .execute(Request::PreviewZone {
            request: request.clone(),
        })
        .unwrap();
    assert_eq!(preview.zone_head.as_ref(), Some(&head));
    let applied = host
        .execute(Request::ApplyZone {
            request: request.clone(),
        })
        .unwrap();
    assert_eq!(applied.zone_receipt, preview.zone_receipt);
    assert_eq!(applied.document.as_ref().unwrap().name, "Clairière");
    assert_eq!(
        host.execute(Request::ApplyZone { request }).unwrap(),
        applied,
        "retry changes nothing"
    );
    host.execute(Request::Save).unwrap();
    host.execute(Request::Close).unwrap();
    let reopened = host.execute(Request::Open { area_id }).unwrap();
    assert_eq!(reopened.zone_head, applied.zone_head);
    assert!(reopened.can_undo);
    assert!(!reopened.dirty);
    let catalog = host.execute(Request::ListZones).unwrap().catalog.unwrap();
    assert_eq!(catalog.zones.len(), 1);
    assert!(catalog.unavailable.is_empty());
    assert_eq!(catalog.zones[0].name, "Clairière");
    assert_eq!(
        catalog.zones[0].saved_hash,
        reopened.zone_head.unwrap().content_hash().unwrap()
    );
    verify_compaction(&mut host, area_id, &path);
    verify_placement_persistence(&mut host, area_id, &path);
    host.execute(Request::Open { area_id }).unwrap();
    verify_river_playtest_snapshot(&mut host, &path);
    drop(host);
    std::fs::remove_dir_all(root).unwrap();
}

fn verify_candidate_rejection(
    host: &mut AuthoringHost,
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    path: &std::path::Path,
) {
    let bytes = std::fs::read(path).unwrap();
    let before = host.execute(Request::Inspect).unwrap();
    let mut corrections = head.corrections.clone();
    corrections.name = Some("Rejected candidate".into());
    let request = ZoneRequest {
        command_id: CommandId::deterministic("rejected-by-application-policy"),
        expected_revision: head.revision,
        expected_hash: head.content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    };
    for command in [
        Request::PreviewZone {
            request: request.clone(),
        },
        Request::ApplyZone { request },
    ] {
        assert_eq!(
            host.execute(command).unwrap_err(),
            "Application policy rejected candidate"
        );
        assert_eq!(host.execute(Request::Inspect).unwrap(), before);
        assert_eq!(std::fs::read(path).unwrap(), bytes);
    }
}

fn verify_compaction(
    host: &mut AuthoringHost,
    area_id: forge_core::AreaId,
    path: &std::path::Path,
) {
    let original = std::fs::read(path).unwrap();
    let state = host.execute(Request::ReviewZoneCompaction).unwrap();
    verify_history(host, &state);
    assert_eq!(std::fs::read(path).unwrap(), original);
    assert!(!state.dirty);
    let review = state.zone_compaction.unwrap();
    let mut altered = review.clone();
    altered.target_bytes += 1;
    assert!(
        host.execute(Request::CompactZoneArchive { review: altered })
            .is_err()
    );
    assert!(!host.execute(Request::Inspect).unwrap().dirty);
    let converted = host
        .execute(Request::CompactZoneArchive { review })
        .unwrap();
    assert_eq!(converted.zone_head, state.zone_head);
    assert!(converted.can_undo);
    assert!(converted.dirty);
    assert_eq!(std::fs::read(path).unwrap(), original);
    assert!(host.execute(Request::Close).is_err());
    host.execute(Request::Save).unwrap();
    let saved: serde_json::Value = serde_json::from_slice(&std::fs::read(path).unwrap()).unwrap();
    assert_eq!(saved["version"], 6);
    host.execute(Request::Close).unwrap();
    let reopened = host.execute(Request::Open { area_id }).unwrap();
    assert_eq!(reopened.zone_head, converted.zone_head);
    assert!(reopened.can_undo);
    assert!(!reopened.dirty);
    assert!(host.execute(Request::ReviewZoneCompaction).is_err());
    host.execute(Request::Close).unwrap();
}

fn verify_history(host: &mut AuthoringHost, state: &forge_project::authoring::AuthoringState) {
    verify_playtest_preparation(host, state);
    let hash = state.zone_head.as_ref().unwrap().content_hash().unwrap();
    let read = host
        .execute(Request::ReadHistory {
            expected_hash: hash,
            offset: 0,
        })
        .unwrap();
    assert_eq!(read.zone_head, state.zone_head);
    assert_eq!(read.dirty, state.dirty);
    assert_eq!(read.can_undo, state.can_undo);
    let page = read.history.unwrap();
    assert_eq!(page.total, 1);
    assert_eq!(page.source_hash, hash);
    assert!(matches!(
        page.entries[0].change,
        forge_project::authoring::history::HistoryChange::Zone { .. }
    ));
    assert!(
        host.execute(Request::ReadHistory {
            expected_hash: state.document.as_ref().unwrap().content_hash().unwrap(),
            offset: 0
        })
        .is_err()
    );
}

fn verify_playtest_preparation(
    host: &mut AuthoringHost,
    state: &forge_project::authoring::AuthoringState,
) {
    let hash = state.zone_head.as_ref().unwrap().content_hash().unwrap();
    let prepared = host
        .execute(Request::PreparePlaytest {
            expected_hash: hash,
        })
        .unwrap();
    assert_eq!(prepared.zone_head, state.zone_head);
    assert_eq!(prepared.dirty, state.dirty);
    assert_eq!(prepared.can_undo, state.can_undo);
    assert!(
        prepared.playtest_hash.is_none(),
        "preparation is not simulation"
    );
    let identity = prepared.playtest_identity.unwrap();
    assert_eq!(identity.source_hash, hash);
    assert_eq!(
        identity.terrain_hash,
        prepared.document.unwrap().content_hash().unwrap()
    );
    assert!(
        host.execute(Request::PreparePlaytest {
            expected_hash: identity.terrain_hash
        })
        .is_err()
    );
    let playing = host
        .execute(Request::StartPlaytest {
            expected_hash: hash,
        })
        .unwrap();
    assert_eq!(playing.playtest_identity, Some(identity));
    assert_eq!(playing.playtest_hash, Some(hash));
    assert_eq!(playing.zone_head, state.zone_head);
    assert_eq!(playing.dirty, state.dirty);
    assert_eq!(playing.can_undo, state.can_undo);
    assert_eq!(host.execute(Request::Inspect).unwrap(), playing);
    assert!(host.execute(Request::Save).is_err());
    assert!(host.execute(Request::Close).is_err());
    let head = state.zone_head.as_ref().unwrap();
    assert!(
        host.execute(Request::ApplyZone {
            request: ZoneRequest {
                command_id: CommandId::deterministic("forbidden-during-play"),
                expected_revision: head.revision,
                expected_hash: hash,
                action: ZoneAction::edit(forge_project::zone_authoring::ZoneCorrections::default()),
            }
        })
        .is_err()
    );
    assert!(
        host.execute(Request::StartPlaytest {
            expected_hash: identity.terrain_hash
        })
        .is_err()
    );
    assert_eq!(host.execute(Request::Inspect).unwrap(), playing);
    verify_snapshot_response(&playing);
    let stopped = host
        .execute(Request::StopPlaytest { diagnostics: None })
        .unwrap();
    assert_eq!(stopped.zone_head, state.zone_head);
    assert_eq!(stopped.dirty, state.dirty);
    assert_eq!(stopped.can_undo, state.can_undo);
    assert!(stopped.playtest_identity.is_none());
    assert!(stopped.playtest_hash.is_none());
}

fn verify_snapshot_response(playing: &forge_project::authoring::AuthoringState) {
    use forge_project::authoring::playtest::PlaytestSnapshot;
    let decoded = serde_json::from_str(&serde_json::to_string(playing).unwrap()).unwrap();
    let snapshot = PlaytestSnapshot::from_state(&decoded).unwrap().unwrap();
    assert_eq!(snapshot.terrain(), playing.document.as_ref().unwrap());
    assert_eq!(
        snapshot.vegetation(),
        playing
            .zone_head
            .as_ref()
            .unwrap()
            .effective()
            .unwrap()
            .1
            .as_ref()
    );
    let mut invalid = playing.clone();
    invalid.document.as_mut().unwrap().name = "Wrong derived view".into();
    assert!(PlaytestSnapshot::from_state(&invalid).is_err());
    invalid = playing.clone();
    invalid.playtest_identity = None;
    assert!(PlaytestSnapshot::from_state(&invalid).is_err());
    invalid = playing.clone();
    invalid.playtest_identity.as_mut().unwrap().vegetation_hash =
        Some(snapshot.identity().terrain_hash);
    assert!(PlaytestSnapshot::from_state(&invalid).is_err());
    invalid = playing.clone();
    invalid.playtest_hash = None;
    assert!(PlaytestSnapshot::from_state(&invalid).unwrap().is_none());
}

fn verify_river_playtest_snapshot(host: &mut AuthoringHost, path: &std::path::Path) {
    use forge_project::authoring::playtest::PlaytestSnapshot;
    use forge_project::zone_authoring::rivers::{AuthoredRiver, RiverEdit};
    let state = host.execute(Request::Inspect).unwrap();
    let head = state.zone_head.unwrap();
    let mut water: forge_project::WaterDocument = serde_json::from_str(include_str!(
        "../../../examples/visual_valley/water.v1.json"
    ))
    .unwrap();
    water.project_id = head.base.terrain.project_id;
    water.area_id = head.base.terrain.area_id;
    let river = AuthoredRiver {
        id: forge_core::EntityId::deterministic("host-river"),
        water,
        maximum_cut_m: 8.0,
        locked: false,
    };
    let request = head
        .prepare_river(
            CommandId::deterministic("host-river-insert"),
            RiverEdit::Insert {
                river: river.clone(),
            },
        )
        .unwrap();
    let applied = host.execute(Request::ApplyZone { request }).unwrap();
    let saved = std::fs::read(path).unwrap();
    verify_playtest_preparation(host, &applied);
    let hash = applied.zone_head.as_ref().unwrap().content_hash().unwrap();
    let playing = host
        .execute(Request::StartPlaytest {
            expected_hash: hash,
        })
        .unwrap();
    let snapshot = PlaytestSnapshot::from_state(&playing).unwrap().unwrap();
    assert_eq!(snapshot.rivers(), &[river]);
    let mut altered = playing;
    altered
        .zone_head
        .as_mut()
        .unwrap()
        .corrections
        .rivers
        .as_mut()
        .unwrap()[0]
        .maximum_cut_m = 7.0;
    assert!(PlaytestSnapshot::from_state(&altered).is_err());
    let stopped = host
        .execute(Request::StopPlaytest { diagnostics: None })
        .unwrap();
    assert_eq!(stopped.zone_head, applied.zone_head);
    assert_eq!(std::fs::read(path).unwrap(), saved);
}

fn vegetation(terrain: &forge_project::TerrainDocument) -> forge_project::VegetationDocument {
    let mut vegetation: forge_project::VegetationDocument = serde_json::from_str(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = terrain.project_id;
    vegetation.area_id = terrain.area_id;
    for layer in &mut vegetation.layers {
        layer.material_filter = None;
        layer.density_mask = None;
    }
    vegetation
}

fn verify_placement_persistence(
    host: &mut AuthoringHost,
    area_id: forge_core::AreaId,
    path: &std::path::Path,
) {
    use forge_project::zone_authoring::placements::{ManualPlacement, PlacementEdit};
    let initial = std::fs::read(path).unwrap();
    let state = host.execute(Request::Open { area_id }).unwrap();
    let placement = ManualPlacement {
        physics: None,
        id: forge_core::EntityId::deterministic("host-door"),
        asset_id: forge_core::AssetId::deterministic("fixture-door"),
        asset_hash: forge_core::ContentHash::from_bytes([4; 32]),
        name: "Porte de test".into(),
        transform: forge_core::Transform::IDENTITY,
        locked: true,
    };
    let request = state
        .zone_head
        .as_ref()
        .unwrap()
        .prepare_placement(
            CommandId::deterministic("host-placement"),
            PlacementEdit::Insert {
                placement: placement.clone(),
            },
        )
        .unwrap();
    let preview = host
        .execute(Request::PreviewZone {
            request: request.clone(),
        })
        .unwrap();
    assert_eq!(preview.zone_head, state.zone_head);
    let applied = host.execute(Request::ApplyZone { request }).unwrap();
    assert!(applied.dirty);
    assert_eq!(std::fs::read(path).unwrap(), initial);
    let playing = host
        .execute(Request::StartPlaytest {
            expected_hash: applied.zone_head.as_ref().unwrap().content_hash().unwrap(),
        })
        .unwrap();
    let snapshot = forge_project::authoring::playtest::PlaytestSnapshot::from_state(&playing)
        .unwrap()
        .unwrap();
    assert_eq!(snapshot.placements(), std::slice::from_ref(&placement));
    assert_eq!(playing.zone_head, applied.zone_head);
    assert_eq!(std::fs::read(path).unwrap(), initial);
    let stopped = host
        .execute(Request::StopPlaytest { diagnostics: None })
        .unwrap();
    assert_eq!(stopped.zone_head, applied.zone_head);
    assert!(stopped.dirty);
    verify_chosen_playtest_start(host, &applied, path, &initial);
    host.execute(Request::Save).unwrap();
    let saved: serde_json::Value = serde_json::from_slice(&std::fs::read(path).unwrap()).unwrap();
    assert_eq!(saved["version"], 6);
    host.execute(Request::Close).unwrap();
    let reopened = host.execute(Request::Open { area_id }).unwrap();
    assert_eq!(reopened.zone_head, applied.zone_head);
    assert_eq!(
        reopened.zone_head.unwrap().corrections.placements.unwrap(),
        vec![placement]
    );
    assert!(!reopened.dirty);
    host.execute(Request::Close).unwrap();
}

fn verify_chosen_playtest_start(
    host: &mut AuthoringHost,
    applied: &forge_project::authoring::AuthoringState,
    path: &std::path::Path,
    initial: &[u8],
) {
    use forge_project::authoring::playtest::{PlaytestSnapshot, PlaytestStart};
    let hash = applied.zone_head.as_ref().unwrap().content_hash().unwrap();
    let start = PlaytestStart {
        x_m: -84.0,
        z_m: 90.0,
        facing_degrees: Some(180.0),
    };
    let request = Request::StartPlaytestAt {
        expected_hash: hash,
        start,
    };
    let wire = serde_json::to_string(&request).unwrap();
    let playing = host.execute(serde_json::from_str(&wire).unwrap()).unwrap();
    assert_eq!(playing.playtest_start, Some(start));
    let remounted = host.execute(Request::Inspect).unwrap();
    let decoded = serde_json::from_slice(&serde_json::to_vec(&remounted).unwrap()).unwrap();
    assert_eq!(
        PlaytestSnapshot::from_state(&decoded)
            .unwrap()
            .unwrap()
            .start(),
        Some(start)
    );
    assert_eq!(remounted.zone_head, applied.zone_head);
    assert_eq!(std::fs::read(path).unwrap(), initial);
    host.execute(Request::StopPlaytest { diagnostics: None })
        .unwrap();
    for (expected_hash, point) in [
        (
            hash,
            PlaytestStart {
                x_m: 900.0,
                z_m: 0.0,
                facing_degrees: None,
            },
        ),
        (forge_core::ContentHash::from_bytes([0; 32]), start),
    ] {
        assert!(
            host.execute(Request::StartPlaytestAt {
                expected_hash,
                start: point
            })
            .is_err()
        );
        let current = host.execute(Request::Inspect).unwrap();
        assert!(current.playtest_hash.is_none());
        assert!(current.playtest_start.is_none());
        assert_eq!(current.zone_head, applied.zone_head);
    }
    assert_eq!(std::fs::read(path).unwrap(), initial);
}
