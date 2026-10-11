//! Imported terrain projection in the existing Studio canvas.
//! The imported edit and playtest views share terrain, ocean and collision projection.
mod solar;
use crate::authoring_assets::imported_buildings;
use bevy::ecs::system::SystemParam;
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::{
    sampled_terrain_physics::{self, PreparedSampledTerrainPhysics, SampledTerrainPhysicsState},
    tidewater_hill_shadow as hill,
    tidewater_object_shadow::graph as shadows,
    tidewater_ocean, tidewater_ocean_geometry, tidewater_ocean_optics,
    tidewater_scene_environment::ResidentEnvironment,
    tidewater_shore_sim,
    tidewater_terrain::{
        TerrainSettings,
        lod::ViewGeometry,
        scene::{self, Bindings, PreparedTerrain, ResidentTerrain},
    },
};
use forge_project::{TerrainDocument, authoring::playtest::PlaytestIdentity};
use forge_world::sampled_terrain::SampledTerrainPlan;
use forge_world::{WorldSketchCamera, WorldSketchCameraRole};
use std::sync::{Arc, atomic::Ordering};

#[derive(Resource)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Source lifecycle flags and independent renderer switches are tracked separately."
)]
struct ProjectionState {
    document: TerrainDocument,
    camera: Entity,
    view: Vec<u32>,
    pending: bool,
    acknowledged: bool,
    failed: bool,
    library: u64,
    environment: Option<AssetId<Image>>,
    sky: Option<Entity>,
    flat_water: Option<Entity>,
    clear_cloud_shadow: Option<Handle<Image>>,
    geometry: Option<ViewGeometry>,
    geometry_nodes: Option<Vec<forge_world::cdlod::CdlodNode>>,
    debug_ocean_geometry: bool,
    ocean_optics: bool,
    shore_sim: bool,
}

#[derive(Resource, Default)]
pub(crate) struct LiveCloudTime {
    previous: Option<f64>,
    failed: bool,
    environment: Option<AssetId<Image>>,
    pub(crate) submitted: u64,
    pub(crate) solar_readback: Option<(f64, Entity)>,
}

#[derive(Resource)]
struct GraphicsExposureHandles {
    curve: Handle<bevy::post_process::auto_exposure::AutoExposureCompensationCurve>,
    mask: Handle<Image>,
}

