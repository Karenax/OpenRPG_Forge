//! Disposable building projection shared by native and Studio adapters.
use crate::{
    tidewater_material::{PROCEDURAL_SOLID_KIND, VillageMaterial, neutral_scene_material},
    tidewater_mesh::{DATA, TINT},
    tidewater_object_shadow::scene::OpaqueCaster,
};
use bevy::prelude::*;
use bevy_rapier3d::prelude::{Collider, RigidBody};
use forge_core::{ContentHash, EntityId};
use forge_project::zone_authoring::ZoneAuthoringHead;
use forge_world::building_layout::{PartRole, RoomLayout};

/// Read-only Rapier component re-exported for adapters that report an existing
/// derived projection. It is never part of a Forge document.
pub use bevy_rapier3d::prelude::RapierColliderHandle as ProjectionRapierColliderHandle;

#[cfg(test)]
#[path = "authoring_buildings_passage.rs"]
mod passage;

#[derive(Component, Clone, Copy, Debug, PartialEq, Eq)]
pub struct BuildingPartIdentity {
    pub building: EntityId,
    pub part: EntityId,
    pub role: PartRole,
}

/// The exact result of the last reconciliation, recorded while it happens.
///
/// These lists name derived building parts only. They are deliberately not a
/// cache, a document mutation, or an input to readiness/playtest decisions.
#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct BuildingProjectionMutation {
    pub retained: Vec<BuildingPartIdentity>,
    pub spawned: Vec<BuildingPartIdentity>,
    pub rebuilt: Vec<BuildingPartIdentity>,
    pub removed: Vec<BuildingPartIdentity>,
}

/// Ephemeral ECS/resource observations for one currently projected building part.
///
/// IDs and handles only identify this process' derived representation. They are
/// not serializable Forge authority and make no GPU presentation claim.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct BuildingProjectionPartDiagnostic {
    pub building: EntityId,
    pub part: EntityId,
    pub role: String,
    pub ecs_entity: u64,
    pub root_ecs_entity: u64,
    pub mesh_handle: Option<String>,
    pub material_handle: Option<String>,
    pub rapier_collider_handle: Option<String>,
}

/// Handles are derived and owned by this projection, never serialized.
#[derive(Default)]
pub struct BuildingProjection {
    entities: Vec<Entity>,
    mesh: Option<Handle<Mesh>>,
    material: Option<Handle<StandardMaterial>>,
    scene_material: Option<Handle<VillageMaterial>>,
    source_hash: Option<ContentHash>,
    preview: bool,
    expected: Vec<(BuildingPartIdentity, Transform)>,
    last_mutation: BuildingProjectionMutation,
}

impl BuildingProjection {
    /// Validate and prepare everything before replacing the previous projection.
    /// Preview parts have no colliders; an accepted batch gets fixed colliders.
    pub fn replace(
        &mut self,
        world: &mut World,
        head: &ZoneAuthoringHead,
        preview: bool,
    ) -> Result<(), String> {
        let hash = head.content_hash()?;
        let parts = prepare(head)?;
        let previous_expected = self.expected.clone();
        let mut mutation = BuildingProjectionMutation::default();
        if !world.contains_resource::<Assets<Mesh>>()
            || !world.contains_resource::<Assets<StandardMaterial>>()
        {
            return Err("Building projection requires mesh and material stores".into());
        }
        // A preview owns a differently coloured material and deliberately has no
        // colliders, so it is a separate projection. Accepted projections retain
        // their shared resources and reconcile every stable part identity.
        if preview != self.preview || !self.resources_are_live(world) {
            self.clear(world);
        }
        if parts.is_empty() {
            self.clear(world);
            mutation.removed = previous_expected
                .into_iter()
                .map(|(identity, _)| identity)
                .collect();
            self.last_mutation = mutation;
            self.source_hash = Some(hash);
            self.preview = preview;
            return Ok(());
        }
        self.ensure_resources(world, preview);

        let mut previous: Vec<_> = self
            .entities
            .iter()
            .copied()
            .zip(self.expected.iter().copied())
            .collect();
        let orphaned: Vec<_> = self
            .entities
            .iter()
            .skip(self.expected.len())
            .copied()
            .collect();
        for entity in orphaned {
            world.despawn(entity);
        }
        let mut retained = Vec::with_capacity(parts.len());
        for (identity, transform) in &parts {
            let matching = previous
                .iter()
                .position(|(_, (old_identity, _))| old_identity == identity)
                .map(|index| previous.swap_remove(index));
            if let Some((entity, (_, old_transform))) = matching {
                if self.part_is_live(world, entity, identity, &old_transform, preview) {
                    if let Ok(mut live) = world.get_entity_mut(entity) {
                        // Keep the derived entity and its render resources when a
                        // stable part merely moves. A changed extent needs a fresh
                        // collider shape, but remains tied to the same ECS entity.
                        if old_transform != *transform {
                            live.insert(*transform);
                            if !preview && old_transform.scale != transform.scale {
                                live.insert(Collider::cuboid(0.5, 0.5, 0.5));
                            }
                        }
                        live.insert(Visibility::Inherited);
                    }
                    retained.push(entity);
                    mutation.retained.push(*identity);
                } else {
                    world.despawn(entity);
                    retained.push(self.spawn_part(world, *identity, *transform, preview)?);
                    mutation.rebuilt.push(*identity);
                }
            } else {
                retained.push(self.spawn_part(world, *identity, *transform, preview)?);
                if previous_expected
                    .iter()
                    .any(|(old_identity, _)| old_identity == identity)
                {
                    mutation.rebuilt.push(*identity);
                } else {
                    mutation.spawned.push(*identity);
                }
            }
        }
        for (entity, (identity, _)) in previous {
            world.despawn(entity);
            mutation.removed.push(identity);
        }
        self.entities = retained;
        self.expected.clone_from(&parts);
        self.source_hash = Some(hash);
        self.preview = preview;
        self.last_mutation = mutation;
        Ok(())
    }

