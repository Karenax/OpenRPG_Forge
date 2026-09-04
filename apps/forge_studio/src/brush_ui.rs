use super::{AreaPreviewRequest, WorkspaceActions, WorldActions, invoke_studio};
use crate::{
    StudioCommand, StudioResponse,
    brush_state::{
        BrushPath, StrokePreviewCoordinator, StrokePreviewDispatch, StrokePreviewTicket,
    },
    workspace_api::{
        VegetationDensityStrokePreviewRequest, VegetationDensityStrokePreviewResponse,
        VegetationExclusionDraft, WorkspaceCommand,
    },
};
use forge_engine::editor_viewport::{EditorBrushHit, EditorBrushPointer};
use forge_project::{TerrainMaterialRole, VegetationLayerId};
use forge_world::{
    TerrainBrush, TerrainBrushOperation, TerrainMaterialBrush, VegetationDensityBrush,
};
use leptos::prelude::*;
use wasm_bindgen::{JsCast, closure::Closure};

#[path = "vegetation_ui.rs"]
mod vegetation;
pub(super) use vegetation::VegetationPanel;

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
    final_preview_queued: bool,
}

#[derive(Clone, Debug, PartialEq)]
struct DensityPreviewTrajectory {
    world_generation: u64,
    brush: VegetationDensityBrush,
    additional_centers: Vec<[f32; 2]>,
}

#[derive(Clone)]
struct FinalDensityCommit {
    request: AreaPreviewRequest,
    generation: u64,
    trajectory: DensityPreviewTrajectory,
    candidate_revision: u64,
    candidate_density_source_hash: forge_core::ContentHash,
    no_op: bool,
    apply_sent: bool,
}

#[derive(Clone)]
enum ActiveBrush {
    Height(TerrainBrush),
    Material(TerrainMaterialBrush),
    VegetationDensity(VegetationDensityBrush),
    VegetationExclusion(ExclusionBrush),
}

#[derive(Clone, Copy)]
enum ExclusionBrush {
    Circle { radius_m: f32 },
    Corridor { half_width_m: f32 },
}

impl ExclusionBrush {
    const fn radius_m(self) -> f32 {
        match self {
            Self::Circle { radius_m } => radius_m,
            Self::Corridor { half_width_m } => half_width_m,
        }
    }
}

impl ActiveBrush {
    const fn radius_m(&self) -> f32 {
        match self {
            Self::Height(brush) => brush.radius_m,
            Self::Material(brush) => brush.radius_m,
            Self::VegetationDensity(brush) => brush.radius_m,
            Self::VegetationExclusion(brush) => brush.radius_m(),
        }
    }

    const fn strength(&self) -> f32 {
        match self {
            Self::Height(brush) => brush.strength,
            Self::Material(brush) => brush.strength,
            Self::VegetationDensity(brush) => brush.strength,
            Self::VegetationExclusion(_) => 1.0,
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
            Self::VegetationExclusion(_) => {}
        }
    }

    const fn is_exclusion(&self) -> bool {
        matches!(self, Self::VegetationExclusion(_))
    }
}

impl StrokeDraft {
    fn record_point(&mut self, point: [f32; 2]) -> Result<(), String> {
        if self.brush.is_exclusion() {
            match self.path.points.len() {
                0 | 1 => self.path.points.push(point),
                _ => {
                    if let Some(last) = self.path.points.last_mut() {
                        *last = point;
                    }
                }
            }
            Ok(())
        } else {
            self.path.append(point, self.brush.radius_m())
        }
    }

