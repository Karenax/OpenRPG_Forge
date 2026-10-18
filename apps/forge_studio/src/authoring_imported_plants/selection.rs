//! One-shot ID readback in the imported terrain canvas. Selection never edits a source.
use super::{State, population};
use crate::authoring_assets::imported_buildings;
use bevy::{
    diagnostic::FrameCount,
    ecs as bevy_ecs,
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, RenderApp,
        gpu_readback::{Readback, ReadbackComplete},
        render_resource::{CachedPipelineState, PipelineCache, PipelineDescriptor},
    },
    shader::ShaderCacheError,
};
use forge_core::{ContentHash, EntityId};
use forge_project::{
    VegetationLayerId,
    authoring::{AuthoringRequest, AuthoringState},
    zone_authoring::{
        ZoneAuthoringHead,
        imported_plants::ImportedPlantPlacementEdit,
        session::{ZoneAction, ZoneRequest},
    },
};
use forge_tidewater::tidewater_plants::picking::{
    self, decode,
    occluders::{self, Prepared},
    render::{Pass, View},
};
use leptos::prelude::*;
use std::cell::Cell;

#[path = "readback.rs"]
mod readback;

thread_local! {
    static EPOCH: Cell<u64> = const { Cell::new(0) };
    static ARMED: Cell<Option<(ContentHash, u64)>> = const { Cell::new(None) };
    static CLICK: Cell<Option<(ContentHash, u64, Vec2)>> = const { Cell::new(None) };
    static SELECTED: Cell<Option<EntityId>> = const { Cell::new(None) };
    static OUTPUT: Cell<Option<RwSignal<Option<EntityId>>>> = const { Cell::new(None) };
    static STATUS: Cell<Option<RwSignal<String>>> = const { Cell::new(None) };
}

#[derive(Resource, Default)]
struct Pipelines {
    ready_frames: u8,
    failure: Option<String>,
    expected: [bool; 4],
}

#[derive(Clone, PartialEq, Eq)]
struct CameraKey {
    matrix: [u32; 16],
    projection: String,
    size: UVec2,
}

impl CameraKey {
    fn new(
        camera: &Camera,
        transform: &GlobalTransform,
        projection: &Projection,
    ) -> Result<Self, String> {
        let size = camera
            .physical_viewport_size()
            .ok_or("Vue GPU sans taille")?;
        if size.x == 0 || size.y == 0 || size.x > 4096 || size.y > 4096 {
            return Err("Vue GPU hors des dimensions de sélection".into());
        }
        Ok(Self {
            matrix: transform.to_matrix().to_cols_array().map(f32::to_bits),
            projection: format!("{projection:?}"),
            size,
        })
    }
}

#[derive(Resource)]
struct Pending {
    pass: Pass,
    head: ContentHash,
    generation: u64,
    epoch: u64,
    position: Vec2,
    camera: Entity,
    camera_key: CameraKey,
    started: u32,
    started_at_secs: f64,
    reader: Option<Entity>,
    done: bool,
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<Pipelines>()
        .add_systems(Update, request_readback)
        .add_systems(
            PostUpdate,
            (stage, cleanup)
                .chain()
                .after(super::update)
                .after(crate::authoring_assets::imported_scene::update)
                .after(bevy::camera::CameraUpdateSystems)
                .after(bevy::transform::TransformSystems::Propagate),
        );
    if let Some(render) = app.get_sub_app_mut(RenderApp) {
        render.add_systems(ExtractSchedule, observe_pipelines);
    }
}

fn report(message: impl Into<String>) {
    let message = message.into();
    STATUS.with(|slot| {
        if let Some(output) = slot.get() {
            output.set(message.clone());
        }
    });
    crate::authoring_vegetation::report(message);
}

fn select(id: Option<EntityId>) {
    SELECTED.with(|slot| slot.set(id));
    OUTPUT.with(|slot| {
        if let Some(output) = slot.get() {
            output.set(id);
        }
    });
}

pub(crate) fn cancel() {
    ARMED.with(|slot| slot.set(None));
    CLICK.with(|slot| slot.set(None));
    EPOCH.with(|slot| slot.set(slot.get().wrapping_add(1)));
}

pub(crate) fn is_armed() -> bool {
    ARMED.with(Cell::get).is_some()
}

