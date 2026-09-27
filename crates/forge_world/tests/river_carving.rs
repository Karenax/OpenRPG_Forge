use forge_core::Vec3;
use forge_project::{
    RiverControlPoint, RiverElevationPolicy, RiverPathAlgorithm, TerrainDocument,
    TerrainHeightDelta, WaterDocument, authoring::new_terrain, terrain_edit::TerrainEdit,
};
use forge_world::{TerrainGenerationPlan, embedded_visual_valley_water_document, river_carving};

fn fixture() -> (TerrainDocument, WaterDocument) {
    let mut terrain = new_terrain("river", "carving", "River").unwrap();
    terrain.height_sampling.base_elevation_m = 3.0;
    terrain.height_deltas = vec![TerrainHeightDelta::new(200, 200, 7.0)];
    let mut water = embedded_visual_valley_water_document().unwrap();
    water.project_id = terrain.project_id;
    water.area_id = terrain.area_id;
    water.river.algorithm = RiverPathAlgorithm::PiecewiseLinearV1;
    water.river.elevation_policy = RiverElevationPolicy::AuthoredSurfaceV1;
    water.river.control_points = [-30.0, -10.0, 10.0, 30.0]
        .map(|x| RiverControlPoint {
            center_m: Vec3::new(x, 2.0, 0.0),
            surface_width_m: 8.0,
            bed_depth_m: 2.0,
        })
        .to_vec();
    water.banks.shoulder_width_m = 4.0;
    water.banks.blend_width_m = 4.0;
    water.banks.wet_margin_m = 1.0;
    if let forge_project::WaterRefractionProfile::ScreenSpace { max_depth_m, .. } =
        &mut water.optics.refraction
    {
        *max_depth_m = 2.0;
    }
    (terrain, water)
}

fn apply(source: &TerrainDocument, edit: TerrainEdit) -> TerrainDocument {
    let TerrainEdit::SetHeightDeltas { samples } = edit else {
        panic!("height edit required")
    };
    let mut result = source.clone();
    result.height_deltas = samples;
    result
}

#[test]
fn channel_is_below_water_blends_to_bank_and_preserves_outside_retouches() {
    let (terrain, water) = fixture();
    let source_hash = terrain.content_hash().unwrap();
    let water_hash = water.content_hash().unwrap();
    let preview = river_carving::prepare(&terrain, &water, 8.0).unwrap();
    assert_eq!(
        preview,
        river_carving::prepare(&terrain, &water, 8.0).unwrap()
    );
    assert_eq!(preview.terrain_hash, source_hash);
    assert_eq!(preview.water_hash, water_hash);
    assert!(preview.modified_samples > 0);
    let carved = apply(&terrain, preview.edit);
    let plan = TerrainGenerationPlan::from_document(&carved).unwrap();
    assert!(plan.height_at(0.0, 0.0).unwrap().abs() < 0.0001);
    assert!((plan.height_at(0.0, 4.0).unwrap() - 1.5).abs() < 0.0001);
    let bank = plan.height_at(0.0, 8.0).unwrap();
    assert!(bank > 1.5 && bank < 3.0);
    assert!((plan.height_at(0.0, 12.0).unwrap() - 3.0).abs() < 0.0001);
    assert!(carved.height_deltas.contains(&terrain.height_deltas[0]));
    assert_eq!(terrain.content_hash().unwrap(), source_hash);
    assert_eq!(water.content_hash().unwrap(), water_hash);
}

#[test]
fn cut_budget_identity_and_unsupported_profiles_fail_without_mutation() {
    let (terrain, mut water) = fixture();
    let before = terrain.content_hash().unwrap();
    assert!(
        river_carving::prepare(&terrain, &water, 1.0)
            .unwrap_err()
            .contains("exceeds budget")
    );
    for budget in [0.0, -1.0, 65.0, f32::NAN] {
        assert!(river_carving::prepare(&terrain, &water, budget).is_err());
    }
    water.river.elevation_policy = RiverElevationPolicy::TerrainClearanceMonotoneV1;
    assert!(river_carving::prepare(&terrain, &water, 8.0).is_err());
    water.river.elevation_policy = RiverElevationPolicy::AuthoredSurfaceV1;
    water.area_id = forge_core::AreaId::deterministic("different-zone");
    assert!(
        river_carving::prepare(&terrain, &water, 8.0)
            .unwrap_err()
            .contains("same zone")
    );
    assert_eq!(terrain.content_hash().unwrap(), before);
}

