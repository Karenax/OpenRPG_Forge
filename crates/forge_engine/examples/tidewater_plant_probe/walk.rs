//! Interactive development walk using the Forge player and canonical collisions.
use super::{Probe, vegetation_scene::ResidentProof};
use bevy::{
    prelude::*,
    render::render_resource::TextureUsages,
    window::{CursorGrabMode, CursorOptions},
};
use forge_engine::{
    ForgePlayerCamera, PlayerControllerPlugin, PlayerInputFocus, PlayerSimulationReady,
    sampled_terrain_physics::{self, PreparedSampledTerrainPhysics, SampledTerrainPhysicsState},
    tidewater_terrain::{lod::ViewGeometry, scene::ResidentTerrain},
};
use std::sync::Arc;
#[path = "graphics.rs"]
mod graphics;
#[path = "ladder_smoke.rs"]
mod ladder_smoke;
#[path = "walk_smoke.rs"]
mod smoke;
#[path = "swim_smoke.rs"]
mod swim_smoke;

const PIER_LADDER: forge_engine::TidewaterLadder =
    forge_engine::TidewaterLadder::new(Vec2::new(62.32, 35.1), 1.95, Vec3::new(61.12, 2.3, 35.1));
pub(super) const WATER_TIME_SCALE: f64 = 1.5;

/// Use the playable camera's full grading and exposure chain for static parity captures.
pub(super) fn install_static_graphics(app: &mut App) -> Result<(), Box<dyn std::error::Error>> {
    app.insert_resource(PlayerInputFocus(false));
    graphics::install(app)
}

#[derive(Resource)]
struct Walk {
    geometry: ViewGeometry,
    previous: Option<Transform>,
    terrain_nodes: Vec<forge_world::cdlod::CdlodNode>,
    ocean_nodes: Vec<forge_world::cdlod::CdlodNode>,
    ocean_updates: usize,
}

#[derive(Component)]
struct LadderPrompt;

fn walk_spawn(smoke: bool, ladder_test: bool, swim_test: bool) -> Vec2 {
    if ladder_test {
        Vec2::new(63.8, 35.1)
    } else if swim_test {
        Vec2::new(40.0, -50.0)
    } else if smoke {
        Vec2::new(42.0, -90.0)
    } else {
        // Tidewater WorldLayout.js WORLD.start, at the boardwalk leading to the pier.
        Vec2::new(53.6, -77.0)
    }
}

