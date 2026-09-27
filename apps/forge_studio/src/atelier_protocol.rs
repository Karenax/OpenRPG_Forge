//! Presentation-only relay. No terrain documents or editable copies live here.
use crate::atelier_drafts::{DraftStore, ObjectInspector, StoredDraft, TransformDraft};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SceneItem {
    pub key: String,
    pub label: String,
    pub topic: u8,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CatalogItem {
    pub id: String,
    pub category: String,
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Snapshot {
    pub project: String,
    pub selected: Option<String>,
    pub topic: u8,
    /// Main map view hides its editing panels only at the author's request.
    pub map_only: bool,
    pub dirty: bool,
    /// Read-only result from the authoritative editor, including command refusals.
    pub command_status: String,
    pub items: Vec<SceneItem>,
    pub catalog: Vec<CatalogItem>,
    pub catalog_selected: Option<String>,
    pub inspector: Option<ObjectInspector>,
}

impl Snapshot {
    pub fn validate(&self) -> Result<(), String> {
        if self.project.len() > 512
            || self.topic > 7
            || self.items.len() > 4096
            || self.catalog.len() > 4096
            || self.command_status.len() > 4096
        {
            return Err("Présentation de l’atelier hors limites".into());
        }
        let mut assets = std::collections::HashSet::new();
        for item in &self.catalog {
            if item.id.is_empty()
                || item.id.len() > 256
                || item.category.len() > 128
                || !assets.insert(&item.id)
            {
                return Err("Catalogue de présentation invalide".into());
            }
        }
        if self
            .catalog_selected
            .as_ref()
            .is_some_and(|id| !assets.contains(id))
        {
            return Err("Sélection absente du catalogue".into());
        }
        let mut keys = std::collections::HashSet::new();
        for item in &self.items {
            if item.key.is_empty()
                || item.key.len() > 256
                || item.label.len() > 1024
                || item.topic > 7
                || !keys.insert(&item.key)
            {
                return Err("Index de scène invalide".into());
            }
        }
        if self
            .selected
            .as_ref()
            .is_some_and(|key| !keys.contains(key))
        {
            return Err("Sélection absente de la scène".into());
        }
        if let Some(inspector) = &self.inspector
            && (!inspector.valid()
                || self.selected.as_deref()
                    != Some(format!("5:{}", inspector.scope.object).as_str()))
        {
            return Err("Inspecteur absent de la sélection courante".into());
        }
        Ok(())
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "action", rename_all = "snake_case", deny_unknown_fields)]
pub enum Intent {
    Select { key: String },
    Topic { index: u8 },
    MapOnly { enabled: bool },
    CatalogSelect { id: String },
    PreviewTransform { draft: Box<TransformDraft> },
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "operation", rename_all = "snake_case", deny_unknown_fields)]
pub enum Request {
    Open,
    Repatriate,
    Read,
    Publish {
        snapshot: Snapshot,
    },
    Submit {
        revision: u64,
        intent: Intent,
    },
    SaveDraft {
        expected: u64,
        draft: TransformDraft,
    },
}

#[derive(Clone, Debug, Default, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct Reply {
    pub open: bool,
    pub revision: u64,
    pub snapshot: Snapshot,
    pub pending: Vec<Intent>,
    pub drafts: Vec<StoredDraft>,
}

/// Bounded presentation state; the native adapter authenticates the caller window.
#[derive(Default)]
pub struct Relay {
    reply: Reply,
    drafts: DraftStore,
}

impl Relay {
    pub fn publish(&mut self, snapshot: Snapshot) -> Result<(), String> {
        snapshot.validate()?;
        if snapshot != self.reply.snapshot {
            self.reply.revision = self
                .reply
                .revision
                .checked_add(1)
                .ok_or("Révision saturée")?;
            self.reply.snapshot = snapshot;
            self.reply.pending.clear();
        }
        Ok(())
    }

    pub fn submit(&mut self, revision: u64, intent: Intent) -> Result<(), String> {
        if !self.reply.open || revision != self.reply.revision {
            return Err("L’atelier a changé : actualisez avant de sélectionner".into());
        }
        match &intent {
            Intent::Select { key }
                if self
                    .reply
                    .snapshot
                    .items
                    .iter()
                    .any(|item| &item.key == key) => {}
            Intent::Topic { index } if *index < 8 => {}
            Intent::MapOnly { .. } => {}
            Intent::CatalogSelect { id }
                if self.reply.snapshot.catalog.iter().any(|a| &a.id == id) => {}
            Intent::PreviewTransform { draft }
                if draft.fields.bounded()
                    && self
                        .reply
                        .snapshot
                        .inspector
                        .as_ref()
                        .is_some_and(|i| !i.locked && i.scope == draft.scope) => {}
            _ => return Err("Action de présentation invalide".into()),
        }
        // Selection is a latest-intent value, not an unbounded command queue.
        self.reply.pending.clear();
        self.reply.pending.push(intent);
        Ok(())
    }

    pub fn set_open(&mut self, open: bool) {
        self.reply.open = open;
        if !open {
            self.reply.pending.clear();
        }
    }

    pub fn save_draft(&mut self, expected: u64, draft: TransformDraft) -> Result<(), String> {
        self.drafts.save(expected, draft)
    }

