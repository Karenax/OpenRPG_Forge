//! Complete box/mesh routing for a source placement. Native observations remain
//! unchanged; each shape adapter owns only its projection beneath source nodes.
use crate::{
    source_collider_projection::{
        self, ColliderProjection, ContactPolicy, SourceColliderIdentity, validate_node,
    },
    source_colliders::{self, PreparedBox},
    source_mesh_collider_projection::{self, MeshColliderProjection, SourceMeshCooking},
    source_mesh_colliders::{self, MeshCookingPolicy, PreparedMeshCollider},
};
use bevy::prelude::{ChildOf, Entity, GlobalTransform, Mat4, Transform, Vec3, World};
use bevy_rapier3d::prelude::{
    Collider, ColliderDisabled, ColliderScale, ColliderView, CollisionGroups, ContactSkin,
    Friction, RapierColliderHandle, RapierContextEntityLink, Restitution, Sensor, SolverGroups,
};
use bevy_rapier3d::{parry::shape::SharedShape, plugin::context::RapierContextColliders};
use forge_assets::source_graph_glb::SourceGraphGlb;
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

#[cfg(test)]
use crate::source_graph_host::source_graph_test_fixture;

pub struct ColliderSceneProjection {
    boxes: ColliderProjection,
    meshes: MeshColliderProjection,
    expected: Vec<OwnedSourceCollider>,
}

/// Kind of source observation, without any claim about backend registration.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum OwnedSourceColliderKind {
    Box,
    Mesh,
    UnassignedMesh,
}

struct ExpectedMesh {
    convex: bool,
    cooking_options: u32,
    geometry_evidence: Option<String>,
}

/// One collider observation owned by this projection. Enabled and trigger are
/// the declared source states, not proof of a synchronized Rapier backend.
pub struct OwnedSourceCollider {
    pub entity: Entity,
    pub source_node: usize,
    pub parent: Entity,
    pub guid: String,
    pub file_id: String,
    pub kind: OwnedSourceColliderKind,
    pub expected_enabled: bool,
    pub expected_trigger: bool,
    local_transform: Transform,
    contact: Option<ContactPolicy>,
    mesh: Option<ExpectedMesh>,
    source_shape: Option<SharedShape>,
}

/// Fresh registration counts for owned source shapes. Disabled and unassigned
/// declarations are observations, never registered solid or sensor shapes.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub struct ColliderBackendObservation {
    pub active_solids: usize,
    pub active_sensors: usize,
    pub registered_solids: usize,
    pub registered_sensors: usize,
    /// Assigned shapes explicitly disabled at the source.
    pub disabled: usize,
    /// Disabled shapes whose backend registration is also disabled and current.
    pub disabled_synchronized: usize,
    /// Mesh declarations with no assigned geometry, including disabled ones.
    pub unassigned: usize,
    /// Unassigned observations with no collider in the admitted context.
    pub unassigned_clear: usize,
}

impl ColliderBackendObservation {
    #[must_use]
    pub fn is_ready(self) -> bool {
        self.active_solids + self.active_sensors > 0
            && self.active_solids == self.registered_solids
            && self.active_sensors == self.registered_sensors
            && self.disabled == self.disabled_synchronized
            && self.unassigned == self.unassigned_clear
    }
}

fn matches_contact(world: &World, entity: Entity, policy: Option<&ContactPolicy>) -> bool {
    match policy {
        Some(policy) => {
            world.get::<Friction>(entity) == Some(&policy.friction)
                && world.get::<Restitution>(entity) == Some(&policy.restitution)
                && world.get::<ContactSkin>(entity) == Some(&policy.contact_skin)
                && world.get::<CollisionGroups>(entity) == Some(&policy.collision_groups)
                && world.get::<SolverGroups>(entity) == Some(&policy.solver_groups)
        }
        None => {
            world.get::<Friction>(entity).is_none()
                && world.get::<Restitution>(entity).is_none()
                && world.get::<ContactSkin>(entity).is_none()
                && world.get::<CollisionGroups>(entity).is_none()
                && world.get::<SolverGroups>(entity).is_none()
        }
    }
}

fn matches_mesh(world: &World, expected: &OwnedSourceCollider) -> bool {
    match (
        &expected.mesh,
        world.get::<SourceMeshCooking>(expected.entity),
    ) {
        (None, None) => true,
        (Some(saved), Some(current)) => {
            saved.convex == current.source_convex
                && saved.cooking_options == current.source_cooking_options
                && saved.geometry_evidence == current.geometry_evidence
        }
        _ => false,
    }
}

