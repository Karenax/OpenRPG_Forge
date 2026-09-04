//! Compact vegetation authoring controls backed only by the active canonical workspace area.

use super::{BrushActions, WorldActions};
use crate::{
    area_preview::AreaPreviewRequest, workspace_api::WorkspaceCommand,
    workspace_ui::WorkspaceActions,
};
use forge_project::{
    MAX_VEGETATION_LAYERS, VegetationDocument, VegetationExclusion, VegetationKind,
    VegetationSpeciesPreset,
};
use leptos::prelude::*;
use std::collections::BTreeMap;

fn active_vegetation(world: WorldActions) -> Option<(AreaPreviewRequest, VegetationDocument)> {
    let key = world.tabs.get().active?;
    let document = world.document.get()?;
    let vegetation = document
        .area(key.module_id, key.area_id)?
        .vegetation
        .clone();
    Some((
        AreaPreviewRequest {
            key,
            base_revision: document.revision.get(),
        },
        vegetation,
    ))
}

fn active_request_untracked(world: WorldActions) -> Option<AreaPreviewRequest> {
    let key = world.tabs.get_untracked().active?;
    let document = world.document.get_untracked()?;
    document.area(key.module_id, key.area_id)?;
    Some(AreaPreviewRequest {
        key,
        base_revision: document.revision.get(),
    })
}

fn parse_preset(value: &str) -> Option<VegetationSpeciesPreset> {
    match value {
        "tree" => Some(VegetationSpeciesPreset::Tree),
        "shrub" => Some(VegetationSpeciesPreset::Shrub),
        "fern" => Some(VegetationSpeciesPreset::Fern),
        "grass" => Some(VegetationSpeciesPreset::Grass),
        _ => None,
    }
}

const fn kind_label(kind: VegetationKind) -> &'static str {
    match kind {
        VegetationKind::Tree => "Arbre",
        VegetationKind::Shrub => "Arbuste",
        VegetationKind::Fern => "Fougère",
        VegetationKind::Grass => "Herbe",
    }
}

fn exclusion_description(exclusion: &VegetationExclusion) -> String {
    match exclusion {
        VegetationExclusion::Circle {
            center_x_m,
            center_z_m,
            radius_m,
            ..
        } => format!("Cercle · X {center_x_m:.1} · Z {center_z_m:.1} · rayon {radius_m:.1} m"),
        VegetationExclusion::CorridorSegment {
            start_x_m,
            start_z_m,
            end_x_m,
            end_z_m,
            half_width_m,
            ..
        } => format!(
            "Passage · ({start_x_m:.1}, {start_z_m:.1}) → ({end_x_m:.1}, {end_z_m:.1}) · demi-largeur {half_width_m:.1} m"
        ),
    }
}

#[component]
fn AddSpeciesForm(
    brush: BrushActions,
    world: WorldActions,
    workspace: WorkspaceActions,
    preset: RwSignal<String>,
    new_name: RwSignal<String>,
    grass_exists: Memo<bool>,
    species_count: Memo<usize>,
) -> impl IntoView {
    view! {
        <form class="vegetation-add-form" on:submit=move |event| {
            event.prevent_default();
            let Some(request) = active_request_untracked(world) else { return };
            let Some(selected_preset) = parse_preset(&preset.get_untracked()) else { return };
            if selected_preset == VegetationSpeciesPreset::Grass && grass_exists.get_untracked() {
                return;
            }
            workspace.send(WorkspaceCommand::AddVegetationSpecies {
                request,
                preset: selected_preset,
                display_name: new_name.get_untracked(),
            });
        }>
            <fieldset disabled=move || workspace.unavailable() || active_vegetation(world).is_none() || brush.stroke.get().is_some()>
                <legend>"Ajouter une espèce"</legend>
                <label>"Preset"
                    <select aria-label="Preset de végétation" prop:value=move || preset.get()
                        on:change=move |event| preset.set(event_target_value(&event))>
                        <option value="tree">"Arbre"</option>
                        <option value="shrub">"Arbuste"</option>
                        <option value="fern">"Fougère"</option>
                        <option value="grass" disabled=move || grass_exists.get()>"Herbe — déjà présente"</option>
                    </select>
                </label>
                <label>"Nom"
                    <input aria-label="Nom de la nouvelle espèce" maxlength="128"
                        prop:value=move || new_name.get()
                        on:input=move |event| new_name.set(event_target_value(&event))/>
                </label>
                <button type="submit" disabled=move || {
                    new_name.get().trim().is_empty()
                        || species_count.get() >= MAX_VEGETATION_LAYERS
                        || (preset.get() == "grass" && grass_exists.get())
                }>
                    "Ajouter"
                </button>
                <p class="hint">{move || if grass_exists.get() {
                    "Herbe indisponible : la couche d’herbe existante utilise déjà le budget de brins."
                } else {
                    "Herbe disponible : une seule couche est admise par le budget actuel."
                }}</p>
            </fieldset>
        </form>
    }
}

