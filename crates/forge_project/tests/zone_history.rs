use forge_core::CommandId;
use forge_project::{
    VegetationDensityMask, VegetationDocument,
    authoring::{
        history::{self, HistoryAction, HistoryChange, zone::ZoneHistoryChange},
        new_terrain,
    },
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        DensityOverride, GeneratorStamp,
        migration::ZoneMigrationProposal,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn session() -> ZoneEditSession {
    let terrain = new_terrain("zone-history", "zone", "Source").unwrap();
    let mut vegetation: VegetationDocument = serde_json::from_str(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = terrain.project_id;
    vegetation.area_id = terrain.area_id;
    for layer in &mut vegetation.layers {
        layer.material_filter = None;
    }
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
            Some(vegetation),
        )
        .unwrap(),
    )
    .unwrap()
}

fn apply(session: &mut ZoneEditSession, action: ZoneAction) {
    let request = ZoneRequest {
        command_id: CommandId::deterministic(format!("history-{}", session.audit().len())),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    };
    session.execute(&request).unwrap();
}

fn changes(session: &ZoneEditSession) -> [ZoneHistoryChange; 1] {
    let page = history::zone::read(session, session.head().content_hash().unwrap(), 0).unwrap();
    let HistoryChange::Zone { changes } = &page.entries[0].change else {
        panic!("aggregate summary required")
    };
    changes.clone().try_into().unwrap()
}