#[allow(
    clippy::too_many_lines,
    reason = "Scene initialization keeps plugin installation, source validation, and collision setup ordered."
)]
pub(crate) fn launch(
    document: TerrainDocument,
    playtest: Option<PlaytestIdentity>,
) -> Result<(), String> {
    let fast_editing = playtest.is_none();
    let query = web_sys::window()
        .and_then(|window| window.location().search().ok())
        .unwrap_or_default();
    let parameters: Vec<_> = query.trim_start_matches('?').split('&').collect();
    let has_parameter = |value: &str| parameters.contains(&value);
    let pinned_village_view =
        playtest.is_some() && has_parameter("forge_tidewater_reference_village=1");
    let complete_water = imported_buildings::ocean_package().is_some()
        && imported_buildings::water_surface_package().is_some()
        && imported_buildings::shore_sim_package().is_some();
    // Editing keeps the source geometry and collisions while expensive water
    // simulation and optical passes are reserved for the playtest renderer.
    let ocean_optics = (complete_water || has_parameter("forge_water_optics=1"))
        && !has_parameter("forge_water_optics=0")
        && !fast_editing;
    let shore_sim = ocean_optics
        && (complete_water || has_parameter("forge_shore_sim=1"))
        && !has_parameter("forge_shore_sim=0");
    let debug_ocean_geometry =
        !fast_editing && (ocean_optics || has_parameter("forge_water_geometry=1"));
    if debug_ocean_geometry && imported_buildings::water_surface_package().is_none() {
        return Err("Le diagnostic d’eau requiert le paquet de surface vérifié.".into());
    }
    if shore_sim && imported_buildings::shore_sim_package().is_none() {
        return Err("Le ressac Tidewater requiert le paquet ShoreSim vérifié.".into());
    }
    let bytes = crate::authoring_raster::source_bytes(&document)?;
    let plan =
        SampledTerrainPlan::from_document(&document, &bytes).map_err(|error| error.to_string())?;
    let package = imported_buildings::terrain_package()
        .ok_or("Charger la bibliothèque importée avec son terrain et son environnement.")?;
    if (!fast_editing && package.file("hill-shadow.wgsl").is_none())
        || !imported_buildings::has_environment()
    {
        return Err(
            "Le terrain importé requiert le paquet terrain v2 et son environnement.".into(),
        );
    }
    let prepared_ocean = if fast_editing {
        None
    } else {
        imported_buildings::ocean_package()
            .as_deref()
            .map(tidewater_ocean::PreparedOcean::new)
            .transpose()?
    };
    let player = playtest
        .map(|identity| {
            if document.content_hash().map_err(|error| error.to_string())? != identity.terrain_hash
            {
                return Err("Snapshot de Playtest importé incohérent.".into());
            }
            let mut profile = forge_world::VisualValleyPlayerControllerRecipe
                .build()
                .map_err(|error| error.to_string())?;
            if ocean_optics {
                // Same source capsule and camera limits as the native Tidewater walk.
                profile.controller.capsule_radius_m = forge_engine::tidewater_locomotion::RADIUS;
                profile.controller.capsule_half_segment_m =
                    forge_engine::tidewater_locomotion::WALK_HEIGHT * 0.5
                        - profile.controller.capsule_radius_m;
                profile.controller.walk_speed_mps = 3.0;
                profile.controller.sprint_multiplier = 6.2 / 3.0;
                profile.controller.jump_speed_mps = 4.6;
                profile.controller.max_step_height_m = 0.4;
                profile.camera.eye_height_m = 1.62;
                profile.camera.look_sensitivity_degrees_per_unit = 0.0022_f32.to_degrees();
                profile.camera.pitch_min_degrees = -1.5_f32.to_degrees();
                profile.camera.pitch_max_degrees = 1.5_f32.to_degrees();
                profile.validate().map_err(|error| error.to_string())?;
            }
            let start = crate::authoring_playtest::start();
            if let Some(start) = start {
                start.validate(&document)?;
            }
            // This import path is Tidewater-specific. Match the native default
            // boardwalk start until the zone author supplies a start point.
            let (x, z) = start.map_or((53.6, -77.0), |start| (start.x_m, start.z_m));
            #[allow(
                clippy::cast_possible_truncation,
                reason = "validated sampled terrain elevation"
            )]
            let ground = plan
                .height_at(f64::from(x), f64::from(z))
                .map_err(|error| error.to_string())? as f32;
            let half_height =
                profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m;
            let spawn = Vec3::new(x, ground + half_height + 0.02, z);
            if !spawn.is_finite() {
                return Err("Départ de Playtest importé invalide.".into());
            }
            let target = if let Some(degrees) = start.and_then(|start| start.facing_degrees) {
                let heading = degrees.rem_euclid(360.0).to_radians();
                spawn + Vec3::new(-heading.sin(), 0.0, -heading.cos())
            } else {
                spawn + Vec3::NEG_Z
            };
            Ok::<_, String>((profile, spawn, target))
        })
        .transpose()?;
    wasm_bindgen_futures::spawn_local(async move {
        let mut app = App::new();
        crate::authoring_assets::preview::register(&mut app);
        app.add_plugins(DefaultPlugins.set(WindowPlugin {
            primary_window: Some(Window {
                title: "Forge · terrain importé".into(),
                canvas: Some("#forge-authoring-canvas".into()),
                fit_canvas_to_parent: true,
                prevent_default_event_handling: false,
                ..default()
            }),
            ..default()
        }));
        if playtest.is_none() {
            crate::authoring_render::install(&mut app);
            use bevy::render::error_handler::RenderErrorHandler;
            app.insert_resource(RenderErrorHandler(|error, main_world, render_world| {
                crate::authoring_viewport::imported_failure(&error.description);
                // Retain Bevy's AppExit::error and StopRendering policy.
                (RenderErrorHandler::default().0)(error, main_world, render_world)
            }));
        }
        app.add_plugins(forge_engine::source_graph::SourceGraphPlugin);
        if playtest.is_some() {
            app.add_plugins((
                bevy::post_process::auto_exposure::AutoExposurePlugin,
                forge_engine::tidewater_gtao::TidewaterGtaoPlugin,
                forge_engine::tidewater_film_grain::TidewaterFilmGrainPlugin,
                forge_engine::tidewater_lens_flare::TidewaterLensFlarePlugin,
                forge_engine::tidewater_air_haze::TidewaterAirHazePlugin,
            ));
        }
        sampled_terrain_physics::register(&mut app);
        // Authored rooms use their explicit world pose and the same generic
        // building projection as procedural zones. Imported plants stay intact;
        // clearing their footprint remains an explicit authoring operation.
        crate::authoring_building_projection::install(&mut app);
        if !fast_editing {
            app.add_plugins(hill::HillShadowPlugin);
        }
        let result = crate::authoring_imported_shadows::install(&mut app, !fast_editing)
            .and_then(|()| crate::authoring_imported_plants::install(&mut app))
            .and_then(|()| scene::register(&mut app))
            .and_then(|()| shadows::register(&mut app))
            .and_then(|()| crate::authoring_assets::imported_environment::install(&mut app));
        if let Err(error) = result {
            crate::authoring_viewport::imported_failure(&error);
            return;
        }
        if let Some((profile, spawn, target)) = player {
            app.add_plugins(
                forge_engine::PlayerControllerPlugin::new(profile, spawn)
                    .with_initial_camera_target(target)
                    .with_initial_camera_mode(forge_engine::PlayerCameraMode::FirstPerson)
                    .with_initial_camera_pitch_degrees(if ocean_optics { 0.0 } else { 12.0 }),
            )
            .insert_resource(forge_engine::DiagnosticsOverlaySettings { visible: false })
            .add_systems(
                PreUpdate,
                crate::authoring_input::update_focus.before(bevy::input::InputSystems),
            )
            .add_systems(Last, crate::authoring_input::publish);
            if let Err(error) = crate::authoring_playtest::install(&mut app, playtest) {
                crate::authoring_viewport::imported_failure(&error);
                return;
            }
            app.add_systems(PostStartup, configure_playtest_graphics);
            app.add_systems(Update, apply_playtest_graphics);
        }
        if pinned_village_view {
            let source_rotation = Quat::from_xyzw(-0.099_833_414, 0.0, 0.0, 0.995_004_2);
            let position = Vec3::new(42.0, 14.0, -90.0);
            let target = position + source_rotation * Vec3::NEG_Z;
            let camera = WorldSketchCamera {
                id: "tidewater-village-source-pinned".into(),
                role: WorldSketchCameraRole::ReferenceMatch,
                source_image_id: None,
                position_m: position.to_array(),
                target_m: target.to_array(),
                vertical_fov_degrees: 62.0,
                near_m: 0.06,
                far_m: 60_000.0,
            };
            let fixed_view = match forge_engine::FixedProofViewPlugin::camera_only(camera) {
                Ok(view) => view,
                Err(error) => {
                    crate::authoring_viewport::imported_failure(&error.to_string());
                    return;
                }
            };
            app.add_plugins(fixed_view);
            app.add_systems(Last, publish_pinned_village_status);
        }
        let camera = app
            .world_mut()
            .spawn((
                Camera3d {
                    // The optical depth copy must be supported before the
                    // editor allocates its first view depth texture.
                    depth_texture_usages: if ocean_optics {
                        (bevy::render::render_resource::TextureUsages::RENDER_ATTACHMENT
                            | bevy::render::render_resource::TextureUsages::TEXTURE_BINDING
                            | bevy::render::render_resource::TextureUsages::COPY_SRC)
                            .into()
                    } else {
                        Camera3d::default().depth_texture_usages
                    },
                    ..default()
                },
                Camera {
                    is_active: playtest.is_none(),
                    ..default()
                },
                Msaa::Off,
                Transform::from_xyz(42.0, 14.0, -90.0).with_rotation(Quat::from_xyzw(
                    0.0,
                    0.995_004_2,
                    0.099_833_414,
                    0.0,
                )),
                Projection::Perspective(PerspectiveProjection {
                    fov: 62.0_f32.to_radians(),
                    near: 0.06,
                    far: 60_000.0,
                    ..default()
                }),
            ))
            .id();
        if playtest.is_none() && ocean_optics {
            app.world_mut().entity_mut(camera).insert((
                bevy::camera::Hdr,
                bevy::core_pipeline::tonemapping::Tonemapping::AcesFitted,
                forge_engine::tidewater_underwater::TidewaterUnderwaterCamera,
            ));
        }
        app.insert_resource(ProjectionState {
            document,
            camera,
            view: Vec::new(),
            pending: true,
            acknowledged: false,
            failed: false,
            library: 0,
            environment: None,
            sky: None,
            flat_water: None,
            clear_cloud_shadow: None,
            geometry: None,
            geometry_nodes: None,
            debug_ocean_geometry,
            ocean_optics,
            shore_sim,
        })
        .init_resource::<LiveCloudTime>()
        .init_resource::<forge_engine::TerrainBrushCursor>()
        .add_systems(Update, crate::authoring_camera::navigate);
        if let Some(ocean) = prepared_ocean {
            let staged = tidewater_ocean::register(&mut app)
                .and_then(|()| ocean.stage(app.world_mut()).map(|_| ()));
            if let Err(error) = staged {
                crate::authoring_viewport::imported_failure(&format!(
                    "Calcul océan Tidewater indisponible : {error}"
                ));
                return;
            }
        }
        if debug_ocean_geometry && let Err(error) = tidewater_ocean_geometry::register(&mut app) {
            crate::authoring_viewport::imported_failure(&format!(
                "Géométrie océan Tidewater indisponible : {error}"
            ));
            return;
        }
        if ocean_optics
            && let Err(error) = tidewater_ocean_optics::register(&mut app)
                .and_then(|()| crate::authoring_immersion::install(&mut app))
        {
            crate::authoring_viewport::imported_failure(&format!(
                "Optique océan Tidewater indisponible : {error}"
            ));
            return;
        }
        if shore_sim && let Err(error) = tidewater_shore_sim::register(&mut app) {
            crate::authoring_viewport::imported_failure(&format!(
                "Ressac Tidewater indisponible : {error}"
            ));
            return;
        }
        if playtest.is_some() && ocean_optics {
            let queries = app
                .world()
                .resource::<forge_engine::tidewater_surface_queries::Targets>()
                .queries
                .clone();
            match forge_engine::TidewaterPlayerPlugin::new(queries, Arc::new(plan)) {
                Ok(player) => {
                    app.add_plugins(player);
                }
                Err(error) => {
                    crate::authoring_viewport::imported_failure(&format!(
                        "Nage Tidewater indisponible : {error}"
                    ));
                    return;
                }
            }
        }
        if playtest.is_some() {
            crate::authoring_temporal::install(&mut app, ocean_optics);
            app.add_systems(
                PostUpdate,
                (adopt_player_camera, project, update_live_clouds)
                    .chain()
                    .after(bevy::transform::TransformSystems::Propagate)
                    .after(forge_engine::tidewater_underwater::TidewaterUnderwaterUpdateSet),
            );
            app.add_systems(
                PostUpdate,
                crate::authoring_temporal::update
                    .after(adopt_player_camera)
                    .before(forge_engine::tidewater_temporal::FrameSystems::Prepare)
                    .before(forge_engine::tidewater_refraction::Synchronize),
            );
            crate::authoring_viewport::report(
                "Préparation du terrain importé et de la simulation à pied.".into(),
            );
        } else {
            app.add_systems(
                PostUpdate,
                (
                    project,
                    crate::authoring_brush_pointer::input,
                    clear_unready_cursor,
                    forge_engine::update_terrain_brush_cursor.run_if(picking_ready),
                    crate::authoring_brush_pointer::picked,
                )
                    .chain()
                    .after(bevy::transform::TransformSystems::Propagate)
                    .after(forge_engine::tidewater_underwater::TidewaterUnderwaterUpdateSet),
            );
            crate::authoring_viewport::report(
                "Préparation du terrain, des bâtiments et de l’environnement importés.".into(),
            );
        }
        app.run();
    });
    Ok(())
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy owns the system resource parameter wrapper"
)]
fn publish_pinned_village_status(status: Res<forge_engine::FixedProofViewStatus>) {
    if status.stable_main_frames < 30 {
        return;
    }
    if let Some(canvas) = web_sys::window()
        .and_then(|window| window.document())
        .and_then(|document| document.get_element_by_id("forge-authoring-canvas"))
    {
        let _ = canvas.set_attribute("data-tidewater-reference-village", "stable");
        let _ = canvas.set_attribute(
            "data-tidewater-reference-time-ns",
            &status.virtual_elapsed_ns.to_string(),
        );
    }
}

