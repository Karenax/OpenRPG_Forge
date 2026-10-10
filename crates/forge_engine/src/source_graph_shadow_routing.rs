//! Derived render entities for admitted static source meshes, never new assets.
//! CPU routing checks do not qualify source materials, images or GPU shadows.
use super::{
    App, Assets, Camera, ChildOf, Children, Component, DirectionalLight, Entity, Handle,
    IntoScheduleConfigs, Mesh, Mesh3d, MeshMaterial3d, Plugin, PointLight, PostUpdate,
    RelationshipTarget, Resource, Result, SourceGraphMaterial, SpotLight, String, ToOwned,
    Transform, Vec, Visibility, With, World, source_graph_shadow_only_material,
};
use bevy::{
    camera::visibility::{
        NoCpuCulling, NoFrustumCulling, RenderLayers, VisibilityRange, VisibilitySystems,
    },
    light::NotShadowCaster,
    mesh::{morph::MorphWeights, skinning::SkinnedMesh},
    transform::TransformSystems,
};
use std::collections::BTreeMap;
#[derive(Component, Clone, Debug)]
pub struct SourceGraphShadowRoute {
    pub proxy: Entity,
    pub private_layer: usize,
    pub source_layers: RenderLayers,
    pub shadow_material: Handle<SourceGraphMaterial>,
    pub enabled: bool,
}
#[derive(Component, Clone, Debug, PartialEq, Eq)]
pub enum SourceGraphShadowRouteStatus {
    Ready,
    Disabled,
    Refused(String),
}
#[derive(Component, Clone)]
struct SourceGraphShadowProxy {
    owner: Entity,
    private_layer: usize,
}
/// Transient author enablement retained while the derived route is detached.
/// Original native casting stays disabled to avoid a camera-threshold fallback.
#[derive(Component, Clone)]
struct SourceGraphShadowDetached {
    author_enabled: bool,
}
#[derive(Resource, Default)]
struct SourceGraphShadowGroups {
    layers: BTreeMap<usize, RenderLayers>,
}
pub struct SourceGraphShadowRoutingPlugin;
impl Plugin for SourceGraphShadowRoutingPlugin {
    fn build(&self, app: &mut App) {
        app.init_resource::<SourceGraphShadowGroups>();
        app.add_systems(
            PostUpdate,
            sync_source_graph_shadow_routes
                .before(TransformSystems::Propagate)
                .before(VisibilitySystems::CheckVisibility),
        );
    }
}
fn source_layers(world: &World, e: Entity) -> RenderLayers {
    world.get::<RenderLayers>(e).cloned().unwrap_or_default()
}
fn light(world: &World, e: Entity) -> bool {
    world.get::<DirectionalLight>(e).is_some()
        || world.get::<PointLight>(e).is_some()
        || world.get::<SpotLight>(e).is_some()
}
fn static_mesh(world: &World, e: Entity) -> Result<Mesh3d, &'static str> {
    if world.get::<NoCpuCulling>(e).is_some() {
        return Err("Source GPU-transformed culling requires a qualified shadow bridge");
    }
    if world.get::<SkinnedMesh>(e).is_some() || world.get::<MorphWeights>(e).is_some() {
        return Err("Source skin/morph shadow routing requires a qualified animated recipe");
    }
    let mesh = world
        .get::<Mesh3d>(e)
        .cloned()
        .ok_or("Source mesh missing")?;
    let meshes = world
        .get_resource::<Assets<Mesh>>()
        .ok_or("Source mesh assets missing")?;
    let loaded = meshes.get(&mesh.0).ok_or("Source mesh is not loaded")?;
    if loaded
        .try_has_morph_targets()
        .map_err(|_| "Source mesh CPU metadata unavailable")?
    {
        return Err("Source morph geometry requires a qualified shadow recipe");
    }
    Ok(mesh)
}
fn camera_conflict(world: &mut World, layer: usize) -> bool {
    let private = RenderLayers::from_layers(&[layer]);
    world
        .query_filtered::<Option<&RenderLayers>, With<Camera>>()
        .iter(world)
        .any(|l| l.cloned().unwrap_or_default().intersects(&private))
}
fn private_layer_conflict(world: &mut World, layer: usize) -> bool {
    if camera_conflict(world, layer) {
        return true;
    }
    let private = RenderLayers::from_layers(&[layer]);
    world
        .query::<(Entity, &RenderLayers)>()
        .iter(world)
        .any(|(e, l)| {
            l.intersects(&private)
                && !light(world, e)
                && world
                    .get::<SourceGraphShadowProxy>(e)
                    .is_none_or(|p| p.private_layer != layer)
        })
}
fn reconcile_source_shadow_light_layers(world: &mut World) {
    let claimed: Vec<usize> = world
        .get_resource::<SourceGraphShadowGroups>()
        .map(|g| g.layers.keys().copied().collect())
        .unwrap_or_default();
    let active: Vec<_> = world
        .query::<(&SourceGraphShadowRoute, &SourceGraphShadowRouteStatus)>()
        .iter(world)
        .filter(|(r, s)| r.enabled && **s == SourceGraphShadowRouteStatus::Ready)
        .map(|(r, _)| (r.private_layer, r.source_layers.clone()))
        .collect();
    let lights: Vec<_> = world
        .query::<(Entity, Option<&RenderLayers>)>()
        .iter(world)
        .filter(|(e, _)| light(world, *e))
        .map(|(e, l)| (e, l.cloned().unwrap_or_default()))
        .collect();
    for (e, current) in lights {
        // Only renderer-owned private bits are removed. All current author layers,
        // including edits since installation, remain the authoritative base mask.
        let mut base = current.clone();
        for layer in &claimed {
            base = base.without(*layer);
        }
        let mut desired = base.clone();
        for (layer, source) in &active {
            if base.intersects(source) {
                desired = desired.with(*layer);
            }
        }
        if desired != current {
            world.entity_mut(e).insert(desired);
        }
    }
}
fn affected_lights(world: &mut World, source: &RenderLayers) -> Vec<(Entity, RenderLayers)> {
    world
        .query::<(Entity, Option<&RenderLayers>)>()
        .iter(world)
        .filter(|(e, l)| light(world, *e) && l.cloned().unwrap_or_default().intersects(source))
        .map(|(e, l)| (e, l.cloned().unwrap_or_default()))
        .collect()
}
/// Exclusive and fail-closed: validate before creating a material/entity or
/// changing a light. Private layers are claimed by a source layer group; two
/// differing source groups cannot share one private layer. No camera is changed.
pub fn install_source_graph_shadow_route(
    world: &mut World,
    original: Entity,
    private_layer: usize,
) -> Result<Entity, &'static str> {
    if private_layer > 4095 {
        return Err("Source private layer exceeds governed routing limit");
    }
    if let Some(route) = world.get::<SourceGraphShadowRoute>(original) {
        if route.private_layer == private_layer
            && route.source_layers == source_layers(world, original)
        {
            return Ok(route.proxy);
        }
        return Err("Source shadow route already exists with different layers");
    }
    let mesh = static_mesh(world, original)?;
    if world.get::<NotShadowCaster>(original).is_some()
        && !world
            .get::<SourceGraphShadowDetached>(original)
            .is_some_and(|s| s.author_enabled)
    {
        return Err("Original source shadows are disabled; do not override");
    }
    let original_layers = source_layers(world, original);
    let private = RenderLayers::from_layers(&[private_layer]);
    if original_layers.intersects(&private) || camera_conflict(world, private_layer) {
        return Err("Private shadow layer is visible to an original mesh or camera");
    }
    let claimed = world
        .get_resource::<SourceGraphShadowGroups>()
        .and_then(|g| g.layers.get(&private_layer))
        .cloned();
    if claimed.as_ref().is_some_and(|g| g != &original_layers) {
        return Err("Private shadow layer already belongs to a different source layer group");
    }
    for (entity, layers) in world.query::<(Entity, &RenderLayers)>().iter(world) {
        if layers.intersects(&private) {
            let managed = world
                .get::<SourceGraphShadowProxy>(entity)
                .is_some_and(|p| p.private_layer == private_layer);
            if claimed.is_none() || (!managed && !light(world, entity)) {
                return Err("Private shadow layer is occupied by unrelated scene content");
            }
        }
    }
    let material_handle = world
        .get::<MeshMaterial3d<SourceGraphMaterial>>(original)
        .ok_or("Source material missing")?
        .0
        .clone();
    let original_material = world
        .get_resource::<Assets<SourceGraphMaterial>>()
        .and_then(|a| a.get(&material_handle))
        .cloned()
        .ok_or("Source material is not loaded")?;
    let shadow = source_graph_shadow_only_material(&original_material)?;
    let lights = affected_lights(world, &original_layers);
    // All failures above leave source entities, materials and lights unchanged.
    let shadow_handle = world
        .resource_mut::<Assets<SourceGraphMaterial>>()
        .add(shadow);
    retain_source_shadow_cpu_metadata(world, &mesh);
    let proxy = world
        .spawn((
            mesh,
            MeshMaterial3d(shadow_handle.clone()),
            Transform::IDENTITY,
            Visibility::Inherited,
            ChildOf(original),
            private,
            SourceGraphShadowProxy {
                owner: original,
                private_layer,
            },
        ))
        .id();
    copy_source_shadow_visibility(world, original, proxy);
    world.entity_mut(original).insert((
        NotShadowCaster,
        SourceGraphShadowRoute {
            proxy,
            private_layer,
            source_layers: original_layers.clone(),
            shadow_material: shadow_handle,
            enabled: true,
        },
        SourceGraphShadowRouteStatus::Ready,
    ));
    world
        .entity_mut(original)
        .remove::<SourceGraphShadowDetached>();
    world.init_resource::<SourceGraphShadowGroups>();
    world
        .resource_mut::<SourceGraphShadowGroups>()
        .layers
        .insert(private_layer, original_layers);
    for (e, layers) in lights {
        world.entity_mut(e).insert(layers.with(private_layer));
    }
    Ok(proxy)
}
fn copy_source_shadow_visibility(world: &mut World, original: Entity, proxy: Entity) {
    let range = world.get::<VisibilityRange>(original).cloned();
    let no_frustum = world.get::<NoFrustumCulling>(original).is_some();
    let mut entity = world.entity_mut(proxy);
    if let Some(range) = range {
        entity.insert(range);
    } else {
        entity.remove::<VisibilityRange>();
    }
    if no_frustum {
        entity.insert(NoFrustumCulling);
    } else {
        entity.remove::<NoFrustumCulling>();
    }
}
fn retain_source_shadow_cpu_metadata(world: &mut World, mesh: &Mesh3d) {
    let needs = world
        .resource::<Assets<Mesh>>()
        .get(&mesh.0)
        .is_some_and(|a| {
            !a.asset_usage
                .contains(bevy::asset::RenderAssetUsages::MAIN_WORLD)
        });
    if needs && let Some(mut asset) = world.resource_mut::<Assets<Mesh>>().get_mut(&mesh.0) {
        asset.asset_usage |= bevy::asset::RenderAssetUsages::MAIN_WORLD;
    }
}
/// Detach only renderer-owned data, preserving originals and all borrowed assets.
/// Keep `NotShadowCaster` until an admitted explicit role is mounted again.
pub fn uninstall_source_graph_shadow_route(
    world: &mut World,
    original: Entity,
) -> Result<bool, &'static str> {
    let Some(route) = world.get::<SourceGraphShadowRoute>(original).cloned() else {
        return Ok(false);
    };
    if world.get_entity(route.proxy).is_ok() {
        if !world
            .get::<SourceGraphShadowProxy>(route.proxy)
            .is_some_and(|p| p.owner == original && p.private_layer == route.private_layer)
            || world
                .get::<ChildOf>(route.proxy)
                .is_none_or(|p| p.parent() != original)
            || world
                .get::<MeshMaterial3d<SourceGraphMaterial>>(route.proxy)
                .is_none_or(|m| m.0 != route.shadow_material)
        {
            return Err("Derived shadow ownership changed; preserve unrelated state");
        }
        if world
            .get::<Children>(route.proxy)
            .is_some_and(|c| !c.is_empty())
        {
            return Err("Derived shadow contains unrelated children; preserve them");
        }
    }
    // Every possible refusal above occurs before mutation.
    if world.get_entity(route.proxy).is_ok() {
        world.despawn(route.proxy);
    }
    world
        .entity_mut(original)
        .remove::<(SourceGraphShadowRoute, SourceGraphShadowRouteStatus)>()
        .insert((
            NotShadowCaster,
            SourceGraphShadowDetached {
                author_enabled: route.enabled,
            },
        ));
    // Do not remove a mesh/material asset: external handles may still borrow it.
    reconcile_source_shadow_light_layers(world);
    retire_unused_source_shadow_groups(world);
    Ok(true)
}
/// Explicit host author action; never infer enablement from a derived marker.
pub fn set_source_graph_shadow_enabled(
    world: &mut World,
    original: Entity,
    enabled: bool,
) -> Result<(), &'static str> {
    if let Some(mut r) = world.get_mut::<SourceGraphShadowRoute>(original) {
        r.enabled = enabled;
        return Ok(());
    }
    if let Some(mut d) = world.get_mut::<SourceGraphShadowDetached>(original) {
        d.author_enabled = enabled;
        return Ok(());
    }
    Err("Source shadow route has not been managed")
}
fn retire_unused_source_shadow_groups(world: &mut World) {
    let mut used: Vec<usize> = world
        .query::<&SourceGraphShadowRoute>()
        .iter(world)
        .map(|r| r.private_layer)
        .collect();
    used.extend(
        world
            .query::<&SourceGraphShadowProxy>()
            .iter(world)
            .map(|r| r.private_layer),
    );
    if let Some(mut groups) = world.get_resource_mut::<SourceGraphShadowGroups>() {
        groups.layers.retain(|layer, _| used.contains(layer));
    }
}
fn refuse(world: &mut World, original: Entity, proxy: Entity, reason: &str) {
    if let Ok(mut p) = world.get_entity_mut(proxy) {
        p.insert(Visibility::Hidden);
    }
    if let Ok(mut e) = world.get_entity_mut(original) {
        e.insert(SourceGraphShadowRouteStatus::Refused(reason.to_owned()));
    }
}
#[expect(
    clippy::too_many_lines,
    reason = "Routes are validated before derived entities are reconciled and retired in the same sync operation"
)]
fn sync_source_graph_shadow_routes(world: &mut World) {
    let routes: Vec<_> = world
        .query::<(Entity, &SourceGraphShadowRoute)>()
        .iter(world)
        .map(|(e, r)| (e, r.clone()))
        .collect();
    for (original, route) in routes {
        // This is derived render state. Author shadow enablement is route.enabled;
        // positions, scales, mesh handles, materials and source layer edits are kept.
        world.entity_mut(original).insert(NotShadowCaster);
        if !route.enabled {
            if let Ok(mut proxy) = world.get_entity_mut(route.proxy) {
                proxy.insert(Visibility::Hidden);
            }
            world
                .entity_mut(original)
                .insert(SourceGraphShadowRouteStatus::Disabled);
            continue;
        }
        if source_layers(world, original) != route.source_layers {
            refuse(
                world,
                original,
                route.proxy,
                "Source render layers changed; route requires reconstruction",
            );
            continue;
        }
        if private_layer_conflict(world, route.private_layer) {
            refuse(
                world,
                original,
                route.proxy,
                "A camera or unrelated scene entity includes the private shadow layer",
            );
            continue;
        }
        let mesh = match static_mesh(world, original) {
            Ok(m) => m,
            Err(e) => {
                refuse(world, original, route.proxy, e);
                continue;
            }
        };
        if world
            .get::<ChildOf>(route.proxy)
            .is_none_or(|c| c.parent() != original)
            || !world
                .get::<SourceGraphShadowProxy>(route.proxy)
                .is_some_and(|p| p.owner == original && p.private_layer == route.private_layer)
        {
            refuse(
                world,
                original,
                route.proxy,
                "Derived shadow parent or ownership changed",
            );
            continue;
        }
        let main = world
            .get::<MeshMaterial3d<SourceGraphMaterial>>(original)
            .map(|h| h.0.clone());
        let material = main.and_then(|h| {
            world
                .get_resource::<Assets<SourceGraphMaterial>>()
                .and_then(|a| a.get(&h))
                .cloned()
        });
        let shadow = material.and_then(|m| source_graph_shadow_only_material(&m).ok());
        let Some(shadow) = shadow else {
            refuse(
                world,
                original,
                route.proxy,
                "Source material is missing or already shadow-only",
            );
            continue;
        };
        let updated = {
            let mut assets = world.resource_mut::<Assets<SourceGraphMaterial>>();

            if let Some(mut target) = assets.get_mut(&route.shadow_material) {
                *target = shadow;
                true
            } else {
                false
            }
        };
        if !updated {
            refuse(
                world,
                original,
                route.proxy,
                "Derived shadow material missing",
            );
            continue;
        }
        retain_source_shadow_cpu_metadata(world, &mesh);
        world.entity_mut(route.proxy).insert((
            mesh,
            Transform::IDENTITY,
            Visibility::Inherited,
            RenderLayers::from_layers(&[route.private_layer]),
        ));
        copy_source_shadow_visibility(world, original, route.proxy);
        world
            .entity_mut(original)
            .insert(SourceGraphShadowRouteStatus::Ready);
    }
    reconcile_source_shadow_light_layers(world);
    retire_unused_source_shadow_groups(world);
}
#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_graph::{
        SourceGraphExtension, SourceGraphForwardState, SourceGraphKey, SourceGraphSurfacePhase,
        SourceGraphUniforms, SourceGraphUvRequirements, source_graph_material,
    };
    use bevy::prelude::{GlobalTransform, Mat4, MinimalPlugins, Quat, Vec3, Vec4};
    use bevy::{asset::RenderAssetUsages, mesh::PrimitiveTopology};
    fn setup(world: &mut World) -> (Entity, Entity, Entity) {
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<SourceGraphMaterial>>();
        let mesh = world.resource_mut::<Assets<Mesh>>().add(Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        ));
        let key = SourceGraphKey {
            requires_native_srgb_encoder: false,
            native_srgb_encoder_ready: false,
            requires_deferred_srgb: false,
            deferred_srgb_ready: false,
            requires_preintegrated_brdf: false,
            preintegrated_brdf_ready: false,
            requires_sky_cube: false,
            sky_cube_ready: false,
            color_write_mask: 15,
            prepass_cull: 2,
            shadow_only: false,
            orthographic_shadow_raster: None,
            vertex: 123,
            fragment: 123,
            prepass_vertex: 456,
            prepass_fragment: 456,
            original_alpha_to_mask: false,
            requires_original_vertex_color: false,
            original_uv_requirements: SourceGraphUvRequirements::default(),
            forward_state: SourceGraphForwardState {
                color_src: 1,
                color_dst: 0,
                alpha_src: 1,
                alpha_dst: 0,
                cull: 2,
                compare: 4,
                write_depth: true,
            },
        };
        let source = SourceGraphUniforms {
            source_object_from_target_object: Mat4::IDENTITY,
            target_object_from_source_object: Mat4::IDENTITY,
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            source_time_parameters: Vec4::ZERO,
            source_previous_time_parameters: Vec4::ZERO,
            source_scale_mip_min: Vec4::ONE,
            normal_decode_modes: [Vec4::ZERO; 2],
        };
        let material = source_graph_material(
            SourceGraphExtension::from_source_bindings(key, source, 0, vec![]).unwrap(),
            SourceGraphSurfacePhase::Opaque,
        );
        let material = world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .add(material);
        let main = world
            .spawn((
                Mesh3d(mesh),
                MeshMaterial3d(material),
                Transform::from_xyz(1., 2., 3.),
                Visibility::Inherited,
            ))
            .id();
        let camera = world
            .spawn((Camera::default(), RenderLayers::default()))
            .id();
        let light = world
            .spawn((PointLight::default(), RenderLayers::default()))
            .id();
        (main, camera, light)
    }
    #[test]
    fn source_graph_shadow_route_shared_geometry_child_preserves_camera_and_light_layers() {
        let mut w = World::new();
        let (main, camera, light) = setup(&mut w);
        let old = w.get::<RenderLayers>(light).unwrap().clone();
        let proxy = install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        assert_eq!(
            w.get::<Mesh3d>(main).unwrap().0,
            w.get::<Mesh3d>(proxy).unwrap().0
        );
        assert_eq!(w.get::<ChildOf>(proxy).unwrap().parent(), main);
        assert_eq!(*w.get::<Transform>(proxy).unwrap(), Transform::IDENTITY);
        assert!(w.get::<NotShadowCaster>(main).is_some());
        assert!(w.get::<NotShadowCaster>(proxy).is_none());
        assert_eq!(
            *w.get::<RenderLayers>(camera).unwrap(),
            RenderLayers::default()
        );
        assert_eq!(*w.get::<RenderLayers>(light).unwrap(), old.with(31));
        assert!(
            !w.get::<RenderLayers>(camera)
                .unwrap()
                .intersects(w.get::<RenderLayers>(proxy).unwrap())
        );
    }
    #[test]
    fn source_graph_shadow_route_repeated_install_keeps_ids_and_material_count() {
        let mut w = World::new();
        let (main, _, _) = setup(&mut w);
        let proxy = install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        let count = w.resource::<Assets<SourceGraphMaterial>>().len();
        assert_eq!(
            install_source_graph_shadow_route(&mut w, main, 31).unwrap(),
            proxy
        );
        assert_eq!(w.resource::<Assets<SourceGraphMaterial>>().len(), count);
    }
    #[test]
    fn source_graph_shadow_route_camera_collision_or_disabled_shadow_refuses_without_mutation() {
        let mut w = World::new();
        let (main, camera, light) = setup(&mut w);
        w.entity_mut(camera)
            .insert(RenderLayers::from_layers(&[0, 31]));
        let count = w.resource::<Assets<SourceGraphMaterial>>().len();
        let entities = w.entities().len();
        assert!(install_source_graph_shadow_route(&mut w, main, 31).is_err());
        assert_eq!(w.entities().len(), entities);
        assert_eq!(w.resource::<Assets<SourceGraphMaterial>>().len(), count);
        assert_eq!(
            *w.get::<RenderLayers>(light).unwrap(),
            RenderLayers::default()
        );
        assert!(w.get::<NotShadowCaster>(main).is_none());
        w.entity_mut(camera).insert(RenderLayers::default());
        w.entity_mut(main).insert(NotShadowCaster);
        assert!(install_source_graph_shadow_route(&mut w, main, 31).is_err());
    }
    #[test]
    fn source_graph_shadow_route_live_material_updates_and_camera_conflict_hides_proxy() {
        let mut w = World::new();
        let (main, camera, _) = setup(&mut w);
        let proxy = install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        let original = w
            .get::<MeshMaterial3d<SourceGraphMaterial>>(main)
            .unwrap()
            .0
            .clone();
        w.resource_mut::<Assets<SourceGraphMaterial>>()
            .get_mut(&original)
            .unwrap()
            .extension
            .source
            .source_time_parameters = Vec4::splat(7.0);
        sync_source_graph_shadow_routes(&mut w);
        let route = w.get::<SourceGraphShadowRoute>(main).unwrap();
        assert_eq!(
            w.resource::<Assets<SourceGraphMaterial>>()
                .get(&route.shadow_material)
                .unwrap()
                .extension
                .source
                .source_time_parameters,
            Vec4::splat(7.0)
        );
        w.entity_mut(camera)
            .insert(RenderLayers::from_layers(&[0, 31]));
        sync_source_graph_shadow_routes(&mut w);
        assert_eq!(*w.get::<Visibility>(proxy).unwrap(), Visibility::Hidden);
        assert!(matches!(
            w.get::<SourceGraphShadowRouteStatus>(main),
            Some(SourceGraphShadowRouteStatus::Refused(_))
        ));
        w.entity_mut(camera).insert(RenderLayers::default());
        sync_source_graph_shadow_routes(&mut w);
        assert_eq!(*w.get::<Visibility>(proxy).unwrap(), Visibility::Inherited);
    }
    #[test]
    fn source_graph_shadow_route_keeps_visibility_range_and_frustum_retouches() {
        let mut w = World::new();
        let (main, _, _) = setup(&mut w);
        w.entity_mut(main)
            .insert((VisibilityRange::abrupt(2., 50.), NoFrustumCulling));
        let proxy = install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        assert!(
            w.get::<VisibilityRange>(proxy).unwrap() == w.get::<VisibilityRange>(main).unwrap()
        );
        assert!(w.get::<NoFrustumCulling>(proxy).is_some());
        w.entity_mut(main).insert(VisibilityRange::abrupt(4., 80.));
        w.entity_mut(main).remove::<NoFrustumCulling>();
        sync_source_graph_shadow_routes(&mut w);
        assert!(
            w.get::<VisibilityRange>(proxy).unwrap() == w.get::<VisibilityRange>(main).unwrap()
        );
        assert!(w.get::<NoFrustumCulling>(proxy).is_none());
        w.entity_mut(main).remove::<VisibilityRange>();
        sync_source_graph_shadow_routes(&mut w);
        assert!(w.get::<VisibilityRange>(proxy).is_none());
    }
    #[test]
    fn source_graph_shadow_route_gpu_culling_refuses_without_new_shadow_or_source_mutation() {
        let mut w = World::new();
        let (main, _, light) = setup(&mut w);
        w.entity_mut(main).insert(NoCpuCulling);
        let count = w.resource::<Assets<SourceGraphMaterial>>().len();
        assert!(install_source_graph_shadow_route(&mut w, main, 31).is_err());
        assert_eq!(count, w.resource::<Assets<SourceGraphMaterial>>().len());
        assert!(w.get::<NotShadowCaster>(main).is_none());
        assert_eq!(
            *w.get::<RenderLayers>(light).unwrap(),
            RenderLayers::default()
        );
    }
    #[test]
    fn source_graph_shadow_route_retains_cpu_metadata_for_live_validation_without_changing_mesh_handle()
     {
        let mut w = World::new();
        let (main, _, _) = setup(&mut w);
        let mesh = w.get::<Mesh3d>(main).unwrap().0.clone();
        w.resource_mut::<Assets<Mesh>>()
            .get_mut(&mesh)
            .unwrap()
            .asset_usage = RenderAssetUsages::RENDER_WORLD;
        let proxy = install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        assert_eq!(w.get::<Mesh3d>(proxy).unwrap().0, mesh);
        assert!(
            w.resource::<Assets<Mesh>>()
                .get(&mesh)
                .unwrap()
                .asset_usage
                .contains(RenderAssetUsages::MAIN_WORLD)
        );
    }
    #[test]
    fn source_graph_shadow_route_inherits_parent_shear_reflection_and_edits_without_decomposition()
    {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, bevy::transform::TransformPlugin));
        let (main, _, _) = setup(app.world_mut());
        let parent = app
            .world_mut()
            .spawn(Transform::from_scale(Vec3::new(2., 3., 4.)))
            .id();
        app.world_mut().entity_mut(main).insert((
            ChildOf(parent),
            Transform {
                translation: Vec3::new(1., 2., 3.),
                rotation: Quat::from_rotation_y(0.7),
                scale: Vec3::new(-1., 0.5, 2.),
            },
        ));
        let proxy = install_source_graph_shadow_route(app.world_mut(), main, 31).unwrap();
        app.update();
        assert_eq!(
            app.world().get::<GlobalTransform>(main).unwrap(),
            app.world().get::<GlobalTransform>(proxy).unwrap()
        );
        app.world_mut()
            .get_mut::<Transform>(parent)
            .unwrap()
            .translation = Vec3::new(8., 9., 10.);
        app.update();
        assert_eq!(
            app.world().get::<GlobalTransform>(main).unwrap(),
            app.world().get::<GlobalTransform>(proxy).unwrap()
        );
    }
    #[test]
    fn source_graph_shadow_detach_remount_preserves_original_and_borrowed_material() {
        let mut w = World::new();
        let (main, _, light) = setup(&mut w);
        let before = w.get::<Transform>(main).copied().unwrap();
        let mesh = w.get::<Mesh3d>(main).unwrap().0.clone();
        let mat = w
            .get::<MeshMaterial3d<SourceGraphMaterial>>(main)
            .unwrap()
            .0
            .clone();
        let proxy = install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        let borrowed = w
            .get::<SourceGraphShadowRoute>(main)
            .unwrap()
            .shadow_material
            .clone();
        assert!(uninstall_source_graph_shadow_route(&mut w, main).unwrap());
        assert!(!uninstall_source_graph_shadow_route(&mut w, main).unwrap());
        assert!(w.get_entity(proxy).is_err());
        assert_eq!(w.get::<Transform>(main).copied().unwrap(), before);
        assert_eq!(w.get::<Mesh3d>(main).unwrap().0, mesh);
        assert_eq!(
            w.get::<MeshMaterial3d<SourceGraphMaterial>>(main)
                .unwrap()
                .0,
            mat
        );
        assert!(
            w.resource::<Assets<SourceGraphMaterial>>()
                .get(&borrowed)
                .is_some()
        );
        assert!(w.get::<NotShadowCaster>(main).is_some());
        assert_eq!(
            *w.get::<RenderLayers>(light).unwrap(),
            RenderLayers::default()
        );
        let next = install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        assert_ne!(proxy, next);
        assert!(w.get::<SourceGraphShadowDetached>(main).is_none());
    }
    #[test]
    fn source_graph_shadow_detach_keeps_disabled_author_intent_until_explicit_reenable() {
        let mut w = World::new();
        let (main, _, _) = setup(&mut w);
        install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        set_source_graph_shadow_enabled(&mut w, main, false).unwrap();
        uninstall_source_graph_shadow_route(&mut w, main).unwrap();
        assert!(install_source_graph_shadow_route(&mut w, main, 31).is_err());
        assert!(
            !w.get::<SourceGraphShadowDetached>(main)
                .unwrap()
                .author_enabled
        );
        set_source_graph_shadow_enabled(&mut w, main, true).unwrap();
        assert!(install_source_graph_shadow_route(&mut w, main, 31).is_ok());
    }
    #[test]
    fn source_graph_shadow_detach_foreign_children_or_parent_refuses_atomically() {
        let mut w = World::new();
        let (main, _, light) = setup(&mut w);
        let proxy = install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        let child = w.spawn(ChildOf(proxy)).id();
        let mask = w.get::<RenderLayers>(light).cloned();
        let count = w.entities().len();
        assert!(uninstall_source_graph_shadow_route(&mut w, main).is_err());
        assert_eq!(w.entities().len(), count);
        assert_eq!(w.get::<RenderLayers>(light).cloned(), mask);
        assert!(w.get_entity(child).is_ok());
        assert!(w.get::<SourceGraphShadowRoute>(main).is_some());
        w.despawn(child);
        let other = w.spawn_empty().id();
        w.entity_mut(proxy).insert(ChildOf(other));
        assert!(uninstall_source_graph_shadow_route(&mut w, main).is_err());
        assert!(w.get_entity(proxy).is_ok());
    }
    #[test]
    fn source_graph_shadow_source_despawn_retires_private_group_preserving_other_light_bits() {
        let mut w = World::new();
        let (main, _, light) = setup(&mut w);
        w.entity_mut(light)
            .insert(RenderLayers::from_layers(&[0, 5]));
        let proxy = install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        w.despawn(main);
        assert!(w.get_entity(proxy).is_err());
        sync_source_graph_shadow_routes(&mut w);
        assert_eq!(
            *w.get::<RenderLayers>(light).unwrap(),
            RenderLayers::from_layers(&[0, 5])
        );
        assert!(
            !w.resource::<SourceGraphShadowGroups>()
                .layers
                .contains_key(&31)
        );
    }
    #[test]
    fn source_graph_shadow_reconstruction_uses_author_layer_edits_without_cross_group_leak() {
        let mut w = World::new();
        let (main, _, light) = setup(&mut w);
        let peer = w
            .spawn((
                w.get::<Mesh3d>(main).unwrap().clone(),
                w.get::<MeshMaterial3d<SourceGraphMaterial>>(main)
                    .unwrap()
                    .clone(),
                Transform::IDENTITY,
                Visibility::Visible,
            ))
            .id();
        install_source_graph_shadow_route(&mut w, main, 31).unwrap();
        install_source_graph_shadow_route(&mut w, peer, 31).unwrap();
        let light2 = w
            .spawn((PointLight::default(), RenderLayers::layer(2)))
            .id();
        w.entity_mut(main).insert(RenderLayers::layer(2));
        sync_source_graph_shadow_routes(&mut w);
        assert!(matches!(
            w.get::<SourceGraphShadowRouteStatus>(main),
            Some(SourceGraphShadowRouteStatus::Refused(_))
        ));
        uninstall_source_graph_shadow_route(&mut w, main).unwrap();
        assert!(
            w.resource::<SourceGraphShadowGroups>()
                .layers
                .contains_key(&31)
        );
        install_source_graph_shadow_route(&mut w, main, 32).unwrap();
        sync_source_graph_shadow_routes(&mut w);
        assert_eq!(
            *w.get::<RenderLayers>(light).unwrap(),
            RenderLayers::from_layers(&[0, 31])
        );
        assert_eq!(
            *w.get::<RenderLayers>(light2).unwrap(),
            RenderLayers::from_layers(&[2, 32])
        );
        assert_eq!(
            *w.get::<RenderLayers>(main).unwrap(),
            RenderLayers::layer(2)
        );
    }
}

