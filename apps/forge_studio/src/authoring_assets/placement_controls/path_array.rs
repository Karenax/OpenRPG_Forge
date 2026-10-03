use super::{PlacementEdit, SendCommand, head};
use forge_core::{CommandId, EntityId};
use forge_project::authoring::{AuthoringRequest, AuthoringState};
use forge_world::path_placement::PathPlacementOptions;
use forge_world::placement_snap::PlacementSnap;
use leptos::prelude::*;

type DraftContext = Option<((forge_core::ProjectId, forge_core::AreaId), EntityId)>;

/// Owned by the stable placement panel, not by its revision-rebuilt inspector.
#[derive(Clone, Copy)]
pub(super) struct Draft {
    path: RwSignal<String>,
    values: [RwSignal<String>; 5],
}

impl Draft {
    pub(super) fn bind(
        state: RwSignal<Option<AuthoringState>>,
        selected: RwSignal<Option<EntityId>>,
    ) -> Self {
        let draft = Self {
            path: RwSignal::new(String::new()),
            values: ["2", "0", "0", "0", "0"].map(|v| RwSignal::new(v.to_owned())),
        };
        Effect::new(move |previous: Option<DraftContext>| {
            let context = state
                .with(|s| {
                    s.as_ref()
                        .and_then(|s| s.zone_head.as_ref())
                        .map(|h| (h.base.terrain.project_id, h.base.terrain.area_id))
                })
                .zip(selected.get());
            if previous.is_some_and(|previous| previous != context) {
                draft.path.set(String::new());
                for (field, value) in draft.values.into_iter().zip(["2", "0", "0", "0", "0"]) {
                    field.set(value.to_owned());
                }
            }
            context
        });
        draft
    }
}

#[component]
pub(super) fn PathArrayControls(
    state: RwSignal<Option<AuthoringState>>,
    source_id: EntityId,
    draft: Draft,
    on_command: SendCommand,
) -> impl IntoView {
    let Draft { path, values } = draft;
    let prepare = move || {
        let head = head(state.get_untracked())?;
        let selected = path.get_untracked();
        let path_id = head
            .corrections
            .paths
            .as_deref()
            .unwrap_or_default()
            .iter()
            .find(|p| p.id.to_string() == selected)
            .map(|p| p.id)
            .ok_or("Choisir un tracé accepté")?;
        let mut numbers = [0.0; 5];
        for (index, value) in values.iter().enumerate() {
            numbers[index] = value
                .get_untracked()
                .trim()
                .parse::<f32>()
                .map_err(|_| "Paramètre numérique invalide")?;
        }
        let options = PathPlacementOptions {
            spacing_m: numbers[0],
            start_m: numbers[1],
            lateral_m: numbers[2],
            surface_offset_m: numbers[3],
            yaw_offset_degrees: numbers[4],
        };
        let source = super::super::objects::lookup(&head, source_id)?;
        let transforms =
            forge_world::path_placement::transforms(&head, source.transform, path_id, options)?;
        let terrain = crate::authoring_scene::terrain(&head)?;
        let alignment = crate::authoring_raster::prepare_alignment(
            &terrain,
            PlacementSnap {
                grid_step_m: None,
                surface_offset_m: Some(options.surface_offset_m),
            },
        )?;
        let key = serde_json::to_string(&(head.content_hash()?, source_id, path_id, options))
            .map_err(|e| e.to_string())?;
        let expected_hash = head.content_hash()?;
        let expected_revision = head.revision;
        let mut candidate = head.clone();
        for (index, pose) in transforms.into_iter().enumerate() {
            let transform = alignment.resolve(pose)?;
            let copy_id = EntityId::deterministic(format!("path-placement:{key}:{index}"));
            let request = super::super::objects::edit(
                &candidate,
                PlacementEdit::Duplicate {
                    source_id,
                    id: copy_id,
                    transform,
                },
                CommandId::deterministic(format!("path-array:{key}:{index}")),
            )?;
            let forge_project::zone_authoring::session::ZoneAction::Edit(corrections) =
                request.action
            else {
                return Err("Répétition : commande de copie inattendue".into());
            };
            candidate.corrections = *corrections;
            candidate.version = candidate.corrections.schema_version();
            candidate.validate()?;
        }
        if candidate.corrections == head.corrections {
            return Err("Répétition : aucune copie préparée".into());
        }
        Ok(AuthoringRequest::PreviewZone {
            request: forge_project::zone_authoring::session::ZoneRequest {
                command_id: CommandId::deterministic(format!("path-array:{key}")),
                expected_revision,
                expected_hash,
                action: forge_project::zone_authoring::session::ZoneAction::edit(
                    candidate.corrections,
                ),
            },
        })
    };
    view! {
        <details><summary>"Répéter sur un tracé · clôture ou mur"</summary>
            <p>"Copie cet objet à intervalles réguliers en conservant son asset épinglé, son échelle et son inclinaison. L’axe X suit la direction du tracé ; ajuster l’angle pour le module choisi. Le pivot est posé sur le terrain effectif."</p>
            <label>"Tracé accepté"<select prop:value=move || path.get() on:change=move |ev| path.set(event_target_value(&ev))>
                <option value="">"Choisir…"</option>
                {move || state.get().and_then(|s| s.zone_head).and_then(|h| h.corrections.paths).unwrap_or_default().into_iter().map(|p| view! {
                    <option value=p.id.to_string()>{p.name}</option>
                }).collect_view()}
            </select></label>
            {values.into_iter().zip(["Espacement horizontal (m)", "Départ sur le tracé (m)", "Décalage latéral (m)", "Hauteur du pivot au-dessus du sol (m)", "Correction d’orientation (°)"]).map(|(value,label)| view! {
                <label>{label}<input type="number" step="0.1" prop:value=move || value.get() on:input=move |ev| value.set(event_target_value(&ev))/></label>
            }).collect_view()}
            <button on:click=move |_| on_command.run(prepare())>"Examiner la rangée d’objets"</button>
            <p>"Maximum 256 copies par opération. Examiner puis Appliquer : une seule annulation. L’original reste intact, les copies sont modifiables séparément et restent fixes après régénération. Le pas est à adapter à la longueur du module ; ni raccord automatique des angles ni étirement des panneaux."</p>
        </details>
    }
}
