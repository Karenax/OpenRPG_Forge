use super::{AreaPreviewRequest, WorkspaceActions, WorldActions};
use crate::{brush_state::BrushPath, workspace_api::WorkspaceCommand};
use forge_engine::editor_viewport::EditorBrushPointer;
use forge_project::{TerrainMaterialRole, VegetationLayerId};
use forge_world::{
    TerrainBrush, TerrainBrushOperation, TerrainMaterialBrush, VegetationDensityBrush,
};
use leptos::prelude::*;
use wasm_bindgen::{JsCast, closure::Closure};

#[derive(Clone)]
struct StrokeDraft {
    request: AreaPreviewRequest,
    generation: u64,
    pointer_id: i32,
    brush: ActiveBrush,
    path: BrushPath,
    last_serial: u64,
    sampled_serial: u64,
    finishing: bool,
}

#[derive(Clone)]
enum ActiveBrush {
    Height(TerrainBrush),
    Material(TerrainMaterialBrush),
    VegetationDensity(VegetationDensityBrush),
}

impl ActiveBrush {
    const fn radius_m(&self) -> f32 {
        match self {
            Self::Height(brush) => brush.radius_m,
            Self::Material(brush) => brush.radius_m,
            Self::VegetationDensity(brush) => brush.radius_m,
        }
    }

    const fn strength(&self) -> f32 {
        match self {
            Self::Height(brush) => brush.strength,
            Self::Material(brush) => brush.strength,
            Self::VegetationDensity(brush) => brush.strength,
        }
    }

    fn set_center(&mut self, center: [f32; 2]) {
        match self {
            Self::Height(brush) => {
                brush.center_x_m = center[0];
                brush.center_z_m = center[1];
            }
            Self::Material(brush) => {
                brush.center_x_m = center[0];
                brush.center_z_m = center[1];
            }
            Self::VegetationDensity(brush) => {
                brush.center_x_m = center[0];
                brush.center_z_m = center[1];
            }
        }
    }
}

#[derive(Clone, Copy)]
pub(super) struct BrushActions {
    mode: RwSignal<String>,
    radius: RwSignal<String>,
    strength: RwSignal<String>,
    amount: RwSignal<String>,
    height: RwSignal<String>,
    slope_x: RwSignal<String>,
    slope_z: RwSignal<String>,
    seed: RwSignal<String>,
    density: RwSignal<String>,
    vegetation_layer: RwSignal<String>,
    stroke: RwSignal<Option<StrokeDraft>>,
    serial: RwSignal<u64>,
    error: RwSignal<Option<String>>,
    point: RwSignal<Option<[f32; 3]>>,
}

fn vegetation_layer_options(world: WorldActions) -> Vec<(String, String)> {
    let Some(key) = world.tabs.get().active else {
        return vec![];
    };
    let Some(document) = world.document.get() else {
        return vec![];
    };
    document
        .area(key.module_id, key.area_id)
        .map(|area| {
            area.vegetation
                .layers
                .iter()
                .map(|layer| (layer.id.to_string(), layer.display_name.clone()))
                .collect()
        })
        .unwrap_or_default()
}