#[component]
fn SpeciesCollection(
    brush: BrushActions,
    world: WorldActions,
    workspace: WorkspaceActions,
    rename_drafts: RwSignal<BTreeMap<String, String>>,
    species_count: Memo<usize>,
) -> impl IntoView {
    view! {
        <section class="vegetation-collection" aria-labelledby="vegetation-species-heading">
            <h3 id="vegetation-species-heading">{move || format!("Espèces · {}/{}", species_count.get(), MAX_VEGETATION_LAYERS)}</h3>
            <fieldset disabled=move || workspace.unavailable() || active_vegetation(world).is_none() || brush.stroke.get().is_some()>
                <legend class="project-sr-only">"Espèces de la zone active"</legend>
                <ul class="vegetation-list">
                    {move || active_vegetation(world).map(|(_, vegetation)| {
                        let can_remove = vegetation.layers.len() > 1;
                        vegetation.layers.into_iter().map(|layer| {
                            let key = layer.id.to_string();
                            let input_key = key.clone();
                            let rename_key = key.clone();
                            let rename_id = layer.id.clone();
                            let paint_id = layer.id.clone();
                            let remove_id = layer.id.clone();
                            view! {
                                <li class="vegetation-row">
                                    <span class="vegetation-row-title">{kind_label(layer.kind)}</span>
                                    <input aria-label=format!("Renommer {}", layer.display_name)
                                        prop:value=move || rename_drafts.get().get(&input_key).cloned().unwrap_or_default()
                                        on:input=move |event| {
                                            let value = event_target_value(&event);
                                            rename_drafts.update(|drafts| { drafts.insert(key.clone(), value); });
                                        }/>
                                    <div class="vegetation-row-actions">
                                        <button type="button" on:click=move |_| {
                                            let Some(request) = active_request_untracked(world) else { return };
                                            let Some(display_name) = rename_drafts.get_untracked().get(&rename_key).cloned() else { return };
                                            workspace.send(WorkspaceCommand::RenameVegetationSpecies {
                                                request,
                                                layer_id: rename_id.clone(),
                                                display_name,
                                            });
                                        }>"Renommer"</button>
                                        <button type="button" on:click=move |_| {
                                            brush.vegetation_layer.set(paint_id.to_string());
                                            brush.mode.set("paint_vegetation_density".to_owned());
                                            brush.error.set(None);
                                        }>"Peindre"</button>
                                        <button type="button" disabled=!can_remove on:click=move |_| {
                                            let Some(request) = active_request_untracked(world) else { return };
                                            workspace.send(WorkspaceCommand::RemoveVegetationSpecies {
                                                request,
                                                layer_id: remove_id.clone(),
                                            });
                                        }>"Supprimer"</button>
                                    </div>
                                </li>
                            }
                        }).collect_view()
                    })}
                </ul>
            </fieldset>
        </section>
    }
}

#[component]
fn ExclusionCollection(
    brush: BrushActions,
    world: WorldActions,
    workspace: WorkspaceActions,
) -> impl IntoView {
    view! {
        <section class="vegetation-collection" aria-labelledby="vegetation-exclusions-heading">
            <h3 id="vegetation-exclusions-heading">"Exclusions"</h3>
            <p class="hint">"Ajoutez un cercle ou un passage avec les outils de zone ci-dessus."</p>
            <fieldset disabled=move || workspace.unavailable() || active_vegetation(world).is_none() || brush.stroke.get().is_some()>
                <legend class="project-sr-only">"Exclusions de la zone active"</legend>
                <ul class="vegetation-list">
                    {move || active_vegetation(world).map(|(_, vegetation)| {
                        if vegetation.exclusions.is_empty() {
                            return view! { <li class="vegetation-empty">"Aucune exclusion"</li> }.into_any();
                        }
                        vegetation.exclusions.into_iter().map(|exclusion| {
                            let id = exclusion.id().clone();
                            view! {
                                <li class="vegetation-row">
                                    <span class="vegetation-exclusion-description">{exclusion_description(&exclusion)}</span>
                                    <button type="button" on:click=move |_| {
                                        let Some(request) = active_request_untracked(world) else { return };
                                        workspace.send(WorkspaceCommand::RemoveVegetationExclusion {
                                            request,
                                            exclusion_id: id.clone(),
                                        });
                                    }>"Supprimer"</button>
                                </li>
                            }
                        }).collect_view().into_any()
                    })}
                </ul>
            </fieldset>
        </section>
    }
}

#[component]
pub(crate) fn VegetationPanel(
    brush: BrushActions,
    world: WorldActions,
    workspace: WorkspaceActions,
) -> impl IntoView {
    let preset = RwSignal::new("tree".to_owned());
    let new_name = RwSignal::new("Nouvelle espèce".to_owned());
    let rename_drafts = RwSignal::new(BTreeMap::<String, String>::new());
    let grass_exists = Memo::new(move |_| {
        active_vegetation(world).is_some_and(|(_, vegetation)| {
            vegetation
                .layers
                .iter()
                .any(|layer| layer.kind == VegetationKind::Grass)
        })
    });
    let species_count = Memo::new(move |_| {
        active_vegetation(world).map_or(0, |(_, vegetation)| vegetation.layers.len())
    });

    Effect::new(move |_| {
        let drafts = active_vegetation(world)
            .map(|(_, vegetation)| {
                vegetation
                    .layers
                    .into_iter()
                    .map(|layer| (layer.id.to_string(), layer.display_name))
                    .collect()
            })
            .unwrap_or_default();
        rename_drafts.set(drafts);
    });
    Effect::new(move |_| {
        if grass_exists.get() && preset.get() == "grass" {
            preset.set("tree".to_owned());
        }
    });

    view! {
        <section class="vegetation-panel" aria-labelledby="vegetation-panel-heading">
            <h2 id="vegetation-panel-heading">"Végétation"</h2>
            <p class="hint">"Recettes procédurales et zones d’exclusion de la zone active."</p>
            <AddSpeciesForm brush=brush world=world workspace=workspace preset=preset new_name=new_name
                grass_exists=grass_exists species_count=species_count/>
            <SpeciesCollection brush=brush world=world workspace=workspace rename_drafts=rename_drafts
                species_count=species_count/>
            <ExclusionCollection brush=brush world=world workspace=workspace/>
        </section>
    }
}
