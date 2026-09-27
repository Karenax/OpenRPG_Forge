//! One operation-pivot setting shared by numeric rotation and rotation handles.
use forge_core::Vec3;
use leptos::prelude::*;

#[derive(Clone, Copy)]
pub(super) struct PivotSettings {
    custom: RwSignal<bool>,
    coordinates: [RwSignal<String>; 3],
}
impl PivotSettings {
    pub(super) fn new(position: Vec3) -> Self {
        Self {
            custom: RwSignal::new(false),
            coordinates: [position.x, position.y, position.z].map(|v| RwSignal::new(v.to_string())),
        }
    }
    pub(super) fn resolve(self) -> Result<Option<Vec3>, String> {
        if !self.custom.get_untracked() {
            return Ok(None);
        }
        let [x, y, z] = self.coordinates.map(|s| {
            s.get_untracked()
                .trim()
                .parse::<f32>()
                .map_err(|_| "Coordonnée de pivot invalide")
        });
        let pivot = Vec3::new(x?, y?, z?);
        if !pivot.is_finite() {
            return Err("Pivot non fini refusé".into());
        }
        Ok(Some(pivot))
    }
}

#[component]
pub(super) fn PivotControls(settings: PivotSettings) -> impl IntoView {
    view! {
        <details><summary>"Pivot de rotation (saisie et anneaux)"</summary>
            <label><input type="checkbox" prop:checked=move || settings.custom.get()
                on:change=move |ev| settings.custom.set(event_target_checked(&ev))/>
                "Point monde précis (sinon pivot de l’objet)"</label>
            {settings.coordinates.into_iter().zip(["Pivot monde X (m)", "Pivot monde Y (m)", "Pivot monde Z (m)"]).map(|(value, label)| view! {
                <label>{label}<input type="number" step="0.01" disabled=move || !settings.custom.get()
                    prop:value=move || value.get() on:input=move |ev| value.set(event_target_value(&ev))/></label>
            }).collect_view()}
            <p>"Un point externe déplace aussi l’objet sur un arc. Réactiver les anneaux après un changement. Paramètre d’opération uniquement : ni origine GLB modifiée, ni charnière de gameplay. Translation et échelle conservent leur pivot propre."</p>
        </details>
    }
}
