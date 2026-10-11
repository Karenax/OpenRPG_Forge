//! Shared form draft client; pending text outlives a selected object's form.
use crate::{
    atelier_drafts::{ObjectInspector, StoredDraft, TransformDraft},
    atelier_protocol::{Intent, Reply, Request},
    atelier_ui::{invoke, periodic},
};
use leptos::prelude::*;
use std::collections::BTreeMap;

#[derive(Clone)]
struct Pending {
    expected: u64,
    draft: TransformDraft,
}

#[derive(Clone, Copy)]
pub(super) struct DraftClient {
    pending: RwSignal<BTreeMap<String, Pending>>,
    remote: RwSignal<Vec<StoredDraft>>,
    sending: RwSignal<bool>,
    error: RwSignal<String>,
    closing: RwSignal<bool>,
}

impl DraftClient {
    pub fn new() -> Self {
        let client = Self {
            pending: RwSignal::new(BTreeMap::new()),
            remote: RwSignal::new(Vec::new()),
            sending: RwSignal::new(false),
            error: RwSignal::new(String::new()),
            closing: RwSignal::new(false),
        };
        periodic(move || client.flush());
        client
    }

    pub fn observe(self, drafts: &[StoredDraft]) {
        // An older poll may finish after a save acknowledgement. Never regress.
        self.remote.update(|current| {
            for incoming in drafts {
                if let Some(old) = current
                    .iter_mut()
                    .find(|v| v.draft.scope.key() == incoming.draft.scope.key())
                {
                    if incoming.version > old.version {
                        *old = incoming.clone();
                    }
                } else {
                    current.push(incoming.clone());
                }
            }
        });
    }

    pub fn has_newer(self, drafts: &[StoredDraft]) -> bool {
        self.remote.with_untracked(|known| {
            drafts.iter().any(|incoming| {
                known
                    .iter()
                    .find(|d| d.draft.scope.key() == incoming.draft.scope.key())
                    .is_none_or(|d| incoming.version > d.version)
            })
        })
    }

    fn value(self, inspector: &ObjectInspector) -> TransformDraft {
        let key = inspector.scope.key();
        self.pending
            .with(|p| p.get(&key).map(|p| p.draft.clone()))
            .or_else(|| {
                self.remote.with(|r| {
                    r.iter()
                        .find(|d| d.draft.scope.key() == key)
                        .map(|d| d.draft.clone())
                })
            })
            .unwrap_or_else(|| TransformDraft {
                scope: inspector.scope.clone(),
                fields: inspector.accepted.clone(),
            })
    }

    fn change(self, draft: TransformDraft) {
        // Local edits invalidate immediately. A later save acknowledgement must
        // not cancel a fresh preview submitted after this edit was acknowledged.
        crate::authoring_shortcuts::cancel_preview();
        let key = draft.scope.key();
        let expected = self.remote.with_untracked(|r| {
            r.iter()
                .find(|d| d.draft.scope.key() == key)
                .map_or(0, |d| d.version)
        });
        self.pending.update(|p| {
            p.entry(key)
                .and_modify(|p| p.draft = draft.clone())
                .or_insert(Pending { expected, draft });
        });
        // All queued objects belong to this client, not to a disposable form.
        self.flush();
    }

    pub fn close(self) {
        self.closing.set(true);
        self.flush();
    }

    fn flush(self) {
        if self.sending.get_untracked() || !self.error.with_untracked(String::is_empty) {
            return;
        }
        let next = self
            .pending
            .with_untracked(|p| p.iter().next().map(|(k, v)| (k.clone(), v.clone())));
        if let Some((key, pending)) = next {
            self.sending.set(true);
            wasm_bindgen_futures::spawn_local(async move {
                match invoke(Request::SaveDraft {
                    expected: pending.expected,
                    draft: pending.draft.clone(),
                })
                .await
                {
                    Ok(reply) => {
                        self.observe(&reply.drafts);
                        let version = reply
                            .drafts
                            .iter()
                            .find(|d| d.draft.scope.key() == key)
                            .map(|d| d.version);
                        if let Some(version) = version {
                            self.pending.update(|all| {
                                if let Some(newer) = all.get_mut(&key) {
                                    if newer.draft == pending.draft {
                                        all.remove(&key);
                                    } else {
                                        newer.expected = version;
                                    }
                                }
                            });
                        } else {
                            self.error
                                .set("Accusé de conservation absent ; fermeture suspendue.".into());
                        }
                    }
                    Err(error) => self.error.set(error),
                }
                self.sending.set(false);
            });
        } else if self.closing.get_untracked() {
            self.sending.set(true);
            wasm_bindgen_futures::spawn_local(async move {
                if let Err(error) = invoke(Request::Repatriate).await {
                    self.error.set(error);
                }
                self.sending.set(false);
            });
        }
    }
}

