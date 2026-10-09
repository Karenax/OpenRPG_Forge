//! Acquired procedural source frames through the actual Studio binding and project commands.
#[allow(
    dead_code,
    reason = "The scoped fixture exercises object binding, not raster association helpers."
)]
#[path = "../../../apps/forge_studio/src/authoring_assets/imported_buildings/source_objects/binding.rs"]
mod binding;

use forge_assets::tidewater_debris::{self, native::NativeInput};
use forge_core::{CommandId, EntityId, Transform, Vec3};
use forge_project::{
    authoring::new_terrain,
    zone_authoring::{
        placements::{ManualPlacement, PlacementEdit},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
        source_objects::{SourceObjectState, resolve},
    },
};
use serde::Deserialize;

#[derive(Deserialize)]
struct Description {
    props: Vec<Object>,
}
#[derive(Deserialize)]
struct Object {
    source_id: String,
    kind: String,
    source_frame: Frame,
    parts: Vec<Part>,
}
#[derive(Deserialize)]
struct Frame {
    translation_m: [f64; 3],
}
#[derive(Deserialize)]
struct Part {
    material: String,
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Verified source coordinates narrow to the common f32 domain, as in the Engine adapter."
)]
fn source(session: &ZoneEditSession, object: &Object, ordinal: usize) -> ManualPlacement {
    let [x, y, z] = object.source_frame.translation_m.map(|v| v as f32);
    binding::placement(
        session.head().base.terrain.area_id,
        &object.source_id,
        object.kind.clone(),
        tidewater_debris::asset_id(ordinal).unwrap(),
        tidewater_debris::package_hash(),
        Transform::from_translation(Vec3::new(x, y, z)),
    )
}

fn edit(session: &mut ZoneEditSession, source: &ManualPlacement, action: PlacementEdit, key: &str) {
    let request = session
        .head()
        .prepare_source_object(source.clone(), action, CommandId::deterministic(key))
        .unwrap();
    session.execute(&request).unwrap();
}

fn history(session: &mut ZoneEditSession, action: ZoneAction, key: &str) {
    let request = ZoneRequest {
        command_id: CommandId::deterministic(key),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action,
    };
    session.execute(&request).unwrap();
}