#[test]
fn existing_deeper_bed_is_never_filled() {
    let (mut terrain, water) = fixture();
    terrain
        .height_deltas
        .insert(0, TerrainHeightDelta::new(128, 128, -8.0));
    let preview = river_carving::prepare(&terrain, &water, 8.0).unwrap();
    let carved = apply(&terrain, preview.edit);
    assert!(carved.height_deltas.contains(&terrain.height_deltas[0]));
}

fn river_head() -> forge_project::zone_authoring::ZoneAuthoringHead {
    use forge_project::zone_authoring::{
        GeneratorStamp, ZoneAuthoringHead, ZoneBase, ZoneCorrections, rivers::AuthoredRiver,
    };
    let (terrain, water) = fixture();
    ZoneAuthoringHead {
        schema: "forge.zone-authoring".into(),
        version: 4,
        revision: terrain.revision,
        base: ZoneBase {
            generator: GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: terrain.content_hash().unwrap(),
            },
            terrain,
            vegetation: None,
        },
        corrections: ZoneCorrections {
            rivers: Some(vec![AuthoredRiver {
                id: forge_core::EntityId::deterministic("river-one"),
                water,
                maximum_cut_m: 8.0,
                locked: false,
            }]),
            ..Default::default()
        },
    }
}

#[test]
fn head_projection_does_not_accumulate_or_mutate_and_removal_restores_source() {
    let mut head = river_head();
    let source = head.to_bytes().unwrap();
    let first = forge_world::river_projection::project(&head).unwrap();
    assert_eq!(
        first,
        forge_world::river_projection::project(&head).unwrap()
    );
    assert_eq!(head.to_bytes().unwrap(), source);
    assert_eq!(first.source_hash, head.content_hash().unwrap());
    assert_eq!(first.waters.len(), 1);
    let mut second = head.corrections.rivers.as_ref().unwrap()[0].clone();
    second.id = forge_core::EntityId::deterministic("river-two");
    let rivers = head.corrections.rivers.as_mut().unwrap();
    rivers.push(second);
    rivers.sort_by_key(|r| r.id);
    let combined = forge_world::river_projection::project(&head).unwrap();
    assert_eq!(combined.terrain, first.terrain); // No second shoulder cut at overlap.
    assert_eq!(combined.waters.len(), 2);
    head.corrections.rivers = Some(vec![]);
    let removed = forge_world::river_projection::project(&head).unwrap();
    assert_eq!(removed.terrain, head.effective().unwrap().0);
    assert!(removed.waters.is_empty());
}

#[test]
fn authored_river_is_not_blocked_by_the_legacy_eight_metre_limit() {
    let mut head = river_head();
    let river = &mut head.corrections.rivers.as_mut().unwrap()[0];
    for point in &mut river.water.river.control_points {
        point.center_m.y = -20.0;
    }
    let water = river.water.clone();
    let source = head.to_bytes().unwrap();
    let terrain = head.effective().unwrap().0;
    assert!(river_carving::prepare(&terrain, &water, 8.0).is_err());
    let projected = forge_world::river_projection::project(&head).unwrap();
    let plan = TerrainGenerationPlan::from_document(&projected.terrain).unwrap();
    assert!(plan.height_at(0.0, 0.0).unwrap() < -20.0);
    assert_eq!(head.to_bytes().unwrap(), source);
}

