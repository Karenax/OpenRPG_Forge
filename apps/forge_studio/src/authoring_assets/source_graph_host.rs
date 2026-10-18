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
    pub source_document: serde_json::Value,
    pub carrier_sha256: forge_core::ContentHash,
    pub particle_instances: Vec<forge_engine::source_particle_host::HostedSourceParticle>,
    pub collider_projection: Option<forge_engine::source_collider_projection::ColliderProjection>,
    pub collider_scene_projection:
        Option<forge_engine::source_collider_scene::ColliderSceneProjection>,
}

#[derive(Component)]
struct ShadowLayer(usize);

impl SourceSceneHost {
    pub fn invalidate_particle_frames(&self, world: &mut World) {
        for instance in &self.particle_instances {
            instance.invalidate_current_frame(world);
        }
    }
    pub fn cancel_particle_frames(&self, world: &mut World) {
        for instance in &self.particle_instances {
            instance.cancel_current_frame(world);
        }
    }
    pub fn update_particle_frames(
        &self,
        world: &mut World,
        source_world: Mat4,
        camera_world: Mat4,
        clip_from_view: Mat4,
    ) -> Result<(), String> {
        if self.particle_instances.is_empty() {
            return Ok(());
        }
        let basis = self
            .object_basis
            .ok_or("Declared source object basis unavailable")?;
        let plans = self
            .particle_instances
            .iter()
            .map(|instance| {
                instance.prepare_current_scene_frame(
                    world,
                    source_world,
                    basis.target_from_source,
                    camera_world,
                    clip_from_view,
                )
            })
            .collect::<Result<Vec<_>, _>>()?;
        for plan in plans {
            plan.apply(world)?;
        }
        Ok(())
    }
    pub fn install_particle_package(
        &mut self,
        world: &mut World,
        carrier: &[u8],
        wire: &[u8],
        expected: forge_core::ContentHash,
        resources: &std::collections::BTreeMap<String, Vec<u8>>,
        slots: &[(u64, u64)],
    ) -> Result<(), String> {
        if !self.particle_instances.is_empty() {
            return Err("Source host already owns particle instances".into());
        }
        if forge_core::bytes_sha256(carrier) != self.carrier_sha256 {
            return Err("Particle carrier differs from attached host".into());
        }
        let package =
            forge_assets::source_particle_transport::SourceParticleTransport::decode_for_carrier(
                wire,
                expected,
                carrier,
                |name| {
                    resources
                        .get(name)
                        .cloned()
                        .ok_or("Particle package resource absent".into())
                },
            )?;
        if slots.len() != package.contract.owners.len() {
            return Err("Particle host slot count differs".into());
        }
        let mut plans = Vec::new();
        for declaration in &package.contract.owners {
            let node = *self
                .attached
                .nodes
                .get(&declaration.source_node)
                .ok_or("Particle source node not attached")?;
            let material = self
                .registered
                .bindings
                .source_materials
                .get(&(declaration.source_node, declaration.source_material))
                .ok_or("Particle source material not attached")?
                .clone();
            let prepared =
                forge_engine::source_particle_transport::PreparedSourceParticleOwner::prepare(
                    declaration,
                    &package.resources,
                )?;
            plans.push((node, material, prepared));
        }
        let mut installed = Vec::new();
        for ((node, material, prepared), (producer, projection)) in plans.into_iter().zip(slots) {
            match forge_engine::source_particle_host::HostedSourceParticle::install(
                world,
                prepared,
                node,
                material,
                *producer,
                *projection,
            ) {
                Ok(instance) => installed.push(instance),
                Err(error) => {
                    for instance in &mut installed {
                        instance.release(world);
                    }
                    return Err(error);
                }
            }
        }
        self.particle_instances = installed;
        Ok(())
    }

    pub fn release_particle_instances(&mut self, world: &mut World) {
        for instance in &mut self.particle_instances {
            instance.release(world);
        }
        self.particle_instances.clear();
    }

    /// Resolve measured contacts only with explicit target-scene settings.
    /// The source offset and original observations remain distinct from target
    /// skin; success does not qualify native simulation or reveal the scene.
    pub fn install_physics_package_with_settings(
        &mut self,
        world: &mut World,
        carrier: &[u8],
        wire: &[u8],
        expected_manifest: forge_core::ContentHash,
        settings: &forge_engine::source_physics_contacts::ContactSceneSettings,
    ) -> Result<usize, String> {
        let package = forge_assets::source_physics_package::SourcePhysicsPackage::decode(
            wire,
            forge_core::bytes_sha256(carrier),
            expected_manifest,
        )?;
        let glb = super::source_graph_route::read(carrier)?
            .ok_or("Source physics carrier declaration absent")?;
        let components = forge_assets::source_physics_transport::read_source_physics_transport(
            &package.manifest,
            expected_manifest,
            forge_core::bytes_sha256(carrier),
            &glb,
            |path| {
                package
                    .candidates
                    .get(path)
                    .cloned()
                    .ok_or("Physics package candidate absent".into())
            },
        )?;
        let contacts =
            forge_engine::source_physics_contacts::contacts_from_transport(&components, settings)?;
        let cooking = forge_engine::source_physics_transport::cooking_from_transport(
            &glb,
            components,
            &settings.evidence,
        )?;
        self.install_source_colliders(world, &glb, &cooking, &contacts)
    }