#[test]
#[ignore = "Requires the acquired scanned package; common Studio binding and commands, no GPU/UI."]
#[allow(
    clippy::too_many_lines,
    reason = "Exercise each acquired prototype through complete history and reopen."
)]
fn acquired_scanned_variations_keep_zone_identity_and_removed_state() {
    use forge_assets::tidewater_debris::scanned::{ScannedPackage, geometry, templates};
    let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
    let package = ScannedPackage::load(
        &std::fs::read(root.join("package.json")).unwrap(),
        |name, _| std::fs::read(root.join(name)).map_err(|error| error.to_string()),
    )
    .unwrap();
    let templates = templates::prepare(&geometry::prepare(&package).unwrap()).unwrap();
    assert_eq!(templates.len(), 141);
    let mut session = ZoneEditSession::from_initial_terrain(
        new_terrain(
            "scanned-common-fixture",
            "zone",
            "Scanned authoring fixture",
        )
        .unwrap(),
    )
    .unwrap();
    let other = ZoneEditSession::from_initial_terrain(
        new_terrain("scanned-common-other", "zone", "Other zone").unwrap(),
    )
    .unwrap();
    let bind = |head: &forge_project::zone_authoring::ZoneAuthoringHead,
                template: &templates::Template| {
        binding::placement(
            head.base.terrain.area_id,
            &template.instance.source_id,
            template.name.clone(),
            template.asset_id,
            template.asset_hash,
            template.transform,
        )
    };
    let mut identities = std::collections::BTreeSet::new();
    for template in &templates {
        let original = bind(session.head(), template);
        assert!(identities.insert(original.id));
        assert_ne!(original.id, bind(other.head(), template).id);
        assert!(
            binding::select_source(
                other.head(),
                &original,
                false,
                binding::SourcePose::PackageTransform
            )
            .unwrap()
            .is_none()
        );
    }
    for kind in 0..4 {
        let template = templates
            .iter()
            .find(|t| t.instance.source.asset == kind)
            .unwrap();
        let original = bind(session.head(), template);
        let variation = template.instance.source.rnd.to_bits();
        let mut transform = original.transform;
        transform.translation_m.x += 2.5;
        transform.rotation = forge_core::UnitQuaternion::new(0.0, 0.5, 0.0, 0.866_025_4);
        transform.scale = Vec3::new(1.3, 0.8, 1.1);
        edit(
            &mut session,
            &original,
            PlacementEdit::Transform {
                id: original.id,
                transform,
            },
            &format!("scanned-{kind}-transform"),
        );
        let copy = EntityId::deterministic(format!("scanned-{kind}-copy"));
        edit(
            &mut session,
            &original,
            PlacementEdit::Duplicate {
                source_id: original.id,
                id: copy,
                transform,
            },
            &format!("scanned-{kind}-duplicate"),
        );
        edit(
            &mut session,
            &original,
            PlacementEdit::SetLocked {
                id: copy,
                locked: true,
            },
            &format!("scanned-{kind}-lock"),
        );
        session =
            ZoneEditSession::from_archive_bytes(&session.delta_archive_bytes().unwrap()).unwrap();
        let reopened = resolve(session.head(), &original).unwrap();
        assert_eq!(reopened.len(), 2);
        assert!(reopened.iter().all(|p| p.transform == transform
            && p.asset_id == template.asset_id
            && p.asset_hash == template.asset_hash));
        assert!(reopened.iter().find(|p| p.id == copy).unwrap().locked);
        edit(
            &mut session,
            &original,
            PlacementEdit::SetLocked {
                id: copy,
                locked: false,
            },
            &format!("scanned-{kind}-unlock"),
        );
        edit(
            &mut session,
            &original,
            PlacementEdit::Remove { id: original.id },
            &format!("scanned-{kind}-remove-source"),
        );
        edit(
            &mut session,
            &original,
            PlacementEdit::Remove { id: copy },
            &format!("scanned-{kind}-remove-copy"),
        );
        assert!(resolve(session.head(), &original).unwrap().is_empty());
        history(
            &mut session,
            ZoneAction::Undo,
            &format!("scanned-{kind}-undo"),
        );
        assert_eq!(resolve(session.head(), &original).unwrap()[0].id, copy);
        history(
            &mut session,
            ZoneAction::Redo,
            &format!("scanned-{kind}-redo"),
        );
        session =
            ZoneEditSession::from_archive_bytes(&session.delta_archive_bytes().unwrap()).unwrap();
        assert!(
            binding::select_source(
                session.head(),
                &original,
                true,
                binding::SourcePose::PackageTransform
            )
            .unwrap()
            .is_some()
        );
        assert!(resolve(session.head(), &original).unwrap().is_empty());
        assert_eq!(template.instance.source.rnd.to_bits(), variation);
    }
}

