//! Prototype scene settings and cloud bindings for the shared sky renderer.
use bevy::prelude::*;
pub use forge_tidewater::tidewater_sky::SkyMaterial;
use forge_tidewater::tidewater_sky::{self, Settings};
#[derive(Resource)]
pub struct CloudInputs {
    pub params: [Vec4; 5],
    pub panorama: Handle<Image>,
    pub shadow: Handle<Image>,
    pub(super) view: Handle<Image>,
}
pub fn install_clouds(
    app: &mut App,
    bytes: &[u8],
    targets: super::clouds::Targets,
) -> Result<(), Box<dyn std::error::Error>> {
    if bytes.len() != 80 {
        return Err("cloud view parameters size".into());
    }
    let params = std::array::from_fn(|i| {
        Vec4::from_array(std::array::from_fn(|j| {
            f32::from_le_bytes(
                bytes[(i * 4 + j) * 4..(i * 4 + j + 1) * 4]
                    .try_into()
                    .expect("four bytes"),
            )
        }))
    });
    app.insert_resource(CloudInputs {
        params,
        panorama: targets.images[9].clone(),
        shadow: targets.images[2].clone(),
        view: targets.images[7].clone(),
    });
    app.add_systems(PostStartup, bind_opaque_clouds);
    Ok(())
}
#[allow(clippy::needless_pass_by_value)]
pub fn setup(
    mut commands: Commands,
    atmosphere: Option<Res<super::atmosphere::Targets>>,
    clouds: Option<Res<CloudInputs>>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<SkyMaterial>>,
) {
    let Some(atmosphere) = atmosphere else {
        return;
    };
    let sun = atmosphere.params[3].truncate();
    let moon = Vec3::new(-sun.x, sun.y.abs() * 0.8 + 0.25, -sun.z).normalize();
    let material = SkyMaterial {
        clouds: clouds.as_ref().map_or([Vec4::ZERO; 5], |v| v.params),
        panorama: clouds.as_ref().map(|v| v.panorama.clone()),
        cloud_view: clouds.as_ref().map(|v| v.view.clone()),
        atmosphere: atmosphere.params,
        settings: Settings {
            moon_disk: moon.extend(1.0),
            stars_time_night: Vec4::new(0.0, 2.0, 0.0, 0.0),
        },
        transmittance: atmosphere.images[0].clone(),
        sky: atmosphere.images[2].clone(),
    };
    tidewater_sky::spawn(&mut commands, &mut meshes, &mut materials, material);
}

// All opaque materials are created during Startup, including CDLOD clones.
#[allow(clippy::needless_pass_by_value)]
fn bind_opaque_clouds(
    clouds: Res<CloudInputs>,
    mut plants: ResMut<Assets<super::PlantMaterial>>,
    mut village: ResMut<Assets<super::village::VillageMaterial>>,
    mut terrain: ResMut<Assets<super::terrain_surface::TerrainMaterial>>,
    mut lod: ResMut<Assets<super::terrain_lod::LodMaterial>>,
) {
    for (_, m) in plants.iter_mut() {
        m.clouds = clouds.params;
        m.cloud_shadow = Some(clouds.shadow.clone());
    }
    for (_, m) in village.iter_mut() {
        m.clouds = clouds.params;
        m.cloud_shadow = Some(clouds.shadow.clone());
    }
    for (_, m) in terrain.iter_mut() {
        m.clouds = clouds.params;
        m.cloud_shadow = Some(clouds.shadow.clone());
    }
    for (_, m) in lod.iter_mut() {
        m.base.clouds = clouds.params;
        m.base.cloud_shadow = Some(clouds.shadow.clone());
    }
}