/// Start imported playtests with the available Tidewater HDR and post passes.
/// The settings control presentation only; authored terrain is unchanged.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy owns the system resource parameter wrappers"
)]
fn configure_playtest_graphics(
    mut commands: Commands,
    camera: Query<Entity, With<forge_engine::ForgePlayerCamera>>,
    mut cameras_3d: Query<&mut Camera3d>,
    state: Res<ProjectionState>,
    mut curves: ResMut<Assets<bevy::post_process::auto_exposure::AutoExposureCompensationCurve>>,
    mut images: ResMut<Assets<Image>>,
    prepasses: Query<Has<bevy::core_pipeline::prepass::MotionVectorPrepass>>,
) {
    use bevy::{
        anti_alias::{contrast_adaptive_sharpening::ContrastAdaptiveSharpening, fxaa::Fxaa},
        camera::Hdr,
        core_pipeline::prepass::MotionVectorPrepass,
        core_pipeline::tonemapping::Tonemapping,
        post_process::{
            auto_exposure::AutoExposure, bloom::Bloom, effect_stack::Vignette,
            motion_blur::MotionBlur,
        },
        render::view::ColorGrading,
    };
    let Ok(camera) = camera.single() else {
        return;
    };
    // GTAO samples the opaque main depth; WebGPU requires the sampling usage
    // before Bevy allocates the view texture, including the very first frame.
    commands.entity(camera).insert(Msaa::Off);
    if state.ocean_optics {
        commands
            .entity(camera)
            .insert(forge_engine::tidewater_underwater::TidewaterUnderwaterCamera);
    }
    if let Ok(mut camera_3d) = cameras_3d.get_mut(camera) {
        camera_3d.depth_texture_usages.0 |=
            bevy::render::render_resource::TextureUsages::TEXTURE_BINDING.bits();
        if state.ocean_optics {
            camera_3d.depth_texture_usages.0 |=
                bevy::render::render_resource::TextureUsages::COPY_SRC.bits();
        }
    }
    let curve = match forge_engine::tidewater_exposure::source_compensation_curve() {
        Ok(curve) => curves.add(curve),
        Err(error) => {
            crate::authoring_viewport::report(format!(
                "Exposition Tidewater indisponible : {error}"
            ));
            return;
        }
    };
    let mask = images.add(forge_engine::tidewater_exposure::source_metering_mask());
    commands.insert_resource(GraphicsExposureHandles {
        curve: curve.clone(),
        mask: mask.clone(),
    });
    let profile = crate::authoring_input::graphics();
    let motion = prepasses.get(camera).unwrap_or(false);
    commands.entity(camera).insert(GraphicsPrepassOwnership {
        motion: profile.motion_blur_degrees > 0.0 && !motion,
    });
    let mut grading = ColorGrading::default();
    grading.global.exposure = profile.exposure_ev;
    grading.global.post_saturation = profile.saturation;
    grading.midtones.contrast = profile.contrast;
    commands.entity(camera).insert((
        Hdr,
        Tonemapping::AcesFitted,
        grading,
        ContrastAdaptiveSharpening {
            enabled: true,
            sharpening_strength: profile.sharpen,
            denoise: false,
        },
        Vignette {
            intensity: profile.vignette,
            ..default()
        },
        Bloom {
            intensity: profile.bloom,
            ..Bloom::NATURAL
        },
        Fxaa::default(),
        forge_engine::tidewater_gtao::TidewaterGtao::new(profile.ambient_occlusion),
        forge_engine::tidewater_lens_flare::TidewaterLensFlare::new(profile.lens_flare),
        (
            forge_engine::tidewater_air_haze::TidewaterAirHaze::default(),
            forge_engine::atmospheric_haze::AirHazeSettings::new(profile.air_haze),
        ),
        forge_engine::tidewater_film_grain::TidewaterFilmGrain::new(profile.film_grain),
        AutoExposure {
            filter: 0.0..=1.0,
            speed_brighten: 1.6,
            speed_darken: 1.1,
            compensation_curve: curve,
            metering_mask: mask,
            ..default()
        },
    ));
    if profile.motion_blur_degrees > 0.0 {
        commands.entity(camera).insert((
            MotionBlur {
                shutter_angle: profile.motion_blur_degrees / 360.0,
                samples: 3,
            },
            MotionVectorPrepass,
        ));
    }
}

