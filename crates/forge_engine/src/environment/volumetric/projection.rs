use crate::environment::{EnvironmentQualityRequest, VisualValleyEnvironmentProfile};
use bevy::{
    light::{CascadeShadowConfig, VolumetricFog, VolumetricLight},
    prelude::*,
};

/// Experimental sun-lit preview. Default off; not a qualified quality preset.
#[derive(Default, Resource)]
pub struct VolumetricPreviewRequest {
    pub enabled: bool,
}

#[derive(Component)]
struct OwnedCamera;
#[derive(Component)]
struct OwnedLight;
#[derive(Component)]
pub(crate) struct OwnedVolume(pub f32);

fn prepare_for_shadow(
    world: &World,
    camera: Vec3,
    maximum: f32,
) -> Option<super::PreparedVolumetric> {
    let profile = world.get_resource::<VisualValleyEnvironmentProfile>()?;
    let mut settings = world
        .get_resource::<EnvironmentQualityRequest>()?
        .settings();
    // Outside shadow coverage Bevy attenuates fog but contributes no sunlight.
    settings.volumetric_fog_max_distance_m = settings.volumetric_fog_max_distance_m.min(maximum);
    super::prepare(profile.document.volumetric_fog, settings, camera)
}

fn effective_maximum(world: &World, shadow: f32) -> f32 {
    let quality = world.resource::<EnvironmentQualityRequest>().settings();
    let source = &world
        .resource::<VisualValleyEnvironmentProfile>()
        .document
        .volumetric_fog;
    shadow
        .min(quality.volumetric_fog_max_distance_m)
        .min(source.maximum_distance_m)
}

pub(crate) fn annotate(
    stats: &mut crate::environment::EnvironmentProjectionStats,
    maximum: Option<f32>,
) {
    // The requested budget follows the selected policy even when no preview volume exists
    // (Low, explicit preview-off, invalid topology). It is not a count of GPU steps executed.
    if let Some(quality) = stats.quality.as_ref() {
        stats.volumetric.requested_step_count = quality.requested.volumetric_fog_step_count;
    }
    let Some(maximum) = maximum else {
        return;
    };
    stats.volumetric.effective_maximum_distance_m = Some(maximum);
    stats.volumetric.applied = true;
    stats.volumetric.resolved_path = "volumetric_preview_plus_distance_fog_unqualified";
    if let Some(quality) = stats.quality.as_mut() {
        quality.volumetric_status = "cpu_projected_preview_gpu_unqualified";
    }
}

