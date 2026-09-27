use bevy::prelude::With;
use bevy::{
    ecs as bevy_ecs,
    prelude::*,
    render::view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
    window::PrimaryWindow,
};
use leptos::prelude::*;
use std::cell::{Cell, RefCell};
use wasm_bindgen::JsCast;

#[derive(Clone, Default)]
struct CaptureUi {
    busy: bool,
    text: String,
}

thread_local! {
    static REQUEST: Cell<bool> = const { Cell::new(false) };
    static OUTPUT: RefCell<Option<WriteSignal<CaptureUi>>> = const { RefCell::new(None) };
}

#[derive(Default, Resource)]
struct PendingCapture(Option<(Entity, f64)>);

fn publish(busy: bool, text: String) {
    OUTPUT.with(|slot| {
        if let Some(output) = *slot.borrow() {
            output.set(CaptureUi { busy, text });
        }
    });
}

#[component]
pub fn ProofExportControls() -> impl IntoView {
    let (capture, set_capture) = signal(CaptureUi::default());
    let (report, set_report) = signal(String::new());
    OUTPUT.with(|slot| slot.replace(Some(set_capture)));
    view! {
        <div class="weather-preview" aria-label="Preuves locales sans sauvegarde du projet">
            <p class="hint">"Qualification locale · ne sauvegarde pas le projet"</p>
            <button id="studio-export-diagnostics" type="button" on:click=move |_| {
                set_report.set(match export_diagnostics() {
                    Ok(()) => "Téléchargement JSON demandé · fichier local non vérifié".to_owned(),
                    Err(error) => format!("Export refusé : {error}"),
                });
            }>"Exporter le diagnostic JSON"</button>
            <output id="studio-export-diagnostics-status">{move || report.get()}</output>
            <button id="studio-capture-viewport" type="button" disabled=move || capture.get().busy on:click=move |_| {
                if !capture.get_untracked().busy {
                    publish(true, "Capture GPU demandée…".to_owned());
                    REQUEST.with(|request| request.set(true));
                }
            }>"Capturer le viewport PNG"</button>
            <output id="studio-capture-viewport-status">{move || capture.get().text}</output>
            <p class="hint">"Viewport seul, jusqu'à 3840 × 2160. JSON et PNG non atomiques ; aucune validation de gate."</p>
        </div>
    }
}

fn export_diagnostics() -> Result<(), String> {
    let document = web_sys::window()
        .and_then(|window| window.document())
        .ok_or("Document navigateur absent")?;
    let text = |id: &str| {
        document
            .get_element_by_id(id)
            .and_then(|element| element.text_content())
    };
    let ledgers = [
        ("scene", text("forge-scene-telemetry-ledger")),
        ("render_warmup", text("studio-render-warmup-ledger")),
        ("profile_warmup", text("studio-profile-warmup-ledger")),
        ("quality", text("studio-quality-ledger")),
        ("quality_motion", text("studio-quality-motion-proof")),
        (super::VOLUMETRIC_LEDGER.0, text(super::VOLUMETRIC_LEDGER.1)),
        ("vegetation", text("studio-vegetation-ledger")),
        ("gpu_memory", text("studio-gpu-memory-ledger")),
        ("bridge_projection", text("studio-bridge-projection-ledger")),
        ("weather", text("studio-weather-preview-ledger")),
        ("cloud_preview", text("studio-cloud-preview-ledger")),
        ("water_optics", text("studio-water-optics-ledger")),
        ("frame_telemetry", text("gate0-telemetry-ledger")),
    ];
    let statuses = [
        ("fixed_view", text("studio-fixed-view-status")),
        ("cloud_preview", text("studio-cloud-preview-status")),
        ("water_optics", text("studio-water-optics-status")),
        ("water_reflections", text("studio-water-reflection-status")),
        ("water_tint", text("studio-water-tint-status")),
        ("kit", text("studio-kit-status")),
        ("bridge_probe", text("studio-bridge-probe-status")),
        ("capture", text("studio-capture-viewport-status")),
    ];
    let timestamp = String::from(js_sys::Date::new_0().to_iso_string());
    let bytes = super::diagnostic_report(&timestamp, &ledgers, &statuses)?;
    let parts = js_sys::Array::of1(&js_sys::Uint8Array::from(bytes.as_slice()).into());
    let blob = web_sys::Blob::new_with_u8_array_sequence(&parts).map_err(js_error)?;
    let url = web_sys::Url::create_object_url_with_blob(&blob).map_err(js_error)?;
    let result = (|| {
        let link = document.create_element("a").map_err(js_error)?;
        link.set_attribute("href", &url).map_err(js_error)?;
        link.set_attribute(
            "download",
            &format!("forge-diagnostic-{}.json", js_sys::Date::now()),
        )
        .map_err(js_error)?;
        link.dyn_into::<web_sys::HtmlElement>()
            .map_err(|_| "Lien de téléchargement invalide")?
            .click();
        Ok(())
    })();
    web_sys::Url::revoke_object_url(&url).map_err(js_error)?;
    result
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Result::map_err supplies an owned JS error"
)]
fn js_error(error: wasm_bindgen::JsValue) -> String {
    format!("{error:?}")
}

pub fn install(app: &mut App) {
    app.init_resource::<PendingCapture>()
        .add_systems(Last, request_capture);
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn request_capture(
    mut commands: Commands,
    time: Res<Time<Real>>,
    windows: Query<&Window, With<PrimaryWindow>>,
    mut pending: ResMut<PendingCapture>,
) {
    if let Some((entity, started)) = pending.0 {
        if time.elapsed_secs_f64() - started > 15.0 {
            commands.entity(entity).try_despawn();
            pending.0 = None;
            publish(
                false,
                "Capture expirée après 15 s · aucun fichier confirmé".to_owned(),
            );
        }
        return;
    }
    if !REQUEST.with(|request| request.replace(false)) {
        return;
    }
    let Ok(window) = windows.single() else {
        publish(
            false,
            "Capture refusée : fenêtre primaire absente ou ambiguë".to_owned(),
        );
        return;
    };
    if !super::supported_capture_size(
        window.resolution.physical_width(),
        window.resolution.physical_height(),
    ) {
        publish(
            false,
            "Capture refusée : dimensions nulles ou supérieures à 3840 × 2160".to_owned(),
        );
        return;
    }
    let name = format!("forge-viewport-{}.png", js_sys::Date::now());
    let mut save = save_to_disk(name.clone());
    let entity = commands.spawn(Screenshot::primary_window()).observe(move |event: On<ScreenshotCaptured>, mut pending: ResMut<PendingCapture>| {
        if pending.0.is_none_or(|(entity, _)| entity != event.entity) { return; }
        let width = event.image.width();
        let height = event.image.height();
        save(event);
        pending.0 = None;
        publish(false, format!("Capture GPU reçue {width} × {height} · téléchargement {name} demandé · fichier local non vérifié"));
    }).id();
    pending.0 = Some((entity, time.elapsed_secs_f64()));
}