#[test]
fn projection_preserves_manual_heights_or_rejects_conflict_including_erasure() {
    let mut head = river_head();
    head.corrections.height_deltas = Some(vec![TerrainHeightDelta::new(200, 200, 9.0)]);
    let outside = forge_world::river_projection::project(&head).unwrap();
    assert!(
        outside
            .terrain
            .height_deltas
            .contains(&TerrainHeightDelta::new(200, 200, 9.0))
    );
    head.corrections
        .height_deltas
        .as_mut()
        .unwrap()
        .insert(0, TerrainHeightDelta::new(128, 128, 1.0));
    let before = head.to_bytes().unwrap();
    assert!(
        forge_world::river_projection::project(&head)
            .unwrap_err()
            .contains("manual terrain sample (128, 128)")
    );
    assert_eq!(before, head.to_bytes().unwrap());
    head.base
        .terrain
        .height_deltas
        .insert(0, TerrainHeightDelta::new(128, 128, 2.0));
    head.corrections.height_deltas = Some(vec![]); // Explicit removal of a base correction.
    assert!(
        forge_world::river_projection::project(&head)
            .unwrap_err()
            .contains("manual terrain sample")
    );
}

#[test]
fn naturally_deep_river_still_projects_water_without_a_terrain_edit() {
    let mut head = river_head();
    head.base.terrain.height_sampling.base_elevation_m = -10.0;
    head.base.terrain.height_deltas.clear();
    let result = forge_world::river_projection::project(&head).unwrap();
    assert_eq!(result.terrain, head.effective().unwrap().0);
    assert_eq!(result.waters.len(), 1);
}

#[test]
fn paths_use_the_carved_surface_without_rewriting_sources() {
    use forge_core::EntityId;
    use forge_project::zone_authoring::paths::{AuthoredPath, PathPoint};
    let mut head = river_head();
    head.corrections.paths = Some(vec![AuthoredPath {
        id: EntityId::deterministic("river-path"),
        name: "Crossing".into(),
        points: vec![
            PathPoint {
                x_m: -5.0,
                z_m: 0.0,
            },
            PathPoint { x_m: 5.0, z_m: 0.0 },
        ],
        width_m: 2.0,
        shoulder_m: 1.0,
        profile: None,
        locked: false,
    }]);
    let source = head.to_bytes().unwrap();
    let terrain = forge_world::river_projection::project(&head)
        .unwrap()
        .terrain;
    let paths = forge_world::path_surface::project(&head).unwrap();
    assert_eq!(paths.terrain_hash, terrain.content_hash().unwrap());
    assert_ne!(
        paths.terrain_hash,
        head.effective().unwrap().0.content_hash().unwrap()
    );
    let plan = forge_world::TerrainGenerationPlan::from_document(&terrain).unwrap();
    for [x, y, z] in &paths.meshes[0].positions {
        let (surface, _) = plan.surface_with_shading_normal_at(*x, *z).unwrap();
        assert!((y - surface.height_m - 0.02).abs() < 0.001);
    }
    assert_eq!(head.to_bytes().unwrap(), source);
}

#[test]
fn recipe_preflight_checks_projection_but_keeps_repair_actions_available() {
    use forge_core::CommandId;
    use forge_project::zone_authoring::rivers::RiverEdit;
    let mut head = river_head();
    let river = head.corrections.rivers.as_mut().unwrap().remove(0);
    let insert = RiverEdit::Insert {
        river: river.clone(),
    };
    let id = CommandId::deterministic("river-preflight");
    assert_eq!(
        forge_world::river_projection::prepare_edit(&head, id, insert.clone()).unwrap(),
        head.prepare_river(id, insert.clone()).unwrap()
    );
    head.corrections.height_deltas = Some(vec![TerrainHeightDelta::new(128, 128, 1.0)]);
    let before = head.to_bytes().unwrap();
    assert!(head.prepare_river(id, insert.clone()).is_ok());
    assert!(
        forge_world::river_projection::prepare_edit(&head, id, insert)
            .unwrap_err()
            .contains("manual terrain")
    );
    assert_eq!(head.to_bytes().unwrap(), before);
    head.corrections.rivers = Some(vec![river.clone()]);
    assert!(
        forge_world::river_projection::prepare_edit(&head, id, RiverEdit::Remove { id: river.id })
            .is_ok()
    );
}

