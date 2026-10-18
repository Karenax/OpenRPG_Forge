//! Feed the resident cloud graph and scene materials from the same player camera.
use bevy::prelude::*;
use {
    forge_engine::ForgePlayerCamera,
    forge_tidewater::{cloud_controller, tidewater_clouds},
};

#[derive(Resource)]
struct Live {
    source: tidewater_clouds::acquisition_controller::Continuation,
    previous_time: Option<f64>,
}

/// Freezes visible cloud history while a qualification capture keeps one pose.
#[derive(Resource, Default)]
pub(super) struct QualificationHold(pub bool);
#[derive(Resource, Default, serde::Serialize)]
pub(super) struct Receipt {
    pub submitted: u32,
    pub step: u32,
    pub time: f32,
    pub first_camera: Option<[f32; 3]>,
    pub last_camera: [f32; 3],
    pub view_index: usize,
    pub history_resets: u32,
}
impl Receipt {
    pub fn valid(&self) -> bool {
        self.submitted > 60
            && self.time > 2.0
            && self.first_camera.is_some_and(|first| {
                Vec3::from_array(first).distance(Vec3::from_array(self.last_camera)) > 3.0
            })
    }
}
pub(super) fn install(app: &mut App) -> Result<(), Box<dyn std::error::Error>> {
    let source = app
        .world_mut()
        .remove_resource::<super::clouds::LiveSource>()
        .ok_or("walk requires a cloud controller continuation")?
        .0;
    app.insert_resource(Live {
        source,
        previous_time: None,
    })
    .init_resource::<QualificationHold>()
    .init_resource::<Receipt>()
    .add_systems(
        PostUpdate,
        update.after(bevy::transform::TransformSystems::Propagate),
    );
    Ok(())
}

fn update(world: &mut World) {
    if world.resource::<QualificationHold>().0 {
        return;
    }
    // The snapshot/bootstrap scene is allowed to settle before live presentation.
    if !world.resource::<forge_engine::PlayerSimulationReady>().0
        || !tidewater_clouds::live::can_submit(world)
    {
        return;
    }
    let mut cameras =
        world.query_filtered::<(&GlobalTransform, &Projection), With<ForgePlayerCamera>>();
    let Ok((transform, Projection::Perspective(projection))) = cameras.single(world) else {
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
    let solar = crate::walk::escape_menu::solar_frame(world);
    if solar.is_some_and(|frame| {
        forge_tidewater::tidewater_atmosphere::readback::latest(world, frame).is_none()
    }) {
        return;
    }
    let now = world.resource::<Time>().elapsed_secs_f64();
    let underwater = world
        .get_resource::<forge_tidewater::tidewater_underwater::UnderwaterObservation>()
        .is_some_and(|state| state.camera_underwater());
    let result = world.resource_scope(|world, mut live: Mut<Live>| -> Result<_, String> {
        let mut input = live.source.input;
        input.camera = camera;
        input.underwater = underwater;
        input.dt = live
            .previous_time
            .map_or(1.0 / 60.0, |previous| now - previous);
        // The current promenade stays above water. The controller still carries the
        // original underwater scheduling contract for a future swimming adapter.
        let output = live
            .source
            .controller
            .update(input)
            .map_err(str::to_owned)?;
        let step = if let Some(frame) = solar {
            tidewater_clouds::live::submit_solar(world, &output, frame)?
        } else {
            tidewater_clouds::live::submit(world, &output)?
        };
        live.previous_time = Some(now);
        live.source.last = output.clone();
        Ok((step, output))
    });
    let (step, output) = match result {
        Ok(value) => value,
        Err(error) => {
            error!(%error, "live cloud update failed");
            world.write_message(bevy::app::AppExit::error());
            return;
        }
    };
    publish(world, &output);
    let mut receipt = world.resource_mut::<Receipt>();
    receipt.submitted += 1;
    receipt.step = step;
    receipt.time = output.frame[10][1];
    receipt
        .first_camera
        .get_or_insert(transform.translation.to_array());
    receipt.last_camera = transform.translation.to_array();
    receipt.view_index = output.view_index;
    receipt.history_resets += u32::from(output.frame[9][3] == 0.0);
}

fn publish(world: &mut World, output: &cloud_controller::Output) {
    let params = output.public.map(Vec4::from_array);
    let view =
        world.resource::<tidewater_clouds::Targets>().images[5 + output.view_index * 2].clone();
    let mut inputs = world.resource_mut::<super::sky::CloudInputs>();
    inputs.params = params;
    inputs.view = view.clone();
    for (_, material) in world
        .resource_mut::<Assets<super::sky::SkyMaterial>>()
        .iter_mut()
    {
        material.clouds = params;
        if output.trace_view {
            material.cloud_view = Some(view.clone());
        }
    }
    macro_rules! bind {
        ($kind:ty) => {
            // Scene shaders read only the shadow row. Publishing the sky's
            // camera basis here invalidates every vegetation specialization
            // during yaw, despite leaving its shading inputs unchanged.
            let mut assets = world.resource_mut::<Assets<$kind>>();
            let changed: Vec<_> = assets
                .iter()
                .filter_map(|(id, material)| (material.clouds[4] != params[4]).then_some(id))
                .collect();
            for id in changed {
                assets.get_mut(id).expect("resident material").clouds[4] = params[4];
            }
        };
    }
    bind!(super::PlantMaterial);
    bind!(super::village::VillageMaterial);
    bind!(super::terrain_surface::TerrainMaterial);
    bind!(super::water_optics::WaterMaterial);
    bind!(super::spray_sprites::SprayMaterial);
    bind!(super::breaker_lip::LipMaterial);
    let mut lod = world.resource_mut::<Assets<super::terrain_lod::LodMaterial>>();
    let changed: Vec<_> = lod
        .iter()
        .filter_map(|(id, material)| (material.base.clouds[4] != params[4]).then_some(id))
        .collect();
    for id in changed {
        lod.get_mut(id)
            .expect("resident terrain material")
            .base
            .clouds[4] = params[4];
    }
}
