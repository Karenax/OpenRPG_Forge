//! Preserve deformed GPU geometry while restricting the lists for local shadows.
use bevy::{
    camera::visibility::{CubemapVisibleEntities, VisibleMeshEntities},
    math::DVec3,
    prelude::*,
};

/// Conservative world-space envelope of the packed source mesh and all its LODs.
#[derive(Component, Clone, Copy)]
pub(super) struct PlantShadowBounds {
    center: DVec3,
    radius: f64,
}

impl PlantShadowBounds {
    pub(super) const fn new(center: DVec3, radius: f64) -> Self {
        Self { center, radius }
    }

    fn outside_light(&self, center: DVec3, range: f64) -> bool {
        self.center.distance_squared(center) > (self.radius + range).powi(2)
    }
}

/// Bevy's mesh bounds are local to the prototype. Keep its culling override on
/// the entity, then use the actual world envelope only for these shadow lists.
pub(super) fn prune_local_plant_casters(
    bounds: Query<&PlantShadowBounds>,
    mut point_lights: Query<(&PointLight, &GlobalTransform, &mut CubemapVisibleEntities)>,
    mut spot_lights: Query<(&SpotLight, &GlobalTransform, &mut VisibleMeshEntities)>,
) {
    let _profile = crate::cpu_timing::Span::new("shadows.plant_local_caster_range");
    point_lights
        .par_iter_mut()
        .for_each(|(light, transform, mut faces)| {
            if light.shadow_maps_enabled
                && let Some((center, range)) = light_reach(transform, light.range)
            {
                for face in faces.iter_mut() {
                    retain_casters(&bounds, face, center, range);
                }
            }
        });
    spot_lights
        .par_iter_mut()
        .for_each(|(light, transform, mut visible)| {
            if light.shadow_maps_enabled
                && let Some((center, range)) = light_reach(transform, light.range)
            {
                retain_casters(&bounds, &mut visible, center, range);
            }
        });
}

fn light_reach(transform: &GlobalTransform, range: f32) -> Option<(DVec3, f64)> {
    let center = transform.translation();
    (center.is_finite() && range.is_finite() && range > 0.0)
        .then(|| (center.as_dvec3(), f64::from(range)))
}

fn retain_casters(
    bounds: &Query<&PlantShadowBounds>,
    visible: &mut VisibleMeshEntities,
    center: DVec3,
    range: f64,
) {
    visible.entities.retain(|&entity| {
        bounds
            .get(entity)
            .map_or(true, |bound| !bound.outside_light(center, range))
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn local_shadow_lists_keep_touching_and_unbounded_meshes() {
        let mut world = World::new();
        let center = DVec3::new(40.0, 0.0, -115.0);
        let touching = world
            .spawn(PlantShadowBounds::new(center + DVec3::X * 12.0, 2.0))
            .id();
        let outside = world
            .spawn(PlantShadowBounds::new(center + DVec3::X * 13.0, 2.0))
            .id();
        let unbounded = world.spawn_empty().id();
        let visible = VisibleMeshEntities {
            entities: vec![touching, outside, unbounded],
        };
        let mut faces = CubemapVisibleEntities::default();
        *faces.get_mut(0) = visible.clone();
        *faces.get_mut(3) = visible.clone();
        let transform = GlobalTransform::from_translation(center.as_vec3());
        let point = world
            .spawn((
                PointLight {
                    range: 10.0,
                    shadow_maps_enabled: true,
                    ..default()
                },
                transform,
                faces,
            ))
            .id();
        let spot = world
            .spawn((
                SpotLight {
                    range: 10.0,
                    shadow_maps_enabled: true,
                    ..default()
                },
                transform,
                visible,
            ))
            .id();
        let mut schedule = Schedule::default();
        schedule.add_systems(prune_local_plant_casters);
        schedule.run(&mut world);
        let faces = world.get::<CubemapVisibleEntities>(point).unwrap();
        assert_eq!(faces.get(0).entities, [touching, unbounded]);
        assert_eq!(faces.get(3).entities, [touching, unbounded]);
        assert_eq!(
            world.get::<VisibleMeshEntities>(spot).unwrap().entities,
            [touching, unbounded]
        );
    }
}