fn matches_shape_type(world: &World, expected: &OwnedSourceCollider) -> bool {
    let collider = world.get::<Collider>(expected.entity);
    match (expected.kind, collider) {
        (OwnedSourceColliderKind::Box, Some(collider)) => {
            matches!(collider.as_unscaled_typed_shape(), ColliderView::Cuboid(_))
        }
        (OwnedSourceColliderKind::Mesh, Some(collider)) => {
            let Some(mesh) = &expected.mesh else {
                return false;
            };
            if mesh.convex {
                matches!(
                    collider.as_unscaled_typed_shape(),
                    ColliderView::ConvexPolyhedron(_)
                )
            } else {
                matches!(collider.as_unscaled_typed_shape(), ColliderView::TriMesh(_))
            }
        }
        (OwnedSourceColliderKind::UnassignedMesh, None) => true,
        _ => false,
    }
}

fn matches_source_shape(world: &World, expected: &OwnedSourceCollider) -> bool {
    let current = world
        .get::<Collider>(expected.entity)
        .map(Collider::as_unscaled_typed_shape);
    match (&expected.source_shape, current) {
        (None, None) => true,
        (Some(saved), Some(ColliderView::Cuboid(current))) => saved
            .as_cuboid()
            .is_some_and(|shape| std::ptr::eq(shape, current.raw)),
        (Some(saved), Some(ColliderView::TriMesh(current))) => saved
            .as_trimesh()
            .is_some_and(|shape| std::ptr::eq(shape, current.raw)),
        (Some(saved), Some(ColliderView::ConvexPolyhedron(current))) => saved
            .as_convex_polyhedron()
            .is_some_and(|shape| std::ptr::eq(shape, current.raw)),
        _ => false,
    }
}

fn backend_contact_matches(world: &World, expected: &OwnedSourceCollider) -> bool {
    let Some(policy) = &expected.contact else {
        return false;
    };
    let Some(handle) = world.get::<RapierColliderHandle>(expected.entity) else {
        return false;
    };
    let Some(link) = world.get::<RapierContextEntityLink>(expected.entity) else {
        return false;
    };
    world
        .get::<RapierContextColliders>(link.0)
        .and_then(|context| context.colliders.get(handle.0))
        .is_some_and(|backend| {
            backend.friction().to_bits() == policy.friction.coefficient.to_bits()
                && backend.friction_combine_rule() == policy.friction.combine_rule.into()
                && backend.restitution().to_bits() == policy.restitution.coefficient.to_bits()
                && backend.restitution_combine_rule() == policy.restitution.combine_rule.into()
                && backend.contact_skin().to_bits() == policy.contact_skin.0.to_bits()
                && backend.collision_groups() == policy.collision_groups.into()
                && backend.solver_groups() == policy.solver_groups.into()
        })
}

fn unit_scale(world: &World, expected: &OwnedSourceCollider) -> Result<(), String> {
    if world.get::<ColliderScale>(expected.entity).is_some() {
        return Err("source collider scale override is not admitted".into());
    }
    let global = crate::physics::current_global(world, expected.entity)
        .ok_or("source collider hierarchy transform is absent")?;
    let matrix = global.to_matrix();
    if !matrix.is_finite() || !matrix.determinant().is_finite() || matrix.determinant() == 0.0 {
        return Err("source collider hierarchy transform is invalid".into());
    }
    let transform = global.compute_transform();
    if !global
        .affine()
        .abs_diff_eq(GlobalTransform::from(transform).affine(), 1.0e-4)
        || bevy_rapier3d::geometry::get_snapped_scale(transform.scale) != Vec3::ONE
    {
        return Err("source collider requires an unscaled, unsheared hierarchy".into());
    }
    Ok(())
}

impl ColliderSceneProjection {
    #[must_use]
    pub fn observation_count(&self) -> usize {
        self.boxes.entities.len() + self.meshes.entities.len()
    }