    fn density_trajectory(&self) -> Option<DensityPreviewTrajectory> {
        let first = self.path.points.first().copied()?;
        let ActiveBrush::VegetationDensity(mut brush) = self.brush.clone() else {
            return None;
        };
        brush.center_x_m = first[0];
        brush.center_z_m = first[1];
        Some(DensityPreviewTrajectory {
            world_generation: self.generation,
            brush,
            additional_centers: self.path.points[1..].to_vec(),
        })
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
    density_preview: StoredValue<Option<StrokePreviewCoordinator<DensityPreviewTrajectory>>>,
    final_density_commit: RwSignal<Option<FinalDensityCommit>>,
    restore_after_preview: StoredValue<bool>,
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
    fn number(field: RwSignal<String>) -> Result<f32, String> {
        field
            .get_untracked()
            .parse::<f32>()
            .map_err(|_| "Renseignez les paramètres numériques de la brosse.".to_owned())
    }

    fn amplitude(self) -> Result<f32, String> {
        let value = Self::number(self.amount)?;
        if value.is_finite() && (0.1..=32.0).contains(&value) {
            Ok(value)
        } else {
            Err("L’amplitude doit être comprise entre 0,1 et 32 mètres.".to_owned())
        }
    }

    fn exclusion_brush(self, mode: &str) -> Result<ActiveBrush, String> {
        let radius_m = Self::number(self.radius)?;
        if !radius_m.is_finite() || !(2.0..=64.0).contains(&radius_m) {
            return Err(
                "Le rayon ou la demi-largeur doit être compris entre 2 et 64 mètres.".into(),
            );
        }
        let brush = if mode == "exclude_vegetation_circle" {
            ExclusionBrush::Circle { radius_m }
        } else {
            ExclusionBrush::Corridor {
                half_width_m: radius_m,
            }
        };
        Ok(ActiveBrush::VegetationExclusion(brush))
    }

    fn density_brush(self) -> Result<ActiveBrush, String> {
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
            radius_m: Self::number(self.radius)?,
            strength: Self::number(self.strength)? / 100.0,
            target_density,
            layer_id: VegetationLayerId::new(layer_id),
        };
        brush.validate().map_err(|error| error.to_string())?;
        Ok(ActiveBrush::VegetationDensity(brush))
    }

    fn material_brush(self, material: TerrainMaterialRole) -> Result<ActiveBrush, String> {
        let brush = TerrainMaterialBrush {
            schema_version: 1,
            center_x_m: 0.0,
            center_z_m: 0.0,
            radius_m: Self::number(self.radius)?,
            strength: Self::number(self.strength)? / 100.0,
            material,
        };
        brush.validate().map_err(|error| error.to_string())?;
        Ok(ActiveBrush::Material(brush))
    }

    fn height_brush(self, mode: &str) -> Result<ActiveBrush, String> {
        let operation = match mode {
            "raise" => TerrainBrushOperation::RaiseLower {
                delta_m: self.amplitude()?,
            },
            "lower" => TerrainBrushOperation::RaiseLower {
                delta_m: -self.amplitude()?,
            },
            "smooth" => TerrainBrushOperation::Smooth,
            "flatten" => TerrainBrushOperation::Flatten {
                height_m: Self::number(self.height)?,
            },
            "noise" => TerrainBrushOperation::Noise {
                amplitude_m: self.amplitude()?,
                seed: self
                    .seed
                    .get_untracked()
                    .parse()
                    .map_err(|_| "Seed invalide (entier u32).")?,
            },
            "slope" => TerrainBrushOperation::Slope {
                height_m: Self::number(self.height)?,
                gradient_x: Self::number(self.slope_x)?,
                gradient_z: Self::number(self.slope_z)?,
            },
            _ => return Err("Brosse inconnue.".into()),
        };
        let brush = TerrainBrush {
            schema_version: 1,
            center_x_m: 0.0,
            center_z_m: 0.0,
            radius_m: Self::number(self.radius)?,
            strength: Self::number(self.strength)? / 100.0,
            operation,
        };
        brush.validate().map_err(|error| error.to_string())?;
        Ok(ActiveBrush::Height(brush))
    }

