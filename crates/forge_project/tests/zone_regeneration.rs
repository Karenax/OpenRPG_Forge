use forge_core::CommandId;
use forge_project::{
    VegetationDensityMask, VegetationDocument,
    authoring::new_terrain,
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        DensityOverride, GeneratorStamp,
        migration::ZoneMigrationProposal,
        regeneration::RegenerationConflict,
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};

fn session() -> ZoneEditSession {
    let terrain = new_terrain("regeneration", "zone", "Generated").unwrap();
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
    let mut session = ZoneEditSession::from_migration(
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
    .unwrap();
    let mut corrections = session.head().corrections.clone();
    corrections.name = Some("Clairière manuelle".into());
    corrections.density.push(DensityOverride {
        layer_id: session.head().base.vegetation.as_ref().unwrap().layers[0]
            .id
            .clone(),
        mask: VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0, 255, 255, 0],
        },
        locked: true,
    });
    let request = request(&session, ZoneAction::edit(corrections));
    session.execute(&request).unwrap();
    session
}

fn request(session: &ZoneEditSession, action: ZoneAction) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(format!("regen-{}", session.audit().len())),
        expected_hash: session.head().content_hash().unwrap(),
        expected_revision: session.head().revision,
        action,
    }
}

#[test]
fn regeneration_refusal_explains_the_target_without_mutating_or_suggesting_unlock_as_a_fix() {
    let mut session = session();
    let before = session.archive_bytes().unwrap();
    let mut base = session.head().base.clone();
    let layer = session.head().corrections.density[0].layer_id.clone();
    base.vegetation
        .as_mut()
        .unwrap()
        .layers
        .retain(|item| item.id != layer);
    let review = session.head().review_regeneration(base.clone()).unwrap();
    let message = review.refusal_message().unwrap();
    assert!(message.contains("DensityLayerRemoved"));
    assert!(message.contains(&layer.to_string()));
    assert!(message.contains("verrou actif"));
    assert!(message.contains("déverrouiller n’efface pas le masque"));
    assert_eq!(
        session
            .head()
            .prepare_regeneration(base.clone(), CommandId::deterministic("refusal"))
            .unwrap_err(),
        message
    );
    let raw = request(&session, ZoneAction::Regenerate(Box::new(base)));
    assert_eq!(session.execute(&raw).unwrap_err(), message);
    assert_eq!(session.archive_bytes().unwrap(), before);
    assert!(
        session
            .head()
            .review_regeneration(session.head().base.clone())
            .unwrap()
            .refusal_message()
            .is_none()
    );
}

#[test]
fn regeneration_preserves_manual_layers_and_can_undo_redo_after_reopen() {
    let mut session = session();
    let initial = session.head().clone();
    let v1 = session.archive_bytes().unwrap();
    assert_eq!(
        serde_json::from_slice::<serde_json::Value>(&v1).unwrap()["version"],
        1
    );
    assert_eq!(
        ZoneEditSession::from_archive_bytes(&v1)
            .unwrap()
            .archive_bytes()
            .unwrap(),
        v1
    );
    let mut proposed = initial.base.clone();
    proposed.terrain.seed += 1;
    proposed.terrain.name = "New procedural name".into();
    let vegetation = proposed.vegetation.as_mut().unwrap();
    vegetation.seed += 1;
    vegetation.layers.reverse();
    proposed.generator.inputs_hash = proposed.terrain.content_hash().unwrap();
    let review = initial.review_regeneration(proposed.clone()).unwrap();
    assert!(review.conflicts.is_empty());
    assert_eq!(review.retained_corrections, initial.corrections);
    let command = initial
        .prepare_regeneration(
            proposed.clone(),
            CommandId::deterministic("accept-regeneration"),
        )
        .unwrap();
    let preview = session.preview(&command).unwrap();
    assert_eq!(session.archive_bytes().unwrap(), v1);
    assert_eq!(session.execute(&command).unwrap(), preview);
    let after = session.head().clone();
    assert_eq!(after.base, proposed);
    assert_eq!(after.corrections, initial.corrections);
    let effective = after.effective().unwrap();
    assert_eq!(effective.0.name, "Clairière manuelle");
    let correction = &initial.corrections.density[0];
    assert_eq!(
        effective
            .1
            .unwrap()
            .layers
            .iter()
            .find(|l| l.id == correction.layer_id)
            .unwrap()
            .density_mask
            .as_ref(),
        Some(&correction.mask)
    );
    let archive = session.archive_bytes().unwrap();
    assert_eq!(session.execute(&command).unwrap(), preview);
    assert_eq!(session.archive_bytes().unwrap(), archive);
    assert_eq!(
        serde_json::from_slice::<serde_json::Value>(&archive).unwrap()["version"],
        2
    );
    let undo = request(&session, ZoneAction::Undo);
    session.execute(&undo).unwrap();
    assert_eq!(session.head().base, initial.base);
    assert_eq!(session.head().corrections, initial.corrections);
    let mut reopened =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    let redo = request(&reopened, ZoneAction::Redo);
    reopened.execute(&redo).unwrap();
    assert_eq!(reopened.head().base, after.base);
    assert_eq!(reopened.head().corrections, after.corrections);
    let mut forged: serde_json::Value = serde_json::from_slice(&archive).unwrap();
    forged["version"] = 1.into();
    assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&forged).unwrap()).is_err());
    forged["version"] = 2.into();
    forged["receipts"][1]["base_change"]["inverse"]["terrain"]["name"] = "fake".into();
    assert!(ZoneEditSession::from_archive_bytes(&serde_json::to_vec(&forged).unwrap()).is_err());
}