    fn resources_are_live(&self, world: &World) -> bool {
        match (&self.mesh, &self.material, &self.scene_material) {
            (None, None, None) => self.entities.is_empty(),
            (Some(mesh), _, Some(material)) => {
                world.resource::<Assets<Mesh>>().get(mesh).is_some()
                    && world
                        .get_resource::<Assets<VillageMaterial>>()
                        .is_some_and(|assets| assets.get(material).is_some())
            }
            (Some(mesh), Some(material), None) => {
                world.resource::<Assets<Mesh>>().get(mesh).is_some()
                    && world
                        .resource::<Assets<StandardMaterial>>()
                        .get(material)
                        .is_some()
            }
            _ => false,
        }
    }

    /// Bind the qualified opaque scene material to the accepted render parts.
    /// Derived geometry, part identities and physics remain on the same entities.
    pub fn use_scene_material(
        &mut self,
        world: &mut World,
        material: &VillageMaterial,
    ) -> Result<(), String> {
        if self.preview {
            return Err("A building preview cannot replace the accepted scene material".into());
        }
        let surface = self
            .material
            .as_ref()
            .and_then(|handle| world.resource::<Assets<StandardMaterial>>().get(handle))
            .ok_or("Accepted building surface absent")?;
        let material = neutral_scene_material(material, surface)?;
        crate::tidewater_material::SceneLighting::new(
            material.scene_lighting.sun_direction.truncate().to_array(),
            material.scene_lighting.sun_color.truncate().to_array(),
            material
                .scene_lighting
                .environment_sh
                .map(|value| value.truncate().to_array()),
        )?;
        if self.entities.is_empty() {
            return Ok(());
        }
        if !world.contains_resource::<Assets<VillageMaterial>>() {
            return Err("Building scene material store absent".into());
        }
        let mesh_handle = self.mesh.clone().ok_or("Building render mesh absent")?;
        if world.resource::<Assets<Mesh>>().get(&mesh_handle).is_none()
            || self
                .entities
                .iter()
                .any(|entity| world.get_entity(*entity).is_err())
        {
            return Err("Building derived render parts absent".into());
        }
        if self.scene_material.is_none() {
            let mut meshes = world.resource_mut::<Assets<Mesh>>();
            let mut mesh = meshes
                .get_mut(&mesh_handle)
                .ok_or("Building render mesh absent")?;
            let count = mesh.count_vertices();
            let tint = [1.0_f32; 3];
            mesh.insert_attribute(TINT, vec![tint; count]);
            // Neutral source kind never changes UV, geometry or surface parameters.
            mesh.insert_attribute(DATA, vec![[0.0_f32; 4]; count]);
        }
        let handle = {
            let mut assets = world.resource_mut::<Assets<VillageMaterial>>();
            match self.scene_material.as_ref() {
                Some(handle) if assets.get(handle).is_some() => {
                    if assets.get(handle) != Some(&material) {
                        *assets
                            .get_mut(handle)
                            .ok_or("Building scene material absent")? = material;
                    }
                    handle.clone()
                }
                _ => assets.add(material),
            }
        };
        for &entity in &self.entities {
            world
                .entity_mut(entity)
                .remove::<MeshMaterial3d<StandardMaterial>>()
                .insert((MeshMaterial3d(handle.clone()), OpaqueCaster));
        }
        self.scene_material = Some(handle);
        Ok(())
    }

    pub fn scene_material_is_current(&self, world: &World, lighting: &VillageMaterial) -> bool {
        let Some(surface) = self
            .material
            .as_ref()
            .and_then(|handle| world.resource::<Assets<StandardMaterial>>().get(handle))
        else {
            return false;
        };
        let Ok(expected) = neutral_scene_material(lighting, surface) else {
            return false;
        };
        self.scene_material.as_ref().is_some_and(|handle| {
            world
                .get_resource::<Assets<VillageMaterial>>()
                .is_some_and(|assets| assets.get(handle) == Some(&expected))
        })
    }

    #[must_use]
    pub const fn uses_standard_material(&self) -> bool {
        self.scene_material.is_none()
    }