    fn brush(self) -> Result<Option<ActiveBrush>, String> {
        let mode = self.mode.get_untracked();
        if mode == "navigate" {
            return Ok(None);
        }
        if matches!(
            mode.as_str(),
            "exclude_vegetation_circle" | "exclude_vegetation_corridor"
        ) {
            return self.exclusion_brush(&mode).map(Some);
        }
        if mode == "paint_vegetation_density" {
            return self.density_brush().map(Some);
        }
        if let Some(material) = match mode.as_str() {
            "paint_grass" => Some(TerrainMaterialRole::Grass),
            "paint_earth" => Some(TerrainMaterialRole::Earth),
            "paint_rock" => Some(TerrainMaterialRole::Rock),
            _ => None,
        } {
            return self.material_brush(material).map(Some);
        }
        self.height_brush(&mode).map(Some)
    }

    fn begin_density_preview(self, request: AreaPreviewRequest) -> Result<(), String> {
        let mut coordinator = self.density_preview.write_value();
        if let Some(coordinator) = coordinator.as_mut() {
            coordinator
                .set_request(request)
                .map_err(|_| "Compteur d’aperçu épuisé.".to_owned())?;
        } else {
            *coordinator = Some(StrokePreviewCoordinator::new(request));
        }
        *self.restore_after_preview.write_value() = false;
        self.final_density_commit.set(None);
        Ok(())
    }

    fn submit_density_preview(
        self,
        world: WorldActions,
        stroke: &StrokeDraft,
        finished: bool,
    ) -> Result<(), String> {
        let trajectory = stroke
            .density_trajectory()
            .ok_or("Trajectoire de densité vide ou incompatible.")?;
        let dispatch = self
            .density_preview
            .write_value()
            .as_mut()
            .ok_or("Coordinateur d’aperçu indisponible.")?
            .submit(preview_now_ms(), trajectory, finished)
            .map_err(|_| "Compteur d’aperçu épuisé.".to_owned())?;
        if let Some(dispatch) = dispatch {
            self.dispatch_density_preview(world, dispatch);
        }
        Ok(())
    }

    fn tick_density_preview(self, world: WorldActions) {
        if world.density_preview_busy.get_untracked() {
            return;
        }
        let dispatch = self
            .density_preview
            .write_value()
            .as_mut()
            .and_then(|coordinator| coordinator.tick(preview_now_ms()).ok())
            .flatten();
        if let Some(dispatch) = dispatch {
            self.dispatch_density_preview(world, dispatch);
        }
    }

    fn dispatch_density_preview(
        self,
        world: WorldActions,
        dispatch: StrokePreviewDispatch<DensityPreviewTrajectory>,
    ) {
        let base_source_hash = world.density_preview_base_hash(
            dispatch.trajectory.world_generation,
            dispatch.ticket.request,
        );
        let preview = VegetationDensityStrokePreviewRequest {
            request: dispatch.ticket.request,
            brush: dispatch.trajectory.brush.clone(),
            additional_centers: dispatch.trajectory.additional_centers.clone(),
            preview_serial: dispatch.ticket.serial,
            base_source_hash,
        };
        world.density_preview_busy.set(true);
        wasm_bindgen_futures::spawn_local(async move {
            let result = fetch_density_preview(world, dispatch.ticket, preview).await;
            world.density_preview_busy.set(false);
            self.finish_density_preview(world, dispatch, result);
        });
    }