/// Apply only presentation changes made in the Studio panel. Zone data and
/// terrain projection remain untouched while the player is walking.
#[derive(Component)]
struct GraphicsPrepassOwnership {
    motion: bool,
}

#[derive(SystemParam)]
struct PlaytestGraphicsParams<'w, 's> {
    commands: Commands<'w, 's>,
    camera: Query<'w, 's, Entity, With<forge_engine::ForgePlayerCamera>>,
    exposure: Option<Res<'w, GraphicsExposureHandles>>,
    grading: Query<'w, 's, &'static mut bevy::render::view::ColorGrading>,
    sharpening: Query<
        'w,
        's,
        &'static mut bevy::anti_alias::contrast_adaptive_sharpening::ContrastAdaptiveSharpening,
    >,
    bloom: Query<'w, 's, &'static mut bevy::post_process::bloom::Bloom>,
    vignette: Query<'w, 's, &'static mut bevy::post_process::effect_stack::Vignette>,
    ambient_occlusion: Query<'w, 's, &'static mut forge_engine::tidewater_gtao::TidewaterGtao>,
    lens_flare: Query<'w, 's, &'static mut forge_engine::tidewater_lens_flare::TidewaterLensFlare>,
    air_haze: Query<'w, 's, &'static mut forge_engine::atmospheric_haze::AirHazeSettings>,
    village_materials: ResMut<'w, Assets<forge_engine::tidewater_material::VillageMaterial>>,
    applied: Local<'s, Option<(Entity, crate::authoring_input::PlaytestGraphics, bool)>>,
    temporal: Query<'w, 's, (), With<bevy::anti_alias::taa::TemporalAntiAliasing>>,
    prepasses: Query<
        'w,
        's,
        (
            Has<bevy::core_pipeline::prepass::MotionVectorPrepass>,
            Option<&'static crate::authoring_temporal::OwnedTemporalPrepasses>,
            &'static mut GraphicsPrepassOwnership,
        ),
    >,
}

#[allow(
    clippy::too_many_lines,
    reason = "The callback updates camera effects together with ownership of their shared prepasses."
)]
fn apply_playtest_graphics(params: PlaytestGraphicsParams) {
    let PlaytestGraphicsParams {
        mut commands,
        camera,
        exposure,
        mut grading,
        mut sharpening,
        mut bloom,
        mut vignette,
        mut ambient_occlusion,
        mut lens_flare,
        mut air_haze,
        mut village_materials,
        mut applied,
        temporal,
        mut prepasses,
    } = params;
    let profile = crate::authoring_input::graphics();
    let Ok(camera) = camera.single() else {
        return;
    };
    let temporal_active = temporal.contains(camera);
    if applied.is_some_and(|previous| previous == (camera, profile, temporal_active)) {
        return;
    }
    let (Ok(mut grade), Ok(mut sharp), Ok(mut bloom), Ok(mut vignette)) = (
        grading.get_mut(camera),
        sharpening.get_mut(camera),
        bloom.get_mut(camera),
        vignette.get_mut(camera),
    ) else {
        return;
    };
    grade.global.exposure = profile.exposure_ev;
    grade.global.post_saturation = profile.saturation;
    grade.midtones.contrast = profile.contrast;
    sharp.sharpening_strength = profile.sharpen;
    bloom.intensity = profile.bloom;
    vignette.intensity = profile.vignette;
    if let Ok(mut ao) = ambient_occlusion.get_mut(camera) {
        ao.set_strength(profile.ambient_occlusion);
    }
    if let Ok(mut flare) = lens_flare.get_mut(camera) {
        flare.set_strength(profile.lens_flare);
    }
    if let Ok(mut haze) = air_haze.get_mut(camera) {
        haze.set_density(profile.air_haze);
    }
    for (_, material) in village_materials.iter_mut() {
        if material.ground_bounce.is_some() {
            material.bounce_settings.x = profile.bounce_light;
        }
    }
    if profile.film_grain > 0.0 {
        commands.entity(camera).insert(
            forge_engine::tidewater_film_grain::TidewaterFilmGrain::new(profile.film_grain),
        );
    } else {
        commands
            .entity(camera)
            .remove::<forge_engine::tidewater_film_grain::TidewaterFilmGrain>();
    }
    if profile.auto_exposure {
        if let Some(exposure) = exposure {
            commands
                .entity(camera)
                .insert(bevy::post_process::auto_exposure::AutoExposure {
                    filter: 0.0..=1.0,
                    speed_brighten: 1.6,
                    speed_darken: 1.1,
                    compensation_curve: exposure.curve.clone(),
                    metering_mask: exposure.mask.clone(),
                    ..default()
                });
        }
    } else {
        commands
            .entity(camera)
            .remove::<bevy::post_process::auto_exposure::AutoExposure>();
    }
    if profile.fxaa {
        commands
            .entity(camera)
            .insert(bevy::anti_alias::fxaa::Fxaa::default());
    } else {
        commands
            .entity(camera)
            .remove::<bevy::anti_alias::fxaa::Fxaa>();
    }
    if profile.motion_blur_degrees > 0.0 {
        if let Ok((motion, taa, mut ownership)) = prepasses.get_mut(camera) {
            ownership.motion |= !motion || taa.is_some_and(|taa| taa.motion);
        }
        commands.entity(camera).insert((
            bevy::post_process::motion_blur::MotionBlur {
                shutter_angle: profile.motion_blur_degrees / 360.0,
                samples: 3,
            },
            bevy::core_pipeline::prepass::MotionVectorPrepass,
        ));
    } else {
        commands
            .entity(camera)
            .remove::<bevy::post_process::motion_blur::MotionBlur>();
    }
    if profile.motion_blur_degrees <= 0.0
        && !temporal_active
        && let Ok((_, _, mut ownership)) = prepasses.get_mut(camera)
        && ownership.motion
    {
        commands
            .entity(camera)
            .remove::<bevy::core_pipeline::prepass::MotionVectorPrepass>();
        ownership.motion = false;
    }
    *applied = Some((camera, profile, temporal_active));
}

