//! One hidden source-scene host shared by preview and persisted placements.
//! Frames and ordinary primitive bindings are explicit host inputs. Admission
//! does not reveal the scene or qualify its GPU passes, camera or render queue.
use super::{
    source_graph_context, source_graph_frame,
    source_graph_intake::{self, SceneIntake},
    source_graph_lights,
    source_graph_resources::{self, RegisteredScene},
    source_graph_scene::{self, AttachedScene, OrdinaryPrimitive, SourceSceneAttached},
    source_graph_state,
};
use bevy::{ecs as bevy_ecs, prelude::*};
use forge_engine::{
    source_graph::SourceGraphUniforms, source_graph_scene_geometry::SourcePrimitiveKey,
};
use std::collections::BTreeMap;

pub(super) struct SourceSceneHost {
    pub root: Entity,
    pub attached: AttachedScene,
    pub registered: RegisteredScene,
    pub original_state: Option<source_graph_state::StatePlan>,
    pub object_basis: Option<source_graph_context::ObjectBasis>,
}

#[derive(Component)]
struct ShadowLayer(usize);

impl SourceSceneHost {
    /// The caller reserves a private layer; no asset or pack selects it.
    /// Source disabled shadows are preserved. A failed batch stays hidden and
    /// rolls back only newly created routes, never a previously borrowed route.
    pub fn install_shadow_routes(&self, world: &mut World, layer: usize) -> Result<usize, String> {
        use forge_engine::source_graph::{
            SourceGraphMaterial, SourceGraphShadowRoute, install_source_graph_shadow_route,
            uninstall_source_graph_shadow_route,
        };
        let mut installed = Vec::new();
        let result = (|| {
            if world.get::<SourceSceneAttached>(self.root).is_none() {
                return Err("Source shadow host is no longer attached".to_owned());
            }
            if world
                .get::<ShadowLayer>(self.root)
                .is_some_and(|old| old.0 != layer)
            {
                return Err("Source host already owns a different shadow layer".to_owned());
            }
            let mut count = 0;
            for draw in &self.attached.primitives {
                if world
                    .get::<MeshMaterial3d<SourceGraphMaterial>>(*draw)
                    .is_none()
                {
                    continue;
                }
                let original = world
                    .get::<source_graph_state::OriginalRendererState>(*draw)
                    .ok_or("Source shadow route requires original renderer state")?;
                match original.shadow_casting_mode {
                    0 => continue,
                    1 => {}
                    _ => return Err("Source shadow mode is not admitted".to_owned()),
                }
                let existed = world.get::<SourceGraphShadowRoute>(*draw).is_some();
                install_source_graph_shadow_route(world, *draw, layer).map_err(str::to_owned)?;
                if !existed {
                    installed.push(*draw);
                }
                count += 1;
            }
            Ok(count)
        })();
        match result {
            Ok(count) => {
                world.entity_mut(self.root).insert(ShadowLayer(layer));
                Ok(count)
            }
            Err(mut error) => {
                for draw in installed.into_iter().rev() {
                    if let Err(reason) = uninstall_source_graph_shadow_route(world, draw) {
                        error.push_str(&format!("; shadow rollback refused: {reason}"));
                    }
                }
                if let Ok(mut root) = world.get_entity_mut(self.root) {
                    root.insert(Visibility::Hidden);
                }
                Err(error)
            }
        }
    }

