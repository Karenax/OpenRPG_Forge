use forge_project::{
    authoring::new_terrain,
    terrain_edit::{TerrainEditSession, file_session::FileTerrainSession},
    zone_authoring::{
        GeneratorStamp, file_session::FileZoneSession, migration::ZoneMigrationProposal,
    },
};

#[test]
fn aggregate_migration_replaces_same_file_only_on_explicit_save() {
    let directory = std::env::temp_dir().join(format!("forge-zone-migrate-{}", std::process::id()));
    std::fs::create_dir(&directory).unwrap();
    let mut legacy = FileTerrainSession::create(
        &directory,
        new_terrain("file-zone", "test", "Original").unwrap(),
    )
    .unwrap();
    legacy.save().unwrap();
    let hash = legacy.session().document().content_hash().unwrap();
    let bytes = std::fs::read(directory.join("authoring.json")).unwrap();
    let proposal = ZoneMigrationProposal::prepare(
        &bytes,
        hash,
        GeneratorStamp {
            name: "accepted-legacy".into(),
            version: "1".into(),
            inputs_hash: hash,
        },
        None,
    )
    .unwrap();
    assert!(
        FileZoneSession::prepare_migration(&directory, &proposal).is_err(),
        "old session still owns lock"
    );
    drop(legacy);
    assert!(
        FileZoneSession::open(&directory).is_err(),
        "opening cannot migrate implicitly"
    );
    let mut migrated = FileZoneSession::prepare_migration(&directory, &proposal).unwrap();
    assert_eq!(
        std::fs::read(directory.join("authoring.json")).unwrap(),
        bytes
    );
    migrated.save().unwrap();
    let expected = migrated.session().head().content_hash().unwrap();
    drop(migrated);
    let reopened = FileZoneSession::open(&directory).unwrap();
    assert_eq!(reopened.session().head().content_hash().unwrap(), expected);
    assert_eq!(
        reopened.session().head().effective().unwrap().0.name,
        "Original"
    );
    assert_eq!(
        TerrainEditSession::from_archive_bytes(proposal.legacy_archive())
            .unwrap()
            .document()
            .content_hash()
            .unwrap(),
        hash
    );
    assert!(!directory.join("authoring.pending").exists());
    drop(reopened);
    std::fs::remove_dir_all(directory).unwrap();
}
