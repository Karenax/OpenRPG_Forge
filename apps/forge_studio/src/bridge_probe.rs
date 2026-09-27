//! Explicit automated qualification; never a substitute for interactive acceptance.
use bevy::prelude::*;
use forge_engine::BridgeCrossingProbeStatus;
use leptos::prelude::*;
use std::cell::RefCell;

thread_local! {
    static OUTPUT: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
}

#[component]
pub fn ProbeStatus() -> impl IntoView {
    let (status, output) = signal("Parcours automatique non demandé".to_owned());
    OUTPUT.with(|slot| slot.replace(Some(output)));
    view! {
        <output id="studio-bridge-probe-status" role="status">{move || status.get()}</output>
        <pre id="studio-quality-motion-proof" hidden></pre>
        <output id="studio-quality-motion-status"></output>
        <pre id="studio-temporal-lod-proof" hidden></pre>
    }
}

pub fn requested() -> Result<bool, String> {
    let search = web_sys::window()
        .ok_or("Fenêtre absente")?
        .location()
        .search()
        .map_err(|_| "URL inaccessible")?;
    let values: Vec<_> = search
        .trim_start_matches('?')
        .split('&')
        .filter_map(|part| part.strip_prefix("forge_probe="))
        .collect();
    match values.as_slice() {
        [] => Ok(false),
        ["bridge"] => Ok(true),
        _ => Err("Paramètre forge_probe invalide ou répété".to_owned()),
    }
}

pub fn temporal_requested() -> Result<bool, String> {
    let search = web_sys::window()
        .ok_or("Fenêtre absente")?
        .location()
        .search()
        .map_err(|_| "URL inaccessible")?;
    let values = search
        .trim_start_matches('?')
        .split('&')
        .filter_map(|part| part.strip_prefix("forge_temporal_lod="))
        .collect::<Vec<_>>();
    match values.as_slice() {
        [] => Ok(false),
        ["1"] if requested()? => Ok(true),
        _ => Err("forge_temporal_lod=1 exige un parcours bridge unique".into()),
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
pub fn publish(
    status: Res<BridgeCrossingProbeStatus>,
    temporal: Option<Res<forge_engine::BridgeTemporalLodStatus>>,
) {
    if let Some(temporal) = temporal.filter(DetectChanges::is_changed)
        && let Some(node) = web_sys::window()
            .and_then(|window| window.document())
            .and_then(|document| document.get_element_by_id("studio-temporal-lod-proof"))
    {
        node.set_text_content(Some(&serde_json::json!(&*temporal).to_string()));
    }
    if !status.is_changed() {
        return;
    }
    let text = format!(
        "Parcours automatique — {} | sol {}/{} ticks | contacts {} | {:.3} m{}",
        status.phase,
        status.grounded_ticks,
        status.crossing_ticks,
        status.deck_contacts,
        status.progress_m,
        status
            .failure_reason
            .as_ref()
            .map_or(String::new(), |reason| format!(" | {reason}"))
    );
    OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(text);
        }
    });
}
