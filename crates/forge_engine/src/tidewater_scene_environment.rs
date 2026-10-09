//! Prepare scene resources entirely from a verified, transport-neutral package.
use crate::{tidewater_atmosphere, tidewater_clouds, tidewater_material::SceneLighting};
use bevy::prelude::*;
use forge_assets::tidewater_environment::EnvironmentPackage;
use serde_json::Value;
use std::error::Error;

/// CPU-ready inputs; installation on Bevy's render device is a separate operation.
pub struct PreparedEnvironment {
    pub atmosphere: tidewater_atmosphere::Input,
    pub atmosphere_params: [Vec4; 4],
    pub clouds: tidewater_clouds::Input,
    pub cloud_view: [Vec4; 5],
    pub cloud_continuation: tidewater_clouds::acquisition_controller::Continuation,
    pub environment: Image,
    pub scene_lighting: SceneLighting,
    pub sky_shader: String,
}
/// Resident outputs, adopted together by the scene after the cloud graph is ready.
#[derive(Resource)]
pub struct ResidentEnvironment {
    pub atmosphere: tidewater_atmosphere::Targets,
    pub clouds: tidewater_clouds::Targets,
    pub cloud_view: [Vec4; 5],
    pub cloud_continuation: tidewater_clouds::acquisition_controller::Continuation,
    pub environment: Handle<Image>,
    pub environment_targets: crate::tidewater_environment::resident::Targets,
    pub scene_lighting: SceneLighting,
}

impl PreparedEnvironment {
    /// Stage validated resources on an already registered renderer.
    /// No filesystem access and no scene camera or authored document mutation.
    pub fn stage(self, world: &mut World) -> Result<ResidentEnvironment, String> {
        tidewater_atmosphere::ensure_registered(world)?;
        tidewater_clouds::ensure_registered(world)?;
        crate::tidewater_clouds::validation::validate(
            &self.clouds,
            10,
            Some(&self.atmosphere_params),
        )?;
        crate::tidewater_sky::stage_shader(world, &self.sky_shader)?;
        let environment_targets = crate::tidewater_environment::resident::stage(
            world,
            &self.environment,
            self.scene_lighting.environment_sh,
        )?;
        crate::tidewater_environment::live::clear(world);
        crate::tidewater_material::scene_buffer::publish(world, &self.scene_lighting)?;
        let atmosphere = tidewater_atmosphere::stage(world, self.atmosphere)?;
        let clouds = tidewater_clouds::stage(
            world,
            self.clouds,
            10,
            Some(tidewater_clouds::SharedAtmosphere {
                images: atmosphere.images.clone(),
                params: atmosphere.params,
                ready: atmosphere.ready.clone(),
            }),
        )?;
        let environment = environment_targets.displayed.clone();
        Ok(ResidentEnvironment {
            atmosphere,
            clouds,
            environment,
            environment_targets,
            cloud_view: self.cloud_view,
            cloud_continuation: self.cloud_continuation,
            scene_lighting: self.scene_lighting,
        })
    }

