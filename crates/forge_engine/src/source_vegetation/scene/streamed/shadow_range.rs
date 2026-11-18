//! Preserve deformed GPU geometry while restricting Bevy's shadow lists.
use crate::{
    source_terrain::{CdlodReceivesSourceSun, lod::LodMaterial},
    source_vegetation::world::WorldPlantMaterial,
};
use bevy::{
    camera::{
        primitives::Aabb,
        visibility::{
            CascadesVisibleEntities, CubemapVisibleEntities, NoAutoAabb, NoFrustumCulling,
            VisibleMeshEntities,
        },
    },
    light::{NotShadowReceiver, VolumetricLight},
    math::DVec3,
    prelude::*,
};

type NativeSunReceivers = (
    With<Mesh3d>,
    Without<NotShadowReceiver>,
    Without<super::StreamedPlantPart>,
    Without<MeshMaterial3d<crate::source_lighting::surfaces::ModelMaterial>>,
    Without<MeshMaterial3d<crate::source_lighting::surfaces::TerrainMaterial>>,
    Without<MeshMaterial3d<crate::source_lighting::surfaces::VegetationMaterial>>,
    Without<MeshMaterial3d<crate::source_surface::SourceSurfaceMaterial>>,
    Without<MeshMaterial3d<crate::source_surface::stall_material::FullMaterial>>,
    Without<MeshMaterial3d<crate::source_fish::material::FishMaterial>>,
    Without<MeshMaterial3d<crate::source_water_optics::OpticalMaterial>>,
    Without<MeshMaterial3d<crate::source_breaker_render::LipMaterial>>,
    Without<MeshMaterial3d<crate::source_spray_render::SprayMaterial>>,
);

type NativeSunReceiver = (
    Entity,
    &'static InheritedVisibility,
    Option<&'static Name>,
    Has<MeshMaterial3d<StandardMaterial>>,
    Has<MeshMaterial3d<LodMaterial>>,
    Option<&'static MeshMaterial3d<WorldPlantMaterial>>,
);

/// Skip the engine sun's Bevy caster lists while the source sun owns all
/// visible receivers. Unknown materials keep the complete Bevy caster lists.
/// Last runs after light visibility and volumetric projection, before extraction.
#[expect(
    clippy::needless_pass_by_value,
    reason = "Bevy system parameters fetch the optional terrain marker by value"
)]
pub(super) fn omit_unused_bevy_sun_casters(
    status: Option<Res<super::host::HostStatus>>,
    source_error: Option<Res<crate::source_lighting::surfaces::live::Error>>,
    plant_materials: Option<Res<Assets<WorldPlantMaterial>>>,
    cdlod_source_sun: Option<Res<CdlodReceivesSourceSun>>,
    receivers: Query<NativeSunReceiver, NativeSunReceivers>,
    mut suns: Query<
        &mut CascadesVisibleEntities,
        (
            With<crate::visual_valley::VisualValleySun>,
            Without<VolumetricLight>,
        ),
    >,
    mut last_route: Local<Option<(bool, Option<Entity>)>>,
) {
    if !status.is_some_and(|state| {
        state.ready
            && state.surfaces.ready
            && state.surfaces.motion_matches
            && state.surfaces.plant_batches > 0
    }) || source_error.is_none_or(|error| error.0.is_some())
    {
        return;
    }
    let plant_materials = plant_materials.map(Res::into_inner);
    let blocker = receivers.iter().find(|(_, visible, _, _, lod, material)| {
        visible.get()
            && !(*lod && cdlod_source_sun.is_some())
            && material
                .and_then(|handle| plant_materials?.get(&handle.0))
                .is_none_or(|material| !material.source_lighting)
    });
    if crate::cpu_timing::enabled() {
        let eligible_sun = !suns.is_empty();
        let route = (eligible_sun, blocker.map(|receiver| receiver.0));
        if *last_route != Some(route) {
            *last_route = Some(route);
            tracing::info!(
                event_kind = "runtime_source_sun_shadow_route",
                eligible_sun,
                receiver = ?route.1,
                name = ?blocker.and_then(|receiver| receiver.2),
                standard_material = blocker.is_some_and(|receiver| receiver.3),
                terrain_lod = blocker.is_some_and(|receiver| receiver.4),
                world_plant = blocker.is_some_and(|receiver| receiver.5.is_some()),
                "Source sun directional caster routing"
            );
        }
    }
    if blocker.is_some() {
        return;
    }
    for mut views in &mut suns {
        for cascades in views.entities.values_mut() {
            for cascade in cascades {
                cascade.entities.clear();
            }
        }
    }
}

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

    /// Plant vertices already contain world coordinates and the resident entity
    /// has an identity transform. Round the bound outward for Bevy's f32 culling.
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Bevy bounds use f32; extents round outward"
    )]
    pub(super) fn render_aabb(self) -> Option<Aabb> {
        let center = self.center.as_vec3();
        let center_error = (self.center - center.as_dvec3()).abs().max_element();
        let extent = ((self.radius + center_error) as f32).next_up();
        (center.is_finite() && extent.is_finite() && extent > 0.0).then_some(Aabb {
            center: center.into(),
            half_extents: bevy::math::Vec3A::splat(extent),
        })
    }
}