    fn validate_shadow_routes(&self, world: &World) -> Result<(), String> {
        use forge_engine::source_graph::{
            SourceGraphMaterial, SourceGraphShadowRoute, SourceGraphShadowRouteStatus,
        };
        let Some(layer) = world.get::<ShadowLayer>(self.root) else {
            return Ok(());
        };
        for draw in &self.attached.primitives {
            if world
                .get::<MeshMaterial3d<SourceGraphMaterial>>(*draw)
                .is_none()
            {
                continue;
            }
            let original = world
                .get::<source_graph_state::OriginalRendererState>(*draw)
                .ok_or("Original source renderer state disappeared")?;
            if original.shadow_casting_mode == 0 {
                if world.get::<SourceGraphShadowRoute>(*draw).is_some() {
                    return Err("Disabled source renderer acquired a shadow route".into());
                }
                continue;
            }
            let route = world
                .get::<SourceGraphShadowRoute>(*draw)
                .ok_or("Expected source shadow route disappeared")?;
            let expected_status = if route.enabled {
                SourceGraphShadowRouteStatus::Ready
            } else {
                SourceGraphShadowRouteStatus::Disabled
            };
            if original.shadow_casting_mode != 1
                || route.private_layer != layer.0
                || world.get::<SourceGraphShadowRouteStatus>(*draw) != Some(&expected_status)
            {
                return Err("Source shadow route is not ready on its reserved layer".into());
            }
        }
        Ok(())
    }
    /// Called after transform propagation. Decoder modes remain owned by the
    /// admitted resident material; no captured time or old scale is reused.
    pub fn update_runtime_frame(
        &self,
        world: &mut World,
        frame: &source_graph_context::HostFrame,
    ) -> Result<usize, String> {
        let contexts = (|| {
            let basis = self
                .object_basis
                .ok_or("Source scene has no declared object basis")?;
            let materials = world
                .get_resource::<Assets<forge_engine::source_graph::SourceGraphMaterial>>()
                .ok_or("Admitted source materials unavailable")?;
            let mut contexts = BTreeMap::new();
            for (key, handle) in &self.registered.bindings.source_materials {
                let entity = self
                    .attached
                    .nodes
                    .get(&key.0)
                    .ok_or("Current source node absent")?;
                let matrix = world
                    .get::<GlobalTransform>(*entity)
                    .ok_or("Current composed source transform absent")?
                    .to_matrix();
                let modes = materials
                    .get(handle)
                    .ok_or("Current source material absent")?
                    .extension
                    .source
                    .normal_decode_modes;
                contexts.insert(*key, frame.uniform(basis, matrix, modes)?);
            }
            Ok::<_, String>(contexts)
        })();
        match contexts {
            Ok(contexts) => self.update_frame(world, &contexts),
            Err(error) => {
                if let Ok(mut root) = world.get_entity_mut(self.root) {
                    root.insert(Visibility::Hidden);
                }
                Err(error)
            }
        }
    }
    /// The host supplies the current source frame after transform composition.
    /// An invalid frame keeps this root hidden rather than displaying old data.
    pub fn update_frame(
        &self,
        world: &mut World,
        contexts: &BTreeMap<(usize, usize), SourceGraphUniforms>,
    ) -> Result<usize, String> {
        if world.get::<SourceSceneAttached>(self.root).is_none() {
            return Err("Source scene host is no longer attached".into());
        }
        let result = self
            .validate_shadow_routes(world)
            .and_then(|()| {
                let current = world
                    .get::<source_graph_resources::SceneSampling>(self.root)
                    .map(|context| context.0)
                    .unwrap_or_default();
                if current != self.registered.sampling {
                    return Err("Source sampling changed; remount required before reveal".into());
                }
                Ok(())
            })
            .and_then(|()| source_graph_frame::update(world, &self.registered.bindings, contexts));
        if result.is_err() {
            world.entity_mut(self.root).insert(Visibility::Hidden);
        }
        result
    }
}

/// None selects the existing ordinary glTF route. Malformed source declarations
/// are errors, never a reason to replace the source programs with ordinary PBR.
/// The caller owns root identity, placement persistence and eventual reveal.
pub(super) fn mount(
    world: &mut World,
    root: Entity,
    bytes: &[u8],
    selected_scene: Option<usize>,
    contexts: &BTreeMap<(usize, usize), SourceGraphUniforms>,
    ordinary: BTreeMap<SourcePrimitiveKey, OrdinaryPrimitive>,
) -> Result<Option<SourceSceneHost>, String> {
    let intake = source_graph_intake::prepare(bytes, selected_scene)?;
    let SceneIntake::SourceGraph { glb, geometry } = intake else {
        return Ok(None);
    };
    if world.get::<Visibility>(root) != Some(&Visibility::Hidden)
        || world.get::<Transform>(root).is_none()
        || world.get::<GlobalTransform>(root).is_none()
        || world.get::<SourceSceneAttached>(root).is_some()
    {
        return Err("Source host requires an existing hidden unattached transform root".into());
    }
    let parents = geometry
        .hierarchy
        .nodes
        .iter()
        .map(|n| (n.source_node, n.parent_source_node))
        .collect();
    let draw_nodes: Vec<_> = geometry.instances.iter().map(|p| p.source_node).collect();
    let object_basis = source_graph_context::ObjectBasis::from_document(glb.document())?;
    let original_state = source_graph_state::prepare(glb.document(), &parents, &draw_nodes)?;
    let light_nodes = geometry
        .hierarchy
        .nodes
        .iter()
        .map(|node| node.source_node)
        .collect();
    let lights = source_graph_lights::prepare(
        glb.document(),
        &light_nodes,
        world
            .get::<source_graph_lights::NativeSourceLightTransport>(root)
            .is_some(),
    )?;
    let sampling = world
        .get::<source_graph_resources::SceneSampling>(root)
        .map(|context| context.0)
        .unwrap_or_default();
    let registered = source_graph_resources::register_with_sampling(
        world, &glb, &geometry, contexts, ordinary, sampling,
    )?;
    let attachment = source_graph_scene::attach(world, root, &geometry, &registered.bindings)
        .and_then(|attached| {
            if let Err(error) = lights.apply(world, &attached.nodes) {
                for entity in attached.nodes.values().chain(attached.primitives.iter()) {
                    if world.get_entity(*entity).is_ok() {
                        world.despawn(*entity);
                    }
                }
                world.entity_mut(root).remove::<SourceSceneAttached>();
                return Err(error);
            }
            if let Some(state) = &original_state {
                state.apply(world, root, &attached.nodes, &attached.primitives);
            }
            Ok(attached)
        });
    match attachment {
        Ok(attached) => Ok(Some(SourceSceneHost {
            root,
            attached,
            registered,
            original_state,
            object_basis,
        })),
        Err(error) => {
            // Remove only allocations owned by this registration. Borrowed
            // ordinary resources and the shared shader registry stay intact.
            let mut images = world.resource_mut::<Assets<Image>>();
            for id in &registered.owned_image_ids {
                images.remove(*id);
            }
            let mut meshes = world.resource_mut::<Assets<Mesh>>();
            for handle in registered.bindings.source_meshes.values() {
                meshes.remove(handle.id());
            }
            let mut materials =
                world.resource_mut::<Assets<forge_engine::source_graph::SourceGraphMaterial>>();
            for handle in registered.bindings.source_materials.values() {
                materials.remove(handle.id());
            }
            Err(error)
        }
    }
}