    fn finish_density_preview(
        self,
        world: WorldActions,
        dispatch: StrokePreviewDispatch<DensityPreviewTrajectory>,
        result: Result<ValidatedDensityPreview, String>,
    ) {
        let ticket = dispatch.ticket;
        let settlement = {
            let mut slot = self.density_preview.write_value();
            let Some(coordinator) = slot.as_mut() else {
                return;
            };
            coordinator.settle(preview_now_ms(), ticket)
        };
        let Ok(settlement) = settlement else {
            self.fail_density_preview(world, "Compteur d’aperçu épuisé.", None);
            return;
        };

        if settlement.present_response && result.is_err() {
            let message = result
                .err()
                .unwrap_or_else(|| "Aperçu de densité invalide.".into());
            self.fail_density_preview(
                world,
                &message,
                settlement.next.as_ref().map(|next| next.ticket),
            );
            return;
        }

        if settlement.present_response
            && let Ok(validated) = result
        {
            if let Err(error) = world.publish_density_preview(
                dispatch.trajectory.world_generation,
                validated.response.candidate_revision,
                validated.projection,
            ) {
                self.fail_density_preview(
                    world,
                    &error,
                    settlement.next.as_ref().map(|next| next.ticket),
                );
                return;
            }
            if dispatch.finished {
                self.final_density_commit.set(Some(FinalDensityCommit {
                    request: ticket.request,
                    generation: dispatch.trajectory.world_generation,
                    trajectory: dispatch.trajectory,
                    candidate_revision: validated.response.candidate_revision,
                    candidate_density_source_hash: validated.response.candidate_density_source_hash,
                    no_op: validated.response.no_op,
                    apply_sent: false,
                }));
            }
        }

        if let Some(next) = settlement.next {
            self.dispatch_density_preview(world, next);
        } else {
            self.restore_canonical_if_idle(world);
        }
    }

    fn fail_density_preview(
        self,
        world: WorldActions,
        message: &str,
        unstarted: Option<StrokePreviewTicket>,
    ) {
        if let Some(coordinator) = self.density_preview.write_value().as_mut() {
            if let Some(ticket) = unstarted {
                let _ = coordinator.discard_before_send(ticket);
            }
            let _ = coordinator.cancel();
        }
        self.error.set(Some(message.to_owned()));
        self.stroke.set(None);
        self.final_density_commit.set(None);
        *self.restore_after_preview.write_value() = true;
        self.restore_canonical_if_idle(world);
    }

    fn restore_canonical_if_idle(self, world: WorldActions) {
        if !*self.restore_after_preview.read_value() {
            return;
        }
        let idle = self
            .density_preview
            .read_value()
            .as_ref()
            .is_none_or(|coordinator| !coordinator.has_in_flight());
        if idle {
            *self.restore_after_preview.write_value() = false;
            *self.density_preview.write_value() = None;
            world.request_canonical_projection();
        }
    }

