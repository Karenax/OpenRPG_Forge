use forge_core::CommandId;
use forge_project::{
    authoring::new_terrain,
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest, TerrainEditSession},
    zone_authoring::{GeneratorStamp, migration::ZoneMigrationProposal},
};

fn edit(session: &mut TerrainEditSession, action: TerrainAction) {
    let request = TerrainEditRequest {
        command_id: CommandId::deterministic(format!("migration-{}", session.audit().len())),
        expected_hash: session.document().content_hash().unwrap(),
        expected_revision: session.document().revision,
        action,
    };
    session.execute(&request).unwrap();
}

#[test]
fn migration_preserves_current_state_original_bytes_and_historical_undo() {
    let mut session =
        TerrainEditSession::new(new_terrain("migration", "zone", "Initial").unwrap()).unwrap();
    edit(
        &mut session,
        TerrainAction::Edit(TerrainEdit::Rename {
            name: "Retouchée".into(),
        }),
    );
    edit(
        &mut session,
        TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 8.0 }),
    );
    let archive = session.archive_bytes().unwrap();
    let hash = session.document().content_hash().unwrap();
    let stamp = GeneratorStamp {
        name: "legacy-accepted-checkpoint".into(),
        version: "1".into(),
        inputs_hash: session.initial_document().content_hash().unwrap(),
    };
    let proposal = ZoneMigrationProposal::prepare(&archive, hash, stamp.clone(), None).unwrap();
    assert_eq!(proposal.legacy_archive(), archive);
    let mut expected = session.document().clone();
    expected.schema_version = 3;
    assert_eq!(proposal.head().effective().unwrap().0, expected);
    assert_eq!(
        proposal.head().corrections.name.as_deref(),
        Some("Retouchée")
    );
    assert_eq!(
        proposal
            .head()
            .corrections
            .base_elevation_m
            .map(f32::to_bits),
        Some(8.0_f32.to_bits())
    );
    assert_eq!(
        session.archive_bytes().unwrap(),
        archive,
        "dry-run must not migrate the source"
    );
    let restored = ZoneMigrationProposal::from_bytes(&proposal.to_bytes().unwrap()).unwrap();
    assert_eq!(restored.head(), proposal.head());
    let mut historical = TerrainEditSession::from_archive_bytes(restored.legacy_archive()).unwrap();
    edit(&mut historical, TerrainAction::Undo);
    assert_eq!(
        historical
            .document()
            .height_sampling
            .base_elevation_m
            .to_bits(),
        0.0_f32.to_bits()
    );
    assert_eq!(historical.document().name, "Retouchée");
    assert!(
        ZoneMigrationProposal::prepare(
            &archive,
            historical.document().content_hash().unwrap(),
            stamp,
            None
        )
        .is_err()
    );
    let mut tampered: serde_json::Value =
        serde_json::from_slice(&proposal.to_bytes().unwrap()).unwrap();
    tampered["head"]["corrections"]["name"] = "Lost edit".into();
    assert!(ZoneMigrationProposal::from_bytes(&serde_json::to_vec(&tampered).unwrap()).is_err());
}
