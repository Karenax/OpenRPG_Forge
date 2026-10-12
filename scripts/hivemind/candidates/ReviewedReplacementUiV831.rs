use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, Vec3};
use forge_project::{authoring::new_terrain, zone_authoring::{placements::{ManualPlacement, PhysicsPackageReference, PlacementEdit}, session::{ZoneAction, ZoneEditSession, ZoneRequest}}};

fn object(label: &str, physical: bool) -> ManualPlacement {
    let hash = ContentHash::from_bytes([3; 32]);
    ManualPlacement { id: EntityId::deterministic(label), name: label.into(), asset_id: AssetId::deterministic("same-carrier"), asset_hash: hash,
        physics: physical.then(|| Box::new(PhysicsPackageReference { schema_version: 1, carrier_sha256: hash, manifest_sha256: ContentHash::from_bytes([4; 32]), package_sha256: ContentHash::from_bytes([5; 32]) })),
        transform: Transform::from_translation(Vec3::new(2., 3., 4.)), locked: false }
}

#[test]
fn ui_reviewed_removal_requires_consent_and_excludes_other_package_versions() {
    let source = object("source", true);
    let copy = object("copy", true);
    let mut variant = object("variant", true);
    variant.physics.as_mut().unwrap().package_sha256 = ContentHash::from_bytes([6; 32]);
    let target = (AssetId::deterministic("ordinary-target"), ContentHash::from_bytes([7; 32]));
    let mut session = ZoneEditSession::from_initial_terrain(new_terrain("ui-replacement", "zone", "Zone").unwrap()).unwrap();
    for p in [&source, &copy, &variant] {
        let edit = session.head().prepare_placement(CommandId::deterministic(format!("insert-{}", p.id)), PlacementEdit::Insert { placement: p.clone() }).unwrap();
        session.execute(&edit).unwrap();
    }
    let placements = session.head().corrections.placements.as_ref().unwrap().clone();
    assert!(request::prepare(&placements, &source, target, true, false).is_err());
    let action = request::prepare(&placements, &source, target, true, true).unwrap();
    let prepared = session.head().prepare_placement(CommandId::deterministic("reviewed-remove"), action).unwrap();
    session.execute(&prepared).unwrap();
    for original in [&source, &copy] {
        let current = session.head().corrections.placements.as_ref().unwrap().iter().find(|p| p.id == original.id).unwrap();
        assert_eq!((current.asset_id, current.asset_hash), target);
        assert!(current.physics.is_none());
        assert_eq!((&current.name, current.transform), (&original.name, original.transform));
    }
    assert_eq!(session.head().corrections.placements.as_ref().unwrap().iter().find(|p| p.id == variant.id), Some(&variant));
    let undo = ZoneRequest { command_id: CommandId::deterministic("undo-ui-removal"), expected_hash: session.head().content_hash().unwrap(), expected_revision: session.head().revision, action: ZoneAction::Undo };
    session.execute(&undo).unwrap();
    assert_eq!(session.head().corrections.placements.as_ref().unwrap(), &placements);
}

#[test]
fn ui_ordinary_replacement_does_not_include_a_physical_object_with_same_carrier() {
    let ordinary = object("ordinary", false);
    let physical = object("physical", true);
    let action = request::prepare(&[ordinary.clone(), physical], &ordinary, (AssetId::deterministic("target"), ContentHash::from_bytes([7; 32])), true, false).unwrap();
    let PlacementEdit::ReplaceAsset { ids, .. } = action else { panic!("ordinary command required") };
    assert_eq!(ids, vec![ordinary.id]);
}
