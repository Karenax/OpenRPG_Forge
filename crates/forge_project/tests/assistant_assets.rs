use forge_core::{AssetId, CommandId, ContentHash, Vec3, bytes_sha256};
use forge_project::{
    assistant::{
        assets::{AssetCatalog, AssetReference},
        planning::{PlanProposal, PlannedOperation},
        provider::{ApiKind, ProviderRequest},
        selection::{Region, Selection, SelectionScope},
    },
    authoring::new_terrain,
    zone_authoring::session::ZoneEditSession,
};

fn asset() -> AssetReference {
    AssetReference {
        asset_id: AssetId::deterministic("assistant-assets/fixture"),
        model_hash: Some(bytes_sha256(b"synthetic verified fixture")),
        physics: None,
        particles: None,
        name: "Porte de recette".into(),
        pack: "Fixture".into(),
        family: "construction".into(),
        asset_type: "door".into(),
        license: "CC0".into(),
        dimensions_m: [1.0, 2.0, 0.2],
        triangles: 12,
        metadata_ready: true,
        runtime_qualified: false,
        model_verified: true,
    }
}
fn scope() -> (ZoneEditSession, SelectionScope) {
    let session =
        ZoneEditSession::from_initial_terrain(new_terrain("assets", "zone", "Original").unwrap())
            .unwrap();
    let scope = SelectionScope::new(
        session.head(),
        &Selection {
            zone_properties: true,
            ..Selection::default()
        },
        Region {
            minimum: Vec3::new(-5.0, -5.0, -5.0),
            maximum: Vec3::new(5.0, 5.0, 5.0),
        },
    )
    .unwrap();
    (session, scope)
}

#[test]
fn asset_metadata_is_bounded_and_cannot_upgrade_integrity_into_runtime_qualification() {
    let revision = bytes_sha256(b"catalog");
    let catalog = AssetCatalog::new(revision, vec![asset()]).unwrap();
    assert!(!catalog.context().assets[0].runtime_qualified);
    assert!(AssetCatalog::new(revision, vec![asset(), asset()]).is_err());
    assert!(AssetCatalog::new(revision, vec![]).is_err());
    let mut invalid = asset();
    invalid.license.clear();
    assert!(AssetCatalog::new(revision, vec![invalid]).is_err());
    let mut invalid = asset();
    invalid.model_hash = None;
    assert!(AssetCatalog::new(revision, vec![invalid]).is_err());
    let mut invalid = asset();
    invalid.dimensions_m[1] = f64::NAN;
    assert!(AssetCatalog::new(revision, vec![invalid]).is_err());
}

#[test]
fn metadata_catalogue_binds_the_shared_context_and_requires_a_new_outbound_approval() {
    let (session, scope) = scope();
    let before = serde_json::to_value(scope.context()).unwrap();
    assert!(before.get("asset_catalog").is_none());
    let catalog = AssetCatalog::new(bytes_sha256(b"catalog"), vec![asset()]).unwrap();
    let extended = scope.clone().with_assets(&catalog).unwrap();
    assert_ne!(
        scope.context().content_hash().unwrap(),
        extended.context().content_hash().unwrap()
    );
    let request = |scope: &SelectionScope| {
        ProviderRequest::new(
            ApiKind::ChatCompletions,
            "http://127.0.0.1:8080/v1/chat/completions",
            "fixture-model",
            "Renommer la carte seulement",
            scope.context(),
            512,
        )
        .unwrap()
    };
    let old = request(&scope);
    let current = request(&extended);
    assert!(
        current
            .approve_outbound(old.content_hash().unwrap())
            .is_err()
    );
    let plan = PlanProposal {
        context_hash: scope.context().content_hash().unwrap(),
        summary: "Synthetic asset context test".into(),
        missing_capabilities: vec![],
        operations: vec![PlannedOperation::RenameZone {
            name: "Variant".into(),
        }],
    };
    assert!(
        plan.compile(
            session.head(),
            &extended,
            CommandId::deterministic("assets/plan")
        )
        .is_err()
    );
    let mut current_plan = plan;
    current_plan.context_hash = extended.context().content_hash().unwrap();
    assert!(
        current_plan
            .compile(
                session.head(),
                &extended,
                CommandId::deterministic("assets/plan")
            )
            .is_ok()
    );
    assert_eq!(
        extended.context().supported_operations,
        scope.context().supported_operations
    );
    assert_eq!(extended.policy(), scope.policy());
}

