//! Reload-only diagnostic mode using authored `WorldSketch` cameras, never a map mutation.
const MAX_VIEW_INPUT_BYTES: usize = 512 * 1024;

fn candidate_requested(search: &str, scene: crate::StudioScene) -> Result<bool, String> {
    let values: Vec<_> = search
        .trim_start_matches('?')
        .split('&')
        .filter_map(|part| {
            let (key, value) = part.split_once('=').unwrap_or((part, ""));
            (key == "forge_view_input").then_some(value)
        })
        .collect();
    match values.as_slice() {
        [] => Ok(false),
        ["candidate"] if requested(search, scene)?.is_some() => Ok(true),
        _ => Err("forge_view_input exige candidate et une vue WorldSketch fixe unique".into()),
    }
}

fn validate_candidate(bytes: &[u8]) -> Result<forge_world::WorldSketchDocument, String> {
    if bytes.len() > MAX_VIEW_INPUT_BYTES {
        return Err("Vues de preuve limitées à 512 KiB".into());
    }
    let text = std::str::from_utf8(bytes).map_err(|error| error.to_string())?;
    let candidate =
        forge_world::WorldSketchDocument::from_json(text).map_err(|error| error.to_string())?;
    let embedded =
        forge_world::embedded_visual_valley_world_sketch().map_err(|error| error.to_string())?;
    let mut comparison = candidate.clone();
    comparison
        .validation_cameras
        .clone_from(&embedded.validation_cameras);
    if comparison != embedded {
        return Err(
            "Les vues candidates diffèrent de la carte embarquée hors caméras ; chargement refusé"
                .into(),
        );
    }
    Ok(candidate)
}

fn requested(search: &str, scene: crate::StudioScene) -> Result<Option<&str>, String> {
    let parameters: Vec<_> = search
        .trim_start_matches('?')
        .split('&')
        .map(|part| part.split_once('=').unwrap_or((part, "")))
        .collect();
    let values: Vec<_> = parameters
        .iter()
        .filter(|(key, _)| *key == "forge_view")
        .map(|(_, value)| *value)
        .collect();
    match values.as_slice() {
        [] => Ok(None),
        [id] if !id.is_empty()
            && id
                .bytes()
                .all(|b| b.is_ascii_alphanumeric() || b == b'_' || b == b'-')
            && scene == crate::StudioScene::WorldSketchVisualValley
            && !parameters.iter().any(|(key, _)| *key == "forge_probe") =>
        {
            Ok(Some(id))
        }
        _ => Err("forge_view exige une caméra WorldSketch unique, sans forge_probe".into()),
    }
}