pub(in crate::source_vegetation::scene) fn plant_culling_intact(
    world: &World,
    entity: Entity,
) -> bool {
    match world
        .get::<PlantShadowBounds>(entity)
        .and_then(|bound| bound.render_aabb())
    {
        Some(bounds) => {
            world.get::<Aabb>(entity) == Some(&bounds)
                && world.get::<NoAutoAabb>(entity).is_some()
                && world.get::<NoFrustumCulling>(entity).is_none()
        }
        None => world.get::<NoFrustumCulling>(entity).is_some(),
    }
}

/// Prune beyond the tighter sphere after Bevy's conservative box culling.
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
    fn bounded_plant_uses_an_outward_rounded_box_and_rejects_a_replaced_box() {
        let mut world = World::new();
        let bound = PlantShadowBounds::new(DVec3::new(12_345.678_901, 4.5, -115.0), 1.234_567_89);
        let expected = bound.render_aabb().unwrap();
        let rounded = Vec3::from(expected.center).as_dvec3();
        let center_error = (bound.center - rounded).abs().max_element();
        assert!(f64::from(expected.half_extents.x) >= bound.radius + center_error);
        let plant = world.spawn(NoFrustumCulling).id();
        assert!(plant_culling_intact(&world, plant));
        world
            .entity_mut(plant)
            .insert((bound, expected, NoAutoAabb))
            .remove::<NoFrustumCulling>();
        assert!(plant_culling_intact(&world, plant));
        world.entity_mut(plant).insert(Aabb::default());
        assert!(!plant_culling_intact(&world, plant));
    }

    #[test]
    fn source_sun_preserves_bevy_casters_when_a_native_receiver_needs_them() {
        use super::super::{StreamedPlantPart, host::HostStatus, tests as fixtures};
        use crate::source_lighting::surfaces::{live::Error, scene::Status};
        use forge_world::imported_vegetation::streaming::PlantRepresentation;

        let (mut world, reference) = fixtures::world();
        world.insert_resource(HostStatus {
            ready: true,
            surfaces: Status {
                ready: true,
                motion_matches: true,
                plant_batches: 1,
                ..default()
            },
            ..default()
        });
        world.init_resource::<Error>();
        let plant = world
            .spawn(StreamedPlantPart {
                id: forge_core::EntityId::deterministic("test plant"),
                representation: PlantRepresentation::Detail,
                source_lod: 0,
            })
            .id();
        let other = world.spawn_empty().id();
        let camera = world.spawn_empty().id();
        let cascade = || CascadesVisibleEntities {
            entities: [(
                camera,
                vec![VisibleMeshEntities {
                    entities: vec![plant, other],
                }],
            )]
            .into_iter()
            .collect(),
        };
        let sun = world
            .spawn((crate::visual_valley::VisualValleySun, cascade()))
            .id();
        let foreign = world.spawn(cascade()).id();
        let receiver = world
            .spawn((Mesh3d::default(), InheritedVisibility::VISIBLE))
            .id();
        let mut schedule = Schedule::default();
        schedule.add_systems(omit_unused_bevy_sun_casters);
        schedule.run(&mut world);
        let list = |world: &World, light| {
            world
                .get::<CascadesVisibleEntities>(light)
                .unwrap()
                .entities[&camera][0]
                .entities
                .clone()
        };
        assert_eq!(list(&world, sun), [plant, other]);
        world
            .entity_mut(receiver)
            .insert(MeshMaterial3d::<WorldPlantMaterial>::default());
        schedule.run(&mut world);
        assert_eq!(list(&world, sun), [plant, other]);
        let mut grass = fixtures::world_material(&mut world, reference);
        grass.kind = 3;
        let grass = world
            .resource_mut::<Assets<WorldPlantMaterial>>()
            .add(grass);
        world
            .entity_mut(receiver)
            .insert(MeshMaterial3d(grass.clone()));
        schedule.run(&mut world);
        assert_eq!(list(&world, sun), [plant, other]);
        world
            .resource_mut::<Assets<WorldPlantMaterial>>()
            .get_mut(&grass)
            .unwrap()
            .source_lighting = true;
        world.entity_mut(sun).insert(VolumetricLight);
        schedule.run(&mut world);
        assert_eq!(list(&world, sun), [plant, other]);
        world.entity_mut(sun).remove::<VolumetricLight>();
        world.resource_mut::<Error>().0 = Some("unavailable".into());
        schedule.run(&mut world);
        assert_eq!(list(&world, sun), [plant, other]);
        world.resource_mut::<Error>().0 = None;
        world.spawn((
            Mesh3d::default(),
            InheritedVisibility::VISIBLE,
            MeshMaterial3d::<LodMaterial>::default(),
        ));
        schedule.run(&mut world);
        assert_eq!(list(&world, sun), [plant, other]);
        world.insert_resource(CdlodReceivesSourceSun);
        world.spawn((
            Mesh3d::default(),
            InheritedVisibility::VISIBLE,
            MeshMaterial3d::<crate::source_water_optics::OpticalMaterial>::default(),
        ));
        schedule.run(&mut world);
        assert!(list(&world, sun).is_empty());
        assert_eq!(list(&world, foreign), [plant, other]);
    }

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