    /// Inspect the identities and declared component state of this owned batch.
    /// This is not Rapier readiness, shape equivalence, a full hierarchy scan,
    /// or a proof that a scene contains no other colliders.
    pub fn observe_owned(&self, world: &World) -> Result<&[OwnedSourceCollider], String> {
        if self.boxes.root != self.meshes.root
            || world.get_entity(self.boxes.root).is_err()
            || !source_collider_projection::owns_batch(world, self.boxes.root)
            || !source_mesh_collider_projection::owns_batch(world, self.boxes.root)
            || self.expected.len() != self.observation_count()
        {
            return Err("source collider owned batch is incomplete".into());
        }
        let mut seen = BTreeSet::new();
        for expected in &self.expected {
            let entity = expected.entity;
            let identity = world
                .get::<SourceColliderIdentity>(entity)
                .ok_or("source collider owned identity absent")?;
            if !seen.insert(entity)
                || identity.placement_root != self.boxes.root
                || identity.guid != expected.guid
                || identity.file_id != expected.file_id
                || identity.policy_evidence.as_str()
                    != expected
                        .contact
                        .as_ref()
                        .map_or("", |policy| policy.evidence.as_str())
                || world.get::<ChildOf>(entity).map(ChildOf::parent) != Some(expected.parent)
                || world.get::<Transform>(entity) != Some(&expected.local_transform)
                || !matches_shape_type(world, expected)
                || !matches_source_shape(world, expected)
                || world.get::<ColliderDisabled>(entity).is_none() != expected.expected_enabled
                || world.get::<Sensor>(entity).is_some() != expected.expected_trigger
                || !matches_contact(world, entity, expected.contact.as_ref())
                || !matches_mesh(world, expected)
            {
                return Err("source collider owned observation changed".into());
            }
            validate_node(
                world,
                self.boxes.root,
                expected.source_node,
                expected.parent,
            )?;
        }
        Ok(&self.expected)
    }

    /// Inspect owned active shapes against the current static Rapier backend.
    /// This qualification slice only admits unit scale and does not assert
    /// source-engine equivalence, rendered pixels, or absence of foreign shapes.
    pub fn observe_backend(&self, world: &World) -> Result<ColliderBackendObservation, String> {
        let mut result = ColliderBackendObservation::default();
        let mut contexts = BTreeSet::new();
        let mut unassigned = Vec::new();
        for expected in self.observe_owned(world)? {
            if expected.kind == OwnedSourceColliderKind::UnassignedMesh {
                result.unassigned += 1;
                unassigned.push(expected.entity);
                continue;
            }
            if let Some(link) = world.get::<RapierContextEntityLink>(expected.entity) {
                contexts.insert(link.0);
            }
            if !expected.expected_enabled {
                result.disabled += 1;
            } else if expected.expected_trigger {
                result.active_sensors += 1;
            } else {
                result.active_solids += 1;
            }
            unit_scale(world, expected)?;
            let collider = world
                .get::<Collider>(expected.entity)
                .ok_or("source collider disappeared")?;
            // An old scale can still be awaiting the next Rapier sync. At
            // unit scale the captured shared allocation must be the live raw
            // shape, including if a different collider has the same type.
            if collider.scale() != Vec3::ONE {
                continue;
            }
            if expected
                .source_shape
                .as_ref()
                .is_none_or(|source| !std::ptr::eq(source.as_ref(), collider.raw.as_ref()))
            {
                return Err("source collider raw shape changed".into());
            }
            let ready = if !expected.expected_enabled {
                crate::physics::static_disabled_collider_synchronized(
                    world,
                    expected.entity,
                    expected.expected_trigger,
                )
            } else if expected.expected_trigger {
                crate::physics::static_sensor_ready(world, expected.entity)
            } else {
                crate::physics::static_collider_ready(world, expected.entity)
            } && backend_contact_matches(world, expected);
            if ready && !expected.expected_enabled {
                result.disabled_synchronized += 1;
            } else if ready && expected.expected_trigger {
                result.registered_sensors += 1;
            } else if ready {
                result.registered_solids += 1;
            }
        }
        if contexts.len() > 1 {
            return Err("source colliders reference different Rapier contexts".into());
        }
        if let Some(&context_id) = contexts.first()
            && let Some(context) = world.get::<RapierContextColliders>(context_id)
        {
            for entity in unassigned {
                let link_matches = world
                    .get::<RapierContextEntityLink>(entity)
                    .is_none_or(|link| link.0 == context_id);
                if link_matches
                    && world.get::<RapierColliderHandle>(entity).is_none()
                    && !context.entity2collider().contains_key(&entity)
                {
                    result.unassigned_clear += 1;
                }
            }
        }
        Ok(result)
    }

    pub fn clear(self, world: &mut World) {
        self.meshes.clear(world);
        self.boxes.clear(world);
    }
}

fn box_document(document: &Value) -> Result<Value, String> {
    let mut result = document.clone();
    for node in result["nodes"]
        .as_array_mut()
        .ok_or("source collider scene nodes missing")?
    {
        if let Some(observations) =
            node.pointer_mut("/extras/unity_source_node/native_collider_observations")
        {
            let observations = observations
                .as_array_mut()
                .ok_or("native collider observations must be an array")?;
            observations.retain(|o| o["identity"]["type"] != "UnityEngine.MeshCollider");
        }
    }
    Ok(result)
}