    /// Return to the ordinary adapter only when no imported environment is expected.
    pub fn use_standard_material(&mut self, world: &mut World) -> Result<(), String> {
        if self.scene_material.is_none() {
            return Ok(());
        }
        let material = self
            .material
            .clone()
            .ok_or("Building standard surface absent")?;
        if world
            .resource::<Assets<StandardMaterial>>()
            .get(&material)
            .is_none()
        {
            return Err("Building standard surface absent".into());
        }
        if self
            .entities
            .iter()
            .any(|entity| world.get_entity(*entity).is_err())
        {
            return Err("Building derived render parts absent".into());
        }
        for &entity in &self.entities {
            world
                .entity_mut(entity)
                .remove::<(MeshMaterial3d<VillageMaterial>, OpaqueCaster)>()
                .insert(MeshMaterial3d(material.clone()));
        }
        if let Some(handle) = self.scene_material.take() {
            world
                .resource_mut::<Assets<VillageMaterial>>()
                .remove(handle.id());
        }
        Ok(())
    }

    fn material_is_live(&self, world: &World, entity: Entity) -> bool {
        if let Some(expected) = &self.scene_material {
            world
                .get::<MeshMaterial3d<VillageMaterial>>(entity)
                .is_some_and(|material| {
                    &material.0 == expected
                        && world
                            .get_resource::<Assets<VillageMaterial>>()
                            .is_some_and(|assets| {
                                assets.get(expected).is_some_and(|value| {
                                    value.kind == PROCEDURAL_SOLID_KIND
                                        && value.shared_scene_lighting
                                        && value.settings.flags.z == 0.0
                                })
                            })
                })
                && world
                    .get::<MeshMaterial3d<StandardMaterial>>(entity)
                    .is_none()
                && world.get::<OpaqueCaster>(entity).is_some()
        } else {
            world
                .get::<MeshMaterial3d<StandardMaterial>>(entity)
                .is_some_and(|material| {
                    self.material.as_ref() == Some(&material.0)
                        && world
                            .resource::<Assets<StandardMaterial>>()
                            .get(&material.0)
                            .is_some()
                })
                && world
                    .get::<MeshMaterial3d<VillageMaterial>>(entity)
                    .is_none()
        }
    }

    fn ensure_resources(&mut self, world: &mut World, preview: bool) {
        if self.mesh.is_none() {
            self.mesh = Some(
                world
                    .resource_mut::<Assets<Mesh>>()
                    .add(Cuboid::new(1.0, 1.0, 1.0)),
            );
        }
        if self.material.is_none() {
            self.material = Some(world.resource_mut::<Assets<StandardMaterial>>().add(
                StandardMaterial {
                    base_color: if preview {
                        Color::srgb(0.05, 0.65, 0.85)
                    } else {
                        Color::srgb(0.55, 0.48, 0.37)
                    },
                    perceptual_roughness: 0.9,
                    ..default()
                },
            ));
        }
    }

    fn spawn_part(
        &self,
        world: &mut World,
        identity: BuildingPartIdentity,
        transform: Transform,
        preview: bool,
    ) -> Result<Entity, String> {
        let mesh = self
            .mesh
            .clone()
            .ok_or_else(|| "Building projection mesh was not prepared".to_owned())?;
        let material = self
            .material
            .clone()
            .ok_or_else(|| "Building projection material was not prepared".to_owned())?;
        let mut entity = world.spawn((
            Name::new(format!(
                "Building {} / {:?}",
                identity.building, identity.role
            )),
            identity,
            Mesh3d(mesh),
            MeshMaterial3d(material),
            transform,
        ));
        if !preview {
            // Standalone static collider, as for accepted asset primitives.
            entity.insert(Collider::cuboid(0.5, 0.5, 0.5));
            if let Some(material) = &self.scene_material {
                entity
                    .remove::<MeshMaterial3d<StandardMaterial>>()
                    .insert((MeshMaterial3d(material.clone()), OpaqueCaster));
            }
        }
        Ok(entity.id())
    }

    fn part_is_live(
        &self,
        world: &World,
        entity: Entity,
        identity: &BuildingPartIdentity,
        transform: &Transform,
        preview: bool,
    ) -> bool {
        let render_components_are_live = world.get::<Mesh3d>(entity).is_some_and(|mesh| {
            self.mesh.as_ref() == Some(&mesh.0)
                && world.resource::<Assets<Mesh>>().get(&mesh.0).is_some()
                && (self.scene_material.is_none()
                    || world
                        .resource::<Assets<Mesh>>()
                        .get(&mesh.0)
                        .is_some_and(|value| {
                            value.attribute(TINT).is_some() && value.attribute(DATA).is_some()
                        }))
        }) && self.material_is_live(world, entity);
        let collider_is_live = preview
            || world
                .get::<Collider>(entity)
                .and_then(|collider| collider.raw.as_cuboid())
                .is_some_and(|collider| {
                    Vec3::new(
                        collider.half_extents.x,
                        collider.half_extents.y,
                        collider.half_extents.z,
                    )
                    .abs_diff_eq(transform.scale * 0.5, 1.0e-4)
                });
        world.get::<BuildingPartIdentity>(entity) == Some(identity)
            && world.get::<Transform>(entity) == Some(transform)
            && world.get::<ChildOf>(entity).is_none()
            && world.get::<RigidBody>(entity).is_none()
            && render_components_are_live
            && collider_is_live
    }