#[cfg(all(target_arch = "wasm32", feature = "web"))]
mod web {
    use bevy::ecs as bevy_ecs;
    use bevy::prelude::With;
    use bevy::prelude::*;
    use forge_engine::{FixedProofClock, FixedProofViewPlugin, FixedProofViewStatus};
    use leptos::prelude::*;
    use std::cell::RefCell;
    #[derive(Resource)]
    pub struct ViewInputLabel(String);
    pub struct PreparedView {
        plugin: FixedProofViewPlugin,
        label: ViewInputLabel,
    }
    #[derive(Resource, Default)]
    struct EnvironmentProbe {
        original: Option<(Entity, EnvironmentMapLight)>,
        disabled: bool,
        observation: String,
    }
    thread_local! {
        static OUTPUT: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
        static ENVIRONMENT_REQUEST: RefCell<Option<bool>> = const { RefCell::new(None) };
        static CLOCK_REQUEST: RefCell<bool> = const { RefCell::new(false) };
    }
    pub fn is_requested() -> bool {
        web_sys::window()
            .and_then(|window| window.location().search().ok())
            .is_some_and(|search| {
                super::requested(&search, crate::StudioScene::WorldSketchVisualValley)
                    .is_ok_and(|request| request.is_some())
            })
    }
    #[component]
    pub fn Status() -> impl IntoView {
        let (status, output) =
            signal("Vue de preuve non observée · commandes manuelles désactivées".to_owned());
        OUTPUT.with(|slot| slot.replace(Some(output)));
        view! {
            <output id="studio-fixed-view-status" role="status">{move || status.get()}</output>
            <button type="button" on:click=move |_| { CLOCK_REQUEST.with(|slot| slot.replace(true)); }>
                "Avancer l’animation de 250 ms"
            </button>
            <p class="hint">"Temps de preuve borné à 8 s par rechargement · sans sauvegarde · hors benchmark"</p>
            <p class="hint">"Sonde IBL de scène · vue fixe uniquement · sans sauvegarde"</p>
            <button type="button" on:click=move |_| { ENVIRONMENT_REQUEST.with(|slot| slot.replace(Some(true))); }>
                "Couper uniquement l’IBL de scène"
            </button>
            <button type="button" on:click=move |_| { ENVIRONMENT_REQUEST.with(|slot| slot.replace(Some(false))); }>
                "Restaurer l’IBL de scène"
            </button>
        }
    }
    pub async fn prepare(scene: crate::StudioScene) -> Result<Option<PreparedView>, String> {
        let search = web_sys::window()
            .ok_or("Fenêtre absente")?
            .location()
            .search()
            .map_err(|_| "URL inaccessible")?;
        let candidate = super::candidate_requested(&search, scene)?;
        if let Some(id) = super::requested(&search, scene)? {
            let (document, label) = if candidate {
                use sha2::{Digest, Sha256};
                let bytes = crate::governed_kit::transport::fetch_view_candidate().await?;
                (
                    super::validate_candidate(&bytes)?,
                    format!(
                        "vues candidates SHA-256 {} · carte inchangée",
                        forge_core::ContentHash::from_bytes(Sha256::digest(&bytes).into())
                    ),
                )
            } else {
                (
                    forge_world::embedded_visual_valley_world_sketch()
                        .map_err(|e| e.to_string())?,
                    "vues embarquées".to_owned(),
                )
            };
            return Ok(Some(PreparedView {
                plugin: FixedProofViewPlugin::new(&document, id)
                    .map_err(|error| error.to_string())?,
                label: ViewInputLabel(label),
            }));
        }
        Ok(None)
    }
    pub fn install(app: &mut App, prepared: Option<PreparedView>) {
        if let Some(prepared) = prepared {
            app.insert_resource(prepared.label)
                .init_resource::<EnvironmentProbe>()
                .add_plugins(prepared.plugin)
                .add_systems(
                    Last,
                    (request_clock_step, apply_environment_probe, publish).chain(),
                );
        }
    }
    fn request_clock_step(mut clock: ResMut<FixedProofClock>) {
        if CLOCK_REQUEST.with(|slot| slot.replace(false)) {
            clock.request_step();
        }
    }
    #[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
    fn apply_environment_probe(
        status: Res<FixedProofViewStatus>,
        mut probe: ResMut<EnvironmentProbe>,
        mut cameras: Query<(Entity, &Camera, Option<&mut EnvironmentMapLight>), With<Camera3d>>,
    ) {
        let request = ENVIRONMENT_REQUEST.with(|slot| slot.borrow_mut().take());
        if !status.installed || status.error.is_some() {
            probe.observation = "IBL : vue fixe non installée, aucune mutation".into();
            return;
        }
        let mut active = cameras.iter_mut().filter(|(_, camera, _)| camera.is_active);
        let Some((entity, _, light)) = active.next() else {
            probe.observation = "IBL : caméra absente, aucune mutation".into();
            return;
        };
        if active.next().is_some() {
            probe.observation = "IBL : caméras ambiguës, aucune mutation".into();
            return;
        }
        let Some(mut light) = light else {
            probe.observation = "IBL : environnement absent, aucune mutation".into();
            return;
        };
        if probe.original.is_none() {
            if !light.intensity.is_finite() || light.intensity <= 0.0 {
                probe.observation = "IBL : intensité initiale invalide, aucune mutation".into();
                return;
            }
            probe.original = Some((entity, light.clone()));
        }
        let Some((camera, original)) = &probe.original else {
            return;
        };
        if *camera != entity
            || original.diffuse_map.id() != light.diffuse_map.id()
            || original.specular_map.id() != light.specular_map.id()
        {
            probe.observation = "IBL : caméra/textures remplacées, aucune mutation".into();
            return;
        }
        let initial = original.intensity;
        if let Some(disabled) = request {
            probe.disabled = disabled;
        }
        let desired = if probe.disabled { 0.0 } else { initial };
        // Observe before writing: a request is not evidence of a rendered frame.
        let observed = light.intensity.to_bits() == desired.to_bits();
        probe.observation = format!(
            "IBL scène : observée {:.3}, demandée {desired:.3}, initiale {initial:.3} · {} · CPU seulement, GPU non qualifié",
            light.intensity,
            if observed {
                "concordance CPU"
            } else {
                "application en attente"
            }
        );
        // Only camera IBL intensity; retain maps, direct lights, water F0 and documents.
        if !observed {
            light.intensity = desired;
        }
    }
    #[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
    fn publish(
        status: Res<FixedProofViewStatus>,
        input: Res<ViewInputLabel>,
        probe: Res<EnvironmentProbe>,
        clock: Res<FixedProofClock>,
        time: Res<Time<Virtual>>,
        mut previous: Local<String>,
    ) {
        let text = if status.installed {
            format!(
                "Vue de preuve {} · caméra fixe, temps virtuel figé · sans sauvegarde, hors benchmark · {}",
                status.camera_id, input.0
            )
        } else {
            format!(
                "Vue de preuve refusée : {}",
                status
                    .error
                    .as_ref()
                    .map_or_else(|| "caméra non observée".to_owned(), ToString::to_string)
            )
        };
        let text = format!(
            "{text} · temps {:.3} s · pas {}/32 · {} · {}",
            time.elapsed_secs_f64(),
            clock.steps,
            clock
                .last_error
                .unwrap_or("horloge CPU observée, pas une preuve GPU"),
            probe.observation
        );
        if *previous == text {
            return;
        }
        previous.clone_from(&text);
        OUTPUT.with(|slot| {
            if let Some(output) = *slot.borrow() {
                output.set(text);
            }
        });
    }
}
#[cfg(all(target_arch = "wasm32", feature = "web"))]
pub use web::*;