fn adopt_player_camera(
    mut state: ResMut<ProjectionState>,
    player: Query<Entity, With<forge_engine::ForgePlayerCamera>>,
    mut cameras: Query<&mut Camera>,
) {
    let Ok(player) = player.single() else {
        return;
    };
    if state.camera == player {
        return;
    }
    if let Ok(mut editor) = cameras.get_mut(state.camera) {
        editor.is_active = false;
    }
    state.camera = player;
    state.pending = true;
    state.acknowledged = false;
}

fn picking_ready(world: &World) -> bool {
    crate::authoring_viewport::can_project_sources()
        && world
            .get_resource::<ProjectionState>()
            .is_some_and(|s| s.acknowledged && !s.pending && !s.failed)
}

fn clear_unready_cursor(world: &mut World) {
    if !picking_ready(world) {
        let mut cursor = world.resource_mut::<forge_engine::TerrainBrushCursor>();
        cursor.hit = None;
        cursor.path_hits = vec![None; cursor.pending_positions.len().min(1024)];
        cursor.pending_positions.clear();
    }
}

pub(crate) fn project(world: &mut World) {
    if !crate::authoring_viewport::can_project_sources() {
        return;
    }
    world.resource_scope(|world, mut state: Mut<ProjectionState>| {
        if let Some(document) = crate::authoring_viewport::latest_imported_update() {
            state.document = document;
            state.pending = true;
            state.acknowledged = false;
            state.failed = false;
        }
        if state.failed {
            return;
        }
        let Some(environment) = world.get_resource::<ResidentEnvironment>() else {
            return;
        };
        if state.library != imported_buildings::published_generation()
            || state.environment != Some(environment.environment.id())
        {
            state.pending = true;
            state.acknowledged = false;
        }
        if !environment.clouds.ready.load(Ordering::Acquire) {
            return;
        }
        if let Err(error) = crate::authoring_raster::source_bytes(&state.document) {
            if !crate::authoring_raster::loading() {
                state.failed = true;
                crate::authoring_viewport::imported_failure(&error);
            }
            return;
        }
        let result = if world.contains_resource::<crate::authoring_render::FastEditing>() {
            stage_unshadowed_clouds(world, &mut state)
                .and_then(|()| project_ready(world, &mut state))
        } else {
            project_ready(world, &mut state)
        };
        if let Err(error) = result {
            state.failed = true;
            crate::authoring_viewport::imported_failure(&error);
        }
    });
}