fn box_expectation(
    shape: &PreparedBox,
    entity: Entity,
    parent: Entity,
    policy: &ContactPolicy,
) -> OwnedSourceCollider {
    OwnedSourceCollider {
        entity,
        source_node: shape.node,
        parent,
        guid: shape.guid.clone(),
        file_id: shape.file_id.clone(),
        kind: OwnedSourceColliderKind::Box,
        expected_enabled: shape.enabled,
        expected_trigger: shape.is_trigger,
        local_transform: Transform::from_translation(shape.center),
        contact: Some(policy.clone()),
        mesh: None,
        source_shape: None,
    }
}

fn mesh_expectation(
    shape: &PreparedMeshCollider,
    entity: Entity,
    parent: Entity,
    policy: Option<&ContactPolicy>,
) -> OwnedSourceCollider {
    OwnedSourceCollider {
        entity,
        source_node: shape.node,
        parent,
        guid: shape.guid.clone(),
        file_id: shape.file_id.clone(),
        kind: if shape.collider.is_some() {
            OwnedSourceColliderKind::Mesh
        } else {
            OwnedSourceColliderKind::UnassignedMesh
        },
        expected_enabled: shape.enabled,
        expected_trigger: shape.is_trigger,
        local_transform: Transform::IDENTITY,
        contact: policy.cloned(),
        mesh: Some(ExpectedMesh {
            convex: shape.source_convex,
            cooking_options: shape.source_cooking_options,
            geometry_evidence: shape.geometry_evidence.clone(),
        }),
        source_shape: None,
    }
}

fn finish_projection(
    world: &mut World,
    prepared_boxes: &[PreparedBox],
    prepared_meshes: &[PreparedMeshCollider],
    boxes: ColliderProjection,
    meshes: MeshColliderProjection,
    nodes: &BTreeMap<usize, Entity>,
    contacts: &BTreeMap<(String, String), ContactPolicy>,
) -> Result<ColliderSceneProjection, String> {
    if boxes.entities.len() != prepared_boxes.len()
        || meshes.entities.len() != prepared_meshes.len()
    {
        meshes.clear(world);
        boxes.clear(world);
        return Err("source collider projection count differs from preparation".into());
    }
    let mut expected: Vec<_> = prepared_boxes
        .iter()
        .zip(&boxes.entities)
        .map(|(shape, &entity)| {
            let key = (shape.guid.clone(), shape.file_id.clone());
            box_expectation(shape, entity, nodes[&shape.node], &contacts[&key])
        })
        .chain(
            prepared_meshes
                .iter()
                .zip(&meshes.entities)
                .map(|(shape, &entity)| {
                    let key = (shape.guid.clone(), shape.file_id.clone());
                    mesh_expectation(shape, entity, nodes[&shape.node], contacts.get(&key))
                }),
        )
        .collect();
    // Attachment is synchronous and has not reached Rapier's scale system.
    // Cloning this shared handle does not copy the source shape or its bytes.
    for item in &mut expected {
        item.source_shape = world
            .get::<Collider>(item.entity)
            .map(|collider| collider.raw.clone());
    }
    let projection = ColliderSceneProjection {
        boxes,
        meshes,
        expected,
    };
    if let Err(error) = projection.observe_owned(world) {
        projection.clear(world);
        return Err(error);
    }
    Ok(projection)
}