fn stroke(z: f32, metres: f32) -> forge_world::terrain_brush_stroke::TerrainBrushStroke {
    use forge_world::terrain_brush::{BrushOperation, TerrainBrush};
    forge_world::terrain_brush_stroke::TerrainBrushStroke::single(TerrainBrush {
        x_m: 0.0,
        z_m: z,
        radius_m: 2.0,
        strength: 1.0,
        operation: BrushOperation::RaiseLower { metres },
    })
}

#[test]
fn brush_on_river_bed_preserves_only_the_manual_gesture() {
    let head = river_head();
    let source = head.to_bytes().unwrap();
    let visible = forge_world::river_projection::project(&head)
        .unwrap()
        .terrain;
    let gesture = stroke(0.0, -1.0);
    let expected = apply(&visible, gesture.prepare(&visible).unwrap());
    let edit = forge_world::river_projection::prepare_brush(&head, &gesture).unwrap();
    let TerrainEdit::SetHeightDeltas { samples } = edit else {
        panic!("height edit")
    };
    assert!(samples.len() < expected.height_deltas.len());
    let mut candidate = head.clone();
    candidate.corrections.height_deltas = Some(samples);
    assert_eq!(
        forge_world::river_projection::project(&candidate)
            .unwrap()
            .terrain,
        expected
    );
    // Removing the procedural river must leave only the original source + retouch.
    candidate.corrections.rivers = Some(vec![]);
    let remaining = forge_world::river_projection::project(&candidate)
        .unwrap()
        .terrain;
    let plan = TerrainGenerationPlan::from_document(&remaining).unwrap();
    assert!((plan.height_at(0.0, 0.0).unwrap() + 1.0).abs() < 0.001);
    assert!((plan.height_at(10.0, 0.0).unwrap() - 3.0).abs() < 0.001);
    assert_eq!(head.to_bytes().unwrap(), source);
}

#[test]
fn river_brush_rejects_conflicting_raise_but_accepts_outside_edits() {
    let head = river_head();
    let source = head.to_bytes().unwrap();
    assert!(forge_world::river_projection::prepare_brush(&head, &stroke(0.0, 1.0)).is_err());
    assert!(forge_world::river_projection::prepare_brush(&head, &stroke(50.0, 1.0)).is_ok());
    assert_eq!(head.to_bytes().unwrap(), source);
    let mut plain = head;
    plain.corrections.rivers = None;
    plain.version = plain.corrections.schema_version();
    let gesture = stroke(50.0, 1.0);
    assert_eq!(
        forge_world::river_projection::prepare_brush(&plain, &gesture).unwrap(),
        gesture.prepare(&plain.effective().unwrap().0).unwrap()
    );
}