#[test]
#[ignore = "Requires the pinned procedural native acquisition; CPU authoring only, no Studio UI."]
#[allow(
    clippy::too_many_lines,
    reason = "One acquired lifecycle keeps source pins, sparse edits and two reopen checks in the same scenario."
)]
fn acquired_debris_edits_copies_and_final_deletion_survive_history_and_reopen() {
    let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_NATIVE_FIXTURE").unwrap());
    let raw = std::fs::read(root.join("debris-native.json")).unwrap();
    let input = NativeInput::load(&raw, forge_core::bytes_sha256(&raw), |name, _| {
        std::fs::read(root.join(name)).map_err(|error| error.to_string())
    })
    .unwrap();
    let description: Description = serde_json::from_value(input.geometry.frames().clone()).unwrap();
    assert_eq!(description.props.len(), 805);
    let mut session = ZoneEditSession::from_initial_terrain(
        new_terrain("debris-common-fixture", "zone", "Authoring fixture").unwrap(),
    )
    .unwrap();
    let other = ZoneEditSession::from_initial_terrain(
        new_terrain("debris-common-other", "zone", "Other zone").unwrap(),
    )
    .unwrap();
    let identities: std::collections::BTreeSet<_> = description
        .props
        .iter()
        .enumerate()
        .map(|(ordinal, object)| source(&session, object, ordinal).id)
        .collect();
    assert_eq!(identities.len(), 805);
    for (ordinal, object) in description.props.iter().enumerate() {
        let original = source(&session, object, ordinal);
        assert_ne!(original.id, source(&other, object, ordinal).id);
        assert!(
            binding::select_source(
                other.head(),
                &original,
                false,
                binding::SourcePose::PackageTransform
            )
            .unwrap()
            .is_none()
        );
    }
    let mut exercised = std::collections::BTreeSet::new();
    for role in ["wood", "fabric", "nature"] {
        let (ordinal, object) = description
            .props
            .iter()
            .enumerate()
            .find(|(ordinal, object)| {
                !exercised.contains(ordinal)
                    && object.parts.iter().any(|part| part.material == role)
            })
            .unwrap();
        exercised.insert(ordinal);
        let original = source(&session, object, ordinal);
        let mut transform = original.transform;
        transform.translation_m.x += 1.25;
        transform.scale = Vec3::new(1.2, 0.9, 1.1);
        edit(
            &mut session,
            &original,
            PlacementEdit::Transform {
                id: original.id,
                transform,
            },
            &format!("debris-{role}-transform"),
        );
        let copy = EntityId::deterministic(format!("debris-{role}-copy"));
        edit(
            &mut session,
            &original,
            PlacementEdit::Duplicate {
                source_id: original.id,
                id: copy,
                transform,
            },
            &format!("debris-{role}-duplicate"),
        );
        let before = resolve(session.head(), &original).unwrap();
        assert_eq!(before.len(), 2);
        assert!(before.iter().all(|p| p.transform == transform
            && p.asset_id == original.asset_id
            && p.asset_hash == original.asset_hash));
        session =
            ZoneEditSession::from_archive_bytes(&session.delta_archive_bytes().unwrap()).unwrap();
        assert_eq!(resolve(session.head(), &original).unwrap(), before);
        edit(
            &mut session,
            &original,
            PlacementEdit::Remove { id: original.id },
            &format!("debris-{role}-remove-original"),
        );
        edit(
            &mut session,
            &original,
            PlacementEdit::Remove { id: copy },
            &format!("debris-{role}-remove-copy"),
        );
        assert!(resolve(session.head(), &original).unwrap().is_empty());
        history(
            &mut session,
            ZoneAction::Undo,
            &format!("debris-{role}-undo"),
        );
        let restored = resolve(session.head(), &original).unwrap();
        assert_eq!(restored.len(), 1);
        assert_eq!(restored[0].id, copy);
        history(
            &mut session,
            ZoneAction::Redo,
            &format!("debris-{role}-redo"),
        );
        session =
            ZoneEditSession::from_archive_bytes(&session.delta_archive_bytes().unwrap()).unwrap();
        let retained = binding::select_source(
            session.head(),
            &original,
            true,
            binding::SourcePose::PackageTransform,
        )
        .unwrap()
        .unwrap();
        assert_eq!(retained, original);
        assert!(resolve(session.head(), &retained).unwrap().is_empty());
        let records = session
            .head()
            .corrections
            .source_object_edits
            .as_ref()
            .unwrap();
        let record = &records[records
            .binary_search_by_key(&original.id, |r| r.source.id)
            .unwrap()];
        assert!(matches!(record.state, SourceObjectState::Removed));
        assert!(record.copies.is_empty());
    }
    assert_eq!(exercised.len(), 3);
}