#[cfg(test)]
mod light_layer_edit_tests {
    use super::*;
    #[test]
    fn source_graph_shadow_route_light_author_edits_remove_stale_private_group_without_cross_group_leak()
     {
        let mut w = World::new();
        w.init_resource::<SourceGraphShadowGroups>();
        w.resource_mut::<SourceGraphShadowGroups>()
            .layers
            .insert(31, RenderLayers::layer(0));
        w.resource_mut::<SourceGraphShadowGroups>()
            .layers
            .insert(32, RenderLayers::layer(2));
        let shadow = Handle::<SourceGraphMaterial>::default();
        w.spawn((
            SourceGraphShadowRoute {
                proxy: Entity::PLACEHOLDER,
                private_layer: 31,
                source_layers: RenderLayers::layer(0),
                shadow_material: shadow.clone(),
                enabled: true,
            },
            SourceGraphShadowRouteStatus::Ready,
        ));
        w.spawn((
            SourceGraphShadowRoute {
                proxy: Entity::PLACEHOLDER,
                private_layer: 32,
                source_layers: RenderLayers::layer(2),
                shadow_material: shadow,
                enabled: true,
            },
            SourceGraphShadowRouteStatus::Ready,
        ));
        let light = w
            .spawn((PointLight::default(), RenderLayers::from_layers(&[0, 31])))
            .id();
        reconcile_source_shadow_light_layers(&mut w);
        assert_eq!(
            *w.get::<RenderLayers>(light).unwrap(),
            RenderLayers::from_layers(&[0, 31])
        );
        w.entity_mut(light)
            .insert(RenderLayers::from_layers(&[2, 31]));
        reconcile_source_shadow_light_layers(&mut w);
        assert_eq!(
            *w.get::<RenderLayers>(light).unwrap(),
            RenderLayers::from_layers(&[2, 32])
        );
        w.entity_mut(light)
            .insert(RenderLayers::from_layers(&[0, 2, 7, 31, 32]));
        reconcile_source_shadow_light_layers(&mut w);
        assert_eq!(
            *w.get::<RenderLayers>(light).unwrap(),
            RenderLayers::from_layers(&[0, 2, 7, 31, 32])
        );
    }
    #[test]
    fn source_graph_shadow_route_private_layer_runtime_collision_is_detected() {
        let mut w = World::new();
        assert!(!private_layer_conflict(&mut w, 31));
        let unrelated = w.spawn(RenderLayers::layer(31)).id();
        assert!(private_layer_conflict(&mut w, 31));
        w.despawn(unrelated);
        let camera = w.spawn((Camera::default(), RenderLayers::layer(31))).id();
        assert!(private_layer_conflict(&mut w, 31));
        w.despawn(camera);
        w.spawn((PointLight::default(), RenderLayers::layer(31)));
        assert!(!private_layer_conflict(&mut w, 31));
    }
}