#[test]
fn source_only_references_are_explicit_and_large_catalogues_fail_closed() {
    let mut source = asset();
    source.model_hash = None;
    source.model_verified = false;
    let catalog = AssetCatalog::new(bytes_sha256(b"source-only"), vec![source]).unwrap();
    assert!(!catalog.context().assets[0].model_available());
    let huge = (0..16)
        .map(|index| {
            let mut entry = asset();
            entry.asset_id = AssetId::deterministic(format!("assets/{index}"));
            entry.license = "x".repeat(4096);
            entry
        })
        .collect();
    assert!(AssetCatalog::new(ContentHash::parse(&"0".repeat(64)).unwrap(), huge).is_err());
}

fn placement_operation() -> PlannedOperation {
    PlannedOperation::PlaceAsset {
        asset_id: asset().asset_id,
        key: "door-1".into(),
        name: "Porte proposée".into(),
        position: Vec3::new(1.0, 0.0, 2.0),
        yaw_degrees: 90.0,
        scale: 1.0,
    }
}

fn placement_plan(scope: &SelectionScope, operations: Vec<PlannedOperation>) -> PlanProposal {
    PlanProposal {
        context_hash: scope.context().content_hash().unwrap(),
        summary: "Synthetic verified asset insertion".into(),
        missing_capabilities: vec![],
        operations,
    }
}

#[test]
fn asset_creation_requires_explicit_permission_and_verified_models() {
    let (session, base) = scope();
    let catalog = AssetCatalog::new(bytes_sha256(b"catalog"), vec![asset()]).unwrap();
    let reference_scope = base.clone().with_assets(&catalog).unwrap();
    let command = CommandId::deterministic("assets/create");
    assert!(
        placement_plan(&reference_scope, vec![placement_operation()])
            .compile(session.head(), &reference_scope, command)
            .is_err()
    );
    let allowed = catalog.authorize_placement().unwrap();
    // Creating assets does not require granting unrelated zone properties.
    let creation = SelectionScope::new_with_assets(
        session.head(),
        &Selection::default(),
        base.context().region,
        &allowed,
    )
    .unwrap();
    assert_eq!(creation.context().supported_operations, ["place_asset"]);
    assert!(
        placement_plan(&creation, vec![placement_operation()])
            .compile(session.head(), &creation, command)
            .is_ok()
    );
    let mut source = asset();
    source.model_verified = false;
    assert!(
        AssetCatalog::new(bytes_sha256(b"catalog"), vec![source])
            .unwrap()
            .authorize_placement()
            .is_err()
    );
    let request = ProviderRequest::new(
        ApiKind::ChatCompletions,
        "http://127.0.0.1/v1/chat/completions",
        "fixture",
        "Ajouter cette porte",
        creation.context(),
        512,
    )
    .unwrap();
    let schema = &request.body()["response_format"]["json_schema"]["schema"];
    let serialized = serde_json::to_string(schema).unwrap();
    assert!(serialized.contains("place_asset"));
    assert!(serialized.contains(&asset().asset_id.to_string()));
    assert!(!serialized.contains("asset_hash"));
}

