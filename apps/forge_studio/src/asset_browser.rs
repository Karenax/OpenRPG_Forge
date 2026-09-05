//! Strict Studio IPC contracts and the browser-only presentation for the verified asset catalog.

use forge_assets::browser::AssetBrowserSnapshot;
#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
use forge_assets::browser::{AssetBrowserEntry, AssetCategory};
use serde::{Deserialize, Serialize};

pub const BROWSE_ASSETS_REQUEST_SCHEMA_VERSION: u32 = 1;

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct BrowseAssetsRequest {
    pub schema_version: u32,
    pub request_serial: u64,
    #[serde(default)]
    pub refresh: bool,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum AssetCatalogCacheStatus {
    FirstScan,
    Hit,
    Refreshed,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetCatalog {
    pub request_serial: u64,
    pub cache_status: AssetCatalogCacheStatus,
    pub snapshot: AssetBrowserSnapshot,
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
fn matches_filter(asset: &AssetBrowserEntry, query: &str, category: &str) -> bool {
    if category != "all" && category_key(asset.category) != category {
        return false;
    }
    let query = query.trim().to_lowercase();
    query.is_empty()
        || asset.model_id.to_lowercase().contains(&query)
        || asset.stable_key.to_lowercase().contains(&query)
        || asset.provenance.pack_id.to_lowercase().contains(&query)
        || asset.provenance.author.to_lowercase().contains(&query)
        || asset.provenance.source_path.to_lowercase().contains(&query)
        || asset.scene_path.to_lowercase().contains(&query)
}

#[cfg(any(test, all(target_arch = "wasm32", feature = "web")))]
const fn category_key(category: AssetCategory) -> &'static str {
    match category {
        AssetCategory::Detail => "detail",
        AssetCategory::Fern => "fern",
        AssetCategory::Rock => "rock",
        AssetCategory::Shrub => "shrub",
        AssetCategory::Tree => "tree",
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod web {
    use super::{AssetCatalog, BROWSE_ASSETS_REQUEST_SCHEMA_VERSION, BrowseAssetsRequest};
    use crate::workspace_ui::invoke_studio;
    use crate::{StudioCommand, StudioResponse};
    use forge_assets::browser::AssetCategory;
    use leptos::prelude::*;
    use wasm_bindgen::JsValue;

    #[derive(Clone, Copy)]
    struct AssetBrowserActions {
        catalog: RwSignal<Option<AssetCatalog>>,
        pending: RwSignal<bool>,
        error: RwSignal<Option<String>>,
        latest_request_serial: RwSignal<u64>,
        backend_available: bool,
    }

    impl AssetBrowserActions {
        fn request(self, refresh: bool) {
            if !self.backend_available {
                self.error.set(Some(
                    "Mode navigateur : backend Tauri absent ; aucun asset n’est inventé.".into(),
                ));
                return;
            }
            let request_serial = self.latest_request_serial.get_untracked().saturating_add(1);
            self.latest_request_serial.set(request_serial);
            self.pending.set(true);
            self.error.set(None);
            wasm_bindgen_futures::spawn_local(async move {
                let result = invoke_studio(StudioCommand::BrowseAssets {
                    request: BrowseAssetsRequest {
                        schema_version: BROWSE_ASSETS_REQUEST_SCHEMA_VERSION,
                        request_serial,
                        refresh,
                    },
                })
                .await;
                if self.latest_request_serial.get_untracked() != request_serial {
                    return;
                }
                match result {
                    Ok(StudioResponse::AssetCatalog { catalog })
                        if catalog.request_serial == request_serial =>
                    {
                        self.catalog.set(Some(catalog));
                    }
                    Ok(StudioResponse::AssetCatalog { catalog }) => self.error.set(Some(format!(
                        "Catalogue Assets incohérent : réponse {}, requête {request_serial}.",
                        catalog.request_serial
                    ))),
                    Ok(StudioResponse::Rejected { reason }) | Err(reason) => {
                        self.error.set(Some(reason));
                    }
                    Ok(_) => self.error.set(Some(
                        "Réponse reçue pour une autre opération Studio.".into(),
                    )),
                }
                self.pending.set(false);
            });
        }
    }

    #[component]
    pub(crate) fn AssetBrowserPanel(visible: Signal<bool>) -> impl IntoView {
        let backend_available = tauri_backend_available();
        let actions = AssetBrowserActions {
            catalog: RwSignal::new(None),
            pending: RwSignal::new(false),
            error: RwSignal::new(None),
            latest_request_serial: RwSignal::new(0),
            backend_available,
        };
        let query = RwSignal::new(String::new());
        let category = RwSignal::new("all".to_owned());
        let selected = RwSignal::new(None::<String>);
        let started = RwSignal::new(false);
        Effect::new(move |_| {
            if visible.get() && !started.get_untracked() {
                started.set(true);
                actions.request(false);
            }
        });
        Effect::new(move |_| {
            let selected_key = selected.get();
            let exists = selected_key.as_ref().is_none_or(|key| {
                actions.catalog.get().is_some_and(|catalog| {
                    catalog
                        .snapshot
                        .assets
                        .iter()
                        .any(|asset| &asset.stable_key == key)
                })
            });
            if !exists {
                selected.set(None);
            }
        });

        view! {
            <main class="asset-browser" data-forge-mode="asset-browser" aria-busy=move || actions.pending.get().to_string()>
                <AssetBrowserHeader actions/>
                <AssetBrowserToolbar actions query category/>
                {move || actions.error.get().map(|reason| view! {
                    <div class="asset-browser-error" role="alert" id="asset-browser-error">{reason}</div>
                })}
                <AssetBrowserContent actions query category selected/>
                <AssetBrowserLedger actions query category selected/>
            </main>
        }
    }

    #[component]
    fn AssetBrowserHeader(actions: AssetBrowserActions) -> impl IntoView {
        view! {
            <header class="asset-browser-header">
                <div>
                    <span class="eyebrow">"BIBLIOTHÈQUE VÉRIFIÉE"</span>
                    <h2>"Assets"</h2>
                    <p>"Parcourez le snapshot local épinglé et sa provenance."</p>
                </div>
                <button id="asset-browser-refresh" type="button"
                    disabled=move || actions.pending.get() || !actions.backend_available
                    on:click=move |_| actions.request(true)>"Actualiser"</button>
            </header>
            <Show when=move || !actions.backend_available>
                <section class="asset-browser-notice is-warning" role="status" id="asset-browser-backend-unavailable">
                    <strong>"Backend Assets absent"</strong>
                    <span>"Ouvrez cette fenêtre dans le Studio Tauri. Aucun catalogue fictif ni accès disque navigateur n’est utilisé."</span>
                </section>
            </Show>
        }
    }

    #[component]
    fn AssetBrowserToolbar(
        actions: AssetBrowserActions,
        query: RwSignal<String>,
        category: RwSignal<String>,
    ) -> impl IntoView {
        view! {
            <div class="asset-browser-toolbar">
                <label for="asset-browser-search">"Recherche"
                    <input id="asset-browser-search" type="search" placeholder="Nom, pack, auteur…"
                        prop:value=move || query.get()
                        on:input=move |event| query.set(event_target_value(&event))/>
                </label>
                <label for="asset-browser-category">"Catégorie"
                    <select id="asset-browser-category" prop:value=move || category.get()
                        on:change=move |event| category.set(event_target_value(&event))>
                        <option value="all">"Toutes"</option>
                        <option value="detail">"Détails"</option>
                        <option value="fern">"Fougères"</option>
                        <option value="rock">"Rochers"</option>
                        <option value="shrub">"Arbustes"</option>
                        <option value="tree">"Arbres"</option>
                    </select>
                </label>
                <span class="asset-browser-count">{move || filtered_count(actions.catalog.get().as_ref(), &query.get(), &category.get()).to_string()}" résultat(s)"</span>
            </div>
        }
    }

    #[component]
    fn AssetBrowserContent(
        actions: AssetBrowserActions,
        query: RwSignal<String>,
        category: RwSignal<String>,
        selected: RwSignal<Option<String>>,
    ) -> impl IntoView {
        view! {
            <div class="asset-browser-layout">
                <section class="asset-browser-results" aria-label="Assets disponibles">
                    <ul>{move || actions.catalog.get().map(|catalog| {
                        let query_value = query.get();
                        let category_value = category.get();
                        catalog.snapshot.assets.into_iter()
                            .filter(|asset| super::matches_filter(asset, &query_value, &category_value))
                            .map(|asset| asset_row(asset, selected)).collect_view()
                    })}</ul>
                    <Show when=move || actions.catalog.get().is_some() && filtered_count(actions.catalog.get().as_ref(), &query.get(), &category.get()) == 0>
                        <p class="asset-browser-empty">"Aucun asset ne correspond à ce filtre."</p>
                    </Show>
                </section>
                <AssetBrowserDetails actions selected/>
            </div>
        }
    }

    fn asset_row(
        asset: forge_assets::browser::AssetBrowserEntry,
        selected: RwSignal<Option<String>>,
    ) -> impl IntoView {
        let key = asset.stable_key.clone();
        let button_key = key.clone();
        let selected_key = key.clone();
        view! {
            <li><button type="button" class:is-selected=move || selected.get().as_deref() == Some(selected_key.as_str())
                aria-pressed=move || (selected.get().as_deref() == Some(key.as_str())).to_string()
                on:click=move |_| selected.set(Some(button_key.clone()))>
                <strong>{asset.model_id}</strong><span>{category_label(asset.category)}</span>
                <small>{asset.provenance.pack_id}</small>
            </button></li>
        }
    }

    #[component]
    fn AssetBrowserDetails(
        actions: AssetBrowserActions,
        selected: RwSignal<Option<String>>,
    ) -> impl IntoView {
        let selected_entry = move || {
            let key = selected.get()?;
            actions
                .catalog
                .get()?
                .snapshot
                .assets
                .into_iter()
                .find(|asset| asset.stable_key == key)
        };
        view! {
            <aside class="asset-browser-details" aria-label="Détails de l’asset sélectionné">
                {move || selected_entry().map_or_else(
                    || view! { <p class="asset-browser-empty">"Sélectionnez un asset pour afficher ses détails vérifiés."</p> }.into_any(),
                    |asset| view! {
                        <h3>{asset.model_id}</h3>
                        <dl>
                            <dt>"Catégorie"</dt><dd>{category_label(asset.category)}</dd>
                            <dt>"Identifiant"</dt><dd class="asset-browser-code">{asset.id.to_string()}</dd>
                            <dt>"Échelle de base"</dt><dd>{asset.base_scale.to_string()}</dd>
                            <dt>"Pack"</dt><dd>{asset.provenance.pack_id}</dd>
                            <dt>"Auteur"</dt><dd>{asset.provenance.author}</dd>
                            <dt>"Version source"</dt><dd>{asset.provenance.source_version}</dd>
                            <dt>"Acquisition"</dt><dd>{asset.provenance.retrieved_at}</dd>
                            <dt>"Licence"</dt><dd>{format!("{} · {}", asset.provenance.license_spdx, if asset.provenance.license_verified { "vérifiée" } else { "non vérifiée" })}</dd>
                            <dt>"Source"</dt><dd class="asset-browser-code">{asset.provenance.source_path}</dd>
                            <dt>"Scène"</dt><dd class="asset-browser-code">{asset.scene_path}</dd>
                            <dt>"Hash scène"</dt><dd class="asset-browser-code">{asset.provenance.scene_content_hash.to_string()}</dd>
                            <dt>"Hash manifeste"</dt><dd class="asset-browser-code">{asset.provenance.manifest_hash.to_string()}</dd>
                            <dt>"Hash snapshot"</dt><dd class="asset-browser-code">{actions.catalog.get().map(|catalog| catalog.snapshot.snapshot_hash.to_string()).unwrap_or_default()}</dd>
                        </dl>
                        <button type="button" disabled title="La pose dans une zone arrive avec R2-027">"Placer dans Monde · R2-027"</button>
                        <p class="asset-browser-placement-note">"Cette tranche consulte et sélectionne seulement. Le placement transactionnel dans Monde est prévu pour R2-027."</p>
                    }.into_any(),
                )}
            </aside>
        }
    }

    #[component]
    fn AssetBrowserLedger(
        actions: AssetBrowserActions,
        query: RwSignal<String>,
        category: RwSignal<String>,
        selected: RwSignal<Option<String>>,
    ) -> impl IntoView {
        view! {
            <output id="asset-browser-ledger" hidden>{move || {
                let catalog = actions.catalog.get();
                serde_json::json!({
                    "backend_available": actions.backend_available,
                    "pending": actions.pending.get(),
                    "request_serial": actions.latest_request_serial.get(),
                    "cache_status": catalog.as_ref().map(|catalog| catalog.cache_status),
                    "snapshot_hash": catalog.as_ref().map(|catalog| catalog.snapshot.snapshot_hash.to_string()),
                    "asset_count": catalog.as_ref().map_or(0, |catalog| catalog.snapshot.assets.len()),
                    "filtered_count": filtered_count(catalog.as_ref(), &query.get(), &category.get()),
                    "query": query.get(), "category": category.get(), "selected": selected.get(),
                    "placement": "r2_027",
                }).to_string()
            }}</output>
        }
    }

    fn filtered_count(catalog: Option<&AssetCatalog>, query: &str, category: &str) -> usize {
        catalog.map_or(0, |catalog| {
            catalog
                .snapshot
                .assets
                .iter()
                .filter(|asset| super::matches_filter(asset, query, category))
                .count()
        })
    }

    const fn category_label(category: AssetCategory) -> &'static str {
        match category {
            AssetCategory::Detail => "Détail",
            AssetCategory::Fern => "Fougère",
            AssetCategory::Rock => "Rocher",
            AssetCategory::Shrub => "Arbuste",
            AssetCategory::Tree => "Arbre",
        }
    }

    fn tauri_backend_available() -> bool {
        let Some(window) = web_sys::window() else {
            return false;
        };
        let Ok(tauri) = js_sys::Reflect::get(window.as_ref(), &JsValue::from_str("__TAURI__"))
        else {
            return false;
        };
        if tauri.is_null() || tauri.is_undefined() {
            return false;
        }
        let Ok(core) = js_sys::Reflect::get(&tauri, &JsValue::from_str("core")) else {
            return false;
        };
        js_sys::Reflect::get(&core, &JsValue::from_str("invoke"))
            .is_ok_and(|invoke| invoke.is_function())
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub(crate) use web::AssetBrowserPanel;

#[cfg(test)]
mod tests {
    use super::*;
    use forge_assets::browser::AssetBrowserProvenance;
    use forge_core::{AssetId, ContentHash};

    #[test]
    fn browse_request_is_strict_and_refresh_defaults_to_false() {
        assert_eq!(
            serde_json::from_str::<BrowseAssetsRequest>(
                r#"{"schema_version":1,"request_serial":7}"#
            )
            .unwrap(),
            BrowseAssetsRequest {
                schema_version: BROWSE_ASSETS_REQUEST_SCHEMA_VERSION,
                request_serial: 7,
                refresh: false,
            }
        );
        assert!(
            serde_json::from_str::<BrowseAssetsRequest>(
                r#"{"schema_version":1,"request_serial":7,"refresh":false,"root":"C:/escape"}"#
            )
            .is_err()
        );
        assert!(serde_json::from_str::<BrowseAssetsRequest>(r#"{"request_serial":7}"#).is_err());
    }

    #[test]
    fn filter_covers_name_provenance_paths_category_empty_and_no_result() {
        let asset = AssetBrowserEntry {
            id: AssetId::deterministic("asset-browser-filter"),
            stable_key: "forest-pack/tree/oak-large".into(),
            model_id: "Oak Large".into(),
            category: AssetCategory::Tree,
            scene_path: "models/trees/oak_large.gltf".into(),
            base_scale: 1.0,
            provenance: AssetBrowserProvenance {
                subset_id: "verified-forest".into(),
                pack_id: "forest-pack".into(),
                author: "Quaternius".into(),
                source_version: "1.0".into(),
                retrieved_at: "2026-01-01T00:00:00Z".into(),
                source_path: "assets/source/oak_large.gltf".into(),
                license_spdx: "CC0-1.0".into(),
                license_verified: true,
                attribution_required: false,
                manifest_hash: ContentHash::from_bytes([1; 32]),
                scene_content_hash: ContentHash::from_bytes([2; 32]),
            },
        };
        for query in [
            "",
            "oak large",
            "FOREST-PACK",
            "quaternius",
            "source/oak",
            "models/trees",
        ] {
            assert!(matches_filter(&asset, query, "all"), "query {query}");
        }
        assert!(matches_filter(&asset, "oak", "tree"));
        assert!(!matches_filter(&asset, "oak", "rock"));
        assert!(!matches_filter(&asset, "missing", "all"));
    }

    #[test]
    fn browse_assets_uses_its_own_strict_studio_ipc_envelope() {
        let command = crate::StudioCommand::BrowseAssets {
            request: BrowseAssetsRequest {
                schema_version: BROWSE_ASSETS_REQUEST_SCHEMA_VERSION,
                request_serial: 9,
                refresh: true,
            },
        };
        let value = serde_json::to_value(&command).unwrap();
        assert_eq!(value["command"], "browse_assets");
        assert_eq!(
            serde_json::from_value::<crate::StudioCommand>(value.clone()).unwrap(),
            command
        );
        assert!(
            serde_json::from_value::<crate::workspace_api::WorkspaceCommand>(value.clone())
                .is_err()
        );
        let mut unexpected = value;
        unexpected["request"]["root"] = serde_json::json!("C:/escape");
        assert!(serde_json::from_value::<crate::StudioCommand>(unexpected).is_err());

        let mut snapshot = AssetBrowserSnapshot {
            schema: forge_assets::browser::ASSET_BROWSER_SNAPSHOT_SCHEMA.to_owned(),
            schema_version: forge_assets::browser::ASSET_BROWSER_SNAPSHOT_VERSION,
            snapshot_hash: ContentHash::from_bytes([0; 32]),
            manifests: Vec::new(),
            categories: Vec::new(),
            assets: Vec::new(),
        };
        snapshot.snapshot_hash = snapshot.recompute_hash().unwrap();
        let response = crate::StudioResponse::AssetCatalog {
            catalog: AssetCatalog {
                request_serial: 9,
                cache_status: AssetCatalogCacheStatus::FirstScan,
                snapshot,
            },
        };
        let encoded = serde_json::to_string(&response).unwrap();
        assert_eq!(
            serde_json::from_str::<crate::StudioResponse>(&encoded).unwrap(),
            response
        );
    }
}
