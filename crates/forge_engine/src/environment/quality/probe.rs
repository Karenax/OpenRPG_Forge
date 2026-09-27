use bevy::{
    light::{AtmosphereEnvironmentMapLight, EnvironmentMapLight, GeneratedEnvironmentMapLight},
    prelude::*,
};
#[cfg(test)]
mod tests;

/// Owns the lifetime of Bevy's private cubemap cache without accessing its component type.
#[derive(Component)]
pub(crate) struct OwnedSkyProbe;

#[derive(Resource, Default)]
pub(crate) struct SkyProbeState {
    pub entity: Option<Entity>,
    pub size: Option<UVec2>,
    pub camera: Option<Entity>,
    pub texture_bound: bool,
}

impl SkyProbeState {
    pub(super) fn request(
        &mut self,
        commands: &mut Commands,
        camera: Entity,
        map: Option<AtmosphereEnvironmentMapLight>,
    ) {
        let next_size = map.as_ref().map(|map| map.size);
        if self.camera == Some(camera) && self.size == next_size {
            return;
        }
        let retain_previous_lighting =
            self.camera == Some(camera) && self.entity.is_some() && next_size.is_some();
        if let Some(previous) = self.entity.take() {
            commands.entity(previous).despawn();
        }
        if let Some(previous_camera) = self.camera
            && previous_camera != camera
        {
            commands
                .entity(previous_camera)
                .remove::<(GeneratedEnvironmentMapLight, EnvironmentMapLight)>();
        }
        self.camera = Some(camera);
        self.texture_bound = false;
        self.size = next_size;
        self.entity = map.map(|map| {
            commands
                .spawn((Name::new("Forge owned sky cubemap"), OwnedSkyProbe, map))
                .id()
        });
        // Keep the last lighting only while replacing our own probe on the same camera.
        // texture_bound remains false until publish validates the replacement; the old
        // map is continuity of presentation, never acknowledgement of the new budget.
        // Do not keep a generator on the persistent camera (its private cache cannot resize).
        commands
            .entity(camera)
            .remove::<GeneratedEnvironmentMapLight>();
        if !retain_previous_lighting {
            commands.entity(camera).remove::<EnvironmentMapLight>();
        }
    }
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameter ownership"
)]
pub(crate) fn publish(
    mut commands: Commands,
    mut state: ResMut<SkyProbeState>,
    probes: Query<(&GeneratedEnvironmentMapLight, &EnvironmentMapLight), With<OwnedSkyProbe>>,
    cameras: Query<(&Camera, Option<&EnvironmentMapLight>), With<Camera3d>>,
    images: Option<Res<Assets<Image>>>,
) {
    state.texture_bound = false;
    let (Some(probe), Some(camera), Some(size)) = (state.entity, state.camera, state.size) else {
        return;
    };
    let (Ok((generated, filtered)), Ok((active, current))) =
        (probes.get(probe), cameras.get(camera))
    else {
        return;
    };
    if !active.is_active
        || cameras
            .iter()
            .filter(|(camera, _)| camera.is_active)
            .count()
            != 1
    {
        return;
    }
    let Some(images) = images else {
        return;
    };
    let Some(image) = images.get(&generated.environment_map) else {
        return;
    };
    let extent = image.texture_descriptor.size;
    if extent.width != size.x || extent.height != size.y || extent.depth_or_array_layers != 6 {
        return;
    }
    let Some(specular) = images.get(&filtered.specular_map) else {
        return;
    };
    let extent = specular.texture_descriptor.size;
    if extent.width != size.x
        || extent.height != size.y
        || extent.depth_or_array_layers != 6
        || images.get(&filtered.diffuse_map).is_none()
    {
        return;
    }
    // Filtering belongs to the replaceable probe. Starting a second generator on the
    // persistent camera would retain its old filtered texture allocation across resizes.
    if current.is_none_or(|current| !same_binding(current, filtered)) {
        commands.entity(camera).insert(filtered.clone());
    }
    state.texture_bound = true;
}

fn same_binding(current: &EnvironmentMapLight, next: &EnvironmentMapLight) -> bool {
    current.diffuse_map.id() == next.diffuse_map.id()
        && current.specular_map.id() == next.specular_map.id()
        && current.intensity.to_bits() == next.intensity.to_bits()
        && current.rotation == next.rotation
        && current.affects_lightmapped_mesh_diffuse == next.affects_lightmapped_mesh_diffuse
}