fn arm(state: Option<AuthoringState>) {
    crate::authoring_brush_pointer::cancel();
    if !crate::authoring_viewport::can_preview_asset() {
        report("Sélection de plante indisponible pendant un aperçu ou le playtest.");
        return;
    }
    let Some(head) = state.and_then(|value| value.zone_head).filter(|head| {
        head.base
            .vegetation
            .as_ref()
            .is_some_and(|vegetation| vegetation.imported_source.is_some())
    }) else {
        report("Carte végétale importée absente.");
        return;
    };
    let Ok(hash) = head.content_hash() else {
        report("Révision de carte invalide.");
        return;
    };
    ARMED.with(|slot| slot.set(Some((hash, EPOCH.with(Cell::get)))));
    report("Cliquer une plante visible pour l'inspecter ; Échap annule.");
}

pub(crate) fn click(position: Vec2) {
    if !is_armed()
        && crate::authoring_viewport::can_preview_asset()
        && !crate::authoring_assets::placements::gizmo::active()
        && let Some(head) = population::current_head()
            .filter(|head| {
                head.base
                    .vegetation
                    .as_ref()
                    .is_some_and(|vegetation| vegetation.imported_source.is_some())
            })
            .and_then(|head| head.content_hash().ok())
    {
        ARMED.with(|slot| slot.set(Some((head, EPOCH.with(Cell::get)))));
    }
    if let Some((head, epoch)) = ARMED.with(Cell::take) {
        CLICK.with(|slot| slot.set(Some((head, epoch, position))));
    }
}

fn translation(fields: [&str; 3]) -> Result<[f64; 3], String> {
    let mut values = [0.0; 3];
    for (value, field) in values.iter_mut().zip(fields) {
        *value = field
            .trim()
            .parse::<f64>()
            .map_err(|_| "Décalage invalide")?;
    }
    Ok(values)
}

fn appearance(yaw: &str, scale: &str) -> Result<(Option<f64>, Option<f64>), String> {
    let yaw = yaw.trim().parse::<f64>().map_err(|_| "Rotation invalide")?;
    let scale = scale
        .trim()
        .parse::<f64>()
        .map_err(|_| "Échelle invalide")?;
    if !yaw.is_finite() || yaw.abs() > 360.0 {
        return Err("Rotation attendue entre -360 et 360 degrés".into());
    }
    if !scale.is_finite() || !(0.1..=10.0).contains(&scale) {
        return Err("Échelle attendue entre 0,1 et 10".into());
    }
    Ok((
        (yaw.to_bits() != 0.0f64.to_bits()).then_some(yaw),
        (scale.to_bits() != 1.0f64.to_bits()).then_some(scale),
    ))
}

fn validate_request(
    head: &ZoneAuthoringHead,
    request: ZoneRequest,
) -> Result<AuthoringRequest, String> {
    if let ZoneAction::Edit(corrections) = &request.action {
        let mut candidate = head.clone();
        candidate.corrections = (**corrections).clone();
        candidate.version = candidate.corrections.schema_version();
        let source = population::population(head)?;
        source.validate_plant_offsets(&candidate)?;
        source.validate_plant_placements(&candidate)?;
    }
    Ok(AuthoringRequest::PreviewZone { request })
}

#[cfg(test)]
fn offset_for_world_target(
    source_position_m: [f64; 3],
    current_offset_m: [f64; 3],
    target_xz_m: [f32; 2],
) -> Result<[f64; 3], String> {
    if target_xz_m.iter().any(|value| !value.is_finite()) {
        return Err("Point de terrain invalide".into());
    }
    Ok([
        f64::from(target_xz_m[0]) - source_position_m[0],
        current_offset_m[1],
        f64::from(target_xz_m[1]) - source_position_m[2],
    ])
}

fn prepare_catalogue_placement(
    state: Option<AuthoringState>,
    layer: &str,
    fields: [&str; 3],
    yaw: &str,
    scale: &str,
) -> Result<AuthoringRequest, String> {
    let head = state
        .and_then(|value| value.zone_head)
        .ok_or("Zone absente")?;
    if layer.is_empty() {
        return Err("Choisir d'abord une espèce du catalogue".into());
    }
    let [x, clearance, z] = translation(fields).map_err(|_| "Coordonnées invalides")?;
    let (yaw_degrees, scale_factor) = appearance(yaw, scale)?;
    let source = population::population(&head)?;
    let seed = serde_json::to_string(&(
        head.content_hash()?,
        layer,
        [x, clearance, z],
        yaw_degrees,
        scale_factor,
    ))
    .map_err(|error| error.to_string())?;
    let placement = source.plant_placement_at(
        &VegetationLayerId::new(layer),
        EntityId::deterministic(format!("authored-imported-plant/catalogue/{seed}")),
        [x, z],
        clearance,
        yaw_degrees,
        scale_factor,
    )?;
    let edit = ImportedPlantPlacementEdit::Insert { placement };
    let identity =
        serde_json::to_string(&(head.content_hash()?, &edit)).map_err(|e| e.to_string())?;
    let request = head
        .prepare_imported_plant_placement(edit, forge_core::CommandId::deterministic(identity))?;
    validate_request(&head, request)
}