impl BrushActions {
    fn brush(self) -> Result<Option<ActiveBrush>, String> {
        let mode = self.mode.get_untracked();
        if mode == "navigate" {
            return Ok(None);
        }
        let number = |field: RwSignal<String>| {
            field
                .get_untracked()
                .parse::<f32>()
                .map_err(|_| "Renseignez les paramètres numériques de la brosse.".to_owned())
        };
        let amplitude = || {
            let value = number(self.amount)?;
            if value.is_finite() && (0.1..=32.0).contains(&value) {
                Ok(value)
            } else {
                Err("L’amplitude doit être comprise entre 0,1 et 32 mètres.".to_owned())
            }
        };
        if mode == "paint_vegetation_density" {
            let percent = self
                .density
                .get_untracked()
                .trim()
                .parse::<u8>()
                .map_err(|_| "La densité cible doit être un entier de 0 à 100 %.".to_owned())?;
            if percent > 100 {
                return Err("La densité cible doit être comprise entre 0 et 100 %.".into());
            }
            let layer_id = self.vegetation_layer.get_untracked();
            if layer_id.is_empty() {
                return Err("Sélectionnez une couche de végétation de la zone.".into());
            }
            let target_density = u8::try_from((u16::from(percent) * 255 + 50) / 100)
                .map_err(|_| "Densité cible invalide.".to_owned())?;
            let brush = VegetationDensityBrush {
                schema_version: 1,
                center_x_m: 0.0,
                center_z_m: 0.0,
                radius_m: number(self.radius)?,
                strength: number(self.strength)? / 100.0,
                target_density,
                layer_id: VegetationLayerId::new(layer_id),
            };
            brush.validate().map_err(|error| error.to_string())?;
            return Ok(Some(ActiveBrush::VegetationDensity(brush)));
        }
        if let Some(material) = match mode.as_str() {
            "paint_grass" => Some(TerrainMaterialRole::Grass),
            "paint_earth" => Some(TerrainMaterialRole::Earth),
            "paint_rock" => Some(TerrainMaterialRole::Rock),
            _ => None,
        } {
            let brush = TerrainMaterialBrush {
                schema_version: 1,
                center_x_m: 0.0,
                center_z_m: 0.0,
                radius_m: number(self.radius)?,
                strength: number(self.strength)? / 100.0,
                material,
            };
            brush.validate().map_err(|error| error.to_string())?;
            return Ok(Some(ActiveBrush::Material(brush)));
        }
        let operation = match mode.as_str() {
            "raise" => TerrainBrushOperation::RaiseLower {
                delta_m: amplitude()?,
            },
            "lower" => TerrainBrushOperation::RaiseLower {
                delta_m: -amplitude()?,
            },
            "smooth" => TerrainBrushOperation::Smooth,
            "flatten" => TerrainBrushOperation::Flatten {
                height_m: number(self.height)?,
            },
            "noise" => TerrainBrushOperation::Noise {
                amplitude_m: amplitude()?,
                seed: self
                    .seed
                    .get_untracked()
                    .parse()
                    .map_err(|_| "Seed invalide (entier u32).")?,
            },
            "slope" => TerrainBrushOperation::Slope {
                height_m: number(self.height)?,
                gradient_x: number(self.slope_x)?,
                gradient_z: number(self.slope_z)?,
            },
            _ => return Err("Brosse inconnue.".into()),
        };
        let brush = TerrainBrush {
            schema_version: 1,
            center_x_m: 0.0,
            center_z_m: 0.0,
            radius_m: number(self.radius)?,
            strength: number(self.strength)? / 100.0,
            operation,
        };
        brush.validate().map_err(|e| e.to_string())?;
        Ok(Some(ActiveBrush::Height(brush)))
    }

