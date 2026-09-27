use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, Vec3};
use forge_project::{
    authoring::new_terrain,
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp,
        migration::ZoneMigrationProposal,
        placements::{ManualPlacement, PlacementEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};
use forge_world::{TerrainGenerationPlan, placement_snap::PlacementSnap};

#[test]
fn snapped_placement_is_revision_bound_undoable_and_persisted_as_absolute_transform() {
    let terrain = new_terrain("snap-session", "zone", "Zone").unwrap();
    let hash = terrain.content_hash().unwrap();
    let legacy = TerrainEditSession::new(terrain).unwrap();
    let mut session = ZoneEditSession::from_migration(
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
    let before = session.head().clone();
    let terrain = TerrainGenerationPlan::from_document(&before.effective().unwrap().0).unwrap();
    let transform = PlacementSnap {
        grid_step_m: Some(0.5),
        surface_offset_m: Some(0.25),
    }
    .resolve(
        Transform::from_translation(Vec3::new(-1.25, 99.0, 1.25)),
        &terrain,
    )
    .unwrap();
    let placement = ManualPlacement {
        id: EntityId::deterministic("snapped"),
        name: "Fixture".into(),
        asset_id: AssetId::deterministic("fixture"),
        asset_hash: ContentHash::from_bytes([1; 32]),
        transform,
        locked: false,
    };
    let request = before
        .prepare_placement(
            CommandId::deterministic("insert-snap"),
            PlacementEdit::Insert {
                placement: placement.clone(),
            },
        )
        .unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    for (key, action) in [("undo", ZoneAction::Undo), ("redo", ZoneAction::Redo)] {
        let command = ZoneRequest {
            command_id: CommandId::deterministic(key),
            expected_revision: session.head().revision,
            expected_hash: session.head().content_hash().unwrap(),
            action,
        };
        session.execute(&command).unwrap();
        if key == "undo" {
            assert_eq!(session.head().corrections, before.corrections);
        }
    }
    let mut corrections = session.head().corrections.clone();
    corrections.base_elevation_m = Some(12.0);
    session
        .execute(&ZoneRequest {
            command_id: CommandId::deterministic("raise-terrain"),
            expected_revision: session.head().revision,
            expected_hash: session.head().content_hash().unwrap(),
            action: ZoneAction::edit(corrections),
        })
        .unwrap();
    let reopened =
        ZoneEditSession::from_archive_bytes(&session.compact_archive_bytes().unwrap()).unwrap();
    assert_eq!(reopened.head(), session.head());
    assert_eq!(
        reopened.head().corrections.placements.as_ref().unwrap(),
        &vec![placement]
    );
    let mut stale = request;
    stale.command_id = CommandId::deterministic("stale-snap");
    assert!(session.execute(&stale).is_err());
    assert_eq!(reopened.head(), session.head());
}