fn arm_catalogue_placement(
    state: RwSignal<Option<AuthoringState>>,
    world_position: [RwSignal<String>; 2],
    layer: &str,
    clearance: &str,
    yaw: &str,
    scale: &str,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> Result<(), String> {
    if !crate::authoring_viewport::can_preview_asset() {
        return Err("Terminer l'aperçu ou le playtest avant de poser une plante".into());
    }
    if !crate::authoring_brush_pointer::ready_for_tool() {
        return Err("Terminer l'aperçu ou la commande en cours avant de poser une plante".into());
    }
    if layer.is_empty() {
        return Err("Choisir d'abord une espèce du catalogue".into());
    }
    let current = state.get_untracked().ok_or("Zone absente")?;
    let head = current.zone_head.ok_or("Zone absente")?;
    population::population(&head)?;
    let clearance_m = clearance
        .trim()
        .parse::<f64>()
        .map_err(|_| "Dégagement vertical invalide")?;
    if !clearance_m.is_finite() || clearance_m.abs() > 4096.0 {
        return Err("Dégagement vertical hors limites".into());
    }
    appearance(yaw, scale)?;
    let expected = head.content_hash()?;
    let layer = layer.to_owned();
    let clearance = clearance.to_owned();
    let yaw = yaw.to_owned();
    let scale = scale.to_owned();
    crate::authoring_brush_pointer::arm(
        2.0,
        false,
        Callback::new(move |picked: Result<Vec<[f32; 2]>, String>| {
            let request = (|| {
                let [x, z] = *picked?.first().ok_or("Point de terrain absent")?;
                let current = state.get_untracked();
                let head = current
                    .as_ref()
                    .and_then(|value| value.zone_head.as_ref())
                    .ok_or("Zone fermée pendant la pose")?;
                if head.content_hash()? != expected {
                    return Err("Carte modifiée pendant la pose : recommencer".into());
                }
                let x = f64::from(x).to_string();
                let z = f64::from(z).to_string();
                world_position[0].set(x.clone());
                world_position[1].set(z.clone());
                prepare_catalogue_placement(current, &layer, [&x, &clearance, &z], &yaw, &scale)
            })();
            crate::authoring_brush_pointer::cancel();
            on_command.run(request);
        }),
    );
    report("Cliquer le terrain pour examiner la pose de cette plante ; Échap annule.");
    Ok(())
}

#[component]
#[allow(
    clippy::too_many_lines,
    reason = "The Leptos plant selection view keeps its controls in one ownership scope"
)]
pub(crate) fn Controls(
    state: RwSignal<Option<AuthoringState>>,
    busy: RwSignal<bool>,
    on_command: Callback<Result<AuthoringRequest, String>>,
) -> impl IntoView {
    let selected = RwSignal::new(SELECTED.with(Cell::get));
    let status = RwSignal::new(String::new());
    let catalogue_layer = RwSignal::new(String::new());
    let world_x = RwSignal::new("0".to_owned());
    let world_z = RwSignal::new("0".to_owned());
    let clearance = RwSignal::new("0".to_owned());
    let catalogue_yaw = RwSignal::new("0".to_owned());
    let catalogue_scale = RwSignal::new("1".to_owned());
    OUTPUT.with(|slot| slot.set(Some(selected)));
    STATUS.with(|slot| slot.set(Some(status)));
    Effect::new(move |previous: Option<Option<ContentHash>>| {
        let current = state
            .get()
            .and_then(|value| value.zone_head)
            .and_then(|head| head.content_hash().ok());
        if previous.is_some_and(|previous| previous != current) {
            cancel();
            select(None);
            catalogue_layer.set(String::new());
        }
        current
    });
    on_cleanup(move || {
        cancel();
        OUTPUT.with(|slot| {
            if slot.get() == Some(selected) {
                slot.set(None);
            }
        });
        STATUS.with(|slot| {
            if slot.get() == Some(status) {
                slot.set(None);
            }
        });
    });
    view! {
        <details><summary>"Plantes importées"</summary>
            <p>"Cliquer une plante visible pour ouvrir l'inspecteur commun : déplacer, tourner, redimensionner, copier, verrouiller ou supprimer. Examiner la proposition, puis Appliquer et Sauvegarder."</p>
            <details><summary>"Placer une espèce du catalogue"</summary>
                <p>"Choisir une espèce importée, puis une position mondiale X/Z. La copie reprend la variation source la plus proche et suit le relief."</p>
                <label>"Espèce"<select prop:value=move || catalogue_layer.get() on:change=move |event| { catalogue_layer.set(event_target_value(&event)); crate::authoring_brush_pointer::settings_changed(); }>
                    <option value="">"Choisir une espèce…"</option>
                    {move || state.get().and_then(|value| value.zone_head).and_then(|head| {
                        let vegetation = head.base.vegetation?;
                        let imported = vegetation.imported_source.as_ref()?;
                        Some(imported.blocks.iter().map(|block| {
                            let id = block.layer_id.clone();
                            let name = vegetation.layers.iter().find(|layer| layer.id.as_str() == id)
                                .map_or(block.prototype_key.as_str(), |layer| layer.display_name.as_str());
                            let label = format!("{} · {} sources", name, block.count);
                            view! { <option value=id>{label}</option> }
                        }).collect_view())
                    })}
                </select></label>
                <label>"Position X (m)"<input type="number" step="0.1" prop:value=move || world_x.get() on:input=move |event| { world_x.set(event_target_value(&event)); crate::authoring_brush_pointer::settings_changed(); } /></label>
                <label>"Position Z (m)"<input type="number" step="0.1" prop:value=move || world_z.get() on:input=move |event| { world_z.set(event_target_value(&event)); crate::authoring_brush_pointer::settings_changed(); } /></label>
                <label>"Dégagement vertical (m)"<input type="number" step="0.1" prop:value=move || clearance.get() on:input=move |event| { clearance.set(event_target_value(&event)); crate::authoring_brush_pointer::settings_changed(); } /></label>
                <label>"Rotation Y (degrés)"<input type="number" min="-360" max="360" step="1" prop:value=move || catalogue_yaw.get() on:input=move |event| { catalogue_yaw.set(event_target_value(&event)); crate::authoring_brush_pointer::settings_changed(); } /></label>
                <label>"Échelle uniforme"<input type="number" min="0.1" max="10" step="0.1" prop:value=move || catalogue_scale.get() on:input=move |event| { catalogue_scale.set(event_target_value(&event)); crate::authoring_brush_pointer::settings_changed(); } /></label>
                <button disabled=move || busy.get() || catalogue_layer.get().is_empty() || state.get().is_none_or(|value| value.playtest_hash.is_some())
                    on:click=move |_| on_command.run(prepare_catalogue_placement(
                        state.get_untracked(), &catalogue_layer.get_untracked(),
                        [&world_x.get_untracked(), &clearance.get_untracked(), &world_z.get_untracked()],
                        &catalogue_yaw.get_untracked(), &catalogue_scale.get_untracked(),
                    ))>"Examiner la pose sur la carte"</button>
                <button disabled=move || busy.get() || catalogue_layer.get().is_empty() || state.get().is_none_or(|value| value.playtest_hash.is_some())
                    on:click=move |_| if let Err(error) = arm_catalogue_placement(
                        state, [world_x, world_z], &catalogue_layer.get_untracked(), &clearance.get_untracked(),
                        &catalogue_yaw.get_untracked(), &catalogue_scale.get_untracked(), on_command,
                    ) { on_command.run(Err(error)); }>"Placer par clic sur le terrain"</button>
            </details>
            <button on:click=move |_| arm(state.get_untracked())>"Sélectionner une plante dans la vue"</button>
            <output aria-live="polite">{move || status.get()}</output>
            <p>{move || selected.get().map_or_else(|| "Aucune plante sélectionnée".into(), |id| format!("Plante : {id}"))}</p>
            <p>"La sélection ouvre les outils communs dans le panneau Objets : position, rotation, échelle, copie, verrouillage et suppression."</p>
        </details>
    }
}