    pub(super) fn move_pointer(
        self,
        world: WorldActions,
        event: &web_sys::PointerEvent,
    ) -> Option<u64> {
        if self
            .stroke
            .get_untracked()
            .is_some_and(|stroke| stroke.finishing || stroke.pointer_id != event.pointer_id())
        {
            return None;
        }
        let brush = match self.brush() {
            Ok(Some(brush)) => brush,
            Ok(None) => return None,
            Err(error) => {
                self.error.set(Some(error));
                return None;
            }
        };
        let element = event
            .current_target()?
            .dyn_into::<web_sys::Element>()
            .ok()?;
        let (width, height) = (element.client_width(), element.client_height());
        if width <= 0 || height <= 0 {
            return None;
        }
        #[allow(
            clippy::cast_possible_truncation,
            reason = "Normalized finite canvas coordinates"
        )]
        let uv = [
            (f64::from(event.offset_x()) / f64::from(width)) as f32,
            (f64::from(event.offset_y()) / f64::from(height)) as f32,
        ];
        if uv.iter().any(|value| !(0.0..=1.0).contains(value)) {
            return None;
        }
        self.serial.update(|value| *value += 1);
        let serial = self.serial.get_untracked();
        if let Ok(mut bridge) = world.viewport.get_value().0.lock() {
            bridge.brush_pointer = Some(EditorBrushPointer {
                generation: world.generation.get_untracked(),
                serial,
                uv,
                radius_m: brush.radius_m(),
                strength: brush.strength(),
            });
        }
        self.stroke.update(|stroke| {
            if let Some(stroke) = stroke {
                stroke.last_serial = serial;
            }
        });
        Some(serial)
    }

    pub(super) fn start(
        self,
        world: WorldActions,
        workspace: WorkspaceActions,
        event: &web_sys::PointerEvent,
    ) -> bool {
        if self.mode.get_untracked() == "navigate" || event.alt_key() || event.button() != 0 {
            return false;
        }
        if workspace.unavailable()
            || world.busy.get_untracked()
            || self.stroke.get_untracked().is_some()
        {
            return true;
        }
        let Some(key) = world.tabs.get_untracked().active else {
            return true;
        };
        let status = world.status.get_untracked();
        if status.generation != world.generation.get_untracked() || status.chunks != 64 {
            return true;
        }
        let Ok(Some(brush)) = self.brush() else {
            self.move_pointer(world, event);
            return true;
        };
        let Some(serial) = self.move_pointer(world, event) else {
            return true;
        };
        if let Ok(mut bridge) = world.viewport.get_value().0.lock() {
            bridge.brush_start_pointer = bridge.brush_pointer;
            bridge.brush_start_hit = None;
        }
        if let Some(element) = event
            .current_target()
            .and_then(|value| value.dyn_into::<web_sys::Element>().ok())
        {
            if element.set_pointer_capture(event.pointer_id()).is_err() {
                return true;
            }
            if let Some(element) = element.dyn_ref::<web_sys::HtmlElement>() {
                let _ = element.focus();
            }
            event.prevent_default();
            self.error.set(None);
            self.stroke.set(Some(StrokeDraft {
                request: AreaPreviewRequest {
                    key,
                    base_revision: workspace.state.get_untracked().revision,
                },
                generation: world.generation.get_untracked(),
                pointer_id: event.pointer_id(),
                brush,
                path: BrushPath::default(),
                last_serial: serial,
                sampled_serial: 0,
                finishing: false,
            }));
        }
        true
    }

    pub(super) fn finish(self, world: WorldActions, event: &web_sys::PointerEvent) {
        if self
            .stroke
            .get_untracked()
            .is_none_or(|stroke| stroke.pointer_id != event.pointer_id())
        {
            return;
        }
        self.move_pointer(world, event);
        self.stroke.update(|stroke| {
            if let Some(stroke) = stroke {
                stroke.finishing = true;
            }
        });
    }

    pub(super) fn cancel(self, unless_finishing: bool) {
        if !unless_finishing
            || !self
                .stroke
                .get_untracked()
                .is_some_and(|stroke| stroke.finishing)
        {
            self.stroke.set(None);
        }
    }

    pub(super) fn clear_hover(self, world: WorldActions) {
        if self.stroke.get_untracked().is_none()
            && let Ok(mut bridge) = world.viewport.get_value().0.lock()
        {
            bridge.brush_pointer = None;
        }
    }

    fn sample(self, world: WorldActions, workspace: WorkspaceActions) {
        let (hit, sampled, start_hit) = {
            let viewport = world.viewport.get_value();
            let Ok(bridge) = viewport.0.lock() else {
                return;
            };
            (
                bridge.brush_hit,
                bridge.brush_sample_serial,
                bridge.brush_start_hit,
            )
        };
        let point = hit.map(|hit| hit.point);
        if self.point.get_untracked() != point {
            self.point.set(point);
        }
        let Some(mut stroke) = self.stroke.get_untracked() else {
            return;
        };
        if world.generation.get_untracked() != stroke.generation
            || workspace.state.get_untracked().revision != stroke.request.base_revision
        {
            self.error.set(Some(
                "Le projet a changé : trait annulé sans modification.".into(),
            ));
            self.stroke.set(None);
            return;
        }
        if stroke.sampled_serial == 0
            && let Some(hit) = start_hit.filter(|hit| hit.generation == stroke.generation)
        {
            if let Err(error) = stroke.path.append(
                [
                    hit.point[0].clamp(-256.0, 256.0),
                    hit.point[2].clamp(-256.0, 256.0),
                ],
                stroke.brush.radius_m(),
            ) {
                self.error.set(Some(error));
                self.stroke.set(None);
                return;
            }
            stroke.sampled_serial = hit.serial;
        }
        if let Some(hit) = hit
            .filter(|hit| hit.generation == stroke.generation && hit.serial > stroke.sampled_serial)
        {
            if let Err(error) = stroke.path.append(
                [
                    hit.point[0].clamp(-256.0, 256.0),
                    hit.point[2].clamp(-256.0, 256.0),
                ],
                stroke.brush.radius_m(),
            ) {
                self.error.set(Some(error));
                self.stroke.set(None);
                return;
            }
            stroke.sampled_serial = hit.serial;
        }
        if stroke.finishing && sampled == Some(stroke.last_serial) {
            self.stroke.set(None);
            let Some(first) = stroke.path.points.first() else {
                self.error
                    .set(Some("Visez le terrain pour appliquer une brosse.".into()));
                return;
            };
            stroke.brush.set_center(*first);
            let additional_centers = stroke.path.points[1..].to_vec();
            match stroke.brush {
                ActiveBrush::Height(brush) => workspace.send(WorkspaceCommand::SculptTerrain {
                    request: stroke.request,
                    brush,
                    additional_centers,
                    dry_run: false,
                }),
                ActiveBrush::Material(brush) => {
                    workspace.send(WorkspaceCommand::PaintTerrainMaterial {
                        request: stroke.request,
                        brush,
                        additional_centers,
                        dry_run: false,
                    });
                }
                ActiveBrush::VegetationDensity(brush) => {
                    workspace.send(WorkspaceCommand::PaintVegetationDensity {
                        request: stroke.request,
                        brush,
                        additional_centers,
                        dry_run: false,
                    });
                }
            }
        } else {
            self.stroke.set(Some(stroke));
        }
    }
}

