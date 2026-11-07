//! Synchronize individual applied records without replacing local field drafts.
use crate::editor_draft::{DraftLedger, Reconcile};
use leptos::prelude::*;
use serde::{Serialize, de::DeserializeOwned};
use serde_json::Value;
use std::sync::Arc;

/// Select records by their semantic identity, rather than the complete map hash.
pub(super) fn records(
    state: RwSignal<Option<forge_project::authoring::AuthoringState>>,
    selected: &str,
) -> Value {
    fn visit(value: &Value, selected: &str, found: &mut Vec<Value>) {
        match value {
            Value::Object(fields) => {
                if ["id", "quest", "actor", "object"]
                    .iter()
                    .any(|key| fields.get(*key).and_then(Value::as_str) == Some(selected))
                {
                    found.push(value.clone());
                } else {
                    for value in fields.values() {
                        visit(value, selected, found);
                    }
                }
            }
            Value::Array(values) => {
                for value in values {
                    visit(value, selected, found);
                }
            }
            _ => (),
        }
    }
    let document = state
        .get()
        .and_then(|state| state.zone_head)
        .and_then(|head| head.corrections.gameplay);
    let value = serde_json::to_value(document).unwrap_or_default();
    let mut found = Vec::new();
    if !selected.is_empty() {
        visit(&value, selected, &mut found);
        let dialogues = found
            .iter()
            .filter_map(|record| {
                record
                    .get("dialogue")
                    .and_then(Value::as_str)
                    .map(str::to_owned)
            })
            .collect::<Vec<_>>();
        for dialogue in dialogues {
            visit(&value, &dialogue, &mut found);
        }
    }
    Value::Array(found)
}

#[derive(Clone)]
pub(super) struct Field {
    read: Arc<dyn Fn() -> Value + Send + Sync>,
    write: Arc<dyn Fn(Value) + Send + Sync>,
}

impl Field {
    pub(super) fn new<T>(signal: RwSignal<T>) -> Self
    where
        T: Clone + Serialize + DeserializeOwned + Send + Sync + 'static,
    {
        Self {
            read: Arc::new(move || {
                serde_json::to_value(signal.get()).expect("serializable draft field")
            }),
            write: Arc::new(move |value| {
                if let Ok(value) = serde_json::from_value(value) {
                    signal.set(value);
                }
            }),
        }
    }
}

#[derive(Clone, Copy)]
pub(super) struct Notice {
    source: RwSignal<Option<String>>,
    reload: RwSignal<u64>,
    ledger: StoredValue<DraftLedger>,
    fields: StoredValue<Vec<Field>>,
}

impl Notice {
    pub(super) fn submitter(self, send: Callback<super::Edit>) -> Callback<super::Edit> {
        self.submitter_with_mask(send, None)
    }
    pub(super) fn submitter_for(
        self,
        send: Callback<super::Edit>,
        mask: &'static [usize],
    ) -> Callback<super::Edit> {
        self.submitter_with_mask(send, Some(mask))
    }
    fn submitter_with_mask(
        self,
        send: Callback<super::Edit>,
        mask: Option<&'static [usize]>,
    ) -> Callback<super::Edit> {
        Callback::new(move |request| {
            if let Ok(request) = &request
                && let Some(id) = crate::authoring_controls::request_command_id(request)
            {
                let fields = untrack(|| {
                    self.fields.with_value(|fields| {
                        Value::Array(fields.iter().map(|field| (field.read)()).collect())
                    })
                });
                self.ledger.update_value(|ledger| {
                    ledger.submit(id.to_string(), fields, mask.map(<[usize]>::to_vec));
                });
            }
            send.run(request);
        })
    }
}

pub(super) fn bind(
    source: impl Fn() -> (String, Value) + Send + Sync + 'static,
    fields: Vec<Field>,
    load: impl Fn() + Send + Sync + 'static,
) -> Notice {
    let source = Memo::new(move |_| source());
    let area = use_context::<Signal<Option<forge_core::AreaId>>>();
    let registry = use_context::<crate::authoring_controls::DraftRegistry>();
    let registration = registry.map(crate::authoring_controls::DraftRegistry::register);
    on_cleanup(move || {
        if let (Some(registry), Some(id)) = (registry, registration) {
            registry.mark(id, false);
        }
    });
    let notice = Notice {
        source: RwSignal::new(None),
        reload: RwSignal::new(0),
        ledger: StoredValue::new(DraftLedger::default()),
        fields: StoredValue::new(fields),
    };
    Effect::new(
        move |previous: Option<(Option<u64>, Option<forge_core::AreaId>)>| {
            let generation =
                registry.map(crate::authoring_controls::DraftRegistry::reset_generation);
            let current_area = area.and_then(|area| area.get());
            let scope = (generation, current_area);
            if previous.is_some() && previous != Some(scope) {
                notice.ledger.update_value(DraftLedger::clear);
            }
            let _ = notice.reload.get();
            let (key, source) = source.get();
            let fields = notice.fields.get_value();
            let key = format!(
                "{}:{key}",
                current_area
                    .map(|area| area.to_string())
                    .unwrap_or_default()
            );
            let snapshot = || Value::Array(fields.iter().map(|field| (field.read)()).collect());
            let local = snapshot();
            let mut decision = Reconcile::Keep;
            notice
                .ledger
                .update_value(|ledger| decision = ledger.reconcile(&key, &source, local));
            if let Some(id) =
                registry.and_then(crate::authoring_controls::DraftRegistry::applied_command)
            {
                notice
                    .ledger
                    .update_value(|ledger| ledger.acknowledge(&id.to_string(), &key, &source));
            }
            match decision {
                Reconcile::Load => {
                    untrack(&load);
                    notice.ledger.update_value(|ledger| {
                        ledger.loaded(key.clone(), source.clone(), snapshot());
                    });
                }
                Reconcile::Restore(Value::Array(values)) => {
                    for (field, value) in fields.iter().zip(values) {
                        (field.write)(value);
                    }
                }
                _ => (),
            }
            let conflict = notice
                .ledger
                .with_value(|ledger| ledger.conflict(&key, &source));
            if let (Some(registry), Some(id)) = (registry, registration) {
                registry.mark(id, notice.ledger.with_value(DraftLedger::has_dirty));
            }
            notice
                .source
                .set(conflict.then(|| serde_json::to_string_pretty(&source).unwrap_or_default()));
            scope
        },
    );
    notice
}

#[component]
pub(super) fn DraftNotice(notice: Notice) -> impl IntoView {
    view! {
        <Show when=move || notice.source.get().is_some()>
            <aside class="draft-conflict" role="status">
                <p>"La version appliquée de cet élément a changé. Votre brouillon est conservé dans les champs ci-dessous ; comparez les versions avant d’examiner une retouche."</p>
                <details><summary>"Voir la version appliquée"</summary><pre>{move || notice.source.get()}</pre></details>
                <button type="button" on:click=move |_| {
                    notice.ledger.update_value(DraftLedger::forget_current);
                    notice.reload.update(|n| *n = n.wrapping_add(1));
                }>"Remplacer ce brouillon par la version appliquée"</button>
            </aside>
        </Show>
    }
}