    pub fn read(&mut self, main: bool) -> Reply {
        let mut reply = self.reply.clone();
        reply.drafts = self.drafts.entries();
        if main {
            self.reply.pending.clear();
        } else {
            reply.pending.clear();
        }
        reply
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn sample() -> Snapshot {
        Snapshot {
            project: "Village".into(),
            items: vec![SceneItem {
                key: "placement:1".into(),
                label: "Arche".into(),
                topic: 5,
            }],
            ..Snapshot::default()
        }
    }
    #[test]
    fn stale_and_unknown_selections_are_rejected() {
        let mut relay = Relay::default();
        relay.set_open(true);
        relay.publish(sample()).unwrap();
        assert!(relay.submit(0, Intent::Topic { index: 1 }).is_err());
        assert!(
            relay
                .submit(
                    1,
                    Intent::Select {
                        key: "absent".into()
                    }
                )
                .is_err()
        );
        assert!(relay.submit(1, Intent::Topic { index: 8 }).is_err());
    }
    #[test]
    fn only_main_drains_latest_intent_and_close_clears_it() {
        let mut relay = Relay::default();
        relay.set_open(true);
        relay.publish(sample()).unwrap();
        relay.submit(1, Intent::Topic { index: 1 }).unwrap();
        relay.submit(1, Intent::Topic { index: 5 }).unwrap();
        assert!(relay.read(false).pending.is_empty());
        assert_eq!(relay.read(true).pending, vec![Intent::Topic { index: 5 }]);
        assert!(relay.read(true).pending.is_empty());
        relay.submit(1, Intent::Topic { index: 1 }).unwrap();
        relay.set_open(false);
        assert!(relay.read(true).pending.is_empty());
        assert!(relay.submit(1, Intent::Topic { index: 1 }).is_err());
    }
    #[test]
    fn map_only_toggle_is_a_bounded_presentation_intent() {
        let mut relay = Relay::default();
        relay.set_open(true);
        relay.publish(sample()).unwrap();
        relay.submit(1, Intent::MapOnly { enabled: true }).unwrap();
        assert!(relay.read(false).pending.is_empty());
        assert_eq!(
            relay.read(true).pending,
            vec![Intent::MapOnly { enabled: true }]
        );
        assert!(relay.read(true).pending.is_empty());
        assert!(relay.submit(0, Intent::MapOnly { enabled: false }).is_err());
    }
    #[test]
    fn invalid_publication_preserves_previous_snapshot() {
        let mut relay = Relay::default();
        relay.publish(sample()).unwrap();
        let mut invalid = sample();
        invalid.items.push(invalid.items[0].clone());
        assert!(relay.publish(invalid).is_err());
        assert_eq!(relay.read(false).snapshot, sample());
    }
    #[test]
    fn catalog_selection_requires_a_published_reference() {
        let mut relay = Relay::default();
        let mut snapshot = sample();
        snapshot.catalog.push(CatalogItem {
            id: "tree-a".into(),
            category: "Arbres".into(),
        });
        relay.publish(snapshot.clone()).unwrap();
        relay.set_open(true);
        assert!(
            relay
                .submit(
                    1,
                    Intent::CatalogSelect {
                        id: "unknown".into()
                    }
                )
                .is_err()
        );
        relay
            .submit(
                1,
                Intent::CatalogSelect {
                    id: "tree-a".into(),
                },
            )
            .unwrap();
        assert_eq!(relay.read(true).pending.len(), 1);
        snapshot.catalog_selected = Some("unknown".into());
        assert!(relay.publish(snapshot).is_err());
    }

    #[test]
    fn command_result_survives_polling_and_rejects_oversized_updates() {
        let mut relay = Relay::default();
        let mut snapshot = sample();
        snapshot.command_status = "Nombre incomplet ou invalide".into();
        relay.publish(snapshot.clone()).unwrap();
        for _ in 0..3 {
            assert_eq!(
                relay.read(false).snapshot.command_status,
                snapshot.command_status
            );
        }
        let mut oversized = snapshot;
        oversized.command_status = "x".repeat(4097);
        assert!(relay.publish(oversized).is_err());
        assert_eq!(
            relay.read(false).snapshot.command_status,
            "Nombre incomplet ou invalide"
        );
        let mut accepted = sample();
        accepted.command_status = "Aperçu prêt".into();
        relay.publish(accepted).unwrap();
        assert_eq!(relay.read(false).snapshot.command_status, "Aperçu prêt");
    }

    #[test]
    fn detached_draft_survives_repatriation_but_stale_preview_is_refused() {
        use crate::{atelier_drafts::DraftScope, atelier_transform::TransformFields};
        let mut relay = Relay::default();
        let scope = DraftScope {
            area: "area-a".into(),
            object: "object-a".into(),
            base_hash: "a".repeat(64),
        };
        let fields = TransformFields::from_transform(forge_core::Transform::IDENTITY);
        let mut snapshot = sample();
        snapshot.items[0].key = "5:object-a".into();
        snapshot.selected = Some("5:object-a".into());
        snapshot.inspector = Some(ObjectInspector {
            scope: scope.clone(),
            label: "Arche".into(),
            locked: false,
            accepted: fields.clone(),
        });
        relay.publish(snapshot.clone()).unwrap();
        relay.set_open(true);
        let draft = TransformDraft { scope, fields };
        relay.save_draft(0, draft.clone()).unwrap();
        relay
            .submit(
                1,
                Intent::PreviewTransform {
                    draft: Box::new(draft.clone()),
                },
            )
            .unwrap();
        relay.set_open(false);
        assert!(relay.read(true).pending.is_empty());
        assert_eq!(relay.read(false).drafts[0].draft, draft);
        relay.set_open(true);
        snapshot.inspector.as_mut().unwrap().scope.base_hash = "b".repeat(64);
        relay.publish(snapshot).unwrap();
        assert!(
            relay
                .submit(
                    2,
                    Intent::PreviewTransform {
                        draft: Box::new(draft)
                    }
                )
                .is_err()
        );
        assert_eq!(relay.read(false).drafts.len(), 1);
    }
}