pub(super) fn create_brush_actions(
    world: WorldActions,
    workspace: WorkspaceActions,
) -> BrushActions {
    let actions = BrushActions {
        mode: RwSignal::new("navigate".into()),
        radius: RwSignal::new("16".into()),
        strength: RwSignal::new("50".into()),
        amount: RwSignal::new("4".into()),
        height: RwSignal::new("4".into()),
        slope_x: RwSignal::new("0.25".into()),
        slope_z: RwSignal::new("0".into()),
        seed: RwSignal::new("1".into()),
        density: RwSignal::new("100".into()),
        vegetation_layer: RwSignal::new(String::new()),
        stroke: RwSignal::new(None),
        serial: RwSignal::new(0),
        error: RwSignal::new(None),
        point: RwSignal::new(None),
    };
    Effect::new(move |_| {
        let _ = world.generation.get();
        let _ = actions.mode.get();
        let layer_ids = vegetation_layer_options(world)
            .into_iter()
            .map(|(id, _)| id)
            .collect::<Vec<_>>();
        let selected = actions.vegetation_layer.get_untracked();
        if !layer_ids.iter().any(|layer_id| layer_id == &selected) {
            actions
                .vegetation_layer
                .set(layer_ids.first().cloned().unwrap_or_default());
        }
        actions.cancel(false);
        actions.point.set(None);
        if let Ok(mut bridge) = world.viewport.get_value().0.lock() {
            bridge.brush_pointer = None;
            bridge.brush_hit = None;
        }
    });
    let poll = Closure::<dyn FnMut()>::new(move || actions.sample(world, workspace));
    if let Some(window) = web_sys::window() {
        match window.set_interval_with_callback_and_timeout_and_arguments_0(
            poll.as_ref().unchecked_ref(),
            32,
        ) {
            Ok(_) => poll.forget(),
            Err(error) => actions
                .error
                .set(Some(format!("Pointeur indisponible : {error:?}"))),
        }
    }
    actions
}

#[component]
fn BrushNumber(
    label: &'static str,
    value: RwSignal<String>,
    min: &'static str,
    max: &'static str,
    #[prop(default = "0.1")] step: &'static str,
) -> impl IntoView {
    view! { <label>{label}<input type="number" min=min max=max step=step prop:value=move || value.get()
    on:input=move |event| value.set(event_target_value(&event))/></label> }
}