    pub fn clear(&mut self, world: &mut World) {
        let removed = self
            .expected
            .iter()
            .map(|(identity, _)| *identity)
            .collect();
        for entity in self.entities.drain(..) {
            world.despawn(entity);
        }
        if let Some(mesh) = self.mesh.take()
            && let Some(mut assets) = world.get_resource_mut::<Assets<Mesh>>()
        {
            assets.remove(mesh.id());
        }
        if let Some(material) = self.material.take()
            && let Some(mut assets) = world.get_resource_mut::<Assets<StandardMaterial>>()
        {
            assets.remove(material.id());
        }
        if let Some(material) = self.scene_material.take()
            && let Some(mut assets) = world.get_resource_mut::<Assets<VillageMaterial>>()
        {
            assets.remove(material.id());
        }
        self.source_hash = None;
        self.preview = false;
        self.expected.clear();
        self.last_mutation = BuildingProjectionMutation {
            removed,
            ..default()
        };
    }

    /// Presentation only: hiding accepted solids never removes their physics.
    pub fn set_visible(&self, world: &mut World, visible: bool) {
        for entity in &self.entities {
            if let Ok(mut entity) = world.get_entity_mut(*entity) {
                entity.insert(if visible {
                    Visibility::Inherited
                } else {
                    Visibility::Hidden
                });
            }
        }
    }

    #[must_use]
    pub fn part_count(&self) -> usize {
        self.entities.len()
    }

    #[must_use]
    pub const fn source_hash(&self) -> Option<ContentHash> {
        self.source_hash
    }

    /// Returns the reconciliation result produced by the last real replace or clear.
    #[must_use]
    pub const fn last_mutation(&self) -> &BuildingProjectionMutation {
        &self.last_mutation
    }

    /// Snapshot the current ECS/resource bindings without changing the world.
    #[must_use]
    pub fn diagnostic_parts(&self, world: &World) -> Vec<BuildingProjectionPartDiagnostic> {
        self.entities
            .iter()
            .copied()
            .zip(self.expected.iter().map(|(identity, _)| *identity))
            .map(|(entity, identity)| BuildingProjectionPartDiagnostic {
                building: identity.building,
                part: identity.part,
                role: format!("{:?}", identity.role),
                ecs_entity: entity.to_bits(),
                // Buildings are deliberately flat derived parts, so the part is
                // its own ECS root rather than a hidden authoring hierarchy.
                root_ecs_entity: entity.to_bits(),
                mesh_handle: world
                    .get::<Mesh3d>(entity)
                    .map(|mesh| format!("{:?}", mesh.0.id())),
                material_handle: if self.scene_material.is_some() {
                    world
                        .get::<MeshMaterial3d<VillageMaterial>>(entity)
                        .map(|material| format!("{:?}", material.0.id()))
                } else {
                    world
                        .get::<MeshMaterial3d<StandardMaterial>>(entity)
                        .map(|material| format!("{:?}", material.0.id()))
                },
                rapier_collider_handle: world
                    .get::<ProjectionRapierColliderHandle>(entity)
                    .map(|handle| format!("{:?}", handle.0)),
            })
            .collect()
    }

    /// Fresh source, render-component and Rapier registration barrier, not a GPU proof.
    pub fn ready(&self, world: &World, hash: ContentHash, building_count: usize) -> bool {
        self.ready_with_visibility(world, hash, building_count, true)
    }

    fn ready_with_visibility(
        &self,
        world: &World,
        hash: ContentHash,
        building_count: usize,
        require_visible: bool,
    ) -> bool {
        if self.preview
            || self.source_hash != Some(hash)
            || self.entities.len() != building_count.saturating_mul(8)
            || self.entities.len() != self.expected.len()
        {
            return false;
        }
        self.entities
            .iter()
            .zip(&self.expected)
            .all(|(&entity, (identity, transform))| {
                let half = transform.scale * 0.5;
                world.get::<BuildingPartIdentity>(entity) == Some(identity)
                    && world.get::<Transform>(entity) == Some(transform)
                    && world.get::<ChildOf>(entity).is_none()
                    && world.get::<RigidBody>(entity).is_none()
                    && (!require_visible
                        || world.get::<Visibility>(entity) != Some(&Visibility::Hidden))
                    && world.get::<Mesh3d>(entity).is_some_and(|m| {
                        self.mesh.as_ref() == Some(&m.0)
                            && world.resource::<Assets<Mesh>>().get(&m.0).is_some()
                            && (self.scene_material.is_none()
                                || world.resource::<Assets<Mesh>>().get(&m.0).is_some_and(
                                    |value| {
                                        value.attribute(TINT).is_some()
                                            && value.attribute(DATA).is_some()
                                    },
                                ))
                    })
                    && self.material_is_live(world, entity)
                    && world
                        .get::<Collider>(entity)
                        .and_then(|c| c.raw.as_cuboid())
                        .is_some_and(|c| {
                            Vec3::new(c.half_extents.x, c.half_extents.y, c.half_extents.z)
                                .abs_diff_eq(half, 1.0e-4)
                        })
                    && crate::physics::static_collider_ready(world, entity)
            })
    }
}

