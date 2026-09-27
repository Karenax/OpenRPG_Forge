use forge_world::{
    VisualValleyVegetationRecipe, vegetation_density_brush::VegetationDensityBrushStroke,
};

#[test]
fn density_strength_blends_both_directions_and_rejects_invalid_values() {
    let mut source = VisualValleyVegetationRecipe::default().build().unwrap();
    let mut stroke = VegetationDensityBrushStroke {
        layer_id: source.layers[0].id.clone(),
        radius_m: 12.0,
        strength: 0.25,
        target_density: 0,
        points: vec![[0.0, 0.0]],
    };
    let before = source.clone();
    let mask = stroke.prepare_mask(&source).unwrap();
    assert_eq!(mask.samples_u8[128 * 257 + 128], 191);
    assert_eq!(mask.samples_u8[0], 255);
    assert_eq!(source, before);
    source.layers[0].density_mask = Some(mask);
    stroke.target_density = 255;
    let restored = stroke.prepare_mask(&source).unwrap();
    assert_eq!(restored.samples_u8[128 * 257 + 128], 207);
    assert_eq!(restored.samples_u8[0], 255);
    for strength in [0.0, -1.0, 1.01, f32::NAN, f32::INFINITY] {
        stroke.strength = strength;
        assert!(stroke.prepare_mask(&source).is_err());
    }
}

#[test]
fn density_stroke_is_local_deterministic_and_preserves_source() {
    let source = VisualValleyVegetationRecipe::default().build().unwrap();
    let before = source.clone();
    let stroke = VegetationDensityBrushStroke {
        layer_id: source.layers[0].id.clone(),
        radius_m: 12.0,
        strength: 1.0,
        target_density: 0,
        points: vec![[0.0, 0.0], [12.0, 0.0]],
    };
    let mask = stroke.prepare_mask(&source).unwrap();
    assert_eq!(mask, stroke.prepare_mask(&source).unwrap());
    assert_eq!(source, before);
    let mut divided = stroke.clone();
    divided.points = vec![[0.0, 0.0], [6.0, 0.0], [12.0, 0.0]];
    assert_eq!(mask, divided.prepare_mask(&source).unwrap());
    assert_eq!(mask.vertices_per_axis, 257);
    assert_eq!(mask.samples_u8[128 * 257 + 128], 0);
    assert_eq!(mask.samples_u8[0], 255);
    let mut candidate = source.clone();
    candidate.layers[0].density_mask = Some(mask);
    candidate.validate().unwrap();
    assert_eq!(&candidate.layers[1..], &source.layers[1..]);
    assert_eq!(candidate.seed, source.seed);
}

#[test]
fn density_overlay_is_bounded_pure_and_rejects_stale_receipts() {
    use forge_world::vegetation_density_preview::DensityMaskPreview;
    let session = zone_session();
    let head = session.head();
    let original = head.content_hash().unwrap();
    let stroke = VegetationDensityBrushStroke {
        layer_id: head.base.vegetation.as_ref().unwrap().layers[0].id.clone(),
        radius_m: 128.0,
        strength: 1.0,
        target_density: 0,
        points: vec![[0.0, 0.0]],
    };
    let request = stroke
        .prepare_request(
            head,
            forge_core::CommandId::deterministic("density-preview"),
        )
        .unwrap();
    let receipt = session.preview(&request).unwrap();
    let preview = DensityMaskPreview::prepare(head, &receipt)
        .unwrap()
        .unwrap();
    assert!(preview.changed_samples > 2048);
    assert!(preview.marks.len() <= 2048);
    assert!(
        preview
            .marks
            .iter()
            .all(|(p, weight)| p.iter().all(|v| v.is_finite()) && (0.0..=1.0).contains(weight))
    );
    assert_eq!(
        preview.marks,
        DensityMaskPreview::prepare(head, &receipt)
            .unwrap()
            .unwrap()
            .marks
    );
    assert_eq!(head.content_hash().unwrap(), original);
    let mut stale = receipt;
    stale.request.expected_revision = head.revision.next().unwrap();
    assert!(DensityMaskPreview::prepare(head, &stale).is_err());
}

#[test]
fn preserves_existing_grid_and_rejects_invalid_strokes() {
    let mut source = VisualValleyVegetationRecipe::default().build().unwrap();
    source.layers[0].density_mask = Some(forge_project::VegetationDensityMask {
        vertices_per_axis: 3,
        samples_u8: vec![100; 9],
    });
    let mut stroke = VegetationDensityBrushStroke {
        layer_id: source.layers[0].id.clone(),
        radius_m: 12.0,
        strength: 1.0,
        target_density: 255,
        points: vec![[0.0, 0.0]],
    };
    let mask = stroke.prepare_mask(&source).unwrap();
    assert_eq!(
        mask.samples_u8,
        vec![100, 100, 100, 100, 255, 100, 100, 100, 100]
    );
    assert_eq!(mask.vertices_per_axis, 3);
    stroke.strength = f32::NAN;
    assert!(stroke.prepare_mask(&source).is_err());
    stroke.strength = 1.0;
    stroke.layer_id = forge_project::VegetationLayerId::new("missing");
    assert!(stroke.prepare_mask(&source).is_err());
}