#[allow(
    clippy::too_many_lines,
    reason = "The callback advances the source cloud producer and publishes its frame diagnostics."
)]
fn update_live_clouds(world: &mut World) {
    use forge_engine::{cloud_controller, tidewater_clouds};
    if crate::authoring_immersion::held(world) {
        return;
    }
    let environment = world
        .get_resource::<ResidentEnvironment>()
        .map(|environment| environment.environment.id());
    if world.resource::<LiveCloudTime>().environment != environment {
        let mut time = world.resource_mut::<LiveCloudTime>();
        *time = LiveCloudTime {
            environment,
            ..default()
        };
    }
    if !world
        .get_resource::<ProjectionState>()
        .is_some_and(|state| state.acknowledged && !state.pending && !state.failed)
        || !tidewater_clouds::live::can_submit(world)
        || (forge_engine::tidewater_environment::live::is_staged(world)
            && !forge_engine::tidewater_environment::live::can_submit(world))
        || world.resource::<LiveCloudTime>().failed
    {
        return;
    }
    let camera_entity = world.resource::<ProjectionState>().camera;
    let (Some(transform), Some(Projection::Perspective(projection))) = (
        world.get::<GlobalTransform>(camera_entity),
        world.get::<Projection>(camera_entity),
    ) else {
        return;
    };
    let transform = transform.compute_transform();
    let camera = cloud_controller::Camera {
        position: transform.translation.to_array().map(f64::from),
        right: transform.right().to_array().map(f64::from),
        up: transform.up().to_array().map(f64::from),
        forward: transform.forward().to_array().map(f64::from),
        fov_degrees: f64::from(projection.fov).to_degrees(),
        aspect: f64::from(projection.aspect_ratio),
        zoom: 1.0,
    };
    let now = world.resource::<Time>().elapsed_secs_f64();
    let dt = world
        .resource::<LiveCloudTime>()
        .previous
        .map_or(1.0 / 60.0, |previous| now - previous);
    let result = world.resource_scope(|world, mut environment: Mut<ResidentEnvironment>| {
        let mut input = environment.cloud_continuation.input;
        input.camera = camera;
        input.underwater = world
            .get_resource::<forge_engine::tidewater_underwater::TidewaterUnderwaterObservation>()
            .is_some_and(|observation| observation.camera_underwater());
        input.dt = dt;
        let solar = solar::advance(world, &mut environment, dt, camera.position[1])?;
        if let Some(frame) = solar {
            input.light_direction = frame.key.to_array();
        }
        if !forge_engine::tidewater_material::scene_buffer::ownership::gpu_owned(
            world,
            &forge_engine::tidewater_material::scene_buffer::HANDLE,
        ) {
            forge_engine::tidewater_material::scene_buffer::publish(
                world,
                &environment.scene_lighting,
            )?;
        }
        let output = environment
            .cloud_continuation
            .controller
            .update(input)
            .map_err(str::to_owned)?;
        if let Some(frame) = solar {
            tidewater_clouds::live::submit_solar(world, &output, frame)?;
        } else {
            // Keep the acquired seed until the first current-generation GPU
            // irradiance event. Encoded commands alone are not a readback.
            tidewater_clouds::live::submit(world, &output)?;
        }
        if forge_engine::tidewater_environment::live::is_staged(world) {
            let submitted = tidewater_clouds::live::submitted_frame(world)?;
            let sky = world
                .resource::<Assets<forge_engine::tidewater_sky::SkyMaterial>>()
                .iter()
                .find(|(_, sky)| sky.transmittance == environment.atmosphere.images[0])
                .map(|(_, sky)| [sky.settings.moon_disk, sky.settings.stars_time_night])
                .ok_or("Ciel de cette génération absent")?;
            let atmosphere = world
                .resource::<forge_engine::tidewater_atmosphere::Targets>()
                .params;
            forge_engine::tidewater_environment::live::submit(
                world,
                forge_engine::tidewater_environment::live::FrameInput {
                    dt,
                    sun: atmosphere[3].truncate().as_dvec3().to_array(),
                    force: false,
                    global: submitted.global,
                    atmosphere,
                    clouds: submitted.clouds,
                    sky,
                    cloud_step: submitted.step,
                    cloud_view: submitted.view,
                },
            )?;
        }
        environment.cloud_continuation.input = input;
        environment.cloud_continuation.last = output.clone();
        environment.cloud_view = output.public.map(Vec4::from_array);
        Ok::<_, String>((
            output,
            environment.cloud_view,
            solar.map(|frame| frame.hours),
        ))
    });
    let (output, params, solar_hours) = match result {
        Ok(value) => value,
        Err(error) => {
            world.resource_mut::<LiveCloudTime>().failed = true;
            crate::authoring_viewport::report(format!(
                "Nuages Tidewater mobiles suspendus : {error}"
            ));
            return;
        }
    };
    let solar_readback =
        solar_hours.zip(forge_engine::tidewater_atmosphere::readback::last_completed_reader(world));
    let mut time = world.resource_mut::<LiveCloudTime>();
    time.previous = Some(now);
    time.submitted += 1;
    time.solar_readback = solar_readback;
    let view =
        world.resource::<tidewater_clouds::Targets>().images[5 + output.view_index * 2].clone();
    for (_, material) in world
        .resource_mut::<Assets<forge_engine::tidewater_sky::SkyMaterial>>()
        .iter_mut()
    {
        material.clouds = params;
        if output.trace_view {
            material.cloud_view = Some(view.clone());
        }
    }
    refresh_cloud_receivers::<forge_engine::tidewater_plants::PlantMaterial>(
        world,
        params,
        |material| &material.clouds,
        |material, value| material.clouds = value,
    );
    refresh_cloud_receivers::<forge_engine::tidewater_material::VillageMaterial>(
        world,
        params,
        |material| &material.clouds,
        |material, value| material.clouds = value,
    );
    refresh_cloud_receivers::<forge_engine::tidewater_terrain::TerrainMaterial>(
        world,
        params,
        |material| &material.clouds,
        |material, value| material.clouds = value,
    );
    refresh_cloud_receivers::<forge_engine::tidewater_terrain::lod::LodMaterial>(
        world,
        params,
        |material| &material.base.clouds,
        |material, value| material.base.clouds = value,
    );
}

/// `Assets::iter_mut` emits Modified events even when the written value is unchanged.
/// Cloud textures animate independently; only a changed receiver pose needs material extraction.
fn refresh_cloud_receivers<M: Asset>(
    world: &mut World,
    params: [Vec4; 5],
    read: fn(&M) -> &[Vec4; 5],
    write: fn(&mut M, [Vec4; 5]),
) {
    let changed: Vec<_> = world
        .resource::<Assets<M>>()
        .iter()
        .filter_map(|(id, material)| (read(material) != &params).then_some(id))
        .collect();
    if changed.is_empty() {
        return;
    }
    let mut materials = world.resource_mut::<Assets<M>>();
    for id in changed {
        if let Some(mut material) = materials.get_mut(id) {
            write(&mut material, params);
        }
    }
}

fn project_ready(world: &mut World, state: &mut ProjectionState) -> Result<(), String> {
    let transform = *world
        .get::<Transform>(state.camera)
        .ok_or("Caméra absente")?;
    let Some(Projection::Perspective(projection)) = world.get::<Projection>(state.camera) else {
        return Err("Perspective absente".into());
    };
    let key: Vec<_> = transform
        .to_matrix()
        .to_cols_array()
        .into_iter()
        .chain([
            projection.fov,
            projection.aspect_ratio,
            projection.near,
            projection.far,
        ])
        .map(f32::to_bits)
        .collect();
    let view = camera_view(&transform, projection)?;
    if state.pending {
        rebuild(world, state, view)?;
        state.view = key;
    } else if state.view != key {
        let nodes = state
            .geometry
            .as_ref()
            .ok_or("Bornes du terrain absentes")?
            .nodes(view)?;
        // Preserve the exact source order, including distance sorting. Only a
        // different selection needs packing, extraction and a fresh GPU mesh.
        if !state
            .geometry_nodes
            .as_deref()
            .is_some_and(|previous| forge_world::cdlod::same_node_geometry(previous, &nodes))
        {
            let resident = world.resource::<ResidentTerrain>().clone();
            if let Some(entity) = resident.entity {
                if let Some((mesh, _)) = ViewGeometry::mesh_from_nodes(&nodes) {
                    let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
                    world
                        .entity_mut(entity)
                        .insert((Mesh3d(mesh), Visibility::Inherited));
                } else {
                    world.entity_mut(entity).insert(Visibility::Hidden);
                }
            } else if !nodes.is_empty() {
                // A prior view outside the terrain had no material/entity to reuse.
                state.pending = true;
            }
            state.geometry_nodes = Some(nodes);
        }
        if state.debug_ocean_geometry {
            tidewater_ocean_geometry::update_view(world, view)?;
        }
        state.view = key;
    }
    if !state.acknowledged && !state.pending {
        let hash = state.document.content_hash().map_err(|e| e.to_string())?;
        let physics = world.resource::<SampledTerrainPhysicsState>();
        if physics.is_synchronized(world, hash) {
            state.acknowledged = true;
            crate::authoring_viewport::report(format!(
                "Terrain importé {hash} : collisions synchronisées. Rendu GPU et édition complète encore à qualifier."
            ));
        }
    }
    if state.ocean_optics {
        tidewater_ocean_optics::update(world, state.camera)?;
    }
    Ok(())
}

