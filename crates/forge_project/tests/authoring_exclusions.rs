use forge_core::CommandId;
use forge_project::{
    VegetationDocument, VegetationExclusion, VegetationExclusionId,
    authoring::new_terrain,
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp,
        exclusions::VegetationExclusionEdit,
        migration::ZoneMigrationProposal,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn session() -> ZoneEditSession {
    let terrain = new_terrain("exclusions", "zone", "Zone").unwrap();
    let hash = terrain.content_hash().unwrap();
    let mut vegetation: VegetationDocument = serde_json::from_str(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = terrain.project_id;
    vegetation.area_id = terrain.area_id;
    for layer in &mut vegetation.layers {
        layer.material_filter = None;
    }
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

fn circle(id: impl Into<String>) -> VegetationExclusion {
    VegetationExclusion::Circle {
        id: VegetationExclusionId::new(id),
        center_x_m: 0.0,
        center_z_m: 0.0,
        radius_m: 8.0,
    }
}

fn command(session: &ZoneEditSession, id: &str, edit: VegetationExclusionEdit) -> ZoneRequest {
    session
        .head()
        .prepare_vegetation_exclusions(edit, CommandId::deterministic(id))
        .unwrap()
}

fn history_command(session: &ZoneEditSession, action: ZoneAction) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(format!("history-{}", session.audit().len())),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    }
}

#[test]
fn exclusions_inherit_or_replace_the_recipe_including_explicit_empty() {
    let mut session = session();
    let inherited = session
        .head()
        .base
        .vegetation
        .as_ref()
        .unwrap()
        .exclusions
        .clone();
    assert_eq!(
        session.head().effective().unwrap().1.unwrap().exclusions,
        inherited
    );

    let request = command(
        &session,
        "empty",
        VegetationExclusionEdit::Replace {
            exclusions: Vec::new(),
        },
    );
    session.execute(&request).unwrap();
    assert_eq!(session.head().version, 9);
    assert_eq!(
        session.head().corrections.vegetation_exclusions,
        Some(Vec::new())
    );
    assert!(
        session
            .head()
            .effective()
            .unwrap()
            .1
            .unwrap()
            .exclusions
            .is_empty()
    );

    let restore = command(
        &session,
        "inherit",
        VegetationExclusionEdit::RestoreInheritance,
    );
    session.execute(&restore).unwrap();
    assert!(session.head().corrections.vegetation_exclusions.is_none());
    assert_eq!(
        session.head().effective().unwrap().1.unwrap().exclusions,
        inherited
    );
}

#[test]
fn exclusion_validation_keeps_canonical_duplicate_and_quota_rules() {
    let session = session();
    assert!(
        session
            .head()
            .prepare_vegetation_exclusions(
                VegetationExclusionEdit::Replace {
                    exclusions: vec![circle("same"), circle("same")],
                },
                CommandId::deterministic("duplicate"),
            )
            .is_err()
    );
    let quota = (0..33)
        .map(|index| circle(format!("circle_{index}")))
        .collect();
    assert!(
        session
            .head()
            .prepare_vegetation_exclusions(
                VegetationExclusionEdit::Replace { exclusions: quota },
                CommandId::deterministic("quota"),
            )
            .is_err()
    );
}

#[test]
fn exclusion_edit_retries_undoes_redoes_and_roundtrips_all_archives() {
    let mut session = session();
    let request = command(
        &session,
        "circle",
        VegetationExclusionEdit::Replace {
            exclusions: vec![circle("author_circle")],
        },
    );
    let preview = session.preview(&request).unwrap();
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(
        session.execute(&request).unwrap(),
        preview,
        "retry is idempotent"
    );
    assert_eq!(session.audit().len(), 1);

    for (bytes, version) in [
        (session.archive_bytes().unwrap(), 28),
        (session.compact_archive_bytes().unwrap(), 29),
        (session.delta_archive_bytes().unwrap(), 30),
    ] {
        let json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(json["version"], version);
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(reopened.audit(), session.audit());
    }

    let undo = history_command(&session, ZoneAction::Undo);
    session.execute(&undo).unwrap();
    assert!(session.head().corrections.vegetation_exclusions.is_none());
    let redo = history_command(&session, ZoneAction::Redo);
    session.execute(&redo).unwrap();
    assert_eq!(
        session.head().corrections.vegetation_exclusions,
        Some(vec![circle("author_circle")])
    );
}