#[component]
pub(super) fn BrushPanel(
    brush: BrushActions,
    world: WorldActions,
    workspace: WorkspaceActions,
) -> impl IntoView {
    view! { <section class="terrain-brush-panel" aria-label="Brosses de zone">
        <h2>"Modifier la zone"</h2>
        <fieldset disabled=move || workspace.unavailable() || brush.stroke.get().is_some()>
            <label>"Outil"<select aria-label="Outil de zone" prop:value=move || brush.mode.get() on:change=move |event| brush.mode.set(event_target_value(&event))>
                <option value="navigate">"Navigation"</option><option value="raise">"Lever"</option><option value="lower">"Abaisser"</option>
                <option value="smooth">"Lisser"</option><option value="flatten">"Aplatir"</option><option value="noise">"Bruit"</option><option value="slope">"Pente"</option>
                <option value="paint_grass">"Peindre · Herbe"</option><option value="paint_earth">"Peindre · Terre"</option><option value="paint_rock">"Peindre · Roche"</option>
                <option value="paint_vegetation_density">"Peindre · Densité végétale"</option>
            </select></label>
            <Show when=move || brush.mode.get() != "navigate">
                <BrushNumber label="Rayon (m)" value=brush.radius min="2" max="64" step="1"/>
                <BrushNumber label="Intensité (%)" value=brush.strength min="1" max="100" step="1"/>
                <Show when=move || matches!(brush.mode.get().as_str(), "raise" | "lower" | "noise")><BrushNumber label="Amplitude (m)" value=brush.amount min="0.1" max="32"/></Show>
                <Show when=move || matches!(brush.mode.get().as_str(), "flatten" | "slope")><BrushNumber label="Altitude cible (m)" value=brush.height min="-1024" max="1024"/></Show>
                <Show when=move || brush.mode.get() == "noise"><BrushNumber label="Seed du bruit" value=brush.seed min="0" max="4294967295" step="1"/></Show>
                <Show when=move || brush.mode.get() == "slope"><BrushNumber label="Pente X (m/m)" value=brush.slope_x min="-4" max="4"/><BrushNumber label="Pente Z (m/m)" value=brush.slope_z min="-4" max="4"/></Show>
                <Show when=move || brush.mode.get() == "paint_vegetation_density">
                    <label>"Couche végétale"<select aria-label="Couche végétale" prop:value=move || brush.vegetation_layer.get()
                        on:change=move |event| brush.vegetation_layer.set(event_target_value(&event))>
                        <option value="">"Aucune couche disponible"</option>
                        {move || vegetation_layer_options(world).into_iter().map(|(id, name)| {
                            view! { <option value=id>{name}</option> }
                        }).collect_view()}
                    </select></label>
                    <BrushNumber label="Densité cible (%)" value=brush.density min="0" max="100" step="1"/>
                </Show>
            </Show>
        </fieldset>
        <p class="hint">"Glisser : tracer · relâcher : appliquer · Alt + glisser : orbite. Un trait = une annulation."</p>
        <p class="brush-position">{move || brush.point.get().map_or_else(|| "Visez le terrain".into(), |p| format!("X {:.1} · Y {:.1} · Z {:.1}", p[0], p[1], p[2]))}</p>
        <p class="brush-position">{move || brush.stroke.get().map_or_else(String::new, |stroke| format!("Trait · {} empreintes · non appliqué", stroke.path.points.len()))}</p>
        <div class="brush-history"><button type="button" disabled=move || workspace.unavailable() || !workspace.state.get().can_undo
            on:click=move |_| workspace.send(WorkspaceCommand::Undo { base_revision: workspace.state.get_untracked().revision })>"Annuler"</button>
            <button type="button" disabled=move || workspace.unavailable() || !workspace.state.get().can_redo
            on:click=move |_| workspace.send(WorkspaceCommand::Redo { base_revision: workspace.state.get_untracked().revision })>"Rétablir"</button></div>
        <p class="brush-error" role="alert">{move || brush.error.get().or(workspace.error.get()).unwrap_or_default()}</p>
        <p class="brush-message" role="status">{move || if workspace.pending.get() { "Application native du trait…".into() } else { workspace.state.get().message }}</p>
        <Show when=move || world.tabs.get().active.is_none()><p class="hint">"Ouvrez une zone avant de la modifier."</p></Show>
    </section> }
}