    #[allow(clippy::too_many_lines)] // Validate every dependency before publishing any GPU input.
    pub fn new(package: &EnvironmentPackage) -> Result<Self, Box<dyn Error>> {
        let file = |name: &str| {
            package
                .file(name)
                .ok_or_else(|| format!("Missing environment resource: {name}"))
        };
        let shader = |name: &str| -> Result<String, Box<dyn Error>> {
            Ok(std::str::from_utf8(file(name)?)?.to_owned())
        };
        let atmosphere_meta: Value = serde_json::from_slice(file("atmosphere.json")?)?;
        let atmosphere = tidewater_atmosphere::from_acquisition(&atmosphere_meta, false, |name| {
            file(name).map(<[u8]>::to_vec)
        })?;
        let atmosphere_params = vectors(file("params.bin")?)?;
        let meta: Value = serde_json::from_slice(file("cloud-environment.json")?)?;
        if meta["schema"] != "forge.tidewater.environment-probe"
            || meta["version"] != 1
            || meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        {
            return Err("Invalid cloud environment acquisition".into());
        }
        let (generated, cloud_continuation) =
            tidewater_clouds::acquisition_controller::generation_and_continuation(&meta)?;
        if generated.len() != package.bootstrap_frames() {
            return Err("Environment controller sequence differs from its package".into());
        }
        let cloud_view_bytes = crate::cloud_controller::bytes(
            &generated
                .last()
                .ok_or("Empty environment controller sequence")?
                .public,
        );
        if cloud_view_bytes != file("cloud-view-params.bin")? {
            return Err("Environment cloud view mismatch".into());
        }
        let cloud_view = vectors(&cloud_view_bytes)?;
        let atmosphere_bytes = file("params.bin")?;
        let mut frames = Vec::with_capacity(generated.len());
        let mut atmosphere_frames = Vec::with_capacity(generated.len());
        for (i, output) in generated.iter().enumerate() {
            let step = i + 1;
            let name = format!("cloud-step{step:03}-atmosphere.bin");
            let observer = checked(package, &meta, &name)?;
            if !crate::tidewater_atmosphere::observer::validate(observer, atmosphere_bytes) {
                return Err("Cloud observer differs from static atmosphere parameters".into());
            }
            atmosphere_frames.push(observer.to_vec());
            let global =
                checked(package, &meta, &format!("cloud-step{step:03}-global.bin"))?.to_vec();
            let local = crate::cloud_controller::bytes(&output.frame);
            let dimensions: Vec<_> = [1920_f32, 1080., 960., 540.]
                .into_iter()
                .flat_map(f32::to_le_bytes)
                .collect();
            if local.len() != 544 || local[128..144] != dimensions {
                return Err("Environment requires qualified FHD cloud dimensions".into());
            }
            frames.push((local, global));
        }
        let noise = (0..7)
            .map(|level| {
                checked(package, &meta, &format!("cloud-volume-m{level}.rgba8.bin"))
                    .map(<[u8]>::to_vec)
            })
            .collect::<Result<Vec<_>, _>>()?;
        for name in [
            "cloud-weather.wgsl",
            "cloud-bounds.wgsl",
            "cloud-shadow.wgsl",
            "cloud-trace.wgsl",
            "cloud-temporal.wgsl",
            "cloud-pano.wgsl",
        ] {
            checked(package, &meta, name)?;
        }
        let clouds = tidewater_clouds::Input {
            stepped: true,
            shaders: [shader("cloud-weather.wgsl")?, shader("cloud-bounds.wgsl")?],
            settings: crate::cloud_controller::bytes(&generated[0].settings),
            shadow: Some(tidewater_clouds::shadow::Input {
                shader: shader("cloud-shadow.wgsl")?,
                noise,
                schedule: Some(
                    generated
                        .iter()
                        .map(|output| tidewater_clouds::shadow::Schedule {
                            shadow_full: output.shadow_full,
                            panorama_full: output.panorama_full,
                            trace_view: output.trace_view,
                            view_index: output.view_index,
                        })
                        .collect(),
                ),
                frames,
                atmosphere_frames: atmosphere_frames
                    .iter()
                    .any(|params| params.as_slice() != atmosphere_bytes)
                    .then_some(atmosphere_frames),
                trace: Some(tidewater_clouds::trace::Input {
                    shader: shader("cloud-trace.wgsl")?,
                    atmosphere: atmosphere_bytes.to_vec(),
                    textures: vec![
                        Vec::new(),
                        Vec::new(),
                        checked(package, &meta, "cloud-blueNoise.bin")?.to_vec(),
                    ],
                    temporal: Some(shader("cloud-temporal.wgsl")?),
                    panorama: Some(shader("cloud-pano.wgsl")?),
                }),
            }),
        };
        tidewater_clouds::validation::validate(&clouds, 10, Some(&atmosphere_params))?;
        let lighting: Value = serde_json::from_slice(file("village-lighting.json")?)?;
        let scene_lighting = SceneLighting::new(
            serde_json::from_value(lighting["sun_direction"].clone())?,
            serde_json::from_value(lighting["sun_color"].clone())?,
            serde_json::from_value(lighting["environment_sh"].clone())?,
        )?;
        let sky_shader = shader("sky.wgsl")?;
        if sky_shader.trim().is_empty() || sky_shader.len() > 1_000_000 {
            return Err("Invalid sky shader size".into());
        }
        Ok(Self {
            atmosphere,
            atmosphere_params,
            clouds,
            cloud_view,
            cloud_continuation,
            scene_lighting,
            sky_shader,
            environment: crate::tidewater_environment::cube_rgba16f(
                128,
                6,
                file("environment-cube.bin")?,
            )?,
        })
    }
}
fn checked<'a>(
    package: &'a EnvironmentPackage,
    meta: &Value,
    name: &str,
) -> Result<&'a [u8], String> {
    let bytes = package
        .file(name)
        .ok_or_else(|| format!("Missing cloud dependency: {name}"))?;
    if meta["files"][name] != forge_core::bytes_sha256(bytes).to_string() {
        return Err(format!("Cloud acquisition pin differs: {name}"));
    }
    Ok(bytes)
}
fn vectors<const N: usize>(bytes: &[u8]) -> Result<[Vec4; N], String> {
    if bytes.len() != N * 16
        || bytes
            .as_chunks::<4>()
            .0
            .iter()
            .any(|b| !f32::from_le_bytes(*b).is_finite())
    {
        return Err("Invalid environment uniform size or value".into());
    }
    Ok(std::array::from_fn(|i| {
        Vec4::from_array(std::array::from_fn(|j| {
            f32::from_le_bytes(
                bytes[(i * 4 + j) * 4..(i * 4 + j + 1) * 4]
                    .try_into()
                    .expect("validated uniform size"),
            )
        }))
    }))
}
