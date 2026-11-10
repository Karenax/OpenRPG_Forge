#![allow(
    clippy::cast_possible_truncation,
    clippy::collapsible_if,
    clippy::needless_pass_by_value,
    clippy::struct_excessive_bools,
    reason = "The visual proof keeps explicit runtime flags and bounded mesh counters; Bevy systems take owned plugin values."
)]
//! Visual M2 visit: loads a validated Aurora bundle, renders the projected WOK
//! walkmesh and objects, and lets the player walk, open doors and transition.

use bevy::{
    asset::RenderAssetUsages,
    prelude::*,
    render::{
        render_resource::PrimitiveTopology,
        view::screenshot::{Screenshot, save_to_disk},
    },
    window::WindowResolution,
};
use forge_aurora_runtime::{
    ModuleSession, ProjectedModule, ProjectedZone, project_module, run_scripted_visit,
};
use std::path::PathBuf;

#[derive(Resource)]
struct AuroraRuntime {
    session: ModuleSession,
    report_path: Option<PathBuf>,
    capture_path: Option<PathBuf>,
    frame: u32,
    exit_after: u32,
    capture_requested: bool,
    captured: bool,
    finished: bool,
    needs_rebuild: bool,
    auto_visit: bool,
    auto_step: u8,
}

#[derive(Component)]
struct ZoneVisual;

#[derive(Component)]
struct PlayerVisual;

#[derive(Component)]
struct HudText;

#[derive(Debug)]
struct Options {
    bundle: PathBuf,
    capture: Option<PathBuf>,
    report: Option<PathBuf>,
    exit_after: u32,
    auto_visit: bool,
}

fn main() {
    let options = match Options::parse(std::env::args().skip(1)) {
        Ok(options) => options,
        Err(message) => {
            eprintln!("forge_aurora_visual: {message}");
            std::process::exit(2);
        }
    };
    let imported = match forge_aurora_bridge::load_bundle(&options.bundle) {
        Ok(imported) => imported,
        Err(error) => {
            eprintln!("forge_aurora_visual: {error}");
            std::process::exit(2);
        }
    };
    let projected = match project_module(&imported) {
        Ok(projected) => projected,
        Err(error) => {
            eprintln!("forge_aurora_visual: {error}");
            std::process::exit(2);
        }
    };
    let entry = projected.entry_area.clone();
    let session = match ModuleSession::new(projected, &entry) {
        Ok(session) => session,
        Err(error) => {
            eprintln!("forge_aurora_visual: {error}");
            std::process::exit(2);
        }
    };
    let runtime = AuroraRuntime {
        session,
        report_path: options.report,
        capture_path: options.capture,
        frame: 0,
        exit_after: options.exit_after.max(30),
        capture_requested: false,
        captured: false,
        finished: false,
        needs_rebuild: false,
        auto_visit: options.auto_visit,
        auto_step: 0,
    };
    App::new()
        .insert_resource(ClearColor(Color::srgb(0.03, 0.045, 0.075)))
        .insert_resource(runtime)
        .add_plugins(DefaultPlugins.set(WindowPlugin {
            primary_window: Some(Window {
                title: "OpenRPG Forge — Visite Aurora M2".to_owned(),
                resolution: WindowResolution::new(1280, 720).with_scale_factor_override(1.0),
                resizable: true,
                ..default()
            }),
            ..default()
        }))
        .add_systems(Startup, setup)
        .add_systems(
            Update,
            (
                move_player,
                interact_or_transition,
                auto_visit,
                rebuild_zone,
                update_player,
                update_camera,
                update_hud,
                request_capture,
                finish_visit,
            )
                .chain(),
        )
        .run();
}

fn setup(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<StandardMaterial>>,
    runtime: Res<AuroraRuntime>,
) {
    commands.spawn(AmbientLight {
        color: Color::WHITE,
        brightness: 450.0,
        ..default()
    });
    commands.spawn((
        DirectionalLight {
            illuminance: 12_000.0,
            shadow_maps_enabled: false,
            ..default()
        },
        Transform::from_rotation(Quat::from_euler(EulerRot::XYZ, -0.9, 0.6, 0.0)),
    ));
    commands.spawn((
        Camera3d::default(),
        Transform::from_xyz(0.0, 10.0, 16.0).looking_at(Vec3::ZERO, Vec3::Y),
    ));
    commands.spawn((
        Text::new("Visite Aurora"),
        TextFont {
            font_size: bevy::text::FontSize::Px(18.0),
            ..default()
        },
        TextColor(Color::WHITE),
        Node {
            position_type: PositionType::Absolute,
            left: Val::Px(14.0),
            top: Val::Px(12.0),
            ..default()
        },
        HudText,
    ));
    spawn_zone(
        &mut commands,
        &mut meshes,
        &mut materials,
        runtime.session.zone(),
    );
    spawn_player(
        &mut commands,
        &mut meshes,
        &mut materials,
        runtime.session.player().position,
    );
}

