// Test the production planner without a WebView, renderer or Tauri host.
#[path = "../src/authoring_assets/objects/manual_sources.rs"]
mod manual_sources;
#[path = "../src/authoring_assets/objects/planning.rs"]
mod planning;

use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, Vec3};
use forge_project::{
    VegetationDocument,
    authoring::new_terrain,
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
    zone_authoring::{
        placements::{ManualPlacement, PlacementEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
        source_objects::{SourceObjectState, resolve},
    },
};
use std::cell::Cell;

fn session() -> ZoneEditSession {
    let mut session = ZoneEditSession::from_initial_terrain(
        new_terrain("accepted-planning", "zone", "Accepted planning").unwrap(),
    )
    .unwrap();
    let mut vegetation = VegetationDocument::from_json(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = session.head().base.terrain.project_id;
    vegetation.area_id = session.head().base.terrain.area_id;
    vegetation.extent_m.size_x_m = session.head().base.terrain.extent_m.size_x_m;
    vegetation.extent_m.size_z_m = session.head().base.terrain.extent_m.size_z_m;
    vegetation.schema_version = 2;
    vegetation.layers.truncate(1);
    vegetation.layers[0].material_filter = None;
    vegetation.imported_source = Some(ImportedVegetationSource {
        schema: "forge.imported-vegetation".into(),
        version: 1,
        blocks: vec![ImportedVegetationBlock {
            layer_id: vegetation.layers[0].id.to_string(),
            prototype_key: "planning_palms".into(),
            count: 2,
            encoding: ImportedInstanceEncoding::TransformF64LeV1,
            instances_sha256: ContentHash::from_bytes([7; 32]),
            parameter_names: Vec::new(),
        }],
    });
    let mut base = session.head().base.clone();
    base.vegetation = Some(vegetation);
    let request = session
        .head()
        .prepare_regeneration(
            base,
            CommandId::deterministic("install imported population"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    session
}

fn object(label: &str, asset_id: AssetId, marker: u8) -> ManualPlacement {
    ManualPlacement {
        physics: None,
        id: EntityId::deterministic(format!("accepted-planning:{label}")),
        name: label.into(),
        asset_id,
        asset_hash: ContentHash::from_bytes([marker; 32]),
        transform: Transform::from_translation(Vec3::new(2.0, 3.0, 4.0)),
        locked: false,
    }
}

fn plant(session: &ZoneEditSession, label: &str) -> ManualPlacement {
    let layer = session.head().base.vegetation.as_ref().unwrap().layers[0]
        .id
        .as_str();
    object(
        label,
        AssetId::deterministic(format!(
            "imported-instance:{}:{}:{layer}",
            session.head().base.terrain.project_id,
            session.head().base.terrain.area_id,
        )),
        7,
    )
}

fn prepare(
    session: &ZoneEditSession,
    sources: &[ManualPlacement],
    objects: &[ManualPlacement],
    action: PlacementEdit,
    label: &str,
) -> Result<ZoneRequest, String> {
    let accepted = session.head().content_hash().unwrap();
    planning::prepare(
        session.head(),
        sources,
        action,
        CommandId::deterministic(label),
        |head, id| {
            if head.content_hash()? != accepted {
                return Err("lookup on speculative revision".into());
            }
            objects
                .iter()
                .find(|object| object.id == id)
                .cloned()
                .ok_or("missing object".into())
        },
        |head, candidate| {
            assert_eq!(head.content_hash()?, accepted);
            assert_ne!(candidate.content_hash()?, accepted);
            candidate.validate()
        },
    )
}

fn corrections(request: &ZoneRequest) -> &forge_project::zone_authoring::ZoneCorrections {
    let ZoneAction::Edit(corrections) = &request.action else {
        panic!("edit request")
    };
    corrections
}

#[test]
fn first_plant_transform_resolves_accepted_head_and_validates_speculative_view() {
    let mut session = session();
    let source = plant(&session, "first plant");
    let before = session.head().clone();
    let transform = Transform::from_translation(Vec3::new(8.0, 9.0, 10.0));
    let request = prepare(
        &session,
        &[],
        std::slice::from_ref(&source),
        PlacementEdit::Transform {
            id: source.id,
            transform,
        },
        "move first plant",
    )
    .unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(request.expected_hash, before.content_hash().unwrap());
    assert_eq!(request.expected_revision, before.revision);
    session.execute(&request).unwrap();
    assert_eq!(
        resolve(session.head(), &source).unwrap()[0].transform,
        transform
    );
}

#[test]
fn two_plant_replacement_resolves_both_before_freezing_static_target_provenance() {
    let mut session = session();
    let first = plant(&session, "first plant");
    let second = plant(&session, "second plant");
    let target = object("static target", AssetId::deterministic("new target"), 9);
    let request = prepare(
        &session,
        std::slice::from_ref(&target),
        &[first.clone(), second.clone()],
        PlacementEdit::ReplaceAsset {
            ids: vec![first.id, second.id],
            expected_asset_id: first.asset_id,
            expected_asset_hash: first.asset_hash,
            asset_id: target.asset_id,
            asset_hash: target.asset_hash,
        },
        "replace two plants",
    )
    .unwrap();
    let records = corrections(&request).source_object_edits.as_ref().unwrap();
    assert!(matches!(
        records
            .iter()
            .find(|r| r.source.id == target.id)
            .unwrap()
            .state,
        SourceObjectState::Inherited
    ));
    session.execute(&request).unwrap();
    for source in [first, second] {
        assert!(resolve(session.head(), &source).unwrap().is_empty());
        let current = session
            .head()
            .corrections
            .placements
            .as_ref()
            .unwrap()
            .iter()
            .find(|p| p.id == source.id)
            .unwrap();
        assert_eq!(
            (current.asset_id, current.asset_hash),
            (target.asset_id, target.asset_hash)
        );
        assert_eq!(current.transform, source.transform);
    }
    assert_eq!(resolve(session.head(), &target).unwrap(), vec![target]);
}

#[test]
fn locked_second_plant_rejects_entire_replacement_without_mutating_the_head() {
    let session = session();
    let first = plant(&session, "first plant");
    let mut second = plant(&session, "second plant");
    second.locked = true;
    let before = session.archive_bytes().unwrap();
    let request = prepare(
        &session,
        &[],
        &[first.clone(), second.clone()],
        PlacementEdit::ReplaceAsset {
            ids: vec![first.id, second.id],
            expected_asset_id: first.asset_id,
            expected_asset_hash: first.asset_hash,
            asset_id: AssetId::deterministic("new target"),
            asset_hash: ContentHash::from_bytes([9; 32]),
        },
        "locked replacement",
    );
    assert!(request.is_err());
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn genuinely_stale_population_head_is_rejected_before_any_candidate_validation() {
    let session = session();
    let source = plant(&session, "stale plant");
    let before = session.archive_bytes().unwrap();
    let validation_called = Cell::new(false);
    let expected = ContentHash::from_bytes([99; 32]);
    let request = planning::prepare(
        session.head(),
        &[],
        PlacementEdit::Transform {
            id: source.id,
            transform: Transform::IDENTITY,
        },
        CommandId::deterministic("stale revision"),
        |head, _| {
            if head.content_hash()? == expected {
                Ok(source.clone())
            } else {
                Err("stale accepted population".into())
            }
        },
        |_, _| {
            validation_called.set(true);
            Ok(())
        },
    );
    assert_eq!(request.unwrap_err(), "stale accepted population");
    assert!(!validation_called.get());
    assert_eq!(session.archive_bytes().unwrap(), before);
}

#[test]
fn later_manual_insert_with_plant_edits_keeps_population_pinned_to_current_accepted_head() {
    let mut session = session();
    let plant = plant(&session, "edited plant");
    let first = prepare(
        &session,
        &[],
        std::slice::from_ref(&plant),
        PlacementEdit::Transform {
            id: plant.id,
            transform: Transform::IDENTITY,
        },
        "first edit",
    )
    .unwrap();
    session.execute(&first).unwrap();
    let accepted = session.head().clone();
    let source = object("static source", AssetId::deterministic("static pin"), 3);
    let manual = object("new manual", source.asset_id, 3);
    let request = prepare(
        &session,
        std::slice::from_ref(&source),
        &[],
        PlacementEdit::Insert {
            placement: manual.clone(),
        },
        "insert after plant edit",
    )
    .unwrap();
    assert_eq!(request.expected_hash, accepted.content_hash().unwrap());
    assert_eq!(session.head(), &accepted);
    session.execute(&request).unwrap();
    assert!(
        session
            .head()
            .corrections
            .placements
            .as_ref()
            .unwrap()
            .contains(&manual)
    );
    assert_eq!(resolve(session.head(), &source).unwrap(), vec![source]);
}

#[test]
fn rejected_speculative_population_view_does_not_produce_a_request_or_mutate_the_head() {
    let session = session();
    let source = plant(&session, "invalid view plant");
    let before = session.archive_bytes().unwrap();
    let request = planning::prepare(
        session.head(),
        &[],
        PlacementEdit::Transform {
            id: source.id,
            transform: Transform::IDENTITY,
        },
        CommandId::deterministic("invalid population view"),
        |_, _| Ok(source.clone()),
        |accepted, candidate| {
            assert_eq!(accepted, session.head());
            assert_ne!(candidate.content_hash()?, accepted.content_hash()?);
            Err("invalid projected population".into())
        },
    );
    assert_eq!(request.unwrap_err(), "invalid projected population");
    assert_eq!(session.archive_bytes().unwrap(), before);
}