/// Build initial frames from the admitted scene and an explicit composed root.
/// The caller owns world-coordinate and clock policy; no captured frame is used.
pub(super) fn mount_initial(
    world: &mut World,
    root: Entity,
    bytes: &[u8],
    selected_scene: Option<usize>,
    target_world_from_root: Mat4,
    frame: &source_graph_context::HostFrame,
    ordinary: BTreeMap<SourcePrimitiveKey, OrdinaryPrimitive>,
) -> Result<Option<SourceSceneHost>, String> {
    let intake = source_graph_intake::prepare(bytes, selected_scene)?;
    let SceneIntake::SourceGraph { glb, geometry } = intake else {
        return Ok(None);
    };
    let contexts = super::source_graph_bootstrap::initial_contexts(
        &glb,
        &geometry,
        target_world_from_root,
        frame,
    )?;
    mount(world, root, bytes, selected_scene, &contexts, ordinary)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn ordinary_glb(extra: &str) -> Vec<u8> {
        let json = format!(
            "{{\"asset\":{{\"version\":\"2.0\"}},\"buffers\":[{{\"byteLength\":0}}],\"scenes\":[{{\"nodes\":[]}}],\"scene\":0{extra}}}"
        );
        let mut encoded = json.into_bytes();
        while encoded.len() % 4 != 0 {
            encoded.push(b' ');
        }
        let mut bytes = b"glTF".to_vec();
        bytes.extend(2u32.to_le_bytes());
        bytes.extend(((28 + encoded.len()) as u32).to_le_bytes());
        bytes.extend((encoded.len() as u32).to_le_bytes());
        bytes.extend(b"JSON");
        bytes.extend(encoded);
        bytes.extend(0u32.to_le_bytes());
        bytes.extend(b"BIN\0");
        bytes
    }

    #[test]
    fn ordinary_gltf_keeps_the_existing_loader_and_root_without_source_allocations() {
        let mut world = World::new();
        let root = world
            .spawn(Name::new("Existing ordinary preview root"))
            .id();
        let before = world.entities().len();
        let result = mount(
            &mut world,
            root,
            &ordinary_glb(""),
            None,
            &BTreeMap::new(),
            BTreeMap::new(),
        )
        .unwrap();
        assert!(result.is_none());
        assert_eq!(world.entities().len(), before);
        assert_eq!(
            world.get::<Name>(root).unwrap().as_str(),
            "Existing ordinary preview root"
        );
        assert!(
            !world.contains_resource::<Assets<forge_engine::source_graph::SourceGraphMaterial>>()
        );
    }

    #[test]
    fn malformed_source_program_cannot_fall_back_to_ordinary_gltf_or_mutate_the_root() {
        let mut world = World::new();
        let pose = Transform::from_xyz(11.0, 3.0, -4.0);
        let root = world.spawn((pose, Visibility::Hidden)).id();
        let bytes = ordinary_glb(",\"materials\":[{\"extensions\":{\"OPENRPG_source_graph\":{}}}]");
        let before = world.entities().len();
        assert!(
            mount(
                &mut world,
                root,
                &bytes,
                None,
                &BTreeMap::new(),
                BTreeMap::new()
            )
            .is_err()
        );
        assert_eq!(world.entities().len(), before);
        assert_eq!(*world.get::<Transform>(root).unwrap(), pose);
        assert_eq!(*world.get::<Visibility>(root).unwrap(), Visibility::Hidden);
        assert!(world.get::<SourceSceneAttached>(root).is_none());
    }
}
