//! Bounded presentation drafts. These are never accepted document mutations.
use crate::atelier_transform::TransformFields;
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DraftScope {
    pub area: String,
    pub object: String,
    pub base_hash: String,
}

impl DraftScope {
    #[must_use]
    pub fn key(&self) -> String {
        format!("{}/{}", self.area, self.object)
    }

    #[must_use]
    pub fn valid(&self) -> bool {
        let identifier = |s: &str| {
            !s.is_empty()
                && s.len() <= 128
                && s.bytes().all(|b| b.is_ascii_alphanumeric() || b == b'-')
        };
        identifier(&self.area)
            && identifier(&self.object)
            && self.base_hash.len() == 64
            && self.base_hash.bytes().all(|b| b.is_ascii_hexdigit())
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ObjectInspector {
    pub scope: DraftScope,
    pub label: String,
    pub locked: bool,
    pub accepted: TransformFields,
}

impl ObjectInspector {
    #[must_use]
    pub fn valid(&self) -> bool {
        self.scope.valid() && self.label.len() <= 1024 && self.accepted.bounded()
    }
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TransformDraft {
    pub scope: DraftScope,
    pub fields: TransformFields,
}

#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct StoredDraft {
    pub version: u64,
    pub draft: TransformDraft,
}

/// Session-only mailbox survives secondary closure, but not application exit.
/// Compare-and-swap prevents two surfaces silently overwriting one another.
#[derive(Default)]
pub struct DraftStore {
    entries: BTreeMap<String, StoredDraft>,
}

impl DraftStore {
    pub fn save(&mut self, expected: u64, draft: TransformDraft) -> Result<(), String> {
        if !draft.scope.valid() || !draft.fields.bounded() {
            return Err("Brouillon de transformation hors limites".into());
        }
        let key = draft.scope.key();
        let current = self.entries.get(&key).map_or(0, |d| d.version);
        if current != expected {
            return Err("Ce brouillon a changé dans l’autre fenêtre. Votre saisie locale est conservée ; comparez avant de remplacer.".into());
        }
        if current == 0 && self.entries.len() >= 64 {
            return Err(
                "Limite de 64 brouillons atteinte. Aucun ancien brouillon n’a été effacé.".into(),
            );
        }
        let version = current
            .checked_add(1)
            .ok_or("Version de brouillon saturée")?;
        self.entries.insert(key, StoredDraft { version, draft });
        Ok(())
    }

    #[must_use]
    pub fn entries(&self) -> Vec<StoredDraft> {
        self.entries.values().cloned().collect()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn draft(object: &str) -> TransformDraft {
        TransformDraft {
            scope: DraftScope {
                area: "area-a".into(),
                object: object.into(),
                base_hash: "a".repeat(64),
            },
            fields: TransformFields::from_transform(forge_core::Transform::IDENTITY),
        }
    }
    #[test]
    fn concurrent_write_is_rejected_without_losing_accepted_draft() {
        let mut store = DraftStore::default();
        let original = draft("object-a");
        store.save(0, original.clone()).unwrap();
        let mut other = original.clone();
        other.fields.position[0] = "-".into();
        assert!(store.save(0, other.clone()).is_err());
        assert_eq!(store.entries()[0].draft, original);
        store.save(1, other.clone()).unwrap();
        assert_eq!(
            store.entries()[0],
            StoredDraft {
                version: 2,
                draft: other
            }
        );
    }
    #[test]
    fn quota_refuses_new_entry_without_evicting_old_ones() {
        let mut store = DraftStore::default();
        for n in 0..64 {
            store.save(0, draft(&format!("object-{n}"))).unwrap();
        }
        assert!(store.save(0, draft("one-too-many")).is_err());
        store.save(1, draft("object-0")).unwrap();
        assert_eq!(store.entries().len(), 64);
    }
    #[test]
    fn invalid_payload_preserves_store_and_revision_is_explicit() {
        let mut store = DraftStore::default();
        let mut value = draft("object-a");
        value.scope.base_hash = "not-a-hash".into();
        assert!(store.save(0, value).is_err());
        assert!(store.entries().is_empty());
        let mut value = draft("object-a");
        value.fields.scale[0] = "x".repeat(65);
        assert!(store.save(0, value).is_err());
        let a = draft("object-a");
        let mut b = a.clone();
        b.scope.base_hash = "b".repeat(64);
        assert_eq!(a.scope.key(), b.scope.key());
        assert_ne!(a.scope, b.scope);
    }
}