#[test]
fn insertion_is_one_reviewed_transaction_and_preserves_existing_locked_models_and_pins() {
    use forge_core::{EntityId, Transform};
    use forge_project::{
        assistant,
        audit::AuditStamp,
        zone_authoring::{
            placements::{ManualPlacement, PhysicsPackageReference, PlacementEdit},
            session::ZoneAction,
        },
    };
    let (mut session, _) = scope();
    let existing = ManualPlacement {
        local_lights: None,
        id: EntityId::deterministic("human/locked"),
        name: "Retouche humaine".into(),
        asset_id: AssetId::deterministic("human/model"),
        asset_hash: bytes_sha256(b"human-model"),
        physics: None,
        particles: None,
        transform: Transform::IDENTITY,
        locked: true,
    };
    let insert = session
        .head()
        .prepare_placement(
            CommandId::deterministic("human/insert"),
            PlacementEdit::Insert {
                placement: existing.clone(),
            },
        )
        .unwrap();
    session
        .execute_with_audit(
            &insert,
            AuditStamp::human("fixture", "2026-10-22T12:00:00Z").unwrap(),
        )
        .unwrap();
    let before = session.head().clone();
    let mut reference = asset();
    reference.physics = Some(Box::new(PhysicsPackageReference {
        schema_version: 1,
        carrier_sha256: reference.model_hash.unwrap(),
        manifest_sha256: bytes_sha256(b"manifest"),
        package_sha256: bytes_sha256(b"package"),
    }));
    let catalog = AssetCatalog::new(bytes_sha256(b"catalog"), vec![reference.clone()])
        .unwrap()
        .authorize_placement()
        .unwrap();
    let authorized = SelectionScope::new_with_assets(
        session.head(),
        &Selection::default(),
        scope().1.context().region,
        &catalog,
    )
    .unwrap();
    let plan = placement_plan(&authorized, vec![placement_operation()]);
    let command = CommandId::deterministic("assets/one-transaction");
    let request = plan.compile(session.head(), &authorized, command).unwrap();
    assert_eq!(
        request,
        plan.compile(session.head(), &authorized, command).unwrap()
    );
    let ZoneAction::Edit(after) = &request.action else {
        panic!("typed edit required");
    };
    assert!(after.placements.as_ref().unwrap().contains(&existing));
    let added = after
        .placements
        .as_ref()
        .unwrap()
        .iter()
        .find(|entry| entry.id != existing.id)
        .unwrap();
    assert_eq!(added.physics, reference.physics);
    assert_eq!(added.asset_hash, reference.model_hash.unwrap());
    let review = assistant::prepare(
        &session,
        &request,
        authorized.policy(),
        AuditStamp::ai("synthetic-assets", "2026-10-22T12:01:00Z").unwrap(),
    )
    .unwrap();
    let approval = review.approve(review.content_hash().unwrap()).unwrap();
    let count = session.audit().len();
    assistant::apply(&mut session, authorized.policy(), &review, &approval).unwrap();
    assert_eq!(session.audit().len(), count + 1);
    let restored = ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(restored.head().corrections, *after.as_ref());
    assert_ne!(
        restored.head().content_hash().unwrap(),
        before.content_hash().unwrap()
    );
}

#[test]
fn forged_unselected_duplicate_and_out_of_region_additions_fail_atomically() {
    let (session, base) = scope();
    let catalog = AssetCatalog::new(bytes_sha256(b"catalog"), vec![asset()])
        .unwrap()
        .authorize_placement()
        .unwrap();
    let scope = base.with_assets(&catalog).unwrap();
    let before = session.archive_bytes().unwrap();
    for invalid in [
        PlannedOperation::PlaceAsset {
            asset_id: AssetId::deterministic("unselected"),
            key: "x".into(),
            name: "X".into(),
            position: Vec3::ZERO,
            yaw_degrees: 0.0,
            scale: 1.0,
        },
        PlannedOperation::PlaceAsset {
            asset_id: asset().asset_id,
            key: "x".into(),
            name: "X".into(),
            position: Vec3::new(50.0, 0.0, 0.0),
            yaw_degrees: 0.0,
            scale: 1.0,
        },
        PlannedOperation::PlaceAsset {
            asset_id: asset().asset_id,
            key: "../path".into(),
            name: "X".into(),
            position: Vec3::ZERO,
            yaw_degrees: 0.0,
            scale: 1.0,
        },
        PlannedOperation::PlaceAsset {
            asset_id: asset().asset_id,
            key: "x".into(),
            name: "X".into(),
            position: Vec3::ZERO,
            yaw_degrees: 0.0,
            scale: -1.0,
        },
    ] {
        let plan = placement_plan(&scope, vec![placement_operation(), invalid]);
        assert!(
            plan.compile(
                session.head(),
                &scope,
                CommandId::deterministic("assets/invalid")
            )
            .is_err()
        );
        assert_eq!(session.archive_bytes().unwrap(), before);
    }
    assert!(
        placement_plan(&scope, vec![placement_operation(), placement_operation()])
            .compile(
                session.head(),
                &scope,
                CommandId::deterministic("assets/duplicate")
            )
            .is_err()
    );
}