fn prepare(head: &ZoneAuthoringHead) -> Result<Vec<(BuildingPartIdentity, Transform)>, String> {
    head.validate()?;
    let mut result = Vec::new();
    for building in head.corrections.buildings.as_deref().unwrap_or_default() {
        let rotation = Quat::from_rotation_y(building.yaw_degrees.to_radians());
        let origin = Vec3::new(
            building.origin_m.x,
            building.origin_m.y,
            building.origin_m.z,
        );
        for part in RoomLayout::generate(building.effective_dimensions()?, building.id)?.parts {
            let min = Vec3::from_array(part.bounds.min);
            let max = Vec3::from_array(part.bounds.max);
            let centre = min.midpoint(max);
            result.push((
                BuildingPartIdentity {
                    building: building.id,
                    part: part.id,
                    role: part.role,
                },
                Transform {
                    translation: origin + rotation * centre,
                    rotation,
                    scale: max - min,
                },
            ));
        }
    }
    Ok(result)
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::CommandId;
    use forge_project::{
        authoring::new_terrain,
        building::RoomDimensions,
        terrain_edit::TerrainEditSession,
        zone_authoring::{
            GeneratorStamp,
            buildings::{AuthoredBuilding, BuildingEdit},
            migration::ZoneMigrationProposal,
            session::ZoneEditSession,
        },
    };

    pub(super) fn head() -> ZoneAuthoringHead {
        let terrain = new_terrain("building-engine", "zone", "Zone").unwrap();
        let hash = terrain.content_hash().unwrap();
        let legacy = TerrainEditSession::new(terrain).unwrap();
        let mut session = ZoneEditSession::from_migration(
            ZoneMigrationProposal::prepare(
                &legacy.archive_bytes().unwrap(),
                hash,
                GeneratorStamp {
                    name: "fixture".into(),
                    version: "1".into(),
                    inputs_hash: hash,
                },
                None,
            )
            .unwrap(),
        )
        .unwrap();
        let request = session
            .head()
            .prepare_building(
                CommandId::deterministic("insert"),
                BuildingEdit::Insert {
                    building: AuthoredBuilding {
                        id: EntityId::deterministic("room"),
                        name: "Room".into(),
                        generator_version: 1,
                        origin_m: forge_core::Vec3::new(10.0, 2.0, 20.0),
                        yaw_degrees: 90.0,
                        locked: false,
                        door_offset_override_m: Some(1.5),
                        dimensions: RoomDimensions {
                            width_m: 8.0,
                            depth_m: 6.0,
                            clear_height_m: 3.0,
                            wall_m: 0.25,
                            slab_m: 0.2,
                            door_width_m: 1.4,
                            door_height_m: 2.2,
                            door_offset_x_m: 0.0,
                        },
                    },
                },
            )
            .unwrap();
        session.execute(&request).unwrap();
        session.head().clone()
    }

    #[test]
    // Preserve one owned projection across geometry checks, rebind and cleanup.
    #[allow(clippy::too_many_lines)]
    fn source_material_preserves_geometry_colliders_and_standard_fixture_path() {
        use bevy_rapier3d::prelude::{NoUserData, RapierColliderHandle, RapierPhysicsPlugin};
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        app.init_resource::<Assets<Mesh>>();
        app.init_resource::<Assets<StandardMaterial>>();
        app.init_resource::<Assets<VillageMaterial>>();
        let head = head();
        let hash = head.content_hash().unwrap();
        let mut projection = BuildingProjection::default();
        projection.replace(app.world_mut(), &head, false).unwrap();
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(projection.uses_standard_material());
        assert!(
            projection.ready(app.world(), hash, 1),
            "a fixture with no resident source must retain ordinary readiness"
        );
        let entities = projection.entities.clone();
        let handles: Vec<_> = entities
            .iter()
            .map(|entity| app.world().get::<RapierColliderHandle>(*entity).unwrap().0)
            .collect();
        let transforms: Vec<_> = entities
            .iter()
            .map(|entity| *app.world().get::<Transform>(*entity).unwrap())
            .collect();
        let mesh = projection.mesh.clone().unwrap();
        let before = app
            .world()
            .resource::<Assets<Mesh>>()
            .get(&mesh)
            .unwrap()
            .clone();
        let lighting = crate::tidewater_material::tests::lighting();
        projection
            .use_scene_material(app.world_mut(), &lighting)
            .unwrap();
        assert_eq!(projection.entities, entities);
        assert_eq!(head.content_hash().unwrap(), hash);
        assert!(projection.scene_material_is_current(app.world(), &lighting));
        assert!(projection.ready(app.world(), hash, 1));
        let after = app.world().resource::<Assets<Mesh>>().get(&mesh).unwrap();
        for attribute in [
            Mesh::ATTRIBUTE_POSITION,
            Mesh::ATTRIBUTE_NORMAL,
            Mesh::ATTRIBUTE_UV_0,
        ] {
            assert_eq!(before.attribute(attribute), after.attribute(attribute));
        }
        assert_eq!(
            before
                .indices()
                .map(|values| values.iter().collect::<Vec<_>>()),
            after
                .indices()
                .map(|values| values.iter().collect::<Vec<_>>())
        );
        let cast = crate::tidewater_object_shadow::scene::opaque_parts(app.world()).unwrap();
        assert_eq!(cast.len(), 8);
        for ((&entity, handle), transform) in entities.iter().zip(&handles).zip(&transforms) {
            assert_eq!(app.world().get::<Transform>(entity), Some(transform));
            assert_eq!(
                app.world().get::<RapierColliderHandle>(entity).unwrap().0,
                *handle
            );
            assert!(
                cast.iter()
                    .any(|(caster, model)| *caster == entity && *model == transform.to_matrix())
            );
        }
        let owned = projection.scene_material.clone().unwrap();
        let mut updated = lighting.clone();
        updated.scene_lighting.sun_color.x += 0.25;
        assert!(!projection.scene_material_is_current(app.world(), &updated));
        projection
            .use_scene_material(app.world_mut(), &updated)
            .unwrap();
        assert_eq!(projection.scene_material.as_ref(), Some(&owned));
        assert!(projection.scene_material_is_current(app.world(), &updated));
        app.world_mut()
            .entity_mut(entities[0])
            .remove::<OpaqueCaster>();
        assert!(!projection.ready(app.world(), hash, 1));
        projection
            .use_scene_material(app.world_mut(), &updated)
            .unwrap();
        assert!(projection.ready(app.world(), hash, 1));
        let mut ghost = BuildingProjection::default();
        ghost.replace(app.world_mut(), &head, true).unwrap();
        assert!(
            ghost
                .entities
                .iter()
                .all(|entity| app.world().get::<OpaqueCaster>(*entity).is_none())
        );
        assert!(
            ghost
                .use_scene_material(app.world_mut(), &lighting)
                .is_err()
        );
        assert_eq!(
            crate::tidewater_object_shadow::scene::opaque_parts(app.world())
                .unwrap()
                .len(),
            8
        );
        ghost.clear(app.world_mut());
        projection.use_standard_material(app.world_mut()).unwrap();
        assert!(projection.uses_standard_material());
        assert!(projection.ready(app.world(), hash, 1));
        assert!(
            crate::tidewater_object_shadow::scene::opaque_parts(app.world())
                .unwrap()
                .is_empty()
        );
        assert!(app.world().resource::<Assets<VillageMaterial>>().is_empty());
        // A disappeared derived part must reject a material transition without
        // panicking or reporting the remaining parts as a ready building.
        projection
            .use_scene_material(
                app.world_mut(),
                &crate::tidewater_material::tests::lighting(),
            )
            .unwrap();
        app.world_mut().despawn(projection.entities[0]);
        assert!(projection.use_standard_material(app.world_mut()).is_err());
        assert!(!projection.ready(app.world(), hash, 1));
        projection.clear(app.world_mut());
        assert!(app.world().resource::<Assets<VillageMaterial>>().is_empty());
        assert!(app.world().resource::<Assets<Mesh>>().is_empty());
        assert!(
            app.world()
                .resource::<Assets<StandardMaterial>>()
                .is_empty()
        );
    }

    #[test]
    fn readiness_waits_for_backend_and_rejects_stale_or_hidden_parts() {
        use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        app.init_resource::<Assets<Mesh>>();
        app.init_resource::<Assets<StandardMaterial>>();
        let head = head();
        let hash = head.content_hash().unwrap();
        let mut projection = BuildingProjection::default();
        projection.replace(app.world_mut(), &head, false).unwrap();
        assert!(!projection.ready(app.world(), hash, 1));
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(projection.ready(app.world(), hash, 1));
        assert!(!projection.ready(app.world(), hash, 2));
        projection.set_visible(app.world_mut(), false);
        assert!(!projection.ready(app.world(), hash, 1));
        projection.set_visible(app.world_mut(), true);
        assert!(projection.ready(app.world(), hash, 1));
        let entity = projection.entities[0];
        app.world_mut()
            .get_mut::<Transform>(entity)
            .unwrap()
            .translation
            .x += 1.0;
        assert!(!projection.ready(app.world(), hash, 1));
        projection.clear(app.world_mut());
        assert!(!projection.ready(app.world(), hash, 1));
    }

    #[test]
    fn stable_parts_update_in_place_while_damaged_parts_rebuild() {
        use bevy_rapier3d::prelude::{NoUserData, RapierColliderHandle, RapierPhysicsPlugin};
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        app.init_resource::<Assets<Mesh>>();
        app.init_resource::<Assets<StandardMaterial>>();
        let mut head = head();
        let mut projection = BuildingProjection::default();
        projection.replace(app.world_mut(), &head, false).unwrap();
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert_eq!(projection.last_mutation().spawned.len(), 8);
        assert!(projection.last_mutation().retained.is_empty());
        let old_hash = head.content_hash().unwrap();
        assert!(projection.ready(app.world(), old_hash, 1));
        let entities = projection.entities.clone();
        let mesh = projection.mesh.clone();
        let handles: Vec<_> = entities
            .iter()
            .map(|&id| app.world().get::<RapierColliderHandle>(id).unwrap().0)
            .collect();
        // Rename and terrain elevation do not change absolute building geometry.
        head.corrections.name = Some("Renamed".into());
        head.corrections.base_elevation_m = Some(3.0);
        projection.set_visible(app.world_mut(), false);
        projection.replace(app.world_mut(), &head, false).unwrap();
        let new_hash = head.content_hash().unwrap();
        assert_ne!(new_hash, old_hash);
        assert_eq!(projection.entities, entities);
        assert_eq!(projection.mesh, mesh);
        assert_eq!(projection.last_mutation().retained.len(), 8);
        assert!(projection.last_mutation().spawned.is_empty());
        assert!(projection.last_mutation().rebuilt.is_empty());
        assert!(projection.last_mutation().removed.is_empty());
        for (&entity, handle) in entities.iter().zip(&handles) {
            assert_eq!(
                app.world().get::<RapierColliderHandle>(entity).unwrap().0,
                *handle
            );
        }
        assert!(projection.ready(app.world(), new_hash, 1));
        assert!(!projection.ready(app.world(), old_hash, 1));
        // A pose changes every derived transform, but not the building-part
        // identities, render resources, or Rapier collider handles.
        head.corrections.buildings.as_mut().unwrap()[0].origin_m.x += 2.0;
        let posed_hash = head.content_hash().unwrap();
        projection.replace(app.world_mut(), &head, false).unwrap();
        assert_eq!(projection.entities, entities);
        assert_eq!(projection.mesh, mesh);
        assert_eq!(projection.last_mutation().retained.len(), 8);
        assert!(projection.last_mutation().rebuilt.is_empty());
        assert!(!projection.ready(app.world(), posed_hash, 1));
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(projection.ready(app.world(), posed_hash, 1));
        for (&entity, handle) in entities.iter().zip(&handles) {
            assert_eq!(
                app.world().get::<RapierColliderHandle>(entity).unwrap().0,
                *handle
            );
        }
        // Equality of the recipe cannot hide a damaged live collider.
        app.world_mut().entity_mut(entities[0]).remove::<Collider>();
        projection.replace(app.world_mut(), &head, false).unwrap();
        assert!(app.world().get_entity(entities[0]).is_err());
        assert_eq!(projection.last_mutation().retained.len(), 7);
        assert_eq!(projection.last_mutation().rebuilt.len(), 1);
        assert!(projection.last_mutation().spawned.is_empty());
        assert!(projection.last_mutation().removed.is_empty());
        for (&entity, handle) in entities.iter().skip(1).zip(handles.iter().skip(1)) {
            assert_eq!(
                app.world().get::<RapierColliderHandle>(entity).unwrap().0,
                *handle
            );
        }
        assert!(!projection.ready(app.world(), posed_hash, 1));
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(projection.ready(app.world(), posed_hash, 1));
        let repaired = projection.entities.clone();
        head.corrections.buildings.as_mut().unwrap()[0].door_offset_override_m = Some(0.5);
        let changed_hash = head.content_hash().unwrap();
        projection.replace(app.world_mut(), &head, false).unwrap();
        assert_eq!(
            projection.entities, repaired,
            "a geometry change must retain stable building-part ECS identities"
        );
        assert!(!projection.ready(app.world(), posed_hash, 1));
        assert!(!projection.ready(app.world(), changed_hash, 1));
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(projection.ready(app.world(), changed_hash, 1));
        projection.clear(app.world_mut());
        assert!(app.world().resource::<Assets<Mesh>>().is_empty());
        assert!(
            app.world()
                .resource::<Assets<StandardMaterial>>()
                .is_empty()
        );
    }

    #[test]
    fn diagnostic_snapshot_is_read_only_and_stable_after_targeted_reuse() {
        use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        app.init_resource::<Assets<Mesh>>();
        app.init_resource::<Assets<StandardMaterial>>();
        let mut head = head();
        let mut projection = BuildingProjection::default();
        projection.replace(app.world_mut(), &head, false).unwrap();
        app.update();
        app.world_mut().run_schedule(FixedUpdate);

        let initial = projection.diagnostic_parts(app.world());
        let repeat = projection.diagnostic_parts(app.world());
        assert_eq!(initial, repeat);
        assert_eq!(initial.len(), 8);
        assert!(initial.iter().all(|part| {
            part.ecs_entity == part.root_ecs_entity
                && part.mesh_handle.is_some()
                && part.material_handle.is_some()
                && part.rapier_collider_handle.is_some()
        }));
        let entities = projection.entities.clone();
        let mesh_count = app.world().resource::<Assets<Mesh>>().len();
        let material_count = app.world().resource::<Assets<StandardMaterial>>().len();

        // A targeted pose mutation uses the existing roots and resources. The
        // mutation lists below come from replace(), not a later identity diff.
        head.corrections.buildings.as_mut().unwrap()[0].origin_m.z += 1.0;
        projection.replace(app.world_mut(), &head, false).unwrap();
        assert_eq!(projection.last_mutation().retained.len(), 8);
        assert!(projection.last_mutation().spawned.is_empty());
        assert!(projection.last_mutation().rebuilt.is_empty());
        assert!(projection.last_mutation().removed.is_empty());
        assert_eq!(projection.entities, entities);

        let after = projection.diagnostic_parts(app.world());
        assert_eq!(after, projection.diagnostic_parts(app.world()));
        assert_eq!(
            after.iter().map(|part| part.ecs_entity).collect::<Vec<_>>(),
            initial
                .iter()
                .map(|part| part.ecs_entity)
                .collect::<Vec<_>>(),
        );
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), mesh_count);
        assert_eq!(
            app.world().resource::<Assets<StandardMaterial>>().len(),
            material_count
        );
    }

    #[test]
    fn adding_or_removing_a_building_only_changes_its_own_parts() {
        use bevy_rapier3d::prelude::{NoUserData, RapierColliderHandle, RapierPhysicsPlugin};
        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        app.init_resource::<Assets<Mesh>>();
        app.init_resource::<Assets<StandardMaterial>>();
        let mut source = head();
        let mut projection = BuildingProjection::default();
        projection.replace(app.world_mut(), &source, false).unwrap();
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let original_hash = source.content_hash().unwrap();
        let original_entities = projection.entities.clone();
        let original_handles: Vec<_> = original_entities
            .iter()
            .map(|entity| app.world().get::<RapierColliderHandle>(*entity).unwrap().0)
            .collect();
        let mesh = projection.mesh.clone();
        let material = projection.material.clone();

        let mut added = source.corrections.buildings.as_ref().unwrap()[0].clone();
        added.id = EntityId::deterministic("second-room");
        let added_id = added.id;
        added.origin_m.x += 20.0;
        source.corrections.buildings.as_mut().unwrap().push(added);
        source
            .corrections
            .buildings
            .as_mut()
            .unwrap()
            .sort_by_key(|building| building.id);
        let added_hash = source.content_hash().unwrap();
        projection.replace(app.world_mut(), &source, false).unwrap();
        assert_eq!(projection.part_count(), 16);
        assert_eq!(projection.mesh, mesh);
        assert_eq!(projection.material, material);
        assert!(!projection.ready(app.world(), original_hash, 1));
        assert!(!projection.ready(app.world(), added_hash, 2));
        for (&entity, handle) in original_entities.iter().zip(&original_handles) {
            assert_eq!(
                app.world().get::<RapierColliderHandle>(entity).unwrap().0,
                *handle
            );
        }
        let added_entities = projection
            .entities
            .iter()
            .copied()
            .filter(|entity| !original_entities.contains(entity))
            .collect::<Vec<_>>();
        assert_eq!(added_entities.len(), 8);
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert!(projection.ready(app.world(), added_hash, 2));

        source
            .corrections
            .buildings
            .as_mut()
            .unwrap()
            .retain(|building| building.id != added_id);
        let restored_hash = source.content_hash().unwrap();
        projection.replace(app.world_mut(), &source, false).unwrap();
        assert_eq!(projection.part_count(), 8);
        for (&entity, handle) in original_entities.iter().zip(&original_handles) {
            assert_eq!(
                app.world().get::<RapierColliderHandle>(entity).unwrap().0,
                *handle
            );
        }
        assert!(
            added_entities
                .iter()
                .all(|entity| app.world().get_entity(*entity).is_err())
        );
        assert!(!projection.ready(app.world(), added_hash, 2));
        assert!(projection.ready(app.world(), restored_hash, 1));
    }

    #[test]
    fn projection_shares_geometry_and_releases_owned_resources() {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<StandardMaterial>>();
        let head = head();
        let expected = prepare(&head).unwrap();
        let mut projection = BuildingProjection::default();
        projection.replace(&mut world, &head, false).unwrap();
        assert_eq!(projection.part_count(), 8);
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 1);
        for (entity, (identity, transform)) in projection.entities.iter().zip(expected) {
            assert_eq!(world.get::<Transform>(*entity), Some(&transform));
            assert_eq!(
                world.get::<BuildingPartIdentity>(*entity).unwrap().part,
                identity.part
            );
            assert!(world.get::<Collider>(*entity).is_some());
            assert!(world.get::<RigidBody>(*entity).is_none());
        }
        let old = projection.entities.clone();
        projection.replace(&mut world, &head, true).unwrap();
        assert!(old.iter().all(|id| world.get_entity(*id).is_err()));
        assert!(
            projection
                .entities
                .iter()
                .all(|id| world.get::<Collider>(*id).is_none())
        );
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 1);
        projection.clear(&mut world);
        assert_eq!(projection.part_count(), 0);
        assert!(world.resource::<Assets<Mesh>>().is_empty());
        assert!(world.resource::<Assets<StandardMaterial>>().is_empty());
    }
}
