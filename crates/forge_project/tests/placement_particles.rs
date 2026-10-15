use forge_core::{AssetId, CommandId, ContentHash, EntityId, Transform, Vec3};
use forge_project::{
    authoring::new_terrain,
    zone_authoring::{
        placements::{ManualPlacement, ParticlePackageReference, PlacementChanges, PlacementEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};
fn object() -> ManualPlacement {
    ManualPlacement {
        id: EntityId::deterministic("particle-placement"),
        name: "Effet retouché".into(),
        asset_id: AssetId::deterministic("particle-asset"),
        asset_hash: ContentHash::from_bytes([3; 32]),
        physics: None,
        particles: Some(Box::new(ParticlePackageReference {
            schema_version: 1,
            carrier_sha256: ContentHash::from_bytes([3; 32]),
            manifest_sha256: ContentHash::from_bytes([4; 32]),
            package_sha256: ContentHash::from_bytes([5; 32]),
        })),
        transform: Transform::from_translation(Vec3::new(2., 3., 4.)),
        locked: false,
    }
}
fn session() -> ZoneEditSession {
    ZoneEditSession::from_initial_terrain(new_terrain("particle-pins", "zone", "Zone").unwrap())
        .unwrap()
}
fn apply(s: &mut ZoneEditSession, edit: PlacementEdit, key: &str) {
    let request = s
        .head()
        .prepare_placement(CommandId::deterministic(key), edit)
        .unwrap();
    s.execute(&request).unwrap();
}
#[test]
fn particle_pins_survive_document_roundtrip_duplicate_transforms_and_undo() {
    let mut s = session();
    let original = object();
    apply(
        &mut s,
        PlacementEdit::Insert {
            placement: original.clone(),
        },
        "insert",
    );
    let serialized = serde_json::to_vec(s.head()).unwrap();
    let decoded: forge_project::zone_authoring::ZoneAuthoringHead =
        serde_json::from_slice(&serialized).unwrap();
    assert_eq!(
        decoded.corrections.placements.as_ref().unwrap()[0],
        original
    );
    let copy = EntityId::deterministic("particle-copy");
    apply(
        &mut s,
        PlacementEdit::Duplicate {
            source_id: original.id,
            id: copy,
            transform: Transform::from_translation(Vec3::new(7., 8., 9.)),
        },
        "duplicate",
    );
    let duplicate = s
        .head()
        .corrections
        .placements
        .as_ref()
        .unwrap()
        .iter()
        .find(|p| p.id == copy)
        .unwrap();
    assert_eq!(duplicate.particles, original.particles);
    assert_ne!(duplicate.transform, original.transform);
    let before = s.head().corrections.placements.clone().unwrap();
    apply(
        &mut s,
        PlacementEdit::Transform {
            id: original.id,
            transform: Transform::IDENTITY,
        },
        "transform",
    );
    assert_eq!(
        s.head()
            .corrections
            .placements
            .as_ref()
            .unwrap()
            .iter()
            .find(|p| p.id == original.id)
            .unwrap()
            .particles,
        original.particles
    );
    s.execute(&ZoneRequest {
        command_id: CommandId::deterministic("undo"),
        expected_hash: s.head().content_hash().unwrap(),
        expected_revision: s.head().revision,
        action: ZoneAction::Undo,
    })
    .unwrap();
    assert_eq!(s.head().corrections.placements.as_ref().unwrap(), &before);
}
#[test]
fn replacement_requires_all_pins_and_preserves_retouches() {
    let mut s = session();
    let original = object();
    apply(
        &mut s,
        PlacementEdit::Insert {
            placement: original.clone(),
        },
        "insert",
    );
    assert!(
        s.head()
            .prepare_placement(
                CommandId::deterministic("unsafe"),
                PlacementEdit::ReplaceAsset {
                    ids: vec![original.id],
                    expected_asset_id: original.asset_id,
                    expected_asset_hash: original.asset_hash,
                    asset_id: original.asset_id,
                    asset_hash: ContentHash::from_bytes([8; 32])
                }
            )
            .is_err()
    );
    let mut target = original.particles.clone().unwrap();
    target.package_sha256 = ContentHash::from_bytes([8; 32]);
    let edit = |expected_particles| PlacementEdit::ReplacePinnedAsset {
        ids: vec![original.id],
        expected_asset_id: original.asset_id,
        expected_asset_hash: original.asset_hash,
        expected_physics: None,
        expected_particles,
        asset_id: original.asset_id,
        asset_hash: original.asset_hash,
        physics: None,
        particles: Some(target.clone()),
    };
    assert!(
        s.head()
            .prepare_placement(CommandId::deterministic("stale"), edit(None))
            .is_err()
    );
    apply(&mut s, edit(original.particles.clone()), "replace");
    let after = s.head().corrections.placements.as_ref().unwrap();
    assert_eq!(after[0].transform, original.transform);
    assert_eq!(after[0].name, original.name);
    assert_eq!(
        PlacementChanges::between(std::slice::from_ref(&original), after)
            .unwrap()
            .references,
        1
    );
    let mut invalid = object();
    invalid.particles.as_mut().unwrap().carrier_sha256 = ContentHash::from_bytes([9; 32]);
    assert!(
        session()
            .head()
            .prepare_placement(
                CommandId::deterministic("bad carrier"),
                PlacementEdit::Insert { placement: invalid }
            )
            .is_err()
    );
    let mut ordinary = object();
    ordinary.particles = None;
    let old = serde_json::to_value(&ordinary).unwrap();
    assert!(old.get("particles").is_none());
    assert_eq!(
        serde_json::from_value::<ManualPlacement>(old).unwrap(),
        ordinary
    );
}