    fn commit_density_if_observed(self, world: WorldActions, workspace: WorkspaceActions) {
        let Some(mut commit) = self.final_density_commit.get_untracked() else {
            return;
        };
        if commit.apply_sent {
            if workspace.pending.get_untracked() {
                return;
            }
            if let Some(error) = workspace.error.get_untracked() {
                self.fail_density_preview(world, &error, None);
            } else if workspace.state.get_untracked().revision == commit.candidate_revision {
                self.final_density_commit.set(None);
            } else {
                self.fail_density_preview(
                    world,
                    "Validation de densité sans révision candidate correspondante.",
                    None,
                );
            }
            return;
        }
        if world.status.get_untracked().error.is_some() {
            self.fail_density_preview(world, "Le viewport a refusé l’aperçu de densité.", None);
            return;
        }
        if world.generation.get_untracked() != commit.generation
            || world.tabs.get_untracked().active != Some(commit.request.key)
            || workspace.state.get_untracked().revision != commit.request.base_revision
        {
            self.fail_density_preview(
                world,
                "Le projet a changé : trait annulé sans modification.",
                None,
            );
            return;
        }
        if workspace.unavailable()
            || !world.density_preview_observed(
                commit.generation,
                commit.request,
                commit.candidate_revision,
                commit.candidate_density_source_hash,
            )
        {
            return;
        }

        self.final_density_commit.set(None);
        self.stroke.set(None);
        *self.density_preview.write_value() = None;
        if commit.no_op {
            return;
        }
        commit.apply_sent = true;
        let command = WorkspaceCommand::PaintVegetationDensity {
            request: commit.request,
            brush: commit.trajectory.brush.clone(),
            additional_centers: commit.trajectory.additional_centers.clone(),
            dry_run: false,
        };
        self.final_density_commit.set(Some(commit));
        workspace.send(command);
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
            let density_preview = matches!(&brush, ActiveBrush::VegetationDensity(_));
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
                final_preview_queued: false,
            }));
            if density_preview
                && let Err(error) = self.begin_density_preview(AreaPreviewRequest {
                    key,
                    base_revision: workspace.state.get_untracked().revision,
                })
            {
                self.error.set(Some(error));
                self.stroke.set(None);
            }
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

    pub(super) fn cancel(self, world: WorldActions, unless_finishing: bool) {
        if self
            .final_density_commit
            .get_untracked()
            .is_some_and(|commit| commit.apply_sent)
        {
            return;
        }
        if unless_finishing
            && self
                .stroke
                .get_untracked()
                .is_some_and(|stroke| stroke.finishing)
        {
            return;
        }
        self.stroke.set(None);
        self.final_density_commit.set(None);
        let had_density_preview = self.density_preview.read_value().is_some();
        if let Some(coordinator) = self.density_preview.write_value().as_mut()
            && coordinator.cancel().is_err()
        {
            self.error.set(Some("Compteur d’aperçu épuisé.".into()));
        }
        if had_density_preview {
            *self.restore_after_preview.write_value() = true;
            self.restore_canonical_if_idle(world);
        }
    }

    pub(super) fn clear_hover(self, world: WorldActions) {
        if self.stroke.get_untracked().is_none()
            && let Ok(mut bridge) = world.viewport.get_value().0.lock()
        {
            bridge.brush_pointer = None;
        }
    }

    fn record_hit(
        self,
        world: WorldActions,
        stroke: &mut StrokeDraft,
        hit: EditorBrushHit,
    ) -> Option<bool> {
        let previous_path = stroke.path.clone();
        if let Err(error) = stroke.record_point([
            hit.point[0].clamp(-256.0, 256.0),
            hit.point[2].clamp(-256.0, 256.0),
        ]) {
            self.error.set(Some(error));
            self.cancel(world, false);
            return None;
        }
        stroke.sampled_serial = hit.serial;
        Some(stroke.path != previous_path)
    }

    fn exclusion_command(
        request: AreaPreviewRequest,
        brush: ExclusionBrush,
        first: [f32; 2],
        last: [f32; 2],
    ) -> Result<WorkspaceCommand, String> {
        let draft = match brush {
            ExclusionBrush::Circle { radius_m } => VegetationExclusionDraft::Circle {
                center_x_m: first[0],
                center_z_m: first[1],
                radius_m,
            },
            ExclusionBrush::Corridor { half_width_m } => {
                let delta_x = f64::from(last[0]) - f64::from(first[0]);
                let delta_z = f64::from(last[1]) - f64::from(first[1]);
                if delta_x * delta_x + delta_z * delta_z == 0.0 {
                    return Err(
                        "Un passage exige un glissement entre deux points distincts. Rien n’a été appliqué."
                            .into(),
                    );
                }
                VegetationExclusionDraft::CorridorSegment {
                    start_x_m: first[0],
                    start_z_m: first[1],
                    end_x_m: last[0],
                    end_z_m: last[1],
                    half_width_m,
                }
            }
        };
        Ok(WorkspaceCommand::AddVegetationExclusion { request, draft })
    }

    fn dispatch_stroke(self, workspace: WorkspaceActions, mut stroke: StrokeDraft) {
        let Some(first) = stroke.path.points.first().copied() else {
            self.error
                .set(Some("Visez le terrain pour appliquer une brosse.".into()));
            return;
        };
        stroke.brush.set_center(first);
        let additional_centers = stroke.path.points[1..].to_vec();
        let last = stroke.path.points.last().copied().unwrap_or(first);
        let command = match stroke.brush {
            ActiveBrush::Height(brush) => WorkspaceCommand::SculptTerrain {
                request: stroke.request,
                brush,
                additional_centers,
                dry_run: false,
            },
            ActiveBrush::Material(brush) => WorkspaceCommand::PaintTerrainMaterial {
                request: stroke.request,
                brush,
                additional_centers,
                dry_run: false,
            },
            ActiveBrush::VegetationDensity(brush) => WorkspaceCommand::PaintVegetationDensity {
                request: stroke.request,
                brush,
                additional_centers,
                dry_run: false,
            },
            ActiveBrush::VegetationExclusion(brush) => {
                match Self::exclusion_command(stroke.request, brush, first, last) {
                    Ok(command) => command,
                    Err(error) => {
                        self.error.set(Some(error));
                        return;
                    }
                }
            }
        };
        workspace.send(command);
    }

    fn sample(self, world: WorldActions, workspace: WorkspaceActions) {
        self.tick_density_preview(world);
        self.commit_density_if_observed(world, workspace);
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
            self.restore_canonical_if_idle(world);
            return;
        };
        if world.generation.get_untracked() != stroke.generation
            || world.tabs.get_untracked().active != Some(stroke.request.key)
            || workspace.state.get_untracked().revision != stroke.request.base_revision
        {
            self.error.set(Some(
                "Le projet a changé : trait annulé sans modification.".into(),
            ));
            self.cancel(world, false);
            return;
        }
        if matches!(&stroke.brush, ActiveBrush::VegetationDensity(_))
            && world.status.get_untracked().error.is_some()
        {
            self.fail_density_preview(world, "Le viewport a refusé l’aperçu de densité.", None);
            return;
        }

        let mut path_changed = false;
        if stroke.sampled_serial == 0
            && let Some(hit) = start_hit.filter(|hit| hit.generation == stroke.generation)
        {
            let Some(changed) = self.record_hit(world, &mut stroke, hit) else {
                return;
            };
            path_changed |= changed;
        }
        if let Some(hit) = hit
            .filter(|hit| hit.generation == stroke.generation && hit.serial > stroke.sampled_serial)
        {
            let Some(changed) = self.record_hit(world, &mut stroke, hit) else {
                return;
            };
            path_changed |= changed;
        }

        if matches!(&stroke.brush, ActiveBrush::VegetationDensity(_)) {
            let final_sampled = stroke.finishing && sampled == Some(stroke.last_serial);
            let queue_final = final_sampled && !stroke.final_preview_queued;
            if queue_final {
                stroke.final_preview_queued = true;
            }
            self.stroke.set(Some(stroke.clone()));
            let preview_result = if queue_final {
                self.submit_density_preview(world, &stroke, true)
            } else if path_changed && !stroke.finishing {
                self.submit_density_preview(world, &stroke, false)
            } else {
                Ok(())
            };
            if let Err(error) = preview_result {
                self.fail_density_preview(world, &error, None);
            }
            return;
        }

        if stroke.finishing && sampled == Some(stroke.last_serial) {
            self.stroke.set(None);
            self.dispatch_stroke(workspace, stroke);
        } else {
            self.stroke.set(Some(stroke));
        }
    }
}

