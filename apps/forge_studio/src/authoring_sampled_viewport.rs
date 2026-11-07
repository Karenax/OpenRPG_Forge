//! Canonical raster editing through the existing cell renderer, camera and authoring commands.
use bevy::{ecs as bevy_ecs, prelude::*, render::error_handler::RenderErrorHandler};
use forge_core::{ContentHash, EntityId};
use forge_engine::cell_streaming::{
    self, CellStreamingSet, CellStreamingStats, ResidentSampledTerrain, SampledCellStreamingPlugin,
};
use forge_project::TerrainDocument;
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::sync::Arc;

#[derive(Resource)]
pub(crate) struct CanonicalRasterViewport;

#[derive(Resource)]
struct ViewportState {
    target: ContentHash,
    pending: Option<TerrainDocument>,
    ready: bool,
    error: Option<String>,
    reported_error: Option<String>,
    head: Option<ContentHash>,
    building_count: usize,
    layer_error: Option<String>,
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Validated finite sampled terrain coordinates"
)]
pub(crate) fn launch(document: &TerrainDocument) -> Result<(), String> {
    let bytes = crate::authoring_raster::source_bytes(document)?;
    let plan = Arc::new(
        SampledTerrainPlan::from_document(document, &bytes).map_err(|error| error.to_string())?,
    );
    if document.material_layers.len() != 3 {
        return Err("Ce terrain nécessite les trois couches de matière du profil actuel.".into());
    }
    let policy = cell_streaming::sampled_policy(plan.raster().source())?;
    let source = plan.raster().source();
    let center = [
        source.origin_xz_m[0] + f64::from(document.extent_m.size_x_m) * 0.5,
        source.origin_xz_m[1] + f64::from(document.extent_m.size_z_m) * 0.5,
    ];
    let pivot = Vec3::new(
        center[0] as f32,
        plan.height_at(center[0], center[1])
            .map_err(|error| error.to_string())? as f32,
        center[1] as f32,
    );
    let distance =
        (document.extent_m.size_x_m.max(document.extent_m.size_z_m) / 512.0).clamp(0.125, 1.0);
    let camera = Transform::from_translation(pivot + Vec3::new(70.0, 60.0, 90.0) * distance)
        .looking_at(pivot, Vec3::Y);
    let cells = SampledCellStreamingPlugin::new(
        plan,
        policy,
        EntityId::deterministic(format!(
            "authoring-raster:{}:{}",
            document.project_id, document.area_id
        )),
        StandardMaterial {
            base_color: Color::WHITE,
            ..default()
        },
        center,
    )?
    .with_authored_material_layers()
    .with_terrain_hlod();
    let target = document.content_hash().map_err(|error| error.to_string())?;
    let document = document.clone();
    crate::authoring_viewport::sampled_ready(false);
    wasm_bindgen_futures::spawn_local(async move {
        let mut app = App::new();
        configure_app(&mut app, cells, target, camera);
        if let Err(error) = crate::authoring_module_viewport::install(&mut app, &document) {
            crate::authoring_viewport::imported_failure(&error);
            return;
        }
        if let Err(error) = crate::authoring_playtest::install(&mut app, None) {
            crate::authoring_viewport::imported_failure(&error);
            return;
        }
        app.run();
    });
    Ok(())
}

fn configure_app(
    app: &mut App,
    cells: SampledCellStreamingPlugin,
    target: ContentHash,
    camera: Transform,
) {
    crate::authoring_assets::preview::register(app);
    app.add_plugins(DefaultPlugins.set(WindowPlugin {
        primary_window: Some(Window {
            title: "Forge · terrain auteur".into(),
            canvas: Some("#forge-authoring-canvas".into()),
            fit_canvas_to_parent: true,
            prevent_default_event_handling: false,
            ..default()
        }),
        ..default()
    }));
    app.add_plugins(forge_engine::source_graph::SourceGraphPlugin);
    crate::authoring_assets::preview::register_rendering(app);
    app.insert_resource(RenderErrorHandler(|error, main, render| {
        crate::authoring_viewport::imported_failure(&error.description);
        (RenderErrorHandler::default().0)(error, main, render)
    }));
    forge_engine::sampled_terrain_physics::register(app);
    app.add_plugins((
        cells,
        forge_engine::TerrainLightingPlugin,
        forge_engine::CaptureReadinessPlugin,
        forge_engine::finite_perspective::FinitePerspectivePlugin,
    ));
    app.insert_resource(CanonicalRasterViewport)
        .insert_resource(ViewportState {
            target,
            pending: None,
            ready: false,
            error: None,
            reported_error: None,
            head: None,
            building_count: 0,
            layer_error: None,
        })
        .init_resource::<forge_engine::TerrainBrushCursor>()
        .add_systems(
            Update,
            (project_source, crate::authoring_camera::navigate)
                .chain()
                .before(CellStreamingSet::Update),
        )
        .add_systems(
            PostUpdate,
            (
                observe_ready,
                crate::authoring_brush_pointer::input,
                clear_unready_cursor,
                forge_engine::update_terrain_brush_cursor.run_if(picking_ready),
                crate::authoring_brush_pointer::picked.run_if(picking_ready),
                crate::authoring_preview::draw,
            )
                .chain()
                .after(crate::authoring_module_viewport::publish)
                .after(bevy::transform::TransformSystems::Propagate),
        );
    app.world_mut().spawn((
        Camera3d::default(),
        camera,
        Projection::Perspective(PerspectiveProjection {
            fov: 62.0_f32.to_radians(),
            near: 0.06,
            far: 20_000.0,
            ..default()
        }),
    ));
    crate::authoring_render::install(app);
    crate::authoring_lighting::install(app);
    crate::authoring_building_projection::install(app);
    crate::authoring_path_projection::install(app);
    crate::authoring_path_guides::install(app);
    crate::authoring_scene::install(app);
}

