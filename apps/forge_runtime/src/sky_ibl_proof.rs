//! One bounded raw atmosphere cubemap readback; no lighting or texture changes.
use bevy::{
    diagnostic::FrameCount,
    light::{AtmosphereEnvironmentMapLight, EnvironmentMapLight, GeneratedEnvironmentMapLight},
    prelude::*,
    render::{
        gpu_readback::{Readback, ReadbackComplete},
        render_resource::{TextureFormat, TextureUsages},
    },
};
use std::{io::Write, path::PathBuf};

#[derive(Resource)]
struct Request {
    path: PathBuf,
    issued: bool,
    completed: bool,
}

pub fn install(app: &mut App, path: PathBuf) {
    app.insert_resource(Request {
        path,
        issued: false,
        completed: false,
    })
    .add_systems(Update, request);
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn request(
    mut commands: Commands,
    frame: Res<FrameCount>,
    mut request: ResMut<Request>,
    probes: Query<(
        &AtmosphereEnvironmentMapLight,
        &GeneratedEnvironmentMapLight,
        Option<&EnvironmentMapLight>,
    )>,
    images: Res<Assets<Image>>,
    cameras: Query<(&Camera, Option<&EnvironmentMapLight>), With<Camera3d>>,
) {
    if request.issued || frame.0 < 900 {
        return;
    }
    let Ok((map, generated, filtered)) = probes.single() else {
        return;
    };
    let Some(image) = images.get(&generated.environment_map) else {
        return;
    };
    let desc = &image.texture_descriptor;
    if desc.format != TextureFormat::Rgba16Float
        || desc.size.depth_or_array_layers != 6
        || desc.size.width != map.size.x
        || desc.size.height != map.size.y
        || !desc.usage.contains(TextureUsages::COPY_SRC)
    {
        return;
    }
    request.issued = true;
    let camera_bindings: Vec<_> = cameras
        .iter()
        .filter(|(camera, _)| camera.is_active)
        .map(|(_, map)| map.map(binding))
        .collect();
    let bindings = serde_json::json!({"filtered_probe": filtered.map(binding), "active_camera_bindings": camera_bindings});
    tracing::info!(event_kind = "sky_ibl_binding_snapshot", report = %bindings,
        "CPU bindings at readback request; filtered GPU pixels not read here");
    tracing::info!(
        event_kind = "sky_ibl_readback_requested",
        width = desc.size.width,
        height = desc.size.height,
        layers = 6,
        format = "rgba16float",
        intensity = map.intensity,
        frame = frame.0,
        "Raw atmosphere cubemap, not filtered diffuse irradiance or visual qualification"
    );
    commands
        .spawn(Readback::texture(generated.environment_map.clone()))
        .observe(complete);
}

fn binding(map: &EnvironmentMapLight) -> serde_json::Value {
    serde_json::json!({
        "diffuse_id": format!("{:?}", map.diffuse_map.id()),
        "specular_id": format!("{:?}", map.specular_map.id()),
        "intensity": map.intensity, "rotation": map.rotation.to_array(),
        "affects_lightmapped_mesh_diffuse": map.affects_lightmapped_mesh_diffuse,
    })
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn complete(event: On<ReadbackComplete>, mut commands: Commands, mut request: ResMut<Request>) {
    if request.completed {
        return;
    }
    request.completed = true;
    commands.entity(event.entity).despawn();
    let result = std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&request.path)
        .and_then(|mut file| file.write_all(&event.data));
    match result {
        Ok(()) => {
            tracing::info!(event_kind = "sky_ibl_readback_completed", bytes = event.data.len(),
            path = %request.path.display(), "GPU bytes saved; analyze format and row layout before interpreting");
        }
        Err(error) => tracing::error!(event_kind = "sky_ibl_readback_failed", %error),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{
        asset::RenderAssetUsages,
        render::render_resource::{Extent3d, TextureDimension},
    };

    #[test]
    fn waits_for_warmup_then_issues_only_one_readback() {
        let mut app = App::new();
        app.init_resource::<Assets<Image>>()
            .insert_resource(FrameCount(899));
        install(&mut app, PathBuf::from("unused-test-proof.bin"));
        let mut image = Image::new_fill(
            Extent3d {
                width: 1,
                height: 1,
                depth_or_array_layers: 6,
            },
            TextureDimension::D2,
            &[0; 8],
            TextureFormat::Rgba16Float,
            RenderAssetUsages::all(),
        );
        image.texture_descriptor.usage |= TextureUsages::COPY_SRC;
        let handle = app.world_mut().resource_mut::<Assets<Image>>().add(image);
        app.world_mut().spawn((
            AtmosphereEnvironmentMapLight {
                size: UVec2::ONE,
                ..default()
            },
            GeneratedEnvironmentMapLight {
                environment_map: handle,
                ..default()
            },
        ));
        app.update();
        assert!(!app.world().resource::<Request>().issued);
        app.world_mut().resource_mut::<FrameCount>().0 = 900;
        app.update();
        app.update();
        assert!(app.world().resource::<Request>().issued);
        assert_eq!(
            app.world_mut()
                .query::<&Readback>()
                .iter(app.world())
                .count(),
            1
        );
    }
}
