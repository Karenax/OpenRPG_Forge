//! Search the complete authoring index; only presentation is paged.
use leptos::prelude::*;

#[derive(Clone, PartialEq, Eq)]
pub(super) struct Target {
    pub id: String,
    pub label: String,
}

#[component]
pub(super) fn TargetSelect(
    label: &'static str,
    selected: RwSignal<String>,
    targets: Signal<Vec<Target>>,
) -> impl IntoView {
    let query = RwSignal::new(String::new());
    let page = RwSignal::new(0_usize);
    let filtered = Memo::new(move |_| {
        let query = crate::authoring_assets::search_text(&query.get());
        targets
            .get()
            .into_iter()
            .filter(|target| {
                let text = crate::authoring_assets::search_text(&format!(
                    "{} {}",
                    target.label, target.id
                ));
                query.split_whitespace().all(|word| text.contains(word))
            })
            .collect::<Vec<_>>()
    });
    let range = Memo::new(move |_| {
        crate::authoring_index_page::page_range(filtered.with(Vec::len), page.get())
    });
    view! {
        <fieldset class="assistant-target-picker">
            <legend>{label}</legend>
            <label>"Rechercher une cible"<input type="search" prop:value=move ||query.get()
                on:input=move |event|{query.set(event_target_value(&event));page.set(0);}/></label>
            <label>{label}<select prop:value=move ||selected.get() on:change=move |event|selected.set(event_target_value(&event))>
                <option value="" prop:selected=move ||selected.get().is_empty()>"Aucune cible autorisée"</option>
                {move || {
                    let all = filtered.get();
                    let mut entries = all[range.get()].to_vec();
                    let current = selected.get();
                    if !current.is_empty() && !entries.iter().any(|target|target.id==current)
                        && let Some(target) = targets.get().into_iter().find(|target|target.id==current) {
                        entries.insert(0,Target { label: format!("Sélection conservée : {}",target.label), ..target });
                    }
                    entries.into_iter().map(|target| {
                        let id = target.id;
                        let value = id.clone();
                        let label = target.label;
                        view!{<option value=value prop:selected=move ||selected.get()==id>{label}</option>}
                    }).collect_view()
                }}
            </select></label>
            <p role="status">{move || {
                let range = range.get();
                format!("{}–{} sur {} résultat(s) · {} cibles au total", if range.is_empty(){0}else{range.start+1},range.end,filtered.with(Vec::len),targets.with(Vec::len))
            }}</p>
            <button type="button" disabled=move ||range.get().start==0 on:click=move |_|page.set(page.get_untracked().saturating_sub(1))>"Cibles précédentes"</button>
            <button type="button" disabled=move || {range.get().end>=filtered.with(Vec::len)} on:click=move |_|page.update(|page|*page+=1)>"Cibles suivantes"</button>
        </fieldset>
    }
}