/// Explicit dispatch of a complete scene. All geometry, policies, ownership and
/// supported shape types are checked before allocating any ECS children. No
/// asynchronous systems run between the two shape projections; rollback removes
/// only allocations made by this call if a projection nevertheless fails.
pub fn attach(
    world: &mut World,
    root: Entity,
    glb: &SourceGraphGlb<'_>,
    basis: Mat4,
    nodes: &BTreeMap<usize, Entity>,
    cooking: &BTreeMap<(String, String), MeshCookingPolicy>,
    contacts: &BTreeMap<(String, String), ContactPolicy>,
) -> Result<ColliderSceneProjection, String> {
    if !world.entities().contains(root)
        || source_collider_projection::owns_batch(world, root)
        || source_mesh_collider_projection::owns_batch(world, root)
    {
        return Err("source collider scene root missing or occupied".into());
    }
    // Mesh preparation refuses unknown collider types and duplicate native IDs,
    // including box IDs. Filtering below dispatches only that validated type.
    let prepared_meshes = source_mesh_colliders::prepare(glb, cooking)?;
    let box_doc = box_document(glb.document())?;
    let prepared_boxes = source_colliders::prepare(&box_doc, basis)?;
    let box_keys: BTreeSet<_> = prepared_boxes
        .iter()
        .map(|s| (s.guid.clone(), s.file_id.clone()))
        .collect();
    let mesh_keys: BTreeSet<_> = prepared_meshes
        .iter()
        .filter(|s| s.collider.is_some())
        .map(|s| (s.guid.clone(), s.file_id.clone()))
        .collect();
    if contacts.len() != box_keys.len() + mesh_keys.len() {
        return Err("contact policies differ from the complete source collider scene".into());
    }
    for key in box_keys.iter().chain(&mesh_keys) {
        contacts
            .get(key)
            .ok_or("source collider contact policy missing")?
            .validate()?;
    }
    for node in prepared_boxes
        .iter()
        .map(|s| s.node)
        .chain(prepared_meshes.iter().map(|s| s.node))
    {
        validate_node(
            world,
            root,
            node,
            *nodes
                .get(&node)
                .ok_or("source collider scene owner absent")?,
        )?;
    }
    let box_contacts = contacts
        .iter()
        .filter(|(id, _)| box_keys.contains(*id))
        .map(|(id, p)| (id.clone(), p.clone()))
        .collect();
    let mesh_contacts = contacts
        .iter()
        .filter(|(id, _)| mesh_keys.contains(*id))
        .map(|(id, p)| (id.clone(), p.clone()))
        .collect();
    let boxes =
        source_collider_projection::attach(world, root, &box_doc, basis, nodes, &box_contacts)?;
    match source_mesh_collider_projection::attach(world, root, glb, nodes, cooking, &mesh_contacts)
    {
        Ok(meshes) => finish_projection(
            world,
            &prepared_boxes,
            &prepared_meshes,
            boxes,
            meshes,
            nodes,
            contacts,
        ),
        Err(error) => {
            boxes.clear(world);
            Err(error)
        }
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    use crate::source_graph_scene::SourceGraphNode;
    use bevy::prelude::GlobalTransform;
    use bevy_rapier3d::prelude::Group;
    use serde_json::json;

    const GUID: &str = "00000000000000000000000000000001";

    fn box_observation(file_id: &str, enabled: bool, trigger: bool) -> Value {
        json!({
            "identity":{"guid":GUID,"file_id":file_id,"type":"UnityEngine.BoxCollider"},
            "serialized":[
                {"property_path":"m_GameObject","property_type":"ObjectReference",
                    "reference":{"guid":GUID,"file_id":"1"}},
                {"property_path":"m_Center","property_type":"Vector3","vector":[0,0,0]},
                {"property_path":"m_Size","property_type":"Vector3","vector":[2,2,2]},
                {"property_path":"m_Enabled","property_type":"Boolean","boolean":enabled},
                {"property_path":"m_IsTrigger","property_type":"Boolean","boolean":trigger}
            ]
        })
    }

    fn mesh_observation(file_id: &str, assigned: bool) -> Value {
        let mesh_reference = if assigned {
            json!({"guid":GUID,"file_id":"200","path":"fixture mesh","type":"UnityEngine.Mesh"})
        } else {
            json!({"guid":"","file_id":"","path":"","type":""})
        };
        json!({
            "identity":{"guid":GUID,"file_id":file_id,"type":"UnityEngine.MeshCollider"},
            "serialized":[
                {"property_path":"m_GameObject","property_type":"ObjectReference",
                    "reference":{"guid":GUID,"file_id":"1"}},
                {"property_path":"m_Enabled","property_type":"Boolean","boolean":assigned},
                {"property_path":"m_IsTrigger","property_type":"Boolean","boolean":!assigned},
                {"property_path":"m_Convex","property_type":"Boolean","boolean":false},
                {"property_path":"m_CookingOptions","property_type":"Integer","number":0},
                {"property_path":"m_Mesh","property_type":"ObjectReference",
                    "reference":mesh_reference}
            ]
        })
    }

    fn carrier_with_observations() -> Vec<u8> {
        let original = source_graph_test_fixture::fixture().unwrap();
        let json_len =
            usize::try_from(u32::from_le_bytes(original[12..16].try_into().unwrap())).unwrap();
        let mut document: Value = serde_json::from_slice(&original[20..20 + json_len]).unwrap();
        let mut binary = original[28 + json_len..].to_vec();
        let position_hash = forge_core::bytes_sha256(&binary[..36]);
        let indices: Vec<u8> = [0_u32, 1, 2]
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect();
        let index_hash = forge_core::bytes_sha256(&indices);
        let index_view = document["bufferViews"].as_array().unwrap().len();
        document["bufferViews"].as_array_mut().unwrap().push(json!({
            "buffer":0,"byteOffset":binary.len(),"byteLength":indices.len()
        }));
        binary.extend(&indices);
        document["buffers"][0]["byteLength"] = json!(binary.len());
        document["extras"]["preserved_collider_buffers"] = json!([{
            "node_path":"fixture",
            "identity":{"guid":GUID,"file_id":"200","type":"UnityEngine.Mesh"},
            "attributes":{"POSITION":{"buffer_view":0,"sha256":position_hash}},
            "primitives":[{"indices":3,"triangles":1,"source_submesh":{"topology":"Triangles"}}],
            "index_buffers":[{"buffer_view":index_view,"sha256":index_hash}]
        }]);
        let source = &mut document["nodes"][0]["extras"]["unity_source_node"];
        source["gameobject"] = json!({"guid":GUID,"file_id":"1"});
        source["native_collider_observations"] = json!([
            box_observation("10", true, false),
            box_observation("11", true, true),
            box_observation("12", false, false),
            mesh_observation("13", false),
            mesh_observation("14", true)
        ]);
        let mut json = serde_json::to_vec(&document).unwrap();
        json.resize(json.len().next_multiple_of(4), b' ');
        let mut result = b"glTF".to_vec();
        result.extend(2_u32.to_le_bytes());
        result.extend(
            u32::try_from(28 + json.len() + binary.len())
                .unwrap()
                .to_le_bytes(),
        );
        result.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
        result.extend(b"JSON");
        result.extend(json);
        result.extend(u32::try_from(binary.len()).unwrap().to_le_bytes());
        result.extend(b"BIN\0");
        result.extend(binary);
        result
    }

    fn policy() -> ContactPolicy {
        ContactPolicy {
            friction: Friction::coefficient(0.5),
            restitution: Restitution::coefficient(0.25),
            contact_skin: ContactSkin(0.01),
            collision_groups: CollisionGroups::new(Group::GROUP_1, Group::GROUP_1 | Group::GROUP_2),
            solver_groups: SolverGroups::new(Group::GROUP_1, Group::GROUP_1),
            evidence: "fixture contact policy".into(),
        }
    }

    pub(crate) fn attach_fixture(world: &mut World) -> (Entity, Entity, ColliderSceneProjection) {
        let bytes = carrier_with_observations();
        let glb = forge_assets::source_graph_glb::read_source_graph_glb(&bytes)
            .unwrap()
            .unwrap();
        let root = world
            .spawn((Transform::IDENTITY, GlobalTransform::IDENTITY))
            .id();
        let node = world
            .spawn((
                Transform::IDENTITY,
                GlobalTransform::IDENTITY,
                SourceGraphNode {
                    source_node: 0,
                    source_mesh: Some(0),
                },
                ChildOf(root),
            ))
            .id();
        let contacts = ["10", "11", "12", "14"]
            .into_iter()
            .map(|file_id| ((GUID.to_owned(), file_id.to_owned()), policy()))
            .collect();
        let cooking = BTreeMap::from([(
            (GUID.to_owned(), "14".to_owned()),
            MeshCookingPolicy {
                mesh_guid: GUID.to_owned(),
                mesh_file_id: "200".to_owned(),
                source_cooking_options: 0,
                evidence: "fixture source triangles".into(),
                shape: crate::source_mesh_colliders::MeshShapeInput::SourceTriangles,
            },
        )]);
        let projection = attach(
            world,
            root,
            &glb,
            Mat4::IDENTITY,
            &BTreeMap::from([(0, node)]),
            &cooking,
            &contacts,
        )
        .unwrap();
        (root, node, projection)
    }

    fn attached_fixture() -> (World, Entity, Entity, ColliderSceneProjection) {
        let mut world = World::new();
        let (root, node, projection) = attach_fixture(&mut world);
        (world, root, node, projection)
    }

    #[test]
    fn owned_observation_keeps_solid_sensor_disabled_and_unassigned_mesh_distinct() {
        let (world, root, node, projection) = attached_fixture();
        let observed = projection.observe_owned(&world).unwrap();
        assert_eq!(observed.len(), 5);
        assert!(
            observed
                .iter()
                .all(|item| item.parent == node && item.guid == GUID)
        );
        let state = |id: &str| {
            let item = observed.iter().find(|item| item.file_id == id).unwrap();
            (item.kind, item.expected_enabled, item.expected_trigger)
        };
        assert_eq!(state("10"), (OwnedSourceColliderKind::Box, true, false));
        assert_eq!(state("11"), (OwnedSourceColliderKind::Box, true, true));
        assert_eq!(state("12"), (OwnedSourceColliderKind::Box, false, false));
        assert_eq!(
            state("13"),
            (OwnedSourceColliderKind::UnassignedMesh, false, true)
        );
        assert_eq!(state("14"), (OwnedSourceColliderKind::Mesh, true, false));
        assert!(world.get_entity(root).is_ok());
    }

    fn backend_fixture() -> (bevy::prelude::App, Entity, ColliderSceneProjection) {
        use bevy::prelude::{App, MinimalPlugins, TransformPlugin};
        use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};

        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        let (root, _, projection) = attach_fixture(app.world_mut());
        (app, root, projection)
    }

    fn sync_backend(app: &mut bevy::prelude::App) {
        app.update();
        app.world_mut().run_schedule(bevy::prelude::FixedUpdate);
    }

    #[test]
    fn backend_observation_counts_only_synchronized_active_solids_and_sensors() {
        let (mut app, _, projection) = backend_fixture();
        let before = projection.observe_backend(app.world()).unwrap();
        assert_eq!((before.active_solids, before.active_sensors), (2, 1));
        assert_eq!(
            (before.registered_solids, before.registered_sensors),
            (0, 0)
        );
        assert_eq!((before.disabled, before.unassigned), (1, 1));
        assert_eq!(
            (before.disabled_synchronized, before.unassigned_clear),
            (0, 0)
        );
        assert!(!before.is_ready());
        sync_backend(&mut app);
        let ready = projection.observe_backend(app.world()).unwrap();
        assert_eq!((ready.registered_solids, ready.registered_sensors), (2, 1));
        assert_eq!((ready.disabled, ready.unassigned), (1, 1));
        assert_eq!(
            (ready.disabled_synchronized, ready.unassigned_clear),
            (1, 1)
        );
        assert!(ready.is_ready());

        let solid = projection
            .observe_owned(app.world())
            .unwrap()
            .iter()
            .find(|item| item.file_id == "10")
            .unwrap()
            .entity;
        let handle = app.world().get::<RapierColliderHandle>(solid).unwrap().0;
        let context = app.world().get::<RapierContextEntityLink>(solid).unwrap().0;
        app.world_mut()
            .get_mut::<RapierContextColliders>(context)
            .unwrap()
            .colliders
            .get_mut(handle)
            .unwrap()
            .set_friction(0.9);
        let stale = projection.observe_backend(app.world()).unwrap();
        assert_eq!((stale.registered_solids, stale.registered_sensors), (1, 1));
        assert!(!stale.is_ready());
        app.world_mut()
            .get_mut::<Friction>(solid)
            .unwrap()
            .coefficient = 0.5;
        sync_backend(&mut app);
        assert!(projection.observe_backend(app.world()).unwrap().is_ready());
    }

    #[test]
    fn disabled_and_unassigned_shapes_never_become_active_readiness() {
        let (mut app, _, projection) = backend_fixture();
        sync_backend(&mut app);
        let owned = projection.observe_owned(app.world()).unwrap();
        let disabled = owned
            .iter()
            .find(|item| item.file_id == "12")
            .unwrap()
            .entity;
        let empty = owned
            .iter()
            .find(|item| item.file_id == "13")
            .unwrap()
            .entity;
        let handle = app.world().get::<RapierColliderHandle>(disabled).unwrap().0;
        let context = app
            .world()
            .get::<RapierContextEntityLink>(disabled)
            .unwrap()
            .0;
        app.world_mut()
            .get_mut::<RapierContextColliders>(context)
            .unwrap()
            .colliders
            .get_mut(handle)
            .unwrap()
            .set_enabled(true);
        let stale = projection.observe_backend(app.world()).unwrap();
        assert_eq!(stale.disabled_synchronized, 0);
        assert_eq!((stale.registered_solids, stale.registered_sensors), (2, 1));
        assert!(!stale.is_ready());
        app.world_mut()
            .get_mut::<RapierContextColliders>(context)
            .unwrap()
            .colliders
            .get_mut(handle)
            .unwrap()
            .set_enabled(false);
        app.world_mut()
            .entity_mut(empty)
            .insert(RapierColliderHandle(handle));
        let stray = projection.observe_backend(app.world()).unwrap();
        assert_eq!(stray.unassigned_clear, 0);
        assert!(!stray.is_ready());
        app.world_mut()
            .entity_mut(empty)
            .remove::<RapierColliderHandle>();
        assert!(projection.observe_backend(app.world()).unwrap().is_ready());
    }

    #[test]
    fn backend_observation_refuses_nonunit_or_replaced_shapes() {
        for fault in 0..4 {
            let (mut app, root, projection) = backend_fixture();
            sync_backend(&mut app);
            assert!(projection.observe_backend(app.world()).unwrap().is_ready());
            let solid = projection
                .observe_owned(app.world())
                .unwrap()
                .iter()
                .find(|item| item.file_id == "10")
                .unwrap()
                .entity;
            match fault {
                0 => app.world_mut().get_mut::<Transform>(root).unwrap().scale = Vec3::splat(2.0),
                1 => {
                    app.world_mut()
                        .entity_mut(solid)
                        .insert(ColliderScale::Absolute(Vec3::ONE));
                }
                2 => {
                    app.world_mut().get_mut::<Collider>(solid).unwrap().raw =
                        Collider::cuboid(2.0, 2.0, 2.0).raw;
                }
                _ => {
                    app.world_mut()
                        .entity_mut(solid)
                        .insert(Collider::cuboid(2.0, 2.0, 2.0));
                }
            }
            assert!(
                projection.observe_backend(app.world()).is_err(),
                "fault {fault}"
            );
        }
    }

    #[test]
    fn owned_observation_refuses_changed_or_missing_components() {
        for change in 0..11 {
            let (mut world, root, node, mut projection) = attached_fixture();
            let observed = projection.observe_owned(&world).unwrap();
            let entity = |id: &str| {
                observed
                    .iter()
                    .find(|item| item.file_id == id)
                    .unwrap()
                    .entity
            };
            let solid = entity("10");
            let disabled = entity("12");
            let empty = entity("13");
            let mesh = entity("14");
            let foreign = world.spawn_empty().id();
            match change {
                0 => {
                    world.entity_mut(solid).remove::<SourceColliderIdentity>();
                }
                1 => {
                    world
                        .get_mut::<SourceColliderIdentity>(solid)
                        .unwrap()
                        .placement_root = foreign;
                }
                2 => {
                    world.entity_mut(solid).insert(ChildOf(foreign));
                }
                3 => {
                    world.entity_mut(solid).remove::<Collider>();
                }
                4 => {
                    world.entity_mut(solid).insert(Sensor);
                }
                5 => {
                    world.entity_mut(disabled).remove::<ColliderDisabled>();
                }
                6 => {
                    world.get_mut::<ContactSkin>(solid).unwrap().0 = 0.02;
                }
                7 => {
                    world.entity_mut(empty).insert(Collider::ball(1.0));
                }
                8 => {
                    projection.expected[1].entity = solid;
                }
                9 => {
                    world.entity_mut(solid).insert(Collider::ball(1.0));
                }
                _ => {
                    world.entity_mut(mesh).insert(Collider::ball(1.0));
                }
            }
            assert!(projection.observe_owned(&world).is_err(), "change {change}");
            projection.clear(&mut world);
            assert!(world.get_entity(root).is_ok());
            assert!(world.get_entity(node).is_ok());
            assert!(world.get_entity(foreign).is_ok());
            if change == 1 {
                // A record whose ownership was replaced is not ours to despawn.
                assert!(world.get_entity(solid).is_ok());
            }
        }
    }

    #[test]
    fn owned_observation_is_not_a_full_hierarchy_absence_proof() {
        let (mut world, root, node, projection) = attached_fixture();
        let foreign = world.spawn((Collider::ball(1.0), ChildOf(node))).id();
        assert_eq!(projection.observe_owned(&world).unwrap().len(), 5);
        projection.clear(&mut world);
        assert!(world.get_entity(foreign).is_ok());
        assert!(world.get_entity(root).is_ok());
        assert!(world.get_entity(node).is_ok());
    }

    #[test]
    fn empty_owned_batch_does_not_assert_that_other_colliders_are_absent() {
        let bytes = source_graph_test_fixture::fixture().unwrap();
        let glb = forge_assets::source_graph_glb::read_source_graph_glb(&bytes)
            .unwrap()
            .unwrap();
        let mut world = World::new();
        let root = world.spawn_empty().id();
        let foreign = world.spawn((Collider::ball(1.0), ChildOf(root))).id();
        let projection = attach(
            &mut world,
            root,
            &glb,
            Mat4::IDENTITY,
            &BTreeMap::new(),
            &BTreeMap::new(),
            &BTreeMap::new(),
        )
        .unwrap();
        assert!(projection.observe_owned(&world).unwrap().is_empty());
        projection.clear(&mut world);
        assert!(world.get_entity(root).is_ok());
        assert!(world.get_entity(foreign).is_ok());
    }
}
