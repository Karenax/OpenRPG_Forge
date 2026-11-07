//! Local form drafts are independent of the accepted document revision.
use serde_json::Value;
use std::collections::BTreeMap;

#[derive(Default)]
pub(crate) struct DraftLedger {
    current: Option<String>,
    entries: BTreeMap<String, Entry>,
    submitted: Option<(String, String, Value, Option<Vec<usize>>)>,
}

struct Entry {
    source: Value,
    baseline: Value,
    draft: Value,
}

pub(crate) enum Reconcile {
    Load,
    Keep,
    Restore(Value),
}

impl DraftLedger {
    pub(crate) fn submit(&mut self, command: String, fields: Value, mask: Option<Vec<usize>>) {
        self.submitted = self
            .current
            .as_ref()
            .map(|key| (command, key.clone(), fields, mask));
    }

    pub(crate) fn acknowledge(&mut self, command: &str, key: &str, source: &Value) {
        let Some((id, target, fields, mask)) = &self.submitted else {
            return;
        };
        if id != command {
            return;
        }
        if let Some(entry) = self.entries.get_mut(target) {
            let matched = if let (
                Some(mask),
                Value::Array(baseline),
                Value::Array(current),
                Value::Array(submitted),
            ) = (mask, &mut entry.baseline, &entry.draft, fields)
            {
                let mut matched = true;
                for index in mask {
                    match (
                        baseline.get_mut(*index),
                        current.get(*index),
                        submitted.get(*index),
                    ) {
                        (Some(baseline), Some(current), Some(value)) if current == value => {
                            baseline.clone_from(value);
                        }
                        _ => matched = false,
                    }
                }
                matched
            } else if entry.draft == *fields {
                entry.baseline = fields.clone();
                true
            } else {
                false
            };
            if matched && target == key {
                entry.source = source.clone();
            }
        }
        self.submitted = None;
    }
    pub(crate) fn has_dirty(&self) -> bool {
        self.entries
            .values()
            .any(|entry| entry.draft != entry.baseline)
    }
    pub(crate) fn reconcile(&mut self, key: &str, source: &Value, local: Value) -> Reconcile {
        if let Some(current) = &self.current
            && let Some(entry) = self.entries.get_mut(current)
        {
            entry.draft = local;
        }
        let switched = self.current.as_deref() != Some(key);
        self.current = Some(key.into());
        let Some(entry) = self.entries.get(key) else {
            return Reconcile::Load;
        };
        if entry.draft != entry.baseline {
            return if switched {
                Reconcile::Restore(entry.draft.clone())
            } else {
                Reconcile::Keep
            };
        }
        if entry.source != *source {
            Reconcile::Load
        } else if switched {
            Reconcile::Restore(entry.draft.clone())
        } else {
            Reconcile::Keep
        }
    }

    pub(crate) fn loaded(&mut self, key: String, source: Value, local: Value) {
        self.entries.insert(
            key,
            Entry {
                source,
                baseline: local.clone(),
                draft: local,
            },
        );
    }

    pub(crate) fn conflict(&self, key: &str, source: &Value) -> bool {
        self.entries
            .get(key)
            .is_some_and(|entry| entry.source != *source && entry.draft != entry.baseline)
    }

    pub(crate) fn forget_current(&mut self) {
        self.submitted = None;
        if let Some(key) = self.current.take() {
            self.entries.remove(&key);
        }
    }

    pub(crate) fn clear(&mut self) {
        *self = Self::default();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    #[test]
    fn only_the_submitted_fields_of_the_accepted_command_become_clean() {
        let mut ledger = DraftLedger::default();
        let original = json!(["old name", "old mass"]);
        ledger.reconcile("object", &json!(0), original.clone());
        ledger.loaded("object".into(), json!(0), original);
        let draft = json!(["new name", "unsubmitted mass"]);
        ledger.reconcile("object", &json!(0), draft.clone());
        ledger.submit("rename".into(), draft.clone(), Some(vec![0]));
        ledger.acknowledge("terrain", "object", &json!(1));
        assert!(ledger.has_dirty());
        ledger.acknowledge("rename", "object", &json!(1));
        assert!(ledger.has_dirty());
        assert!(!ledger.conflict("object", &json!(1)));
        ledger.reconcile("object", &json!(1), json!(["new name", "old mass"]));
        assert!(!ledger.has_dirty());
    }

    #[test]
    fn edits_after_preview_are_not_acknowledged_by_applying_the_older_proposal() {
        let mut ledger = DraftLedger::default();
        ledger.reconcile("line", &json!(0), json!("old"));
        ledger.loaded("line".into(), json!(0), json!("old"));
        ledger.reconcile("line", &json!(0), json!("proposed"));
        ledger.submit("reply".into(), json!("proposed"), None);
        ledger.reconcile("line", &json!(1), json!("later draft"));
        ledger.acknowledge("reply", "line", &json!(1));
        assert!(ledger.has_dirty());
        assert!(ledger.conflict("line", &json!(1)));
        ledger.submit("new reply".into(), json!("later draft"), None);
        ledger.acknowledge("new reply", "line", &json!(2));
        assert!(!ledger.has_dirty());
    }

    #[test]
    fn independent_revisions_and_real_conflicts_preserve_drafts() {
        let mut ledger = DraftLedger::default();
        let source = json!({"text":"applied"});
        assert!(matches!(
            ledger.reconcile("line", &source, json!("empty")),
            Reconcile::Load
        ));
        ledger.loaded("line".into(), source.clone(), json!("applied"));
        assert!(matches!(
            ledger.reconcile("line", &source, json!("my draft")),
            Reconcile::Keep
        ));
        assert!(!ledger.conflict("line", &source));
        assert!(ledger.has_dirty());
        let changed = json!({"text":"external edit"});
        assert!(matches!(
            ledger.reconcile("line", &changed, json!("my draft")),
            Reconcile::Keep
        ));
        assert!(ledger.conflict("line", &changed));
    }

    #[test]
    fn switching_identity_recovers_each_local_draft_and_clean_forms_refresh() {
        let mut ledger = DraftLedger::default();
        ledger.reconcile("a", &json!(1), json!(null));
        ledger.loaded("a".into(), json!(1), json!("a"));
        assert!(matches!(
            ledger.reconcile("b", &json!(2), json!("draft a")),
            Reconcile::Load
        ));
        ledger.loaded("b".into(), json!(2), json!("b"));
        assert!(
            matches!(ledger.reconcile("a", &json!(3), json!("b")), Reconcile::Restore(value) if value == json!("draft a"))
        );
        ledger.forget_current();
        assert!(matches!(
            ledger.reconcile("a", &json!(3), json!("draft a")),
            Reconcile::Load
        ));
        ledger.loaded("a".into(), json!(3), json!("fresh"));
        assert!(matches!(
            ledger.reconcile("a", &json!(4), json!("fresh")),
            Reconcile::Load
        ));
        ledger.clear();
        assert!(!ledger.has_dirty());
        assert!(matches!(
            ledger.reconcile("a", &json!(4), json!("fresh")),
            Reconcile::Load
        ));
    }
}
