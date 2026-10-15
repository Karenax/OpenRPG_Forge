//! Exact translation editing; orientation and non-uniform scale are preserved.
use super::{ManualPlacement, PlacementEdit, SendCommand, edit, head};
use crate::authoring_scene::terrain as projected_terrain;
use forge_core::{EntityId, Vec3};
use forge_project::authoring::{AuthoringRequest, AuthoringState};
use leptos::prelude::*;

fn snap_settings(
    grid_enabled: RwSignal<bool>,
    grid_step: RwSignal<String>,
    surface_enabled: RwSignal<bool>,
    surface_offset: RwSignal<String>,
) -> Result<forge_world::placement_snap::PlacementSnap, String> {
    Ok(forge_world::placement_snap::PlacementSnap {
        grid_step_m: grid_enabled
            .get_untracked()
            .then(|| alignment_number(grid_step))
            .transpose()?,
        surface_offset_m: surface_enabled
            .get_untracked()
            .then(|| alignment_number(surface_offset))
            .transpose()?,
    })
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "Basic and advanced views retain the same protected transform draft"
)]
pub(super) fn PlacedObjectControls(
    state: RwSignal<Option<AuthoringState>>,
    placement: ManualPlacement,
    path_array: super::path_array::Draft,
    catalog: RwSignal<Option<forge_assets::asset_browser::AssetBrowser>>,
    selected: RwSignal<String>,
    busy: RwSignal<bool>,
    on_command: SendCommand,
) -> impl IntoView {
    let (id, locked, name) = (placement.id, placement.locked, placement.name);
    let has_effect = placement.particles.is_some();
    let physical_reference = placement.physics.map(|pin| view! {
        <super::super::vault::SavedPhysicsDetails asset_id=placement.asset_id model_sha256=placement.asset_hash pin=*pin/>
    });
    let on_command = crate::authoring_draft_scope::protect(state, on_command);
    let translation = placement.transform.translation_m;
    let coordinates =
        [translation.x, translation.y, translation.z].map(|value| RwSignal::new(value.to_string()));
    let transform = placement.transform;
    let source = format!(
        "Source acceptée : asset {} · SHA-256 {}. Position, rotation et échelle sont une retouche absolue de ce placement {}, conservée lors des régénérations de zone. Verrou du placement entier : {}. Les champs sont un brouillon jusqu’à Appliquer ; ce hash ne prouve pas à lui seul la licence.",
        placement.asset_id,
        placement.asset_hash,
        id,
        if locked { "actif" } else { "inactif" }
    );
    let pivot = super::pivot::PivotSettings::new(transform.translation_m);
    let grid_enabled = RwSignal::new(false);
    let grid_step = RwSignal::new("1".to_owned());
    let surface_enabled = RwSignal::new(false);
    let surface_offset = RwSignal::new("0".to_owned());
    let snap = move || snap_settings(grid_enabled, grid_step, surface_enabled, surface_offset);
    let advanced = RwSignal::new(false);
    let prepare = move |duplicate| -> Result<AuthoringRequest, String> {
        let values = coordinates.map(|signal| signal.get_untracked().trim().parse::<f32>());
        let [x, y, z] = values;
        let mut target = transform;
        target.translation_m = Vec3::new(
            x.map_err(|_| "X invalide")?,
            y.map_err(|_| "Y invalide")?,
            z.map_err(|_| "Z invalide")?,
        );
        target.validate().map_err(|e| e.to_string())?;
        if grid_enabled.get_untracked() || surface_enabled.get_untracked() {
            let current = head(state.get_untracked())?;
            target =
                crate::authoring_raster::align(&projected_terrain(&current)?, snap()?, target)?;
        }
        let action = if duplicate {
            let head = head(state.get_untracked())?;
            let key = serde_json::to_string(&(head.content_hash()?, id, target))
                .map_err(|e| e.to_string())?;
            PlacementEdit::Duplicate {
                source_id: id,
                id: EntityId::deterministic(format!("manual-placement-copy:{key}")),
                transform: target,
            }
        } else {
            PlacementEdit::Transform {
                id,
                transform: target,
            }
        };
        edit(state.get_untracked(), action)
    };
    view! {
        <section on:input=move |_| crate::authoring_shortcuts::cancel_preview()
            on:change=move |_| crate::authoring_shortcuts::cancel_preview()>
            <p>{format!("{} · ({:.2}, {:.2}, {:.2}) m · {}", name,
                translation.x, translation.y, translation.z, if locked { "verrouillé" } else { "modifiable" })}</p>
            <button on:click=move |_| super::super::placements::focus(id)>"Cadrer cet objet dans la carte"</button>
            {physical_reference}
            <Show when=move ||has_effect>
                <super::super::source_graph_particles::SourceParticleControls
                    target=super::super::source_graph_particles::ControlTarget::Placement(id)/>
            </Show>
            <div hidden=move || advanced.get()>
                <super::gizmo::GizmoControls state=state id=id locked=locked pivot=pivot alignment=Callback::new(move |()| snap()) on_command=on_command/>
                <button disabled=locked on:click=move |_| {
                    if let Err(error) = snap().and_then(|snap| super::drag::arm(state, id, snap, on_command)) {
                        on_command.run(Err(error));
                    }
                }>"Déplacer sur le terrain"</button>
                <p>"Glisser une poignée, puis Appliquer. Échap annule le geste."</p>
                <div class="gold-subtools">
                    <button on:click=move |_| on_command.run(prepare(true))>"Dupliquer"</button>
                    <button disabled=locked on:click=move |_| on_command.run(edit(state.get_untracked(), PlacementEdit::Remove { id }))>"Supprimer…"</button>
                </div>
                <p class="authoring-hint">"Copie ou suppression : un aperçu est présenté avant application."</p>
                <button on:click=move |_| advanced.set(true)>"Réglages avancés →"</button>
            </div>
            <div class="gold-advanced-view" hidden=move || !advanced.get()>
            <button on:click=move |_| advanced.set(false)>"← Retour à l’objet"</button>
            <details><summary>"Position précise et duplication"</summary>
                <p>"Coordonnées absolues en mètres. Rotation et échelle conservées. Une copie reçoit un nouvel identifiant et n’est pas verrouillée ; l’original reste intact."</p>
                {coordinates.into_iter().zip(["X (m)", "Y (m)", "Z (m)"]).map(|(value, label)| view! {
                    <label>{label}<input type="number" step="0.01" prop:value=move || value.get()
                        on:input=move |ev| value.set(event_target_value(&ev))/></label>
                }).collect_view()}
                <label><input type="checkbox" prop:checked=move || grid_enabled.get()
                    on:change=move |ev| grid_enabled.set(event_target_checked(&ev))/>
                    "Aligner X/Z sur la grille mondiale"</label>
                <label>"Pas de grille (m)"<input type="number" min="0.01" max="128" step="0.01"
                    disabled=move || !grid_enabled.get() prop:value=move || grid_step.get()
                    on:input=move |ev| grid_step.set(event_target_value(&ev))/></label>
                <label><input type="checkbox" prop:checked=move || surface_enabled.get()
                    on:change=move |ev| surface_enabled.set(event_target_checked(&ev))/>
                    "Poser le pivot sur le terrain"</label>
                <label>"Décalage depuis la surface (m)"<input type="number" step="0.01"
                    disabled=move || !surface_enabled.get() prop:value=move || surface_offset.get()
                    on:input=move |ev| surface_offset.set(event_target_value(&ev))/></label>
                <p>"Grille horizontale puis altitude du terrain. Alignement ponctuel : la position appliquée reste absolue après régénération. Aucun alignement automatique sur les autres objets."</p>
                <button disabled=locked on:click=move |_| on_command.run(prepare(false))>"Prévisualiser le déplacement"</button>
                <button disabled=locked on:click=move |_| {
                    if let Err(error) = snap().and_then(|snap| super::drag::arm(state, id, snap, on_command)) {
                        on_command.run(Err(error));
                    }
                }>"Déplacer cet objet par glissement sur le terrain"</button>
                <p>"Glissement horizontal relatif : le pivot ne saute pas sous le pointeur. L’objet est prévisualisé au relâchement, puis Appliquer. Les options grille/surface ci-dessus s’appliquent ; sinon altitude conservée."</p>
                <button on:click=move |_| on_command.run(prepare(true))>"Prévisualiser une copie à ces coordonnées"</button>
                <p style="overflow-wrap:anywhere">{format!("Objet {id} · asset {} · contenu {}", placement.asset_id, placement.asset_hash)}</p>
            </details>
            <super::pivot::PivotControls settings=pivot/>
            <super::replacement::ReplacementControls state=state id=id locked=locked catalog=catalog selected=selected busy=busy on_command=on_command/>
            <super::path_array::PathArrayControls state=state source_id=id draft=path_array on_command=on_command/>
            <super::orientation::OrientationControls state=state id=id transform=transform locked=locked pivot=pivot on_command=on_command/>
            <super::local_translation::LocalTranslationControls state=state id=id locked=locked alignment=Callback::new(move |()| snap()) on_command=on_command/>
            <details><summary>"Provenance et conservation"</summary><p style="overflow-wrap:anywhere">{source}</p></details>
            <button on:click=move |_| on_command.run(edit(state.get_untracked(), PlacementEdit::SetLocked { id, locked: !locked }))>{if locked { "Prévisualiser le déverrouillage" } else { "Prévisualiser le verrouillage" }}</button>
            <button disabled=locked on:click=move |_| on_command.run(edit(state.get_untracked(), PlacementEdit::Remove { id }))>"Prévisualiser la suppression"</button>
            </div>
        </section>
    }
}

fn alignment_number(signal: RwSignal<String>) -> Result<f32, String> {
    signal
        .get_untracked()
        .trim()
        .parse::<f32>()
        .map_err(|_| "Paramètre d’alignement invalide".to_owned())
}