fn zone_session() -> forge_project::zone_authoring::session::ZoneEditSession {
    use forge_project::{
        authoring::new_terrain,
        terrain_edit::TerrainEditSession,
        zone_authoring::{
            GeneratorStamp, migration::ZoneMigrationProposal, session::ZoneEditSession,
        },
    };
    let terrain = new_terrain("density-authoring", "zone", "Terrain conservé").unwrap();
    let mut vegetation = VisualValleyVegetationRecipe::default().build().unwrap();
    vegetation.project_id = terrain.project_id;
    vegetation.area_id = terrain.area_id;
    for layer in &mut vegetation.layers {
        layer.material_filter = None;
    }
    vegetation.layers[0].density_mask = Some(forge_project::VegetationDensityMask {
        vertices_per_axis: 3,
        samples_u8: vec![255; 9],
    });
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

fn edit_zone(
    session: &mut forge_project::zone_authoring::session::ZoneEditSession,
    edit: forge_project::zone_authoring::vegetation_commands::VegetationEdit,
) {
    let id =
        forge_core::CommandId::deterministic(format!("density-edit-{}", session.audit().len()));
    let request = session.head().prepare_vegetation_edit(edit, id).unwrap();
    session.execute(&request).unwrap();
}

#[test]
fn brush_transaction_lock_clear_and_reopen_preserve_other_layers() {
    use forge_project::zone_authoring::{
        session::ZoneEditSession, vegetation_commands::VegetationEdit,
    };
    let mut session = zone_session();
    let initial = session.head().effective().unwrap();
    let vegetation = initial.1.unwrap();
    let id = vegetation.layers[0].id.clone();
    let stroke = VegetationDensityBrushStroke {
        layer_id: id.clone(),
        radius_m: 12.0,
        strength: 1.0,
        target_density: 0,
        points: vec![[0.0, 0.0]],
    };
    let request = stroke
        .prepare_request(
            session.head(),
            forge_core::CommandId::deterministic("paint-clearing"),
        )
        .unwrap();
    let unchanged = session.archive_bytes().unwrap();
    let preview = session.preview(&request).unwrap();
    assert_eq!(unchanged, session.archive_bytes().unwrap());
    assert_eq!(session.execute(&request).unwrap(), preview);
    let once = session.archive_bytes().unwrap();
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(once, session.archive_bytes().unwrap());
    let painted = session.head().effective().unwrap().1.unwrap();
    assert_eq!(
        painted.layers[0].density_mask.as_ref().unwrap().samples_u8[4],
        0
    );
    assert_eq!(&painted.layers[1..], &vegetation.layers[1..]);
    assert_eq!(painted.seed, vegetation.seed);
    assert_eq!(session.head().base.terrain.name, initial.0.name);

    edit_zone(
        &mut session,
        VegetationEdit::SetDensityLock {
            layer_id: id.clone(),
            locked: true,
        },
    );
    assert_lock_enforced(&mut session, &id);
    edit_zone(
        &mut session,
        VegetationEdit::SetDensityLock {
            layer_id: id.clone(),
            locked: false,
        },
    );
    assert_eq!(
        session.head().effective().unwrap().1.unwrap().layers[0].density_mask,
        painted.layers[0].density_mask
    );
    edit_zone(
        &mut session,
        VegetationEdit::ClearDensityOverride { layer_id: id },
    );
    assert_eq!(
        session.head().effective().unwrap().1.unwrap().layers[0].density_mask,
        vegetation.layers[0].density_mask
    );
    let undo = session
        .head()
        .prepare_terrain_action(
            forge_project::terrain_edit::TerrainAction::Undo,
            forge_core::CommandId::deterministic("undo-density-clear"),
        )
        .unwrap();
    session.execute(&undo).unwrap();
    let restored = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(restored.head(), session.head());
    assert_eq!(
        restored.head().effective().unwrap().1.unwrap().layers[0].density_mask,
        painted.layers[0].density_mask
    );
}

fn assert_lock_enforced(
    session: &mut forge_project::zone_authoring::session::ZoneEditSession,
    id: &forge_project::VegetationLayerId,
) {
    use forge_project::zone_authoring::{
        session::{ZoneAction, ZoneRequest},
        vegetation_commands::VegetationEdit,
    };
    let locked = session.archive_bytes().unwrap();
    let mut replacement = session.head().corrections.clone();
    replacement.density[0].mask.samples_u8[4] = 255;
    let bypass = ZoneRequest {
        command_id: forge_core::CommandId::deterministic("bypass-lock"),
        expected_hash: session.head().content_hash().unwrap(),
        expected_revision: session.head().revision,
        action: ZoneAction::edit(replacement),
    };
    assert!(
        session.execute(&bypass).is_err(),
        "raw command cannot bypass the lock"
    );
    assert!(
        session
            .head()
            .prepare_vegetation_edit(
                VegetationEdit::ClearDensityOverride {
                    layer_id: id.clone()
                },
                forge_core::CommandId::deterministic("clear-locked")
            )
            .is_err()
    );
    assert_eq!(locked, session.archive_bytes().unwrap());
}
