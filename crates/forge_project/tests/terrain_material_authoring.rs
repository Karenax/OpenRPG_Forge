use forge_core::CommandId;
use forge_project::{
    TerrainMaterialOverride, TerrainMaterialWeight,
    authoring::new_terrain,
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest, TerrainEditSession},
};

fn request(session: &TerrainEditSession, id: &str, action: TerrainAction) -> TerrainEditRequest {
    TerrainEditRequest {
        command_id: CommandId::deterministic(id),
        expected_revision: session.document().revision,
        expected_hash: session.document().content_hash().unwrap(),
        action,
    }
}

fn sample(session: &TerrainEditSession) -> TerrainMaterialOverride {
    TerrainMaterialOverride {
        sample_x: 32,
        sample_z: 32,
        weights: vec![TerrainMaterialWeight {
            layer_id: session.document().material_layers[1].id.clone(),
            weight: u16::MAX,
        }],
    }
}

#[test]
fn material_save_close_open_preserves_paint_relief_and_undo() {
    use forge_project::terrain_edit::file_session::FileTerrainSession;
    let directory =
        std::env::temp_dir().join(format!("forge-material-disk-{}", std::process::id()));
    std::fs::create_dir(&directory).unwrap();
    let mut file =
        FileTerrainSession::create(&directory, new_terrain("disk", "paint", "Paint").unwrap())
            .unwrap();
    let migration = request(
        file.session(),
        "disk-migrate",
        TerrainAction::Edit(TerrainEdit::SetSchemaVersion { version: 3 }),
    );
    file.execute(&migration).unwrap();
    let paint = sample(file.session());
    let command = request(
        file.session(),
        "disk-paint",
        TerrainAction::Edit(TerrainEdit::SetMaterialOverrides {
            samples: vec![paint.clone()],
        }),
    );
    file.execute(&command).unwrap();
    let relief = request(
        file.session(),
        "disk-relief",
        TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 8.0 }),
    );
    file.execute(&relief).unwrap();
    file.save().unwrap();
    let expected = file.session().document().clone();
    let archive = std::fs::read(directory.join("authoring.json")).unwrap();
    drop(file);
    let mut reopened = FileTerrainSession::open(&directory).unwrap();
    assert_eq!(reopened.session().document(), &expected);
    assert!(!reopened.is_dirty());
    let undo = request(reopened.session(), "disk-undo", TerrainAction::Undo);
    reopened.execute(&undo).unwrap();
    assert_eq!(
        reopened.session().document().material_overrides,
        vec![paint]
    );
    assert_eq!(
        std::fs::read(directory.join("authoring.json")).unwrap(),
        archive,
        "Undo is not an implicit save"
    );
    let redo = request(reopened.session(), "disk-redo", TerrainAction::Redo);
    reopened.execute(&redo).unwrap();
    reopened.save().unwrap();
    let hash = reopened.session().document().content_hash().unwrap();
    drop(reopened);
    let final_open = FileTerrainSession::open(&directory).unwrap();
    assert_eq!(
        final_open.session().document().content_hash().unwrap(),
        hash
    );
    assert!(!directory.join("authoring.pending").exists());
    drop(final_open);
    std::fs::remove_dir_all(directory).unwrap();
}