#[cfg(test)]
mod tests {
    use super::*;
    use crate::StudioScene::*;
    #[test]
    fn candidate_views_are_explicit_bounded_and_camera_only() {
        assert!(!candidate_requested("", Gate0).unwrap());
        assert!(
            candidate_requested(
                "?forge_view=water_detail&forge_view_input=candidate",
                WorldSketchVisualValley
            )
            .unwrap()
        );
        for query in [
            "?forge_view_input=candidate",
            "?forge_view=x&forge_view_input=https://example.org",
            "?forge_view=x&forge_view_input=candidate&forge_view_input=candidate",
        ] {
            assert!(candidate_requested(query, WorldSketchVisualValley).is_err());
        }
        let original = forge_world::embedded_visual_valley_world_sketch().unwrap();
        let mut candidate = original.clone();
        candidate.validation_cameras[0].position_m[0] += 1.0;
        let bytes = serde_json::to_vec(&candidate).unwrap();
        assert_eq!(validate_candidate(&bytes).unwrap(), candidate);
        candidate.terrain_seed += 1;
        assert!(validate_candidate(&serde_json::to_vec(&candidate).unwrap()).is_err());
        for invalid in [
            b"{}".to_vec(),
            vec![0xff],
            vec![b' '; MAX_VIEW_INPUT_BYTES + 1],
        ] {
            assert!(validate_candidate(&invalid).is_err());
        }
        assert_eq!(
            forge_world::embedded_visual_valley_world_sketch().unwrap(),
            original
        );
    }
    #[test]
    fn proof_view_is_explicit_unique_and_incompatible_with_movement_probe() {
        assert_eq!(requested("", Gate0).unwrap(), None);
        assert_eq!(
            requested("?forge_view=human_bridge", WorldSketchVisualValley).unwrap(),
            Some("human_bridge")
        );
        for search in [
            "?forge_view",
            "?forge_view=",
            "?forge_view=x&forge_view=y",
            "?forge_view=x&forge_probe=bridge",
            "?forge_view=%2e%2e",
        ] {
            assert!(requested(search, WorldSketchVisualValley).is_err());
        }
        for scene in [Gate0, VisualValley] {
            assert!(requested("?forge_view=human_bridge", scene).is_err());
        }
    }
}