    /// Consume a binary package only with the manifest pin from the governed
    /// library revision. The carrier pin is recomputed from the model bytes.
    pub fn install_physics_package(
        &mut self,
        world: &mut World,
        carrier: &[u8],
        wire: &[u8],
        expected_manifest: forge_core::ContentHash,
        contacts: &BTreeMap<
            (String, String),
            forge_engine::source_collider_projection::ContactPolicy,
        >,
    ) -> Result<usize, String> {
        let package = forge_assets::source_physics_package::SourcePhysicsPackage::decode(
            wire,
            forge_core::bytes_sha256(carrier),
            expected_manifest,
        )?;
        self.install_physics_transport(
            world,
            carrier,
            &package.manifest,
            expected_manifest,
            |path| {
                package
                    .candidates
                    .get(path)
                    .cloned()
                    .ok_or("Physics package candidate absent".into())
            },
            contacts,
        )
    }

    /// Read a pinned transport for the full carrier and install its shapes with
    /// caller-supplied contact policy. The resolver never receives provenance
    /// paths, only validated relative candidate paths.
    pub fn install_physics_transport(
        &mut self,
        world: &mut World,
        carrier: &[u8],
        manifest: &[u8],
        expected_manifest: forge_core::ContentHash,
        resolve: impl FnMut(&str) -> Result<Vec<u8>, String>,
        contacts: &BTreeMap<
            (String, String),
            forge_engine::source_collider_projection::ContactPolicy,
        >,
    ) -> Result<usize, String> {
        let glb = super::source_graph_route::read(carrier)?
            .ok_or("Source physics carrier declaration absent")?;
        let components = forge_assets::source_physics_transport::read_source_physics_transport(
            manifest,
            expected_manifest,
            forge_core::bytes_sha256(carrier),
            &glb,
            resolve,
        )?;
        let evidence = format!(
            "source-physics-transport SHA-256 {expected_manifest}; native equivalence unqualified"
        );
        let cooking = forge_engine::source_physics_transport::cooking_from_transport(
            &glb, components, &evidence,
        )?;
        self.install_source_colliders(world, &glb, &cooking, contacts)
    }

    /// Install the declared collision batch under the same source node entities.
    /// The caller supplies resolved physical policy with provenance. Render
    /// visibility and LOD decisions do not enable or disable these colliders.
    pub fn install_colliders(
        &mut self,
        world: &mut World,
        policies: &BTreeMap<
            (String, String),
            forge_engine::source_collider_projection::ContactPolicy,
        >,
    ) -> Result<usize, String> {
        if self.collider_projection.is_some()
            || self.collider_scene_projection.is_some()
            || world.get::<SourceSceneAttached>(self.root).is_none()
        {
            return Err("Source collision host missing or already owns a projection".into());
        }
        let basis = self
            .object_basis
            .ok_or("Source collider object basis absent")?;
        let projection = forge_engine::source_collider_projection::attach(
            world,
            self.root,
            &self.source_document,
            basis.target_from_source,
            &self.attached.nodes,
            policies,
        )?;
        let count = projection.entities.len();
        self.collider_projection = Some(projection);
        Ok(count)
    }

    /// Mount the complete box/mesh batch for this exact admitted source carrier.
    /// Caller-supplied cooking/contact evidence never implies render admission.
    pub fn install_source_colliders(
        &mut self,
        world: &mut World,
        glb: &forge_assets::source_graph_glb::SourceGraphGlb<'_>,
        cooking: &BTreeMap<
            (String, String),
            forge_engine::source_mesh_colliders::MeshCookingPolicy,
        >,
        contacts: &BTreeMap<
            (String, String),
            forge_engine::source_collider_projection::ContactPolicy,
        >,
    ) -> Result<usize, String> {
        if self.collider_projection.is_some()
            || self.collider_scene_projection.is_some()
            || world.get::<SourceSceneAttached>(self.root).is_none()
            || glb.document() != &self.source_document
        {
            return Err("Source collision host missing, occupied or carrier differs".into());
        }
        let basis = self
            .object_basis
            .ok_or("Source collider object basis absent")?;
        let projection = forge_engine::source_collider_scene::attach(
            world,
            self.root,
            glb,
            basis.target_from_source,
            &self.attached.nodes,
            cooking,
            contacts,
        )?;
        let count = projection.observation_count();
        self.collider_scene_projection = Some(projection);
        Ok(count)
    }

    /// Remove owned collision children without changing source nodes or draws.
    pub fn remove_colliders(&mut self, world: &mut World) -> usize {
        let mut count = 0;
        if let Some(projection) = self.collider_projection.take() {
            count += projection.entities.len();
            projection.clear(world);
        }
        if let Some(projection) = self.collider_scene_projection.take() {
            count += projection.observation_count();
            projection.clear(world);
        }
        count
    }

    /// Select source LODs from the caller's current perspective camera.
    /// Success does not reveal this host or qualify its GPU passes.
    pub fn update_lod(
        &self,
        world: &mut World,
        camera: &super::source_graph_lod::LodCamera,
    ) -> Result<Vec<super::source_graph_lod::LodDecision>, String> {
        let result = (|| {
            let state = self
                .original_state
                .as_ref()
                .ok_or("Source renderer state absent")?;
            let basis = self.object_basis.ok_or("Source object basis absent")?;
            super::source_graph_lod::update(
                world,
                self.root,
                &self.attached.nodes,
                &self.attached.primitives,
                state,
                basis.target_from_source,
                camera,
            )
        })();
        if result.is_err()
            && let Ok(mut root) = world.get_entity_mut(self.root)
        {
            root.insert(Visibility::Hidden);
        }
        result
    }

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
                        use std::fmt::Write as _;
                        let _ = write!(error, "; shadow rollback refused: {reason}");
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
            source_document: glb.document().clone(),
            carrier_sha256: forge_core::bytes_sha256(bytes),
            particle_instances: Vec::new(),
            collider_projection: None,
            collider_scene_projection: None,
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