#[allow(
    clippy::too_many_lines,
    reason = "GPU geometry replacement retains ordered staging, readiness, and error handling."
)]
fn rebuild(
    world: &mut World,
    state: &mut ProjectionState,
    view: forge_world::cdlod::CdlodView,
) -> Result<(), String> {
    if !imported_buildings::has_environment() {
        return Err("La bibliothèque courante ne fournit pas d'environnement".into());
    }
    let fast_editing = world.contains_resource::<crate::authoring_render::FastEditing>();
    let bytes = crate::authoring_raster::source_bytes(&state.document)?;
    let plan = Arc::new(
        SampledTerrainPlan::from_document(&state.document, &bytes).map_err(|e| e.to_string())?,
    );
    let package = imported_buildings::terrain_package().ok_or("Paquet terrain absent")?;
    let terrain = PreparedTerrain::new(&plan, &bytes, &package, view)?;
    let ocean_geometry = if state.debug_ocean_geometry {
        let water =
            imported_buildings::water_surface_package().ok_or("Paquet de surface d’eau absent")?;
        Some(tidewater_ocean_geometry::PreparedGeometry::new(
            &water,
            &state.document,
            &bytes,
        )?)
    } else {
        None
    };
    let shore_sim = if state.shore_sim {
        let shore =
            imported_buildings::shore_sim_package().ok_or("Paquet ShoreSim absent de la scène")?;
        let surface =
            imported_buildings::water_surface_package().ok_or("Paquet optique absent du ressac")?;
        Some(tidewater_shore_sim::PreparedShoreSim::new(
            &shore,
            &surface,
            &state.document,
            &plan,
            &bytes,
        )?)
    } else {
        None
    };
    let ocean_optics = if state.ocean_optics {
        let water =
            imported_buildings::water_surface_package().ok_or("Paquet optique d’eau absent")?;
        Some(tidewater_ocean_optics::PreparedOptics::new(
            &water,
            &state.document,
            &plan,
            shore_sim
                .as_ref()
                .map(tidewater_shore_sim::PreparedShoreSim::shore_field),
        )?)
    } else {
        None
    };
    let physics = PreparedSampledTerrainPhysics::new(plan.clone())?;
    let geometry = ViewGeometry::new(&plan)?;
    let environment = world.resource::<ResidentEnvironment>();
    let sun = environment.scene_lighting.sun_direction;
    let hill = if fast_editing {
        None
    } else {
        Some(hill::PreparedInput::new(
            std::str::from_utf8(
                package
                    .file("hill-shadow.wgsl")
                    .ok_or("Shader relief absent")?,
            )
            .map_err(|e| e.to_string())?
            .into(),
            hill::mip_chain(&plan.cell_center_heights())?,
            hill::Settings {
                origin: -1024.0,
                size: 2048.0,
                sun_bake: sun.truncate(),
            },
        )?)
    };
    let environment_map = environment.environment.clone();
    let cloud_view = environment.cloud_view;
    let cloud_shadow = environment.clouds.images[2].clone();
    let environment_id = environment.environment.id();
    let hill = match hill {
        Some(prepared) => prepared.stage(world)?,
        None => stage_unshadowed_hill(world),
    };
    let shadow = shadows::stage(
        world,
        shadows::Input {
            settings: default(),
            draws: Vec::new(),
            matrices: Vec::new(),
            terrain: None,
            plants: None,
            eyes: Vec::new(),
        },
    )?;
    let shore_targets = shore_sim
        .map(|prepared| prepared.stage(world))
        .transpose()?;
    let placeholder = world.resource_mut::<Assets<Image>>().add(Image::default());
    let shore_state = shore_targets
        .as_ref()
        .map_or_else(|| placeholder.clone(), |targets| targets.state.clone());
    let shore_lace = shore_targets
        .as_ref()
        .map_or_else(|| placeholder.clone(), |targets| targets.lace.clone());
    let shore_params = shore_targets
        .as_ref()
        .map_or([UVec4::ZERO; 3], |targets| targets.params);
    world.insert_resource(crate::authoring_imported_shadows::Receiver(shadow.clone()));
    let caustics = world
        .get_resource::<forge_engine::tidewater_caustics::CausticMaterialInputs>()
        .cloned();
    terrain.stage(
        world,
        Bindings {
            environment: environment_map,
            hill_shadow: hill.output,
            object_shadow: shadow.image,
            object_settings: shadow.settings,
            clouds: cloud_view,
            cloud_shadow: Some(cloud_shadow),
            shore_state,
            shore_lace,
            shore_params,
            caustics,
            settings: TerrainSettings {
                wind: Vec4::new(1.0, 0.0, 6.0, 2.0),
                sun,
                domain: Vec4::new(-1024.0, 2048.0, 2048.0, 0.58),
                flags: Vec4::ZERO,
            },
        },
    )?;
    if let Some(ocean_geometry) = ocean_geometry {
        ocean_geometry.stage(world, view)?;
    }
    if let Some(ocean_optics) = ocean_optics {
        forge_engine::tidewater_ocean_depth::resize(world, state.camera)?;
        ocean_optics.stage(world)?;
    }
    physics.stage(world);
    state.acknowledged = false;
    if let Some(previous) = state.sky.take() {
        world.despawn(previous);
    }
    if fast_editing {
        if state
            .flat_water
            .is_none_or(|entity| world.get_entity(entity).is_err())
        {
            state.flat_water = Some(spawn_flat_water(world));
        }
    } else {
        if let Some(previous) = state.flat_water.take() {
            world.despawn(previous);
        }
        state.sky = Some(spawn_sky(world));
    }
    state.pending = false;
    state.geometry_nodes = Some(geometry.nodes(view)?);
    state.geometry = Some(geometry);
    state.library = imported_buildings::published_generation();
    state.environment = Some(environment_id);
    Ok(())
}

