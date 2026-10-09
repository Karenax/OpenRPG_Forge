//! Opt-in static comparison using the same verified environment package as Studio.
use bevy::{prelude::*, render::RenderApp};
use forge_assets::tidewater_environment::EnvironmentPackage;
use forge_engine::tidewater_scene_environment::PreparedEnvironment;
use std::{collections::BTreeMap, error::Error, path::PathBuf};

pub(super) struct Prepared {
    environment: PreparedEnvironment,
    receipt: Receipt,
}

#[derive(Resource, serde::Serialize)]
pub(super) struct Receipt {
    manifest_sha256: String,
    bootstrap_frames: usize,
    camera_checked: bool,
    replaced_scene_inputs: [&'static str; 4],
}

pub(super) fn load(
    files: &mut BTreeMap<String, Vec<u8>>,
    camera: Option<&super::CameraPose>,
    static_projection: bool,
) -> Result<Option<Prepared>, Box<dyn Error>> {
    let root = std::env::var_os("FORGE_TIDEWATER_STATIC_ENVIRONMENT_PACKAGE");
    let pin = std::env::var("FORGE_TIDEWATER_STATIC_ENVIRONMENT_SHA256").ok();
    let (root, pin) = match (root, pin) {
        (None, None) => return Ok(None),
        (Some(root), Some(pin)) => (PathBuf::from(root).canonicalize()?, pin),
        _ => return Err("Static environment requires package path and manifest hash".into()),
    };
    if !static_projection
        || std::env::var("FORGE_TIDEWATER_STATIC_SOURCE_FRAME").as_deref() != Ok("1")
    {
        return Err("Acquired environment is restricted to acquired static comparisons".into());
    }
    let expected: forge_core::ContentHash = pin.parse()?;
    let manifest = std::fs::read(root.join("package.json"))?;
    let package = EnvironmentPackage::load(&manifest, expected, |name, size| {
        let path = root.join(name).canonicalize().map_err(|e| e.to_string())?;
        if !path.starts_with(&root)
            || path.metadata().map_err(|e| e.to_string())?.len() != size as u64
        {
            return Err("Static environment payload path or size mismatch".into());
        }
        std::fs::read(path).map_err(|e| e.to_string())
    })?;
    let environment = PreparedEnvironment::new(&package)?;
    let camera = camera.ok_or("Acquired environment requires an explicit scene camera")?;
    let actual = &environment.cloud_continuation.input.camera;
    let rotation = Quat::from_array(camera.rotation).normalize();
    let close = |a: [f64; 3], b: Vec3| {
        a.into_iter()
            .zip(b.to_array())
            .all(|(a, b)| (a - f64::from(b)).abs() <= 1e-5)
    };
    if !close(actual.position, Vec3::from_array(camera.position))
        || !close(actual.right, rotation * Vec3::X)
        || !close(actual.up, rotation * Vec3::Y)
        || !close(actual.forward, rotation * Vec3::NEG_Z)
        || (actual.fov_degrees - f64::from(camera.fov_degrees)).abs() > 1e-5
    {
        return Err("Acquired environment final camera differs from the scene camera".into());
    }
    let replaced_scene_inputs = [
        "environment-cube.bin",
        "village-lighting.json",
        "cloud-view-params.bin",
        "sky.wgsl",
    ];
    // Validate everything before replacing the scene's derivative inputs.
    let replacements = replaced_scene_inputs
        .iter()
        .map(|name| {
            package
                .file(name)
                .map(|bytes| ((*name).to_owned(), bytes.to_vec()))
                .ok_or("Acquired environment scene input missing")
        })
        .collect::<Result<Vec<_>, _>>()?;
    files.extend(replacements);
    Ok(Some(Prepared {
        environment,
        receipt: Receipt {
            manifest_sha256: expected.to_string(),
            bootstrap_frames: package.bootstrap_frames(),
            camera_checked: true,
            replaced_scene_inputs,
        },
    }))
}

impl Prepared {
    pub(super) fn install(
        self,
        app: &mut App,
    ) -> Result<forge_engine::tidewater_clouds::Targets, Box<dyn Error>> {
        let environment = self.environment;
        let atmosphere = forge_engine::tidewater_atmosphere::install(app, environment.atmosphere)?;
        app.sub_app_mut(RenderApp).insert_resource(
            forge_engine::tidewater_clouds::SharedAtmosphere {
                images: atmosphere.images,
                ready: atmosphere.ready,
                params: atmosphere.params,
            },
        );
        let targets = forge_engine::tidewater_clouds::install(app, environment.clouds, 10)?;
        app.insert_resource(super::clouds::LiveSource(environment.cloud_continuation));
        app.insert_resource(self.receipt);
        Ok(targets)
    }
}