// Locomotion, terrain, water and smoke plugins must be staged in this order so
// the probe observes the same collision and camera pipeline as runtime.
#[allow(clippy::too_many_lines)]
pub(super) fn install(app: &mut App) -> Result<(), Box<dyn std::error::Error>> {
    let probe = app.world().resource::<Probe>();
    let smoke = matches!(
        probe.plant_projection,
        super::PlantProjection::WalkSmoke | super::PlantProjection::WalkLongSmoke
    );
    let ladder_test = smoke && std::env::var("FORGE_TIDEWATER_LADDER_SMOKE").as_deref() == Ok("1");
    let swim_test =
        !ladder_test && smoke && std::env::var("FORGE_TIDEWATER_SWIM_SMOKE").as_deref() == Ok("1");
    let plan = Arc::new(
        forge_world::sampled_terrain::SampledTerrainPlan::from_document(
            probe
                .terrain
                .as_ref()
                .ok_or("Walk requires canonical terrain")?
                .document(),
            probe
                .lod_heights
                .as_ref()
                .ok_or("Walk requires height bytes")?,
        )?,
    );
    let physics = PreparedSampledTerrainPhysics::new(plan.clone())?;
    let geometry = ViewGeometry::new(&plan)?;
    let mut profile = forge_world::embedded_visual_valley_player_controller_profile()?;
    profile.controller.capsule_radius_m = forge_engine::tidewater_locomotion::RADIUS;
    profile.controller.capsule_half_segment_m =
        forge_engine::tidewater_locomotion::WALK_HEIGHT * 0.5 - profile.controller.capsule_radius_m;
    profile.controller.walk_speed_mps = 3.0;
    profile.controller.sprint_multiplier = 6.2 / 3.0;
    profile.controller.jump_speed_mps = 4.6;
    profile.controller.max_step_height_m = 0.4;
    profile.camera.eye_height_m = 1.62;
    profile.camera.look_sensitivity_degrees_per_unit = 0.0022_f32.to_degrees();
    profile.camera.pitch_min_degrees = -1.5_f32.to_degrees();
    profile.camera.pitch_max_degrees = 1.5_f32.to_degrees();
    let center = profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m;
    let spawn_xz = walk_spawn(smoke, ladder_test, swim_test);
    #[allow(clippy::cast_possible_truncation)]
    let ground = plan.height_at(f64::from(spawn_xz.x), f64::from(spawn_xz.y))? as f32;
    let initial_target = if ladder_test {
        Vec3::new(PIER_LADDER.center_xz.x, ground, PIER_LADDER.center_xz.y)
    } else {
        Vec3::new(spawn_xz.x, ground, 0.0)
    };
    sampled_terrain_physics::register(app);
    super::spray::enable_live(app)?;
    app.world()
        .resource::<super::ocean::LiveClock>()
        .set_time_scale(WATER_TIME_SCALE)?;
    super::walk_clouds::install(app)?;
    super::walk_wind::install(app);
    super::walk_grass::install(app);
    app.init_resource::<super::water_time::WaterTime>()
        .add_systems(Update, super::water_time::update);
    physics.stage(app.world_mut());
    let locomotion = forge_engine::TidewaterPlayerPlugin::new(
        app.world()
            .resource::<super::spray::water_queries::Targets>()
            .queries
            .clone(),
        plan,
    )?
    .with_interactive_input(!smoke)
    .with_first_person(!smoke || swim_test || ladder_test)
    .with_ladders([PIER_LADDER]);
    let underwater = underwater_plugin(app)?;
    if !smoke {
        graphics::install(app)?;
    }
    app.add_plugins(
        PlayerControllerPlugin::new(
            profile,
            Vec3::new(spawn_xz.x, ground + center + 0.15, spawn_xz.y),
        )
        .with_initial_camera_target(initial_target)
        .with_interactive_input(!smoke),
    )
    .add_plugins((locomotion, underwater))
    .insert_resource(PlayerSimulationReady(false))
    .insert_resource(PlayerInputFocus(false))
    .insert_resource(Walk {
        geometry,
        previous: None,
        terrain_nodes: Vec::new(),
        ocean_nodes: Vec::new(),
        ocean_updates: 0,
    })
    .add_systems(PostStartup, (configure, buildings))
    .add_systems(
        Update,
        (cursor.after(graphics::toggle), update_ladder_prompt),
    )
    .add_systems(
        PostUpdate,
        (update, live_camera).after(bevy::transform::TransformSystems::Propagate),
    );
    if ladder_test {
        ladder_smoke::install(app);
    } else if swim_test {
        swim_smoke::install(app);
    } else if smoke {
        smoke::install(app);
    }
    Ok(())
}

pub(super) fn underwater_plugin(
    app: &App,
) -> Result<forge_engine::tidewater_underwater::TidewaterUnderwaterPlugin, Box<dyn std::error::Error>>
{
    let targets = app
        .world()
        .get_resource::<super::spray::water_queries::Targets>()
        .ok_or("Native immersion requires installed ocean/shore surface queries")?;
    let maps = app
        .world()
        .get_resource::<forge_engine::tidewater_caustics::CausticMaps>()
        .ok_or("Native immersion requires the live caustic photon maps")?
        .clone();
    let params = &app
        .world()
        .get_resource::<forge_engine::tidewater_caustics::CausticMaterialInputs>()
        .ok_or("Native immersion requires the real caustic material inputs")?
        .params;
    Ok(
        forge_engine::tidewater_underwater::TidewaterUnderwaterPlugin::new(
            targets.queries.clone(),
            targets.packet.clone(),
            super::water_optics::underwater_optics(app)?,
        )
        .with_surface_shader(
            super::ocean_geometry::UNDERWATER_SURFACE_SHADER,
            <super::water_optics::WaterMaterial as bevy::render::render_resource::AsBindGroup>::bind_group_layout_descriptor,
        )
        .with_caustic_maps(maps, params.tiles_m.x, params.tiles_m.y)?
        .with_effect_enabled(std::env::var_os("FORGE_TIDEWATER_UNDERWATER_DISABLED").is_none()),
    )
}