/// Exclusive reconciliation after propagation keeps the volume and camera in the same frame.
/// Foreign fog components are never replaced; distance fog and authored light values stay intact.
pub(crate) fn apply(world: &mut World) {
    let enabled = world
        .get_resource::<VolumetricPreviewRequest>()
        .is_some_and(|r| r.enabled);
    let cameras: Vec<_> = world
        .query_filtered::<(
            Entity,
            &Camera,
            &GlobalTransform,
            Has<VolumetricFog>,
            Has<OwnedCamera>,
        ), With<Camera3d>>()
        .iter(world)
        .filter(|(_, c, ..)| c.is_active)
        .map(|(e, _, t, fog, owned)| (e, t.translation(), fog, owned))
        .collect();
    let suns: Vec<_> = world
        .query_filtered::<(
            Entity,
            &DirectionalLight,
            Has<VolumetricLight>,
            &CascadeShadowConfig,
        ), With<crate::visual_valley::VisualValleySun>>()
        .iter(world)
        .map(|(e, light, marked, cascades)| {
            (
                e,
                light.shadow_maps_enabled,
                marked,
                cascades.bounds.last().copied().unwrap_or(0.0),
            )
        })
        .collect();
    let prepared = if enabled
        && cameras.len() == 1
        && (!cameras[0].2 || cameras[0].3)
        && suns.len() == 1
        && suns[0].1
        && suns[0].3.is_finite()
        && suns[0].3 > 0.0
    {
        prepare_for_shadow(world, cameras[0].1, suns[0].3)
    } else {
        None
    };
    let owned_lights: Vec<_> = world
        .query_filtered::<Entity, With<OwnedLight>>()
        .iter(world)
        .collect();
    for entity in owned_lights {
        if prepared.is_none() || entity != suns[0].0 {
            world
                .entity_mut(entity)
                .remove::<(OwnedLight, VolumetricLight)>();
        }
    }
    if prepared.is_some() && !suns[0].2 {
        world
            .entity_mut(suns[0].0)
            .insert((OwnedLight, VolumetricLight));
    }
    let owned_cameras: Vec<_> = world
        .query_filtered::<Entity, With<OwnedCamera>>()
        .iter(world)
        .collect();
    for entity in owned_cameras {
        if prepared.is_none() || entity != cameras[0].0 {
            world
                .entity_mut(entity)
                .remove::<(OwnedCamera, VolumetricFog)>();
        }
    }
    let volumes: Vec<_> = world
        .query_filtered::<Entity, With<OwnedVolume>>()
        .iter(world)
        .collect();
    let Some(mut prepared) = prepared else {
        for entity in volumes {
            world.despawn(entity);
        }
        return;
    };
    prepared.volume.density_texture = Some(super::density::handle(world));
    world
        .entity_mut(cameras[0].0)
        .insert((OwnedCamera, prepared.fog));
    let bundle = (
        OwnedVolume(effective_maximum(world, suns[0].3)),
        prepared.volume,
        prepared.transform,
        GlobalTransform::from(prepared.transform),
    );
    if let Some(entity) = volumes.first() {
        world.entity_mut(*entity).insert(bundle);
        for duplicate in volumes.iter().skip(1) {
            world.despawn(*duplicate);
        }
    } else {
        world.spawn((Name::new("Forge volumetric preview (unqualified)"), bundle));
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::light::FogVolume;
    use forge_project::VisualQualityTier;

    #[test]
    fn propagated_camera_and_volume_stay_in_the_same_frame() {
        let mut app = App::new();
        app.add_plugins(bevy::transform::TransformPlugin);
        app.init_resource::<Assets<Image>>();
        let source = forge_world::embedded_visual_valley_environment_profile().unwrap();
        app.insert_resource(VisualValleyEnvironmentProfile {
            source_hash: source.content_hash().unwrap().to_string(),
            document: source,
        });
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        app.insert_resource(
            EnvironmentQualityRequest::new(&terrain, policy, VisualQualityTier::High).unwrap(),
        );
        app.insert_resource(VolumetricPreviewRequest { enabled: true });
        app.add_systems(
            PostUpdate,
            apply.after(bevy::transform::TransformSystems::Propagate),
        );
        let camera = app
            .world_mut()
            .spawn((Camera3d::default(), Camera::default(), Transform::default()))
            .id();
        app.world_mut().spawn((
            crate::visual_valley::VisualValleySun,
            DirectionalLight {
                shadow_maps_enabled: true,
                ..default()
            },
            CascadeShadowConfig {
                bounds: vec![128.0],
                ..default()
            },
        ));
        // Large alternating jumps make a one-frame-old center fail the bound immediately.
        for position in [
            Vec3::new(-200.0, 5.0, 0.0),
            Vec3::new(200.0, 5.0, 0.0),
            Vec3::ZERO,
            Vec3::splat(4000.0),
            Vec3::new(0.0, 5.0, -200.0),
        ] {
            app.world_mut()
                .entity_mut(camera)
                .insert(Transform::from_translation(position));
            app.update();
            let world = app.world_mut();
            assert_eq!(
                world.get::<GlobalTransform>(camera).unwrap().translation(),
                position
            );
            let mut volumes =
                world.query_filtered::<(&Transform, &GlobalTransform), With<OwnedVolume>>();
            if position == Vec3::splat(4000.0) {
                assert_eq!(volumes.iter(world).count(), 0);
                assert!(world.get::<VolumetricFog>(camera).is_none());
            } else {
                let (local, global) = volumes.single(world).unwrap();
                assert_eq!(local.translation, global.translation());
                for x in [-0.5, 0.5] {
                    for y in [-0.5, 0.5] {
                        for z in [-0.5, 0.5] {
                            let corner = local.translation + local.scale * Vec3::new(x, y, z);
                            assert!(corner.distance(position) <= 128.0);
                        }
                    }
                }
                assert!(world.get::<VolumetricFog>(camera).is_some());
            }
        }
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "one world verifies the full ownership and quality transition lifecycle"
    )]
    fn preview_tracks_camera_and_cleans_only_owned_components() {
        let mut world = World::new();
        world.init_resource::<Assets<Image>>();
        let source = forge_world::embedded_visual_valley_environment_profile().unwrap();
        let hash = source.content_hash().unwrap().to_string();
        world.insert_resource(VisualValleyEnvironmentProfile {
            document: source,
            source_hash: hash.clone(),
        });
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        world.insert_resource(
            EnvironmentQualityRequest::new(&terrain, policy, VisualQualityTier::High).unwrap(),
        );
        world.insert_resource(VolumetricPreviewRequest { enabled: true });
        let camera = world
            .spawn((
                Camera3d::default(),
                Camera::default(),
                GlobalTransform::default(),
            ))
            .id();
        let sun = world
            .spawn((
                crate::visual_valley::VisualValleySun,
                DirectionalLight {
                    shadow_maps_enabled: true,
                    ..default()
                },
                CascadeShadowConfig {
                    bounds: vec![128.0],
                    ..default()
                },
            ))
            .id();
        let foreign = world.spawn(FogVolume::default()).id();
        apply(&mut world);
        let volume = world
            .query_filtered::<Entity, With<OwnedVolume>>()
            .single(&world)
            .unwrap();
        assert_eq!(world.get::<VolumetricFog>(camera).unwrap().step_count, 64);
        let transform = world.get::<Transform>(volume).unwrap();
        assert!((transform.translation + transform.scale * 0.5).length() <= 128.0);
        world
            .entity_mut(camera)
            .insert(GlobalTransform::from_translation(Vec3::new(
                100.0, 0.0, 0.0,
            )));
        apply(&mut world);
        assert_eq!(
            world
                .query_filtered::<Entity, With<OwnedVolume>>()
                .single(&world)
                .unwrap(),
            volume
        );
        assert!(world.get::<Transform>(volume).unwrap().translation.x > 0.0);
        world
            .resource_mut::<EnvironmentQualityRequest>()
            .request(VisualQualityTier::Low);
        apply(&mut world);
        assert!(world.get::<VolumetricFog>(camera).is_none());
        assert!(world.get::<FogVolume>(volume).is_none());
        assert!(world.get::<FogVolume>(foreign).is_some());
        assert!(world.get::<VolumetricLight>(sun).is_none());
        world
            .resource_mut::<EnvironmentQualityRequest>()
            .request(VisualQualityTier::High);
        apply(&mut world);
        assert!(world.get::<VolumetricFog>(camera).is_some());
        world.resource_mut::<VolumetricPreviewRequest>().enabled = false;
        apply(&mut world);
        assert!(world.get::<VolumetricFog>(camera).is_none());
        world.resource_mut::<VolumetricPreviewRequest>().enabled = true;
        world.entity_mut(camera).insert(VolumetricFog {
            step_count: 17,
            ..default()
        });
        world
            .resource_mut::<EnvironmentQualityRequest>()
            .request(VisualQualityTier::Ultra);
        apply(&mut world);
        assert_eq!(world.get::<VolumetricFog>(camera).unwrap().step_count, 17);
        assert!(
            world
                .query_filtered::<Entity, With<OwnedVolume>>()
                .iter(&world)
                .next()
                .is_none()
        );
        assert_eq!(
            world
                .resource::<VisualValleyEnvironmentProfile>()
                .document
                .content_hash()
                .unwrap()
                .to_string(),
            hash
        );
    }
}
