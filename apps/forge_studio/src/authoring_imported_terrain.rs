//! Imported terrain projection in the existing Studio canvas.
//! This is a terrain authoring stage; ocean, authored objects and playtest remain separate.
use crate::authoring_assets::imported_buildings;
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::{
    sampled_terrain_physics::{self, PreparedSampledTerrainPhysics, SampledTerrainPhysicsState},
    tidewater_hill_shadow as hill,
    tidewater_object_shadow::graph as shadows,
    tidewater_scene_environment::ResidentEnvironment,
    tidewater_terrain::{
        TerrainSettings,
        lod::ViewGeometry,
        scene::{self, Bindings, PreparedTerrain, ResidentTerrain},
    },
};
use forge_project::TerrainDocument;
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::sync::{Arc, atomic::Ordering};

#[derive(Resource)]
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
    geometry: Option<ViewGeometry>,
}

pub(crate) fn launch(document: TerrainDocument) -> Result<(), String> {
    crate::authoring_raster::source_bytes(&document)?;
    let package = imported_buildings::terrain_package()
        .ok_or("Charger la bibliothèque importée avec son terrain et son environnement.")?;
    if package.file("hill-shadow.wgsl").is_none() || !imported_buildings::has_environment() {
        return Err(
            "Le terrain importé requiert le paquet terrain v2 et son environnement.".into(),
        );
    }
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
        sampled_terrain_physics::register(&mut app);
        crate::authoring_imported_shadows::install(&mut app);
        crate::authoring_imported_plants::install(&mut app);
        app.add_plugins(hill::HillShadowPlugin);
        let result = scene::register(&mut app)
            .and_then(|()| shadows::register(&mut app))
            .and_then(|()| crate::authoring_assets::imported_environment::install(&mut app));
        if let Err(error) = result {
            crate::authoring_viewport::imported_failure(&error);
            return;
        }
        let camera = app
            .world_mut()
            .spawn((
                Camera3d::default(),
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
            geometry: None,
        })
        .init_resource::<forge_engine::TerrainBrushCursor>()
        .add_systems(Update, crate::authoring_camera::navigate)
        .add_systems(
            PostUpdate,
            (
                project,
                crate::authoring_brush_pointer::input,
                clear_unready_cursor,
                forge_engine::update_terrain_brush_cursor.run_if(picking_ready),
                crate::authoring_brush_pointer::picked,
            )
                .chain()
                .after(bevy::transform::TransformSystems::Propagate),
        );
        crate::authoring_viewport::report("Préparation du terrain et des bâtiments importés. Eau et végétation encore en cours d’intégration.".into());
        app.run();
    });
    Ok(())
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
        let result = project_ready(world, &mut state);
        if let Err(error) = result {
            state.failed = true;
            crate::authoring_viewport::imported_failure(&error);
        }
    });
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
        let geometry = state
            .geometry
            .as_ref()
            .ok_or("Bornes du terrain absentes")?
            .mesh(view)?;
        let resident = world.resource::<ResidentTerrain>().clone();
        if let Some(entity) = resident.entity {
            if let Some((mesh, _)) = geometry {
                let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
                world
                    .entity_mut(entity)
                    .insert((Mesh3d(mesh), Visibility::Inherited));
            } else {
                world.entity_mut(entity).insert(Visibility::Hidden);
            }
        } else if geometry.is_some() {
            // A prior view outside the terrain had no material/entity to reuse.
            state.pending = true;
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
    Ok(())
}

fn rebuild(
    world: &mut World,
    state: &mut ProjectionState,
    view: forge_world::cdlod::CdlodView,
) -> Result<(), String> {
    if !imported_buildings::has_environment() {
        return Err("La bibliothèque courante ne fournit pas d'environnement".into());
    }
    let bytes = crate::authoring_raster::source_bytes(&state.document)?;
    let plan = Arc::new(
        SampledTerrainPlan::from_document(&state.document, &bytes).map_err(|e| e.to_string())?,
    );
    let package = imported_buildings::terrain_package().ok_or("Paquet terrain absent")?;
    let terrain = PreparedTerrain::new(&plan, &bytes, &package, view)?;
    let physics = PreparedSampledTerrainPhysics::new(plan.clone())?;
    let geometry = ViewGeometry::new(&plan)?;
    let environment = world.resource::<ResidentEnvironment>();
    let sun = environment.scene_lighting.sun_direction;
    let hill = hill::PreparedInput::new(
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
    )?;
    let environment_map = environment.environment.clone();
    let cloud_view = environment.cloud_view;
    let cloud_shadow = environment.clouds.images[2].clone();
    let environment_id = environment.environment.id();
    let hill = hill.stage(world)?;
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
    let placeholder = world.resource_mut::<Assets<Image>>().add(Image::default());
    world.insert_resource(crate::authoring_imported_shadows::Receiver(shadow.clone()));
    terrain.stage(
        world,
        Bindings {
            environment: environment_map,
            hill_shadow: hill.output,
            object_shadow: shadow.image,
            object_settings: shadow.settings,
            clouds: cloud_view,
            cloud_shadow: Some(cloud_shadow),
            shore_state: placeholder.clone(),
            shore_lace: placeholder,
            shore_params: [UVec4::ZERO; 3],
            settings: TerrainSettings {
                wind: Vec4::new(1.0, 0.0, 6.0, 2.0),
                sun,
                domain: Vec4::new(-1024.0, 2048.0, 2048.0, 0.58),
                flags: Vec4::ZERO,
            },
        },
    )?;
    physics.stage(world);
    state.acknowledged = false;
    if let Some(previous) = state.sky.take() {
        world.despawn(previous);
    }
    state.sky = Some(spawn_sky(world));
    state.pending = false;
    state.geometry = Some(geometry);
    state.library = imported_buildings::published_generation();
    state.environment = Some(environment_id);
    Ok(())
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
fn camera_view(
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
