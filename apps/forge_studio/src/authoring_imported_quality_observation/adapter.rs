//! On-demand collection, using existing main-world state only.
use super::camera::camera_profile;
use super::domains::{
    environment_profile, shadow_profile, terrain_profile, vegetation_profile, water_profile,
};
use super::{CurrentProfileInventory, Observation, Origin, ParameterValue as V};
use bevy::prelude::*;
use forge_engine::ForgePlayerCamera;
use forge_project::authoring::playtest::PlaytestIdentity;

impl CurrentProfileInventory {
    pub(super) fn put(&mut self, key: impl Into<String>, value: V, origin: Origin, stable: bool) {
        self.observations
            .insert(key.into(), Observation::value(value, origin, stable));
    }

    pub(super) fn image(&mut self, world: &World, key: &str, handle: &Handle<Image>) {
        // Asset IDs identify handles in this App only, not content hashes/generations.
        self.put(
            format!("{key}/asset_id"),
            V::Text(format!("{:?}", handle.id())),
            Origin::MainWorldResource,
            false,
        );
        let descriptor = world
            .get_resource::<Assets<Image>>()
            .and_then(|images| images.get(handle));
        let key = format!("{key}/descriptor_size");
        if let Some(image) = descriptor {
            let size = image.texture_descriptor.size;
            self.put(
                key,
                V::Dimensions(vec![size.width, size.height, size.depth_or_array_layers]),
                Origin::CpuImageDescriptor,
                true,
            );
        } else {
            self.observations.insert(
                key,
                Observation::unavailable("image_asset_unavailable", true),
            );
        }
    }

    pub(super) fn number(&mut self, key: &str, number: f32, origin: Origin, stable: bool) {
        self.put(key, V::Number(f64::from(number)), origin, stable);
    }

    pub(super) fn numbers(
        &mut self,
        key: &str,
        numbers: impl IntoIterator<Item = f32>,
        origin: Origin,
        stable: bool,
    ) {
        self.put(
            key,
            V::Numbers(numbers.into_iter().map(f64::from).collect()),
            origin,
            stable,
        );
    }
}

/// Called only on start/stop of a bounded collection, never as a per-frame system.
pub(crate) fn snapshot(
    world: &mut World,
    expected: Option<PlaytestIdentity>,
) -> CurrentProfileInventory {
    let frame = world
        .get_resource::<bevy::diagnostic::FrameCount>()
        .map(|frame| frame.0);
    let mut inventory = CurrentProfileInventory::new(expected, frame);
    let cameras: Vec<_> = world
        .query_filtered::<(Entity, &Camera), (
            With<ForgePlayerCamera>,
            Without<forge_tidewater::tidewater_refraction::CaptureCamera>,
        )>()
        .iter(world)
        .filter(|(_, camera)| camera.is_active)
        .map(|(entity, _)| entity)
        .collect();
    inventory.put(
        "camera/active_player_count",
        V::Count(cameras.len()),
        Origin::MainWorldComponent,
        true,
    );
    if let [camera] = cameras.as_slice() {
        camera_profile(world, *camera, &mut inventory);
    }
    terrain_profile(world, &mut inventory);
    shadow_profile(world, &mut inventory);
    vegetation_profile(world, &mut inventory);
    environment_profile(world, &mut inventory);
    water_profile(world, &mut inventory);
    inventory.put(
        "rain/weather_runtime_present",
        V::Boolean(world.contains_resource::<forge_engine::WeatherRuntimeState>()),
        Origin::MainWorldResource,
        true,
    );
    inventory
}
