//! Interactive development walk using the Forge player and canonical collisions.
use super::{Probe, vegetation_scene::ResidentProof};
use bevy::{
    prelude::*,
    window::{CursorGrabMode, CursorOptions},
};
use forge_engine::{
    ForgePlayerCamera, PlayerControllerPlugin, PlayerInputFocus, PlayerSimulationReady,
    sampled_terrain_physics::{self, PreparedSampledTerrainPhysics, SampledTerrainPhysicsState},
    tidewater_terrain::{lod::ViewGeometry, scene::ResidentTerrain},
};
use std::sync::Arc;

#[derive(Resource)]
struct Walk {
    geometry: ViewGeometry,
    previous: Option<Transform>,
}

pub(super) fn install(app: &mut App) -> Result<(), Box<dyn std::error::Error>> {
    let plan = Arc::new(
        app.world()
            .resource::<Probe>()
            .terrain
            .clone()
            .ok_or("Walk requires canonical terrain")?,
    );
    let physics = PreparedSampledTerrainPhysics::new(plan.clone())?;
    let geometry = ViewGeometry::new(&plan)?;
    let profile = forge_world::embedded_visual_valley_player_controller_profile()?;
    let center = profile.controller.capsule_radius_m + profile.controller.capsule_half_segment_m;
    #[allow(clippy::cast_possible_truncation)]
    let ground = plan.height_at(42.0, -90.0)? as f32;
    sampled_terrain_physics::register(app);
    physics.stage(app.world_mut());
    app.add_plugins(
        PlayerControllerPlugin::new(profile, Vec3::new(42.0, ground + center + 0.15, -90.0))
            .with_initial_camera_target(Vec3::new(42.0, ground, 0.0)),
    )
    .insert_resource(PlayerSimulationReady(false))
    .insert_resource(PlayerInputFocus(false))
    .insert_resource(Walk {
        geometry,
        previous: None,
    })
    .add_systems(PostStartup, configure)
    .add_systems(Update, cursor)
    .add_systems(
        PostUpdate,
        update.after(bevy::transform::TransformSystems::Propagate),
    );
    Ok(())
}

fn configure(
    mut windows: Query<&mut Window>,
    mut cameras: Query<(&mut Projection, Entity), With<ForgePlayerCamera>>,
    mut commands: Commands,
) {
    for mut window in &mut windows {
        window.title = "Tidewater — parcours Forge | clic: jouer | WASD: marcher | Maj: courir | Espace: sauter | V: vue | Échap: souris".into();
    }
    for (mut projection, entity) in &mut cameras {
        if let Projection::Perspective(p) = &mut *projection {
            p.far = 60_000.0;
        }
        commands.entity(entity).insert(Msaa::Off);
    }
}

fn cursor(
    keys: Res<ButtonInput<KeyCode>>,
    mouse: Res<ButtonInput<MouseButton>>,
    mut focus: ResMut<PlayerInputFocus>,
    mut windows: Query<(&Window, &mut CursorOptions)>,
) {
    for (window, mut cursor) in &mut windows {
        if keys.just_pressed(KeyCode::Escape) || !window.focused {
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

fn update(world: &mut World) {
    let physics = world.resource::<SampledTerrainPhysicsState>();
    let ready = physics.is_synchronized(world, physics.plan.document_hash())
        && world.resource::<Probe>().ready_frames >= 8;
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
    let mesh = state
        .geometry
        .mesh(super::terrain_lod::source_view(&transform, &projection))
        .expect("walk terrain view");
    if let Some((mesh, _)) = mesh {
        let terrain = world
            .resource::<ResidentTerrain>()
            .entity
            .expect("terrain projection");
        let handle = world.resource_mut::<Assets<Mesh>>().add(mesh);
        world.entity_mut(terrain).insert(Mesh3d(handle));
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
    world.resource_mut::<Walk>().previous = Some(transform);
}