fn stage(world: &mut World) {
    if world.contains_resource::<Pending>() {
        return;
    }
    let Some((head, epoch, position)) = CLICK.with(Cell::take) else {
        return;
    };
    let result = (|| -> Result<Pending, String> {
        if !crate::authoring_viewport::can_preview_asset()
            || EPOCH.with(Cell::get) != epoch
            || !position.is_finite()
            || position.min_element() < 0.0
            || position.max_element() > 1.0
            || population::current_head().and_then(|value| value.content_hash().ok()) != Some(head)
        {
            return Err("Clic de plante périmé ou hors vue".into());
        }
        let generation = imported_buildings::published_generation();
        let snapshot = world.resource_scope(|world, state: Mut<State>| {
            if state.source != Some((head, generation)) || state.failed.is_some() {
                return Err("Projection végétale non synchronisée".into());
            }
            state
                .resident
                .as_ref()
                .ok_or("Plantes non projetées")?
                .picking_snapshot(world)
        })?;
        let (_, camera) = crate::authoring_imported_terrain::shadow_source(world)
            .ok_or("Terrain non synchronisé")?;
        let camera_view = world.get::<Camera>(camera).ok_or("Caméra absente")?;
        let transform = world
            .get::<GlobalTransform>(camera)
            .ok_or("Transformée caméra absente")?;
        let projection = world
            .get::<Projection>(camera)
            .ok_or("Perspective absente")?;
        let camera_key = CameraKey::new(camera_view, transform, projection)?;
        let view = View {
            width: camera_key.size.x,
            height: camera_key.size.y,
            transform: Transform::from_matrix(transform.to_matrix()),
            projection: projection.clone(),
        };
        let occluders = Prepared::from_world(world)?;
        let (terrain_count, village_count) = occluders.counts();
        let standard_count = occluders.standard_count();
        let mut pass = Pass::spawn(world, snapshot, view)?;
        if let Err(error) = pass.add_occluders(world, occluders) {
            pass.retire(world);
            return Err(error);
        }
        let mut pipelines = world.resource_mut::<Pipelines>();
        pipelines.ready_frames = 0;
        pipelines.failure = None;
        pipelines.expected = [
            true,
            terrain_count > 0,
            village_count > 0,
            standard_count > 0,
        ];
        Ok(Pending {
            pass,
            head,
            generation,
            epoch,
            position,
            camera,
            camera_key,
            started: world.resource::<FrameCount>().0,
            started_at_secs: world.resource::<Time<Real>>().elapsed_secs_f64(),
            reader: None,
            done: false,
        })
    })();
    match result {
        Ok(pending) => {
            world.insert_resource(pending);
            report("Lecture GPU de la plante en cours.");
        }
        Err(error) => report(format!("Sélection de plante refusée : {error}")),
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn request_readback(
    frame: Res<FrameCount>,
    time: Res<Time<Real>>,
    pipelines: Res<Pipelines>,
    mut pending: Option<ResMut<Pending>>,
    mut commands: Commands,
) {
    let Some(pending) = pending.as_deref_mut() else {
        return;
    };
    if pending.done {
        return;
    }
    if let Some(error) = &pipelines.failure {
        report(format!("Sélection GPU impossible : {error}"));
        pending.done = true;
        return;
    }
    if time.elapsed_secs_f64() - pending.started_at_secs > 120.0 {
        report("Sélection GPU expirée après 120 secondes de préparation des shaders.");
        pending.done = true;
        return;
    }
    if pending.reader.is_some()
        || frame.0.saturating_sub(pending.started) < 8
        || pipelines.ready_frames < 8
    {
        return;
    }
    pending.reader = Some(
        commands
            .spawn(Readback::texture(pending.pass.target.clone()))
            .observe(receive)
            .id(),
    );
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn receive(
    event: On<ReadbackComplete>,
    pending: Option<ResMut<Pending>>,
    state: Res<State>,
    cameras: Query<(&Camera, &GlobalTransform, &Projection)>,
) {
    let Some(mut pending) = pending else {
        return;
    };
    if pending.reader != Some(event.entity) || pending.done {
        return;
    }
    let result = (|| -> Result<Option<EntityId>, String> {
        let current_head = population::current_head()
            .ok_or("Carte fermée")?
            .content_hash()?;
        let (camera, transform, projection) = cameras
            .get(pending.camera)
            .map_err(|_| "Caméra remplacée")?;
        if !crate::authoring_viewport::can_preview_asset()
            || EPOCH.with(Cell::get) != pending.epoch
            || current_head != pending.head
            || state.source != Some((pending.head, pending.generation))
            || imported_buildings::published_generation() != pending.generation
            || CameraKey::new(camera, transform, projection)? != pending.camera_key
        {
            return Err("Réponse GPU périmée : carte, bibliothèque ou caméra modifiée".into());
        }
        let x = pixel_coordinate(pending.position.x, pending.camera_key.size.x);
        let y = pixel_coordinate(pending.position.y, pending.camera_key.size.y);
        let pixel = readback::rgba8_pixel(&event.data, pending.camera_key.size.to_array(), [x, y])?;
        if pixel[3] != 255 {
            return Err("Pixel de sélection non opaque".into());
        }
        pending
            .pass
            .snapshot
            .resolve(decode(pixel).unwrap_or(0), current_head)
    })();
    match result {
        Ok(id) => {
            if let Some(id) = id {
                crate::authoring_assets::placements::selection::choose(Some(id));
            }
            select(id);
            report(id.map_or_else(
                || "Aucune plante visible à ce point.".into(),
                |id| format!("Plante sélectionnée : {id}. Carte intacte."),
            ));
        }
        Err(error) => report(format!("Sélection de plante refusée : {error}")),
    }
    pending.done = true;
}

fn cleanup(world: &mut World) {
    if world
        .get_resource::<Pending>()
        .is_some_and(|pending| pending.done)
        && let Some(pending) = world.remove_resource::<Pending>()
    {
        if let Some(reader) = pending.reader {
            world.despawn(reader);
        }
        pending.pass.retire(world);
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render system parameters"
)]
fn observe_pipelines(mut main: ResMut<MainWorld>, pipelines: Res<PipelineCache>) {
    if !main.contains_resource::<Pending>() {
        return;
    }
    let Some(mut status) = main.get_resource_mut::<Pipelines>() else {
        return;
    };
    if status.failure.is_some() {
        return;
    }
    let shader_ids = [
        picking::shader_id(),
        occluders::shader_ids()[0],
        occluders::shader_ids()[1],
        occluders::shader_ids()[2],
    ];
    let mut seen = [false; 4];
    let mut ready = true;
    for pipeline in pipelines.pipelines() {
        let PipelineDescriptor::RenderPipelineDescriptor(descriptor) = &pipeline.descriptor else {
            continue;
        };
        let Some(fragment) = &descriptor.fragment else {
            continue;
        };
        let Some(index) = shader_ids.iter().position(|id| *id == fragment.shader.id()) else {
            continue;
        };
        if !status.expected[index] {
            continue;
        }
        seen[index] = true;
        match &pipeline.state {
            CachedPipelineState::Ok(_) => {}
            CachedPipelineState::Err(
                ShaderCacheError::ShaderNotLoaded(_)
                | ShaderCacheError::ShaderImportNotYetAvailable,
            ) => ready = false,
            CachedPipelineState::Err(error) => {
                status.failure = Some(format!("{error:?}"));
                ready = false;
            }
            _ => ready = false,
        }
    }
    status.ready_frames = if ready
        && status
            .expected
            .iter()
            .zip(seen)
            .all(|(expected, seen)| !expected || seen)
    {
        status.ready_frames.saturating_add(1)
    } else {
        0
    };
}

/// Inputs are finite normalized cursor positions and a bounded 1..=4096 target.
#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss,
    clippy::cast_precision_loss,
    reason = "validated normalized coordinates and bounded target dimensions"
)]
fn pixel_coordinate(position: f32, size: u32) -> usize {
    ((position * size as f32).floor() as usize).min(size as usize - 1)
}

#[cfg(test)]
mod tests {
    use super::offset_for_world_target;

    #[test]
    fn terrain_click_becomes_source_relative_offset_and_keeps_clearance() {
        assert_eq!(
            offset_for_world_target([10.0, 30.0, -20.0], [3.0, 1.25, -5.0], [18.0, 7.0]),
            Ok([8.0, 1.25, 27.0])
        );
    }

    #[test]
    fn non_finite_terrain_click_is_rejected() {
        assert!(offset_for_world_target([0.0; 3], [0.0; 3], [f32::NAN, 0.0]).is_err());
    }
}