#[test]
fn missing_corrected_layer_and_identity_changes_refuse_without_mutation() {
    let mut session = session();
    let before = session.archive_bytes().unwrap();
    let mut proposed = session.head().base.clone();
    proposed.vegetation.as_mut().unwrap().layers.remove(0);
    let review = session
        .head()
        .review_regeneration(proposed.clone())
        .unwrap();
    assert!(review.effective.is_none());
    assert!(review.conflicts.iter().any(|c| matches!(
        c,
        RegenerationConflict::DensityLayerRemoved { locked: true, .. }
    )));
    let bypass = request(&session, ZoneAction::Regenerate(Box::new(proposed)));
    assert!(session.execute(&bypass).is_err());
    assert_eq!(session.archive_bytes().unwrap(), before);
    let mut wrong_zone = session.head().base.clone();
    wrong_zone.terrain.area_id = forge_core::AreaId::deterministic("foreign");
    assert!(
        session
            .head()
            .review_regeneration(wrong_zone)
            .unwrap()
            .conflicts
            .contains(&RegenerationConflict::ZoneIdentityChanged)
    );
    let mut wrong_grid = session.head().base.clone();
    wrong_grid.terrain.height_sampling.sample_spacing_m = 4.0;
    assert!(
        session
            .head()
            .review_regeneration(wrong_grid)
            .unwrap()
            .conflicts
            .contains(&RegenerationConflict::TerrainGridChanged)
    );
    let mut unlocked = session.head().clone();
    unlocked.corrections.density[0].locked = false;
    let mut absent = unlocked.base.clone();
    absent.vegetation = None;
    assert!(
        unlocked
            .review_regeneration(absent)
            .unwrap()
            .conflicts
            .iter()
            .any(|c| matches!(
                c,
                RegenerationConflict::DensityLayerRemoved { locked: false, .. }
            ))
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn relief_delta_rebase_is_reported_instead_of_silently_moving_manual_height() {
    let mut session = session();
    let mut corrections = session.head().corrections.clone();
    corrections.height_deltas = Some(vec![forge_project::TerrainHeightDelta::new(128, 128, 3.0)]);
    let edit = request(&session, ZoneAction::edit(corrections));
    session.execute(&edit).unwrap();
    let before = session.archive_bytes().unwrap();
    let mut base = session.head().base.clone();
    base.terrain.height_sampling.amplitude_m = 10.0;
    let review = session.head().review_regeneration(base.clone()).unwrap();
    assert!(
        review
            .conflicts
            .contains(&RegenerationConflict::TerrainHeightReferenceChanged)
    );
    assert!(review.effective.is_none());
    assert!(
        session
            .execute(&request(&session, ZoneAction::Regenerate(Box::new(base))))
            .is_err()
    );
    assert_eq!(session.archive_bytes().unwrap(), before);
    let mut vegetation_only = session.head().base.clone();
    vegetation_only.vegetation.as_mut().unwrap().seed += 1;
    assert!(
        session
            .head()
            .review_regeneration(vegetation_only)
            .unwrap()
            .conflicts
            .is_empty()
    );
}