fn spawn_player(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<StandardMaterial>,
    position: forge_core::Vec3,
) {
    commands.spawn((
        Mesh3d(meshes.add(Sphere::new(0.35).mesh().ico(3).unwrap())),
        MeshMaterial3d(materials.add(StandardMaterial {
            base_color: Color::srgb(0.25, 0.95, 0.55),
            emissive: LinearRgba::rgb(0.15, 0.8, 0.3),
            ..default()
        })),
        Transform::from_xyz(position.x, position.y + 0.9, position.z),
        PlayerVisual,
    ));
}

fn spawn_zone(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<StandardMaterial>,
    zone: &ProjectedZone,
) {
    let walkable = mesh_from_triangles(zone, true);
    let blocked = mesh_from_triangles(zone, false);
    let (walkable_color, blocked_color) = if zone.resref.to_ascii_lowercase().contains("crypt") {
        (Color::srgb(0.22, 0.22, 0.42), Color::srgb(0.50, 0.12, 0.45))
    } else {
        (Color::srgb(0.20, 0.35, 0.28), Color::srgb(0.55, 0.15, 0.18))
    };
    let walkable_material = materials.add(StandardMaterial {
        base_color: walkable_color,
        perceptual_roughness: 0.9,
        ..default()
    });
    let blocked_material = materials.add(StandardMaterial {
        base_color: blocked_color,
        perceptual_roughness: 0.9,
        ..default()
    });
    if let Some(mesh) = walkable {
        commands.spawn((
            Mesh3d(meshes.add(mesh)),
            MeshMaterial3d(walkable_material),
            Transform::default(),
            ZoneVisual,
        ));
    }
    if let Some(mesh) = blocked {
        commands.spawn((
            Mesh3d(meshes.add(mesh)),
            MeshMaterial3d(blocked_material),
            Transform::default(),
            ZoneVisual,
        ));
    }
    let door_material = materials.add(StandardMaterial {
        base_color: Color::srgb(0.95, 0.68, 0.20),
        emissive: LinearRgba::rgb(0.4, 0.2, 0.02),
        ..default()
    });
    let prop_material = materials.add(StandardMaterial {
        base_color: Color::srgb(0.55, 0.42, 0.28),
        ..default()
    });
    let creature_material = materials.add(StandardMaterial {
        base_color: Color::srgb(0.75, 0.25, 0.45),
        ..default()
    });
    for instance in &zone.instances {
        let (mesh, material) = match instance.category.as_str() {
            "door" => (
                meshes.add(Cuboid::new(0.12, 2.2, 1.4)),
                door_material.clone(),
            ),
            "creature" => (
                meshes.add(Capsule3d::new(0.35, 0.9)),
                creature_material.clone(),
            ),
            "placeable" => (
                meshes.add(Cuboid::new(0.7, 0.9, 0.7)),
                prop_material.clone(),
            ),
            _ => (meshes.add(Sphere::new(0.25)), prop_material.clone()),
        };
        let base_y = zone
            .walkmesh
            .height_at(instance.position.x, instance.position.z)
            .unwrap_or(instance.position.y);
        commands.spawn((
            Mesh3d(mesh),
            MeshMaterial3d(material),
            Transform::from_xyz(instance.position.x, base_y + 0.5, instance.position.z),
            ZoneVisual,
        ));
    }
}

fn mesh_from_triangles(zone: &ProjectedZone, walkable: bool) -> Option<Mesh> {
    let triangles = zone
        .walkmesh
        .triangles
        .iter()
        .filter(|triangle| {
            forge_aurora_runtime::walkmesh::surface_is_walkable(triangle.surface) == walkable
        })
        .collect::<Vec<_>>();
    if triangles.is_empty() {
        return None;
    }
    let mut positions = Vec::with_capacity(triangles.len() * 3);
    let mut normals = Vec::with_capacity(triangles.len() * 3);
    let mut indices = Vec::with_capacity(triangles.len() * 3);
    for triangle in triangles {
        let base = positions.len() as u32;
        for point in [triangle.a, triangle.b, triangle.c] {
            positions.push([point.x, point.y + 0.01, point.z]);
            normals.push([0.0, 1.0, 0.0]);
        }
        indices.extend_from_slice(&[base, base + 1, base + 2]);
    }
    let mut mesh = Mesh::new(
        PrimitiveTopology::TriangleList,
        RenderAssetUsages::default(),
    );
    mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, positions);
    mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, normals);
    mesh.insert_indices(bevy::mesh::Indices::U32(indices));
    Some(mesh)
}

