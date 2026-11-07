//! Explicit prepared inputs and resident outputs of the common environment graph.
use crate::{source_atmosphere, source_clouds, source_lighting::SceneLighting};
use bevy::prelude::*;

/// CPU-ready inputs; installation on Bevy's render device is a separate operation.
pub struct PreparedEnvironment {
    pub atmosphere: source_atmosphere::Input,
    pub atmosphere_params: [Vec4; 4],
    pub clouds: source_clouds::Input,
    pub cloud_view: [Vec4; 5],
    pub cloud_continuation: crate::source_clouds::Continuation,
    pub environment: Image,
    pub scene_lighting: SceneLighting,
    pub sky_shader: String,
}
/// Resident outputs, adopted together by the scene after the cloud graph is ready.
#[derive(Resource)]
pub struct ResidentEnvironment {
    pub atmosphere: source_atmosphere::Targets,
    pub clouds: source_clouds::Targets,
    pub cloud_view: [Vec4; 5],
    pub cloud_continuation: crate::source_clouds::Continuation,
    pub environment: Handle<Image>,
    pub environment_targets: crate::source_environment::resident::Targets,
    pub scene_lighting: SceneLighting,
}

impl PreparedEnvironment {
    pub fn stage(self, world: &mut World) -> Result<ResidentEnvironment, String> {
        source_atmosphere::ensure_registered(world)?;
        source_clouds::ensure_registered(world)?;
        crate::source_clouds::validation::validate(
            &self.clouds,
            10,
            Some(&self.atmosphere_params),
        )?;
        crate::source_sky::stage_shader(world, &self.sky_shader)?;
        let environment_targets = crate::source_environment::resident::stage(
            world,
            &self.environment,
            self.scene_lighting.environment_sh,
        )?;
        crate::source_environment::live::clear(world);
        crate::source_lighting::scene_buffer::publish(world, &self.scene_lighting)?;
        let atmosphere = source_atmosphere::stage(world, self.atmosphere)?;
        let clouds = source_clouds::stage(
            world,
            self.clouds,
            10,
            Some(source_clouds::SharedAtmosphere {
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
}