#[component]
pub(super) fn Inspector(
    #[prop(into)] reply: Signal<Reply>,
    client: DraftClient,
    submit: Callback<Intent>,
) -> impl IntoView {
    let inspector = Memo::new(move |_| reply.get().snapshot.inspector);
    let value = Memo::new(move |_| inspector.get().map(|i| client.value(&i)));
    let stale = move || match (inspector.get(), value.get()) {
        (Some(i), Some(v)) => i.scope != v.scope,
        _ => false,
    };
    view! {
        <section class="atelier-transform-inspector">
            <Show when=move ||inspector.get().is_some() fallback=||view!{<p>"Sélectionnez un objet placé dans la scène pour modifier ses propriétés."</p>}>
                <header class="atelier-inspected-object"><strong>{move ||inspector.get().map(|i|i.label).unwrap_or_default()}</strong><span>{move ||if inspector.get().is_some_and(|i|i.locked){"Lecture seule"}else{"Transformation précise"}}</span></header>
                <fieldset disabled=move ||inspector.get().is_none_or(|i|i.locked)||stale()||client.closing.get()>
                    {[("Position · m",0),("Rotation relative · °",1),("Échelle",2)].into_iter().map(|(label,group)|view! {
                        <div class="atelier-transform-group"><h3>{label}</h3><div class="atelier-transform-axes">
                            {['X','Y','Z'].into_iter().enumerate().map(move |(axis,name)|view! {
                                <label>{name.to_string()}<input inputmode="decimal" maxlength="64" aria-label=format!("{label} · {name}")
                                    prop:value=move || value.get().map(|v| match group {0=>v.fields.position[axis].clone(),1=>v.fields.rotation_delta[axis].clone(),_=>v.fields.scale[axis].clone()}).unwrap_or_default()
                                    on:input=move |event| { if let Some(mut draft)=value.get_untracked() {
                                        let text=event_target_value(&event);
                                        match group {0=>draft.fields.position[axis]=text,1=>draft.fields.rotation_delta[axis]=text,_=>draft.fields.scale[axis]=text}
                                        client.change(draft);
                                    }}/></label>
                            }).collect_view()}
                        </div></div>
                    }).collect_view()}
                    <button disabled=move ||client.sending.get()||!client.pending.with(BTreeMap::is_empty)
                        on:click=move |_|if let Some(draft)=value.get_untracked(){submit.run(Intent::PreviewTransform{draft:Box::new(draft)});}>
                        "Prévisualiser dans la carte"
                    </button>
                </fieldset>
                <Show when=stale><p role="alert">"Carte modifiée · reprenez ses valeurs ci-dessous."</p></Show>
                <button disabled=move ||client.sending.get() on:click=move |_|if let Some(i)=inspector.get_untracked(){
                    client.change(TransformDraft{scope:i.scope,fields:i.accepted});
                }>"Reprendre les valeurs de la carte"</button>
                <details><summary>"Repères et rotation"</summary><p>"Prévisualisez, puis validez avec Appliquer. Position en mètres. Rotation relative à l’orientation acceptée, axes monde X puis Y puis Z. Les deux fenêtres partagent ce brouillon ; aucune modification de la carte avant Appliquer. Un brouillon périmé reste conservé jusqu’à votre choix de reprendre les valeurs de la carte."</p></details>
            </Show>
            <output aria-live="polite">{move ||if client.sending.get()||!client.pending.with(BTreeMap::is_empty){"Conservation du brouillon en cours…"}else if inspector.get().is_some_and(|i|client.remote.with(|r|r.iter().any(|d|d.draft.scope.key()==i.scope.key()))){"Brouillon de session conservé · non appliqué"}else{""}}</output>
            <Show when=move ||!client.error.with(String::is_empty)>
                <p role="alert">{move ||client.error.get()}</p>
                <button on:click=move |_|{client.error.set(String::new());client.flush();}>"Réessayer la conservation"</button>
                <button disabled=move ||client.sending.get() on:click=move |_|{
                    client.pending.update(|pending|for (key,p) in pending {
                        p.expected=client.remote.with_untracked(|r|r.iter().find(|v|v.draft.scope.key()==*key).map_or(0,|v|v.version));
                    });
                    client.error.set(String::new());client.flush();
                }>"Remplacer le brouillon conservé par ma saisie"</button>
                <button disabled=move ||client.sending.get() on:click=move |_|{
                    client.pending.update(BTreeMap::clear);client.error.set(String::new());client.flush();
                }>"Abandonner mes saisies non conservées"</button>
                <button on:click=move |_|{client.closing.set(false);}>"Rester dans l’atelier"</button>
            </Show>
        </section>
    }
}