#[test]
fn river_brush_roundtrips_through_review_history_and_regeneration() {
    use forge_core::CommandId;
    use forge_project::{
        terrain_edit::{TerrainAction, TerrainEditSession},
        zone_authoring::{
            migration::ZoneMigrationProposal,
            session::{ZoneAction, ZoneEditSession, ZoneRequest},
        },
    };
    let fixture = river_head();
    let legacy = TerrainEditSession::new(fixture.base.terrain.clone()).unwrap();
    let mut session = ZoneEditSession::from_migration(
        ZoneMigrationProposal::prepare(
            &legacy.archive_bytes().unwrap(),
            fixture.base.terrain.content_hash().unwrap(),
            fixture.base.generator.clone(),
            None,
        )
        .unwrap(),
    )
    .unwrap();
    let request = |session: &ZoneEditSession, action| ZoneRequest {
        command_id: CommandId::deterministic(format!("brush-action-{}", session.audit().len())),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    };
    session
        .execute(&request(&session, ZoneAction::edit(fixture.corrections)))
        .unwrap();
    let original = session.head().corrections.clone();
    let edit =
        forge_world::river_projection::prepare_brush(session.head(), &stroke(0.0, -1.0)).unwrap();
    let edit = session
        .head()
        .prepare_terrain_action(
            TerrainAction::Edit(edit),
            CommandId::deterministic("bed-brush"),
        )
        .unwrap();
    let preview = session.preview(&edit).unwrap();
    assert_eq!(session.head().corrections, original);
    assert_eq!(session.execute(&edit).unwrap(), preview);
    assert_eq!(session.execute(&edit).unwrap(), preview);
    let manual = session.head().corrections.clone();
    session
        .execute(&request(&session, ZoneAction::Undo))
        .unwrap();
    assert_eq!(session.head().corrections, original);
    session
        .execute(&request(&session, ZoneAction::Redo))
        .unwrap();
    assert_eq!(session.head().corrections, manual);
    let mut base = session.head().base.clone();
    base.terrain.seed += 1;
    let before = session.head().clone();
    assert!(
        session
            .execute(&request(&session, ZoneAction::Regenerate(Box::new(base))))
            .unwrap_err()
            .contains("TerrainHeightReferenceChanged")
    );
    assert_eq!(session.head(), &before);
    let mut compatible = session.head().base.clone();
    compatible.generator.version = "2".into();
    session
        .execute(&request(
            &session,
            ZoneAction::Regenerate(Box::new(compatible)),
        ))
        .unwrap();
    assert_eq!(session.head().corrections, manual);
    let projected = forge_world::river_projection::project(session.head()).unwrap();
    for archive in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&archive).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(
            forge_world::river_projection::project(reopened.head()).unwrap(),
            projected
        );
    }
}

#[test]
fn river_clearance_filters_generated_vegetation_without_spending_manual_quota() {
    use forge_project::{VegetationExclusion, VegetationExclusionId};
    use forge_world::{VegetationGenerationPlan, editor_vegetation};
    let mut head = river_head();
    let river = &mut head.corrections.rivers.as_mut().unwrap()[0];
    for (point, x) in river
        .water
        .river
        .control_points
        .iter_mut()
        .zip([-190.0, -60.0, 60.0, 190.0])
    {
        point.center_m.x = x;
    }
    let terrain = &head.base.terrain;
    let mut vegetation = editor_vegetation::prepare(terrain, 42).unwrap();
    vegetation.exclusions = (0..32)
        .map(|i| VegetationExclusion::Circle {
            id: VegetationExclusionId::new(format!("manual_{i}")),
            center_x_m: 200.0,
            center_z_m: 200.0,
            radius_m: 1.0,
        })
        .collect();
    let before = vegetation.clone();
    let plain = VegetationGenerationPlan::from_documents(terrain, &vegetation)
        .unwrap()
        .generate()
        .unwrap();
    let empty =
        VegetationGenerationPlan::from_documents_with_waterways(terrain, &vegetation, &[], &[])
            .unwrap()
            .generate()
            .unwrap();
    assert_eq!(plain, empty);
    let cleared = VegetationGenerationPlan::from_documents_with_waterways(
        terrain,
        &vegetation,
        &[],
        head.corrections.rivers.as_ref().unwrap(),
    )
    .unwrap()
    .generate()
    .unwrap();
    let inside = |x: f32, z: f32| (x - x.clamp(-190.0, 190.0)).powi(2) + z * z <= 144.0;
    assert!(
        plain
            .chunks
            .iter()
            .flat_map(|c| &c.instances)
            .any(|i| inside(i.position_m.x, i.position_m.z))
    );
    assert!(
        !cleared
            .chunks
            .iter()
            .flat_map(|c| &c.instances)
            .any(|i| inside(i.position_m.x, i.position_m.z))
    );
    assert_eq!(vegetation, before);
    assert_ne!(
        plain.vegetation_document_hash,
        cleared.vegetation_document_hash
    );
}