#[test]
fn object_history_records_empty_layers_path_updates_removal_and_undo() {
    use forge_project::authoring::history::zone::objects::ObjectDomain;
    use forge_project::zone_authoring::paths::{AuthoredPath, PathPoint};
    let mut session = session();
    let mut c = session.head().corrections.clone();
    c.buildings = Some(Vec::new());
    c.paths = Some(Vec::new());
    c.rivers = Some(Vec::new());
    apply(&mut session, ZoneAction::edit(c));
    let page = history::zone::read(&session, session.head().content_hash().unwrap(), 0).unwrap();
    let HistoryChange::Zone { changes } = &page.entries[0].change else {
        panic!("zone")
    };
    assert_eq!(changes.len(), 3);
    for change in changes {
        assert!(
            matches!(change, ZoneHistoryChange::Objects { before_explicit: false, after_explicit: true, changes, .. } if changes.is_empty())
        );
    }
    let id = forge_core::EntityId::deterministic("history-path");
    let mut c = session.head().corrections.clone();
    c.paths = Some(vec![AuthoredPath {
        id,
        name: "Chemin".into(),
        points: vec![
            PathPoint { x_m: 0.0, z_m: 0.0 },
            PathPoint {
                x_m: 10.0,
                z_m: 0.0,
            },
        ],
        width_m: 3.0,
        shoulder_m: 1.0,
        profile: None,
        locked: false,
    }]);
    apply(&mut session, ZoneAction::edit(c));
    let mut c = session.head().corrections.clone();
    c.paths.as_mut().unwrap()[0].width_m = 4.0;
    apply(&mut session, ZoneAction::edit(c));
    let before = session.archive_bytes().unwrap();
    let page = history::zone::read(&session, session.head().content_hash().unwrap(), 0).unwrap();
    let HistoryChange::Zone { changes } = &page.entries[0].change else {
        panic!("zone")
    };
    let [
        ZoneHistoryChange::Objects {
            domain: ObjectDomain::Paths,
            changes,
            ..
        },
    ] = changes.as_slice()
    else {
        panic!("path")
    };
    assert_eq!(changes.len(), 1);
    assert_eq!(changes[0].id, id);
    assert_ne!(
        changes[0].before.as_ref().unwrap().hash,
        changes[0].after.as_ref().unwrap().hash
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
    let mut c = session.head().corrections.clone();
    c.paths = Some(Vec::new());
    apply(&mut session, ZoneAction::edit(c));
    apply(&mut session, ZoneAction::Undo);
    let bytes = session.archive_bytes().unwrap();
    let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
    let page = history::zone::read(&reopened, reopened.head().content_hash().unwrap(), 0).unwrap();
    assert_eq!(page.entries[0].action, HistoryAction::Undo);
    let HistoryChange::Zone { changes } = &page.entries[0].change else {
        panic!("zone")
    };
    let [ZoneHistoryChange::Objects { changes, .. }] = changes.as_slice() else {
        panic!("objects")
    };
    assert!(changes[0].before.is_none());
    assert!(changes[0].after.is_some());
    assert_eq!(reopened.archive_bytes().unwrap(), bytes);
}

#[test]
fn aggregate_history_distinguishes_mask_content_lock_and_recipe_without_arrays() {
    let mut session = session();
    let mut corrections = session.head().corrections.clone();
    corrections.density.push(DensityOverride {
        layer_id: session.head().base.vegetation.as_ref().unwrap().layers[0]
            .id
            .clone(),
        mask: VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0, 255, 255, 0],
        },
        locked: false,
    });
    apply(&mut session, ZoneAction::edit(corrections));
    let mut corrections = session.head().corrections.clone();
    corrections.density[0].mask.samples_u8[0] = 128;
    apply(&mut session, ZoneAction::edit(corrections));
    let [
        ZoneHistoryChange::Density {
            before: Some(before),
            after: Some(after),
            ..
        },
    ] = changes(&session)
    else {
        panic!("density summary required")
    };
    assert_eq!(before.mask.count, after.mask.count);
    assert_ne!(before.mask.hash, after.mask.hash);
    let mut corrections = session.head().corrections.clone();
    corrections.density[0].locked = true;
    apply(&mut session, ZoneAction::edit(corrections));
    let [
        ZoneHistoryChange::Density {
            before: Some(before),
            after: Some(after),
            ..
        },
    ] = changes(&session)
    else {
        panic!("lock summary required")
    };
    assert_eq!(before.mask, after.mask);
    assert!(!before.locked && after.locked);
    let mut base = session.head().base.clone();
    let previous_seed = base.vegetation.as_ref().unwrap().seed;
    base.vegetation.as_mut().unwrap().seed += 1;
    apply(&mut session, ZoneAction::Regenerate(Box::new(base)));
    let [ZoneHistoryChange::Recipe { before, after }] = changes(&session) else {
        panic!("recipe summary required")
    };
    assert_eq!(before.vegetation_seed, Some(previous_seed));
    assert_eq!(after.vegetation_seed, Some(previous_seed + 1));
    apply(&mut session, ZoneAction::Undo);
    let [
        ZoneHistoryChange::Recipe {
            before: undone_before,
            after: undone_after,
        },
    ] = changes(&session)
    else {
        panic!("inverse recipe summary required")
    };
    assert_eq!((undone_before, undone_after), (after, before));
    apply(&mut session, ZoneAction::Redo);
    let bytes = session.compact_archive_bytes().unwrap();
    let head_hash = session.head().content_hash().unwrap();
    let page = history::zone::read(&session, head_hash, 0).unwrap();
    assert_eq!(page.entries[0].action, HistoryAction::Redo);
    assert_eq!(page.entries[1].action, HistoryAction::Undo);
    assert_eq!(page.entries[2].action, HistoryAction::Regenerate);
    let encoded = serde_json::to_string(&page).unwrap();
    assert!(!encoded.contains("samples_u8"));
    let restored = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
    assert_eq!(history::zone::read(&restored, head_hash, 0).unwrap(), page);
    assert_eq!(session.compact_archive_bytes().unwrap(), bytes);
}

#[test]
fn aggregate_pages_are_bounded_read_only_and_reject_stale_or_derived_hashes() {
    let mut session = session();
    let stale = session.head().content_hash().unwrap();
    for i in 0..35 {
        let mut corrections = session.head().corrections.clone();
        corrections.name = Some(format!("Name {i}"));
        apply(&mut session, ZoneAction::edit(corrections));
    }
    let hash = session.head().content_hash().unwrap();
    let bytes = session.archive_bytes().unwrap();
    let first = history::zone::read(&session, hash, 0).unwrap();
    let second = history::zone::read(&session, hash, history::PAGE_SIZE).unwrap();
    assert_eq!(
        (first.total, first.entries.len(), second.entries.len()),
        (35, 32, 3)
    );
    assert_ne!(
        first.entries.last().unwrap().command_id,
        second.entries[0].command_id
    );
    assert!(history::zone::read(&session, stale, 0).is_err());
    assert!(
        history::zone::read(
            &session,
            session
                .head()
                .effective()
                .unwrap()
                .0
                .content_hash()
                .unwrap(),
            0
        )
        .is_err()
    );
    assert!(history::zone::read(&session, hash, usize::MAX).is_err());
    assert!(
        history::zone::read(&session, hash, 35)
            .unwrap()
            .entries
            .is_empty()
    );
    assert_eq!(session.archive_bytes().unwrap(), bytes);
}