fn move_player(
    keys: Res<ButtonInput<KeyCode>>,
    time: Res<Time>,
    mut runtime: ResMut<AuroraRuntime>,
) {
    if runtime.finished {
        return;
    }
    let mut forward = 0.0;
    let mut strafe = 0.0;
    if keys.pressed(KeyCode::KeyW) || keys.pressed(KeyCode::ArrowUp) {
        forward += 1.0;
    }
    if keys.pressed(KeyCode::KeyS) || keys.pressed(KeyCode::ArrowDown) {
        forward -= 1.0;
    }
    if keys.pressed(KeyCode::KeyD) || keys.pressed(KeyCode::ArrowRight) {
        strafe += 1.0;
    }
    if keys.pressed(KeyCode::KeyA) || keys.pressed(KeyCode::ArrowLeft) {
        strafe -= 1.0;
    }
    if forward != 0.0 || strafe != 0.0 {
        runtime
            .session
            .move_by(forward, strafe, time.delta_secs().min(0.1));
    }
}

fn interact_or_transition(keys: Res<ButtonInput<KeyCode>>, mut runtime: ResMut<AuroraRuntime>) {
    if runtime.finished {
        return;
    }
    if keys.just_pressed(KeyCode::KeyE) {
        let _ = runtime.session.interact();
    }
    if keys.just_pressed(KeyCode::KeyF) || keys.just_pressed(KeyCode::Enter) {
        if let Ok(Some(_)) = runtime.session.try_transition() {
            runtime.needs_rebuild = true;
        }
    }
}

fn auto_visit(mut runtime: ResMut<AuroraRuntime>) {
    if !runtime.auto_visit || runtime.finished {
        return;
    }
    match runtime.auto_step {
        0 if runtime.frame >= 20 => {
            approach_and_open(&mut runtime, 0);
            runtime.auto_step = 1;
        }
        1 => {
            if let Ok(Some(_)) = runtime.session.try_transition() {
                runtime.needs_rebuild = true;
                runtime.auto_step = 2;
            }
        }
        2 if runtime.frame >= 55 => {
            approach_and_open(&mut runtime, 1);
            runtime.auto_step = 3;
        }
        3 => {
            if let Ok(Some(_)) = runtime.session.try_transition() {
                runtime.needs_rebuild = true;
                runtime.auto_step = 4;
            }
        }
        _ => {}
    }
}

fn approach_and_open(runtime: &mut AuroraRuntime, ordinal: usize) {
    if let Some(door) = runtime
        .session
        .zone()
        .doors
        .iter()
        .filter(|door| door.destination.is_some())
        .nth(ordinal)
        .cloned()
        .or_else(|| {
            runtime
                .session
                .zone()
                .doors
                .iter()
                .find(|door| door.destination.is_some())
                .cloned()
        })
    {
        let target = approach_point(door.position, runtime.session.player().position);
        let _ = runtime.session.walk_to(target);
        let _ = runtime.session.interact();
    }
}

fn approach_point(door: forge_core::Vec3, from: forge_core::Vec3) -> forge_core::Vec3 {
    let dx = from.x - door.x;
    let dz = from.z - door.z;
    let length = (dx * dx + dz * dz).sqrt();
    if length <= 1.0e-4 {
        forge_core::Vec3::new(door.x, door.y, door.z + 1.2)
    } else {
        forge_core::Vec3::new(
            door.x + dx / length * 1.2,
            door.y,
            door.z + dz / length * 1.2,
        )
    }
}

fn rebuild_zone(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<StandardMaterial>>,
    existing: Query<Entity, With<ZoneVisual>>,
    mut runtime: ResMut<AuroraRuntime>,
) {
    if !runtime.needs_rebuild {
        return;
    }
    for entity in &existing {
        commands.entity(entity).despawn();
    }
    spawn_zone(
        &mut commands,
        &mut meshes,
        &mut materials,
        runtime.session.zone(),
    );
    runtime.needs_rebuild = false;
}

