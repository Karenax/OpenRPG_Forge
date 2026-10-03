#[path = "../src/atelier_drafts.rs"]
mod atelier_drafts;
#[path = "../src/atelier_protocol.rs"]
#[expect(
    dead_code,
    reason = "This selection fixture loads the shared protocol but does not dispatch the host Request enum."
)]
mod atelier_protocol;
#[path = "../src/atelier_selection.rs"]
mod atelier_selection;
#[path = "../src/atelier_transform.rs"]
mod atelier_transform;

use atelier_protocol::{Relay, SceneItem, Snapshot};
use atelier_selection::{SCENE_ITEM_LIMIT, preserve_selected_object_item, resolve_object_key};
use forge_core::EntityId;

fn object(seed: &str) -> EntityId {
    EntityId::deterministic(seed)
}

fn row(id: EntityId) -> SceneItem {
    SceneItem {
        key: format!("5:{id}"),
        label: "Objet conservé".into(),
        topic: 5,
    }
}

fn publish_selected(items: Vec<SceneItem>, id: EntityId) -> Snapshot {
    let key = format!("5:{id}");
    let snapshot = Snapshot {
        project: "Carte auteur".into(),
        topic: 5,
        selected: items.iter().any(|item| item.key == key).then_some(key),
        items,
        inspector: Some(atelier_drafts::ObjectInspector {
            scope: atelier_drafts::DraftScope {
                area: object("area").to_string(),
                object: id.to_string(),
                base_hash: "a".repeat(64),
            },
            label: "Plante héritée".into(),
            locked: false,
            accepted: atelier_transform::TransformFields::from_transform(
                serde_json::from_str(r#"{"translation_m":{"x":0.0,"y":0.0,"z":0.0},"rotation":{"x":0.0,"y":0.0,"z":0.0,"w":1.0},"scale":{"x":1.0,"y":1.0,"z":1.0}}"#).unwrap(),
            ),
        }),
        ..Snapshot::default()
    };
    Relay::default()
        .publish(snapshot.clone())
        .expect("A resolved plant and its inspector must publish together");
    snapshot
}

#[test]
fn resolved_batched_object_publishes_with_its_inspector_and_siblings() {
    let plant = object("plant");
    let sibling = row(object("sibling"));
    let mut items = vec![sibling.clone()];
    assert!(publish_without_selected(items.clone(), plant).is_err());
    preserve_selected_object_item(&mut items, Some((plant, "Plante héritée".into())));
    let snapshot = publish_selected(items, plant);
    assert_eq!(snapshot.items[0], sibling);
    assert_eq!(snapshot.selected, Some(format!("5:{plant}")));
}

fn publish_without_selected(items: Vec<SceneItem>, plant: EntityId) -> Result<(), String> {
    // Reproduce the previous producer's incomplete membership without weakening the relay.
    let mut complete = vec![];
    preserve_selected_object_item(&mut complete, Some((plant, "Plante héritée".into())));
    let mut snapshot = publish_selected(complete, plant);
    snapshot.items = items;
    snapshot.selected = None;
    Relay::default().publish(snapshot)
}

#[test]
fn full_presentation_retains_selected_object_within_the_limit() {
    let plant = object("outside-initial-page");
    let mut items = (0..SCENE_ITEM_LIMIT)
        .map(|i| row(object(&format!("sibling/{i}"))))
        .collect::<Vec<_>>();
    let retained = items[..SCENE_ITEM_LIMIT - 1].to_vec();
    preserve_selected_object_item(&mut items, Some((plant, "Plante héritée".into())));
    let snapshot = publish_selected(items, plant);
    assert_eq!(snapshot.items.len(), SCENE_ITEM_LIMIT);
    assert_eq!(&snapshot.items[..SCENE_ITEM_LIMIT - 1], retained.as_slice());
    assert_eq!(snapshot.items.last().unwrap().key, format!("5:{plant}"));
}

#[test]
fn existing_selection_does_not_duplicate_or_reorder_the_scene() {
    let selected = object("already-listed");
    let before = vec![row(object("before")), row(selected), row(object("after"))];
    let mut items = before.clone();
    preserve_selected_object_item(&mut items, Some((selected, "Plante héritée".into())));
    assert_eq!(items, before);
    publish_selected(items, selected);
}

#[test]
fn unresolved_selection_creates_no_scene_item() {
    let before = vec![row(object("unchanged"))];
    let mut items = before.clone();
    preserve_selected_object_item(&mut items, None);
    assert_eq!(items, before);
}

#[test]
fn invalid_object_keys_do_not_invoke_the_current_head_lookup() {
    let id = object("plant");
    for key in [
        format!("2:{id}"),
        "5:invalid".into(),
        format!("5:{id}:extra"),
    ] {
        assert_eq!(
            resolve_object_key(&key, |_| panic!("Invalid key must not resolve an object")),
            None
        );
    }
}

#[test]
fn object_selection_requires_resolution_in_the_current_head() {
    let id = object("plant");
    let key = format!("5:{id}");
    let mut calls = 0;
    assert_eq!(
        resolve_object_key(&key, |candidate| {
            calls += 1;
            assert_eq!(candidate, id);
            false
        }),
        None
    );
    assert_eq!(calls, 1);
    assert_eq!(
        resolve_object_key(&key, |candidate| candidate == id),
        Some(id)
    );
}