struct ValidatedDensityPreview {
    response: VegetationDensityStrokePreviewResponse,
    projection: crate::area_preview::AreaPreview,
}

async fn fetch_density_preview(
    world: WorldActions,
    ticket: StrokePreviewTicket,
    sent: VegetationDensityStrokePreviewRequest,
) -> Result<ValidatedDensityPreview, String> {
    let response = match invoke_studio(StudioCommand::VegetationDensityStrokePreview {
        preview: sent.clone(),
    })
    .await?
    {
        StudioResponse::VegetationDensityStrokePreview { preview } => preview,
        StudioResponse::Rejected { reason } => return Err(reason),
        _ => return Err("Réponse inattendue pour l’aperçu de densité.".into()),
    };
    validate_density_preview(world, ticket, &sent, response)
}

fn validate_density_preview(
    world: WorldActions,
    ticket: StrokePreviewTicket,
    sent: &VegetationDensityStrokePreviewRequest,
    response: VegetationDensityStrokePreviewResponse,
) -> Result<ValidatedDensityPreview, String> {
    if sent.request != ticket.request
        || sent.preview_serial != ticket.serial
        || response.request != ticket.request
        || response.preview_serial != ticket.serial
        || response.no_op != (response.changed_cells == 0)
    {
        return Err("Enveloppe d’aperçu de densité périmée ou incohérente.".into());
    }
    let changed_revision = ticket
        .request
        .base_revision
        .checked_add(1)
        .ok_or("Révision candidate épuisée.")?;
    let expected_revision = if response.no_op {
        ticket.request.base_revision
    } else {
        changed_revision
    };
    if response.candidate_revision != expected_revision {
        return Err("Révision candidate d’aperçu incohérente.".into());
    }

    let document = world
        .document
        .get_untracked()
        .ok_or("Projet absent pendant l’aperçu.")?;
    if document.revision.get() != ticket.request.base_revision
        || world.tabs.get_untracked().active != Some(ticket.request.key)
    {
        return Err("Cible d’aperçu changée avant la réponse.".into());
    }
    let area = document
        .area(ticket.request.key.module_id, ticket.request.key.area_id)
        .ok_or("Zone d’aperçu introuvable.")?;
    let terrain_hash = area
        .terrain
        .content_hash()
        .map_err(|error| error.to_string())?;
    let vegetation_hash = area
        .vegetation
        .content_hash()
        .map_err(|error| error.to_string())?;
    let projection = crate::area_preview::read_area_preview(&response.preview_json)?;
    if projection.request != ticket.request
        || projection.terrain.source_document_hash != terrain_hash
        || projection.vegetation.terrain_document_hash != terrain_hash
        || projection.vegetation.vegetation_document_hash != vegetation_hash
        || projection.vegetation_density_source_hash != response.candidate_density_source_hash
        || projection
            .vegetation_changed_chunks
            .checked_add(projection.vegetation_retained_chunks)
            != Some(64)
        || (projection.base_source_hash.is_some()
            && projection.base_source_hash != sent.base_source_hash)
    {
        return Err("Projection candidate périmée ou incohérente.".into());
    }
    Ok(ValidatedDensityPreview {
        response,
        projection,
    })
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    reason = "Browser monotonic milliseconds are finite and clamped to JavaScript's exact range"
)]
fn preview_now_ms() -> u64 {
    const MAX_EXACT_JS_INTEGER: f64 = 9_007_199_254_740_991.0;
    web_sys::window()
        .and_then(|window| window.performance())
        .map_or_else(js_sys::Date::now, |performance| performance.now())
        .clamp(0.0, MAX_EXACT_JS_INTEGER) as u64
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
        density_preview: StoredValue::new(None),
        final_density_commit: RwSignal::new(None),
        restore_after_preview: StoredValue::new(false),
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
        actions.cancel(world, false);
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
                <option value="exclude_vegetation_circle">"Exclusion végétale · Cercle"</option>
                <option value="exclude_vegetation_corridor">"Exclusion végétale · Passage"</option>
            </select></label>
            <Show when=move || brush.mode.get() != "navigate">
                <Show when=move || brush.mode.get() == "exclude_vegetation_corridor" fallback=move || view! {
                    <BrushNumber label="Rayon (m)" value=brush.radius min="2" max="64" step="1"/>
                }>
                    <BrushNumber label="Demi-largeur (m)" value=brush.radius min="2" max="64" step="1"/>
                </Show>
                <Show when=move || !matches!(brush.mode.get().as_str(), "exclude_vegetation_circle" | "exclude_vegetation_corridor")>
                    <BrushNumber label="Intensité (%)" value=brush.strength min="1" max="100" step="1"/>
                </Show>
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
        <p class="hint">{move || match brush.mode.get().as_str() {
            "exclude_vegetation_circle" => "Cliquer : poser un cercle au premier point · relâcher : appliquer.",
            "exclude_vegetation_corridor" => "Glisser : passage droit du premier au dernier point · relâcher : appliquer.",
            _ => "Glisser : tracer · relâcher : appliquer · Alt + glisser : orbite. Un trait = une annulation.",
        }}</p>
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