pub(crate) fn project_source(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() {
        return;
    }
    let latest = crate::authoring_viewport::latest_imported_update();
    world.resource_scope(|world, mut projection: Mut<ViewportState>| {
        if let Some(document) = latest {
            match document.content_hash() {
                Ok(hash) => {
                    projection.target = hash;
                    projection.pending = Some(document);
                    projection.ready = false;
                    projection.error = None;
                    projection.reported_error = None;
                    crate::authoring_viewport::sampled_ready(false);
                    crate::authoring_loading::begin("Actualisation du relief…");
                    forge_engine::observe_render_readiness(world, false);
                }
                Err(error) => {
                    projection.error = Some(error.to_string());
                    return;
                }
            }
        }
        let Some(document) = projection.pending.as_ref() else {
            return;
        };
        let result = (|| {
            let bytes = crate::authoring_raster::source_bytes(document)?;
            let plan = Arc::new(
                SampledTerrainPlan::from_document(document, &bytes)
                    .map_err(|error| error.to_string())?,
            );
            let expected = world
                .resource::<ResidentSampledTerrain>()
                .plan()
                .document_hash();
            cell_streaming::request_source_update(world, expected, plan)
        })();
        match result {
            Ok(()) => projection.pending = None,
            Err(_) if crate::authoring_raster::loading() => (),
            Err(error) => {
                projection.pending = None;
                projection.error = Some(error);
            }
        }
    });
}

fn refresh_scene(world: &mut World, state: &mut ViewportState) {
    let hash = crate::authoring_assets::placements::accepted_hash();
    if state.head == hash {
        return;
    }
    state.head = hash;
    state.ready = false;
    state.layer_error = None;
    state.building_count = 0;
    state.reported_error = None;
    crate::authoring_viewport::sampled_ready(false);
    forge_engine::observe_render_readiness(world, false);
    if let Some(head) = crate::authoring_assets::placements::accepted_head() {
        state.building_count = head.corrections.buildings.as_ref().map_or(0, Vec::len);
        state.layer_error = match head.effective() {
            Ok((_, vegetation))
                if vegetation.as_ref().is_some_and(|vegetation| !crate::authoring_module_viewport::supports_vegetation(vegetation))
                    || head.corrections.paths.as_ref().is_some_and(|paths| !paths.is_empty())
                    || head.corrections.rivers.as_ref().is_some_and(|rivers| !rivers.is_empty()) =>
            {
                Some("La végétation, les chemins ou les rivières de cette carte ne peuvent pas encore être affichés sur ce relief. Les données sont conservées.".into())
            }
            Ok(_) => None,
            Err(error) => Some(error),
        };
    }
}

fn layer_readiness(world: &World, state: &ViewportState) -> Result<bool, String> {
    if let Some(error) = &state.layer_error {
        return Err(error.clone());
    }
    if crate::authoring_module_viewport::installed() && !crate::authoring_module_viewport::ready()?
    {
        return Ok(false);
    }
    // Disposable previews do not replace the accepted head or suspend their own renderer.
    if state.ready {
        return Ok(true);
    }
    Ok(crate::authoring_assets::placements::proposal_ready()?
        && state.head.is_none_or(|hash| {
            crate::authoring_building_projection::ready(world, hash, state.building_count)
        }))
}

fn observe_ready(world: &mut World) {
    world.resource_scope(|world, mut projection: Mut<ViewportState>| {
        refresh_scene(world, &mut projection);
        let source_ready = projection.pending.is_none()
            && projection.error.is_none()
            && cell_streaming::source_is_synchronized(world, projection.target);
        let layers = layer_readiness(world, &projection);
        let scene_ready = source_ready && layers.as_ref().copied().unwrap_or(false);
        let render = forge_engine::observe_render_readiness(world, scene_ready);
        let error = projection
            .error
            .clone()
            .or_else(|| world.resource::<CellStreamingStats>().last_error.clone())
            .or_else(|| layers.err())
            .or_else(|| {
                render
                    .as_ref()
                    .filter(|state| state.failed > 0)
                    .map(|_| "Un shader du terrain n’a pas pu être préparé.".into())
            });
        if let Some(error) = error {
            projection.ready = false;
            crate::authoring_viewport::sampled_ready(false);
            crate::authoring_loading::finish();
            if projection.reported_error.as_ref() != Some(&error) {
                crate::authoring_viewport::report(format!(
                    "Affichage du relief incomplet : {error}"
                ));
                projection.reported_error = Some(error);
            }
            return;
        }
        let ready = scene_ready && render.is_some_and(|state| state.ready_to_sample);
        if ready && !projection.ready {
            projection.ready = true;
            crate::authoring_viewport::sampled_ready(true);
            projection.reported_error = None;
            crate::authoring_loading::finish();
            crate::authoring_viewport::report(
                "Terrain affiché. Vous pouvez déplacer la vue et préparer vos retouches.".into(),
            );
        } else if !source_ready && projection.ready {
            projection.ready = false;
            crate::authoring_viewport::sampled_ready(false);
        }
    });
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameter")]
fn picking_ready(projection: Res<ViewportState>) -> bool {
    projection.ready
}

fn clear_unready_cursor(world: &mut World) {
    if !world.resource::<ViewportState>().ready {
        let mut cursor = world.resource_mut::<forge_engine::TerrainBrushCursor>();
        cursor.hit = None;
        cursor.pending_positions.clear();
        cursor.path_hits.clear();
    }
}