/// Derived editor texture keeps every terrain/building/plant binding available.
/// Two texels per axis satisfy the shaders' four manual bilinear texture loads;
/// binary16 (-65504, 0, 0, 1) puts the occluder below the valid terrain domain.
fn stage_unshadowed_hill(world: &mut World) -> hill::Images {
    use bevy::asset::RenderAssetUsages;
    use bevy::render::render_resource::{Extent3d, TextureDimension, TextureFormat};
    if let Some(images) = world.get_resource::<hill::Images>() {
        return images.clone();
    }
    let size = Extent3d {
        width: 2,
        height: 2,
        depth_or_array_layers: 1,
    };
    let input = Image::new_fill(
        size,
        TextureDimension::D2,
        &[0, 0],
        TextureFormat::R16Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    let image = Image::new_fill(
        size,
        TextureDimension::D2,
        &[0xff, 0xfb, 0, 0, 0, 0, 0, 0x3c],
        TextureFormat::Rgba16Float,
        RenderAssetUsages::RENDER_WORLD,
    );
    let mut assets = world.resource_mut::<Assets<Image>>();
    let images = hill::Images {
        input: assets.add(input),
        output: assets.add(image),
        ready: Arc::new(std::sync::atomic::AtomicBool::new(true)),
    };
    world.insert_resource(images.clone());
    world.insert_resource(hill::Ready(true));
    images
}

/// A clear, same-format cloud-shadow binding retains every material's readiness.
/// The acquired producer is left intact and never advanced by the edit renderer.
fn stage_unshadowed_clouds(world: &mut World, state: &mut ProjectionState) -> Result<(), String> {
    use bevy::asset::RenderAssetUsages;
    use bevy::render::render_resource::{Extent3d, TextureDimension, TextureFormat};
    let current = world
        .resource::<ResidentEnvironment>()
        .clouds
        .images
        .get(2)
        .ok_or("L'environnement ne fournit pas de texture d'ombre des nuages.")?
        .id();
    if state
        .clear_cloud_shadow
        .as_ref()
        .is_some_and(|image| image.id() == current)
    {
        return Ok(());
    }
    let image = state.clear_cloud_shadow.get_or_insert_with(|| {
        let image = Image::new_fill(
            Extent3d {
                width: 256,
                height: 256,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            &1.0_f32.to_le_bytes(),
            TextureFormat::R32Float,
            RenderAssetUsages::RENDER_WORLD,
        );
        world.resource_mut::<Assets<Image>>().add(image)
    });
    world.resource_mut::<ResidentEnvironment>().clouds.images[2] = image.clone();
    Ok(())
}

/// Transient two-triangle editor water, at the source ocean's sea-level zero.
/// It has no placement identity, collider, animation, or persisted document entry.
fn spawn_flat_water(world: &mut World) -> Entity {
    let mesh = world
        .resource_mut::<Assets<Mesh>>()
        .add(Plane3d::default().mesh().size(120_000.0, 120_000.0));
    let material = world
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial {
            base_color: Color::srgb(0.12, 0.32, 0.48),
            unlit: true,
            alpha_mode: AlphaMode::Opaque,
            ..default()
        });
    world
        .spawn((
            Mesh3d(mesh),
            MeshMaterial3d(material),
            Transform::from_xyz(0.0, 0.0, 0.0),
            bevy::light::NotShadowCaster,
        ))
        .id()
}

fn spawn_sky(world: &mut World) -> Entity {
    use forge_engine::tidewater_sky::{Settings, SkyMaterial};
    let environment = world.resource::<ResidentEnvironment>();
    let atmosphere = &environment.atmosphere;
    let sun = atmosphere.params[3].truncate();
    let moon = Vec3::new(-sun.x, sun.y.abs() * 0.8 + 0.25, -sun.z).normalize();
    let material = SkyMaterial {
        atmosphere: atmosphere.params,
        settings: Settings {
            moon_disk: moon.extend(1.0),
            stars_time_night: Vec4::new(0.0, 2.0, 0.0, 0.0),
        },
        transmittance: atmosphere.images[0].clone(),
        sky: atmosphere.images[2].clone(),
        clouds: environment.cloud_view,
        panorama: Some(environment.clouds.images[9].clone()),
        cloud_view: Some(environment.clouds.images[7].clone()),
    };
    let material = world.resource_mut::<Assets<SkyMaterial>>().add(material);
    let mesh = world
        .resource_mut::<Assets<Mesh>>()
        .add(Rectangle::new(2.0, 2.0));
    world
        .spawn((
            Mesh3d(mesh),
            MeshMaterial3d(material),
            Transform::default(),
            bevy::camera::visibility::NoFrustumCulling,
            bevy::light::NotShadowCaster,
        ))
        .id()
}

// SPDX-License-Identifier: MIT
// View adaptation from Tidewater, copyright 2026 DRG Software Solutions LLC.
// Upstream 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
pub(crate) fn camera_view(
    transform: &Transform,
    projection: &PerspectiveProjection,
) -> Result<forge_world::cdlod::CdlodView, String> {
    use bevy::math::{DMat4, DQuat, DVec3, DVec4};
    let near = f64::from(projection.near);
    let far = f64::from(projection.far);
    let aspect = f64::from(projection.aspect_ratio);
    if !(near > 0.0 && far > near && far.is_finite() && aspect > 0.0 && aspect.is_finite()) {
        return Err("Perspective importée invalide".into());
    }
    let y = 1.0 / (f64::from(projection.fov) * 0.5).tan();
    let clip = DMat4::from_cols(
        DVec4::new(y / aspect, 0.0, 0.0, 0.0),
        DVec4::new(0.0, y, 0.0, 0.0),
        DVec4::new(0.0, 0.0, near / (far - near), -1.0),
        DVec4::new(0.0, 0.0, near * far / (far - near), 0.0),
    );
    let position = DVec3::from_array(transform.translation.to_array().map(f64::from));
    let world = DMat4::from_rotation_translation(
        DQuat::from_array(transform.rotation.to_array().map(f64::from)).normalize(),
        position,
    );
    let rows = (clip * world.inverse())
        .transpose()
        .to_cols_array_2d()
        .map(DVec4::from_array);
    Ok(forge_world::cdlod::CdlodView {
        position: position.to_array(),
        planes: [
            rows[3] + rows[0],
            rows[3] - rows[0],
            rows[3] + rows[1],
            rows[3] - rows[1],
            rows[3] - rows[2],
            rows[2],
        ]
        .map(|p| (p / p.truncate().length()).to_array()),
    })
}

/// Only expose the terrain generation already installed and synchronized.
pub(crate) fn shadow_source(world: &World) -> Option<(&TerrainDocument, Entity)> {
    let state = world.get_resource::<ProjectionState>()?;
    (state.acknowledged && !state.pending && !state.failed)
        .then_some((&state.document, state.camera))
}

/// The imported terrain and its Rapier tiles must belong to the same playtest snapshot.
pub(crate) fn ready(world: &World, expected: forge_core::ContentHash) -> bool {
    let Some(state) = world.get_resource::<ProjectionState>() else {
        return false;
    };
    state.acknowledged
        && !state.pending
        && !state.failed
        && world
            .get::<forge_engine::ForgePlayerCamera>(state.camera)
            .is_some()
        && state
            .document
            .content_hash()
            .is_ok_and(|hash| hash == expected)
        && world
            .get_resource::<SampledTerrainPhysicsState>()
            .is_some_and(|physics| physics.is_synchronized(world, expected))
}
