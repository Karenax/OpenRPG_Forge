//! Keep a resolved batched object in the bounded presentation of the editor.
use crate::atelier_protocol::SceneItem;
use forge_core::EntityId;

pub(super) const SCENE_ITEM_LIMIT: usize = 4096;

pub(super) fn preserve_selected_object_item(
    items: &mut Vec<SceneItem>,
    selected: Option<(EntityId, String)>,
) {
    let Some((id, label)) = selected else {
        return;
    };
    let key = format!("5:{id}");
    items.truncate(SCENE_ITEM_LIMIT);
    if items.iter().any(|item| item.key == key) {
        return;
    }
    if items.len() == SCENE_ITEM_LIMIT {
        let _ = items.pop();
    }
    items.push(SceneItem {
        key,
        label,
        topic: 5,
    });
}

/// Selection is revalidated in the current head after an asynchronous reply.
pub(super) fn resolve_object_key(
    key: &str,
    lookup: impl FnOnce(EntityId) -> bool,
) -> Option<EntityId> {
    let id = key.strip_prefix("5:")?.parse::<EntityId>().ok()?;
    lookup(id).then_some(id)
}