fn live_camera(
    cameras: Query<&GlobalTransform, With<ForgePlayerCamera>>,
    target: Res<super::spray::LiveCamera>,
    queries: Res<super::spray::water_queries::Targets>,
    player: Res<forge_engine::PlayerRuntimeSnapshot>,
) {
    if let Ok(camera) = cameras.single() {
        target.set_position(camera.translation());
        if queries
            .bootstrap_ready
            .load(std::sync::atomic::Ordering::Acquire)
        {
            let camera = camera.translation();
            queries
                .queries
                .set_point(0, camera.x, camera.z)
                .expect("finite camera query");
            queries
                .queries
                .set_point(1, player.player_position.x, player.player_position.z)
                .expect("finite player query");
        }
    }
}

fn configure(
    mut windows: Query<&mut Window>,
    mut cameras: Query<(&mut Projection, Entity), With<ForgePlayerCamera>>,
    mut commands: Commands,
) {
    for mut window in &mut windows {
        window.title = "Tidewater | clic: jouer | WASD: marcher/nager | Maj: accélérer | Espace: sauter/monter/échelle | C/Ctrl: plonger | V: vue | H: graphismes | Échap: souris".into();
    }
    for (mut projection, entity) in &mut cameras {
        if let Projection::Perspective(p) = &mut *projection {
            p.far = 60_000.0;
        }
        commands.entity(entity).insert((
            Msaa::Off,
            Camera3d {
                depth_texture_usages: (TextureUsages::RENDER_ATTACHMENT
                    | TextureUsages::COPY_SRC
                    | TextureUsages::TEXTURE_BINDING)
                    .into(),
                ..default()
            },
            forge_engine::tidewater_underwater::TidewaterUnderwaterCamera,
        ));
    }
    commands
        .spawn((
            LadderPrompt,
            Visibility::Hidden,
            Node {
                position_type: PositionType::Absolute,
                bottom: Val::Px(72.0),
                left: Val::Percent(50.0),
                padding: UiRect::axes(Val::Px(14.0), Val::Px(8.0)),
                ..default()
            },
            BackgroundColor(Color::srgba(0.02, 0.03, 0.04, 0.78)),
        ))
        .with_child((
            Text::new("Space — Climb ladder"),
            TextFont {
                font_size: FontSize::Px(26.0),
                ..default()
            },
            TextColor(Color::WHITE),
        ));
}

fn update_ladder_prompt(
    state: Res<forge_engine::TidewaterPlayerState>,
    mut prompt: Query<&mut Visibility, With<LadderPrompt>>,
) {
    if let Ok(mut visibility) = prompt.single_mut() {
        *visibility = if state.ladder_nearby {
            Visibility::Visible
        } else {
            Visibility::Hidden
        };
    }
}

fn cursor(
    keys: Res<ButtonInput<KeyCode>>,
    mouse: Res<ButtonInput<MouseButton>>,
    mut focus: ResMut<PlayerInputFocus>,
    mut windows: Query<(&Window, &mut CursorOptions)>,
    graphics: Option<Res<graphics::GraphicsState>>,
) {
    for (window, mut cursor) in &mut windows {
        if graphics.as_ref().is_some_and(|graphics| graphics.open)
            || keys.just_pressed(KeyCode::Escape)
            || !window.focused
        {
            focus.0 = false;
            cursor.grab_mode = CursorGrabMode::None;
            cursor.visible = true;
        } else if mouse.just_pressed(MouseButton::Left) {
            focus.0 = true;
            cursor.grab_mode = CursorGrabMode::Locked;
            cursor.visible = false;
        }
    }
}