#[test]
fn material_migration_preserves_legacy_history_and_undo_replay() {
    let doc = new_terrain("paint", "zone", "Original").unwrap();
    let mut session = TerrainEditSession::new(doc).unwrap();
    let rename = request(
        &session,
        "rename",
        TerrainAction::Edit(TerrainEdit::Rename {
            name: "Retouche".into(),
        }),
    );
    session.execute(&rename).unwrap();
    let legacy = session.archive_bytes().unwrap();
    assert_eq!(
        TerrainEditSession::from_archive_bytes(&legacy)
            .unwrap()
            .archive_bytes()
            .unwrap(),
        legacy
    );
    assert!(
        !String::from_utf8(legacy.clone())
            .unwrap()
            .contains("material_overrides")
    );
    let paint = TerrainEdit::SetMaterialOverrides {
        samples: vec![sample(&session)],
    };
    assert!(
        session
            .execute(&request(
                &session,
                "unmigrated",
                TerrainAction::Edit(paint.clone())
            ))
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), legacy);
    let migration = request(
        &session,
        "migration",
        TerrainAction::Edit(TerrainEdit::SetSchemaVersion { version: 3 }),
    );
    let receipt = session.preview(&migration).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), legacy);
    assert_eq!(session.execute(&migration).unwrap(), receipt);
    assert_eq!(session.audit().len(), 2);
    let command = request(&session, "paint", TerrainAction::Edit(paint));
    session.execute(&command).unwrap();
    let painted = session.document().material_overrides.clone();
    let height = request(
        &session,
        "height",
        TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: 12.0 }),
    );
    session.execute(&height).unwrap();
    assert_eq!(session.document().material_overrides, painted);
    assert_eq!(
        session
            .document()
            .migrate_to_latest()
            .unwrap()
            .material_overrides,
        painted
    );
    let bytes = session.archive_bytes().unwrap();
    assert_eq!(
        serde_json::from_slice::<serde_json::Value>(&bytes).unwrap()["version"],
        2
    );
    let mut reopened = TerrainEditSession::from_archive_bytes(&bytes).unwrap();
    assert_eq!(reopened.archive_bytes().unwrap(), bytes);
    let retry = reopened.execute(&command).unwrap();
    assert_eq!(retry.request, command);
    assert_eq!(reopened.archive_bytes().unwrap(), bytes);
    let downgrade = request(
        &reopened,
        "downgrade",
        TerrainAction::Edit(TerrainEdit::SetSchemaVersion { version: 2 }),
    );
    assert!(reopened.execute(&downgrade).is_err());
    assert_eq!(reopened.archive_bytes().unwrap(), bytes);
    for id in ["undo-height", "undo-paint", "undo-migration"] {
        reopened
            .execute(&request(&reopened, id, TerrainAction::Undo))
            .unwrap();
    }
    assert_eq!(reopened.document().schema_version, 2);
    assert!(reopened.document().material_overrides.is_empty());
    assert_eq!(reopened.document().name, "Retouche");
    let restored = reopened.archive_bytes().unwrap();
    let mut reopened = TerrainEditSession::from_archive_bytes(&restored).unwrap();
    reopened
        .execute(&request(&reopened, "redo-migration", TerrainAction::Redo))
        .unwrap();
    reopened
        .execute(&request(&reopened, "redo-paint", TerrainAction::Redo))
        .unwrap();
    assert_eq!(reopened.document().material_overrides, painted);
}

#[test]
fn material_input_rejections_and_archive_limit_are_non_mutating() {
    let mut doc = new_terrain("paint", "limits", "Original").unwrap();
    doc.schema_version = 3;
    let mut session = TerrainEditSession::new(doc).unwrap();
    let initial = session.archive_bytes().unwrap();
    let valid = sample(&session);
    let mut invalid = vec![];
    let mut zero = valid.clone();
    zero.weights[0].weight = 0;
    invalid.push(vec![zero]);
    let mut sum = valid.clone();
    sum.weights[0].weight = 1;
    invalid.push(vec![sum]);
    let mut unknown = valid.clone();
    unknown.weights[0].layer_id = forge_project::TerrainMaterialLayerId::new("missing");
    invalid.push(vec![unknown]);
    let mut outside = valid.clone();
    outside.sample_x = 257;
    invalid.push(vec![outside]);
    invalid.push(vec![valid.clone(), valid.clone()]);
    let mut duplicate = valid.clone();
    duplicate.weights.push(duplicate.weights[0].clone());
    invalid.push(vec![duplicate]);
    for (index, samples) in invalid.into_iter().enumerate() {
        let req = request(
            &session,
            &format!("invalid-{index}"),
            TerrainAction::Edit(TerrainEdit::SetMaterialOverrides { samples }),
        );
        assert!(session.execute(&req).is_err());
        assert_eq!(session.archive_bytes().unwrap(), initial);
    }
    let samples = (0..=256)
        .flat_map(|z| (0..=256).map(move |x| (x, z)))
        .map(|(x, z)| TerrainMaterialOverride {
            sample_x: x,
            sample_z: z,
            weights: valid.weights.clone(),
        })
        .collect();
    let large = request(
        &session,
        "oversize",
        TerrainAction::Edit(TerrainEdit::SetMaterialOverrides { samples }),
    );
    assert!(session.preview(&large).is_err());
    assert!(session.execute(&large).is_err());
    assert_eq!(session.archive_bytes().unwrap(), initial);
}