fn update_player(
    mut player: Query<&mut Transform, With<PlayerVisual>>,
    runtime: Res<AuroraRuntime>,
) {
    let position = runtime.session.player().position;
    if let Ok(mut transform) = player.single_mut() {
        transform.translation = Vec3::new(position.x, position.y + 0.9, position.z);
    }
}

fn update_camera(
    mut camera: Query<&mut Transform, (With<Camera3d>, Without<PlayerVisual>)>,
    runtime: Res<AuroraRuntime>,
) {
    let position = runtime.session.player().position;
    if let Ok(mut transform) = camera.single_mut() {
        let target = Vec3::new(position.x, position.y + 0.9, position.z);
        transform.translation = target + Vec3::new(0.0, 9.0, 12.0);
        transform.look_at(target, Vec3::Y);
    }
}

fn update_hud(runtime: Res<AuroraRuntime>, mut text: Query<&mut Text, With<HudText>>) {
    if let Ok(mut text) = text.single_mut() {
        let zone = runtime.session.zone();
        let door = runtime
            .session
            .nearest_door(2.5)
            .and_then(|door| door.tag.clone())
            .unwrap_or_else(|| "aucune".to_owned());
        text.0 = format!(
            "Visite Aurora M2\nZone : {} ({} triangles WOK)\nJoueur : [{:.1}, {:.1}, {:.1}]\nPorte proche : {}\nWASD déplacer · E ouvrir/fermer · F transition\nÉvénements : {}",
            zone.name,
            zone.walkmesh.triangles.len(),
            runtime.session.player().position.x,
            runtime.session.player().position.y,
            runtime.session.player().position.z,
            door,
            runtime.session.events().len()
        );
    }
}

fn request_capture(mut commands: Commands, mut runtime: ResMut<AuroraRuntime>) {
    runtime.frame = runtime.frame.saturating_add(1);
    let Some(path) = runtime.capture_path.clone() else {
        return;
    };
    if !runtime.capture_requested && runtime.frame >= 45 {
        commands
            .spawn(Screenshot::primary_window())
            .observe(save_to_disk(path));
        runtime.capture_requested = true;
    }
    if runtime.capture_requested {
        runtime.captured = true;
    }
}

fn finish_visit(mut commands: Commands, mut runtime: ResMut<AuroraRuntime>) {
    if runtime.finished {
        return;
    }
    let capture_wait = if runtime.capture_requested { 12 } else { 0 };
    if runtime.frame >= runtime.exit_after + capture_wait {
        if let Some(path) = runtime.report_path.clone() {
            let report = runtime.session.report();
            if let Ok(bytes) = serde_json::to_vec_pretty(&report) {
                let _ = std::fs::write(path, bytes);
            }
        }
        runtime.finished = true;
        commands.write_message(AppExit::Success);
    }
}

impl Options {
    fn parse(arguments: impl IntoIterator<Item = String>) -> Result<Self, String> {
        let mut arguments = arguments.into_iter();
        let mut bundle = None;
        let mut capture = None;
        let mut report = None;
        let mut exit_after = 180;
        let mut auto_visit = false;
        while let Some(flag) = arguments.next() {
            if matches!(flag.as_str(), "--help" | "-h") {
                println!(
                    "forge_aurora_visual --bundle <dir> [--capture <png>] [--report <json>] [--exit-after-frames <n>] [--auto-visit]"
                );
                std::process::exit(0);
            }
            if flag == "--auto-visit" {
                auto_visit = true;
                continue;
            }
            let value = arguments
                .next()
                .ok_or_else(|| format!("{flag} requires a value"))?;
            match flag.as_str() {
                "--bundle" => bundle = Some(PathBuf::from(value)),
                "--capture" => capture = Some(PathBuf::from(value)),
                "--report" => report = Some(PathBuf::from(value)),
                "--exit-after-frames" => {
                    exit_after = value
                        .parse()
                        .map_err(|_| "--exit-after-frames requires an integer".to_owned())?;
                }
                _ => return Err(format!("unknown option {flag}")),
            }
        }
        Ok(Self {
            bundle: bundle.ok_or_else(|| "usage: --bundle <dir>".to_owned())?,
            capture,
            report,
            exit_after,
            auto_visit,
        })
    }
}

// Keep the scripted visit helper linked for headless users of the same crate.
#[allow(dead_code)]
fn headless(module: ProjectedModule) -> Option<forge_aurora_runtime::VisitReport> {
    run_scripted_visit(module).ok()
}