fn buildings(world: &mut World) {
    let mut query = world.query::<(
        Entity,
        &Mesh3d,
        &MeshMaterial3d<super::village::VillageMaterial>,
        &Visibility,
    )>();
    let colliders = query
        .iter(world)
        .filter(|(_, _, _, visible)| **visible == Visibility::Visible)
        .map(|(entity, mesh, _, _)| {
            let mesh = world
                .resource::<Assets<Mesh>>()
                .get(&mesh.0)
                .expect("village mesh");
            let (collider, _) =
                forge_engine::tidewater_assets::scene::surface(mesh).expect("village collision");
            (entity, collider)
        })
        .collect::<Vec<_>>();
    for (entity, collider) in colliders {
        world.entity_mut(entity).insert(collider);
    }
}

fn update(world: &mut World) {
    let physics = world.resource::<SampledTerrainPhysicsState>();
    let ready = physics.is_synchronized(world, physics.plan.document_hash())
        && world
            .resource::<super::spray::water_queries::Targets>()
            .bootstrap_ready
            .load(std::sync::atomic::Ordering::Acquire)
        && world.resource::<Probe>().ready_frames >= 8
        && world
            .resource::<super::spray::Targets>()
            .readback_complete
            .load(std::sync::atomic::Ordering::Acquire)
        && world
            .resource::<super::ocean::Targets>()
            .readback_complete
            .load(std::sync::atomic::Ordering::Acquire)
        && world
            .resource::<super::clouds::Targets>()
            .ready
            .load(std::sync::atomic::Ordering::Acquire);
    world.resource_mut::<PlayerSimulationReady>().0 = ready;
    if !ready {
        return;
    }
    let mut query = world.query_filtered::<(&Transform, &Projection), With<ForgePlayerCamera>>();
    let Ok((transform, Projection::Perspective(projection))) = query.single(world) else {
        return;
    };
    let transform = *transform;
    let projection = projection.clone();
    let state = world.resource::<Walk>();
    if state.previous.is_some_and(|p| {
        p.translation.distance_squared(transform.translation) < 1.0
            && p.rotation.dot(transform.rotation).abs() > 0.9999
    }) {
        return;
    }
    let nodes = state
        .geometry
        .nodes(super::terrain_lod::source_view(&transform, &projection))
        .expect("walk terrain view");
    if !forge_world::cdlod::same_node_geometry(&state.terrain_nodes, &nodes) {
        let terrain = world
            .resource::<ResidentTerrain>()
            .entity
            .expect("terrain projection");
        if let Some((mesh, _)) = ViewGeometry::mesh_from_nodes(&nodes) {
            let handle = world.resource_mut::<Assets<Mesh>>().add(mesh);
            world
                .entity_mut(terrain)
                .insert((Mesh3d(handle), Visibility::Inherited));
        } else {
            world.entity_mut(terrain).insert(Visibility::Hidden);
        }
        world.resource_mut::<Walk>().terrain_nodes = nodes;
    }
    world.resource_scope(|world, mut plants: Mut<ResidentProof>| {
        plants
            .resident
            .select(
                world,
                forge_engine::tidewater_plants::scene::View {
                    camera: transform.translation,
                    visibility: Visibility::Visible,
                },
            )
            .expect("walk vegetation selection");
    });
    update_ocean(world, &transform, &projection);
    world.resource_mut::<Walk>().previous = Some(transform);
}

fn update_ocean(world: &mut World, transform: &Transform, projection: &PerspectiveProjection) {
    let nodes =
        super::ocean_geometry::view_nodes(transform, projection).expect("walk ocean selection");
    if forge_world::cdlod::same_node_geometry(&nodes, &world.resource::<Walk>().ocean_nodes) {
        return;
    }
    let mut query = world.query_filtered::<Entity, With<super::ocean_geometry::OceanSurface>>();
    let Ok(entity) = query.single(world) else {
        return;
    };
    let mesh = world
        .resource_mut::<Assets<Mesh>>()
        .add(super::ocean_geometry::node_mesh(&nodes));
    world.entity_mut(entity).insert(Mesh3d(mesh));
    let mut state = world.resource_mut::<Walk>();
    state.ocean_nodes = nodes;
    state.ocean_updates += 1;
}
